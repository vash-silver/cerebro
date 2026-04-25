using System;
using System.Diagnostics;
using MarvelHeroes.DpsMeter.Interop;

namespace MarvelHeroes.DpsMeter.Services;

/// <summary>
/// Lightweight "is Marvel Heroes (or our own overlay) the foreground window right now?"
/// check used by <see cref="DpsOverlayPresenter.ShouldBeVisible"/> to auto-hide the overlay
/// whenever the user Alt+Tabs to another app.  Polled at 4 Hz from the existing decay-tick
/// timer in the presenter, so latency between focus change and overlay hide is &lt; 250 ms.
///
/// <para>Why a static class with cached state instead of an injected service: this watcher
/// has no per-instance state worth disposing, the cache is just a perf optimisation (skip
/// <see cref="Process.GetProcessById"/> when the foreground HWND hasn't changed since the
/// last poll), and there's only ever one overlay so a singleton is fine.  Promote to an
/// instance class only if a future scenario needs per-window behavior (e.g. multi-game
/// support, configurable process-name list).</para>
///
/// <para>Process names matched: <c>MarvelHeroesOmega</c> (the live MH client; same name the
/// parent comporator's <c>Helpers/GameWindowLocator</c> uses), and the standalone overlay's
/// own process name (resolved at startup via <see cref="Process.GetCurrentProcess"/>).
/// Including <em>self</em> is critical — without it, opening the right-click context menu
/// would change the foreground HWND to the menu popup, which would then make the watcher
/// return false and Visibility.Collapsed the overlay mid-click.  The self-check covers the
/// menu popup HWND because popups inherit the owner process.</para>
/// </summary>
internal static class GameForegroundWatcher
{
    /// <summary>Process names (without the <c>.exe</c> suffix) that we treat as "the game"
    /// for visibility purposes.  Add custom-server renames here if a new build ships under
    /// a different exe name — the diagnostic line emitted on visibility flips makes it
    /// trivial to discover the right value for an unfamiliar setup.</summary>
    private static readonly string[] GameProcessNames = { "MarvelHeroesOmega", "MarvelHeroes2016" };

    /// <summary>Resolved once at type-init from <see cref="Process.GetCurrentProcess"/>.
    /// Used to allow the right-click menu popup (owned by us) to count as "foreground OK".</summary>
    private static readonly string _selfProcessName = ResolveSelfProcessName();

    private static IntPtr _cachedHwnd = IntPtr.Zero;
    private static bool _cachedDecision = true;
    /// <summary>Process name observed for the currently cached HWND — exposed via
    /// <see cref="LastForegroundProcessName"/> so the presenter's transition log can
    /// include it (useful for "why did the overlay just hide?" triage).</summary>
    private static string _cachedProcessName = "";

    /// <summary>The process name of the most recently observed foreground window, or empty
    /// string if no foreground was ever resolved.  Snapshot only — race with the next poll
    /// is fine since this is for diagnostic logging, not correctness.</summary>
    public static string LastForegroundProcessName => _cachedProcessName;

    /// <summary>True when the foreground window's owning process is either the game
    /// (<see cref="GameProcessNames"/>) or our own overlay process.  Cached on the foreground
    /// HWND so repeated polls during stable focus are a single Win32 call (no Process.GetProcessById
    /// churn).  Conservative on errors: returns the previous decision when the HWND is zero
    /// (transient activation glitch) or true when Process.GetProcessById fails (process exited
    /// between GetForegroundWindow and the lookup) — hiding on a transient error would be
    /// surprising behaviour.</summary>
    public static bool IsGameOrSelfForeground()
    {
        var fg = User32.GetForegroundWindow();

        if (fg == IntPtr.Zero)
        {
            // Activation in progress (Alt-Tab transition, login screen, etc.).  Don't flip
            // the decision on a momentary nothing — the next poll in 250 ms will resolve it.
            return _cachedDecision;
        }

        if (fg == _cachedHwnd)
            return _cachedDecision;

        _cachedHwnd = fg;
        try
        {
            User32.GetWindowThreadProcessId(fg, out uint pid);
            using var p = Process.GetProcessById((int)pid);
            string name = p.ProcessName ?? string.Empty;
            _cachedProcessName = name;

            if (string.Equals(name, _selfProcessName, StringComparison.OrdinalIgnoreCase))
            {
                _cachedDecision = true;
                return true;
            }

            for (int i = 0; i < GameProcessNames.Length; i++)
            {
                if (string.Equals(name, GameProcessNames[i], StringComparison.OrdinalIgnoreCase))
                {
                    _cachedDecision = true;
                    return true;
                }
            }

            _cachedDecision = false;
            return false;
        }
        catch
        {
            // Most likely cause: the foreground process exited between GetForegroundWindow()
            // and Process.GetProcessById() — extremely transient.  Less commonly: access
            // denied on a protected process (System Idle, lsass).  Default to "show" on any
            // error so a diagnostic glitch doesn't black-hole the overlay.
            _cachedProcessName = "";
            _cachedDecision = true;
            return true;
        }
    }

    private static string ResolveSelfProcessName()
    {
        try
        {
            using var self = Process.GetCurrentProcess();
            return self.ProcessName ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
