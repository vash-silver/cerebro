using System;
using System.Runtime.InteropServices;

namespace MarvelHeroes.DpsMeter.Interop;

/// <summary>
/// Minimal P/Invoke surface for the standalone DPS overlay — just the bits needed to make a
/// borderless WPF window non-activating (so a click on the overlay never steals focus from
/// Marvel Heroes' fullscreen window) and to extend its window-style flags.
///
/// <para>
/// This is a hand-trimmed subset of the parent comporator's <c>User32</c> wrapper — we ship
/// only the constants + entry points actually called from <c>DpsOverlayWindow</c>, so the
/// standalone exe doesn't drag in unrelated Win32 surfaces (hotkey registration, DPI helpers,
/// process-id lookups, screen-capture exclusion).  Add to this file as new overlay features
/// require additional Win32 calls; do NOT pull the whole parent file in wholesale.
/// </para>
/// </summary>
internal static class User32
{
    /// <summary>WM_MOUSEACTIVATE — sent before WM_*BUTTONDOWN so we can intercept and return
    /// MA_NOACTIVATE, telling Windows "process the click but do not bring me to foreground".
    /// Without this, every drag of the overlay would yank focus away from the game.</summary>
    public const int WM_MOUSEACTIVATE = 0x0021;

    /// <summary>Return value for WM_MOUSEACTIVATE: hit-test passes through and the click is
    /// processed but the window is NOT activated and NOT brought to the foreground.</summary>
    public const int MA_NOACTIVATE = 3;

    /// <summary>Index for GetWindowLongPtr / SetWindowLongPtr — extended window style bitfield.</summary>
    public const int GWL_EXSTYLE = -20;

    /// <summary>WS_EX_NOACTIVATE — window does not get foreground activation when clicked.
    /// Combined with the WM_MOUSEACTIVATE hook gives us the "draggable but non-activating"
    /// behaviour expected of a HUD overlay.</summary>
    public const nint WS_EX_NOACTIVATE = 0x08000000;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    public static extern nint GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    public static extern nint SetWindowLongPtr(IntPtr hWnd, int nIndex, nint dwNewLong);

    /// <summary>Handle of the window currently in the foreground (the one receiving keyboard
    /// input).  Used by the foreground watcher to auto-hide the overlay while Marvel Heroes
    /// isn't the active window.  Returns <c>IntPtr.Zero</c> during transient states (e.g. an
    /// app is in the middle of activating) — callers should treat zero as "indeterminate" and
    /// keep the previous decision rather than flipping visibility on a glitch.</summary>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr GetForegroundWindow();

    /// <summary>Maps an HWND to the OS process id of its owning thread.  Combined with
    /// <see cref="System.Diagnostics.Process.GetProcessById"/> this gives us the process name,
    /// which is how the foreground watcher decides whether the active window is the game,
    /// our own overlay (right-click menu open), or something else entirely.</summary>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
}
