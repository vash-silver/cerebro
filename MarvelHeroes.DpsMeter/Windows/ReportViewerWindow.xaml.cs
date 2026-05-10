using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using MarvelHeroes.DpsMeter.Models;

namespace MarvelHeroes.DpsMeter.Windows;

public partial class ReportViewerWindow : Window
{
    private const double DetailTrackWidthPx = 420.0;

    private static readonly Color[] s_pwrColors =
    {
        Color.FromRgb(0xFF, 0x69, 0x00), Color.FromRgb(0x3D, 0x8F, 0xD9),
        Color.FromRgb(0x4C, 0xAF, 0x50), Color.FromRgb(0xAB, 0x47, 0xBC),
        Color.FromRgb(0x00, 0xBC, 0xD4), Color.FromRgb(0xFF, 0xC1, 0x07),
        Color.FromRgb(0xE9, 0x1E, 0x63), Color.FromRgb(0x00, 0x96, 0x88),
    };

    private List<SnapshotListItem> _items = new();
    private DpsSnapshot? _selected;
    private int          _selectedHeroIndex = -1;  // index into _selected.Leaderboard

    private FileSystemWatcher? _watcher;
    private DispatcherTimer?   _refreshDebounce;

    private string _sortMode   = "date";
    private string _heroFilter = "";

    private List<DpsSnapshot.SparkPoint>? _pendingSparkline;

    public ReportViewerWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            InitWatcher();
            RefreshList();
        };
    }

    private void InitWatcher()
    {
        _refreshDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _refreshDebounce.Tick += (_, _) => { _refreshDebounce.Stop(); RefreshList(); };

        try
        {
            Directory.CreateDirectory(DpsReportStore.ReportsDirectory);
            _watcher = new FileSystemWatcher(DpsReportStore.ReportsDirectory, "dps-*.json")
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                EnableRaisingEvents = true,
            };
            _watcher.Created += OnReportsChanged;
            _watcher.Deleted += OnReportsChanged;
        }
        catch { }
    }

    private void OnReportsChanged(object sender, FileSystemEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            _refreshDebounce?.Stop();
            _refreshDebounce?.Start();
        });
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _refreshDebounce?.Stop();
        _watcher?.Dispose();
    }

    // ── List management ──────────────────────────────────────────────────────────────────────

    private void RefreshList()
    {
        if (!IsLoaded) return;  // called during InitializeComponent before elements are ready
        string? selectedId = _selected?.Id;
        var all = DpsReportStore.LoadAll();

        RebuildHeroFilter(all);

        IEnumerable<DpsSnapshot> view = all;
        if (!string.IsNullOrEmpty(_heroFilter))
            view = view.Where(s => s.HeroName == _heroFilter);

        view = _sortMode switch
        {
            "dps"  => view.OrderByDescending(s => s.Dps),
            "hero" => view.OrderBy(s => s.HeroName).ThenByDescending(s => s.SavedUtc),
            _      => view,
        };

        _items.Clear();
        foreach (var s in view) _items.Add(new SnapshotListItem(s));
        SnapshotList.ItemsSource = null;
        SnapshotList.ItemsSource = _items;

        if (selectedId != null)
        {
            int idx = _items.FindIndex(i => i.Snapshot.Id == selectedId);
            if (idx >= 0) { SnapshotList.SelectedIndex = idx; return; }
        }
        if (_items.Count > 0) SnapshotList.SelectedIndex = 0;
        else ShowEmpty();
    }

    private void RebuildHeroFilter(List<DpsSnapshot> all)
    {
        var heroes = all
            .Where(s => !string.IsNullOrEmpty(s.HeroName))
            .Select(s => s.HeroName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(h => h)
            .ToList();

        FilterCombo.SelectionChanged -= FilterCombo_SelectionChanged;
        try
        {
            FilterCombo.Items.Clear();
            FilterCombo.Items.Add(new ComboBoxItem { Content = "All heroes" });
            foreach (var h in heroes)
                FilterCombo.Items.Add(new ComboBoxItem { Content = h });

            if (!string.IsNullOrEmpty(_heroFilter) && heroes.Contains(_heroFilter, StringComparer.OrdinalIgnoreCase))
            {
                foreach (ComboBoxItem item in FilterCombo.Items)
                    if (item.Content?.ToString() == _heroFilter) { FilterCombo.SelectedItem = item; break; }
            }
            else
            {
                FilterCombo.SelectedIndex = 0;
                _heroFilter = "";
            }
        }
        finally { FilterCombo.SelectionChanged += FilterCombo_SelectionChanged; }
    }

    private void SortCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SortCombo.SelectedItem is ComboBoxItem ci)
            _sortMode = ci.Tag?.ToString() ?? "date";
        RefreshList();
    }

    private void FilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FilterCombo.SelectedItem is ComboBoxItem ci)
        {
            string v = ci.Content?.ToString() ?? "";
            _heroFilter = v == "All heroes" ? "" : v;
        }
        RefreshList();
    }

    private void SnapshotList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SnapshotList.SelectedItem is SnapshotListItem item)
        {
            _selected = item.Snapshot;
            DeleteButton.IsEnabled = true;
            CopyButton.IsEnabled   = true;
            ShowDetail(_selected);
        }
        else
        {
            _selected = null;
            DeleteButton.IsEnabled = false;
            CopyButton.IsEnabled   = false;
            ShowEmpty();
        }
    }

    // ── Detail rendering ─────────────────────────────────────────────────────────────────────

    private void ShowEmpty()
    {
        DetailPanel.Visibility = Visibility.Collapsed;
    }

    private void ShowDetail(DpsSnapshot s)
    {
        DetailPanel.Visibility = Visibility.Visible;

        DetailLabel.Text     = s.Label;
        DetailDate.Text      = s.SavedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        DetailMode.Text      = s.Mode;
        // Boss badge: only shown when we know which boss this fight was against.  Older
        // saves (and non-boss Session snapshots) leave BossName empty, so the badge stays
        // collapsed and the layout falls back to the original Mode/Hero/... arrangement.
        if (!string.IsNullOrEmpty(s.BossName))
        {
            DetailBoss.Text = s.BossName;
            DetailBossBorder.Visibility = Visibility.Visible;
        }
        else
        {
            DetailBossBorder.Visibility = Visibility.Collapsed;
        }
        DetailHero.Text      = string.IsNullOrEmpty(s.HeroName) ? "unknown hero" : s.HeroName;
        DetailDps.Text       = $"DPS: {FormatNum(s.Dps)}";
        DetailDuration.Text  = s.DurationSeconds > 0 ? FormatDuration(s.DurationSeconds) : "";
        DetailMaxHit.Text    = s.MaxSingleHit > 0 ? $"Max hit: {FormatNum(s.MaxSingleHit)}" : "";
        PbDetailBadge.Visibility = s.IsPersonalBest ? Visibility.Visible : Visibility.Collapsed;

        // Hide empty badges
        var detDurationParent = DetailDuration.Parent as Border;
        if (detDurationParent != null)
            detDurationParent.Visibility = s.DurationSeconds > 0 ? Visibility.Visible : Visibility.Collapsed;
        var detMaxHitParent = DetailMaxHit.Parent as Border;
        if (detMaxHitParent != null)
            detMaxHitParent.Visibility = s.MaxSingleHit > 0 ? Visibility.Visible : Visibility.Collapsed;

        // Sparkline
        if (s.DpsTimeline.Count >= 2)
        {
            SparkBorder.Visibility = Visibility.Visible;
            RenderSparklineFromData(s.DpsTimeline);
        }
        else
        {
            SparkBorder.Visibility = Visibility.Collapsed;
            _pendingSparkline = null;
            SparkCanvas.Children.Clear();
        }

        // Leaderboard
        double maxPct = 0;
        foreach (var r in s.Leaderboard) if (r.Percent > maxPct) maxPct = r.Percent;
        if (maxPct <= 0) maxPct = 100;

        var selfBarBrush = new SolidColorBrush(Color.FromArgb(0x99, 0xD6, 0x2A, 0x2A));
        var peerBarBrush = new SolidColorBrush(Color.FromArgb(0x88, 0x3D, 0x8F, 0xD9));
        var selfFg       = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xB3, 0x47));
        var peerFg       = new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF));

        var lbRows = new List<LeaderboardRow>();
        int selfLbIndex = -1;
        for (int i = 0; i < s.Leaderboard.Count; i++)
        {
            var r = s.Leaderboard[i];
            string name = !string.IsNullOrEmpty(r.Name) && !string.IsNullOrEmpty(r.PlayerName)
                ? $"{r.Name} ({r.PlayerName})"
                : !string.IsNullOrEmpty(r.Name) ? r.Name
                : !string.IsNullOrEmpty(r.PlayerName) ? r.PlayerName
                : r.IsSelf ? "you" : "?";

            // Fallback DPS for older saves that only stored a non-zero Dps for the self row:
            // derive it from total ÷ fight duration so every leaderboard row shows a number.
            double effectiveDps = r.Dps > 0.1
                ? r.Dps
                : (s.DurationSeconds > 0 && r.Total > 0 ? (double)r.Total / s.DurationSeconds : 0.0);

            lbRows.Add(new LeaderboardRow
            {
                DisplayName    = name,
                DpsText        = effectiveDps > 0.1 ? FormatNum(effectiveDps) : "",
                TotalText      = r.Total > 0    ? FormatNum(r.Total) : "",
                PctText        = $"{r.Percent:0}%",
                BarWidth       = DetailTrackWidthPx * (r.Percent / maxPct),
                BarFill        = r.IsSelf ? selfBarBrush : peerBarBrush,
                TextColor      = r.IsSelf ? selfFg : peerFg,
                LeaderboardIdx = i,
            });
            if (r.IsSelf && selfLbIndex < 0) selfLbIndex = i;
        }
        LeaderboardItems.SelectionChanged -= LeaderboardItems_SelectionChanged;
        LeaderboardItems.ItemsSource = lbRows;
        LeaderboardItems.SelectionChanged += LeaderboardItems_SelectionChanged;

        // Default selection: self row, or first row if no self entry.
        _selectedHeroIndex = selfLbIndex >= 0 ? selfLbIndex : (s.Leaderboard.Count > 0 ? 0 : -1);
        if (_selectedHeroIndex >= 0 && _selectedHeroIndex < lbRows.Count)
            LeaderboardItems.SelectedIndex = _selectedHeroIndex;

        // Ability breakdown — show the selected leaderboard player's abilities.
        ShowPowerBreakdownForHero(s, _selectedHeroIndex);
    }

    private void LeaderboardItems_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_selected == null) return;
        if (LeaderboardItems.SelectedItem is LeaderboardRow row)
        {
            _selectedHeroIndex = row.LeaderboardIdx;
            ShowPowerBreakdownForHero(_selected, _selectedHeroIndex);
        }
    }

    private void ShowPowerBreakdownForHero(DpsSnapshot s, int heroIndex)
    {
        // Find which hero's breakdown to show; fall back to the top-level snapshot breakdown
        // (pre-per-player-tracking saves) when no per-hero data is available.
        List<DpsSnapshot.PowerEntry>? powers = null;
        string playerLabel = "MY";

        if (heroIndex >= 0 && heroIndex < s.Leaderboard.Count)
        {
            var hero = s.Leaderboard[heroIndex];
            if (hero.PowerBreakdown.Count > 0)
            {
                powers = hero.PowerBreakdown;
                string displayName = !string.IsNullOrEmpty(hero.Name) ? hero.Name
                    : !string.IsNullOrEmpty(hero.PlayerName) ? hero.PlayerName
                    : hero.IsSelf ? "MY" : "?";
                playerLabel = hero.IsSelf ? "MY" : displayName.ToUpperInvariant();
            }
        }

        // Fallback for old saves that stored breakdown at snapshot level.
        if (powers == null && s.PowerBreakdown.Count > 0)
            powers = s.PowerBreakdown;

        if (powers != null && powers.Count > 0)
        {
            PowerSectionHeader.Visibility = Visibility.Visible;
            PowerSectionTitle.Text = $"{playerLabel} ABILITIES";

            double pctSum = powers.Sum(p => p.Percent);
            if (pctSum <= 0) pctSum = 100;

            var pwrRows = new List<PowerRow>();
            for (int i = 0; i < powers.Count; i++)
            {
                var p = powers[i];
                var c = s_pwrColors[i % s_pwrColors.Length];
                pwrRows.Add(new PowerRow
                {
                    Name       = p.Name,
                    HitsText   = p.Hits + "x",
                    AvgHitText = p.Hits > 0 ? FormatNum((double)p.TotalDamage / p.Hits) : "",
                    MaxHitText = p.MaxHit > 0 ? FormatNum(p.MaxHit) : "",
                    TotalText  = FormatNum(p.TotalDamage),
                    PctText    = $"{p.Percent:0}%",
                    BarWidth   = DetailTrackWidthPx * (p.Percent / pctSum),
                    BarFill    = new SolidColorBrush(Color.FromArgb(0x44, c.R, c.G, c.B)),
                    TextColor  = new SolidColorBrush(Color.FromArgb(0xCC, c.R, c.G, c.B)),
                });
            }
            PowerItems.ItemsSource = pwrRows;

            int   totalHits  = powers.Sum(p => p.Hits);
            long  totalDmg   = powers.Sum(p => p.TotalDamage);
            long  maxHitAll  = powers.Max(p => p.MaxHit);
            long  avgAll     = totalHits > 0 ? totalDmg / totalHits : 0;
            TotalsHits.Text  = totalHits + "x";
            TotalsAvg.Text   = avgAll > 0 ? FormatNum(avgAll) : "";
            TotalsMax.Text   = maxHitAll > 0 ? FormatNum(maxHitAll) : "";
            TotalsTotal.Text = FormatNum(totalDmg);
            PowerTotalsRow.Visibility = Visibility.Visible;
        }
        else
        {
            PowerSectionHeader.Visibility = Visibility.Collapsed;
            PowerTotalsRow.Visibility     = Visibility.Collapsed;
            PowerItems.ItemsSource = null;
        }
    }

    // ── Sparkline ────────────────────────────────────────────────────────────────────────────

    private void RenderSparklineFromData(List<DpsSnapshot.SparkPoint> points)
    {
        _pendingSparkline = points;
        SparkCanvas.Children.Clear();
        if (points.Count == 0) return;

        double canvasW = SparkCanvas.ActualWidth;
        double canvasH = SparkCanvas.ActualHeight;
        if (canvasW <= 0 || canvasH <= 0) return; // will re-fire from SizeChanged

        double maxDps = points.Max(p => p.Dps);
        if (maxDps <= 0) return;

        int n = points.Count;
        double step = canvasW / Math.Max(n - 1, 1);

        var linePoints   = new PointCollection(n);
        var filledPoints = new PointCollection(n + 2);
        filledPoints.Add(new Point(0, canvasH));

        for (int i = 0; i < n; i++)
        {
            double x = i == n - 1 ? canvasW : i * step;
            double y = canvasH - (points[i].Dps / maxDps) * (canvasH - 2);
            y = Math.Max(0, Math.Min(canvasH, y));
            linePoints.Add(new Point(x, y));
            filledPoints.Add(new Point(x, y));
        }
        filledPoints.Add(new Point(canvasW, canvasH));

        // Filled area
        SparkCanvas.Children.Add(new Polygon
        {
            Points          = filledPoints,
            Fill            = new SolidColorBrush(Color.FromArgb(0x28, 0xFF, 0x6B, 0x00)),
            StrokeThickness = 0,
        });

        // Line
        SparkCanvas.Children.Add(new Polyline
        {
            Points          = linePoints,
            Stroke          = new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0x6B, 0x00)),
            StrokeThickness = 1.5,
            StrokeLineJoin  = PenLineJoin.Round,
        });

        // Dots at each sample
        foreach (var pt in linePoints)
        {
            var dot = new Ellipse
            {
                Width  = 3,
                Height = 3,
                Fill   = new SolidColorBrush(Color.FromArgb(0xDD, 0xFF, 0x6B, 0x00)),
            };
            Canvas.SetLeft(dot, pt.X - 1.5);
            Canvas.SetTop(dot, pt.Y - 1.5);
            SparkCanvas.Children.Add(dot);
        }
    }

    private void SparkCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_pendingSparkline != null && _pendingSparkline.Count >= 2)
            RenderSparklineFromData(_pendingSparkline);
    }

    // ── Rename (inline TextBox) ───────────────────────────────────────────────────────────────

    private void DetailLabel_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitRename();
            Keyboard.ClearFocus();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            if (_selected != null) DetailLabel.Text = _selected.Label;
            Keyboard.ClearFocus();
            e.Handled = true;
        }
    }

    private void DetailLabel_LostFocus(object sender, RoutedEventArgs e) => CommitRename();

    private void CommitRename()
    {
        if (_selected == null) return;
        string newLabel = DetailLabel.Text.Trim();
        if (string.IsNullOrEmpty(newLabel) || newLabel == _selected.Label) return;
        DpsReportStore.UpdateLabel(_selected.Id, newLabel);
        _selected.Label = newLabel;
        // Refresh the matching list item label without a full reload.
        var item = _items.FirstOrDefault(i => i.Snapshot.Id == _selected.Id);
        item?.UpdateLabel(newLabel);
        SnapshotList.Items.Refresh();
    }

    // ── Copy to clipboard ────────────────────────────────────────────────────────────────────

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        try { Clipboard.SetText(BuildCopyText(_selected)); }
        catch { /* clipboard may be locked */ }
    }

    private static string BuildCopyText(DpsSnapshot s)
    {
        var sb = new StringBuilder();
        sb.AppendLine(s.Label);

        var line2 = s.SavedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        if (s.DurationSeconds > 0) line2 += $"  ·  {FormatDuration(s.DurationSeconds)}";
        if (!string.IsNullOrEmpty(s.Mode)) line2 += $"  ·  {s.Mode}";
        if (!string.IsNullOrEmpty(s.BossName)) line2 += $"  ·  vs {s.BossName}";
        if (s.IsPersonalBest) line2 += "  ·  ★ Personal Best";
        sb.AppendLine(line2);
        sb.AppendLine();

        var stats = $"DPS: {FormatNum(s.Dps)}";
        if (s.MaxSingleHit > 0) stats += $"   Max Hit: {FormatNum(s.MaxSingleHit)}";
        if (s.TotalDamage > 0)  stats += $"   Total: {FormatNum(s.TotalDamage)}";
        sb.AppendLine(stats);

        if (s.Leaderboard.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("LEADERBOARD");
            foreach (var r in s.Leaderboard)
            {
                string name = !string.IsNullOrEmpty(r.Name) && !string.IsNullOrEmpty(r.PlayerName)
                    ? $"{r.Name} ({r.PlayerName})"
                    : !string.IsNullOrEmpty(r.Name) ? r.Name
                    : !string.IsNullOrEmpty(r.PlayerName) ? r.PlayerName
                    : r.IsSelf ? "you" : "?";
                sb.AppendLine($"  {name,-30} {FormatNum(r.Dps),8}  {FormatNum(r.Total),8}  {r.Percent:0}%");
            }
        }

        if (s.PowerBreakdown.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("MY ABILITIES");
            foreach (var p in s.PowerBreakdown)
            {
                string avg = p.Hits > 0 ? FormatNum((double)p.TotalDamage / p.Hits) : "-";
                string max = p.MaxHit > 0 ? FormatNum(p.MaxHit) : "-";
                sb.AppendLine($"  {p.Name,-28}  {p.Hits,3}x  avg {avg,8}  max {max,8}  {FormatNum(p.TotalDamage),8}  {p.Percent:0}%");
            }
        }

        return sb.ToString().TrimEnd();
    }

    // ── Delete ───────────────────────────────────────────────────────────────────────────────

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        var result = MessageBox.Show(
            $"Delete snapshot \"{_selected.Label}\"?",
            "Delete snapshot", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;
        DpsReportStore.Delete(_selected.Id);
        _selected = null;
        RefreshList();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    // ── Helpers ──────────────────────────────────────────────────────────────────────────────

    private static string FormatNum(double v)
    {
        if (v >= 1_000_000) return (v / 1_000_000.0).ToString("0.00") + "M";
        if (v >= 100_000)   return (v / 1_000.0).ToString("0") + "k";
        if (v >= 10_000)    return (v / 1_000.0).ToString("0.0") + "k";
        return ((long)v).ToString("N0");
    }

    private static string FormatDuration(int secs)
    {
        if (secs <= 0) return "";
        int m = secs / 60, s = secs % 60;
        return m > 0 ? $"{m}m {s:00}s" : $"{s}s";
    }

    // ── View-model types ─────────────────────────────────────────────────────────────────────

    private sealed class SnapshotListItem(DpsSnapshot s)
    {
        public DpsSnapshot Snapshot            { get; } = s;
        public string      Label               { get; private set; } = s.Label;
        public string      SavedLocal          { get; } = s.SavedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        public Visibility  AutoBadgeVisibility { get; } = s.IsAutoSave    ? Visibility.Visible : Visibility.Collapsed;
        public Visibility  PbBadgeVisibility   { get; } = s.IsPersonalBest ? Visibility.Visible : Visibility.Collapsed;
        public string      DurationText        { get; } = s.DurationSeconds > 0 ? FormatDuration(s.DurationSeconds) : "";

        public void UpdateLabel(string newLabel) => Label = newLabel;

        private static string FormatDuration(int secs)
        {
            int m = secs / 60, sc = secs % 60;
            return m > 0 ? $"{m}m {sc:00}s" : $"{secs}s";
        }
    }

    private sealed class LeaderboardRow
    {
        public string DisplayName    { get; init; } = "";
        public string DpsText        { get; init; } = "";
        public string TotalText      { get; init; } = "";
        public string PctText        { get; init; } = "";
        public double BarWidth       { get; init; }
        public Brush  BarFill        { get; init; } = Brushes.Transparent;
        public Brush  TextColor      { get; init; } = Brushes.White;
        public int    LeaderboardIdx { get; init; }
    }

    private sealed class PowerRow
    {
        public string Name       { get; init; } = "";
        public string HitsText   { get; init; } = "";
        public string AvgHitText { get; init; } = "";
        public string MaxHitText { get; init; } = "";
        public string TotalText  { get; init; } = "";
        public string PctText    { get; init; } = "";
        public double BarWidth   { get; init; }
        public Brush  BarFill    { get; init; } = Brushes.Transparent;
        public Brush  TextColor  { get; init; } = Brushes.White;
    }
}
