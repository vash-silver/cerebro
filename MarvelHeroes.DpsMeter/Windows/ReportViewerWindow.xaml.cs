using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using MarvelHeroes.DpsMeter.Models;

namespace MarvelHeroes.DpsMeter.Windows;

public partial class ReportViewerWindow : Window
{
    private const double DetailTrackWidthPx = 360.0;

    private static readonly Color[] s_pwrColors =
    {
        Color.FromRgb(0xFF, 0x69, 0x00),
        Color.FromRgb(0x3D, 0x8F, 0xD9),
        Color.FromRgb(0x4C, 0xAF, 0x50),
        Color.FromRgb(0xAB, 0x47, 0xBC),
        Color.FromRgb(0x00, 0xBC, 0xD4),
        Color.FromRgb(0xFF, 0xC1, 0x07),
        Color.FromRgb(0xE9, 0x1E, 0x63),
        Color.FromRgb(0x00, 0x96, 0x88),
    };

    private List<SnapshotListItem> _items = new();
    private DpsSnapshot? _selected;

    public ReportViewerWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshList();
    }

    private void RefreshList()
    {
        var snaps = DpsReportStore.LoadAll();
        _items.Clear();
        foreach (var s in snaps)
            _items.Add(new SnapshotListItem(s));
        SnapshotList.ItemsSource = null;
        SnapshotList.ItemsSource = _items;
        if (_items.Count > 0)
            SnapshotList.SelectedIndex = 0;
        else
            ShowEmpty();
    }

    private void SnapshotList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (SnapshotList.SelectedItem is SnapshotListItem item)
        {
            _selected = item.Snapshot;
            DeleteButton.IsEnabled = true;
            ShowDetail(_selected);
        }
        else
        {
            _selected = null;
            DeleteButton.IsEnabled = false;
            ShowEmpty();
        }
    }

    private void ShowEmpty()
    {
        DetailPanel.Visibility = Visibility.Collapsed;
    }

    private void ShowDetail(DpsSnapshot s)
    {
        DetailPanel.Visibility = Visibility.Visible;

        DetailLabel.Text = s.Label;
        DetailDate.Text  = s.SavedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        DetailMode.Text  = s.Mode;
        DetailHero.Text  = string.IsNullOrEmpty(s.HeroName) ? "unknown hero" : s.HeroName;
        DetailDps.Text   = $"DPS: {FormatNum(s.Dps)}";
        DetailMaxHit.Text = s.MaxSingleHit > 0 ? $"Max hit: {FormatNum(s.MaxSingleHit)}" : "";

        // Leaderboard rows
        double maxPct = 0;
        foreach (var r in s.Leaderboard) if (r.Percent > maxPct) maxPct = r.Percent;
        if (maxPct <= 0) maxPct = 100;

        var selfBarBrush = new SolidColorBrush(Color.FromArgb(0x99, 0xD6, 0x2A, 0x2A));
        var peerBarBrush = new SolidColorBrush(Color.FromArgb(0x88, 0x3D, 0x8F, 0xD9));
        var selfFg       = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xB3, 0x47));
        var peerFg       = new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF));

        var lbRows = new List<LeaderboardRow>();
        foreach (var r in s.Leaderboard)
        {
            string name = !string.IsNullOrEmpty(r.Name) && !string.IsNullOrEmpty(r.PlayerName)
                ? $"{r.Name} ({r.PlayerName})"
                : !string.IsNullOrEmpty(r.Name) ? r.Name
                : !string.IsNullOrEmpty(r.PlayerName) ? r.PlayerName
                : r.IsSelf ? "you" : "?";
            lbRows.Add(new LeaderboardRow
            {
                DisplayName = name,
                DpsText     = r.Dps   > 0.1 ? FormatNum(r.Dps)   : "",
                TotalText   = r.Total > 0    ? FormatNum(r.Total) : "",
                PctText     = $"{r.Percent:0}%",
                BarWidth    = DetailTrackWidthPx * (r.Percent / maxPct),
                BarFill     = r.IsSelf ? selfBarBrush : peerBarBrush,
                TextColor   = r.IsSelf ? selfFg : peerFg,
            });
        }
        LeaderboardItems.ItemsSource = lbRows;

        // Power breakdown
        if (s.PowerBreakdown.Count > 0)
        {
            PowerSectionHeader.Visibility = Visibility.Visible;
            double pctSum = 0;
            foreach (var p in s.PowerBreakdown) pctSum += p.Percent;
            if (pctSum <= 0) pctSum = 100;

            var pwrRows = new List<PowerRow>();
            for (int i = 0; i < s.PowerBreakdown.Count; i++)
            {
                var p = s.PowerBreakdown[i];
                var c = s_pwrColors[i % s_pwrColors.Length];
                pwrRows.Add(new PowerRow
                {
                    Name      = p.Name,
                    HitsText  = p.Hits + "x",
                    TotalText = FormatNum(p.TotalDamage),
                    PctText   = $"{p.Percent:0}%",
                    BarWidth  = DetailTrackWidthPx * (p.Percent / pctSum),
                    BarFill   = new SolidColorBrush(Color.FromArgb(0x44, c.R, c.G, c.B)),
                    TextColor = new SolidColorBrush(Color.FromArgb(0xCC, c.R, c.G, c.B)),
                });
            }
            PowerItems.ItemsSource = pwrRows;
        }
        else
        {
            PowerSectionHeader.Visibility = Visibility.Collapsed;
            PowerItems.ItemsSource = null;
        }
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        var result = MessageBox.Show(
            $"Delete snapshot \"{_selected.Label}\"?",
            "Delete snapshot", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;
        DpsReportStore.Delete(_selected.Id);
        RefreshList();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private static string FormatNum(double v)
    {
        if (v >= 1_000_000) return (v / 1_000_000.0).ToString("0.00") + "M";
        if (v >= 100_000)   return (v / 1_000.0).ToString("0") + "k";
        if (v >= 10_000)    return (v / 1_000.0).ToString("0.0") + "k";
        return ((long)v).ToString("N0");
    }

    // ── View-model types ─────────────────────────────────────────────────────────────────────

    private sealed class SnapshotListItem(DpsSnapshot s)
    {
        public DpsSnapshot Snapshot           { get; } = s;
        public string      Label              { get; } = s.Label;
        public string      SavedLocal         { get; } = s.SavedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        public Visibility  AutoBadgeVisibility{ get; } = s.IsAutoSave ? Visibility.Visible : Visibility.Collapsed;
    }

    private sealed class LeaderboardRow
    {
        public string DisplayName { get; init; } = "";
        public string DpsText     { get; init; } = "";
        public string TotalText   { get; init; } = "";
        public string PctText     { get; init; } = "";
        public double BarWidth    { get; init; }
        public Brush  BarFill     { get; init; } = Brushes.Transparent;
        public Brush  TextColor   { get; init; } = Brushes.White;
    }

    private sealed class PowerRow
    {
        public string Name      { get; init; } = "";
        public string HitsText  { get; init; } = "";
        public string TotalText { get; init; } = "";
        public string PctText   { get; init; } = "";
        public double BarWidth  { get; init; }
        public Brush  BarFill   { get; init; } = Brushes.Transparent;
        public Brush  TextColor { get; init; } = Brushes.White;
    }
}
