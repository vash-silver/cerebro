using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MarvelHeroes.DpsMeter.Services;
using Microsoft.Win32;

namespace MarvelHeroes.DpsMeter.Controls;

/// <summary>
/// "Buff Tracker" tab.  WeakAuras-style discovery + watchlist UI for the buff system:
///
/// <list type="bullet">
///   <item><b>Watchlist</b> -- the user's picked set of buffs.  Persisted to
///         <c>buff-watchlist.json</c> via <see cref="TrackedBuffsConfig"/>.</item>
///   <item><b>Currently active</b> -- live snapshot of <c>BuffTracker.GetActiveBuffs()</c>
///         grouped by display name.  Each row has a Track / Tracked button so the user
///         can adopt a fresh proc the moment they notice it.</item>
///   <item><b>Recently seen</b> -- snapshot of <c>BuffTracker.GetRecentBuffs()</c> filtered
///         to entries whose <c>CurrentlyActive == 0</c>.  Same per-row Track button.</item>
/// </list>
///
/// <para>Refresh model: the panel polls the live tracker on a 2 Hz <see cref="DispatcherTimer"/>
/// while visible.  We don't subscribe to <c>BuffTracker.BuffChanged</c> directly because
/// (a) those events fire on the sniffer thread and would need cross-thread marshalling,
/// and (b) 2 Hz is fast enough for human perception of "the buff appeared" without
/// blasting the UI with re-renders during dense combat where 10+ buffs can apply per second.
/// The timer is stopped when the tab control switches away (visibility=Collapsed) so an
/// inactive Buff Tracker tab costs zero per-frame work.</para>
/// </summary>
public partial class BuffTrackerPanel : UserControl
{
    // Suppress flag pattern -- matches LootScannerPanel.  Defaults to true so the XAML
    // loader's initial Checkbox.IsChecked write doesn't cycle a save/publish before
    // Initialize() has had a chance to load the real config.
    private bool _suppressEvents = true;

    private BuffTracker? _buffTracker;
    private readonly DispatcherTimer _refreshTimer;

    public BuffTrackerPanel()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        // 2 Hz refresh while visible.  Cheap (a Dictionary.Values copy + a sort under
        // the BuffTracker lock); WPF's data-template ItemsControl rebuilds the visuals
        // from the new ItemsSource each tick, which at the row counts we expect (under
        // ~50 distinct buff names per session) is sub-millisecond.
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _refreshTimer.Tick += (_, _) => RefreshLists();

        // Republish-config event: when ReplaceCurrent fires (e.g. another surface
        // mutates the watchlist), re-sync our UI so the tracked-list view stays
        // consistent.  Unhook on Unloaded.
        TrackedBuffsConfig.Changed += OnConfigChanged;
    }

    /// <summary>Hand the live <see cref="BuffTracker"/> reference to the panel.  Called by
    /// the host (<c>MainAppWindow</c>) after the presenter has constructed the tracker --
    /// before this is called the panel renders empty lists since it has no data source.
    /// Passing <c>null</c> is harmless; the timer just polls an absent tracker and the
    /// lists stay empty.</summary>
    public void SetBuffTracker(BuffTracker? tracker)
    {
        _buffTracker = tracker;
        // Force an immediate refresh so the user doesn't wait 500 ms for the first paint
        // after the tracker is plumbed in.
        if (IsLoaded) RefreshLists();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // First-load: load config from disk + publish it.  This is the first time the
        // tab is being displayed in this session; the config might already be in
        // TrackedBuffsConfig.Current if some other surface loaded it earlier (the
        // presenter loads it at startup so the BuffStripPanel filter is wired before
        // any panel paints), but Load() is idempotent and we always want this panel's
        // view to reflect what's actually on disk.
        var loaded = TrackedBuffsConfig.Load();
        TrackedBuffsConfig.ReplaceCurrent(loaded);
        SyncUiFromConfig();

        // Initial paint.  RefreshLists itself is safe to call before the timer starts.
        RefreshLists();
        _refreshTimer.Start();

        _suppressEvents = false;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _refreshTimer.Stop();
    }

    /// <summary>Push current config values into UI controls.  Suppress-bracketed so the
    /// checkbox-change handler doesn't try to immediately re-publish.</summary>
    private void SyncUiFromConfig()
    {
        _suppressEvents = true;
        try
        {
            var cfg = TrackedBuffsConfig.Current;
            OnlyShowTrackedCheck.IsChecked = cfg.OnlyShowTracked;
            ShowStealthPillCheck.IsChecked = cfg.ShowStealthStatePill;
        }
        finally { _suppressEvents = false; }
    }

    private void OnConfigChanged(object? sender, EventArgs e)
    {
        // External update -- re-sync the master checkbox + re-render the tracked list.
        // RefreshLists rebuilds tracked/active/recent every tick anyway, so we don't
        // need to force a refresh from here.
        if (!Dispatcher.CheckAccess()) Dispatcher.BeginInvoke(new Action(SyncUiFromConfig));
        else SyncUiFromConfig();
    }

    // ── Refresh loop ─────────────────────────────────────────────────────────────────

    /// <summary>Rebuild all three lists from the live tracker.  Cheap; safe to call every
    /// UI tick.  Empty-hint visibility is updated based on each list's row count so the
    /// "no buffs yet" text only shows when the corresponding list is empty.</summary>
    private void RefreshLists()
    {
        var cfg = TrackedBuffsConfig.Current;

        // Build the three view-model collections.  Track button label / colours depend
        // on whether each row's ShortName is in the watchlist, so we compute that here
        // rather than relying on a converter.
        IReadOnlyList<RecentBuffSummary> recent = _buffTracker?.GetRecentBuffs()
            ?? Array.Empty<RecentBuffSummary>();
        var active     = new List<BuffRowVm>();
        var recentOnly = new List<BuffRowVm>();
        foreach (var r in recent)
        {
            bool isTracked = cfg.Tracked.Contains(r.ShortName);
            var vm = BuildRowVm(r, isTracked, cfg.GetIconPath(r.ShortName));
            if (r.CurrentlyActive > 0) active.Add(vm);
            else                       recentOnly.Add(vm);
        }

        // Tracked list: synthesise a VM per watchlist entry.  When a tracked name is
        // currently active we show the live counts; when it's not (e.g. user added a
        // buff name then removed the cosmetic that supplied it) we show "(not seen yet)".
        var tracked = new List<BuffRowVm>();
        foreach (var name in cfg.Tracked)
        {
            string? iconPath = cfg.GetIconPath(name);
            var match = recent.FirstOrDefault(r => string.Equals(r.ShortName, name, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                tracked.Add(BuildRowVm(match, isTracked: true, iconPath: iconPath));
            }
            else
            {
                (var icon, bool hasIcon, string tooltip) = LoadIconPreview(iconPath);
                string displayName = cfg.GetDisplayName(name);
                bool hasAlias = !string.Equals(displayName, name, StringComparison.Ordinal);
                tracked.Add(new BuffRowVm
                {
                    ShortName             = name,
                    DisplayName           = displayName,
                    IsNameReadOnly        = false,
                    NameTooltip           = hasAlias
                        ? $"Click to rename.  Original: {name}"
                        : "Click to rename this buff (Enter to save, Esc to cancel)",
                    StackLabel            = "",
                    StackBadgeVisibility  = Visibility.Collapsed,
                    Metadata              = "(not seen yet)",
                    TrackButtonLabel      = "Untrack",
                    TrackButtonBackground = TrackedButtonBg,
                    TrackButtonBorder     = TrackedButtonBorder,
                    IsTracked             = true,
                    IconImageSource       = icon,
                    HasIcon               = hasIcon,
                    IconTooltip           = tooltip,
                });
            }
        }

        TrackedList.ItemsSource = tracked;
        ActiveList.ItemsSource  = active;
        RecentList.ItemsSource  = recentOnly;

        WatchlistEmptyHint.Visibility = tracked.Count == 0    ? Visibility.Visible : Visibility.Collapsed;
        ActiveEmptyHint.Visibility    = active.Count == 0     ? Visibility.Visible : Visibility.Collapsed;
        RecentEmptyHint.Visibility    = recentOnly.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        // Active-count readout in the section header.  Counts unique names, not total
        // condId stacks -- a "x4" Empowered is one entry, not four.
        ActiveCountReadout.Text = active.Count == 1 ? "1 active" : $"{active.Count} active";

        // Status line: surface owner-id + history size for quick "is this thing working"
        // verification.  The user can flip Verbose Diagnostics for the full chatter.
        ulong owner = _buffTracker?.SelfOwnerId ?? 0;
        StatusLine.Text =
            owner == 0
                ? "Self-avatar not identified yet -- buffs won't be tracked until you load into a region with your hero."
                : $"Tracking owner=0x{owner:X}.  History: {recent.Count} distinct names since last hero change.  "
                  + $"Filter: {(cfg.OnlyShowTracked && cfg.Tracked.Count > 0 ? "ON" : "off")}.";
    }

    private static BuffRowVm BuildRowVm(RecentBuffSummary r, bool isTracked, string? iconPath = null)
    {
        // "Last fired" age in human terms.  Short, glanceable -- the user wants to scan
        // a list and find the buff they just saw fire, not parse a full timestamp.
        var age = DateTime.UtcNow - r.LastSeenUtc;
        string ago =
            age.TotalSeconds < 2  ? "just now"
            : age.TotalSeconds < 60 ? $"{(int)age.TotalSeconds}s ago"
            : age.TotalMinutes < 60 ? $"{(int)age.TotalMinutes}m ago"
            : $"{(int)age.TotalHours}h ago";

        (var icon, bool hasIcon, string tooltip) = LoadIconPreview(iconPath);

        string displayName = TrackedBuffsConfig.Current.GetDisplayName(r.ShortName);
        bool hasAlias = !string.Equals(displayName, r.ShortName, StringComparison.Ordinal);
        string nameTooltip = isTracked
            ? (hasAlias
                ? $"Click to rename.  Original: {r.ShortName}"
                : "Click to rename this buff (Enter to save, Esc to cancel)")
            : ""; // discovery rows: read-only, no tooltip

        return new BuffRowVm
        {
            ShortName             = r.ShortName,
            DisplayName           = displayName,
            IsNameReadOnly        = !isTracked,
            NameTooltip           = nameTooltip,
            StackLabel            = r.CurrentlyActive > 1 ? $"x{r.CurrentlyActive}" : "",
            StackBadgeVisibility  = r.CurrentlyActive > 1 ? Visibility.Visible : Visibility.Collapsed,
            Metadata              = $"{r.TotalFires} fires · {ago}",
            TrackButtonLabel      = isTracked ? "Untrack" : "+ Track",
            TrackButtonBackground = isTracked ? TrackedButtonBg     : UntrackedButtonBg,
            TrackButtonBorder     = isTracked ? TrackedButtonBorder : UntrackedButtonBorder,
            IsTracked             = isTracked,
            IconImageSource       = icon,
            HasIcon               = hasIcon,
            IconTooltip           = tooltip,
        };
    }

    /// <summary>Decode the user-configured icon file (if any) into a freezable
    /// <see cref="BitmapImage"/>.  Loads with <c>CacheOption=OnLoad</c> so the file isn't
    /// kept open between rebuilds (we re-decode each tick; cost is negligible for the
    /// 18x18 preview).  Returns <c>(null, false, "")</c> on null/empty path or any decode
    /// failure -- the UI gracefully falls back to text-only chips.</summary>
    private static (BitmapImage? image, bool hasIcon, string tooltip) LoadIconPreview(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return (null, false, "");
        try
        {
            // Two flavors of path get stored in TrackedBuffsConfig.IconPaths:
            //   * pack://application:,,,/Images/powers/<name>.png -- auto-suggested in-game
            //     icons (the user clicked Track and we filled in the source-power's icon
            //     via PowerIconByProto).  These don't exist as files on disk; they're
            //     resolved from the WPF Resource bundle.
            //   * Absolute file paths -- user-picked icons via the "Set icon..." dialog.
            // The discriminator is the URI scheme.  We attempt File.Exists ONLY for the
            // file-path flavor; pack URIs skip the existence check (the BitmapImage will
            // throw on decode if the resource is missing, which the catch handles).
            bool isPackUri = path.StartsWith("pack://", StringComparison.OrdinalIgnoreCase);
            if (!isPackUri && !File.Exists(path)) return (null, false, $"{path} (file not found)");

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption  = BitmapCacheOption.OnLoad;
            bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            bmp.UriSource    = new Uri(path, UriKind.Absolute);
            bmp.DecodePixelWidth = 64;  // generous upper bound so the same decode works in the bigger chip-strip render too
            bmp.EndInit();
            if (bmp.CanFreeze) bmp.Freeze();
            return (bmp, true, path);
        }
        catch (Exception ex)
        {
            return (null, false, $"{path} (decode failed: {ex.GetType().Name})");
        }
    }

    // Colour palette for the per-row Track button.  Tracked state uses the same orange the
    // section-header / accent uses elsewhere in the app; untracked is a neutral dark grey.
    private static readonly Brush TrackedButtonBg       = (Brush)new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xCC, 0x66)).GetAsFrozen();
    private static readonly Brush TrackedButtonBorder   = (Brush)new SolidColorBrush(Color.FromArgb(0xAA, 0xFF, 0xCC, 0x66)).GetAsFrozen();
    private static readonly Brush UntrackedButtonBg     = (Brush)new SolidColorBrush(Color.FromArgb(0xFF, 0x22, 0x22, 0x22)).GetAsFrozen();
    private static readonly Brush UntrackedButtonBorder = (Brush)new SolidColorBrush(Color.FromArgb(0x44, 0xFF, 0xFF, 0xFF)).GetAsFrozen();

    // ── Event handlers ──────────────────────────────────────────────────────────────

    private void OnlyShowTrackedCheck_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressEvents) return;
        var cfg = CloneCurrent();
        cfg.OnlyShowTracked = OnlyShowTrackedCheck.IsChecked == true;
        PublishConfig(cfg);
    }

    /// <summary>Toggle the derived Stealthed / Invisible state pill at the top of the
    /// chip strip.  Persisted to <c>buff-watchlist.json</c>; the chip strip's render
    /// path consults this on every tick so the pill flips on/off live.</summary>
    private void ShowStealthPillCheck_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressEvents) return;
        var cfg = CloneCurrent();
        cfg.ShowStealthStatePill = ShowStealthPillCheck.IsChecked == true;
        PublishConfig(cfg);
    }

    private void TrackButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        if (btn.Tag is not string shortName || string.IsNullOrEmpty(shortName)) return;

        var cfg = CloneCurrent();
        // Toggle: if it's already in the set, remove; otherwise add.  Same button serves
        // both "Track" and "Untrack" semantics depending on the row's current state.
        bool nowTracking = !cfg.Tracked.Remove(shortName);
        if (nowTracking)
        {
            cfg.Tracked.Add(shortName);

            // Auto-suggest icon: when the user adopts a buff into the watchlist AND we
            // haven't already got a custom icon for it AND the BuffTracker observed the
            // buff's source-power prototype (it always does for ability-applied buffs --
            // item-applied effects without a creator-power are the rare exception), look
            // up the in-game icon via PowerIconByProto and stamp it in.  The user can
            // still override via the "Change icon..." button if they want a different
            // image; auto-suggest only fills empty slots.
            if (!cfg.IconPaths.ContainsKey(shortName) && _buffTracker != null)
            {
                var summary = _buffTracker.GetRecentBuffs()
                    .FirstOrDefault(r => string.Equals(r.ShortName, shortName, StringComparison.OrdinalIgnoreCase));
                if (summary != null && summary.CreatorPowerProto != 0)
                {
                    var uri = PowerIconByProto.GetPackUri(summary.CreatorPowerProto);
                    if (!string.IsNullOrEmpty(uri))
                        cfg.IconPaths[shortName] = uri;
                }
            }
        }
        PublishConfig(cfg);
        // Force an immediate refresh so the button instantly switches state.  Without
        // this the user would wait up to 500 ms for the timer to re-render the row.
        RefreshLists();
    }

    private void ClearAllButton_Click(object sender, RoutedEventArgs e)
    {
        var cfg = CloneCurrent();
        cfg.Tracked.Clear();
        PublishConfig(cfg);
        RefreshLists();
    }

    private void ClearHistoryButton_Click(object sender, RoutedEventArgs e)
    {
        _buffTracker?.ClearRecentHistory();
        RefreshLists();
    }

    /// <summary>"Browse..." button -- opens the bundled-icon grid picker so the user can
    /// pick from the ~2.3k extracted in-game power/talent icons without using a file
    /// dialog.  Stores the picked icon's pack URI in the row's IconPaths entry; from
    /// there it loads identically to any other icon (both chip strip and watchlist row
    /// preview).  Cancel does nothing.</summary>
    private void BrowseIconButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        if (btn.Tag is not string shortName || string.IsNullOrEmpty(shortName)) return;

        // Owner is the main window so the modal positions on top of Cerebro and minimizes
        // / restores with it.  Showing the dialog blocks the UI thread but the work
        // inside is small (one ItemsControl rebuild + lazy-decoded thumbnails as the
        // user scrolls).
        var picker = new Windows.IconPickerWindow { Owner = Window.GetWindow(this) };
        if (picker.ShowDialog() != true) return;
        if (string.IsNullOrEmpty(picker.SelectedBasename)) return;

        var cfg = CloneCurrent();
        cfg.IconPaths[shortName] = BundledIconCatalog.GetPackUri(picker.SelectedBasename);
        PublishConfig(cfg);
        RefreshLists();
    }

    /// <summary>"Set icon..." / "Change icon..." button.  Opens a File Open dialog filtered
    /// to common image types and stashes the picked path in the tracked-buffs config under
    /// the row's ShortName.  Persisted via the standard save+publish path.  No-op when the
    /// user cancels the dialog.</summary>
    private void SetIconButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        if (btn.Tag is not string shortName || string.IsNullOrEmpty(shortName)) return;

        var dlg = new OpenFileDialog
        {
            Title  = $"Pick an image for \"{shortName}\"",
            Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp)|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp|All files (*.*)|*.*",
            CheckFileExists = true,
        };
        // Preserve the previous directory when re-picking so users curating multiple icons
        // don't have to navigate to the same folder repeatedly.
        string? prevPath = TrackedBuffsConfig.Current.GetIconPath(shortName);
        if (!string.IsNullOrWhiteSpace(prevPath) && File.Exists(prevPath))
            dlg.InitialDirectory = Path.GetDirectoryName(prevPath);

        if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;

        var cfg = CloneCurrent();
        cfg.IconPaths[shortName] = dlg.FileName;
        PublishConfig(cfg);
        RefreshLists();
    }

    // ── Inline rename (watchlist rows) ────────────────────────────────────────────────

    /// <summary>Select-all when the user clicks into the name TextBox so a rename starts
    /// with the existing text highlighted (matching most click-to-edit conventions in
    /// list UIs).  Skipped on read-only rows because there's no rename target there.</summary>
    private void NameTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb && !tb.IsReadOnly)
            tb.SelectAll();
    }

    /// <summary>Enter commits the rename, Escape reverts.  Keep handling minimal -- the
    /// commit-on-LostFocus path below handles the "user clicked elsewhere" case.</summary>
    private void NameTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox tb || tb.IsReadOnly) return;

        if (e.Key == Key.Enter)
        {
            CommitAlias(tb);
            // Pull focus off the TextBox so the visual highlight goes away after commit.
            // RefreshLists() rebuilds the row VMs from scratch so the TextBox loses focus
            // naturally; the explicit ClearFocus call is belt-and-suspenders.
            Keyboard.ClearFocus();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            // Revert: drop the edit by refreshing from the current config.  Don't commit.
            RefreshLists();
            Keyboard.ClearFocus();
            e.Handled = true;
        }
    }

    private void NameTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb && !tb.IsReadOnly)
            CommitAlias(tb);
    }

    /// <summary>Save the edited name to <see cref="TrackedBuffsConfig.Aliases"/>.  Empty
    /// or whitespace-only aliases (or aliases that match the original short name) remove
    /// the override rather than storing an explicit "no-op" entry -- keeps the on-disk
    /// JSON clean.</summary>
    private void CommitAlias(TextBox tb)
    {
        if (tb.Tag is not string shortName || string.IsNullOrEmpty(shortName)) return;
        string newName = (tb.Text ?? "").Trim();
        bool currentlyAliased = TrackedBuffsConfig.Current.Aliases.ContainsKey(shortName);
        bool wantAliased = !string.IsNullOrEmpty(newName)
                        && !string.Equals(newName, shortName, StringComparison.Ordinal);

        // Skip the publish round-trip when nothing actually changed -- avoids a needless
        // RefreshLists every time the user just clicks into a name and clicks out without
        // typing anything.
        string currentAlias = currentlyAliased ? TrackedBuffsConfig.Current.Aliases[shortName] : shortName;
        if (string.Equals(currentAlias, newName, StringComparison.Ordinal)) return;
        if (!currentlyAliased && !wantAliased) return;

        var cfg = CloneCurrent();
        if (wantAliased) cfg.Aliases[shortName] = newName;
        else             cfg.Aliases.Remove(shortName);
        PublishConfig(cfg);
        RefreshLists();
    }

    /// <summary>"✕" clear-icon button.  Removes the row's entry from
    /// <see cref="TrackedBuffsConfig.IconPaths"/> and persists, falling the chip strip
    /// back to its text-only rendering.</summary>
    private void ClearIconButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        if (btn.Tag is not string shortName || string.IsNullOrEmpty(shortName)) return;

        var cfg = CloneCurrent();
        if (cfg.IconPaths.Remove(shortName))
        {
            PublishConfig(cfg);
            RefreshLists();
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────

    /// <summary>Deep-ish clone of the current config so mutations don't accidentally
    /// affect the live snapshot before <see cref="PublishConfig"/> publishes the new
    /// instance.  HashSet is rebuilt with the same comparer so case-insensitive
    /// behaviour is preserved.</summary>
    private static TrackedBuffsConfig CloneCurrent()
    {
        var src = TrackedBuffsConfig.Current;
        return new TrackedBuffsConfig
        {
            OnlyShowTracked       = src.OnlyShowTracked,
            ShowStealthStatePill  = src.ShowStealthStatePill,
            Tracked   = new HashSet<string>(src.Tracked, StringComparer.OrdinalIgnoreCase),
            IconPaths = new Dictionary<string, string>(src.IconPaths, StringComparer.OrdinalIgnoreCase),
            Aliases   = new Dictionary<string, string>(src.Aliases,   StringComparer.OrdinalIgnoreCase),
        };
    }

    /// <summary>Save + publish.  Matches LootHuntConfig pattern: write to disk first
    /// (durability) then publish in-memory (live readers see the new state).</summary>
    private static void PublishConfig(TrackedBuffsConfig cfg)
    {
        TrackedBuffsConfig.Save(cfg);
        TrackedBuffsConfig.ReplaceCurrent(cfg);
    }

    /// <summary>View-model for one row in any of the three lists.  Built fresh each
    /// refresh tick (cheap; tens of rows max), bound through the static
    /// <c>BuffRowTemplate</c> DataTemplate in the XAML.</summary>
    public sealed class BuffRowVm
    {
        /// <summary>Unique key for the row -- the chip-short-name as derived by
        /// <c>BuffDisplayClassifier.ShortenForChip</c>.  Used as the dictionary key in
        /// <see cref="TrackedBuffsConfig.Tracked"/>, <see cref="TrackedBuffsConfig.IconPaths"/>,
        /// and <see cref="TrackedBuffsConfig.Aliases"/>.  Never shown to the user
        /// directly when an alias is set -- see <see cref="DisplayName"/>.</summary>
        public required string ShortName { get; init; }
        /// <summary>The name to RENDER -- alias if set, otherwise the short name.  Bound
        /// to the row's name TextBox; on commit the handler writes back to
        /// <c>cfg.Aliases[ShortName]</c>.</summary>
        public required string DisplayName { get; init; }
        /// <summary>Watchlist rows are editable (the user can rename their picks).
        /// Active / Recent (discovery) rows are read-only -- a rename there would have
        /// no effect since the buff isn't on the watchlist yet, and an editable input
        /// in those crowded lists would invite accidental edits.</summary>
        public required bool IsNameReadOnly { get; init; }
        /// <summary>Tooltip explaining the rename UX (only meaningful on watchlist rows).
        /// Shows the original name so users know what they're aliasing.</summary>
        public string NameTooltip { get; init; } = "";
        public required string StackLabel { get; init; }
        public required Visibility StackBadgeVisibility { get; init; }
        public required string Metadata { get; init; }
        public required string TrackButtonLabel { get; init; }
        public required Brush TrackButtonBackground { get; init; }
        public required Brush TrackButtonBorder { get; init; }
        public required bool IsTracked { get; init; }

        // ── Icon fields (Watchlist rows only) ──────────────────────────────────────────
        /// <summary>Decoded bitmap for the row's configured icon, or <c>null</c> when no
        /// icon is set (or when the configured path failed to decode -- e.g. the file was
        /// deleted after being chosen).</summary>
        public ImageSource? IconImageSource { get; init; }
        /// <summary>True when <see cref="IconImageSource"/> resolved successfully -- gates
        /// both the preview Image visibility AND the "✕" clear button visibility.</summary>
        public bool HasIcon { get; init; }
        public Visibility IconImageVisibility => HasIcon ? Visibility.Visible : Visibility.Collapsed;
        public Visibility ClearIconButtonVisibility => (IsTracked && HasIcon) ? Visibility.Visible : Visibility.Collapsed;
        /// <summary>The icon set / change button is only meaningful for tracked rows --
        /// non-tracked rows are still discovery candidates, and setting an icon before
        /// tracking would be confusing.</summary>
        public Visibility IconButtonRowVisibility => IsTracked ? Visibility.Visible : Visibility.Collapsed;
        public string IconButtonLabel => HasIcon ? "Custom file..." : "Custom file...";
        public string IconTooltip { get; init; } = "";
    }
}
