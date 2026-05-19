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
    private BuffTracker? _buffTracker;
    private CooldownTracker? _cooldownTracker;
    private LootScannerDiagnostic? _lootScanner;

    /// <summary>How many hero rows the main app's Live dashboard leaderboard requests per
    /// tick.  Raids run 10-15 players, so we ship 15 by default to fit a full raid roster.
    /// The floating overlay (<c>DpsDisplayPanel</c>) is still capped at 5 by its hardcoded
    /// XAML row slots -- changing that requires refactoring the overlay to a data-driven
    /// <c>ItemsControl</c>, which isn't done yet.  See call sites in
    /// <c>SelectTopHeroesForOverlay</c> and <c>SnapshotBossMeter</c>.</summary>
    private const int MaxLeaderboardRows = 15;
    private DpsOverlayWindow? _overlayWindow;
    private BuffOverlayWindow? _buffOverlayWindow;
    private CooldownOverlayWindow? _cooldownOverlayWindow;
    private MainAppWindow? _mainWindow;
    private DpsOverlaySettingsFile? _sharedSettings;
    private GlobalHotkey? _armSplinterHotkey;
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
        // a cooldown ticker (see EternitySplinterTracker.CooldownDuration for the actual
        // window length).  Independent of the DPS meters and unaffected by boss-only mode /
        // encounter lifecycle.
        _splinterTracker = new EternitySplinterTracker(_sniffer)
        {
            Diagnostic = AppendLog,
            // Persist the last-drop timestamp on every change so a mid-cooldown Cerebro
            // restart resumes the countdown from where it left off (the user typically
            // wants to know "is my splinter timer still running"; resetting on every
            // launch is the legacy behavior we're fixing here).  Best-effort save -- the
            // settings file's atomic write-temp-then-rename pattern means a crash in the
            // middle of a save leaves the previous valid file intact.  _sharedSettings
            // may not be loaded yet on the first invocation (the field is populated later
            // on the UI thread); the null-check guards against that pre-init window.
            LastDropTimestampChanged = utc =>
            {
                if (_sharedSettings == null) return;
                _sharedSettings.LastSplinterDropUtc = utc;
                DpsOverlaySettingsFile.Save(_sharedSettings);
            },
        };
        // Route the sound player's fallback diagnostics through the same log so we can see
        // *why* the system-asterisk fallback fired -- "file not found" vs. "exception
        // during MediaPlayer.Open" are very different failure modes and previously looked
        // identical from outside (just the "played system asterisk" line).
        SplinterCooldownSoundPlayer.Diagnostic = AppendLog;
        // Two alert moments, same sound: "a splinter just dropped, go grab it!" and
        // "the cooldown expired, next drop is eligible".  Routed through one helper so the
        // user's single SplinterCooldownSoundEnabled toggle controls both -- splitting
        // them into separate settings would be twice the UI cost for negligible benefit;
        // the drop and cooldown-expired events are always one CooldownDuration apart so
        // there's no overlap risk regardless of which value we pick.
        //
        // THREADING: SplinterDropped fires from the sniffer's capture thread (because
        // EternitySplinterTracker.OnEntityCreated runs there), but WPF's MediaPlayer --
        // used inside PlaySplinterAlert -> SplinterCooldownSoundPlayer.Play for the
        // custom-file path -- is a Freezable bound to its creating Dispatcher and silently
        // misbehaves when used cross-thread.  Marshal to the UI dispatcher before invoking
        // the alert.  CooldownExpired is already raised from the UI-dispatcher decay timer
        // (via Tick()), so it doesn't need the same hop.
        _splinterTracker.SplinterDropped += (_, args) =>
        {
            AppendLog($"DpsOverlayPresenter: splinter dropped at {args.Utc:HH:mm:ss} ({(args.Manual ? "manual" : "auto-detect")}) -- {EternitySplinterTracker.CooldownDuration.TotalMinutes:0} min cooldown armed");
            // Play the drop alert for BOTH auto-detect AND manual arms (live-view button,
            // global hotkey, Settings tab's "Arm now" button).  The hotkey case is the
            // important one: it fires while the user is focused on the game and CAN'T see
            // the overlay, so the audio cue is the only confirmation that the binding
            // actually registered.  The previous behaviour skipped manual arms on the
            // assumption "you pressed it, you already know" -- but that's exactly wrong for
            // the use case where the user pressed a global hotkey blindly.
            _uiDispatcher.BeginInvoke(new Action(() => PlaySplinterAlert("drop")));
        };
        _splinterTracker.CooldownExpired += (_, _) =>
        {
            AppendLog("DpsOverlayPresenter: splinter cooldown expired -- next drop eligible");
            PlaySplinterAlert("cooldown-expired");
        };

        // Buff tracker -- owns the active-buffs dictionary, fires events on add/remove.
        // SelfOwnerId is pushed in OnDecayTick from _meter.LikelySelfOwnerId, since the
        // self-owner identification can land mid-session (we need the LocalPlayer message
        // or a self-pinning power activation before _meter.LikelySelfOwnerId becomes
        // non-zero).  The tracker handles a 0 SelfOwnerId by ignoring events, so wiring
        // up early is safe.
        _buffTracker = new BuffTracker(_sniffer) { Diagnostic = AppendLog };
        // Cooldown tracker -- subscribes to LocalPowerActivated.  Self-owner id is pushed
        // in OnDecayTick alongside the buff tracker, since the same self-pinning logic
        // covers both.  Cheap to construct; idle until SelfOwnerId becomes non-zero.
        _cooldownTracker = new CooldownTracker(_sniffer) { Diagnostic = AppendLog };
        // Bootstrap the persisted cooldown-watchlist into Current so the tab reads the
        // user's saved set immediately on first paint.  Same publish/subscribe pattern
        // as TrackedBuffsConfig.
        CooldownTrackerConfig.ReplaceCurrent(CooldownTrackerConfig.Load());
        // Event-driven snapshot push: BuffChanged fires on the sniffer thread the moment
        // a condition is added or removed.  Driving the UI off this (in addition to the
        // 4 Hz decay-tick poll) gives near-zero latency on the state pill -- critical for
        // short-window buffs like Nightcrawler's ~1.5 s Bamf-teleport stealth, where the
        // tick-rate poll would leave the user blind for up to a quarter of the window.
        _buffTracker.BuffChanged += OnBuffChanged;

        // Loot scanner -- recon-only diagnostic that dumps every Unique-item EntityCreate's
        // property collection when verbose diagnostics is enabled.  Phase 1 of the
        // loot-filter feature: confirm wire-format assumptions before we invest in the
        // affix-range table + roll-quality scorer.  Zero overhead when verbose is off
        // (single hash probe + early-exit per EntityCreate).
        _lootScanner = new LootScannerDiagnostic(_sniffer)
        {
            Diagnostic         = AppendLog,
            IsVerboseEnabled   = () => DpsOverlaySettingsFile.IsVerboseDiagnosticsEnabled,
            // Provide the local avatar's prototype enum index lazily -- the value lands
            // mid-session once the user activates a power, so the scanner re-reads it on
            // every drop.  Returning 0 (avatar not yet identified) just skips the hunt
            // match silently; the rest of the scanner still works.
            SelfPrototypeIndex = () => _meter?.LikelySelfPrototypeIndex ?? 0u,
            // SelfOwnerId is plumbed alongside so the diagnostic log can distinguish
            // "meter never pinned a self-owner" (heuristic-only DPS mode) from "meter
            // pinned self but the avatar's EntityCreate was missed so the proto cache
            // is empty".  Both produce selfProto=0 but require different fixes.
            SelfOwnerId = () => _meter?.LikelySelfOwnerId ?? 0uL,
            // SelfHeroName drives the SelfOnly filter's hero-name comparison.  Sourced
            // from DpsMeter.LikelySelfHeroName which is set when the BuffTracker observes
            // a self-buff whose source power is in PowerHeroByProto.  Empty until pinned.
            SelfHeroName = () => _meter?.LikelySelfHeroName ?? string.Empty,
        };
        // Hunt-match sound: marshal to UI dispatcher because WPF's MediaPlayer is
        // Freezable-bound to its creating thread.  HuntMatched fires from the capture
        // thread (EntityCreated path), so we'd silently misbehave without the hop.
        _lootScanner.HuntMatched += (_, args) =>
        {
            _uiDispatcher.BeginInvoke(new Action(() => PlayHuntMatchAlert(args)));
        };

        // Self-hero inference: when a condition is applied to (or by) the local avatar and
        // the source power resolves to a known shipping-hero powerset, pin the hero name on
        // the meter.  This is the server-merge-resistant identification path -- the buff's
        // CreatorPowerPrototypeRef uses the root Prototype enum (stable across merge drift),
        // and PowerHeroByProto maps root-power-enum -> hero name.  Fires from the sniffer's
        // capture thread; NoteSelfBuffFromPower is lock-guarded so concurrent reads from the
        // UI thread are safe.  Cheap on miss (dictionary probe).
        _sniffer.ConditionAdded += (_, ev) =>
        {
            ulong selfOwner = _meter?.LikelySelfOwnerId ?? 0uL;
            if (selfOwner == 0 || ev.CreatorPowerPrototypeRef == 0) return;
            bool isSelf = ev.OwnerEntityId == selfOwner;
            bool isMine = ev.CreatorEntityId == selfOwner || ev.UltimateCreatorEntityId == selfOwner;
            if (!isSelf && !isMine) return;
            _meter!.NoteSelfBuffFromPower((uint)ev.CreatorPowerPrototypeRef);
        };

        // Discovery hook -- ONLY when verbose diagnostics is enabled.  Dumps the full
        // property collection of every matched condition so we can identify new buffs by
        // their effect (DamagePctBonus etc) the way we identified Empowered.  In normal
        // operation the BuffTracker's one-line "+ Overwatch (5.0s)" summary is plenty.
        _sniffer.ConditionAdded += (_, ev) =>
        {
            if (!DpsOverlaySettingsFile.IsVerboseDiagnosticsEnabled) return;
            ulong selfOwner = _meter?.LikelySelfOwnerId ?? 0uL;
            bool isSelf = selfOwner != 0 && ev.OwnerEntityId == selfOwner;
            bool isMine = selfOwner != 0 && (ev.CreatorEntityId == selfOwner || ev.UltimateCreatorEntityId == selfOwner);
            if (!isSelf && !isMine) return;
            // PowerNamesByProto (root Prototype enum) -- buff sources arrive via
            // Serializer.Transfer(ref PrototypeId) which keys against the root enum.
            // PowerNames is for NetMessagePowerResult damage events (Power-specific enum).
            string powerName = PowerNamesByProto.Get((uint)ev.CreatorPowerPrototypeRef) ?? "<unmapped>";
            // Pass PropertyEnumNames.Get as the symbolic-name resolver so the dump shows
            // "enum=283 DamagePctBonus" instead of "enum=283 ?", speeding up discovery of
            // new buff properties.  The resolver is optional; old call sites that don't pass
            // one still get the raw-enum log line.
            MhMissionSniffer.DumpPropertyCollectionAt(
                ev.RawProperties, ev.PropertyCollectionOffset, AppendLog,
                contextTag: $"BUFFDBG condId={ev.ConditionId} \"{powerName}\"",
                propertyEnumNameResolver: PropertyEnumNames.Get);
        };

        // Wire the sniffer's diagnostic to the presenter's AppendLog (which
        // applies the verbose-noise filter on top of the same on-disk file
        // writer App.xaml.cs uses).  Overwrite outright -- App.xaml.cs
        // ALREADY set Diagnostic to its own AppendLog at sniffer construction
        // time (so TryStart's device-probe messages have a log target before
        // the presenter exists), and our chain-on-non-null pattern would
        // double-log every event from this point forward because BOTH
        // AppendLog implementations append the same line to the same file.
        // The early-lifecycle messages already landed via App's AppendLog;
        // from here on, the filtered presenter version is what we want.
        _sniffer.Diagnostic = AppendLog;

        bool initialBossOnly = false;
        _uiDispatcher.Invoke(() =>
        {
            _sharedSettings = DpsOverlaySettingsFile.Load();
            _overlayVisible = _sharedSettings.ShowOverlay;

            // Restore the persisted splinter cooldown anchor if we have one.  Older
            // session timestamps (past the 6-min window) are still set on the tracker,
            // but CooldownRemaining returns zero so the UI shows "ready" -- no harm.
            // Within-window timestamps resume the countdown from the restored value, so
            // a mid-cooldown Cerebro restart picks up where it left off instead of
            // resetting to "ready" (the legacy behavior).
            if (_splinterTracker != null && _sharedSettings.LastSplinterDropUtc != DateTime.MinValue)
                _splinterTracker.RestoreLastDrop(_sharedSettings.LastSplinterDropUtc);

            // Load the loot-hunt config from its own JSON file and publish as the
            // current.  The LootScannerPanel reads from Current on its first paint; the
            // live HuntCriteria reads on every loot scan.
            LootHuntConfig.ReplaceCurrent(LootHuntConfig.Load());
            // Same pattern for the buff watchlist -- BuffStripPanel reads
            // TrackedBuffsConfig.Current to filter the chip strip; the BuffTrackerPanel
            // reads / mutates it from its tab.  Loading here means the strip respects
            // the user's saved filter from the very first paint, not after the user
            // visits the tab.
            TrackedBuffsConfig.ReplaceCurrent(TrackedBuffsConfig.Load());

            // App-first layout: the main window is created and shown unconditionally; the
            // floating overlay is created up front too (so we can push DPS updates to it
            // even before it's visible), but Show()n only when the user opts in.
            _mainWindow        = new MainAppWindow(_sharedSettings);
            _overlayWindow     = new DpsOverlayWindow(_sharedSettings);
            _buffOverlayWindow = new BuffOverlayWindow(_sharedSettings);
            _cooldownOverlayWindow = new CooldownOverlayWindow();
            _cooldownOverlayWindow.SetTracker(_cooldownTracker);

            // Hand the live buff tracker to the Buff Tracker tab so its discovery lists
            // can poll it.  Must come after _mainWindow construction (panel must exist)
            // and after _buffTracker construction above (tracker must exist) -- ordering
            // already satisfies both.
            _mainWindow.SetBuffTracker(_buffTracker);
            _mainWindow.SetCooldownTracker(_cooldownTracker);

            initialBossOnly = _mainWindow.InitialBossOnlyPreference;

            WireWindowEvents(_mainWindow);
            WireWindowEvents(_overlayWindow);

            // Buff overlay's only out-bound event is "user closed me via Alt+F4 / WM_CLOSE"
            // -- when that happens we sync the header checkbox + persist ShowBuffOverlay=false
            // so the user's dismissal sticks across launches.  No other event wiring needed;
            // the window doesn't drive any DPS-meter actions.
            _buffOverlayWindow.HideRequested += () =>
            {
                _sharedSettings.ShowBuffOverlay = false;
                DpsOverlaySettingsFile.Save(_sharedSettings);
                _mainWindow?.SetShowBuffOverlayChecked(false);
            };

            // Cooldown overlay's user-close handler -- mirrors the buff overlay pattern.
            _cooldownOverlayWindow.HideRequested += () =>
            {
                _sharedSettings.ShowCooldownOverlay = false;
                DpsOverlaySettingsFile.Save(_sharedSettings);
                _mainWindow?.SetShowCooldownOverlayChecked(false);
            };

            _mainWindow.Show();
            if (_overlayVisible)
                _overlayWindow.ShowWithoutActivating();
            if (_sharedSettings.ShowBuffOverlay)
                _buffOverlayWindow.ShowWithoutActivating();
            if (_sharedSettings.ShowCooldownOverlay)
                _cooldownOverlayWindow.ShowWithoutActivating();

            // Global "I got a splinter" hotkey -- registered AFTER Show() so the main
            // window's HWND exists.  Failures (combo already owned by another app) are
            // logged but non-fatal; the in-app button and Settings tab "Arm Splinter
            // cooldown now" remain available either way.
            if (_sharedSettings.SplinterArmHotkeyEnabled)
            {
                _armSplinterHotkey = new GlobalHotkey(_mainWindow) { Diagnostic = AppendLog };
                // Hotkey arms with the default count of 1 -- the user pressing the binding
                // doesn't tell us the actual quantity, but the auto-detect path will fill in
                // the real number from the entity's stack count when it fires.
                _armSplinterHotkey.Pressed += () => ArmSplinterCooldownNow(1);
                _armSplinterHotkey.TryRegister(
                    _sharedSettings.SplinterArmHotkeyModifiers,
                    _sharedSettings.SplinterArmHotkeyVk);
            }

            _decayTimer = new DispatcherTimer(
                TimeSpan.FromMilliseconds(250),
                DispatcherPriority.Background,
                OnDecayTick,
                _uiDispatcher);
            _decayTimer.Start();
        });

        _meter.BossOnlyMode = initialBossOnly;

        AppendLog($"DpsOverlayPresenter started (sniffer running={_sniffer.IsRunning}, overlayVisible={_overlayVisible})");

        // Log the build version + informational version (which the csproj can stamp with the
        // commit SHA) on every Start.  This is purely a diagnostic for triage: "your log
        // shows stackCount=0 -- which build are you on?" becomes a grep instead of a
        // back-and-forth.  Cerebro builds embed AssemblyInformationalVersionAttribute
        // automatically; if not set, we fall back to the regular AssemblyVersion.
        try
        {
            var asm  = System.Reflection.Assembly.GetExecutingAssembly();
            var info = System.Reflection.CustomAttributeExtensions.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(asm);
            var fileVer = System.Diagnostics.FileVersionInfo.GetVersionInfo(asm.Location);
            AppendLog($"Cerebro build: AsmVer={asm.GetName().Version} InfoVer='{info?.InformationalVersion ?? "<none>"}' FileVer='{fileVer.FileVersion}'");
        }
        catch { /* version-stamp diagnostic is best-effort -- never block startup */ }
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
        // Live-view button / Settings tab button both arm with count=1 (the auto-detect path
        // is the canonical source for splinter quantity now).
        w.ArmSplinterCooldownRequested   += () => ArmSplinterCooldownNow(1);
        // Live re-bind of the global hotkey when the user changes it in Settings -- the
        // Settings panel has already persisted to _sharedSettings, but we own the OS-level
        // registration and need to drop + re-add the binding for it to take effect.
        w.SplinterArmHotkeyChanged       += OnSplinterArmHotkeyChanged;
        w.SplinterArmHotkeyEnabledChanged += OnSplinterArmHotkeyEnabledChanged;
        // The Live tab's right-click "View reports" switches tabs in-place (handled inside
        // MainAppWindow); the presenter doesn't need to do anything here.  No ViewReports
        // subscription means we avoid double-opening a standalone window.

        // Header "Show overlay" checkbox -- the canonical user-facing toggle in the new layout.
        w.ShowOverlayToggled += SetOverlayVisible;
        // Header "Show buff overlay" checkbox -- spawns / hides the dedicated floating
        // buff-tracker window.  Independent of the DPS overlay above.
        w.ShowBuffOverlayToggled += SetBuffOverlayVisible;
        w.ShowCooldownOverlayToggled += SetCooldownOverlayVisible;

        // "Show buffs and procs" -- forwards from the Settings tab to the live dashboard.
        // The Settings panel has already persisted the new value; the presenter just pushes
        // it down to the live dashboard so the UI updates without an app restart.
        w.ShowBuffPanelsToggled += enabled =>
        {
            _mainWindow?.SetBuffPanelsVisible(enabled);
            AppendLog($"DpsOverlayPresenter: buff panels visible = {enabled}");
        };

        // "Show DPS summary in overlay" -- forwards from the Settings tab to the floating
        // overlay window's DpsDisplayPanel.  Only affects the overlay; the main window's
        // Live tab still shows the DPS number in its summary card (different surface).
        w.ShowOverlayDpsSummaryToggled += enabled =>
        {
            _overlayWindow?.SetDpsSummaryVisible(enabled);
            AppendLog($"DpsOverlayPresenter: overlay DPS summary visible = {enabled}");
        };

        // "Lock overlay (click-through)" -- forwards from the Settings tab to the
        // floating overlay's WS_EX_TRANSPARENT bit so the lock takes effect
        // immediately.  Setting is persisted in DpsOverlaySettingsFile by the panel
        // itself before the event fires; the presenter just flips the runtime state.
        w.OverlayLockedToggled += locked =>
        {
            _overlayWindow?.SetLocked(locked);
            AppendLog($"DpsOverlayPresenter: overlay locked = {locked}");
        };
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

    /// <summary>Show or hide the floating buff overlay window.  Persists the new state to
    /// <see cref="DpsOverlaySettingsFile.ShowBuffOverlay"/> so the choice survives restart.
    /// Mirrors <see cref="SetOverlayVisible"/> but for the buff overlay -- the two windows
    /// are independently toggleable so users can run any combination (DPS only, buffs only,
    /// both, neither).</summary>
    public void SetBuffOverlayVisible(bool visible)
    {
        if (_buffOverlayWindow == null) return;
        if (visible)
            _buffOverlayWindow.ShowWithoutActivating();
        else
            _buffOverlayWindow.Hide();
        if (_sharedSettings != null)
        {
            _sharedSettings.ShowBuffOverlay = visible;
            DpsOverlaySettingsFile.Save(_sharedSettings);
        }
        AppendLog($"DpsOverlayPresenter: buff overlay visibility = {visible}");
    }

    /// <summary>Show or hide the floating cooldown overlay window.  Mirrors
    /// <see cref="SetBuffOverlayVisible"/>; the two overlays are independent so the
    /// user can opt into any combination.</summary>
    public void SetCooldownOverlayVisible(bool visible)
    {
        if (_cooldownOverlayWindow == null) return;
        if (visible)
            _cooldownOverlayWindow.ShowWithoutActivating();
        else
            _cooldownOverlayWindow.Hide();
        if (_sharedSettings != null)
        {
            _sharedSettings.ShowCooldownOverlay = visible;
            DpsOverlaySettingsFile.Save(_sharedSettings);
        }
        AppendLog($"DpsOverlayPresenter: cooldown overlay visibility = {visible}");
    }

    public void Stop()
    {
        if (!IsRunning) return;

        _uiDispatcher.Invoke(() =>
        {
            _decayTimer?.Stop();
            _decayTimer = null;
            try { _armSplinterHotkey?.Dispose(); } catch { }
            _armSplinterHotkey = null;
            // Belt-and-suspenders: flush the current in-memory settings to disk on shutdown.
            // Every individual toggle path (Show overlay, Show buff overlay, splinter
            // timestamp, etc.) already persists synchronously when it fires -- but a settings
            // mutation that happened JUST BEFORE quit might not have been saved yet if the
            // Save was queued behind a panel SaveAll race.  An unconditional save here costs
            // a single ~1KB JSON write at app exit and guarantees the latest state lands.
            // Best-effort: failure (file locked, disk full) is silently swallowed.
            if (_sharedSettings != null)
            {
                try { DpsOverlaySettingsFile.Save(_sharedSettings); }
                catch (Exception ex) { AppendLog($"DpsOverlayPresenter: shutdown settings save failed: {ex.Message}"); }
            }
            try { _overlayWindow?.CloseByPresenter(); } catch { }
            _overlayWindow = null;
            try { _buffOverlayWindow?.CloseByPresenter(); } catch { }
            _buffOverlayWindow = null;
            try { _cooldownOverlayWindow?.CloseByPresenter(); } catch { }
            _cooldownOverlayWindow = null;
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

        if (_buffTracker != null)
        {
            _buffTracker.Dispose();
            _buffTracker = null;
        }

        if (_cooldownTracker != null)
        {
            _cooldownTracker.Dispose();
            _cooldownTracker = null;
        }

        if (_lootScanner != null)
        {
            _lootScanner.Dispose();
            _lootScanner = null;
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

    /// <summary>True when an event-driven buff-snapshot push has already been scheduled
    /// on the UI dispatcher but hasn't run yet.  Used by <see cref="OnBuffChanged"/> to
    /// coalesce a burst of add/remove events (artifact procs in heavy combat can fire
    /// 10+ per second) into a single UI-thread invoke -- the queued invoke always reads
    /// the latest snapshot when it runs, so subsequent events are no-ops.  Reset to false
    /// inside the invoke before doing the snapshot read, so a new event that arrives
    /// while we're in the middle of pushing still triggers a follow-up.</summary>
    private int _buffPushPending;

    /// <summary>Push the current <see cref="_buffTracker"/> active-buffs snapshot to the
    /// main window's dashboard strip, the BuffStats panel, and the floating buff overlay.
    /// Idempotent -- safe to call as often as desired (no-op when the tracker isn't
    /// constructed yet or the main window has been torn down).
    ///
    /// <para>Routed through both the periodic decay tick (every 250 ms, drives countdown
    /// smoothness) and <see cref="OnBuffChanged"/> (fires the instant a condition is
    /// added/removed, drives state-pill latency for transient buffs like Nightcrawler's
    /// 1.5-second Bamf stealth window).</para></summary>
    private void PushBuffSnapshot()
    {
        if (_buffTracker == null || _mainWindow == null) return;
        var snapshot = _buffTracker.GetActiveBuffs();
        var buffNow  = DateTime.UtcNow;
        _mainWindow.UpdateBuffs(snapshot, buffNow);
        _mainWindow.UpdateBuffStats(_buffTracker);
        // Same snapshot fed into the floating buff overlay -- when it's not visible this
        // is still cheap (the BuffStripPanel rebuilds its internal ItemsControl from
        // the new snapshot but no rendering happens until the window becomes visible).
        _buffOverlayWindow?.UpdateBuffs(snapshot, buffNow);
        // Cooldown overlay tick.  Stateless from the overlay's perspective -- it
        // reads its data from CooldownTracker on each call, so we just need to
        // poke it with the current wall-clock.  Cheap when hidden (Visibility check
        // inside ApplyClickThrough / SetEditMode short-circuits).
        _cooldownOverlayWindow?.UpdateCooldowns(buffNow);
    }

    /// <summary>Fires on the SNIFFER thread the instant a buff is added or removed.
    /// Marshals to the UI dispatcher to push a fresh snapshot down to the strip / overlay,
    /// coalescing rapid fires so we don't queue 50 invokes during heavy combat -- the
    /// latest invoke always wins because <see cref="PushBuffSnapshot"/> reads the live
    /// tracker state when it runs.</summary>
    private void OnBuffChanged(ActiveBuff _, bool __)
    {
        // CAS-style guard: only enqueue an invoke when one isn't already pending.  We
        // clear the flag inside the invoke before doing the actual push, so any further
        // events that fire while we're in PushBuffSnapshot still get picked up by a
        // follow-up invoke.
        if (System.Threading.Interlocked.Exchange(ref _buffPushPending, 1) != 0) return;
        _uiDispatcher.BeginInvoke(new Action(() =>
        {
            System.Threading.Interlocked.Exchange(ref _buffPushPending, 0);
            PushBuffSnapshot();
        }));
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

        // Push the latest self-owner id into the buff tracker.  The meter's identification
        // happens at session start (via NetMessageLocalPlayer or self-pinning) and on every
        // hero swap / region change; the tracker handles 0 by ignoring events and clears
        // its state on any non-zero transition, so this is safe to call every tick (no-op
        // unless the value actually changed).
        if (_buffTracker != null)
            _buffTracker.SelfOwnerId = _meter.LikelySelfOwnerId;
        if (_cooldownTracker != null)
            _cooldownTracker.SelfOwnerId = _meter.LikelySelfOwnerId;

        // Push the latest active-buff snapshot to the dashboard's BuffStrip.  We do this
        // every tick (4 Hz) rather than only on BuffChanged events so the chip countdowns
        // tick smoothly between server-driven add/remove events; the strip's rebuild is
        // cheap (typically 5-15 chips total post-classification).
        //
        // Also push the tracker itself to the BuffStats panel so it can recompute the
        // option-A stat tiles ("+%damage from active buffs" etc).  Same tick rate; the
        // panel asks the tracker for a one-pass aggregate of the curated PropertyEnum set,
        // so the cost is the same order as the buff-strip rebuild.
        PushBuffSnapshot();

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
                _splinterTracker.TotalSplintersThisSession,
                justDropped);
            _mainWindow?.UpdateSplinterStatus(
                _splinterTracker.IsCooldownActive,
                _splinterTracker.RemainingCooldown,
                _splinterTracker.DropCount,
                _splinterTracker.TotalSplintersThisSession,
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
            // PersistOverlay short-circuits the foreground check: multi-monitor users park
            // the overlay on a secondary display and want it readable while focused on a
            // browser / Discord / etc. on another monitor.  Single-monitor users leave it
            // off (the default), keeping the original auto-hide-when-not-game behaviour.
            bool shouldShow = _sharedSettings?.PersistOverlay == true
                || (ShouldBeVisible?.Invoke() ?? true);
            if (shouldShow != _lastVisibilityDecision)
            {
                _lastVisibilityDecision = shouldShow;
                _overlayWindow.Visibility = shouldShow ? Visibility.Visible : Visibility.Collapsed;
                string fg = GameForegroundWatcher.LastForegroundProcessName;
                string reason = _sharedSettings?.PersistOverlay == true ? "persist=on" : $"foreground='{fg}'";
                AppendLog(shouldShow
                    ? $"DpsOverlayPresenter: overlay shown — {reason}"
                    : $"DpsOverlayPresenter: overlay hidden — {reason}");
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
            ? _bossMeter.GetTopHeroesByEncounterShare(MaxLeaderboardRows)
            : Array.Empty<DpsMeter.HeroShareEntry>();
    }

    private static IReadOnlyList<DpsMeter.HeroShareEntry> SelectTopHeroesForOverlay(
        DpsMeter meter,
        bool bossOnly,
        DpsMeter.EncounterSnapshot encounter)
    {
        if (!bossOnly)
            return meter.GetTopHeroesBySessionShare(MaxLeaderboardRows);
        if (encounter.IsActive || encounter.IsEnded)
            return meter.GetTopHeroesByEncounterShare(MaxLeaderboardRows);
        return Array.Empty<DpsMeter.HeroShareEntry>();
    }

    private static void AppendLog(string line)
    {
        if (!DpsOverlaySettingsFile.IsLoggingEnabled) return;
        // Verbose-noise filter: when verbose-diagnostics is OFF, drop the high-volume
        // patterns that the network sniffer / DPS meter / per-tick state dumps would
        // otherwise flood the file with.  These are debug-only signals -- splinter drops,
        // snapshot saves, app lifecycle, errors all still log.  See IsNoiseLine for the
        // exact list of dropped prefixes / substrings.
        if (!DpsOverlaySettingsFile.IsVerboseDiagnosticsEnabled && IsNoiseLine(line)) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DiagnosticLogPath)!);
            File.AppendAllText(DiagnosticLogPath, $"[{DateTime.UtcNow:HH:mm:ss.fff}] {line}{Environment.NewLine}");
        }
        catch { }
    }

    /// <summary>Returns true if a log line is part of the curated "verbose only" set --
    /// patterns the components emit at high volume that are only useful when actively
    /// debugging that specific subsystem.  Curated by reading real-world logs and picking
    /// out the lines that dominate the file size without contributing to high-level
    /// understanding of what the app is doing.  New noisy patterns should be added here
    /// when they're discovered; the alternative (changing every Diagnostic call site) is
    /// way more invasive for a debug-tier toggle.</summary>
    private static bool IsNoiseLine(string line)
    {
        // Cheap substring checks -- ordered roughly by frequency in actual logs so the
        // common case short-circuits early.  Each pattern is a SUBSTRING (not a regex) so
        // a class that emits double-logged variants (some components log the same event
        // twice, once via their own Diagnostic and once via the host's) catches both.
        return line.Contains("ModifyCommunityMember")        // every player community update
            || line.Contains("boss-filter drop")              // every non-boss damage event
            || line.Contains("prototype-cache cleanup")       // every mob death (verbose paragraph)
            || line.Contains("DpsMeter.State:")               // periodic state dump (every few sec)
            || line.Contains("PowerResultStats:")             // 5s sniffer-health heartbeat
            || line.Contains("EntityCreate[Avatar]")          // every avatar load
            || line.Contains("AOI-nearby add")                // every nearby player
            || line.Contains("paired avatar entityId")        // player-name resolver internals
            || line.Contains("unknown non-avatar EntityCreate") // discovery log (one per uniq idx)
            || line.Contains("queued hero avatar")            // hero-pairing internals
            || line.Contains("learned nickname")              // nickname resolver internals
            || line.Contains("community-slot hero")           // hero-from-community internals
            || line.Contains("PowerResult#")                  // per-damage-event verbose trace
            ;
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

    /// <summary>Manually start the splinter cooldown from this moment.  Useful when
    /// the user saw a splinter drop in-game that the auto-detection missed -- e.g. it dropped
    /// before they launched the meter, the proto-index match failed because of a game patch,
    /// or the EntityCreate happened during a region-load packet storm that exceeded the
    /// per-session discovery log cap.</summary>
    /// <summary>Manually arm the splinter cooldown.  <paramref name="splinterCount"/> is
    /// the actual quantity of splinters the user reports receiving from this drop event
    /// (the game's "+9 Eternity Splinters!" popup); contributes to the session running total
    /// in <see cref="EternitySplinterTracker.TotalSplintersThisSession"/>.  Defaults to 1
    /// for callers that don't know or don't care (the global hotkey, the Settings tab's
    /// quick-arm button).</summary>
    public void ArmSplinterCooldownNow(int splinterCount = 1)
    {
        _splinterTracker?.ArmFromNow(splinterCount);
        AppendLog($"DpsOverlayPresenter: splinter cooldown armed manually by user (count={splinterCount})");
    }

    /// <summary>Live re-register the global hotkey with a new (modifiers, vk) combo.  Called
    /// when the user changes the binding via Settings -> Rebind.  The Settings panel has
    /// already persisted the new values to _sharedSettings; this method just drops the old
    /// OS-level registration and adds a new one.  No-op if the hotkey is currently disabled
    /// (toggle is off) -- in that case the new combo will be picked up when the user re-enables.</summary>
    private void OnSplinterArmHotkeyChanged(uint modifiers, uint vk)
    {
        if (_sharedSettings?.SplinterArmHotkeyEnabled != true) return;
        if (_armSplinterHotkey == null)
        {
            // Toggle was on but registration hadn't been built yet (shouldn't happen in
            // practice -- Start() builds it -- but defensive).
            if (_mainWindow == null) return;
            _armSplinterHotkey = new GlobalHotkey(_mainWindow) { Diagnostic = AppendLog };
            _armSplinterHotkey.Pressed += () => ArmSplinterCooldownNow(1);
        }
        _armSplinterHotkey.TryRegister(modifiers, vk);
    }

    /// <summary>Handle the user toggling the hotkey on or off in Settings.  When turning
    /// ON, register with the current persisted combo; when turning OFF, unregister so the
    /// OS-level shortcut stops firing.</summary>
    private void OnSplinterArmHotkeyEnabledChanged(bool enabled)
    {
        if (enabled)
        {
            if (_mainWindow == null || _sharedSettings == null) return;
            if (_armSplinterHotkey == null)
            {
                _armSplinterHotkey = new GlobalHotkey(_mainWindow) { Diagnostic = AppendLog };
                _armSplinterHotkey.Pressed += () => ArmSplinterCooldownNow(1);
            }
            _armSplinterHotkey.TryRegister(
                _sharedSettings.SplinterArmHotkeyModifiers,
                _sharedSettings.SplinterArmHotkeyVk);
        }
        else
        {
            _armSplinterHotkey?.Unregister();
            AppendLog("DpsOverlayPresenter: splinter-arm hotkey disabled by user");
        }
    }

    /// <summary>Play a splinter alert sound, selecting the right configured file based on
    /// which event fired.  Two event types, two file/volume pairs:
    /// <list type="bullet">
    ///   <item><c>"drop"</c> -- uses <c>SplinterDropSoundPath</c> when set, falls back to
    ///         <c>SplinterCooldownSoundPath</c> (so existing single-sound configurations
    ///         keep working unchanged after the split).</item>
    ///   <item><c>"cooldown-expired"</c> -- always uses <c>SplinterCooldownSoundPath</c>.</item>
    /// </list>
    /// Both paths null/empty falls through to the Windows asterisk via
    /// <c>SplinterCooldownSoundPlayer.Play</c>'s built-in fallback.  Gated on the shared
    /// <c>SplinterCooldownSoundEnabled</c> master toggle -- one switch controls both events.</summary>
    private void PlaySplinterAlert(string contextForLog)
    {
        if (_sharedSettings?.SplinterCooldownSoundEnabled != true) return;

        // Pick the (path, volume) pair for this event.  Drop event prefers its own
        // configured sound; falls back to the cooldown sound when not set so legacy
        // single-sound configs preserve their behavior.
        string? path;
        double volume;
        if (contextForLog == "drop" && !string.IsNullOrWhiteSpace(_sharedSettings.SplinterDropSoundPath))
        {
            path   = _sharedSettings.SplinterDropSoundPath;
            volume = _sharedSettings.SplinterDropSoundVolume;
        }
        else
        {
            path   = _sharedSettings.SplinterCooldownSoundPath;
            volume = _sharedSettings.SplinterCooldownSoundVolume;
        }

        bool playedCustom = SplinterCooldownSoundPlayer.Play(path, volume);
        AppendLog(playedCustom
            ? $"DpsOverlayPresenter: played custom splinter sound for {contextForLog} ('{path}', vol={volume:0.00})"
            : $"DpsOverlayPresenter: played system asterisk for splinter {contextForLog} (vol slider doesn't apply -- Windows controls)");
    }

    /// <summary>Play the hunt-match alert sound configured in <see cref="LootHuntConfig"/>.
    /// Reuses the splinter sound infrastructure (<c>SplinterCooldownSoundPlayer.Play</c>)
    /// which handles the WPF MediaPlayer lifecycle, volume scaling, and asterisk fallback.
    /// Caller must already be on the UI dispatcher (we're triggered from a BeginInvoke).</summary>
    private void PlayHuntMatchAlert(HuntMatchEventArgs args)
    {
        var cfg = LootHuntConfig.Current;
        if (!cfg.SoundEnabled) return;

        bool playedCustom = SplinterCooldownSoundPlayer.Play(cfg.SoundPath, cfg.SoundVolume);
        AppendLog(playedCustom
            ? $"DpsOverlayPresenter: played hunt-match sound (entityId={args.EntityId} matched [{string.Join(", ", args.MatchedAffixes)}], '{cfg.SoundPath}', vol={cfg.SoundVolume:0.00})"
            : $"DpsOverlayPresenter: played system asterisk for hunt match (entityId={args.EntityId})");
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
