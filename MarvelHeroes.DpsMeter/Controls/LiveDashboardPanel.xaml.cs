using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MarvelHeroesComporator.NetworkSniffer;
using MarvelHeroes.DpsMeter.Services;
using DpsMeterClass = MarvelHeroes.DpsMeter.Services.DpsMeter;

namespace MarvelHeroes.DpsMeter.Controls;

/// <summary>
/// "Dashboard" rendering of the same data <see cref="DpsDisplayPanel"/> shows, but laid out
/// for the wider main-app window instead of the 280-px overlay.  Same update signatures so
/// the presenter pushes identical payloads to both views; both stay in sync independently.
///
/// <para>Key differences vs the overlay panel:</para>
/// <list type="bullet">
///   <item>Large hero portrait (84 px) + big DPS number in a summary card at the top.</item>
///   <item>Leaderboard and power-breakdown side-by-side in two cards instead of stacked.</item>
///   <item>Boss-fight info shown as an inline banner only when an encounter is active or
///         just-ended -- no manual toggle needed.</item>
///   <item>Three max-hit scopes (fight / session / record) stacked in the summary card.</item>
///   <item>No right-click context menu -- the Settings tab and right-click on the overlay
///         carry the per-display toggles, and the main window's Save snapshot is a
///         dedicated button on the summary card.</item>
/// </list>
/// </summary>
public partial class LiveDashboardPanel : UserControl
{
    // Same event surface DpsLiveWindow / MainAppWindow expect from a panel, so the existing
    // bubbling chain in MainAppWindow keeps working without changes.
    public event Action<IReadOnlyList<DpsMeterClass.HeroShareEntry>?,
                        DpsMeterClass.EncounterSnapshot,
                        IReadOnlyList<DpsMeterClass.PowerBreakdownEntry>?>? SaveSnapshotRequested;

    public LiveDashboardPanel()
    {
        InitializeComponent();
    }

    // Per-tick caches so the Save snapshot button has something to forward.
    private IReadOnlyList<DpsMeterClass.HeroShareEntry>? _lastTopHeroes;
    private DpsMeterClass.EncounterSnapshot              _lastEncounter;
    private IReadOnlyList<DpsMeterClass.PowerBreakdownEntry>? _lastPowerBreakdown;

    // Bar-colour palette for the power breakdown -- same ordering / hues the overlay uses
    // so the visual identity carries across views.
    private static readonly Color[] s_powerColors =
    {
        Color.FromRgb(0xFF, 0x69, 0x00), Color.FromRgb(0x3D, 0x8F, 0xD9),
        Color.FromRgb(0x4C, 0xAF, 0x50), Color.FromRgb(0xAB, 0x47, 0xBC),
        Color.FromRgb(0x00, 0xBC, 0xD4), Color.FromRgb(0xFF, 0xC1, 0x07),
        Color.FromRgb(0xE9, 0x1E, 0x63), Color.FromRgb(0x00, 0x96, 0x88),
    };

    // Cached brushes for self vs peer leaderboard rows.
    private static readonly SolidColorBrush s_selfBar   = Freeze(new SolidColorBrush(Color.FromArgb(0x99, 0xD6, 0x2A, 0x2A)));
    private static readonly SolidColorBrush s_peerBar   = Freeze(new SolidColorBrush(Color.FromArgb(0x88, 0x3D, 0x8F, 0xD9)));
    private static readonly SolidColorBrush s_selfFg    = Freeze(new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xB3, 0x47)));
    private static readonly SolidColorBrush s_peerFg    = Freeze(new SolidColorBrush(Color.FromArgb(0xDD, 0xFF, 0xFF, 0xFF)));

    // Splinter pill state brushes -- same colours the compact panel uses.
    private static readonly SolidColorBrush s_splReady     = Freeze(new SolidColorBrush(Color.FromArgb(0xFF, 0xB0, 0xCD, 0xFF)));
    private static readonly SolidColorBrush s_splCount     = Freeze(new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xCC, 0x66)));
    private static readonly SolidColorBrush s_splFlash     = Freeze(new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0x9A, 0x3C)));
    private static readonly SolidColorBrush s_splReadyBg   = Freeze(new SolidColorBrush(Color.FromArgb(0x33, 0x2E, 0x7F, 0xFF)));
    private static readonly SolidColorBrush s_splReadyBd   = Freeze(new SolidColorBrush(Color.FromArgb(0x55, 0x2E, 0x7F, 0xFF)));
    private static readonly SolidColorBrush s_splCountBg   = Freeze(new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xB3, 0x47)));
    private static readonly SolidColorBrush s_splCountBd   = Freeze(new SolidColorBrush(Color.FromArgb(0x66, 0xFF, 0xB3, 0x47)));
    private static readonly SolidColorBrush s_splFlashBg   = Freeze(new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0x6B, 0x00)));
    private static readonly SolidColorBrush s_splFlashBd   = Freeze(new SolidColorBrush(Color.FromArgb(0x99, 0xFF, 0x6B, 0x00)));

    private static SolidColorBrush Freeze(SolidColorBrush b) { if (b.CanFreeze) b.Freeze(); return b; }

    // ── Main update entry ─────────────────────────────────────────────────────────────────────
    // Signature matches DpsDisplayPanel.UpdateDps so MainAppWindow can call either with the
    // same args.  The implementation diverges in how it presents the same data.

    public void UpdateDps(
        double dps,
        long totalDamage60s,
        long totalDamageSession,
        ulong ownerEntityId,
        uint maxSingleHit,
        uint maxSingleHitSession,
        uint maxSingleHitEncounter,
        string heroDisplayName,
        string bossDisplayName,
        bool bossOnlyMode,
        IReadOnlyList<DpsMeterClass.HeroShareEntry>? topHeroes,
        DpsMeterClass.EncounterSnapshot encounter,
        double bossDps = 0.0,
        long bossTotalDamage60s = 0,
        IReadOnlyList<DpsMeterClass.HeroShareEntry>? bossTopHeroes = null,
        DpsMeterClass.EncounterSnapshot bossEncounter = default,
        IReadOnlyList<DpsMeterClass.PowerBreakdownEntry>? powerBreakdown = null)
    {
        _lastTopHeroes      = topHeroes;
        _lastEncounter      = encounter;
        _lastPowerBreakdown = powerBreakdown;

        // ── Header / title text ──────────────────────────────────────────────────────────────
        bool inBossFight = bossOnlyMode && (encounter.IsActive || encounter.IsEnded);
        string titlePrefix = inBossFight && !string.IsNullOrEmpty(bossDisplayName)
            ? $"BOSS: {bossDisplayName}"
            : (bossOnlyMode ? "BOSS DPS" : "DPS");
        string titleSuffix = heroDisplayName ?? string.Empty;
        if (string.IsNullOrEmpty(titleSuffix) && topHeroes != null)
        {
            for (int i = 0; i < topHeroes.Count; i++)
            {
                var r = topHeroes[i];
                if (r.IsSelf && !string.IsNullOrEmpty(r.PlayerName)) { titleSuffix = r.PlayerName; break; }
            }
        }
        HeroTitleText.Text = string.IsNullOrEmpty(titleSuffix) ? titlePrefix : $"{titlePrefix} · {titleSuffix}";

        // ── Big DPS number ───────────────────────────────────────────────────────────────────
        bool liveActive = dps > 0.1;
        double displayDps = liveActive ? dps : (totalDamage60s > 0 ? totalDamage60s / 60.0 : 0.0);
        DpsText.Text = displayDps <= 0.1 ? "—" : FormatDps(displayDps);

        // ── Detail / mode line ───────────────────────────────────────────────────────────────
        DetailText.Text = BuildDetailLine(liveActive, totalDamage60s, totalDamageSession,
                                          ownerEntityId, bossOnlyMode, encounter);

        // ── Hero portrait ────────────────────────────────────────────────────────────────────
        // heroDisplayName is declared non-null but the compiler sees the `?? ""` above and
        // infers it as nullable for the rest of the method, so guard explicitly here.
        HeroPortrait.Source = HeroAvatarImages.TryGet(heroDisplayName ?? "");

        // ── Max-hit triplet ──────────────────────────────────────────────────────────────────
        MaxHitFightText.Text   = maxSingleHitEncounter == 0 ? "—" : FormatTotal(maxSingleHitEncounter);
        MaxHitSessionText.Text = maxSingleHitSession   == 0 ? "—" : FormatTotal(maxSingleHitSession);
        MaxHitRecordText.Text  = maxSingleHit          == 0 ? "—" : FormatTotal(maxSingleHit);

        // ── Leaderboard rows ─────────────────────────────────────────────────────────────────
        RenderLeaderboard(topHeroes);

        // ── Power breakdown rows ─────────────────────────────────────────────────────────────
        RenderPowers(powerBreakdown);

        // ── Boss-fight banner ────────────────────────────────────────────────────────────────
        if (inBossFight)
        {
            BossFightBanner.Visibility = Visibility.Visible;
            BossNameText.Text   = string.IsNullOrEmpty(bossDisplayName) ? "(unknown)" : bossDisplayName;
            BossDpsText.Text    = bossDps > 0.1 ? FormatDps(bossDps) : "—";
            BossStatusText.Text = encounter.IsEnded
                ? $"fight ended · Fight: {FormatTotal(encounter.SelfTotal)}"
                : (bossDps > 0.1
                    ? $"live · Fight: {FormatTotal(encounter.SelfTotal)}"
                    : $"60s avg · Fight: {FormatTotal(encounter.SelfTotal)}");
        }
        else
        {
            BossFightBanner.Visibility = Visibility.Collapsed;
        }
    }

    public void UpdateSplinterStatus(bool cooldownActive, TimeSpan remaining, int dropCount, bool justDropped)
    {
        // The dashboard always shows the splinter pill if there's any state to show.  Hide
        // entirely when there's nothing yet -- avoids a "ready / no data" pill confusing
        // the user on first launch.
        bool anyData = dropCount > 0 || cooldownActive || justDropped;
        if (!anyData)
        {
            SplinterPanel.Visibility = Visibility.Collapsed;
            return;
        }

        SplinterPanel.Visibility = Visibility.Visible;
        string suffix = dropCount > 0 ? $"  ({dropCount} today)" : string.Empty;
        if (justDropped)
        {
            SplinterText.Text = "ES: dropped!" + suffix;
            SplinterText.Foreground   = s_splFlash;
            SplinterPanel.Background  = s_splFlashBg;
            SplinterPanel.BorderBrush = s_splFlashBd;
        }
        else if (cooldownActive)
        {
            int totalSec = (int)Math.Ceiling(remaining.TotalSeconds);
            int mm = totalSec / 60;
            int ss = totalSec % 60;
            SplinterText.Text = $"ES: {mm}:{ss:00}" + suffix;
            SplinterText.Foreground   = s_splCount;
            SplinterPanel.Background  = s_splCountBg;
            SplinterPanel.BorderBrush = s_splCountBd;
        }
        else
        {
            SplinterText.Text = "ES: ready" + suffix;
            SplinterText.Foreground   = s_splReady;
            SplinterPanel.Background  = s_splReadyBg;
            SplinterPanel.BorderBrush = s_splReadyBd;
        }
    }

    // ── Leaderboard rendering ─────────────────────────────────────────────────────────────────

    private void RenderLeaderboard(IReadOnlyList<DpsMeterClass.HeroShareEntry>? rows)
    {
        if (rows == null || rows.Count == 0)
        {
            LeaderboardRows.ItemsSource = null;
            LeaderboardEmptyHint.Visibility = Visibility.Visible;
            return;
        }

        LeaderboardEmptyHint.Visibility = Visibility.Collapsed;
        double maxPercent = 0;
        foreach (var r in rows) if (r.Percent > maxPercent) maxPercent = r.Percent;
        if (maxPercent <= 0.01) maxPercent = 1.0;

        const double trackWidthPx = 380.0;  // approximate; ItemTemplate trims as needed
        var view = new List<LeaderboardRow>(rows.Count);
        foreach (var r in rows)
        {
            // Same "treat synthetic #XXXX as not a real nickname" rule as the compact panel
            // and report viewer -- prefer hero name when the only thing we'd otherwise show
            // is the OwnerId-derived hash.
            bool realNick = !string.IsNullOrEmpty(r.PlayerName)
                && !(r.PlayerName.Length > 1 && r.PlayerName[0] == '#');
            string display = !string.IsNullOrEmpty(r.Name) && realNick
                ? $"{r.Name}  ({r.PlayerName})"
                : !string.IsNullOrEmpty(r.Name) ? r.Name
                : realNick ? r.PlayerName!
                : r.IsSelf ? "you" : "?";

            double pct = Math.Clamp(r.Percent, 0, 100);
            view.Add(new LeaderboardRow
            {
                Portrait    = HeroAvatarImages.TryGet(r.Name),
                DisplayName = display,
                DpsText     = r.Dps   > 0.1 ? FormatDps(r.Dps)   : "",
                TotalText   = r.Total60s > 0 ? FormatTotal(r.Total60s) : "",
                PctText     = $"{r.Percent:0}%",
                BarWidth    = trackWidthPx * (pct / maxPercent),
                BarFill     = r.IsSelf ? s_selfBar : s_peerBar,
                TextColor   = r.IsSelf ? s_selfFg  : s_peerFg,
                FontWeight  = r.IsSelf ? FontWeights.SemiBold : FontWeights.Normal,
            });
        }
        LeaderboardRows.ItemsSource = view;
    }

    private void RenderPowers(IReadOnlyList<DpsMeterClass.PowerBreakdownEntry>? rows)
    {
        if (rows == null || rows.Count == 0)
        {
            PowerRows.ItemsSource = null;
            PowersEmptyHint.Visibility = Visibility.Visible;
            return;
        }

        PowersEmptyHint.Visibility = Visibility.Collapsed;
        double pctSum = 0;
        foreach (var r in rows) pctSum += r.Percent;
        if (pctSum <= 0) pctSum = 100;

        const double trackWidthPx = 380.0;
        var view = new List<PowerRowVm>(rows.Count);
        for (int i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            var c = s_powerColors[i % s_powerColors.Length];
            long avg = r.Hits > 0 ? r.TotalDamage / r.Hits : 0;
            view.Add(new PowerRowVm
            {
                Name      = r.Name,
                HitsText  = r.Hits + "x",
                AvgText   = avg > 0 ? FormatTotal(avg) : "",
                MaxText   = r.MaxHit > 0 ? FormatTotal(r.MaxHit) : "",
                TotalText = FormatTotal(r.TotalDamage),
                PctText   = $"{r.Percent:0}%",
                BarWidth  = trackWidthPx * (r.Percent / pctSum),
                BarFill   = Freeze(new SolidColorBrush(Color.FromArgb(0x44, c.R, c.G, c.B))),
                TextColor = Freeze(new SolidColorBrush(Color.FromArgb(0xCC, c.R, c.G, c.B))),
            });
        }
        PowerRows.ItemsSource = view;
    }

    // ── Detail line builder (mirrors DpsDisplayPanel logic) ──────────────────────────────────

    private static string BuildDetailLine(bool liveActive, long totalDamage60s, long totalDamageSession,
        ulong ownerEntityId, bool bossOnlyMode, DpsMeterClass.EncounterSnapshot encounter)
    {
        if (ownerEntityId == 0) return "locating you…";
        if (bossOnlyMode)
        {
            if (encounter.IsEnded)
                return $"fight ended · Fight: {FormatTotal(encounter.SelfTotal)}";
            if (encounter.IsActive)
                return liveActive
                    ? $"live · Fight: {FormatTotal(encounter.SelfTotal)}"
                    : $"60s avg · Fight: {FormatTotal(encounter.SelfTotal)}";
            if (liveActive)
                return $"live · 60s: {FormatTotal(totalDamage60s)}";
            if (totalDamage60s > 0)
                return $"60s avg · 60s: {FormatTotal(totalDamage60s)}";
            return "waiting for boss…";
        }
        if (liveActive)
            return $"live · Total: {FormatTotal(totalDamageSession)}";
        if (totalDamageSession > 0)
            return totalDamage60s > 0
                ? $"60s avg · Total: {FormatTotal(totalDamageSession)}"
                : $"idle · Total: {FormatTotal(totalDamageSession)}";
        return "idle · waiting for damage";
    }

    // ── Button handlers ───────────────────────────────────────────────────────────────────────

    private void SaveSnapshotButton_OnClick(object sender, RoutedEventArgs e)
        => SaveSnapshotRequested?.Invoke(_lastTopHeroes, _lastEncounter, _lastPowerBreakdown);

    // ── Format helpers (same scaling as DpsDisplayPanel) ─────────────────────────────────────

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

    // ── View-model classes used by the ItemsControl bindings ─────────────────────────────────

    private sealed class LeaderboardRow
    {
        public ImageSource? Portrait   { get; init; }
        public string DisplayName      { get; init; } = "";
        public string DpsText          { get; init; } = "";
        public string TotalText        { get; init; } = "";
        public string PctText          { get; init; } = "";
        public double BarWidth         { get; init; }
        public Brush  BarFill          { get; init; } = Brushes.Transparent;
        public Brush  TextColor        { get; init; } = Brushes.White;
        public FontWeight FontWeight   { get; init; } = FontWeights.Normal;
    }

    private sealed class PowerRowVm
    {
        public string Name      { get; init; } = "";
        public string HitsText  { get; init; } = "";
        public string AvgText   { get; init; } = "";
        public string MaxText   { get; init; } = "";
        public string TotalText { get; init; } = "";
        public string PctText   { get; init; } = "";
        public double BarWidth  { get; init; }
        public Brush  BarFill   { get; init; } = Brushes.Transparent;
        public Brush  TextColor { get; init; } = Brushes.White;
    }
}
