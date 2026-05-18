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

    /// <summary>True when the window is currently sized + positioned to the full primary
    /// screen for free-layout mode.  Strip-mode geometry (the user's drag-resized box)
    /// stays preserved in <c>_settings.BuffOverlayLeft/Top/Width/Height</c>; while this
    /// flag is on, the LocationChanged / SizeChanged handlers skip persistence so the
    /// full-screen rectangle doesn't overwrite the strip-mode coordinates.</summary>
    private bool _isFreeLayoutGeometryActive;

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
            // Skip strip-mode geometry persistence while in free-layout mode.  The full-
            // screen transparent window's Left/Top/Width/Height are derived from the
            // primary screen's WorkArea -- if we wrote those into the settings file, the
            // user's careful strip-mode positioning would be lost the next time they
            // flip out of free layout.
            if (_isFreeLayoutGeometryActive) return;
            _settings.BuffOverlayLeft = Left;
            _settings.BuffOverlayTop  = Top;
            DpsOverlaySettingsFile.Save(_settings);
        };

        SizeChanged += (_, _) =>
        {
            if (_isFreeLayoutGeometryActive) return;
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

    /// <summary>Push a fresh buffs snapshot down to whichever child panel is visible.
    /// Marshals to the UI dispatcher when called off it.  Mode swap (strip vs free
    /// layout) is checked on every call so toggles in the Buff Tracker tab take effect
    /// within one tick.</summary>
    public void UpdateBuffs(IReadOnlyList<ActiveBuff> active, DateTime nowUtc)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(() => UpdateBuffs(active, nowUtc)));
            return;
        }

        var cfg = Services.TrackedBuffsConfig.Current;
        bool freeLayout = cfg.FreeLayoutMode;
        bool locked     = cfg.OverlayLocked;
        ApplyWindowMode(freeLayout, locked);

        // Toggle child visibility based on the current mode.  Cheap (one Visibility
        // assignment per call) and means we don't have to re-wire anything when the
        // user flips the toggle -- next UpdateBuffs reflects it.
        if (freeLayout)
        {
            StripFrame.Visibility    = Visibility.Collapsed;
            FreeLayoutFrame.Visibility = Visibility.Visible;
            // In free layout: "edit mode" (drag chips, show resize grips) is the
            // inverse of locked.  Locked = click-through, no edit chrome.
            FreeLayout.SetEditMode(!locked);
            FreeLayout.UpdateBuffs(active, nowUtc);
        }
        else
        {
            StripFrame.Visibility    = Visibility.Visible;
            FreeLayoutFrame.Visibility = Visibility.Collapsed;
            Strip.UpdateBuffs(active, nowUtc);
        }
    }

    /// <summary>Forwarded from the host (BuffTrackerPanel via the presenter) when the
    /// user toggles edit mode.  Drives the dashed-border + resize-grip chrome on the
    /// free-layout chips so the user can see what they're grabbing.</summary>
    public void SetEditMode(bool edit) => FreeLayout.SetEditMode(edit);

    /// <summary>Apply window geometry + click-through state for the current
    /// (free-layout, locked) combination:
    /// <list type="bullet">
    ///   <item>Strip mode, unlocked: user-resized box, normal window chrome, mouse-
    ///         interactive (drag body to move, grab edges to resize).</item>
    ///   <item>Strip mode, locked: user-resized box stays in place but click-through.
    ///         The game receives all mouse input even though the strip paints over it.</item>
    ///   <item>Free layout, unlocked: full primary screen, transparent, mouse-
    ///         interactive so the user can drag / resize individual chips.</item>
    ///   <item>Free layout, locked: full primary screen, transparent, click-through.
    ///         Icons render in their saved positions and the game gets all input.</item>
    /// </list>
    ///
    /// <para>Idempotent: only does real work when the desired state differs from the
    /// current one.  Safe to call on every UpdateBuffs tick.</para></summary>
    private void ApplyWindowMode(bool freeLayout, bool locked)
    {
        // ── Geometry swap ──
        if (freeLayout && !_isFreeLayoutGeometryActive)
        {
            // Switching INTO free layout: enter full-screen transparent mode.  The strip
            // geometry is already persisted in _settings (we just wrote it on the user's
            // last drag/resize), so we can swap freely and restore on the way back.
            _isFreeLayoutGeometryActive = true;
            // SystemParameters.WorkArea returns the primary screen's working area in
            // device-independent pixels, excluding the taskbar.  Use Bounds (full screen
            // including taskbar) if we wanted edge-to-edge; WorkArea is the safer
            // default so the taskbar stays interactive.
            var area = SystemParameters.WorkArea;
            Left   = area.Left;
            Top    = area.Top;
            Width  = area.Width;
            Height = area.Height;
        }
        else if (!freeLayout && _isFreeLayoutGeometryActive)
        {
            // Switching OUT of free layout: restore the saved strip-mode geometry.
            _isFreeLayoutGeometryActive = false;
            Left   = _settings.BuffOverlayLeft;
            Top    = _settings.BuffOverlayTop;
            Width  = _settings.BuffOverlayWidth  > 0 ? _settings.BuffOverlayWidth  : 251;
            Height = _settings.BuffOverlayHeight > 0 ? _settings.BuffOverlayHeight : 60;
        }

        // ── Click-through state ──
        // Locked = click-through in every mode.  Strip mode used to be permanently
        // interactive (always draggable); now locking it lets the user park the strip
        // in a corner of the screen for play without accidentally grabbing it mid-
        // combat.  Unlock to reposition.
        ApplyClickThrough(locked);
    }

    /// <summary>Toggle the <c>WS_EX_TRANSPARENT</c> extended window style.  When set, the
    /// OS routes mouse input through this window to whatever's underneath -- enabling
    /// click-through so the game receives clicks normally even though the overlay paints
    /// over it.  Idempotent: re-applying the same state is a no-op at the Win32 level.</summary>
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
        // In free-layout mode the window covers the full primary screen -- there's no
        // sensible "drag the window" gesture, and we don't want background clicks to
        // start a DragMove that would only move the full-screen rectangle slightly.
        // Clicks on icons in unlocked mode are handled by the icons' own handlers
        // (they set Handled=true), so falling through here means the user clicked
        // empty space.
        if (_isFreeLayoutGeometryActive) return;
        // Strip mode + locked: WS_EX_TRANSPARENT should already prevent this fire,
        // but defense-in-depth -- if a stray click reaches us, never start a DragMove
        // because the user explicitly asked for the overlay not to move.
        if (Services.TrackedBuffsConfig.Current.OverlayLocked) return;
        // Strip mode + unlocked: mouse-down on the body fires DragMove so the user
        // can reposition.  Edge clicks never reach here because WM_NCHITTEST routes
        // them to OS resize first.
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
        // In free-layout mode the window covers the full primary screen -- there's no
        // meaningful "edge" to grab, and treating screen edges as resize zones would
        // fight the game's own UI at the screen perimeter.  Return 0 so WPF falls
        // through to its default hit-test (HTCLIENT for the whole body).  Resize lives
        // on individual icons via their corner grips instead.
        if (_isFreeLayoutGeometryActive) return 0;
        // Locked strip mode: no edge resize.  The user explicitly froze the overlay
        // in place; turning the perimeter back into a sizing border would be a
        // surprise.  Unlock to resize.
        if (Services.TrackedBuffsConfig.Current.OverlayLocked) return 0;

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
