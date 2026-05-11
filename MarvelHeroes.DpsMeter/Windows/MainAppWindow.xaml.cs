using System;
using System.Collections.Generic;
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
    public bool InitialBossOnlyPreference => LivePanel.InitialBossOnlyPreference;

    // Forwarded panel events -- same set DpsLiveWindow / DpsOverlayWindow expose so the
    // presenter's wiring code doesn't have to special-case this window.
    public event Action<bool>?   BossOnlyToggled;
    public event Action?         SwitchModeRequested;   // unused in app-first mode; kept for API parity
    public event Action<IReadOnlyList<DpsMeterClass.HeroShareEntry>?,
                        DpsMeterClass.EncounterSnapshot,
                        IReadOnlyList<DpsMeterClass.PowerBreakdownEntry>?>? SaveSnapshotRequested;
    public event Action?         ClearDpsRequested;
    public event Action?         ResetMaxHitRecordRequested;
    public event Action?         ResetSplinterCooldownRequested;
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

    private bool _suppressShowOverlayCheckboxEvents;
    private bool _closingByPresenter;

    public MainAppWindow(DpsOverlaySettingsFile settings)
    {
        InitializeComponent();

        // Live tab's DpsDisplayPanel uses the same Initialize signature as the overlay /
        // live-window variants -- isOverlayMode:false hides the on-panel close button
        // (the window's title bar X handles closing) and the cursor stays default.
        LivePanel.Initialize(settings, isOverlayMode: false);
        SettingsPanel.Initialize(settings);
        SetShowOverlayChecked(settings.ShowOverlay);

        // Settings tab fires the same events the live panel fires (BossOnlyToggled, ClearDps,
        // ResetMaxHitRecord, ResetSplinterCooldown) so the presenter doesn't have to special-
        // case it.  Funnel them into the same window-level events.
        SettingsPanel.BossOnlyToggled             += v  => BossOnlyToggled?.Invoke(v);
        SettingsPanel.ClearDpsRequested           += () => ClearDpsRequested?.Invoke();
        SettingsPanel.ResetMaxHitRecordRequested  += () => ResetMaxHitRecordRequested?.Invoke();
        SettingsPanel.ResetSplinterCooldownRequested += () => ResetSplinterCooldownRequested?.Invoke();

        // Bubble panel events out so the presenter can subscribe to this window the same
        // way it does to DpsLiveWindow / DpsOverlayWindow.
        LivePanel.DragStarted             += () => { /* no-op: main window has a real title bar */ };
        LivePanel.CloseRequested          += () => { /* close button hidden in non-overlay mode */ };
        LivePanel.SwitchModeRequested     += () => SwitchModeRequested?.Invoke();
        LivePanel.BossOnlyToggled         += v  => BossOnlyToggled?.Invoke(v);
        LivePanel.SaveSnapshotRequested   += (h, enc, p) => SaveSnapshotRequested?.Invoke(h, enc, p);
        LivePanel.ClearDpsRequested       += () => ClearDpsRequested?.Invoke();
        LivePanel.ResetMaxHitRecordRequested  += () => ResetMaxHitRecordRequested?.Invoke();
        LivePanel.ResetSplinterCooldownRequested += () => ResetSplinterCooldownRequested?.Invoke();
        // The right-click "View reports" menu item in the live tab makes sense to interpret
        // as "switch to the Reports tab here" rather than opening a separate window.
        LivePanel.ViewReportsRequested    += () => MainTabs.SelectedIndex = 1;

        // Auto-size? No -- the main app should remember its user-resized geometry once we
        // add that.  For now, the XAML's Width/Height defaults apply.
        Closing += (_, _) =>
        {
            if (_closingByPresenter)
            {
                LivePanel.SaveAll();
                return;
            }
            // Normal window close = quit the app.  The overlay is auxiliary; if the user
            // closes the main window we shut everything down (matches Windows convention).
            // The presenter's Stop() will fire from App.OnExit and tear down cleanly.
            Application.Current?.Shutdown();
        };
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

    public void UpdateSplinterStatus(bool cooldownActive, TimeSpan remaining, int dropCount, bool justDropped)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(() =>
                LivePanel.UpdateSplinterStatus(cooldownActive, remaining, dropCount, justDropped)));
            return;
        }
        LivePanel.UpdateSplinterStatus(cooldownActive, remaining, dropCount, justDropped);
    }
}
