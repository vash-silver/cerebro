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

    // Heartbeat: log PowerResult counters every 5s so "Total frozen" vs "Total climbing" is
    // obvious even when the numeric snapshot doesn't change (previous version only logged on
    // delta, which hid "sniffer alive but idle" sessions).
    private DateTime _lastStatsLogUtc = DateTime.MinValue;

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

        bool initialBossOnly = false;
        // Window creation has to happen on the UI thread. We Invoke (not BeginInvoke) so that
        // callers can assume the overlay is visible on return — simpler invariant for tests.
        _uiDispatcher.Invoke(() =>
        {
            _window = new DpsOverlayWindow();
            initialBossOnly = _window.InitialBossOnlyPreference;
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

        _meter.BossOnlyMode = initialBossOnly;

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
        long total60s = _meter.CurrentOwnerTotal60s; // ALWAYS the 60s rolling total — the big
                                                     // DPS number's fallback math depends on it.
        ulong owner = _meter.LikelySelfOwnerId;
        uint maxHit = _meter.MaxSingleHit;
        string heroName = _meter.CurrentHeroDisplayName;
        bool bossOnly = _meter.BossOnlyMode;
        var encounter = _meter.GetEncounterSnapshot();

        // Only the BAR LIST source is mode-swapped; the big DPS number always reads as live 5s
        // (with 60s rolling-avg fallback) regardless of mode — that's the user's primary
        // "what am I doing right now" signal and it must not change semantics underfoot.
        //
        // Boss mode + no encounter (waiting for boss…): do NOT feed GetTopHeroesBy60sShare —
        // that window includes everyone in AOI, so idle users still see peers' trash/hub damage
        // drifting the bars. Encounter view only when a fight is active or frozen ended.
        var top5 = SelectTopHeroesForBossOverlay(_meter, bossOnly, encounter);

        _window?.UpdateDps(dps, total60s, owner, maxHit, heroName, bossOnly, top5, encounter);
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

        // Same source selection as OnDpsChanged — only the BAR LIST is mode-swapped; the big
        // number always reads from CurrentOwnerTotal60s so its fallback math is identical
        // to the pre-encounter behavior.  Keep both call sites in lockstep.
        bool bossOnly = _meter.BossOnlyMode;
        var encounter = _meter.GetEncounterSnapshot();
        var top5 = SelectTopHeroesForBossOverlay(_meter, bossOnly, encounter);

        _window.UpdateDps(
            _meter.CurrentDps,
            _meter.CurrentOwnerTotal60s,
            _meter.LikelySelfOwnerId,
            _meter.MaxSingleHit,
            _meter.CurrentHeroDisplayName,
            bossOnly,
            top5,
            encounter);

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
            // Surface the foreground process name on the transition so log readers can answer
            // "why did the overlay just hide?" without re-running the watcher.  The watcher
            // exposes its last-cached process name on `LastForegroundProcessName`; on a hide
            // it tells the user which app stole focus, on a show it tells them what came back.
            string fg = GameForegroundWatcher.LastForegroundProcessName;
            AppendLog(shouldShow
                ? $"DpsOverlayPresenter: overlay shown — foreground='{fg}' (game or self)"
                : $"DpsOverlayPresenter: overlay hidden — foreground='{fg}' (not game, not self)");
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
            AppendLog($"PowerResultStats: Total={snap.Total} NoSubscriber={snap.NoSubscriber} ParseFailures={snap.ParseFailures}");
        }
    }

    /// <summary>Boss-only overlay: show encounter leaderboard only while a fight is active or
    /// the ended snapshot is frozen. Otherwise return an empty list — never the global 60s
    /// AOI leaderboard, which would keep moving from other players while the detail line reads
    /// <c>waiting for boss…</c>.</summary>
    private static IReadOnlyList<DpsMeter.HeroShareEntry> SelectTopHeroesForBossOverlay(
        DpsMeter meter,
        bool bossOnly,
        DpsMeter.EncounterSnapshot encounter)
    {
        if (!bossOnly)
            return meter.GetTopHeroesBy60sShare(5);
        if (encounter.IsActive || encounter.IsEnded)
            return meter.GetTopHeroesByEncounterShare(5);
        return Array.Empty<DpsMeter.HeroShareEntry>();
    }

    /// <summary>Per-event diagnostic sink shared by the meter and the sniffer. Gated by
    /// <see cref="DpsOverlaySettingsFile.IsLoggingEnabled"/> — when the user sets
    /// <c>"LoggingEnabled": false</c> in <c>dps-overlay.json</c> this becomes a no-op,
    /// suppressing the per-event triage stream (PowerResultStats heartbeat, encounter
    /// lifecycle, peer-pet folds, hero-resolution misses, etc.) so the log file stops
    /// growing.  Hot path — the gate check is a single static field read so the cost of
    /// having logging disabled is effectively zero per call.</summary>
    private static void AppendLog(string line)
    {
        if (!DpsOverlaySettingsFile.IsLoggingEnabled) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DiagnosticLogPath)!);
            File.AppendAllText(DiagnosticLogPath, $"[{DateTime.UtcNow:HH:mm:ss.fff}] {line}{Environment.NewLine}");
        }
        catch { /* log I/O errors swallowed — don't let logging crash the presenter */ }
    }

    public void Dispose() => Stop();
}
