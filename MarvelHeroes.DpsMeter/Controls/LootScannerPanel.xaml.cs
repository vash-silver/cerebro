using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MarvelHeroes.DpsMeter.Services;

namespace MarvelHeroes.DpsMeter.Controls;

/// <summary>
/// Loot Scanner configuration tab.  Lets the user pick which affix patterns the hunt
/// match should care about, tune the minimum hit threshold, and gate by rarity / self.
/// All changes save instantly to <c>loot-hunt-config.json</c> via
/// <see cref="LootHuntConfig.Save"/> and publish through
/// <see cref="LootHuntConfig.ReplaceCurrent"/> so the live scanner sees them mid-session.
/// </summary>
public partial class LootScannerPanel : UserControl
{
    // Suppress flag pattern: when we sync the UI from a loaded config in Initialize(),
    // the checkbox/slider Changed events fire as a side-effect of setting their values.
    // Those events would re-write the config back to disk with the same values, fine in
    // theory but pointless I/O + log noise.  The flag short-circuits handlers during
    // programmatic initialization.
    //
    // CRITICAL: must default to `true` -- WPF's XAML loader fires Slider.ValueChanged
    // BEFORE all controls are constructed (Slider.Minimum="1" triggers a coerce of Value,
    // which raises ValueChanged, which hits our handler, which tries to write to
    // MinHitsReadout.Text -- but MinHitsReadout hasn't been instantiated yet, NRE).
    // Initial-true skips those bootstrap events; OnLoaded flips it false after the real
    // SyncUiFromConfig pass.
    private bool _suppressEvents = true;

    public LootScannerPanel()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Build the category-grouped pattern list once on first load -- the catalog is
        // static so we don't need to refresh it on every load.
        BuildPatternList();
        SyncUiFromConfig();
        UpdateStatus();
        // Bootstrap finished -- from here onwards user interaction with the controls
        // should actually persist + publish.
        _suppressEvents = false;
    }

    /// <summary>Group the flat <see cref="AffixPatternCatalog.Patterns"/> list by Category
    /// and project each entry to a checkbox-bindable VM with a two-way IsSelected
    /// property.  The category groups feed the outer ItemsControl, the per-pattern list
    /// feeds the inner.</summary>
    private void BuildPatternList()
    {
        var groups = AffixPatternCatalog.Patterns
            .GroupBy(p => p.Category)
            .Select(g => new CategoryVm
            {
                CategoryLabel = g.Key.ToString(),
                Patterns = new ObservableCollection<PatternVm>(
                    g.Select(p => new PatternVm(this, p))),
            })
            .ToList();
        PatternList.ItemsSource = groups;
    }

    /// <summary>Push current config values into the UI controls -- called once on Loaded
    /// and any time the config changes externally (e.g. another tab somehow rewriting it,
    /// though right now this is the only writer).</summary>
    private void SyncUiFromConfig()
    {
        _suppressEvents = true;
        try
        {
            var cfg = LootHuntConfig.Current;
            EnabledCheck.IsChecked = cfg.Enabled;
            SelfOnlyCheck.IsChecked = cfg.SelfOnly;
            MinHitsSlider.Value = cfg.MinHits;
            MinHitsReadout.Text = cfg.MinHits.ToString();
            switch (cfg.Rarity)
            {
                case LootHuntConfig.RarityGate.Any:        RarityAnyRadio.IsChecked = true;    break;
                case LootHuntConfig.RarityGate.CosmicOnly: RarityCosmicRadio.IsChecked = true; break;
            }

            // Sound config UI sync.  Placeholder text mirrors the SettingsPanel's splinter-
            // sound convention so users recognise "(system default)" as "Windows asterisk
            // fallback" rather than something missing.
            SoundEnabledCheck.IsChecked = cfg.SoundEnabled;
            SoundPathText.Text = string.IsNullOrWhiteSpace(cfg.SoundPath)
                ? "(system default)"
                : cfg.SoundPath;
            SoundVolumeSlider.Value = Math.Clamp(cfg.SoundVolume, 0.0, 1.0);
            UpdateSoundVolumeReadout(SoundVolumeSlider.Value);
            // The pattern checkboxes' IsSelected flags pull from cfg.WantedPatterns at
            // VM construction time.  Force a refresh in case BuildPatternList was called
            // when the config was different from now (uncommon but defensive).
            if (PatternList.ItemsSource is IEnumerable<CategoryVm> groups)
            {
                foreach (var g in groups)
                    foreach (var p in g.Patterns)
                        p.SyncFromConfig();
            }
        }
        finally { _suppressEvents = false; }
    }

    private void UpdateStatus()
    {
        var cfg = LootHuntConfig.Current;
        if (!cfg.Enabled)
        {
            StatusLine.Text = "Hunt matches DISABLED -- toggle 'Enable hunt match alerts' to re-enable.";
            return;
        }
        if (cfg.WantedPatterns.Count == 0)
        {
            StatusLine.Text = "No affixes selected -- hunt match will never fire.  Pick at least one above.";
            return;
        }
        string rarityLabel = cfg.Rarity switch
        {
            LootHuntConfig.RarityGate.CosmicOnly => "Cosmic-rarity items only",
            LootHuntConfig.RarityGate.Any        => "any rarity",
            _                                    => "?",
        };
        string selfLabel = cfg.SelfOnly ? "your current hero only" : "any hero";
        StatusLine.Text =
            $"Active: alert on {selfLabel}, {rarityLabel}, when a drop matches at least " +
            $"{cfg.MinHits} of {cfg.WantedPatterns.Count} selected affixes.";
    }

    private void PersistAndPublish(LootHuntConfig cfg)
    {
        LootHuntConfig.Save(cfg);
        LootHuntConfig.ReplaceCurrent(cfg);
        UpdateStatus();
    }

    // ── Event handlers ──────────────────────────────────────────────────────────────

    private void EnabledCheck_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressEvents) return;
        var cfg = LootHuntConfig.Current;
        cfg.Enabled = EnabledCheck.IsChecked == true;
        PersistAndPublish(cfg);
    }

    private void SelfOnlyCheck_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressEvents) return;
        var cfg = LootHuntConfig.Current;
        cfg.SelfOnly = SelfOnlyCheck.IsChecked == true;
        PersistAndPublish(cfg);
    }

    private void RarityRadio_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressEvents) return;
        var cfg = LootHuntConfig.Current;
        cfg.Rarity = RarityCosmicRadio.IsChecked == true
            ? LootHuntConfig.RarityGate.CosmicOnly
            : LootHuntConfig.RarityGate.Any;
        PersistAndPublish(cfg);
    }

    private void MinHitsSlider_OnChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // Suppress check FIRST -- XAML-load fires ValueChanged before all named child
        // controls are realised, so any MinHitsReadout.Text write here would NRE.  Real
        // user interaction goes through OnLoaded's sync first, so by the time the user
        // touches the slider, MinHitsReadout exists and the readout-update is safe.
        if (_suppressEvents) return;
        int v = (int)Math.Round(e.NewValue);
        MinHitsReadout.Text = v.ToString();
        var cfg = LootHuntConfig.Current;
        cfg.MinHits = v;
        PersistAndPublish(cfg);
    }

    private void SoundEnabledCheck_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressEvents) return;
        var cfg = LootHuntConfig.Current;
        cfg.SoundEnabled = SoundEnabledCheck.IsChecked == true;
        PersistAndPublish(cfg);
    }

    private void BrowseSoundButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title  = "Pick a sound file for hunt-match alerts",
            // Mirrors the splinter-sound file picker -- WPF MediaPlayer can decode all
            // four of these formats.  WAV / MP3 are the practical 99%.
            Filter = "Sound files (*.wav;*.mp3;*.wma;*.aac)|*.wav;*.mp3;*.wma;*.aac"
                   + "|WAV (*.wav)|*.wav"
                   + "|MP3 (*.mp3)|*.mp3"
                   + "|All files (*.*)|*.*",
            CheckFileExists = true,
        };
        if (!string.IsNullOrWhiteSpace(LootHuntConfig.Current.SoundPath))
        {
            try { dialog.InitialDirectory = Path.GetDirectoryName(LootHuntConfig.Current.SoundPath); }
            catch { /* invalid path; let the dialog pick its own default */ }
        }
        if (dialog.ShowDialog() == true)
        {
            var cfg = LootHuntConfig.Current;
            cfg.SoundPath = dialog.FileName;
            SoundPathText.Text = dialog.FileName;
            PersistAndPublish(cfg);
        }
    }

    private void ClearSoundButton_Click(object sender, RoutedEventArgs e)
    {
        var cfg = LootHuntConfig.Current;
        cfg.SoundPath = null;
        SoundPathText.Text = "(system default)";
        PersistAndPublish(cfg);
    }

    private void SoundVolumeSlider_OnChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressEvents) return;
        UpdateSoundVolumeReadout(e.NewValue);
        var cfg = LootHuntConfig.Current;
        cfg.SoundVolume = Math.Clamp(e.NewValue, 0.0, 1.0);
        PersistAndPublish(cfg);
    }

    private void UpdateSoundVolumeReadout(double value)
        => SoundVolumeReadout.Text = $"{Math.Clamp(value, 0.0, 1.0) * 100:0}%";

    /// <summary>Called by a child pattern VM when its checkbox toggles -- mutates the
    /// shared <see cref="LootHuntConfig.WantedPatterns"/> set and persists.</summary>
    internal void OnPatternToggled(string substring, bool isSelected)
    {
        if (_suppressEvents) return;
        var cfg = LootHuntConfig.Current;
        if (isSelected) cfg.WantedPatterns.Add(substring);
        else            cfg.WantedPatterns.Remove(substring);
        PersistAndPublish(cfg);
    }

    // ── View-model types ─────────────────────────────────────────────────────────────

    private sealed class CategoryVm
    {
        public required string CategoryLabel { get; init; }
        public required ObservableCollection<PatternVm> Patterns { get; init; }
    }

    private sealed class PatternVm : INotifyPropertyChanged
    {
        private readonly LootScannerPanel _host;
        private readonly AffixPatternCatalog.Pattern _pattern;
        private bool _isSelected;

        public PatternVm(LootScannerPanel host, AffixPatternCatalog.Pattern pattern)
        {
            _host = host;
            _pattern = pattern;
            _isSelected = LootHuntConfig.Current.WantedPatterns.Contains(pattern.Substring);
        }

        public string Label       => _pattern.Label;
        public string Description => _pattern.Description;
        public string Substring   => _pattern.Substring;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                _host.OnPatternToggled(_pattern.Substring, value);
            }
        }

        /// <summary>Re-read selection state from the current config without firing the
        /// host callback.  Used by SyncUiFromConfig.</summary>
        public void SyncFromConfig()
        {
            bool nowSelected = LootHuntConfig.Current.WantedPatterns.Contains(_pattern.Substring);
            if (_isSelected == nowSelected) return;
            _isSelected = nowSelected;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
