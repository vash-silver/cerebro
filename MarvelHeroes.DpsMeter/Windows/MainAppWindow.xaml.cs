using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using MarvelHeroesComporator.NetworkSniffer;
using MarvelHeroes.DpsMeter.Services;
using DpsMeterClass = MarvelHeroes.DpsMeter.Services.DpsMeter;

namespace MarvelHeroes.DpsMeter.Windows;

/// <summary>
/// The application's primary window in the new app-first layout (commit "Main-app GUI rework").
/// Hosts the existing <c>DpsDisplayPanel</c> in a "Live" tab and the existing
/// <c>ReportViewerPanel</c> in a "Reports" tab so the user no longer has to right-click the
/// overlay to reach Reports.
///
/// <para>A header checkbox lets the user toggle the floating overlay independently of the
/// main window -- the overlay is now optional rather than the only UI surface.  When the
/// checkbox is on, both windows exist simultaneously and the presenter pushes DPS / splinter
/// updates to both via the same code path it already used for the legacy overlay+live-window
/// dual-display.</para>
///
/// <para>The forwarded events (BossOnlyToggled, ClearDpsRequested, ...) match the existing
/// <c>DpsLiveWindow</c> signatures so <c>DpsOverlayPresenter.WireWindowEvents</c> can subscribe
/// without any signature changes.  A new <see cref="ShowOverlayToggled"/> event surfaces the
/// checkbox state up to the presenter so it can show or hide the overlay window in lockstep.</para>
/// </summary>
public partial class MainAppWindow : Window
{
    public bool InitialBossOnlyPreference { get; private set; }

    // Forwarded panel events -- same set DpsLiveWindow / DpsOverlayWindow expose so the
    // presenter's wiring code doesn't have to special-case this window.
    public event Action<bool>?   BossOnlyToggled;
    /// <summary>Forwarded from the Settings tab's "Show buffs and procs" checkbox.  The
    /// presenter listens to flip both buff-tracking surfaces on/off in real time.</summary>
    public event Action<bool>?   ShowBuffPanelsToggled;
    /// <summary>Forwarded from the Settings tab's "Show DPS summary in overlay" checkbox.
    /// The presenter listens and pushes the new visibility down to the floating overlay's
    /// DpsDisplayPanel.</summary>
    public event Action<bool>?   ShowOverlayDpsSummaryToggled;
    /// <summary>Forwarded from the Settings tab's "Lock overlay (click-through)" checkbox.
    /// The presenter listens and flips <c>WS_EX_TRANSPARENT</c> on the floating DPS overlay
    /// window so click-through takes effect immediately.</summary>
    public event Action<bool>?   OverlayLockedToggled;
    // SwitchModeRequested kept for API parity with the overlay window so the presenter's
    // WireWindowEvents overload signatures stay symmetric.  The main window itself has no
    // way to fire it (no "switch mode" surface) -- suppress the never-used warning.
#pragma warning disable CS0067
    public event Action?         SwitchModeRequested;
#pragma warning restore CS0067
    public event Action<IReadOnlyList<DpsMeterClass.HeroShareEntry>?,
                        DpsMeterClass.EncounterSnapshot,
                        IReadOnlyList<DpsMeterClass.PowerBreakdownEntry>?>? SaveSnapshotRequested;
    public event Action?         ClearDpsRequested;
    public event Action?         ResetMaxHitRecordRequested;
    public event Action?         ResetSplinterCooldownRequested;
    public event Action?         ArmSplinterCooldownRequested;
    public event Action<uint, uint>? SplinterArmHotkeyChanged;
    public event Action<bool>?       SplinterArmHotkeyEnabledChanged;
    /// <summary>Forwarded from the Settings tab when the user rebinds the global
    /// "toggle all overlays" hotkey.  Presenter listens to re-register the system
    /// hotkey.</summary>
    public event Action<uint, uint>? ToggleOverlaysHotkeyChanged;
    /// <summary>Forwarded from the Settings tab when the user toggles the
    /// "toggle all overlays" hotkey enable / disable checkbox.</summary>
    public event Action<bool>?       ToggleOverlaysHotkeyEnabledChanged;
    /// <summary>Forwarded from the Settings tab's overlay-scale slider so the
    /// presenter can apply the new scale to the live DPS overlay without a
    /// restart.</summary>
    public event Action<double>?     OverlayScaleChanged;
    // ViewReportsRequested is kept in the API surface for signature parity with
    // DpsLiveWindow / DpsOverlayWindow, but the main window short-circuits the right-click
    // "View reports" menu by switching to the Reports tab in-place rather than asking the
    // presenter to spawn a separate window.  Suppress the never-used warning since
    // subscribers are intentionally absent on this window.
#pragma warning disable CS0067
    public event Action?         ViewReportsRequested;
#pragma warning restore CS0067

    /// <summary>Raised when the user ticks / unticks the "Show overlay" header checkbox.
    /// The presenter listens to this and shows / hides the floating overlay window
    /// accordingly, then persists the new state to <c>dps-overlay.json</c>.</summary>
    public event Action<bool>?   ShowOverlayToggled;

    /// <summary>Raised when the user ticks / unticks the "Show buff overlay" header
    /// checkbox.  Mirrors <see cref="ShowOverlayToggled"/> but drives the floating buff
    /// overlay (a separate window from the DPS overlay).</summary>
    public event Action<bool>?   ShowBuffOverlayToggled;

    /// <summary>Raised when the user ticks / unticks the "Show cooldown overlay" header
    /// checkbox.  Presenter listens to spawn / hide the floating cooldown overlay window
    /// and persist <c>ShowCooldownOverlay</c> to <c>dps-overlay.json</c>.</summary>
    public event Action<bool>?   ShowCooldownOverlayToggled;

    private bool _suppressShowOverlayCheckboxEvents;
    private bool _suppressPersistOverlayCheckboxEvents;
    private bool _suppressShowBuffOverlayCheckboxEvents;
    private bool _suppressShowCooldownOverlayCheckboxEvents;
    /// <summary>Stashed reference to the shared settings object so the persist-overlay
    /// checkbox handler can mutate + persist without going through an event-roundtrip to
    /// the presenter (the presenter polls the field every decay tick, so a direct write +
    /// Save is sufficient -- no event needed to flush a stale visibility decision).</summary>
    private DpsOverlaySettingsFile? _settings;
    private bool _closingByPresenter;

    public MainAppWindow(DpsOverlaySettingsFile settings)
    {
        InitializeComponent();

        // LiveDashboardPanel has no menu / settings of its own -- it's a pure data display.
        // We snapshot the persisted "boss only" preference here so the presenter can set
        // _meter.BossOnlyMode at startup without going through the now-absent panel hook.
        InitialBossOnlyPreference = settings.BossDpsOnly;
        _settings = settings;
        SettingsTab.Initialize(settings);
        SetShowOverlayChecked(settings.ShowOverlay);
        // Persist-overlay checkbox initial state -- bootstrap-suppressed like Show overlay
        // so we don't fire the toggle event back at the presenter just from setting the
        // initial state from disk.
        _suppressPersistOverlayCheckboxEvents = true;
        try { PersistOverlayCheckbox.IsChecked = settings.PersistOverlay; }
        finally { _suppressPersistOverlayCheckboxEvents = false; }
        // Same bootstrap suppression for the buff-overlay checkbox.
        SetShowBuffOverlayChecked(settings.ShowBuffOverlay);
        SetShowCooldownOverlayChecked(settings.ShowCooldownOverlay);
        // Honour the persisted buff-panels preference at startup -- the LiveDashboardPanel
        // defaults to visible, so we only need to push down the explicit-off case here.
        // (Pushing down "on" would be a no-op but keeps the code path symmetric.)
        LivePanel.SetBuffPanelsVisible(settings.ShowBuffPanels);

        // Settings tab raises the same events the overlay's right-click menu does so the
        // presenter only needs to subscribe once and either UI surface can drive the action.
        SettingsTab.BossOnlyToggled                 += v  => BossOnlyToggled?.Invoke(v);
        SettingsTab.ShowBuffPanelsToggled           += v  => ShowBuffPanelsToggled?.Invoke(v);
        SettingsTab.ShowOverlayDpsSummaryToggled    += v  => ShowOverlayDpsSummaryToggled?.Invoke(v);
        SettingsTab.OverlayLockedToggled            += v  => OverlayLockedToggled?.Invoke(v);
        SettingsTab.ClearDpsRequested               += () => ClearDpsRequested?.Invoke();
        SettingsTab.ResetMaxHitRecordRequested      += () => ResetMaxHitRecordRequested?.Invoke();
        SettingsTab.ResetSplinterCooldownRequested  += () => ResetSplinterCooldownRequested?.Invoke();
        SettingsTab.ArmSplinterCooldownRequested    += () => ArmSplinterCooldownRequested?.Invoke();
        // Hotkey rebind / enable / disable -- routed up to the presenter which owns the
        // Win32 RegisterHotKey lifecycle.  The Settings panel itself only persists the
        // new binding; the presenter does the live re-register.
        SettingsTab.SplinterArmHotkeyChanged        += (m, v) => SplinterArmHotkeyChanged?.Invoke(m, v);
        SettingsTab.SplinterArmHotkeyEnabledChanged += en     => SplinterArmHotkeyEnabledChanged?.Invoke(en);
        SettingsTab.ToggleOverlaysHotkeyChanged        += (m, v) => ToggleOverlaysHotkeyChanged?.Invoke(m, v);
        SettingsTab.ToggleOverlaysHotkeyEnabledChanged += en     => ToggleOverlaysHotkeyEnabledChanged?.Invoke(en);
        SettingsTab.OverlayScaleChanged                += s      => OverlayScaleChanged?.Invoke(s);

        // The dashboard's "Save snapshot" button forwards a snapshot of the current tick
        // (top heroes / encounter state / power breakdown all cached on the last UpdateDps).
        LivePanel.SaveSnapshotRequested += (h, enc, p) => SaveSnapshotRequested?.Invoke(h, enc, p);

        // Dashboard's "I got a splinter" button -- routes to the same presenter action as the
        // Settings tab's "Arm Splinter cooldown now" button and the (future) global hotkey.
        LivePanel.ArmSplinterCooldownRequested += () => ArmSplinterCooldownRequested?.Invoke();

        // Auto-size? No -- the main app should remember its user-resized geometry once we
        // add that.  For now, the XAML's Width/Height defaults apply.
        Closing += (_, _) =>
        {
            if (_closingByPresenter) return;
            // Normal window close = quit the app.  The overlay is auxiliary; if the user
            // closes the main window we shut everything down (matches Windows convention).
            // The presenter's Stop() will fire from App.OnExit and tear down cleanly.  No
            // per-panel SaveAll() needed -- the Settings tab persists each toggle as it
            // happens, and the LiveDashboardPanel has no settings to save.
            Application.Current?.Shutdown();
        };

        // Kick off the GitHub update check once the window is up.  Deliberately fired
        // from Loaded (not the ctor) so a slow/blocked network doesn't delay window
        // paint.  Best-effort: any failure (offline, GitHub rate-limited, etc.) is
        // silently swallowed inside UpdateChecker.CheckAsync.
        Loaded += async (_, _) =>
        {
            try
            {
                var result = await Services.UpdateChecker.CheckAsync();
                ApplyUpdateCheckResult(result);
            }
            catch { /* extra-defensive: never let the update check crash startup */ }
        };
    }

    /// <summary>Cached most-recent update result, used so the manual "Check for updates"
    /// button in Settings can pop a toast with the same data the startup check found.</summary>
    private Services.UpdateChecker.Result _lastUpdateResult = Services.UpdateChecker.Result.None;

    /// <summary>Show / hide the update banner based on a CheckAsync result, honoring the
    /// user's persisted dismissal so a banner the user has already clicked "✕" on doesn't
    /// reappear until a NEWER release supersedes it.</summary>
    private void ApplyUpdateCheckResult(Services.UpdateChecker.Result result)
    {
        _lastUpdateResult = result;
        if (!result.Available)
        {
            UpdateBanner.Visibility = Visibility.Collapsed;
            return;
        }
        // Dismissal: if the user previously clicked ✕ on this exact tag, stay hidden.
        // Any newer tag wins (the user's dismissed-version is overwritten only on click,
        // and the result we have here is by definition strictly newer than local, so a
        // mismatch between "dismissed tag" and "current tag" means a fresh release).
        string? dismissed = _settings?.DismissedUpdateVersion;
        if (!string.IsNullOrEmpty(dismissed) && string.Equals(dismissed, result.TagName, StringComparison.OrdinalIgnoreCase))
        {
            UpdateBanner.Visibility = Visibility.Collapsed;
            return;
        }
        UpdateBannerText.Text =
            $"Cerebro v{result.DisplayVersion} is available  (you have v{Services.CerebroVersion.DisplayVersion})";
        UpdateBanner.Visibility = Visibility.Visible;
    }

    /// <summary>"Update now" -- the primary action: download the release zip from
    /// GitHub, verify its SHA-256, extract <c>Cerebro.exe</c>, write a bootstrap
    /// PowerShell script, launch the script, and quit so the script can swap the
    /// running EXE.  Progress (download bytes, verify, extract) is reported into
    /// the banner.  On any failure the banner reverts to an error state with a
    /// fallback "Open release page" button.</summary>
    private async void UpdateBannerInstall_Click(object sender, RoutedEventArgs e)
    {
        // Guard against double-clicks while a previous install is in flight --
        // disable the install button + dismiss button while we work; the latter
        // re-enables on failure so the user can still dismiss the failed banner.
        UpdateBannerInstallButton.IsEnabled = false;
        UpdateBannerDismissButton.IsEnabled = false;
        UpdateBannerOpenPageButton.Visibility = Visibility.Collapsed;
        UpdateBannerProgress.Visibility = Visibility.Visible;
        UpdateBannerProgress.IsIndeterminate = true;
        UpdateBannerText.Text = "Preparing update…";

        var progress = new Progress<Services.UpdateInstaller.DownloadProgress>(p =>
        {
            // Marshal already happens via Progress<T>; just update the UI.
            if (p.TotalBytes > 0)
            {
                UpdateBannerProgress.IsIndeterminate = false;
                UpdateBannerProgress.Value = (double)p.BytesReceived / p.TotalBytes * 100.0;
                double mbRecv = p.BytesReceived / 1024.0 / 1024.0;
                double mbTot  = p.TotalBytes    / 1024.0 / 1024.0;
                UpdateBannerText.Text = $"{p.Status}  {mbRecv:0.0} / {mbTot:0.0} MB";
            }
            else
            {
                UpdateBannerProgress.IsIndeterminate = true;
                UpdateBannerText.Text = p.Status;
            }
        });

        var outcome = await Services.UpdateInstaller.InstallAsync(_lastUpdateResult, progress);

        if (outcome.Success)
        {
            // Bootstrap is launched; shut Cerebro down so the file lock releases
            // and the PowerShell script can swap the EXE.  The script will
            // relaunch Cerebro automatically.
            UpdateBannerText.Text = "Restarting Cerebro to finish the update…";
            UpdateBannerProgress.IsIndeterminate = true;
            // Tiny pause so the user can read the status before the window closes.
            await Task.Delay(400);
            Application.Current.Shutdown();
            return;
        }

        // Failure path: surface the error, re-enable buttons, expose the
        // "Open release page" fallback so the user can grab the zip manually.
        UpdateBannerProgress.Visibility = Visibility.Collapsed;
        UpdateBannerProgress.IsIndeterminate = false;
        UpdateBannerInstallButton.IsEnabled = true;
        UpdateBannerDismissButton.IsEnabled = true;
        UpdateBannerOpenPageButton.Visibility = string.IsNullOrEmpty(_lastUpdateResult.HtmlUrl)
            ? Visibility.Collapsed
            : Visibility.Visible;
        UpdateBannerText.Text = "Update failed: " + outcome.ErrorMessage;
    }

    /// <summary>Manual fallback when self-update fails: open the GitHub release page
    /// in the user's default browser so they can grab the zip the old way.</summary>
    private void UpdateBannerOpenPage_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_lastUpdateResult.HtmlUrl)) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName        = _lastUpdateResult.HtmlUrl,
                UseShellExecute = true,
            });
        }
        catch { /* shell-execute can fail on locked-down systems; non-fatal */ }
    }

    /// <summary>Persist the dismissed tag so the banner stays hidden across launches
    /// until a newer release supersedes this one.</summary>
    private void UpdateBannerDismiss_Click(object sender, RoutedEventArgs e)
    {
        UpdateBanner.Visibility = Visibility.Collapsed;
        if (_settings == null) return;
        _settings.DismissedUpdateVersion = _lastUpdateResult.TagName ?? "";
        DpsOverlaySettingsFile.Save(_settings);
    }

    /// <summary>Triggered by the Settings tab's "Check for updates" button.  Runs a fresh
    /// network call so the user can force a re-check after dismissing or after a known
    /// upstream release.  Returns the result so the Settings tab can show a toast/dialog
    /// rather than relying on the silent banner.</summary>
    internal async Task<Services.UpdateChecker.Result> CheckForUpdatesNowAsync()
    {
        var result = await Services.UpdateChecker.CheckAsync();
        ApplyUpdateCheckResult(result);
        return result;
    }

    /// <summary>Called by the presenter during teardown so the Closing handler doesn't
    /// recursively try to shut down the app while we're already shutting down.</summary>
    public void CloseByPresenter()
    {
        _closingByPresenter = true;
        Close();
    }

    /// <summary>Sync the checkbox's visual state to the underlying boolean WITHOUT firing
    /// the Toggled event back at the presenter (avoids an infinite ping-pong when the
    /// presenter pushes a new state down to the window).</summary>
    public void SetShowOverlayChecked(bool value)
    {
        if (ShowOverlayCheckbox.IsChecked == value) return;
        _suppressShowOverlayCheckboxEvents = true;
        try { ShowOverlayCheckbox.IsChecked = value; }
        finally { _suppressShowOverlayCheckboxEvents = false; }
    }

    /// <summary>Mirror of <see cref="SetShowOverlayChecked"/> for the buff overlay's header
    /// checkbox.  Used by the presenter when the user closes the buff overlay window via
    /// Alt+F4 / WM_CLOSE -- we sync the checkbox visual back to false so the dismissal is
    /// reflected everywhere.</summary>
    public void SetShowBuffOverlayChecked(bool value)
    {
        if (ShowBuffOverlayCheckbox.IsChecked == value) return;
        _suppressShowBuffOverlayCheckboxEvents = true;
        try { ShowBuffOverlayCheckbox.IsChecked = value; }
        finally { _suppressShowBuffOverlayCheckboxEvents = false; }
    }

    /// <summary>Mirror of <see cref="SetShowOverlayChecked"/> for the cooldown overlay
    /// header checkbox.  Called by the presenter when the user closes the cooldown
    /// overlay via Alt+F4 / WM_CLOSE so the dismissal is reflected here too.</summary>
    public void SetShowCooldownOverlayChecked(bool value)
    {
        if (ShowCooldownOverlayCheckbox.IsChecked == value) return;
        _suppressShowCooldownOverlayCheckboxEvents = true;
        try { ShowCooldownOverlayCheckbox.IsChecked = value; }
        finally { _suppressShowCooldownOverlayCheckboxEvents = false; }
    }

    /// <summary>Update the small badge on the right of the header.  Wired by the presenter
    /// from the same data the live overlay shows: hero name, splinter cooldown state, etc.
    /// Keep it short -- the badge is informational, not a settings surface.</summary>
    public void SetStatusBadge(string text)
        => StatusBadge.Text = text ?? string.Empty;

    /// <summary>Update the small status strip at the bottom of the window (Npcap status,
    /// active port, packet counters, etc.).  Truncate or wrap on the caller's side -- this
    /// just sets the text verbatim.</summary>
    public void SetFooterStatus(string text)
        => FooterStatus.Text = text ?? string.Empty;

    private void ShowOverlayCheckbox_OnChecked(object sender, RoutedEventArgs e)
    {
        if (_suppressShowOverlayCheckboxEvents) return;
        ShowOverlayToggled?.Invoke(true);
    }

    private void ShowOverlayCheckbox_OnUnchecked(object sender, RoutedEventArgs e)
    {
        if (_suppressShowOverlayCheckboxEvents) return;
        ShowOverlayToggled?.Invoke(false);
    }

    /// <summary>"Persist overlay" toggle.  Writes directly to the shared settings file and
    /// persists -- no event roundtrip needed because the presenter polls
    /// <c>_sharedSettings.PersistOverlay</c> every decay tick (250 ms), so a flag flip is
    /// visible within one tick.  Same Checked / Unchecked handler since the only state
    /// it cares about is the IsChecked bool.</summary>
    private void PersistOverlayCheckbox_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressPersistOverlayCheckboxEvents || _settings == null) return;
        _settings.PersistOverlay = PersistOverlayCheckbox.IsChecked == true;
        DpsOverlaySettingsFile.Save(_settings);
    }

    /// <summary>"Show buff overlay" header checkbox.  Forwards through
    /// <see cref="ShowBuffOverlayToggled"/> so the presenter can spawn / hide the floating
    /// buff window and persist the new state.  Single handler for both Checked and
    /// Unchecked since the toggle event-arg is just the bool.</summary>
    private void ShowBuffOverlayCheckbox_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressShowBuffOverlayCheckboxEvents) return;
        ShowBuffOverlayToggled?.Invoke(ShowBuffOverlayCheckbox.IsChecked == true);
    }

    /// <summary>"Show cooldown overlay" header checkbox.  Mirrors the buff-overlay
    /// toggle -- forwards through <see cref="ShowCooldownOverlayToggled"/> for the
    /// presenter to spawn / hide the floating cooldown window.</summary>
    private void ShowCooldownOverlayCheckbox_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressShowCooldownOverlayCheckboxEvents) return;
        ShowCooldownOverlayToggled?.Invoke(ShowCooldownOverlayCheckbox.IsChecked == true);
    }

    // ── Forwarded UpdateDps / UpdateSplinterStatus ────────────────────────────────────────────
    //
    // Mirrors DpsLiveWindow.UpdateDps exactly so DpsOverlayPresenter.PushUpdateToWindows can
    // call this without any signature changes (the presenter just keeps an extra reference).

    public void UpdateDps(
        double dps,
        long totalDamage60s,
        long totalDamageSession,
        ulong ownerEntityId,
        uint maxSingleHit,
        uint maxSingleHitSession,
        uint maxSingleHitEncounter,
        string heroDisplayName,
        string bossDisplayName,
        bool bossOnlyMode,
        IReadOnlyList<DpsMeterClass.HeroShareEntry>? topHeroes,
        DpsMeterClass.EncounterSnapshot encounter,
        double bossDps = 0.0,
        long bossTotalDamage60s = 0,
        IReadOnlyList<DpsMeterClass.HeroShareEntry>? bossTopHeroes = null,
        DpsMeterClass.EncounterSnapshot bossEncounter = default,
        IReadOnlyList<DpsMeterClass.PowerBreakdownEntry>? powerBreakdown = null)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(() => UpdateDps(
                dps, totalDamage60s, totalDamageSession, ownerEntityId,
                maxSingleHit, maxSingleHitSession, maxSingleHitEncounter,
                heroDisplayName, bossDisplayName, bossOnlyMode, topHeroes, encounter,
                bossDps, bossTotalDamage60s, bossTopHeroes, bossEncounter, powerBreakdown)));
            return;
        }
        LivePanel.UpdateDps(dps, totalDamage60s, totalDamageSession, ownerEntityId,
            maxSingleHit, maxSingleHitSession, maxSingleHitEncounter,
            heroDisplayName, bossDisplayName, bossOnlyMode, topHeroes, encounter,
            bossDps, bossTotalDamage60s, bossTopHeroes, bossEncounter, powerBreakdown);
    }

    public void UpdateSplinterStatus(bool cooldownActive, TimeSpan remaining, int dropCount, int totalSplinters, bool justDropped)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(() =>
                LivePanel.UpdateSplinterStatus(cooldownActive, remaining, dropCount, totalSplinters, justDropped)));
            return;
        }
        LivePanel.UpdateSplinterStatus(cooldownActive, remaining, dropCount, totalSplinters, justDropped);
    }

    /// <summary>Forwards an active-buffs snapshot to the Live dashboard's two-tier strip.
    /// Marshals to the UI thread when called from the sniffer/decay timer thread.</summary>
    public void UpdateBuffs(System.Collections.Generic.IReadOnlyList<MarvelHeroes.DpsMeter.Services.ActiveBuff> active, DateTime nowUtc)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(() => LivePanel.UpdateBuffs(active, nowUtc)));
            return;
        }
        LivePanel.UpdateBuffs(active, nowUtc);
    }

    /// <summary>Forwards the live <c>BuffTracker</c> to the Live dashboard's stats panel
    /// (the option-A "sum of buff property deltas" tile strip).  Marshals to the UI thread
    /// when called off the decay/sniffer thread.</summary>
    public void UpdateBuffStats(MarvelHeroes.DpsMeter.Services.BuffTracker? tracker)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(() => LivePanel.UpdateBuffStats(tracker)));
            return;
        }
        LivePanel.UpdateBuffStats(tracker);
    }

    /// <summary>Show or hide BOTH buff-tracking surfaces on the live dashboard.  Passed
    /// through to <c>LivePanel.SetBuffPanelsVisible</c>; the presenter calls this when the
    /// user toggles "Show buffs and procs" in Settings.  No marshalling needed -- callers
    /// are already on the UI thread (Settings checkbox handlers fire there).</summary>
    public void SetBuffPanelsVisible(bool visible)
        => LivePanel.SetBuffPanelsVisible(visible);

    /// <summary>Hand the live <see cref="MarvelHeroes.DpsMeter.Services.BuffTracker"/>
    /// reference to the Buff Tracker tab so its discovery lists can poll it.  Called by
    /// the presenter exactly once after <c>new BuffTracker(...)</c> completes -- before
    /// that the tab renders with empty lists.</summary>
    public void SetBuffTracker(MarvelHeroes.DpsMeter.Services.BuffTracker? tracker)
        => BuffTrackerTab.SetBuffTracker(tracker);

    /// <summary>Hand the live <see cref="MarvelHeroes.DpsMeter.Services.CooldownTracker"/>
    /// reference to the Cooldown Tracker tab so its discovery / watchlist UI can poll
    /// it.  Called by the presenter post-construction.</summary>
    public void SetCooldownTracker(MarvelHeroes.DpsMeter.Services.CooldownTracker? tracker)
        => CooldownTrackerTab.SetCooldownTracker(tracker);
}
