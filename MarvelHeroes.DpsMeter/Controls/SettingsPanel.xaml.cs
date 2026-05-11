using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using MarvelHeroesComporator.NetworkSniffer;
using MarvelHeroes.DpsMeter.Services;

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
    private bool _suppressShowSplinter;
    private bool _suppressSplinterSound;
    private bool _suppressLogging;
    private bool _suppressScale;

    // ── Events surfaced upward ────────────────────────────────────────────────────────────────
    // Same set MainAppWindow forwards from DpsDisplayPanel so the presenter can subscribe
    // once and let either UI surface trigger the action.

    public event Action<bool>? BossOnlyToggled;
    public event Action?       ClearDpsRequested;
    public event Action?       ResetMaxHitRecordRequested;
    public event Action?       ResetSplinterCooldownRequested;
    public event Action?       ArmSplinterCooldownRequested;

    public SettingsPanel()
    {
        InitializeComponent();
        // Populate the read-only display bits that don't depend on settings.
        LogPathText.Text = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MarvelHeroesComporator", "dps-meter.log");

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        AboutText.Text = $"Marvel Heroes DPS Meter  ·  v{version}\n" +
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
        SetChecked(ShowSplinterCheckbox,        settings.ShowEternitySplinterTracker, ref _suppressShowSplinter);
        SetChecked(SplinterSoundCheckbox,       settings.SplinterCooldownSoundEnabled, ref _suppressSplinterSound);

        // Custom splinter sound path -- read-only textbox so the user always sees what's
        // configured; "(system default)" placeholder when empty.
        SplinterSoundPathText.Text = string.IsNullOrWhiteSpace(settings.SplinterCooldownSoundPath)
            ? "(system default)"
            : settings.SplinterCooldownSoundPath;

        // Diagnostics
        SetChecked(LoggingCheckbox,             settings.LoggingEnabled,             ref _suppressLogging);

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
        // what'd play on a real cooldown expiry (custom file if set, system fallback if not).
        SplinterCooldownSoundPlayer.Play(_settings?.SplinterCooldownSoundPath);
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
}
