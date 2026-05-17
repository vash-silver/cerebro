using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using MarvelHeroesComporator.NetworkSniffer;
using MarvelHeroes.DpsMeter.Interop;
using MarvelHeroes.DpsMeter.Services;

namespace MarvelHeroes.DpsMeter;

/// <summary>
/// Separate floating always-on-top window dedicated to the buff watchlist -- the
/// WeakAuras-equivalent surface that lives on top of the game during play.  Shares the
/// chrome implementation with <see cref="DpsOverlayWindow"/> (transparent borderless,
/// WM_NCHITTEST-driven edge resize, persisted geometry, non-activating focus model so
/// clicks never steal focus from Marvel Heroes) but renders only the
/// <see cref="BuffStripPanel"/> chip strip -- no DPS numbers, no leaderboard, no power
/// breakdown.  Independent on/off from the DPS overlay so users can pick whichever
/// combination suits their playstyle.
///
/// <para>Refresh model mirrors the DPS overlay: the presenter calls <see cref="UpdateBuffs"/>
/// on every decay tick (4 Hz) with a fresh snapshot from <c>BuffTracker.GetActiveBuffs()</c>.
/// The hosted <see cref="BuffStripPanel"/> applies the watchlist filter from
/// <see cref="TrackedBuffsConfig.Current"/> -- so when the user has toggled "Only show
/// tracked buffs" the chip strip is naturally focused on the few they care about.  When the
/// watchlist filter is off the overlay shows the same content as the dashboard's inline
/// strip; in practice users who open the floating overlay tend to also enable the filter
/// (otherwise the chip strip can grow long in dense combat).</para>
/// </summary>
public partial class BuffOverlayWindow : Window
{
    private readonly DpsOverlaySettingsFile _settings;
    private bool _closingByPresenter;

    /// <summary>Raised when the user closed the window via the system close-key path
    /// (Alt+F4 / WM_CLOSE) so the presenter can sync the header checkbox + persist
    /// <c>ShowBuffOverlay = false</c>.  Mirrors <see cref="DpsOverlayWindow.HideRequested"/>.</summary>
    public event Action? HideRequested;

    public BuffOverlayWindow(DpsOverlaySettingsFile settings)
    {
        InitializeComponent();
        _settings = settings;

        Left = settings.BuffOverlayLeft;
        Top  = settings.BuffOverlayTop;
        if (settings.BuffOverlayWidth  > 0) Width  = settings.BuffOverlayWidth;
        if (settings.BuffOverlayHeight > 0) Height = settings.BuffOverlayHeight;

        // Drop content auto-sizing once first layout settles so user-driven resizes
        // stick -- same pattern as DpsOverlayWindow.  Also capture the auto-fit size on
        // first launch so subsequent runs restore the natural starting size instead of
        // re-auto-fitting each time (which would be slightly different if the watchlist
        // grew between sessions).
        Loaded += (_, _) =>
        {
            SizeToContent = SizeToContent.Manual;
            if (_settings.BuffOverlayWidth  <= 0) _settings.BuffOverlayWidth  = ActualWidth;
            if (_settings.BuffOverlayHeight <= 0) _settings.BuffOverlayHeight = ActualHeight;
        };

        LocationChanged += (_, _) =>
        {
            _settings.BuffOverlayLeft = Left;
            _settings.BuffOverlayTop  = Top;
            DpsOverlaySettingsFile.Save(_settings);
        };

        SizeChanged += (_, _) =>
        {
            _settings.BuffOverlayWidth  = ActualWidth;
            _settings.BuffOverlayHeight = ActualHeight;
            DpsOverlaySettingsFile.Save(_settings);
        };

        Closing += (_, args) =>
        {
            if (_closingByPresenter) return;
            args.Cancel = true;
            Hide();
            HideRequested?.Invoke();
        };

        SourceInitialized += OnSourceInitialized;
    }

    /// <summary>Push a fresh buffs snapshot down to the hosted strip.  Marshals to the UI
    /// dispatcher when called off it (the presenter's decay tick already runs on UI but
    /// keeping the guard makes the API safe to call from anywhere).</summary>
    public void UpdateBuffs(IReadOnlyList<ActiveBuff> active, DateTime nowUtc)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(() => Strip.UpdateBuffs(active, nowUtc)));
            return;
        }
        Strip.UpdateBuffs(active, nowUtc);
    }

    /// <summary>Show without activating (non-stealing-focus).  Same trick as the DPS
    /// overlay -- <c>ShowActivated=false</c> for the duration of the <c>Show()</c> call
    /// so the window appears on screen but doesn't grab keyboard focus from Marvel Heroes.</summary>
    public void ShowWithoutActivating()
    {
        var prev = ShowActivated;
        ShowActivated = false;
        Show();
        ShowActivated = prev;
    }

    /// <summary>Used by the presenter during teardown so the Closing handler doesn't
    /// recurse into a Hide-instead-of-Close path while the app is actually shutting down.</summary>
    public void CloseByPresenter()
    {
        _closingByPresenter = true;
        Close();
    }

    // ── Chrome handling ────────────────────────────────────────────────────────────────

    private void Frame_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Mouse-down on the body fires DragMove so the user can reposition.  Edge clicks
        // never reach here because WM_NCHITTEST below routes them to OS resize first.
        try { DragMove(); } catch { /* DragMove throws if the mouse button was already released */ }
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;

        // WS_EX_NOACTIVATE: clicks process but never bring the window forward.  Combined
        // with the WM_MOUSEACTIVATE hook below this gives the "draggable HUD that never
        // steals focus from MH" behavior the DPS overlay uses.
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

    private const double ResizeEdgePx = 8.0;

    private nint HitTestNc(IntPtr lParam)
    {
        int packed = unchecked((int)lParam.ToInt64());
        short sx = (short)(packed & 0xFFFF);
        short sy = (short)((packed >> 16) & 0xFFFF);

        Point local;
        try { local = PointFromScreen(new Point(sx, sy)); }
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
