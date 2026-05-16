using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MarvelHeroesComporator.NetworkSniffer;
using MarvelHeroes.DpsMeter.Services;
using MarvelHeroes.DpsMeter.Windows;

namespace MarvelHeroes.DpsMeter.Controls;

/// <summary>
/// Consolidates the right-click-menu settings into a proper tabbed surface in the main app.
/// Backed by the same <see cref="DpsOverlaySettingsFile"/> singleton the panels share, so
/// toggles here persist through the existing <see cref="DpsOverlaySettingsFile.Save"/> path
/// and take effect on the next presenter read (or immediately for the cases routed via
/// events -- BossOnlyToggled etc.).
///
/// <para>Cosmetic drift caveat: the existing <see cref="DpsDisplayPanel"/> caches some of
/// these settings in instance fields and reflects them in its right-click menu checkmarks.
/// Toggling a setting here updates the persisted file but does NOT re-sync the live panel's
/// menu checkmark -- a panel restart (or app restart) shows the correct state.  The actual
/// behaviour (boss-only filter, sound playback, etc.) IS correct because either:</para>
/// <list type="bullet">
///   <item>The setting is consulted at-use-time off the shared settings object
///         (sound enabled, logging enabled).</item>
///   <item>The setting flows through an event we re-emit here
///         (<see cref="BossOnlyToggled"/>, etc.).</item>
/// </list>
/// </summary>
public partial class SettingsPanel : UserControl
{
    private DpsOverlaySettingsFile? _settings;

    // Per-checkbox suppression flags so initial state-sync in Initialize() doesn't recursively
    // fire the user-facing Checked / Unchecked handlers (which would in turn re-save the
    // settings file, fire events, etc.).  Same pattern DpsDisplayPanel uses for its menu.
    private bool _suppressBossOnly;
    private bool _suppressShowBossSection;
    private bool _suppressShowPowerBreakdown;
    private bool _suppressShowBuffPanels;
    private bool _suppressShowOverlayDpsSummary;
    private bool _suppressShowSplinter;
    private bool _suppressSplinterSound;
    private bool _suppressSplinterVolume;
    private bool _suppressSplinterDropVolume;
    private bool _suppressSplinterArmHotkey;
    private bool _suppressLogging;
    private bool _suppressVerboseLogging;
    private bool _suppressScale;

    // ── Events surfaced upward ────────────────────────────────────────────────────────────────
    // Same set MainAppWindow forwards from DpsDisplayPanel so the presenter can subscribe
    // once and let either UI surface trigger the action.

    public event Action<bool>? BossOnlyToggled;
    /// <summary>Fires when the user toggles "Show buffs and procs".  The live dashboard
    /// listens so it can hide/show the stats tile row and the two-tier buff strip in real
    /// time without waiting for an app restart; the persisted setting controls startup state.</summary>
    public event Action<bool>? ShowBuffPanelsToggled;
    /// <summary>Fires when the user toggles "Show DPS summary in overlay".  The presenter
    /// listens and pushes the new visibility down to the floating overlay's DpsDisplayPanel
    /// so the change takes effect immediately.</summary>
    public event Action<bool>? ShowOverlayDpsSummaryToggled;
    public event Action?       ClearDpsRequested;
    public event Action?       ResetMaxHitRecordRequested;
    public event Action?       ResetSplinterCooldownRequested;
    public event Action?       ArmSplinterCooldownRequested;
    /// <summary>Fires after the user finishes rebinding the global hotkey via the
    /// Settings UI.  Args are the new (modifiers, vk) pair.  Presenter listens to
    /// re-register the system hotkey with the new combo.</summary>
    public event Action<uint, uint>? SplinterArmHotkeyChanged;
    /// <summary>Fires when the user toggles the hotkey on or off.  Presenter listens to
    /// register / unregister the system hotkey accordingly.</summary>
    public event Action<bool>? SplinterArmHotkeyEnabledChanged;

    public SettingsPanel()
    {
        InitializeComponent();
        // Populate the read-only display bits that don't depend on settings.
        LogPathText.Text = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MarvelHeroesComporator", "dps-meter.log");

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        AboutText.Text = $"Cerebro  ·  DPS meter for Marvel Heroes  ·  v{version}\n" +
                         $"Settings file: %LocalAppData%\\MarvelHeroesComporator\\dps-overlay.json";
    }

    /// <summary>Wire the panel to the shared settings object and sync every control to the
    /// current values.  Idempotent -- safe to call again if settings change externally.</summary>
    public void Initialize(DpsOverlaySettingsFile settings)
    {
        _settings = settings;

        // Display checkboxes
        SetChecked(BossOnlyCheckbox,            settings.BossDpsOnly,                ref _suppressBossOnly);
        SetChecked(ShowBossSectionCheckbox,     settings.ShowBossSection,            ref _suppressShowBossSection);
        SetChecked(ShowPowerBreakdownCheckbox,  settings.ShowPowerBreakdown,         ref _suppressShowPowerBreakdown);
        SetChecked(ShowBuffPanelsCheckbox,      settings.ShowBuffPanels,             ref _suppressShowBuffPanels);
        SetChecked(ShowOverlayDpsSummaryCheckbox, settings.ShowOverlayDpsSummary,    ref _suppressShowOverlayDpsSummary);
        SetChecked(ShowSplinterCheckbox,        settings.ShowEternitySplinterTracker, ref _suppressShowSplinter);
        SetChecked(SplinterSoundCheckbox,       settings.SplinterCooldownSoundEnabled, ref _suppressSplinterSound);

        // Custom splinter cooldown-sound path -- read-only textbox so the user always sees
        // what's configured; "(system default)" placeholder when empty.
        SplinterSoundPathText.Text = string.IsNullOrWhiteSpace(settings.SplinterCooldownSoundPath)
            ? "(system default)"
            : settings.SplinterCooldownSoundPath;

        // Drop-sound path -- separate field.  Placeholder reads "(same as cooldown sound)"
        // when unset because that's the actual fallback behavior in PlaySplinterAlert.
        SplinterDropSoundPathText.Text = string.IsNullOrWhiteSpace(settings.SplinterDropSoundPath)
            ? "(same as cooldown sound)"
            : settings.SplinterDropSoundPath;

        // Splinter volume sliders -- snap to settings without firing the ValueChanged path
        // that would re-save (would be a no-op but the suppression keeps the flow clean).
        _suppressSplinterVolume = true;
        try { SplinterVolumeSlider.Value = Math.Clamp(settings.SplinterCooldownSoundVolume, 0.0, 1.0); }
        finally { _suppressSplinterVolume = false; }
        UpdateSplinterVolumeReadout(SplinterVolumeSlider.Value);

        _suppressSplinterDropVolume = true;
        try { SplinterDropVolumeSlider.Value = Math.Clamp(settings.SplinterDropSoundVolume, 0.0, 1.0); }
        finally { _suppressSplinterDropVolume = false; }
        UpdateSplinterDropVolumeReadout(SplinterDropVolumeSlider.Value);

        // Splinter "arm cooldown" hotkey -- toggle + readable binding string.
        SetChecked(SplinterArmHotkeyCheckbox, settings.SplinterArmHotkeyEnabled, ref _suppressSplinterArmHotkey);
        SplinterArmHotkeyText.Text = GlobalHotkey.Format(
            settings.SplinterArmHotkeyModifiers,
            settings.SplinterArmHotkeyVk);

        // Diagnostics
        SetChecked(LoggingCheckbox,             settings.LoggingEnabled,             ref _suppressLogging);
        SetChecked(VerboseLoggingCheckbox,      settings.VerboseDiagnostics,         ref _suppressVerboseLogging);

        // Scale slider -- snap initial position without firing the ValueChanged path.
        _suppressScale = true;
        try { ScaleSlider.Value = Math.Clamp(settings.Scale, 0.25, 2.0); }
        finally { _suppressScale = false; }
        UpdateScaleReadout(ScaleSlider.Value);
    }

    private static void SetChecked(CheckBox box, bool value, ref bool suppressFlag)
    {
        suppressFlag = true;
        try { box.IsChecked = value; }
        finally { suppressFlag = false; }
    }

    private void Save()
    {
        if (_settings == null) return;
        DpsOverlaySettingsFile.Save(_settings);
    }

    // ── Display toggles ───────────────────────────────────────────────────────────────────────

    private void BossOnlyCheckbox_OnChecked(object sender, RoutedEventArgs e)
    {
        if (_suppressBossOnly || _settings == null) return;
        _settings.BossDpsOnly = true; Save();
        // The presenter sets DpsMeter.BossOnlyMode (the runtime source of truth) via this
        // event -- the persisted setting only matters on the NEXT app launch.
        BossOnlyToggled?.Invoke(true);
    }
    private void BossOnlyCheckbox_OnUnchecked(object sender, RoutedEventArgs e)
    {
        if (_suppressBossOnly || _settings == null) return;
        _settings.BossDpsOnly = false; Save();
        BossOnlyToggled?.Invoke(false);
    }

    private void ShowBossSectionCheckbox_OnChecked(object sender, RoutedEventArgs e)
    {
        if (_suppressShowBossSection || _settings == null) return;
        _settings.ShowBossSection = true; Save();
    }
    private void ShowBossSectionCheckbox_OnUnchecked(object sender, RoutedEventArgs e)
    {
        if (_suppressShowBossSection || _settings == null) return;
        _settings.ShowBossSection = false; Save();
    }

    private void ShowPowerBreakdownCheckbox_OnChecked(object sender, RoutedEventArgs e)
    {
        if (_suppressShowPowerBreakdown || _settings == null) return;
        _settings.ShowPowerBreakdown = true; Save();
    }
    private void ShowPowerBreakdownCheckbox_OnUnchecked(object sender, RoutedEventArgs e)
    {
        if (_suppressShowPowerBreakdown || _settings == null) return;
        _settings.ShowPowerBreakdown = false; Save();
    }

    private void ShowBuffPanelsCheckbox_OnChecked(object sender, RoutedEventArgs e)
    {
        if (_suppressShowBuffPanels || _settings == null) return;
        _settings.ShowBuffPanels = true; Save();
        // Fire the live event so the dashboard updates immediately rather than waiting for
        // an app restart -- buff visibility is a "right now I want this gone" kind of toggle,
        // not a startup preference.
        ShowBuffPanelsToggled?.Invoke(true);
    }
    private void ShowBuffPanelsCheckbox_OnUnchecked(object sender, RoutedEventArgs e)
    {
        if (_suppressShowBuffPanels || _settings == null) return;
        _settings.ShowBuffPanels = false; Save();
        ShowBuffPanelsToggled?.Invoke(false);
    }

    private void ShowOverlayDpsSummaryCheckbox_OnChecked(object sender, RoutedEventArgs e)
    {
        if (_suppressShowOverlayDpsSummary || _settings == null) return;
        _settings.ShowOverlayDpsSummary = true; Save();
        ShowOverlayDpsSummaryToggled?.Invoke(true);
    }
    private void ShowOverlayDpsSummaryCheckbox_OnUnchecked(object sender, RoutedEventArgs e)
    {
        if (_suppressShowOverlayDpsSummary || _settings == null) return;
        _settings.ShowOverlayDpsSummary = false; Save();
        ShowOverlayDpsSummaryToggled?.Invoke(false);
    }

    private void ShowSplinterCheckbox_OnChecked(object sender, RoutedEventArgs e)
    {
        if (_suppressShowSplinter || _settings == null) return;
        _settings.ShowEternitySplinterTracker = true; Save();
    }
    private void ShowSplinterCheckbox_OnUnchecked(object sender, RoutedEventArgs e)
    {
        if (_suppressShowSplinter || _settings == null) return;
        _settings.ShowEternitySplinterTracker = false; Save();
    }

    private void SplinterSoundCheckbox_OnChecked(object sender, RoutedEventArgs e)
    {
        if (_suppressSplinterSound || _settings == null) return;
        _settings.SplinterCooldownSoundEnabled = true; Save();
    }
    private void SplinterSoundCheckbox_OnUnchecked(object sender, RoutedEventArgs e)
    {
        if (_suppressSplinterSound || _settings == null) return;
        _settings.SplinterCooldownSoundEnabled = false; Save();
    }

    private void SplinterVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateSplinterVolumeReadout(e.NewValue);
        if (_suppressSplinterVolume || _settings == null) return;
        // Persist rounded to 2 decimals so the JSON stays compact and the readout
        // can't drift due to slider snap quirks.
        _settings.SplinterCooldownSoundVolume = Math.Round(Math.Clamp(e.NewValue, 0.0, 1.0), 2);
        Save();
    }

    // ── Splinter "I got a splinter" global hotkey ────────────────────────────────────────────

    private void SplinterArmHotkeyCheckbox_OnChecked(object sender, RoutedEventArgs e)
    {
        if (_suppressSplinterArmHotkey || _settings == null) return;
        _settings.SplinterArmHotkeyEnabled = true; Save();
        SplinterArmHotkeyEnabledChanged?.Invoke(true);
    }
    private void SplinterArmHotkeyCheckbox_OnUnchecked(object sender, RoutedEventArgs e)
    {
        if (_suppressSplinterArmHotkey || _settings == null) return;
        _settings.SplinterArmHotkeyEnabled = false; Save();
        SplinterArmHotkeyEnabledChanged?.Invoke(false);
    }

    /// <summary>Captures the user's next key combo as the new hotkey.  Pops a small modal
    /// "press a key combo" window because trying to capture from a normal Settings-panel
    /// keyboard handler is hairy: WPF's input system swallows modifier-only events, the
    /// user has to be told what state we're in ("currently listening"), and we want a
    /// clean Esc-to-cancel.  A focused dialog is simpler than juggling state on the
    /// panel itself.</summary>
    private void RebindSplinterHotkeyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_settings == null) return;
        var dlg = new HotkeyCaptureWindow
        {
            Owner = Window.GetWindow(this),
            // Pre-fill with the current binding so the user can see what they're replacing.
            InitialDisplay = GlobalHotkey.Format(
                _settings.SplinterArmHotkeyModifiers,
                _settings.SplinterArmHotkeyVk),
        };
        var result = dlg.ShowDialog();
        if (result == true && dlg.CapturedModifiers != 0 && dlg.CapturedVk != 0)
        {
            _settings.SplinterArmHotkeyModifiers = dlg.CapturedModifiers;
            _settings.SplinterArmHotkeyVk        = dlg.CapturedVk;
            Save();
            SplinterArmHotkeyText.Text = GlobalHotkey.Format(dlg.CapturedModifiers, dlg.CapturedVk);
            SplinterArmHotkeyChanged?.Invoke(dlg.CapturedModifiers, dlg.CapturedVk);
        }
    }

    private void UpdateSplinterVolumeReadout(double value)
    {
        // Guard for the same pre-construction null trap as the scale-slider readout --
        // the Slider fires ValueChanged during InitializeComponent when its default
        // (0.0) is assigned, BEFORE the sibling SplinterVolumeReadout TextBlock has
        // been parsed.
        if (SplinterVolumeReadout == null) return;
        SplinterVolumeReadout.Text = $"{value * 100:0}%";
    }

    // ── Scale ─────────────────────────────────────────────────────────────────────────────────

    private void ScaleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateScaleReadout(e.NewValue);
        if (_suppressScale || _settings == null) return;
        // Persist only -- the live DpsDisplayPanel applies its own Scale based on what it
        // read at Initialize time.  A future commit could wire a scale-changed event to
        // re-apply live; for now scale changes show up on next launch (acceptable since
        // settings is a "set and forget" surface).
        _settings.Scale = Math.Round(e.NewValue, 2);
        Save();
    }

    private void UpdateScaleReadout(double value)
    {
        // The Slider fires ValueChanged during InitializeComponent when its Value default
        // (0.0) gets assigned, BEFORE the sibling ScaleReadout TextBlock has been parsed
        // into existence.  Guard accordingly -- the next call (post-InitializeComponent, from
        // Initialize() or a user drag) will succeed normally.
        if (ScaleReadout == null) return;
        ScaleReadout.Text = $"{value * 100:0}%";
    }

    // ── Diagnostics ───────────────────────────────────────────────────────────────────────────

    private void LoggingCheckbox_OnChecked(object sender, RoutedEventArgs e)
    {
        if (_suppressLogging || _settings == null) return;
        _settings.LoggingEnabled = true;
        DpsOverlaySettingsFile.IsLoggingEnabled = true;   // flip the process-wide gate too
        Save();
    }
    private void LoggingCheckbox_OnUnchecked(object sender, RoutedEventArgs e)
    {
        if (_suppressLogging || _settings == null) return;
        _settings.LoggingEnabled = false;
        DpsOverlaySettingsFile.IsLoggingEnabled = false;
        Save();
    }

    private void VerboseLoggingCheckbox_OnChecked(object sender, RoutedEventArgs e)
    {
        if (_suppressVerboseLogging || _settings == null) return;
        _settings.VerboseDiagnostics = true;
        DpsOverlaySettingsFile.IsVerboseDiagnosticsEnabled = true;
        Save();
    }
    private void VerboseLoggingCheckbox_OnUnchecked(object sender, RoutedEventArgs e)
    {
        if (_suppressVerboseLogging || _settings == null) return;
        _settings.VerboseDiagnostics = false;
        DpsOverlaySettingsFile.IsVerboseDiagnosticsEnabled = false;
        Save();
    }

    private void OpenLogFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MarvelHeroesComporator");
        try
        {
            Directory.CreateDirectory(folder);
            // Open the folder in Explorer.  UseShellExecute=true so the OS handles the URI;
            // ProcessStartInfo without it would try to run the folder as an executable.
            Process.Start(new ProcessStartInfo
            {
                FileName        = folder,
                UseShellExecute = true,
            });
        }
        catch { /* best-effort; the user can navigate manually if Explorer is unavailable */ }
    }

    // ── Action buttons ────────────────────────────────────────────────────────────────────────

    private void ClearDpsButton_Click(object sender, RoutedEventArgs e)
        => ClearDpsRequested?.Invoke();

    private void ResetMaxHitButton_Click(object sender, RoutedEventArgs e)
    {
        // Same confirmation dialog the right-click menu shows -- destructive, no undo.
        var result = MessageBox.Show(
            "Erase the saved max-hit record for the current hero?\n\nThis cannot be undone. " +
            "Session and fight max-hit values are not affected.",
            "Reset max-hit record",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes)
            ResetMaxHitRecordRequested?.Invoke();
    }

    private void ResetSplinterButton_Click(object sender, RoutedEventArgs e)
        => ResetSplinterCooldownRequested?.Invoke();

    private void ArmSplinterButton_Click(object sender, RoutedEventArgs e)
        => ArmSplinterCooldownRequested?.Invoke();

    private void TestSoundButton_Click(object sender, RoutedEventArgs e)
    {
        // Route through the same helper the presenter uses, so the test always matches
        // what'd play on a real splinter alert (custom file if set + volume slider, or
        // system fallback if no path is configured).
        SplinterCooldownSoundPlayer.Play(
            _settings?.SplinterCooldownSoundPath,
            _settings?.SplinterCooldownSoundVolume ?? 1.0);
    }

    private void BrowseSplinterSoundButton_Click(object sender, RoutedEventArgs e)
    {
        if (_settings == null) return;
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title  = "Pick a sound file for the Splinter cooldown notification",
            // The filter list mirrors what WPF's MediaPlayer can natively decode.
            // Most users will pick a WAV or MP3; the others are listed for completeness.
            Filter = "Sound files (*.wav;*.mp3;*.wma;*.aac)|*.wav;*.mp3;*.wma;*.aac"
                   + "|WAV (*.wav)|*.wav"
                   + "|MP3 (*.mp3)|*.mp3"
                   + "|All files (*.*)|*.*",
            CheckFileExists = true,
        };
        if (string.IsNullOrWhiteSpace(_settings.SplinterCooldownSoundPath) == false)
        {
            try { dialog.InitialDirectory = Path.GetDirectoryName(_settings.SplinterCooldownSoundPath); }
            catch { /* invalid path -- let the dialog pick its own default */ }
        }
        if (dialog.ShowDialog() == true)
        {
            _settings.SplinterCooldownSoundPath = dialog.FileName;
            SplinterSoundPathText.Text = dialog.FileName;
            Save();
        }
    }

    private void ClearSplinterSoundButton_Click(object sender, RoutedEventArgs e)
    {
        if (_settings == null) return;
        _settings.SplinterCooldownSoundPath = null;
        SplinterSoundPathText.Text = "(system default)";
        Save();
    }

    // ── Drop-sound handlers ──────────────────────────────────────────────────────────────
    // Mirror the cooldown-sound handlers above, just bound to the second pair of settings
    // fields (SplinterDropSoundPath / SplinterDropSoundVolume).  Volume changes save
    // immediately; path changes are persisted on Browse-confirm or Clear.

    private void BrowseSplinterDropSoundButton_Click(object sender, RoutedEventArgs e)
    {
        if (_settings == null) return;
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title  = "Pick a sound file for the Splinter DROP event",
            Filter = "Sound files (*.wav;*.mp3;*.wma;*.aac)|*.wav;*.mp3;*.wma;*.aac"
                   + "|WAV (*.wav)|*.wav"
                   + "|MP3 (*.mp3)|*.mp3"
                   + "|All files (*.*)|*.*",
            CheckFileExists = true,
        };
        if (!string.IsNullOrWhiteSpace(_settings.SplinterDropSoundPath))
        {
            try { dialog.InitialDirectory = Path.GetDirectoryName(_settings.SplinterDropSoundPath); }
            catch { /* invalid path -- let the dialog pick its own default */ }
        }
        if (dialog.ShowDialog() == true)
        {
            _settings.SplinterDropSoundPath = dialog.FileName;
            SplinterDropSoundPathText.Text  = dialog.FileName;
            Save();
        }
    }

    private void ClearSplinterDropSoundButton_Click(object sender, RoutedEventArgs e)
    {
        if (_settings == null) return;
        _settings.SplinterDropSoundPath = null;
        // Placeholder reflects what actually happens when this is unset -- drop event falls
        // back to the cooldown sound (see PlaySplinterAlert).  Clearer than "(system default)"
        // because users would otherwise expect the Windows asterisk instead.
        SplinterDropSoundPathText.Text  = "(same as cooldown sound)";
        Save();
    }

    private void SplinterDropVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateSplinterDropVolumeReadout(e.NewValue);
        if (_suppressSplinterDropVolume || _settings == null) return;
        _settings.SplinterDropSoundVolume = Math.Clamp(e.NewValue, 0.0, 1.0);
        Save();
    }

    private void UpdateSplinterDropVolumeReadout(double value)
        => SplinterDropVolumeReadout.Text = $"{Math.Clamp(value, 0.0, 1.0) * 100:0}%";
}
