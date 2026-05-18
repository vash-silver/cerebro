using System;
using System.Windows;
using System.Windows.Interop;
using MarvelHeroesComporator.NetworkSniffer;
using MarvelHeroes.DpsMeter.Interop;
using MarvelHeroes.DpsMeter.Services;

namespace MarvelHeroes.DpsMeter;

/// <summary>
/// Separate always-on-top floating window dedicated to power-cooldown tracking.
/// Always renders the free-layout (WeakAuras-style) canvas at full primary-screen
/// size; the user positions individual chips with drag/resize when the overlay is
/// unlocked, then locks it for play to make the whole window click-through.
///
/// <para>Independent from the DPS / Buff overlays so users can pick whichever combo
/// works for their playstyle.  Persisted lock state lives on
/// <see cref="CooldownTrackerConfig.OverlayLocked"/>; geometry isn't persisted (the
/// window always sizes to the primary screen).</para>
/// </summary>
public partial class CooldownOverlayWindow : Window
{
    private bool _closingByPresenter;

    /// <summary>Raised when the user closes the window via Alt+F4 / WM_CLOSE so the
    /// presenter can sync the header checkbox + persist <c>ShowCooldownOverlay=false</c>.
    /// Mirrors <see cref="BuffOverlayWindow.HideRequested"/>.</summary>
    public event Action? HideRequested;

    public CooldownOverlayWindow()
    {
        InitializeComponent();

        // Always full primary screen.  Same approach as the buff overlay's free-
        // layout mode -- gives the user the whole screen as a canvas to position
        // icons on.  No saved geometry: locking + chip placement is the persistence
        // story here.
        var area = SystemParameters.WorkArea;
        Left   = area.Left;
        Top    = area.Top;
        Width  = area.Width;
        Height = area.Height;

        Closing += (_, args) =>
        {
            if (_closingByPresenter) return;
            args.Cancel = true;
            Hide();
            HideRequested?.Invoke();
        };

        SourceInitialized += OnSourceInitialized;
    }

    /// <summary>Push a fresh cooldown snapshot down to the canvas.  Marshals to the
    /// UI dispatcher when called off it.  Locked vs unlocked is checked on every
    /// call so toggles in the Cooldowns tab take effect within one tick.</summary>
    public void UpdateCooldowns(DateTime nowUtc)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(() => UpdateCooldowns(nowUtc)));
            return;
        }

        bool locked = CooldownTrackerConfig.Current.OverlayLocked;
        ApplyClickThrough(locked);
        FreeLayout.SetEditMode(!locked);
        FreeLayout.UpdateCooldowns(nowUtc);
    }

    /// <summary>Hook the live cooldown tracker so the panel can read state from it
    /// on every refresh tick.  Called by the presenter post-construction.</summary>
    public void SetTracker(CooldownTracker? tracker) => FreeLayout.SetTracker(tracker);

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

    public void ShowWithoutActivating()
    {
        var prev = ShowActivated;
        ShowActivated = false;
        Show();
        ShowActivated = prev;
    }

    public void CloseByPresenter()
    {
        _closingByPresenter = true;
        Close();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        var exStyle = User32.GetWindowLongPtr(hwnd, User32.GWL_EXSTYLE);
        User32.SetWindowLongPtr(hwnd, User32.GWL_EXSTYLE, exStyle | User32.WS_EX_NOACTIVATE);
        if (HwndSource.FromHwnd(hwnd) is { } source)
            source.AddHook(WndProc);

        // Apply persisted lock state once the HWND exists.
        ApplyClickThrough(CooldownTrackerConfig.Current.OverlayLocked);
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
