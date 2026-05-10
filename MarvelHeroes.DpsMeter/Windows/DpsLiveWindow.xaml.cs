using System;
using System.Collections.Generic;
using System.Windows;
using MarvelHeroesComporator.NetworkSniffer;
using MarvelHeroes.DpsMeter.Services;
using DpsMeterClass = MarvelHeroes.DpsMeter.Services.DpsMeter;

namespace MarvelHeroes.DpsMeter.Windows;

public partial class DpsLiveWindow : Window
{
    public bool InitialBossOnlyPreference => Panel.InitialBossOnlyPreference;

    public event Action<bool>?   BossOnlyToggled;
    public event Action?         SwitchModeRequested;
    public event Action<IReadOnlyList<DpsMeterClass.HeroShareEntry>?,
                        DpsMeterClass.EncounterSnapshot,
                        IReadOnlyList<DpsMeterClass.PowerBreakdownEntry>?>? SaveSnapshotRequested;
    public event Action?         ClearDpsRequested;
    public event Action?         ResetMaxHitRecordRequested;
    public event Action?         ResetSplinterCooldownRequested;
    public event Action?         ViewReportsRequested;

    private bool _closingByPresenter;

    public DpsLiveWindow(DpsOverlaySettingsFile settings)
    {
        InitializeComponent();
        Panel.Initialize(settings, isOverlayMode: false);

        // Let the window auto-size to content on first show, then hand control back
        // to the user so they can resize freely.
        Loaded += (_, _) => SizeToContent = SizeToContent.Manual;

        Panel.DragStarted          += () => { };  // window chrome handles dragging
        Panel.CloseRequested       += () => { };  // close button hidden in window mode
        Panel.SwitchModeRequested  += () => SwitchModeRequested?.Invoke();
        Panel.BossOnlyToggled      += v  => BossOnlyToggled?.Invoke(v);
        Panel.SaveSnapshotRequested += (h, enc, p) => SaveSnapshotRequested?.Invoke(h, enc, p);
        Panel.ClearDpsRequested    += () => ClearDpsRequested?.Invoke();
        Panel.ResetMaxHitRecordRequested += () => ResetMaxHitRecordRequested?.Invoke();
        Panel.ResetSplinterCooldownRequested += () => ResetSplinterCooldownRequested?.Invoke();
        Panel.ViewReportsRequested += () => ViewReportsRequested?.Invoke();

        // Closing via the title-bar X switches back to overlay rather than closing the app.
        Closing += (_, e) =>
        {
            if (_closingByPresenter)
            {
                Panel.SaveAll();
                return;
            }
            e.Cancel = true;
            SwitchModeRequested?.Invoke();
        };
    }

    public void UpdateSplinterStatus(bool cooldownActive, TimeSpan remaining, int dropCount, bool justDropped)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(() =>
                Panel.UpdateSplinterStatus(cooldownActive, remaining, dropCount, justDropped)));
            return;
        }
        Panel.UpdateSplinterStatus(cooldownActive, remaining, dropCount, justDropped);
    }

    public void CloseByPresenter()
    {
        _closingByPresenter = true;
        Close();
    }

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
        Panel.UpdateDps(dps, totalDamage60s, totalDamageSession, ownerEntityId,
            maxSingleHit, maxSingleHitSession, maxSingleHitEncounter,
            heroDisplayName, bossDisplayName, bossOnlyMode, topHeroes, encounter,
            bossDps, bossTotalDamage60s, bossTopHeroes, bossEncounter, powerBreakdown);
    }
}
