using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using MarvelHeroes.DpsMeter.Services;

namespace MarvelHeroes.DpsMeter.Windows;

/// <summary>
/// Small modal "press a key combo" dialog for capturing a global hotkey.  Hosts a
/// keyboard listener and validates that the user pressed at least one modifier plus a
/// non-modifier key before allowing them to accept.  Used by the Settings panel's
/// "Rebind…" button for the splinter-arm hotkey; could be reused for any future global
/// hotkey settings without changes.
///
/// <para>Why a dialog instead of capturing inline on the Settings panel: WPF input
/// routing makes "while focused, listen for arbitrary key combos" finicky.  Modifier-only
/// presses arrive but you need state to track them; the user has to be told what mode
/// the UI is in ("listening for key…"); and Esc-to-cancel wants to suppress the panel's
/// own default behaviour.  A focused dialog with its own KeyDown handler is much simpler
/// to reason about and gives a clear "you are now binding a hotkey" affordance.</para>
/// </summary>
public partial class HotkeyCaptureWindow : Window
{
    /// <summary>Pre-fill text shown until the user presses a key.  Set by the caller to
    /// e.g. "Ctrl+Shift+E" so they can see what they're about to replace.</summary>
    public string InitialDisplay { get; set; } = "(waiting…)";

    /// <summary>Modifier bitmask captured from the user's last valid key press.
    /// Combination of <see cref="GlobalHotkey.MOD_CONTROL"/> / <c>MOD_ALT</c> /
    /// <c>MOD_SHIFT</c> / <c>MOD_WIN</c>.  Zero if nothing valid was captured.</summary>
    public uint CapturedModifiers { get; private set; }

    /// <summary>Win32 virtual-key code captured from the user's last valid key press.
    /// Zero if nothing valid was captured.</summary>
    public uint CapturedVk { get; private set; }

    public HotkeyCaptureWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (!string.IsNullOrEmpty(InitialDisplay))
                DisplayText.Text = InitialDisplay;
            // Make sure we get focus so KeyDown fires here, not on whatever button the
            // user clicked to open us.
            Focus();
        };
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        // Esc cancels via the standard IsCancel=true button -- don't capture it as
        // a binding.
        if (e.Key == Key.Escape) return;

        // Skip modifier-only presses -- they're useful for showing "ctrl is held" feedback
        // but we can't bind to a modifier-only combo (RegisterHotKey requires a non-modifier
        // virtual key).  We DO show the modifier-only state in the display so the user sees
        // the modifiers being captured live; we just don't enable the Accept button yet.
        Key actualKey = e.Key == Key.System ? e.SystemKey : e.Key;
        bool isModifierOnly =
            actualKey == Key.LeftCtrl   || actualKey == Key.RightCtrl   ||
            actualKey == Key.LeftAlt    || actualKey == Key.RightAlt    ||
            actualKey == Key.LeftShift  || actualKey == Key.RightShift  ||
            actualKey == Key.LWin       || actualKey == Key.RWin;

        // Build the modifier mask from the current keyboard state.
        uint mods = 0;
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) mods |= GlobalHotkey.MOD_CONTROL;
        if ((Keyboard.Modifiers & ModifierKeys.Alt)     != 0) mods |= GlobalHotkey.MOD_ALT;
        if ((Keyboard.Modifiers & ModifierKeys.Shift)   != 0) mods |= GlobalHotkey.MOD_SHIFT;
        if ((Keyboard.Modifiers & ModifierKeys.Windows) != 0) mods |= GlobalHotkey.MOD_WIN;

        if (isModifierOnly)
        {
            // Show "Ctrl+…" so the user sees their modifiers being detected.
            DisplayText.Text = mods == 0 ? "(press a key…)" : GlobalHotkey.Format(mods, 0) + "+…";
            AcceptButton.IsEnabled = false;
            e.Handled = true;
            return;
        }

        // Require at least one modifier -- a bare letter hotkey (e.g. just "E") would
        // collide with normal typing in every other app on the system.
        if (mods == 0)
        {
            DisplayText.Text = "(need at least one modifier)";
            AcceptButton.IsEnabled = false;
            e.Handled = true;
            return;
        }

        // Translate the WPF Key to a Win32 VK.  KeyInterop has a helper for this.
        uint vk = (uint)KeyInterop.VirtualKeyFromKey(actualKey);
        if (vk == 0)
        {
            DisplayText.Text = "(unsupported key)";
            AcceptButton.IsEnabled = false;
            e.Handled = true;
            return;
        }

        CapturedModifiers = mods;
        CapturedVk        = vk;
        DisplayText.Text  = GlobalHotkey.Format(mods, vk);
        AcceptButton.IsEnabled = true;
        e.Handled = true;
    }

    private void AcceptButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
