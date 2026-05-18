using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MarvelHeroes.DpsMeter.Services;

namespace MarvelHeroes.DpsMeter.Controls;

/// <summary>
/// "Cooldown Tracker" tab.  WeakAuras-style discovery + watchlist UI for power
/// cooldowns:
///
/// <list type="bullet">
///   <item><b>Watchlist</b> -- the user's tracked powers.  Each row exposes a
///         cooldown-duration TextBox so the user can configure how long the icon
///         should fade for after each activation.</item>
///   <item><b>Recently fired</b> -- live snapshot of every power the local player has
///         activated this session (per <see cref="CooldownTracker.GetRecentPowers"/>).
///         Click "+ Track" to add to the watchlist.</item>
/// </list>
///
/// <para>Refresh model: 2 Hz <see cref="DispatcherTimer"/> while visible -- the
/// recently-fired list re-renders to update the "last fired Xs ago" label.  Stopped
/// on <c>Unloaded</c> so an inactive tab costs nothing.  Same pattern as
/// <see cref="BuffTrackerPanel"/>.</para>
/// </summary>
public partial class CooldownTrackerPanel : UserControl
{
    private bool _suppressEvents = true;
    private CooldownTracker? _tracker;
    private readonly DispatcherTimer _refreshTimer;

    public CooldownTrackerPanel()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _refreshTimer.Tick += (_, _) => RefreshLists();
        CooldownTrackerConfig.Changed += OnConfigChanged;
    }

    /// <summary>Hand the live <see cref="CooldownTracker"/> reference to the panel.
    /// Called by the host (<c>MainAppWindow</c>) after the presenter has constructed
    /// the tracker -- before this is called the panel renders an empty
    /// "recently fired" list.</summary>
    public void SetCooldownTracker(CooldownTracker? tracker)
    {
        _tracker = tracker;
        if (IsLoaded) RefreshLists();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var loaded = CooldownTrackerConfig.Load();
        CooldownTrackerConfig.ReplaceCurrent(loaded);
        SyncUiFromConfig();
        RefreshLists();
        _refreshTimer.Start();
        _suppressEvents = false;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => _refreshTimer.Stop();

    private void SyncUiFromConfig()
    {
        _suppressEvents = true;
        try
        {
            var cfg = CooldownTrackerConfig.Current;
            FreeLayoutModeCheck.IsChecked = cfg.FreeLayoutMode;
            LockOverlayCheck.IsChecked    = cfg.OverlayLocked;
        }
        finally { _suppressEvents = false; }
    }

    private void OnConfigChanged(object? sender, EventArgs e)
    {
        if (!Dispatcher.CheckAccess()) Dispatcher.BeginInvoke(new Action(SyncUiFromConfig));
        else SyncUiFromConfig();
    }

    // ── Refresh loop ─────────────────────────────────────────────────────────────────

    private void RefreshLists()
    {
        var cfg = CooldownTrackerConfig.Current;
        var recent = _tracker?.GetRecentPowers() ?? Array.Empty<PowerCooldownState>();

        // Build the watchlist VMs.  Walk the config's Tracked list (which preserves
        // user-curated ordering) and synthesise a row per entry.  If the power was
        // also fired this session we tag the metadata with usage info.
        var tracked = new List<CooldownRowVm>();
        var trackedSet = new HashSet<uint>(cfg.Tracked);
        foreach (var protoId in cfg.Tracked)
        {
            var match = recent.FirstOrDefault(r => r.ProtoId == protoId);
            tracked.Add(BuildRowVm(protoId, isTracked: true, state: match));
        }

        // Recent list = powers fired this session that AREN'T tracked.  Tracked
        // entries already render up top so showing them again here would be
        // redundant.
        var recentOnly = new List<CooldownRowVm>();
        foreach (var s in recent)
        {
            if (trackedSet.Contains(s.ProtoId)) continue;
            recentOnly.Add(BuildRowVm(s.ProtoId, isTracked: false, state: s));
        }

        TrackedList.ItemsSource = tracked;
        RecentList.ItemsSource  = recentOnly;

        WatchlistEmptyHint.Visibility = tracked.Count    == 0 ? Visibility.Visible : Visibility.Collapsed;
        RecentEmptyHint.Visibility    = recentOnly.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        ulong owner = _tracker?.SelfOwnerId ?? 0;
        StatusLine.Text = owner == 0
            ? "Self-avatar not identified yet -- cooldowns won't track until you fire an ability in-game."
            : $"Tracking owner=0x{owner:X}.  {recent.Count} distinct powers fired this session.  Watchlist: {tracked.Count} tracked.";
    }

    private static CooldownRowVm BuildRowVm(uint protoId, bool isTracked, PowerCooldownState? state)
    {
        var cfg = CooldownTrackerConfig.Current;
        string displayName = cfg.GetDisplayName(protoId);

        // Resolve icon: config override first, then auto-resolved from prototype.
        string? iconPath = cfg.GetIconPath(protoId) ?? PowerIconByProto.GetPackUri(protoId);
        (var icon, bool hasIcon, string tooltip) = LoadIconPreview(iconPath);

        // Metadata: surface cooldown / activation state.  Two flavours:
        //   * Server cooldown observed: "8.0s CD · 2 fires"
        //   * Activation observed but no cooldown ever seen: "{fires} · {ago}"
        //   * Nothing observed for this watchlist entry: "(not fired yet)"
        string metadata;
        if (state != null)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (state.CooldownDurationMs > 0)
            {
                parts.Add($"{state.CooldownDurationMs / 1000.0:0.#}s CD");
                if (state.OnCooldown)
                {
                    double elapsedMs = (DateTime.UtcNow - state.CooldownStartUtc).TotalMilliseconds;
                    double remMs     = Math.Max(0, state.CooldownDurationMs - elapsedMs);
                    if (remMs > 0) parts.Add($"on cd · {remMs / 1000.0:0.#}s left");
                    else           parts.Add("ready");
                }
                else
                {
                    parts.Add("ready");
                }
            }
            if (state.TotalFires > 0)
            {
                var age = DateTime.UtcNow - state.LastFiredUtc;
                string ago = age.TotalSeconds < 2  ? "just now"
                           : age.TotalSeconds < 60 ? $"{(int)age.TotalSeconds}s ago"
                           : age.TotalMinutes < 60 ? $"{(int)age.TotalMinutes}m ago"
                           : $"{(int)age.TotalHours}h ago";
                parts.Add($"{state.TotalFires} fires · {ago}");
            }
            metadata = parts.Count > 0 ? string.Join(" · ", parts) : (isTracked ? "(not fired yet)" : "");
        }
        else
        {
            metadata = isTracked ? "(not fired yet)" : "";
        }

        return new CooldownRowVm
        {
            ProtoId              = protoId,
            DisplayName          = displayName,
            Metadata             = metadata,
            IconButtonRowVisibility = isTracked ? Visibility.Visible : Visibility.Collapsed,
            TrackButtonLabel     = isTracked ? "Untrack" : "+ Track",
            TrackButtonBackground = isTracked ? TrackedButtonBg     : UntrackedButtonBg,
            TrackButtonBorder     = isTracked ? TrackedButtonBorder : UntrackedButtonBorder,
            IsTracked            = isTracked,
            IconImageSource      = icon,
            HasIcon              = hasIcon,
            IconTooltip          = tooltip,
        };
    }

    private static (BitmapImage? image, bool hasIcon, string tooltip) LoadIconPreview(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return (null, false, "");
        try
        {
            bool isPackUri = path.StartsWith("pack://", StringComparison.OrdinalIgnoreCase);
            if (!isPackUri && !System.IO.File.Exists(path)) return (null, false, $"{path} (file not found)");
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption       = BitmapCacheOption.OnLoad;
            bmp.CreateOptions     = BitmapCreateOptions.IgnoreColorProfile;
            bmp.UriSource         = new Uri(path, UriKind.Absolute);
            bmp.DecodePixelWidth  = 64;
            bmp.EndInit();
            if (bmp.CanFreeze) bmp.Freeze();
            return (bmp, true, path);
        }
        catch (Exception ex) { return (null, false, $"{path} (decode failed: {ex.GetType().Name})"); }
    }

    private static readonly Brush TrackedButtonBg       = (Brush)new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xCC, 0x66)).GetAsFrozen();
    private static readonly Brush TrackedButtonBorder   = (Brush)new SolidColorBrush(Color.FromArgb(0xAA, 0xFF, 0xCC, 0x66)).GetAsFrozen();
    private static readonly Brush UntrackedButtonBg     = (Brush)new SolidColorBrush(Color.FromArgb(0xFF, 0x22, 0x22, 0x22)).GetAsFrozen();
    private static readonly Brush UntrackedButtonBorder = (Brush)new SolidColorBrush(Color.FromArgb(0x44, 0xFF, 0xFF, 0xFF)).GetAsFrozen();

    // ── Event handlers ──────────────────────────────────────────────────────────────

    private void FreeLayoutModeCheck_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressEvents) return;
        var cfg = CloneCurrent();
        cfg.FreeLayoutMode = FreeLayoutModeCheck.IsChecked == true;
        PublishConfig(cfg);
    }

    private void LockOverlayCheck_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressEvents) return;
        var cfg = CloneCurrent();
        cfg.OverlayLocked = LockOverlayCheck.IsChecked == true;
        PublishConfig(cfg);
    }

    private void TrackButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        if (!TryGetProtoId(btn.Tag, out uint protoId)) return;
        var cfg = CloneCurrent();
        if (cfg.Tracked.Contains(protoId)) cfg.Tracked.Remove(protoId);
        else                                cfg.Tracked.Add(protoId);
        PublishConfig(cfg);
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
        _tracker?.ClearRecentHistory();
        RefreshLists();
    }

    private void BrowseIconButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        if (!TryGetProtoId(btn.Tag, out uint protoId)) return;
        var picker = new Windows.IconPickerWindow { Owner = Window.GetWindow(this) };
        if (picker.ShowDialog() != true) return;
        if (string.IsNullOrEmpty(picker.SelectedBasename)) return;
        var cfg = CloneCurrent();
        cfg.IconPaths[protoId] = BundledIconCatalog.GetPackUri(picker.SelectedBasename);
        PublishConfig(cfg);
        RefreshLists();
    }

    private void ClearIconButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        if (!TryGetProtoId(btn.Tag, out uint protoId)) return;
        var cfg = CloneCurrent();
        if (cfg.IconPaths.Remove(protoId))
        {
            PublishConfig(cfg);
            RefreshLists();
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────

    private static bool TryGetProtoId(object? tag, out uint protoId)
    {
        protoId = 0;
        if (tag is uint u) { protoId = u; return true; }
        if (tag is string s && uint.TryParse(s, out var parsed)) { protoId = parsed; return true; }
        return false;
    }

    private static CooldownTrackerConfig CloneCurrent()
    {
        var src = CooldownTrackerConfig.Current;
        return new CooldownTrackerConfig
        {
            FreeLayoutMode = src.FreeLayoutMode,
            OverlayLocked  = src.OverlayLocked,
            Tracked   = new List<uint>(src.Tracked),
            Cooldowns = new Dictionary<uint, double>(src.Cooldowns),
            IconPaths = new Dictionary<uint, string>(src.IconPaths),
            Aliases   = new Dictionary<uint, string>(src.Aliases),
            Layouts   = new Dictionary<uint, CooldownLayout>(src.Layouts),
        };
    }

    private static void PublishConfig(CooldownTrackerConfig cfg)
    {
        CooldownTrackerConfig.Save(cfg);
        CooldownTrackerConfig.ReplaceCurrent(cfg);
    }

    /// <summary>Row VM for the watchlist + recently-fired lists.  Built fresh each
    /// refresh tick (no INPC); the lists are short so rebuilding is cheap.</summary>
    public sealed class CooldownRowVm
    {
        public required uint ProtoId { get; init; }
        public required string DisplayName { get; init; }
        public required string Metadata { get; init; }
        public required Visibility IconButtonRowVisibility { get; init; }
        public required string TrackButtonLabel { get; init; }
        public required Brush TrackButtonBackground { get; init; }
        public required Brush TrackButtonBorder { get; init; }
        public required bool IsTracked { get; init; }
        public ImageSource? IconImageSource { get; init; }
        public bool HasIcon { get; init; }
        public Visibility IconImageVisibility => HasIcon ? Visibility.Visible : Visibility.Collapsed;
        public Visibility ClearIconButtonVisibility => (IsTracked && CooldownTrackerConfig.Current.IconPaths.ContainsKey(ProtoId))
            ? Visibility.Visible : Visibility.Collapsed;
        public string IconTooltip { get; init; } = "";
    }
}
