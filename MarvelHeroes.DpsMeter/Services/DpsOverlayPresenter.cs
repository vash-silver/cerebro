using System;
using System.Collections.Generic;
using System.IO;
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
    private DpsOverlayWindow? _overlayWindow;
    private DpsLiveWindow? _liveWindow;
    private DpsOverlaySettingsFile? _sharedSettings;
    private bool _inWindowMode;
    private DispatcherTimer? _decayTimer;
    private bool _lastVisibilityDecision = true;

    private DateTime _lastStatsLogUtc = DateTime.MinValue;

    // Tracks previous boss-encounter state so we can detect the active→ended transition
    // and trigger an auto-save exactly once per fight.
    private bool _prevBossEncounterActive;
    private bool _prevBossEncounterEnded;

    public DpsOverlayPresenter(MhMissionSniffer sniffer, Dispatcher uiDispatcher)
    {
        _sniffer = sniffer ?? throw new ArgumentNullException(nameof(sniffer));
        _uiDispatcher = uiDispatcher ?? throw new ArgumentNullException(nameof(uiDispatcher));
    }

    public bool IsRunning => _meter != null;

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
            _inWindowMode   = _sharedSettings.WindowMode;

            _overlayWindow = new DpsOverlayWindow(_sharedSettings);
            _liveWindow    = new DpsLiveWindow(_sharedSettings);

            initialBossOnly = _overlayWindow.InitialBossOnlyPreference;

            WireWindowEvents(_overlayWindow);
            WireWindowEvents(_liveWindow);

            if (_inWindowMode)
                _liveWindow.Show();
            else
                _overlayWindow.ShowWithoutActivating();

            _decayTimer = new DispatcherTimer(
                TimeSpan.FromMilliseconds(250),
                DispatcherPriority.Background,
                OnDecayTick,
                _uiDispatcher);
            _decayTimer.Start();
        });

        _meter.BossOnlyMode = initialBossOnly;

        AppendLog($"DpsOverlayPresenter started (sniffer running={_sniffer.IsRunning}, windowMode={_inWindowMode})");
    }

    private void WireWindowEvents(DpsOverlayWindow w)
    {
        w.BossOnlyToggled      += (enabled) => { if (_meter != null) _meter.BossOnlyMode = enabled; };
        w.SwitchModeRequested  += ToggleWindowMode;
        w.SaveSnapshotRequested += (h, enc, p) => SaveSnapshotNow(h, enc, p);
        w.ClearDpsRequested    += ClearDpsNow;
        w.ViewReportsRequested += OpenReportViewer;
    }

    private void WireWindowEvents(DpsLiveWindow w)
    {
        w.BossOnlyToggled      += (enabled) => { if (_meter != null) _meter.BossOnlyMode = enabled; };
        w.SwitchModeRequested  += ToggleWindowMode;
        w.SaveSnapshotRequested += (h, enc, p) => SaveSnapshotNow(h, enc, p);
        w.ClearDpsRequested    += ClearDpsNow;
        w.ViewReportsRequested += OpenReportViewer;
    }

    private void ToggleWindowMode()
    {
        _inWindowMode = !_inWindowMode;
        if (_inWindowMode)
        {
            _overlayWindow?.Hide();
            _liveWindow?.Show();
        }
        else
        {
            _liveWindow?.Hide();
            _overlayWindow?.ShowWithoutActivating();
        }
        if (_sharedSettings != null)
        {
            _sharedSettings.WindowMode = _inWindowMode;
            DpsOverlaySettingsFile.Save(_sharedSettings);
        }
        AppendLog($"DpsOverlayPresenter: switched to {(_inWindowMode ? "window" : "overlay")} mode");
    }

    public void Stop()
    {
        if (!IsRunning) return;

        _uiDispatcher.Invoke(() =>
        {
            _decayTimer?.Stop();
            _decayTimer = null;
            try { _overlayWindow?.Close(); } catch { }
            _overlayWindow = null;
            try { _liveWindow?.CloseByPresenter(); } catch { }
            _liveWindow = null;
            _sharedSettings = null;
        });

        if (_bossMeter != null)
        {
            _bossMeter.DpsChanged -= OnDpsChanged;
            _bossMeter.Dispose();
            _bossMeter = null;
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
        string heroName     = _meter.CurrentHeroDisplayName;
        bool   bossOnly     = _meter.BossOnlyMode;
        var    encounter    = _meter.GetEncounterSnapshot();
        var    top5         = SelectTopHeroesForOverlay(_meter, bossOnly, encounter);

        SnapshotBossMeter(out double bossDps, out long bossTotal60s,
            out var bossTop5, out var bossEncounter);

        var powerBreakdown = _meter.GetSelfPowerBreakdown(8, bossOnly && (encounter.IsActive || encounter.IsEnded));

        PushUpdateToWindows(dps, total60s, sessionTotal, owner, maxHit, heroName, bossOnly, top5, encounter,
            bossDps, bossTotal60s, bossTop5, bossEncounter, powerBreakdown);
    }

    private void OnDecayTick(object? sender, EventArgs e)
    {
        if (_meter is null || (_overlayWindow is null && _liveWindow is null)) return;

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
            _meter.CurrentHeroDisplayName,
            bossOnly,
            top5,
            encounter,
            bossDps,
            bossTotal60s,
            bossTop5,
            bossEncounter,
            powerBreakdown);

        // Auto-save when a boss fight transitions from active → ended.
        // Guard: previous tick must have seen IsActive=true so we don't fire on a stale
        // "already ended" state left over from a previous fight or from startup.
        if (_prevBossEncounterActive && !_prevBossEncounterEnded && bossEncounter.IsEnded)
            AutoSaveFight(bossTop5, bossEncounter, powerBreakdown);
        _prevBossEncounterActive = bossEncounter.IsActive;
        _prevBossEncounterEnded  = bossEncounter.IsEnded;

        // Visibility gating applies only to the overlay (the live window is a normal window).
        if (!_inWindowMode && _overlayWindow != null)
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
        ulong owner, uint maxHit, string heroName,
        bool bossOnly,
        IReadOnlyList<DpsMeter.HeroShareEntry>? top5,
        DpsMeter.EncounterSnapshot encounter,
        double bossDps, long bossTotal60s,
        IReadOnlyList<DpsMeter.HeroShareEntry> bossTop5,
        DpsMeter.EncounterSnapshot bossEncounter,
        IReadOnlyList<DpsMeter.PowerBreakdownEntry>? powerBreakdown)
    {
        _overlayWindow?.UpdateDps(dps, total60s, sessionTotal, owner, maxHit, heroName, bossOnly,
            top5, encounter, bossDps, bossTotal60s, bossTop5, bossEncounter, powerBreakdown);
        _liveWindow?.UpdateDps(dps, total60s, sessionTotal, owner, maxHit, heroName, bossOnly,
            top5, encounter, bossDps, bossTotal60s, bossTop5, bossEncounter, powerBreakdown);
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

        string label;
        if (bossOnly && (encounter.IsActive || encounter.IsEnded))
            label = string.IsNullOrEmpty(heroName) ? "Boss Fight" : $"Boss Fight — {heroName}";
        else
            label = string.IsNullOrEmpty(heroName) ? "Session" : $"Session — {heroName}";

        var id = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ");
        var snap = new DpsSnapshot
        {
            Id                 = id,
            SavedUtc           = DateTime.UtcNow,
            Label              = label,
            Mode               = bossOnly ? "Boss Only" : "All Damage",
            HeroName           = heroName,
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

    private void AutoSaveFight(
        IReadOnlyList<DpsMeter.HeroShareEntry>? topHeroes,
        DpsMeter.EncounterSnapshot encounter,
        IReadOnlyList<DpsMeter.PowerBreakdownEntry>? powerBreakdown)
    {
        if (_bossMeter is null) return;
        if (encounter.SelfTotal <= 0) return;  // no personal damage → skip

        string heroName = _meter?.CurrentHeroDisplayName ?? "";
        string label = string.IsNullOrEmpty(heroName)
            ? "Boss Fight"
            : $"Boss Fight — {heroName}";

        var id   = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ");
        var snap = new DpsSnapshot
        {
            Id                 = id,
            SavedUtc           = DateTime.UtcNow,
            Label              = label,
            Mode               = "Boss Only",
            HeroName           = heroName,
            Dps                = _bossMeter.CurrentDps,
            TotalDamage        = encounter.SelfTotal,
            MaxSingleHit       = _meter?.MaxSingleHit ?? 0,
            EncounterEnded     = true,
            EncounterSelfTotal = encounter.SelfTotal,
            IsAutoSave         = true,
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
        DpsReportStore.PruneOldAutoSaves(50);
        AppendLog($"DpsOverlayPresenter: auto-saved fight — id={id}, label='{label}', selfTotal={encounter.SelfTotal}");
    }

    public void ClearDpsNow()
    {
        _meter?.ResetSession();
        _bossMeter?.ResetSession();
        AppendLog("DpsOverlayPresenter: DPS cleared by user request");
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
