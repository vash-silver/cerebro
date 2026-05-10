using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MarvelHeroesComporator.NetworkSniffer;
using MarvelHeroes.DpsMeter.Services;
using Rectangle = System.Windows.Shapes.Rectangle;
using DpsMeterClass = MarvelHeroes.DpsMeter.Services.DpsMeter;

namespace MarvelHeroes.DpsMeter.Controls;

public partial class DpsDisplayPanel : UserControl
{
    public DpsDisplayPanel()
    {
        InitializeComponent();
    }

    private DpsOverlaySettingsFile _settings = new();

    public bool InitialBossOnlyPreference { get; private set; }

    // ── Events raised to the host window ─────────────────────────────────────────────────────
    public event Action? DragStarted;
    public event Action? CloseRequested;
    public event Action? SwitchModeRequested;
    public event Action<bool>? BossOnlyToggled;
    public event Action<IReadOnlyList<DpsMeterClass.HeroShareEntry>?,
                        DpsMeterClass.EncounterSnapshot,
                        IReadOnlyList<DpsMeterClass.PowerBreakdownEntry>?>? SaveSnapshotRequested;
    public event Action? ClearDpsRequested;
    public event Action? ViewReportsRequested;

    public void Initialize(DpsOverlaySettingsFile settings, bool isOverlayMode)
    {
        _settings = settings;

        InitialBossOnlyPreference = settings.BossDpsOnly;
        _bossOnlyMode = settings.BossDpsOnly;
        _suppressBossOnlyMenuEvents = true;
        try { BossOnlyMenuItem.IsChecked = settings.BossDpsOnly; }
        finally { _suppressBossOnlyMenuEvents = false; }

        _showBossSection = settings.ShowBossSection;
        _suppressShowBossSectionMenuEvents = true;
        try { ShowBossSectionMenuItem.IsChecked = settings.ShowBossSection; }
        finally { _suppressShowBossSectionMenuEvents = false; }
        BossFightPanel.Visibility = settings.ShowBossSection ? Visibility.Visible : Visibility.Collapsed;

        _showPowerBreakdown = settings.ShowPowerBreakdown;
        _suppressShowPowerBreakdownMenuEvents = true;
        try { ShowPowerBreakdownMenuItem.IsChecked = settings.ShowPowerBreakdown; }
        finally { _suppressShowPowerBreakdownMenuEvents = false; }
        PowerBreakdownPanel.Visibility = settings.ShowPowerBreakdown ? Visibility.Visible : Visibility.Collapsed;

        ApplyScale(settings.Scale, save: false);

        SetDisplayMode(isOverlayMode);
    }

    public void SetDisplayMode(bool isOverlayMode)
    {
        CloseButton.Visibility = isOverlayMode ? Visibility.Visible : Visibility.Collapsed;
        ContentBorder.Cursor   = isOverlayMode ? Cursors.SizeAll : Cursors.Arrow;
        SwitchModeMenuItem.Header = isOverlayMode ? "Switch to window mode" : "Switch to overlay mode";
    }

    // ── Main update entry point ───────────────────────────────────────────────────────────────

    // Last data delivered — forwarded to host on Save Snapshot.
    private IReadOnlyList<DpsMeterClass.HeroShareEntry>?       _lastTopHeroes;
    private DpsMeterClass.EncounterSnapshot                     _lastEncounter;
    private IReadOnlyList<DpsMeterClass.PowerBreakdownEntry>?  _lastPowerBreakdown;

    public void UpdateDps(
        double dps,
        long totalDamage60s,
        long totalDamageSession,
        ulong ownerEntityId,
        uint maxSingleHit,
        string heroDisplayName,
        bool bossOnlyMode,
        IReadOnlyList<DpsMeterClass.HeroShareEntry>? topHeroes,
        DpsMeterClass.EncounterSnapshot encounter,
        double bossDps = 0.0,
        long bossTotalDamage60s = 0,
        IReadOnlyList<DpsMeterClass.HeroShareEntry>? bossTopHeroes = null,
        DpsMeterClass.EncounterSnapshot bossEncounter = default,
        IReadOnlyList<DpsMeterClass.PowerBreakdownEntry>? powerBreakdown = null)
    {
        _bossOnlyMode = bossOnlyMode;
        if (BossOnlyMenuItem.IsChecked != bossOnlyMode)
        {
            _suppressBossOnlyMenuEvents = true;
            try { BossOnlyMenuItem.IsChecked = bossOnlyMode; }
            finally { _suppressBossOnlyMenuEvents = false; }
        }

        string titlePrefix = bossOnlyMode ? "BOSS DPS" : "DPS";
        string titleSuffix = heroDisplayName ?? string.Empty;
        if (string.IsNullOrEmpty(titleSuffix) && topHeroes != null)
        {
            for (int i = 0; i < topHeroes.Count; i++)
            {
                var r = topHeroes[i];
                if (r.IsSelf && !string.IsNullOrEmpty(r.PlayerName)) { titleSuffix = r.PlayerName; break; }
            }
        }
        HeroTitleText.Text = string.IsNullOrEmpty(titleSuffix) ? titlePrefix : $"{titlePrefix} - {titleSuffix}";

        bool liveActive = dps > 0.1;
        double displayDps = liveActive ? dps : (totalDamage60s > 0 ? totalDamage60s / 60.0 : 0.0);
        DpsText.Text = displayDps <= 0.1 ? "—" : FormatDps(displayDps);
        MaxHitText.Text = maxSingleHit == 0 ? "" : $"Max hit: {FormatTotal(maxSingleHit)}";

        string modeTag;
        if (ownerEntityId == 0)
            modeTag = "locating you…";
        else if (bossOnlyMode)
        {
            if (encounter.IsEnded)
                modeTag = $"fight ended · Fight: {FormatTotal(encounter.SelfTotal)}";
            else if (encounter.IsActive)
                modeTag = liveActive
                    ? $"live · Fight: {FormatTotal(encounter.SelfTotal)}"
                    : $"60s avg · Fight: {FormatTotal(encounter.SelfTotal)}";
            else if (liveActive)
                modeTag = $"live · 60s: {FormatTotal(totalDamage60s)}";
            else if (totalDamage60s > 0)
                modeTag = $"60s avg · 60s: {FormatTotal(totalDamage60s)}";
            else
                modeTag = "waiting for boss…";
        }
        else if (liveActive)
            modeTag = $"live · Total: {FormatTotal(totalDamageSession)}";
        else if (totalDamageSession > 0)
            modeTag = totalDamage60s > 0
                ? $"60s avg · Total: {FormatTotal(totalDamageSession)}"
                : $"idle · Total: {FormatTotal(totalDamageSession)}";
        else
            modeTag = "idle · waiting for damage";
        DetailText.Text = modeTag;

        _lastTopHeroes      = topHeroes;
        _lastEncounter      = encounter;
        _lastPowerBreakdown = powerBreakdown;

        RenderTopHeroes(topHeroes);
        if (_showBossSection)
            RenderBossSection(bossDps, bossTotalDamage60s, bossTopHeroes, bossEncounter);
        if (_showPowerBreakdown)
            RenderPowerBreakdown(powerBreakdown);
    }

    // ── Row arrays ───────────────────────────────────────────────────────────────────────────
    private TextBlock[]? _topHeroRows, _topHeroDpsCells, _topHeroTotals, _topHeroPct;
    private Image[]?     _topHeroImages;
    private Rectangle[]? _topHeroBars;
    private TextBlock[]? _bossRows, _bossDpsCells, _bossTotals, _bossPct;
    private Image[]?     _bossImages;
    private Rectangle[]? _bossBars;

    private const double BarTrackWidthPx = 280.0;

    private void RenderTopHeroes(IReadOnlyList<DpsMeterClass.HeroShareEntry>? rows)
    {
        _topHeroRows     ??= new[] { Top1Text,  Top2Text,  Top3Text,  Top4Text,  Top5Text  };
        _topHeroDpsCells ??= new[] { Top1Dps,   Top2Dps,   Top3Dps,   Top4Dps,   Top5Dps   };
        _topHeroTotals   ??= new[] { Top1Total, Top2Total, Top3Total, Top4Total, Top5Total };
        _topHeroPct      ??= new[] { Top1Pct,   Top2Pct,   Top3Pct,   Top4Pct,   Top5Pct   };
        _topHeroImages   ??= new[] { Top1Image, Top2Image, Top3Image, Top4Image, Top5Image };
        _topHeroBars     ??= new[] { Top1Bar,   Top2Bar,   Top3Bar,   Top4Bar,   Top5Bar   };
        RenderRows(rows, _topHeroRows, _topHeroDpsCells, _topHeroTotals, _topHeroPct, _topHeroImages, _topHeroBars);
    }

    private void RenderBossSection(double bossDps, long bossTotalDamage60s,
        IReadOnlyList<DpsMeterClass.HeroShareEntry>? rows, DpsMeterClass.EncounterSnapshot enc)
    {
        bool liveActive = bossDps > 0.1;
        double displayDps = liveActive ? bossDps : (bossTotalDamage60s > 0 ? bossTotalDamage60s / 60.0 : 0.0);
        BossDpsText.Text = displayDps <= 0.1 ? "—" : FormatDps(displayDps);
        BossDetailText.Text = enc.IsEnded
            ? $"fight ended · Fight: {FormatTotal(enc.SelfTotal)}"
            : enc.IsActive
                ? (liveActive ? $"live · Fight: {FormatTotal(enc.SelfTotal)}"
                               : $"60s avg · Fight: {FormatTotal(enc.SelfTotal)}")
                : "waiting for boss…";

        _bossRows     ??= new[] { Boss1Text,  Boss2Text,  Boss3Text,  Boss4Text,  Boss5Text  };
        _bossDpsCells ??= new[] { Boss1Dps,   Boss2Dps,   Boss3Dps,   Boss4Dps,   Boss5Dps   };
        _bossTotals   ??= new[] { Boss1Total, Boss2Total, Boss3Total, Boss4Total, Boss5Total };
        _bossPct      ??= new[] { Boss1Pct,   Boss2Pct,   Boss3Pct,   Boss4Pct,   Boss5Pct   };
        _bossImages   ??= new[] { Boss1Image, Boss2Image, Boss3Image, Boss4Image, Boss5Image };
        _bossBars     ??= new[] { Boss1Bar,   Boss2Bar,   Boss3Bar,   Boss4Bar,   Boss5Bar   };
        RenderRows(rows, _bossRows, _bossDpsCells, _bossTotals, _bossPct, _bossImages, _bossBars);
    }

    // ── Power breakdown ───────────────────────────────────────────────────────────────────────
    private TextBlock[]? _pwrNames, _pwrHits, _pwrTotals, _pwrPcts;
    private Rectangle[]? _pwrSegs, _pwrBars;
    private SolidColorBrush[]? _pwrBrushes, _pwrBarBrushes;

    private const double PwrBarTrackWidthPx = 280.0;
    private const double PwrSegGapPx        = 2.0;

    private static readonly Color[] s_pwrColors =
    {
        Color.FromRgb(0xFF, 0x69, 0x00), Color.FromRgb(0x3D, 0x8F, 0xD9),
        Color.FromRgb(0x4C, 0xAF, 0x50), Color.FromRgb(0xAB, 0x47, 0xBC),
        Color.FromRgb(0x00, 0xBC, 0xD4), Color.FromRgb(0xFF, 0xC1, 0x07),
        Color.FromRgb(0xE9, 0x1E, 0x63), Color.FromRgb(0x00, 0x96, 0x88),
    };

    private void RenderPowerBreakdown(IReadOnlyList<DpsMeterClass.PowerBreakdownEntry>? rows)
    {
        _pwrNames  ??= new[] { Pwr1Name,  Pwr2Name,  Pwr3Name,  Pwr4Name,  Pwr5Name,  Pwr6Name,  Pwr7Name,  Pwr8Name  };
        _pwrHits   ??= new[] { Pwr1Hits,  Pwr2Hits,  Pwr3Hits,  Pwr4Hits,  Pwr5Hits,  Pwr6Hits,  Pwr7Hits,  Pwr8Hits  };
        _pwrTotals ??= new[] { Pwr1Total, Pwr2Total, Pwr3Total, Pwr4Total, Pwr5Total, Pwr6Total, Pwr7Total, Pwr8Total };
        _pwrPcts   ??= new[] { Pwr1Pct,   Pwr2Pct,   Pwr3Pct,   Pwr4Pct,   Pwr5Pct,   Pwr6Pct,   Pwr7Pct,   Pwr8Pct   };
        _pwrSegs   ??= new[] { PwrSeg1,   PwrSeg2,   PwrSeg3,   PwrSeg4,   PwrSeg5,   PwrSeg6,   PwrSeg7,   PwrSeg8   };
        _pwrBars   ??= new[] { Pwr1Bar,   Pwr2Bar,   Pwr3Bar,   Pwr4Bar,   Pwr5Bar,   Pwr6Bar,   Pwr7Bar,   Pwr8Bar   };

        if (_pwrBrushes == null)
        {
            _pwrBrushes    = new SolidColorBrush[s_pwrColors.Length];
            _pwrBarBrushes = new SolidColorBrush[s_pwrColors.Length];
            for (int k = 0; k < s_pwrColors.Length; k++)
            {
                var c = s_pwrColors[k];
                _pwrBrushes[k]    = FreezeBrush(new SolidColorBrush(Color.FromArgb(0xCC, c.R, c.G, c.B)));
                _pwrBarBrushes[k] = FreezeBrush(new SolidColorBrush(Color.FromArgb(0x44, c.R, c.G, c.B)));
            }
        }

        double pctSum = 0.0;
        if (rows != null) foreach (var r in rows) pctSum += r.Percent;
        if (pctSum <= 0) pctSum = 100.0;

        double segLeft = 0.0;
        for (int i = 0; i < _pwrSegs.Length; i++)
        {
            if (rows != null && i < rows.Count)
            {
                double fraction = rows[i].Percent / pctSum;
                double segWidth = Math.Max(0, fraction * PwrBarTrackWidthPx - PwrSegGapPx);
                _pwrSegs[i].Fill = _pwrBrushes[i];
                _pwrSegs[i].Width = segWidth;
                Canvas.SetLeft(_pwrSegs[i], segLeft);
                _pwrSegs[i].Visibility = segWidth > 0.5 ? Visibility.Visible : Visibility.Collapsed;
                segLeft += fraction * PwrBarTrackWidthPx;
            }
            else
            {
                _pwrSegs[i].Width = 0; _pwrSegs[i].Visibility = Visibility.Collapsed;
            }
        }

        var dimBrush = _peerBrush ??= FreezeBrush(new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF)));
        for (int i = 0; i < _pwrNames.Length; i++)
        {
            if (rows != null && i < rows.Count)
            {
                var r = rows[i]; var brush = _pwrBrushes[i]; bool top = i == 0;
                _pwrNames[i].Text = r.Name; _pwrHits[i].Text = r.Hits.ToString("N0") + "x";
                _pwrTotals[i].Text = FormatRowTotalCompact(r.TotalDamage); _pwrPcts[i].Text = $"{r.Percent:0}%";
                _pwrNames[i].Foreground = brush; _pwrNames[i].FontWeight = top ? FontWeights.SemiBold : FontWeights.Normal;
                _pwrPcts[i].Foreground  = brush; _pwrPcts[i].FontWeight  = top ? FontWeights.SemiBold : FontWeights.Normal;
                double barW = (r.Percent / pctSum) * PwrBarTrackWidthPx;
                _pwrBars![i].Fill = _pwrBarBrushes![i]; _pwrBars[i].Width = barW;
                _pwrBars[i].Visibility = barW > 0.5 ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                _pwrNames[i].Text = ""; _pwrHits[i].Text = ""; _pwrTotals[i].Text = ""; _pwrPcts[i].Text = "";
                _pwrNames[i].Foreground = dimBrush; _pwrPcts[i].Foreground = dimBrush;
                _pwrBars![i].Width = 0; _pwrBars[i].Visibility = Visibility.Collapsed;
            }
        }
    }

    // ── Shared row renderer ───────────────────────────────────────────────────────────────────
    private SolidColorBrush? _selfBrush, _peerBrush, _selfBarBrush, _peerBarBrush;

    private void RenderRows(IReadOnlyList<DpsMeterClass.HeroShareEntry>? rows,
        TextBlock[] nameTexts, TextBlock[] dpsCells, TextBlock[] totalTexts, TextBlock[] pctTexts,
        Image[] images, Rectangle[] bars)
    {
        double maxPercent = 0.0;
        if (rows != null) foreach (var r in rows) if (r.Percent > maxPercent) maxPercent = r.Percent;
        if (maxPercent <= 0.01) maxPercent = 1.0;

        var selfBrush    = _selfBrush    ??= FreezeBrush(new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xB3, 0x47)));
        var peerBrush    = _peerBrush    ??= FreezeBrush(new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF)));
        var selfBarBrush = _selfBarBrush ??= FreezeBrush(new SolidColorBrush(Color.FromArgb(0x99, 0xD6, 0x2A, 0x2A)));
        var peerBarBrush = _peerBarBrush ??= FreezeBrush(new SolidColorBrush(Color.FromArgb(0x88, 0x3D, 0x8F, 0xD9)));

        for (int i = 0; i < nameTexts.Length; i++)
        {
            var tb = nameTexts[i]; var dpc = dpsCells[i]; var tot = totalTexts[i];
            var pct = pctTexts[i]; var img = images[i];   var bar = bars[i];

            if (rows != null && i < rows.Count)
            {
                var row     = rows[i];
                var portrait = HeroAvatarImages.TryGet(row.Name);
                bool hasPortrait = portrait != null;
                img.Source = portrait; img.Visibility = hasPortrait ? Visibility.Visible : Visibility.Collapsed;

                bool hasNick = !string.IsNullOrEmpty(row.PlayerName);
                bool hasHero = !string.IsNullOrEmpty(row.Name);
                string nameText;
                if (hasPortrait)
                    nameText = hasNick ? Truncate(row.PlayerName, 12) : hasHero ? Truncate(row.Name, 12) : (row.IsSelf ? "you" : "");
                else if (hasHero && hasNick) nameText = $"{Truncate(row.Name, 10)} {Truncate(row.PlayerName, 10)}";
                else if (hasHero)            nameText = Truncate(row.Name, 14);
                else if (hasNick)            nameText = Truncate(row.PlayerName, 14);
                else                         nameText = row.IsSelf ? "you" : "?";

                var fg = row.IsSelf ? selfBrush : peerBrush;
                var fw = row.IsSelf ? FontWeights.SemiBold : FontWeights.Normal;
                tb.Text = nameText; dpc.Text = row.Dps > 0.1 ? FormatDps(row.Dps) : "";
                tot.Text = "(" + FormatRowTotalCompact(row.Total60s) + ")"; pct.Text = $"{row.Percent:0}%";
                tb.Foreground  = fg; tb.FontWeight  = fw; dpc.Foreground = fg; dpc.FontWeight = fw;
                tot.Foreground = fg; tot.FontWeight = fw; pct.Foreground = fg; pct.FontWeight = fw;
                double clamped = Math.Clamp(row.Percent, 0.0, 100.0);
                bar.Width = BarTrackWidthPx * (clamped / maxPercent);
                bar.Fill  = row.IsSelf ? selfBarBrush : peerBarBrush;
                bar.Visibility = bar.Width > 0.5 ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                tb.Text = ""; dpc.Text = ""; tot.Text = ""; pct.Text = "";
                tb.Foreground = peerBrush; tb.FontWeight = FontWeights.Normal;
                dpc.Foreground = peerBrush; dpc.FontWeight = FontWeights.Normal;
                tot.Foreground = peerBrush; tot.FontWeight = FontWeights.Normal;
                pct.Foreground = peerBrush; pct.FontWeight = FontWeights.Normal;
                img.Source = null; img.Visibility = Visibility.Collapsed;
                bar.Width = 0; bar.Visibility = Visibility.Collapsed;
            }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────────────────
    private static SolidColorBrush FreezeBrush(SolidColorBrush b) { if (b.CanFreeze) b.Freeze(); return b; }
    private static string Truncate(string s, int max) { if (string.IsNullOrEmpty(s) || s.Length <= max) return s; return s[..(max - 1)] + "…"; }
    internal static string FormatDps(double dps)
    {
        if (dps >= 1_000_000) return (dps / 1_000_000.0).ToString("0.00") + "M";
        if (dps >= 100_000)   return (dps / 1_000.0).ToString("0") + "k";
        if (dps >= 10_000)    return (dps / 1_000.0).ToString("0.0") + "k";
        return ((int)dps).ToString("N0");
    }
    internal static string FormatTotal(long total)
    {
        if (total >= 1_000_000) return (total / 1_000_000.0).ToString("0.00") + "M";
        if (total >= 1_000)     return (total / 1_000.0).ToString("0.0") + "k";
        return total.ToString("N0");
    }
    private static string FormatRowTotalCompact(long total)
    {
        if (total >= 1_000_000) return (total / 1_000_000.0).ToString("0.0") + "M";
        if (total >= 1_000)     return (total / 1_000.0).ToString("0.0") + "k";
        return total.ToString("N0");
    }

    // ── Drag ─────────────────────────────────────────────────────────────────────────────────
    private void ContentBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragStarted?.Invoke();
    }

    // ── Save preferences ─────────────────────────────────────────────────────────────────────
    public void SaveAll(double? left = null, double? top = null)
    {
        if (left.HasValue) _settings.Left = left.Value;
        if (top.HasValue)  _settings.Top  = top.Value;
        _settings.Scale              = _scale;
        _settings.BossDpsOnly        = _bossOnlyMode;
        _settings.ShowBossSection    = _showBossSection;
        _settings.ShowPowerBreakdown = _showPowerBreakdown;
        DpsOverlaySettingsFile.Save(_settings);
    }

    // ── Boss-only toggle ─────────────────────────────────────────────────────────────────────
    private bool _bossOnlyMode;
    private bool _suppressBossOnlyMenuEvents;

    private void BossOnlyMenuItem_OnChecked(object sender, RoutedEventArgs e)
    {
        if (_suppressBossOnlyMenuEvents) return;
        _bossOnlyMode = true; BossOnlyToggled?.Invoke(true); SaveAll();
    }
    private void BossOnlyMenuItem_OnUnchecked(object sender, RoutedEventArgs e)
    {
        if (_suppressBossOnlyMenuEvents) return;
        _bossOnlyMode = false; BossOnlyToggled?.Invoke(false); SaveAll();
    }

    // ── Boss-section visibility ───────────────────────────────────────────────────────────────
    private bool _showBossSection;
    private bool _suppressShowBossSectionMenuEvents;

    private void ShowBossSectionMenuItem_OnChecked(object sender, RoutedEventArgs e)
    {
        if (_suppressShowBossSectionMenuEvents) return;
        _showBossSection = true; BossFightPanel.Visibility = Visibility.Visible; SaveAll();
    }
    private void ShowBossSectionMenuItem_OnUnchecked(object sender, RoutedEventArgs e)
    {
        if (_suppressShowBossSectionMenuEvents) return;
        _showBossSection = false; BossFightPanel.Visibility = Visibility.Collapsed; SaveAll();
    }

    // ── Power breakdown visibility ────────────────────────────────────────────────────────────
    private bool _showPowerBreakdown;
    private bool _suppressShowPowerBreakdownMenuEvents;

    private void ShowPowerBreakdownMenuItem_OnChecked(object sender, RoutedEventArgs e)
    {
        if (_suppressShowPowerBreakdownMenuEvents) return;
        _showPowerBreakdown = true; PowerBreakdownPanel.Visibility = Visibility.Visible; SaveAll();
    }
    private void ShowPowerBreakdownMenuItem_OnUnchecked(object sender, RoutedEventArgs e)
    {
        if (_suppressShowPowerBreakdownMenuEvents) return;
        _showPowerBreakdown = false; PowerBreakdownPanel.Visibility = Visibility.Collapsed; SaveAll();
    }

    // ── Scale ─────────────────────────────────────────────────────────────────────────────────
    private double _scale = 1.0;
    private MenuItem[]? _scaleMenuItems;

    private void ApplyScale(double scale, bool save = true)
    {
        _scale = Math.Clamp(scale, 0.25, 3.0);
        ContentBorder.LayoutTransform = new ScaleTransform(_scale, _scale);
        _scaleMenuItems ??= new[] { Scale25MenuItem, Scale50MenuItem, Scale75MenuItem, Scale100MenuItem,
                                    Scale125MenuItem, Scale150MenuItem, Scale175MenuItem, Scale200MenuItem };
        foreach (var mi in _scaleMenuItems)
        {
            if (double.TryParse(mi.Tag?.ToString(), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double v))
                mi.IsChecked = Math.Abs(v - _scale) < 0.01;
        }
        if (save) SaveAll();
    }

    private void ScaleMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem mi) return;
        if (double.TryParse(mi.Tag?.ToString(), System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out double scale))
            ApplyScale(scale);
    }

    // ── Menu actions ──────────────────────────────────────────────────────────────────────────
    private void SaveSnapshotMenuItem_OnClick(object sender, RoutedEventArgs e)
        => SaveSnapshotRequested?.Invoke(_lastTopHeroes, _lastEncounter, _lastPowerBreakdown);
    private void ClearDpsMenuItem_OnClick(object sender, RoutedEventArgs e)
        => ClearDpsRequested?.Invoke();
    private void ViewReportsMenuItem_OnClick(object sender, RoutedEventArgs e)
        => ViewReportsRequested?.Invoke();
    private void SwitchModeMenuItem_OnClick(object sender, RoutedEventArgs e)
        => SwitchModeRequested?.Invoke();
    private void CloseButton_OnClick(object sender, MouseButtonEventArgs e)
        => CloseRequested?.Invoke();
    private void ExitMenuItem_OnClick(object sender, RoutedEventArgs e)
        => Application.Current?.Shutdown();
}
