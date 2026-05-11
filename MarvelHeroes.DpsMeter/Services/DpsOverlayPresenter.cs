using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using MarvelHeroesComporator.NetworkSniffer;
using MarvelHeroes.DpsMeter.Models;
using MarvelHeroes.DpsMeter.Windows;

namespace MarvelHeroes.DpsMeter.Services;

/// <summary>
/// Glue between the passive network sniffer, the <see cref="DpsMeter"/> aggregator, and the on-screen
/// overlay or live window.  Single entry point for the host app: call <see cref="Start"/> once
/// (after the sniffer is running) and a floating DPS number appears; <see cref="Stop"/> hides and
/// tears down.
/// </summary>
public sealed class DpsOverlayPresenter : IDisposable
{
    private readonly MhMissionSniffer _sniffer;
    private readonly Dispatcher _uiDispatcher;

    private static readonly string DiagnosticLogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MarvelHeroesComporator", "dps-meter.log");

    private DpsMeter? _meter;
    private DpsMeter? _bossMeter;
    private EternitySplinterTracker? _splinterTracker;
    private DpsOverlayWindow? _overlayWindow;
    private MainAppWindow? _mainWindow;
    private DpsOverlaySettingsFile? _sharedSettings;
    // Tracks the current visibility state of the floating overlay (formerly tracked by
    // _inWindowMode; renamed to reflect the new app-first design where the main window is
    // always up and the overlay is optional).
    private bool _overlayVisible;
    private DispatcherTimer? _decayTimer;
    private bool _lastVisibilityDecision = true;

    private DateTime _lastStatsLogUtc = DateTime.MinValue;

    // Tracks previous boss-encounter state so we can detect the active→ended transition
    // and trigger an auto-save exactly once per fight.
    private bool _prevBossEncounterActive;
    private bool _prevBossEncounterEnded;

    // DPS sparkline: samples collected every 5s during the active boss encounter.
    private readonly List<(int Second, float Dps)> _sparkSamples = new();
    private DateTime _sparkEncounterStartUtc = DateTime.MinValue;
    private DateTime _lastSparkSampleUtc     = DateTime.MinValue;

    public DpsOverlayPresenter(MhMissionSniffer sniffer, Dispatcher uiDispatcher)
    {
        _sniffer = sniffer ?? throw new ArgumentNullException(nameof(sniffer));
        _uiDispatcher = uiDispatcher ?? throw new ArgumentNullException(nameof(uiDispatcher));
    }

    public bool IsRunning => _meter != null;

    /// <summary>Exposed for <see cref="TestModeDataFeed"/> so the feed can inject synthetic events
    /// directly into the running meters without going through the sniffer.  Non-null after <see cref="Start"/>.</summary>
    internal DpsMeter? Meter     => _meter;
    internal DpsMeter? BossMeter => _bossMeter;
    internal EternitySplinterTracker? SplinterTracker => _splinterTracker;

    /// <summary>How long the overlay stays in the bright "ES: dropped!" flash state after a
    /// splinter is detected.  Short enough that it doesn't crowd out the countdown, long
    /// enough to catch the user's eye on a single decay-tick frame.</summary>
    private static readonly TimeSpan JustDroppedFlashDuration = TimeSpan.FromSeconds(3);

    /// <summary>Optional predicate polled at ~4 Hz. When it returns <c>false</c>, the overlay
    /// hides itself; ignored when in window mode. Safe to touch WPF directly inside the delegate.</summary>
    public Func<bool>? ShouldBeVisible { get; set; }

    public void Start()
    {
        if (IsRunning) return;

        _meter = new DpsMeter(_sniffer) { Diagnostic = AppendLog };
        _meter.DpsChanged += OnDpsChanged;

        _bossMeter = new DpsMeter(_sniffer) { Diagnostic = AppendLog };
        _bossMeter.BossOnlyMode = true;
        _bossMeter.DpsChanged += OnDpsChanged;

        // Eternity Splinter tracker -- subscribes to the sniffer's LootDropped events for
        // a 7-minute cooldown ticker.  Independent of the DPS meters and unaffected by
        // boss-only mode / encounter lifecycle.
        _splinterTracker = new EternitySplinterTracker(_sniffer) { Diagnostic = AppendLog };
        _splinterTracker.SplinterDropped += (_, args) =>
            AppendLog($"DpsOverlayPresenter: splinter dropped at {args.Utc:HH:mm:ss} -- 7 min cooldown armed");
        _splinterTracker.CooldownExpired += (_, _) =>
        {
            AppendLog("DpsOverlayPresenter: splinter cooldown expired -- next drop eligible");
            // Optional audio cue.  Routed through SplinterCooldownSoundPlayer which tries
            // the user's configured custom sound file first (any WPF-decodable format) and
            // falls back to the Windows notification sound when no path is set or the file
            // can't be played.  Both code paths are wrapped in try/catch internally; the
            // tick handler never throws because of a bad audio device.
            if (_sharedSettings?.SplinterCooldownSoundEnabled == true)
            {
                bool playedCustom = SplinterCooldownSoundPlayer.Play(_sharedSettings.SplinterCooldownSoundPath);
                AppendLog(playedCustom
                    ? $"DpsOverlayPresenter: played custom splinter sound '{_sharedSettings.SplinterCooldownSoundPath}'"
                    : "DpsOverlayPresenter: played system asterisk for splinter cooldown");
            }
        };

        {
            var prior = _sniffer.Diagnostic;
            _sniffer.Diagnostic = prior == null
                ? AppendLog
                : msg => { prior(msg); AppendLog(msg); };
        }

        bool initialBossOnly = false;
        _uiDispatcher.Invoke(() =>
        {
            _sharedSettings = DpsOverlaySettingsFile.Load();
            _overlayVisible = _sharedSettings.ShowOverlay;

            // App-first layout: the main window is created and shown unconditionally; the
            // floating overlay is created up front too (so we can push DPS updates to it
            // even before it's visible), but Show()n only when the user opts in.
            _mainWindow    = new MainAppWindow(_sharedSettings);
            _overlayWindow = new DpsOverlayWindow(_sharedSettings);

            initialBossOnly = _mainWindow.InitialBossOnlyPreference;

            WireWindowEvents(_mainWindow);
            WireWindowEvents(_overlayWindow);

            _mainWindow.Show();
            if (_overlayVisible)
                _overlayWindow.ShowWithoutActivating();

            _decayTimer = new DispatcherTimer(
                TimeSpan.FromMilliseconds(250),
                DispatcherPriority.Background,
                OnDecayTick,
                _uiDispatcher);
            _decayTimer.Start();
        });

        _meter.BossOnlyMode = initialBossOnly;

        AppendLog($"DpsOverlayPresenter started (sniffer running={_sniffer.IsRunning}, overlayVisible={_overlayVisible})");
    }

    private void WireWindowEvents(DpsOverlayWindow w)
    {
        w.BossOnlyToggled      += (enabled) => { if (_meter != null) _meter.BossOnlyMode = enabled; };
        // The overlay's right-click "Switch mode" menu item used to flip between overlay /
        // live-window mode.  In the app-first layout it instead toggles the overlay's own
        // visibility -- same effect from the user's perspective (the meter "moves out of
        // the way") while leaving the main window untouched.
        w.SwitchModeRequested  += () => SetOverlayVisible(!_overlayVisible);
        // Alt+F4 / WM_CLOSE on the overlay -> hide + persist (matches X-button behavior).
        w.HideRequested        += () => SetOverlayVisible(false);
        w.SaveSnapshotRequested += (h, enc, p) => SaveSnapshotNow(h, enc, p);
        w.ClearDpsRequested    += ClearDpsNow;
        w.ResetMaxHitRecordRequested += ResetMaxHitRecordNow;
        w.ResetSplinterCooldownRequested += ResetSplinterCooldownNow;
        w.ViewReportsRequested += OpenReportViewer;
    }

    private void WireWindowEvents(MainAppWindow w)
    {
        w.BossOnlyToggled      += (enabled) => { if (_meter != null) _meter.BossOnlyMode = enabled; };
        w.SwitchModeRequested  += () => SetOverlayVisible(!_overlayVisible);  // see overlay-window note
        w.SaveSnapshotRequested += (h, enc, p) => SaveSnapshotNow(h, enc, p);
        w.ClearDpsRequested    += ClearDpsNow;
        w.ResetMaxHitRecordRequested += ResetMaxHitRecordNow;
        w.ResetSplinterCooldownRequested += ResetSplinterCooldownNow;
        w.ArmSplinterCooldownRequested   += ArmSplinterCooldownNow;
        // The Live tab's right-click "View reports" switches tabs in-place (handled inside
        // MainAppWindow); the presenter doesn't need to do anything here.  No ViewReports
        // subscription means we avoid double-opening a standalone window.

        // Header "Show overlay" checkbox -- the canonical user-facing toggle in the new layout.
        w.ShowOverlayToggled += SetOverlayVisible;
    }

    /// <summary>Show or hide the floating overlay and persist the choice.  Keeps the main
    /// window's checkbox in sync so the user can toggle from either the checkbox or the
    /// overlay's right-click menu without the two views disagreeing.</summary>
    private void SetOverlayVisible(bool visible)
    {
        if (_overlayVisible == visible) return;
        _overlayVisible = visible;
        if (visible)
            _overlayWindow?.ShowWithoutActivating();
        else
            _overlayWindow?.Hide();
        _mainWindow?.SetShowOverlayChecked(visible);
        if (_sharedSettings != null)
        {
            _sharedSettings.ShowOverlay = visible;
            DpsOverlaySettingsFile.Save(_sharedSettings);
        }
        AppendLog($"DpsOverlayPresenter: overlay visibility = {visible}");
    }

    public void Stop()
    {
        if (!IsRunning) return;

        _uiDispatcher.Invoke(() =>
        {
            _decayTimer?.Stop();
            _decayTimer = null;
            try { _overlayWindow?.CloseByPresenter(); } catch { }
            _overlayWindow = null;
            try { _mainWindow?.CloseByPresenter(); } catch { }
            _mainWindow = null;
            _sharedSettings = null;
        });

        if (_bossMeter != null)
        {
            _bossMeter.DpsChanged -= OnDpsChanged;
            _bossMeter.Dispose();
            _bossMeter = null;
        }

        if (_splinterTracker != null)
        {
            _splinterTracker.Dispose();
            _splinterTracker = null;
        }

        if (_meter != null)
        {
            _meter.DpsChanged -= OnDpsChanged;
            try { _meter.FlushPlayerIndexNow(); } catch { }
            _meter.Dispose();
            _meter = null;
        }
        AppendLog("DpsOverlayPresenter stopped");
    }

    private void OnDpsChanged(object? sender, EventArgs e)
    {
        if (_meter is null) return;
        double dps          = _meter.CurrentDps;
        long   total60s     = _meter.CurrentOwnerTotal60s;
        long   sessionTotal = _meter.CurrentOwnerSessionTotal;
        ulong  owner        = _meter.LikelySelfOwnerId;
        uint   maxHit       = _meter.MaxSingleHit;
        uint   maxHitSess   = _meter.MaxSingleHitSession;
        uint   maxHitEnc    = _meter.MaxSingleHitEncounter;
        string heroName     = _meter.CurrentHeroDisplayName;
        string bossName     = CurrentBossNameOrEmpty();
        bool   bossOnly     = _meter.BossOnlyMode;
        var    encounter    = _meter.GetEncounterSnapshot();
        var    top5         = SelectTopHeroesForOverlay(_meter, bossOnly, encounter);

        SnapshotBossMeter(out double bossDps, out long bossTotal60s,
            out var bossTop5, out var bossEncounter);

        var powerBreakdown = _meter.GetSelfPowerBreakdown(8, bossOnly && (encounter.IsActive || encounter.IsEnded));

        PushUpdateToWindows(dps, total60s, sessionTotal, owner, maxHit, maxHitSess, maxHitEnc,
            heroName, bossName, bossOnly, top5, encounter,
            bossDps, bossTotal60s, bossTop5, bossEncounter, powerBreakdown);
    }

    // Boss name is independent of mode — the boss-only meter is the canonical source while a
    // fight is active, but if the user toggled off boss-only mid-fight, the all-damage meter
    // also still holds the name from when the fight started.  Prefer boss meter, fall back
    // to all-damage, finally empty so the title degrades to plain "BOSS DPS".
    private string CurrentBossNameOrEmpty()
        => !string.IsNullOrEmpty(_bossMeter?.CurrentBossName)
            ? _bossMeter!.CurrentBossName
            : (_meter?.CurrentBossName ?? string.Empty);

    private void OnDecayTick(object? sender, EventArgs e)
    {
        if (_meter is null || (_overlayWindow is null && _mainWindow is null)) return;

        _meter.Tick(DateTime.UtcNow);
        _bossMeter?.Tick(DateTime.UtcNow);
        _meter.FlushPlayerIndexIfDirty();

        bool bossOnly = _meter.BossOnlyMode;
        var  encounter = _meter.GetEncounterSnapshot();
        var  top5      = SelectTopHeroesForOverlay(_meter, bossOnly, encounter);

        SnapshotBossMeter(out double bossDps, out long bossTotal60s,
            out var bossTop5, out var bossEncounter);

        var powerBreakdown = _meter.GetSelfPowerBreakdown(8, bossOnly && (encounter.IsActive || encounter.IsEnded));

        PushUpdateToWindows(
            _meter.CurrentDps,
            _meter.CurrentOwnerTotal60s,
            _meter.CurrentOwnerSessionTotal,
            _meter.LikelySelfOwnerId,
            _meter.MaxSingleHit,
            _meter.MaxSingleHitSession,
            _meter.MaxSingleHitEncounter,
            _meter.CurrentHeroDisplayName,
            CurrentBossNameOrEmpty(),
            bossOnly,
            top5,
            encounter,
            bossDps,
            bossTotal60s,
            bossTop5,
            bossEncounter,
            powerBreakdown);

        // Tick the splinter tracker so its cooldown-expired event lands on the UI dispatcher,
        // then push the resulting state to the overlay so the countdown ticks down visibly.
        // We track a short post-drop "flash" window (the JustDroppedFlashDuration window after
        // LastDropUtc) so the UI can render an attention-grabbing "ES: dropped!" state for a
        // second or two before settling into the regular countdown.
        if (_splinterTracker != null)
        {
            _splinterTracker.Tick();
            var splinterNowUtc = DateTime.UtcNow;
            var lastDropUtc = _splinterTracker.LastDropUtc;
            bool justDropped = lastDropUtc != DateTime.MinValue
                && (splinterNowUtc - lastDropUtc) < JustDroppedFlashDuration;
            _overlayWindow?.UpdateSplinterStatus(
                _splinterTracker.IsCooldownActive,
                _splinterTracker.RemainingCooldown,
                _splinterTracker.DropCount,
                justDropped);
            _mainWindow?.UpdateSplinterStatus(
                _splinterTracker.IsCooldownActive,
                _splinterTracker.RemainingCooldown,
                _splinterTracker.DropCount,
                justDropped);
        }

        // Collect sparkline samples every 5s while encounter is live.
        CollectSparklineSample(bossEncounter, bossDps);

        // Auto-save when a boss fight transitions from active → ended.
        // Guard: previous tick must have seen IsActive=true so we don't fire on a stale
        // "already ended" state left over from a previous fight or from startup.
        if (_prevBossEncounterActive && !_prevBossEncounterEnded && bossEncounter.IsEnded)
            AutoSaveFight(bossTop5, bossEncounter, powerBreakdown);
        _prevBossEncounterActive = bossEncounter.IsActive;
        _prevBossEncounterEnded  = bossEncounter.IsEnded;

        // Visibility gating applies only to the floating overlay -- the main window is a
        // standard taskbar app and stays visible regardless of which process is in the
        // foreground.  When the user hasn't asked for the overlay at all (_overlayVisible
        // false), we also skip the auto-hide dance entirely.
        if (_overlayVisible && _overlayWindow != null)
        {
            bool shouldShow = ShouldBeVisible?.Invoke() ?? true;
            if (shouldShow != _lastVisibilityDecision)
            {
                _lastVisibilityDecision = shouldShow;
                _overlayWindow.Visibility = shouldShow ? Visibility.Visible : Visibility.Collapsed;
                string fg = GameForegroundWatcher.LastForegroundProcessName;
                AppendLog(shouldShow
                    ? $"DpsOverlayPresenter: overlay shown — foreground='{fg}' (game or self)"
                    : $"DpsOverlayPresenter: overlay hidden — foreground='{fg}' (not game, not self)");
            }
        }

        var nowUtc = DateTime.UtcNow;
        if ((nowUtc - _lastStatsLogUtc).TotalSeconds >= 5.0)
        {
            _lastStatsLogUtc = nowUtc;
            var snap = _sniffer.PowerResultStats;
            AppendLog($"PowerResultStats: Total={snap.Total} NoSubscriber={snap.NoSubscriber} ParseFailures={snap.ParseFailures}");
        }
    }

    private void PushUpdateToWindows(
        double dps, long total60s, long sessionTotal,
        ulong owner, uint maxHit, uint maxHitSession, uint maxHitEncounter,
        string heroName, string bossName,
        bool bossOnly,
        IReadOnlyList<DpsMeter.HeroShareEntry>? top5,
        DpsMeter.EncounterSnapshot encounter,
        double bossDps, long bossTotal60s,
        IReadOnlyList<DpsMeter.HeroShareEntry> bossTop5,
        DpsMeter.EncounterSnapshot bossEncounter,
        IReadOnlyList<DpsMeter.PowerBreakdownEntry>? powerBreakdown)
    {
        _overlayWindow?.UpdateDps(dps, total60s, sessionTotal, owner, maxHit, maxHitSession, maxHitEncounter,
            heroName, bossName, bossOnly, top5, encounter,
            bossDps, bossTotal60s, bossTop5, bossEncounter, powerBreakdown);
        _mainWindow?.UpdateDps(dps, total60s, sessionTotal, owner, maxHit, maxHitSession, maxHitEncounter,
            heroName, bossName, bossOnly, top5, encounter,
            bossDps, bossTotal60s, bossTop5, bossEncounter, powerBreakdown);
    }

    private void SnapshotBossMeter(
        out double bossDps,
        out long   bossTotal60s,
        out IReadOnlyList<DpsMeter.HeroShareEntry> bossTop5,
        out DpsMeter.EncounterSnapshot bossEncounter)
    {
        if (_bossMeter is null)
        {
            bossDps = 0; bossTotal60s = 0;
            bossTop5 = Array.Empty<DpsMeter.HeroShareEntry>();
            bossEncounter = default;
            return;
        }
        bossDps      = _bossMeter.CurrentDps;
        bossTotal60s = _bossMeter.CurrentOwnerTotal60s;
        bossEncounter = _bossMeter.GetEncounterSnapshot();
        bossTop5 = bossEncounter.IsActive || bossEncounter.IsEnded
            ? _bossMeter.GetTopHeroesByEncounterShare(5)
            : Array.Empty<DpsMeter.HeroShareEntry>();
    }

    private static IReadOnlyList<DpsMeter.HeroShareEntry> SelectTopHeroesForOverlay(
        DpsMeter meter,
        bool bossOnly,
        DpsMeter.EncounterSnapshot encounter)
    {
        if (!bossOnly)
            return meter.GetTopHeroesBySessionShare(5);
        if (encounter.IsActive || encounter.IsEnded)
            return meter.GetTopHeroesByEncounterShare(5);
        return Array.Empty<DpsMeter.HeroShareEntry>();
    }

    private static void AppendLog(string line)
    {
        if (!DpsOverlaySettingsFile.IsLoggingEnabled) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DiagnosticLogPath)!);
            File.AppendAllText(DiagnosticLogPath, $"[{DateTime.UtcNow:HH:mm:ss.fff}] {line}{Environment.NewLine}");
        }
        catch { }
    }

    public void Dispose() => Stop();

    // ── Snapshot / clear / report viewer ─────────────────────────────────────────────────────

    public void SaveSnapshotNow(
        IReadOnlyList<DpsMeter.HeroShareEntry>? topHeroes,
        DpsMeter.EncounterSnapshot encounter,
        IReadOnlyList<DpsMeter.PowerBreakdownEntry>? powerBreakdown)
    {
        if (_meter is null) return;

        bool bossOnly   = _meter.BossOnlyMode;
        string heroName = _meter.CurrentHeroDisplayName;
        double dps      = _meter.CurrentDps;
        long   total    = bossOnly ? encounter.SelfTotal : _meter.CurrentOwnerSessionTotal;

        // Same boss-name lookup as AutoSaveFight — pulled from whichever meter knows the
        // current encounter.  Stays empty for normal-mode (Session) saves so the label keeps
        // its existing "Session — <hero>" shape.
        bool bossEncounterActive = bossOnly && (encounter.IsActive || encounter.IsEnded);
        string bossName = bossEncounterActive ? (_bossMeter?.CurrentBossName ?? _meter.CurrentBossName ?? "") : "";

        string label;
        if (bossEncounterActive)
        {
            if (!string.IsNullOrEmpty(bossName) && !string.IsNullOrEmpty(heroName))
                label = $"{bossName} — {heroName}";
            else if (!string.IsNullOrEmpty(bossName))
                label = bossName;
            else if (!string.IsNullOrEmpty(heroName))
                label = $"Boss Fight — {heroName}";
            else
                label = "Boss Fight";
        }
        else
        {
            label = string.IsNullOrEmpty(heroName) ? "Session" : $"Session — {heroName}";
        }

        var id = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ");
        var snap = new DpsSnapshot
        {
            Id                 = id,
            SavedUtc           = DateTime.UtcNow,
            Label              = label,
            Mode               = bossOnly ? "Boss Only" : "All Damage",
            HeroName           = heroName,
            BossName           = bossName,
            Dps                = dps,
            TotalDamage        = total,
            MaxSingleHit       = _meter.MaxSingleHit,
            EncounterEnded     = encounter.IsEnded,
            EncounterSelfTotal = encounter.SelfTotal,
        };

        if (topHeroes != null)
            foreach (var r in topHeroes)
                snap.Leaderboard.Add(new DpsSnapshot.HeroEntry
                {
                    Name       = r.Name,
                    PlayerName = r.PlayerName,
                    IsSelf     = r.IsSelf,
                    Dps        = r.Dps,
                    Total      = r.Total60s,
                    Percent    = r.Percent,
                });

        if (powerBreakdown != null)
            foreach (var p in powerBreakdown)
                snap.PowerBreakdown.Add(new DpsSnapshot.PowerEntry
                {
                    Name        = p.Name,
                    Hits        = p.Hits,
                    TotalDamage = p.TotalDamage,
                    Percent     = p.Percent,
                    MaxHit      = p.MaxHit,
                });

        DpsReportStore.Save(snap);
        AppendLog($"DpsOverlayPresenter: snapshot saved — id={id}, label='{label}'");
    }

    private void CollectSparklineSample(DpsMeter.EncounterSnapshot enc, double bossDps)
    {
        if (!enc.IsActive) return;

        // Clear if a new encounter started.
        if (enc.StartUtc != _sparkEncounterStartUtc)
        {
            _sparkSamples.Clear();
            _sparkEncounterStartUtc = enc.StartUtc;
            _lastSparkSampleUtc     = DateTime.MinValue;
        }

        var now = DateTime.UtcNow;
        if (_lastSparkSampleUtc != DateTime.MinValue &&
            (now - _lastSparkSampleUtc).TotalSeconds < 5.0) return;

        int second = enc.StartUtc != DateTime.MinValue
            ? (int)(now - enc.StartUtc).TotalSeconds : 0;
        _sparkSamples.Add((second, (float)bossDps));
        _lastSparkSampleUtc = now;
    }

    private void AutoSaveFight(
        IReadOnlyList<DpsMeter.HeroShareEntry>? topHeroes,
        DpsMeter.EncounterSnapshot encounter,
        IReadOnlyList<DpsMeter.PowerBreakdownEntry>? powerBreakdown)
    {
        if (_bossMeter is null) return;
        if (encounter.SelfTotal <= 0) return;  // no personal damage → skip

        // Hero name: prefer the normal meter; fall back to the self entry in the leaderboard
        // so short fights where _meter hasn't yet identified the player still get a name.
        string heroName = _meter?.CurrentHeroDisplayName ?? "";
        if (string.IsNullOrEmpty(heroName) && topHeroes != null)
            heroName = topHeroes.FirstOrDefault(h => h.IsSelf).Name ?? "";

        // Boss name is resolved by the meter from the first engaged boss's prototype index.
        // Empty when the prototype had no mapping in BossNames (unrecognised content, or the
        // off-by-one fallback failed) — keep the legacy "Boss Fight" wording in that case.
        // _bossMeter is already null-checked at the top of this method, so no ?. needed here.
        string bossName = _bossMeter.CurrentBossName;

        string label;
        if (!string.IsNullOrEmpty(bossName) && !string.IsNullOrEmpty(heroName))
            label = $"{bossName} — {heroName}";
        else if (!string.IsNullOrEmpty(bossName))
            label = bossName;
        else if (!string.IsNullOrEmpty(heroName))
            label = $"Boss Fight — {heroName}";
        else
            label = "Boss Fight";

        int duration = encounter.StartUtc != DateTime.MinValue && encounter.EndUtc != DateTime.MinValue
            ? (int)(encounter.EndUtc - encounter.StartUtc).TotalSeconds : 0;

        // Self DPS comes from the self entry in the leaderboard (active-time DPS computed by
        // the meter); fall back to simple total/duration if the self row isn't in the list.
        double selfDps = 0;
        if (topHeroes != null)
        {
            foreach (var h in topHeroes) if (h.IsSelf) { selfDps = h.Dps; break; }
        }
        if (selfDps <= 0 && duration > 0)
            selfDps = (double)encounter.SelfTotal / duration;

        var id   = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ");
        var snap = new DpsSnapshot
        {
            Id                 = id,
            SavedUtc           = DateTime.UtcNow,
            Label              = label,
            Mode               = "Boss Only",
            HeroName           = heroName,
            BossName           = bossName,
            Dps                = selfDps,
            TotalDamage        = encounter.SelfTotal,
            MaxSingleHit       = _meter?.MaxSingleHit ?? 0,
            EncounterEnded     = true,
            EncounterSelfTotal = encounter.SelfTotal,
            IsAutoSave         = true,
            IsPersonalBest     = PersonalBestStore.CheckAndUpdate(heroName, selfDps),
            DurationSeconds    = duration,
            DpsTimeline        = _sparkSamples.Select(s => new DpsSnapshot.SparkPoint { Second = s.Second, Dps = s.Dps }).ToList(),
        };

        if (topHeroes != null)
            foreach (var r in topHeroes)
            {
                var heroEntry = new DpsSnapshot.HeroEntry
                {
                    Name       = r.Name,
                    PlayerName = r.PlayerName,
                    IsSelf     = r.IsSelf,
                    Dps        = r.Dps,  // active-time DPS already computed by the meter
                    Total      = r.Total60s,
                    Percent    = r.Percent,
                };
                // Per-player power breakdown (up to 20 abilities per player).
                var ownerBreakdown = _bossMeter.GetPowerBreakdownForOwner(r.OwnerId, 20);
                foreach (var p in ownerBreakdown)
                    heroEntry.PowerBreakdown.Add(new DpsSnapshot.PowerEntry
                    {
                        Name        = p.Name,
                        Hits        = p.Hits,
                        TotalDamage = p.TotalDamage,
                        Percent     = p.Percent,
                        MaxHit      = p.MaxHit,
                    });
                snap.Leaderboard.Add(heroEntry);
            }

        if (powerBreakdown != null)
            foreach (var p in powerBreakdown)
                snap.PowerBreakdown.Add(new DpsSnapshot.PowerEntry
                {
                    Name        = p.Name,
                    Hits        = p.Hits,
                    TotalDamage = p.TotalDamage,
                    Percent     = p.Percent,
                    MaxHit      = p.MaxHit,
                });

        DpsReportStore.Save(snap);
        DpsReportStore.PruneOldAutoSaves(50);
        AppendLog($"DpsOverlayPresenter: auto-saved fight — id={id}, label='{label}', selfTotal={encounter.SelfTotal}");
    }

    public void ClearDpsNow()
    {
        _meter?.ResetSession();
        _bossMeter?.ResetSession();
        AppendLog("DpsOverlayPresenter: DPS cleared by user request");
    }

    /// <summary>Wipe the all-time max-hit record for the currently identified hero.  Routed
    /// through both meters so the persisted file stays in sync regardless of which one happens
    /// to fire next.  No-op when no hero is identified yet.</summary>
    public void ResetMaxHitRecordNow()
    {
        _meter?.ResetSelfMaxHitRecord();
        _bossMeter?.ResetSelfMaxHitRecord();
        AppendLog("DpsOverlayPresenter: max-hit record cleared by user request");
    }

    /// <summary>Clear the Eternity Splinter cooldown timer.  Useful when the server-side
    /// throttle is known to have been reset (relog, zone change, the user picked up a splinter
    /// before launching the meter) so the visible countdown can be brought back in sync with
    /// what the server thinks.</summary>
    public void ResetSplinterCooldownNow()
    {
        _splinterTracker?.Reset();
        AppendLog("DpsOverlayPresenter: splinter cooldown cleared by user request");
    }

    /// <summary>Manually start the 7-minute splinter cooldown from this moment.  Useful when
    /// the user saw a splinter drop in-game that the auto-detection missed -- e.g. it dropped
    /// before they launched the meter, the proto-index match failed because of a game patch,
    /// or the EntityCreate happened during a region-load packet storm that exceeded the
    /// per-session discovery log cap.</summary>
    public void ArmSplinterCooldownNow()
    {
        _splinterTracker?.ArmFromNow();
        AppendLog("DpsOverlayPresenter: splinter cooldown armed manually by user request");
    }

    public void OpenReportViewer()
    {
        _uiDispatcher.Invoke(() =>
        {
            var viewer = new ReportViewerWindow();
            viewer.Show();
        });
    }
}
