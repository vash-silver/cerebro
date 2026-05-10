using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Interop;
using MarvelHeroesComporator.NetworkSniffer;
using MarvelHeroes.DpsMeter.Interop;
using MarvelHeroes.DpsMeter.Services;
using DpsMeterClass = MarvelHeroes.DpsMeter.Services.DpsMeter;

namespace MarvelHeroes.DpsMeter;

public partial class DpsOverlayWindow : Window
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

    public DpsOverlayWindow(DpsOverlaySettingsFile settings)
    {
        InitializeComponent();

        Left = settings.Left;
        Top  = settings.Top;
        Panel.Initialize(settings, isOverlayMode: true);

        Panel.DragStarted          += () => { try { DragMove(); } catch { } };
        Panel.CloseRequested       += () => Application.Current?.Shutdown();
        Panel.SwitchModeRequested  += () => SwitchModeRequested?.Invoke();
        Panel.BossOnlyToggled      += v  => BossOnlyToggled?.Invoke(v);
        Panel.SaveSnapshotRequested += (h, enc, p) => SaveSnapshotRequested?.Invoke(h, enc, p);
        Panel.ClearDpsRequested    += () => ClearDpsRequested?.Invoke();
        Panel.ResetMaxHitRecordRequested += () => ResetMaxHitRecordRequested?.Invoke();
        Panel.ResetSplinterCooldownRequested += () => ResetSplinterCooldownRequested?.Invoke();
        Panel.ViewReportsRequested += () => ViewReportsRequested?.Invoke();

        SourceInitialized += OnSourceInitialized;
        LocationChanged   += (_, _) => Panel.SaveAll(Left, Top);
        Closing           += (_, _) => Panel.SaveAll(Left, Top);
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

    public void ShowWithoutActivating()
    {
        var prev = ShowActivated;
        ShowActivated = false;
        Show();
        ShowActivated = prev;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;

        var exStyle = User32.GetWindowLongPtr(hwnd, User32.GWL_EXSTYLE);
        User32.SetWindowLongPtr(hwnd, User32.GWL_EXSTYLE, exStyle | User32.WS_EX_NOACTIVATE);

        if (HwndSource.FromHwnd(hwnd) is { } source)
            source.AddHook(WndProc);
    }

    private nint WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == User32.WM_MOUSEACTIVATE)
        {
            handled = true;
            return User32.MA_NOACTIVATE;
        }
        return IntPtr.Zero;
    }
}
