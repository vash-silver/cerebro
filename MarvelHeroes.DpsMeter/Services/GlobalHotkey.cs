using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace MarvelHeroes.DpsMeter.Services;

/// <summary>
/// Registers a single system-wide hotkey via Win32 <c>RegisterHotKey</c> and routes the
/// resulting <c>WM_HOTKEY</c> message to a managed callback.  Used by the splinter "arm
/// cooldown now" override so the user can peg the timer to their own pickup without
/// leaving the game window.
///
/// <para>Why this matters: open-world maps spawn loot for every nearby player, and the
/// auto-detection's <c>EntityCreate</c> match-on-proto-index can't distinguish whose drop
/// is whose — it'll arm the cooldown on any nearby splinter.  A global hotkey lets the
/// user override the timer to fire from THEIR pickup, without alt-tabbing out.</para>
///
/// <para>Threading: the underlying <c>WM_HOTKEY</c> dispatch happens on the WPF UI thread
/// (we hook the main window's HwndSource), so the <see cref="Pressed"/> callback fires on
/// the dispatcher and can touch UI directly.</para>
///
/// <para>Failure mode: if the requested hotkey is already registered by another app
/// (typical for popular combos like Win+E or Ctrl+Alt+Del), <c>RegisterHotKey</c> returns
/// false and we log via <see cref="Diagnostic"/> — no exception, no crash.  The caller can
/// retry with a different combo.</para>
/// </summary>
public sealed class GlobalHotkey : IDisposable
{
    // Win32 modifier mask bits, matching the values RegisterHotKey expects.
    public const uint MOD_NONE     = 0x0000;
    public const uint MOD_ALT      = 0x0001;
    public const uint MOD_CONTROL  = 0x0002;
    public const uint MOD_SHIFT    = 0x0004;
    public const uint MOD_WIN      = 0x0008;
    /// <summary>Prevents the OS from re-triggering the hotkey on key-repeat.  Always set --
    /// we want one Pressed event per physical press, not 30/s while held.</summary>
    public const uint MOD_NOREPEAT = 0x4000;

    private const int WM_HOTKEY = 0x0312;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private readonly Window _hostWindow;
    private readonly int    _id;
    private readonly Action _callback;
    private HwndSource?     _source;
    private bool            _registered;

    /// <summary>Optional log sink for register / unregister / suppress diagnostics.</summary>
    public Action<string>? Diagnostic { get; set; }

    /// <summary>Fires (on the UI dispatcher thread) every time the hotkey is pressed.</summary>
    public event Action? Pressed;

    /// <summary>Build a hotkey bound to a specific host window's HWND.  Doesn't register
    /// yet -- call <see cref="TryRegister"/> with the desired modifier / vk combo.</summary>
    /// <param name="hostWindow">A WPF Window whose HWND will receive WM_HOTKEY messages.
    /// Typically the main app window.  Must be source-initialised (post-Loaded) before
    /// <see cref="TryRegister"/> is called.</param>
    /// <param name="hotkeyId">Per-app unique id for the hotkey -- pass 1 if you only have
    /// one hotkey; higher ids if you wire up multiple.  Used to disambiguate which hotkey
    /// fired when WM_HOTKEY arrives.</param>
    public GlobalHotkey(Window hostWindow, int hotkeyId = 1)
    {
        _hostWindow = hostWindow ?? throw new ArgumentNullException(nameof(hostWindow));
        _id         = hotkeyId;
        _callback   = () => Pressed?.Invoke();
    }

    /// <summary>Register the given modifier / vk combination as a global hotkey.  Returns
    /// <c>true</c> on success; <c>false</c> if the OS rejected the registration (usually
    /// because another app already owns that combo).  Idempotent: if already registered,
    /// the previous binding is unregistered first.</summary>
    /// <param name="modifiers">Bitmask of <c>MOD_*</c> constants (no need to OR in
    /// <c>MOD_NOREPEAT</c> -- this method always adds it).</param>
    /// <param name="vk">Win32 virtual-key code of the non-modifier key (e.g.
    /// <c>0x45</c> for 'E').</param>
    public bool TryRegister(uint modifiers, uint vk)
    {
        // Idempotent: if we're already holding a binding, drop it first so the new one can
        // take its place.  Callers don't need to remember to Unregister() before re-Register().
        if (_registered) Unregister();

        // Pull the HWND.  Done lazily on first register rather than in the ctor because the
        // host window may not be source-initialised at construction time (we sometimes wire
        // up the hotkey before the window has Loaded).
        if (_source == null)
        {
            var hwnd = new WindowInteropHelper(_hostWindow).EnsureHandle();
            _source  = HwndSource.FromHwnd(hwnd);
            if (_source == null)
            {
                Diagnostic?.Invoke("GlobalHotkey: HwndSource.FromHwnd returned null -- window not source-initialised yet?");
                return false;
            }
            _source.AddHook(WndProc);
        }

        // Always set MOD_NOREPEAT so a held key doesn't generate a torrent of Pressed events.
        uint mods = modifiers | MOD_NOREPEAT;
        bool ok = RegisterHotKey(_source.Handle, _id, mods, vk);
        if (!ok)
        {
            int err = Marshal.GetLastWin32Error();
            Diagnostic?.Invoke($"GlobalHotkey: RegisterHotKey failed -- modifiers=0x{modifiers:X} vk=0x{vk:X} winErr={err} (likely already owned by another app, or invalid combo).");
            return false;
        }

        _registered = true;
        Diagnostic?.Invoke($"GlobalHotkey: registered modifiers=0x{modifiers:X} vk=0x{vk:X} (id={_id}).");
        return true;
    }

    /// <summary>Unregister the current binding, if any.  Safe to call when nothing is
    /// registered (no-op).</summary>
    public void Unregister()
    {
        if (!_registered || _source == null) return;
        UnregisterHotKey(_source.Handle, _id);
        _registered = false;
        Diagnostic?.Invoke($"GlobalHotkey: unregistered id={_id}.");
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == _id)
        {
            // Already on the UI dispatcher thread -- HwndSource pumps WM_* on the thread
            // that created it, which for the main window is the WPF UI thread.
            _callback();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        Unregister();
        if (_source != null)
        {
            _source.RemoveHook(WndProc);
            _source = null;
        }
    }

    // ── Pretty-print helpers ─────────────────────────────────────────────────────────────────

    /// <summary>Render a (modifiers, vk) pair as a user-readable string like "Ctrl+Shift+E"
    /// or "Alt+F12".  Used in the Settings UI to show the current binding.</summary>
    public static string Format(uint modifiers, uint vk)
    {
        var parts = new System.Collections.Generic.List<string>();
        if ((modifiers & MOD_CONTROL) != 0) parts.Add("Ctrl");
        if ((modifiers & MOD_ALT)     != 0) parts.Add("Alt");
        if ((modifiers & MOD_SHIFT)   != 0) parts.Add("Shift");
        if ((modifiers & MOD_WIN)     != 0) parts.Add("Win");
        parts.Add(FormatVk(vk));
        return string.Join("+", parts);
    }

    private static string FormatVk(uint vk)
    {
        // Friendly names for the cases that come up in practice.  Everything else falls
        // through to System.Windows.Forms.Keys.ToString() via the cast, which produces
        // readable names like "F12", "OemQuestion", etc.  We avoid the System.Windows.Forms
        // reference and do the common ones inline:
        if (vk >= 0x41 && vk <= 0x5A) return ((char)vk).ToString();         // A-Z
        if (vk >= 0x30 && vk <= 0x39) return ((char)vk).ToString();         // 0-9
        if (vk >= 0x70 && vk <= 0x87) return "F" + (vk - 0x70 + 1).ToString();  // F1-F24
        return vk switch
        {
            0x20 => "Space",
            0x09 => "Tab",
            0x0D => "Enter",
            0x1B => "Esc",
            0x2D => "Insert",
            0x2E => "Delete",
            0x24 => "Home",
            0x23 => "End",
            0x21 => "PageUp",
            0x22 => "PageDown",
            _    => $"VK_0x{vk:X2}",
        };
    }
}
