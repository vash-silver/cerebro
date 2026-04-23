using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using MarvelHeroesComporator.NetworkSniffer;

namespace MarvelHeroes.DpsMeter.Services;

/// <summary>
/// Glue between the passive network sniffer, the <see cref="DpsMeter"/> aggregator, and the on-screen
/// <see cref="DpsOverlayWindow"/>.  Single entry point for the host app: call <see cref="Start"/> once
/// (after the sniffer is running) and a floating DPS number appears; <see cref="Stop"/> hides and
/// tears down.
/// </summary>
/// <remarks>
/// Lifetime model:
/// <list type="bullet">
///   <item><c>DpsMeter</c> is lazily constructed on Start so it can hook the sniffer's events, and
///         explicitly disposed on Stop to unsubscribe.</item>
///   <item><c>DpsOverlayWindow</c> lives on the UI thread; we use the provided dispatcher to
///         hop between the sniffer's capture thread (where DpsChanged fires) and WPF.</item>
///   <item>A small DispatcherTimer ticks at 4 Hz independent of incoming damage so the number
///         naturally decays to 0 when combat stops — without this, an idle meter would keep
///         showing the last burst's DPS until the next hit.</item>
/// </list>
/// </remarks>
public sealed class DpsOverlayPresenter : IDisposable
{
    private readonly MhMissionSniffer _sniffer;
    private readonly Dispatcher _uiDispatcher;

    /// <summary>Path to the log file we mirror meter diagnostics into. Same directory pattern used
    /// by the other diagnostic logs in this project so support dumps grab them together.</summary>
    private static readonly string DiagnosticLogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MarvelHeroesComporator", "dps-meter.log");

    private DpsMeter? _meter;
    private DpsOverlayWindow? _window;
    private DispatcherTimer? _decayTimer;
    private bool _lastVisibilityDecision = true;

    // Heartbeat state for the 5-second PowerResult stats line.  Compared against the snapshot
    // produced by MhMissionSniffer.PowerResultStats so we only log when the numbers move
    // — avoids drowning the log file in "Total=0, NoSubscriber=0" repeats when the user is
    // standing in a town with no active combat on screen.
    private DateTime _lastStatsLogUtc = DateTime.MinValue;
    private (int Total, int NoSubscriber, int ParseFailures) _lastStatsSnapshot;

    public DpsOverlayPresenter(MhMissionSniffer sniffer, Dispatcher uiDispatcher)
    {
        _sniffer = sniffer ?? throw new ArgumentNullException(nameof(sniffer));
        _uiDispatcher = uiDispatcher ?? throw new ArgumentNullException(nameof(uiDispatcher));
    }

    public bool IsRunning => _meter != null;

    /// <summary>Optional predicate polled at the decay-tick rate (~4 Hz). When it returns
    /// <c>false</c>, the overlay hides itself (<see cref="Visibility.Collapsed"/>); when it
    /// returns <c>true</c> (or the predicate is <c>null</c>), the overlay is shown. Intended
    /// for "hide while game is not foreground" — the host passes a delegate that consults
    /// <see cref="GameWindowLocator"/> plus whatever settings toggle the user exposed.
    ///
    /// Runs on the UI dispatcher so it's safe to touch WPF directly inside the delegate if
    /// needed; keep it fast (single Win32 call) because it fires four times per second forever.
    /// Setting this after <see cref="Start"/> is allowed — the next tick picks it up.</summary>
    public Func<bool>? ShouldBeVisible { get; set; }

    public void Start()
    {
        if (IsRunning) return;

        _meter = new DpsMeter(_sniffer)
        {
            Diagnostic = AppendLog,
        };
        _meter.DpsChanged += OnDpsChanged;

        // Hook the sniffer's own diagnostic stream into the same log file. Previously this sink
        // was unused, so verbose per-message logs added for nickname-resolution debugging
        // (ParseModifyCommunityMember, etc.) never reached disk.  Chain any pre-existing
        // handler a host app may have installed before Start.
        {
            var prior = _sniffer.Diagnostic;
            _sniffer.Diagnostic = prior == null
                ? AppendLog
                : msg => { prior(msg); AppendLog(msg); };
        }

        // Window creation has to happen on the UI thread. We Invoke (not BeginInvoke) so that
        // callers can assume the overlay is visible on return — simpler invariant for tests.
        _uiDispatcher.Invoke(() =>
        {
            _window = new DpsOverlayWindow();
            // Wire the right-click "Boss DPS only" toggle: window fires the event with the new
            // checkbox state, we forward it to the meter.  The setter on DpsMeter.BossOnlyMode
            // already clears the sliding windows + emits a diagnostic log line, so this side
            // stays a one-liner.  No need to mirror back into the window (the menu item's
            // IsChecked is already the source of truth via IsCheckable).
            _window.BossOnlyToggled += (enabled) =>
            {
                if (_meter != null) _meter.BossOnlyMode = enabled;
            };
            _window.ShowWithoutActivating();

            // Decay timer: poll CurrentDps at 4 Hz so the overlay fades back to "—" within a
            // second of combat ending (no incoming DamageDealt events means no DpsChanged
            // firings, so without this tick the last burst number would stick forever).
            _decayTimer = new DispatcherTimer(
                TimeSpan.FromMilliseconds(250),
                DispatcherPriority.Background,
                OnDecayTick,
                _uiDispatcher);
            _decayTimer.Start();
        });

        AppendLog($"DpsOverlayPresenter started (sniffer running={_sniffer.IsRunning})");
    }

    public void Stop()
    {
        if (!IsRunning) return;

        _uiDispatcher.Invoke(() =>
        {
            _decayTimer?.Stop();
            _decayTimer = null;
            try { _window?.Close(); } catch { }
            _window = null;
        });

        if (_meter != null)
        {
            _meter.DpsChanged -= OnDpsChanged;
            // Force-flush any pending dbId/nick/hero learnings before tearing down — debounce
            // might otherwise swallow the last mutations on a short session.
            try { _meter.FlushPlayerIndexNow(); } catch { }
            _meter.Dispose();
            _meter = null;
        }
        AppendLog("DpsOverlayPresenter stopped");
    }

    private void OnDpsChanged(object? sender, EventArgs e)
    {
        if (_meter is null) return;
        // Snapshot meter values here (on capture thread) so the UI update reflects a consistent
        // view even if more events fire before the dispatcher runs the lambda.
        double dps = _meter.CurrentDps;
        long total = _meter.CurrentOwnerTotal60s;
        ulong owner = _meter.LikelySelfOwnerId;
        uint maxHit = _meter.MaxSingleHit;
        string heroName = _meter.CurrentHeroDisplayName;
        var top5 = _meter.GetTopHeroesBy60sShare(5);

        _window?.UpdateDps(dps, total, owner, maxHit, heroName, top5);
    }

    private void OnDecayTick(object? sender, EventArgs e)
    {
        // Push wall-clock time into the meter so stale queue entries get evicted and CurrentDps
        // naturally falls to zero during idle periods. Without this call the meter would be
        // frame-locked to incoming DamageDealt events — perfectly correct during combat but
        // producing a "frozen last value" when combat ends (the original v1 bug).
        //
        // Tick() raises DpsChanged only when the number actually moves, so this is cheap at 4 Hz
        // and the UI sees updates just when it needs them.
        if (_meter is null || _window is null) return;
        _meter.Tick(DateTime.UtcNow);
        // Also poke the player-index persistence on each tick. Cheap no-op when the dirty flag
        // isn't set; when it is, this is our backstop in case the OnCommunityMemberUpdated /
        // OnEntityCreated call paths were skipped due to the debounce window.
        _meter.FlushPlayerIndexIfDirty();
        _window.UpdateDps(
            _meter.CurrentDps,
            _meter.CurrentOwnerTotal60s,
            _meter.LikelySelfOwnerId,
            _meter.MaxSingleHit,
            _meter.CurrentHeroDisplayName,
            _meter.GetTopHeroesBy60sShare(5));

        // ── Visibility gating (e.g. hide while game is not in foreground) ──────────────────
        // Polled here instead of hooking Win32 WinEvents because:
        //   (a) GetForegroundWindow + Process.GetProcessById is sub-millisecond;
        //   (b) piggybacking on an existing 4 Hz timer avoids a second wakeable source;
        //   (c) hook-based approaches need to run on a dispatcher pump anyway and can miss
        //       focus changes that happen during explorer transitions.
        // Hysteresis (`_lastVisibilityDecision`) prevents repeatedly calling Hide()/Show() on
        // every tick — the WPF hit-test / render cost is tiny but the diagnostic log would
        // otherwise flood with transitions that don't actually change anything.
        bool shouldShow = ShouldBeVisible?.Invoke() ?? true;
        if (shouldShow != _lastVisibilityDecision)
        {
            _lastVisibilityDecision = shouldShow;
            _window.Visibility = shouldShow ? Visibility.Visible : Visibility.Collapsed;
        }

        // Every ~5s, surface the sniffer's PowerResult counters so the log shows whether
        // server→client damage packets are actually reaching ParsePowerResult.  This is the
        // single best signal for triaging "DPS stays at 0":
        //   Total unchanged       → no NetMessagePowerResult on wire (sniffer / route issue)
        //   Total↑, NoSubscriber↑ → DpsMeter didn't subscribe (lifetime bug)
        //   Total↑, ParseFail↑    → archive schema drift (hex dump in early verbose logs)
        //   Total↑, none of above → packets arrive + parse OK → problem is in DpsMeter gate
        var nowUtc = DateTime.UtcNow;
        if ((nowUtc - _lastStatsLogUtc).TotalSeconds >= 5.0)
        {
            _lastStatsLogUtc = nowUtc;
            var snap = _sniffer.PowerResultStats;
            if (snap != _lastStatsSnapshot)
            {
                _lastStatsSnapshot = snap;
                AppendLog($"PowerResultStats: Total={snap.Total} NoSubscriber={snap.NoSubscriber} ParseFailures={snap.ParseFailures}");
            }
        }
    }

    private static void AppendLog(string line)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DiagnosticLogPath)!);
            File.AppendAllText(DiagnosticLogPath, $"[{DateTime.UtcNow:HH:mm:ss.fff}] {line}{Environment.NewLine}");
        }
        catch { /* log I/O errors swallowed — don't let logging crash the presenter */ }
    }

    public void Dispose() => Stop();
}
