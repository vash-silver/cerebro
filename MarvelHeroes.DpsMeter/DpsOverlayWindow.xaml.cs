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

    private readonly DpsOverlaySettingsFile _settings;

    public DpsOverlayWindow(DpsOverlaySettingsFile settings)
    {
        InitializeComponent();

        _settings = settings;
        Left = settings.Left;
        Top  = settings.Top;
        // Apply persisted size if the user has previously resized.  Zero means "no saved
        // size yet" — leave SizeToContent=WidthAndHeight so the window auto-fits on first
        // launch, then OnLoaded captures the resulting dimensions for next time.
        if (settings.OverlayWidth  > 0) Width  = settings.OverlayWidth;
        if (settings.OverlayHeight > 0) Height = settings.OverlayHeight;
        Panel.Initialize(settings, isOverlayMode: true);

        // Drop content-auto-sizing once layout has settled so subsequent user-driven
        // resizes stick instead of getting clobbered by SizeToContent recomputing on every
        // panel layout pass.  Pair-mirrored from DpsLiveWindow's resize fix (commit 090d355).
        Loaded += (_, _) =>
        {
            SizeToContent = SizeToContent.Manual;
            // First launch: capture the auto-fit dimensions so the user's "natural" first
            // size becomes the default on subsequent runs (instead of re-auto-fitting based
            // on whatever the current content happens to want).
            if (_settings.OverlayWidth  <= 0) _settings.OverlayWidth  = ActualWidth;
            if (_settings.OverlayHeight <= 0) _settings.OverlayHeight = ActualHeight;
        };

        // Persist size on every change.  WPF fires SizeChanged for both user-driven resizes
        // and programmatic ones; we don't gate them because the values are always current,
        // and Panel.SaveAll's debounced disk write is cheap.
        SizeChanged += (_, _) =>
        {
            _settings.OverlayWidth  = ActualWidth;
            _settings.OverlayHeight = ActualHeight;
            Panel.SaveAll(Left, Top);
        };

        Panel.DragStarted          += () =>
        {
            // Locked: defense-in-depth.  WS_EX_TRANSPARENT should already prevent the
            // click from ever reaching the panel, but if a stray event slips through
            // (DragMove is also invoked by some keyboard paths), never start a drag
            // when the user has explicitly frozen the overlay in place.
            if (_settings.OverlayLocked) return;
            try { DragMove(); } catch { }
        };
        // Clicking the overlay's small X button used to quit the entire app -- the overlay
        // WAS the app.  In the new app-first layout the main window is the app, and the
        // overlay is an auxiliary view, so X just hides the overlay (same effect as
        // unticking the main window's "Show overlay" checkbox).  The presenter's
        // SwitchModeRequested handler routes through SetOverlayVisible(false), which also
        // syncs the checkbox and persists the new setting.
        Panel.CloseRequested       += () => SwitchModeRequested?.Invoke();
        Panel.SwitchModeRequested  += () => SwitchModeRequested?.Invoke();
        Panel.BossOnlyToggled      += v  => BossOnlyToggled?.Invoke(v);
        Panel.SaveSnapshotRequested += (h, enc, p) => SaveSnapshotRequested?.Invoke(h, enc, p);
        Panel.ClearDpsRequested    += () => ClearDpsRequested?.Invoke();
        Panel.ResetMaxHitRecordRequested += () => ResetMaxHitRecordRequested?.Invoke();
        Panel.ResetSplinterCooldownRequested += () => ResetSplinterCooldownRequested?.Invoke();
        Panel.ViewReportsRequested += () => ViewReportsRequested?.Invoke();

        SourceInitialized += OnSourceInitialized;
        LocationChanged   += (_, _) => Panel.SaveAll(Left, Top);
        // Cancel-and-hide on user-initiated close so the window object survives between
        // toggles of the "Show overlay" checkbox.  Without this, an Alt+F4 on the overlay
        // would destroy the WPF window, leaving _overlayWindow pointing at a dead object;
        // the next checkbox-on toggle would throw on ShowWithoutActivating().  The presenter
        // sets _closingByPresenter via CloseByPresenter() before its final Close() so
        // app shutdown can still actually destroy the window.
        Closing += (sender, args) =>
        {
            Panel.SaveAll(Left, Top);
            if (_closingByPresenter) return;
            args.Cancel = true;
            Hide();
            HideRequested?.Invoke();
        };
    }

    /// <summary>Raised when the user closed the overlay via Alt+F4 / WM_CLOSE.  The presenter
    /// uses this to sync the main window's checkbox + persist ShowOverlay=false (the user
    /// dismissed the overlay, so reflect that choice everywhere).</summary>
    public event Action? HideRequested;

    private bool _closingByPresenter;
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

    public void UpdateSplinterStatus(bool cooldownActive, TimeSpan remaining, int dropCount, int totalSplinters, bool justDropped)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(() =>
                Panel.UpdateSplinterStatus(cooldownActive, remaining, dropCount, totalSplinters, justDropped)));
            return;
        }
        Panel.UpdateSplinterStatus(cooldownActive, remaining, dropCount, totalSplinters, justDropped);
    }

    /// <summary>Show or hide the DPS summary block (title + big number + max-hit + status
    /// text) on the floating overlay's panel.  Called by the presenter when the user
    /// toggles "Show DPS summary in overlay" in Settings.  No-op-cheap; marshals to UI
    /// thread if called off it.</summary>
    public void SetDpsSummaryVisible(bool visible)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(() => Panel.SetDpsSummaryVisible(visible)));
            return;
        }
        Panel.SetDpsSummaryVisible(visible);
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

        // Apply persisted lock state.  Must happen here (after HWND exists) rather
        // than in the ctor because WS_EX_TRANSPARENT is a Win32 ex-style bit and we
        // need a valid window handle to set it.
        ApplyClickThrough(_settings.OverlayLocked);
    }

    /// <summary>Toggle the overlay's lock state at runtime.  The presenter calls this
    /// when the user flips the "Lock overlay" checkbox in Settings so the change takes
    /// effect immediately.  Idempotent at the Win32 level; safe to call with the same
    /// value as the current one.</summary>
    public void SetLocked(bool locked)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(() => SetLocked(locked)));
            return;
        }
        ApplyClickThrough(locked);
    }

    /// <summary>Toggle the <c>WS_EX_TRANSPARENT</c> extended window style.  When set,
    /// the OS routes mouse input through this window to whatever's underneath --
    /// enabling click-through so the game receives all clicks normally even though the
    /// overlay paints over it.  Idempotent: re-applying the same state is a no-op at
    /// the Win32 level.</summary>
    private void ApplyClickThrough(bool clickThrough)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        var ex = User32.GetWindowLongPtr(hwnd, User32.GWL_EXSTYLE);
        nint newEx = clickThrough
            ? (ex | User32.WS_EX_TRANSPARENT)
            : (ex & ~User32.WS_EX_TRANSPARENT);
        if (newEx != ex)
            User32.SetWindowLongPtr(hwnd, User32.GWL_EXSTYLE, newEx);
    }

    private nint WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == User32.WM_MOUSEACTIVATE)
        {
            handled = true;
            return User32.MA_NOACTIVATE;
        }
        if (msg == User32.WM_NCHITTEST)
        {
            var hit = HitTestNc(lParam);
            if (hit != 0)
            {
                handled = true;
                return hit;
            }
        }
        return IntPtr.Zero;
    }

    /// <summary>Edge-thickness in WPF DIPs treated as a resize-grip zone.  8 px matches the
    /// width of the system's invisible sizing border on a standard-DPI display — wide enough
    /// to grab without precise aim, narrow enough that the body of the overlay still receives
    /// mouse clicks for DragMove / right-click menu.</summary>
    private const double ResizeEdgePx = 8.0;

    /// <summary>Given the screen-space lParam from WM_NCHITTEST, decide whether the cursor is
    /// on a resize edge / corner.  Returns the appropriate HT* code so Windows handles the
    /// sizing drag natively (same behaviour as a normal window's invisible sizing border —
    /// we have to do this ourselves because <c>WindowStyle="None"</c> + <c>AllowsTransparency</c>
    /// hides the system frame that would otherwise own this hit test).  Returns 0 when the
    /// cursor is outside any edge zone so the caller falls through to WPF's default
    /// (HTCLIENT for the body, which routes the click to the panel for DragMove / context
    /// menu).</summary>
    private nint HitTestNc(IntPtr lParam)
    {
        // Locked: no edge-resize.  Return 0 so WPF falls through to its default
        // HTCLIENT path; combined with WS_EX_TRANSPARENT this means the cursor never
        // changes when hovering the overlay and edge clicks never resize it.  Unlock
        // to resize.
        if (_settings.OverlayLocked) return 0;

        // lParam packs the screen X / Y as two signed 16-bit values.  Use the unchecked cast
        // path so negative coords on multi-monitor setups (window straddles the primary)
        // still round-trip correctly.
        int packed = unchecked((int)lParam.ToInt64());
        short sx = (short)(packed & 0xFFFF);
        short sy = (short)((packed >> 16) & 0xFFFF);

        // PointFromScreen translates to window-local DIPs, accounting for DPI scaling and
        // the window's current Left/Top.  Cheap (no allocation) and DPI-correct.
        System.Windows.Point local;
        try { local = PointFromScreen(new System.Windows.Point(sx, sy)); }
        catch { return 0; }

        double w = ActualWidth;
        double h = ActualHeight;
        if (w <= 0 || h <= 0) return 0;

        bool onLeft   = local.X >= 0 && local.X < ResizeEdgePx;
        bool onRight  = local.X <= w && local.X > w - ResizeEdgePx;
        bool onTop    = local.Y >= 0 && local.Y < ResizeEdgePx;
        bool onBottom = local.Y <= h && local.Y > h - ResizeEdgePx;

        if (onLeft  && onTop)    return User32.HTTOPLEFT;
        if (onRight && onTop)    return User32.HTTOPRIGHT;
        if (onLeft  && onBottom) return User32.HTBOTTOMLEFT;
        if (onRight && onBottom) return User32.HTBOTTOMRIGHT;
        if (onLeft)              return User32.HTLEFT;
        if (onRight)             return User32.HTRIGHT;
        if (onTop)               return User32.HTTOP;
        if (onBottom)            return User32.HTBOTTOM;
        return 0;
    }
}
