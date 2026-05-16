using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
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

    /// <summary>Manual "I just got a splinter, start the cooldown" override.  Bubbles up to
    /// the presenter which calls <c>EternitySplinterTracker.ArmFromNow()</c>.  Same effect as
    /// the Settings tab's "Arm Splinter cooldown now" button or the global hotkey -- exposed
    /// prominently in the live dashboard because the in-game open-world case (lots of other
    /// players around, auto-detection picks up everyone's drops) is the most common moment
    /// the user needs to override the timer mid-play.  Quantity isn't passed -- it's the
    /// auto-detection path's job to extract that from the wire archive.</summary>
    public event Action? ArmSplinterCooldownRequested;

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

    // Mode pill -- neutral grey for "all damage", orange-tinted for "boss damage only".
    private static readonly SolidColorBrush s_modePillNeutralBg = Freeze(new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF)));
    private static readonly SolidColorBrush s_modePillNeutralBd = Freeze(new SolidColorBrush(Color.FromArgb(0x44, 0xFF, 0xFF, 0xFF)));
    private static readonly SolidColorBrush s_modePillNeutralFg = Freeze(new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF)));
    private static readonly SolidColorBrush s_modePillBossBg    = Freeze(new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0x6B, 0x00)));
    private static readonly SolidColorBrush s_modePillBossBd    = Freeze(new SolidColorBrush(Color.FromArgb(0x88, 0xFF, 0x6B, 0x00)));
    private static readonly SolidColorBrush s_modePillBossFg    = Freeze(new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xCC, 0x66)));

    private static SolidColorBrush Freeze(SolidColorBrush b) { if (b.CanFreeze) b.Freeze(); return b; }

    // ── Splinter "READY" soft-pulse animation ────────────────────────────────────────────────
    // When the cooldown has expired the banner gently breathes between full and ~55% opacity
    // so the user's peripheral vision catches "your next splinter is eligible to drop now"
    // without it being a hard flash.  Stopped (and Opacity restored to 1.0) the moment the
    // cooldown re-arms or a fresh drop is detected.
    //
    // Single Storyboard instance reused for the lifetime of the panel -- starting/stopping
    // is cheap; rebuilding the Storyboard on every state change would churn allocations on
    // the 4 Hz tick.  `_pulseActive` tracks whether it's currently running so we don't
    // re-Begin() on every tick of the decay timer (each Begin would reset the easing curve
    // and visibly snap the banner back to full opacity).
    private Storyboard? _readyPulse;
    private bool _pulseActive;

    private void EnsureReadyPulseBuilt()
    {
        if (_readyPulse != null) return;

        // 1.0 -> 0.55 -> 1.0 over 1.6s, soft sinusoidal easing, repeat forever.
        // Easing avoids the linear "two-state blink" feel; the 0.55 floor keeps the text
        // legible mid-pulse (going below ~0.4 makes the countdown text hard to read).
        var anim = new DoubleAnimation
        {
            From           = 1.0,
            To             = 0.55,
            Duration       = new Duration(TimeSpan.FromMilliseconds(800)),
            AutoReverse    = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
        };
        Storyboard.SetTarget(anim, SplinterBanner);
        Storyboard.SetTargetProperty(anim, new PropertyPath(OpacityProperty));

        _readyPulse = new Storyboard();
        _readyPulse.Children.Add(anim);
    }

    private void StartReadyPulse()
    {
        if (_pulseActive) return;          // idempotent across 4 Hz ticks
        EnsureReadyPulseBuilt();
        _readyPulse!.Begin();
        _pulseActive = true;
    }

    private void StopReadyPulse()
    {
        if (!_pulseActive) return;
        _readyPulse?.Stop();
        // Explicitly restore Opacity to 1.0 -- Stop() leaves the property at whatever value
        // the animation last set, which would freeze the banner at e.g. 0.7 if we stopped
        // mid-cycle.  Direct assignment cancels the animation hold without needing
        // BeginAnimation(null).
        SplinterBanner.BeginAnimation(OpacityProperty, null);
        SplinterBanner.Opacity = 1.0;
        _pulseActive = false;
    }

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
        // The mode pill (set below) carries the "what damage am I counting" info on its
        // own, so the title strips the redundant "DPS" / "BOSS DPS" prefix the compact
        // panel uses -- the huge 42-pt number IS the DPS, the label was just doubling up.
        // When we're inside a known boss fight we surface that on top:
        //   "vs Juggernaut  ·  Cyclops"     (boss known, in fight)
        //   "Cyclops  (Vash)"               (no fight or unmapped boss, hero known)
        //   "DPS Meter"                     (nothing known yet)
        bool inBossFight = bossOnlyMode && (encounter.IsActive || encounter.IsEnded);
        string heroOrPlayer = heroDisplayName ?? string.Empty;
        if (string.IsNullOrEmpty(heroOrPlayer) && topHeroes != null)
        {
            for (int i = 0; i < topHeroes.Count; i++)
            {
                var r = topHeroes[i];
                if (r.IsSelf && !string.IsNullOrEmpty(r.PlayerName)) { heroOrPlayer = r.PlayerName; break; }
            }
        }
        if (inBossFight && !string.IsNullOrEmpty(bossDisplayName))
            HeroTitleText.Text = string.IsNullOrEmpty(heroOrPlayer)
                ? $"vs {bossDisplayName}"
                : $"vs {bossDisplayName}  ·  {heroOrPlayer}";
        else if (!string.IsNullOrEmpty(heroOrPlayer))
            HeroTitleText.Text = heroOrPlayer;
        else
            HeroTitleText.Text = "DPS Meter";

        // ── Mode pill ────────────────────────────────────────────────────────────────────────
        // Explicit "what damage am I looking at" badge -- the leaderboard numbers depend
        // entirely on this, and the difference was invisible at-a-glance before.
        if (bossOnlyMode)
        {
            ModeText.Text = "BOSS DAMAGE ONLY";
            ModePill.Background  = s_modePillBossBg;
            ModePill.BorderBrush = s_modePillBossBd;
            ModeText.Foreground  = s_modePillBossFg;
        }
        else
        {
            ModeText.Text = "ALL DAMAGE";
            ModePill.Background  = s_modePillNeutralBg;
            ModePill.BorderBrush = s_modePillNeutralBd;
            ModeText.Foreground  = s_modePillNeutralFg;
        }

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

        // ── Boss-fight section ───────────────────────────────────────────────────────────────
        // Show the dedicated boss section whenever the parallel _bossMeter is mid-encounter
        // or just-ended, AND we're NOT already in boss-only mode.  In boss-only mode the
        // main leaderboard above IS the boss view, so a duplicate section would just take
        // up space; in all-damage mode this section gives the user "both views at once"
        // without changing modes.
        bool showBossSection = !bossOnlyMode
            && (bossEncounter.IsActive || bossEncounter.IsEnded);
        if (showBossSection)
        {
            BossFightBanner.Visibility = Visibility.Visible;
            BossNameText.Text   = string.IsNullOrEmpty(bossDisplayName) ? "(unknown)" : bossDisplayName;
            BossDpsText.Text    = bossDps > 0.1 ? FormatDps(bossDps) : "—";
            BossStatusText.Text = bossEncounter.IsEnded
                ? $"fight ended · Fight: {FormatTotal(bossEncounter.SelfTotal)}"
                : (bossDps > 0.1
                    ? $"live · Fight: {FormatTotal(bossEncounter.SelfTotal)}"
                    : $"60s avg · Fight: {FormatTotal(bossEncounter.SelfTotal)}");
            RenderBossLeaderboard(bossTopHeroes);
        }
        else
        {
            BossFightBanner.Visibility = Visibility.Collapsed;
        }
    }

    private void RenderBossLeaderboard(IReadOnlyList<DpsMeterClass.HeroShareEntry>? rows)
    {
        if (rows == null || rows.Count == 0)
        {
            BossLeaderboardRows.ItemsSource = null;
            return;
        }

        // Same scaling logic as the main leaderboard; smaller hit area (30px portrait,
        // 34px row height) since this section is supplementary -- the all-damage view above
        // is the primary one.
        double maxPercent = 0;
        foreach (var r in rows) if (r.Percent > maxPercent) maxPercent = r.Percent;
        if (maxPercent <= 0.01) maxPercent = 1.0;

        const double trackWidthPx = 600.0;  // boss section is full-width, wider track
        var view = new List<LeaderboardRow>(rows.Count);
        foreach (var r in rows)
        {
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
        BossLeaderboardRows.ItemsSource = view;
    }

    // User's "Show buffs and procs" preference -- when false we short-circuit the two buff
    // updaters AND force the controls Collapsed.  Default true: opt-in feature surface,
    // visible out of the box.  Toggled at runtime via SetBuffPanelsVisible from Settings.
    private bool _buffPanelsVisible = true;

    /// <summary>Forwards a snapshot of active buffs to the embedded two-tier buff strip.
    /// Called from <c>DpsOverlayPresenter</c> on every decay tick (4 Hz).  No-op-cheap when
    /// the snapshot is empty; the strip's rows collapse themselves in that case.  Skipped
    /// entirely when the user has disabled buff panels in Settings -- saves the per-tick
    /// classification cost when the feature is off.</summary>
    public void UpdateBuffs(IReadOnlyList<ActiveBuff> active, DateTime nowUtc)
    {
        if (!_buffPanelsVisible) return;
        BuffStrip.UpdateBuffs(active, nowUtc);
    }

    /// <summary>Forwards the live <c>BuffTracker</c> to the stats panel so it can compute
    /// per-tile property sums.  Called from <c>DpsOverlayPresenter</c> alongside
    /// <see cref="UpdateBuffs"/>.  Passing the tracker (not a precomputed snapshot) lets the
    /// stats panel ask only for the enums in its catalog -- avoids us building a generic
    /// "all property sums" dictionary on every tick.  Skipped when buff panels are
    /// hidden.</summary>
    public void UpdateBuffStats(BuffTracker? tracker)
    {
        if (!_buffPanelsVisible) return;
        BuffStats.UpdateStats(tracker);
    }

    /// <summary>Show or hide BOTH buff-tracking surfaces -- the stats tiles (BuffStats) and
    /// the two-tier chip strip (BuffStrip).  Treated as a single toggle on purpose: they're
    /// the same feature surface from the user's perspective ("buff tracking"), and a hide
    /// state where the stats row is gone but the chip strip remains (or vice versa) would
    /// be confusing.  Called from the presenter when the user toggles "Show buffs and
    /// procs" in Settings, and once at startup to honour the persisted preference.
    ///
    /// <para>When toggled off we force both child controls to <see cref="Visibility.Collapsed"/>
    /// AND short-circuit future updates -- so the row stays hidden even between decay ticks
    /// that would otherwise re-show the controls based on active-buff state.  When toggled
    /// back on, the next decay tick's update will re-render normally.</para></summary>
    public void SetBuffPanelsVisible(bool visible)
    {
        _buffPanelsVisible = visible;
        if (!visible)
        {
            // Hard-collapse both -- the internal "no buffs => Collapsed" logic in each
            // control won't run again while _buffPanelsVisible is false, so we have to set
            // this here once to actually clear the row.
            BuffStats.Visibility = Visibility.Collapsed;
            BuffStrip.Visibility = Visibility.Collapsed;
        }
        // Re-show case: deliberately do nothing.  The next presenter tick (~250 ms later)
        // calls UpdateBuffs / UpdateBuffStats, which will set Visibility per active-buff
        // state.  No flicker because the user just toggled the checkbox -- they expect a
        // brief moment before content fills back in.
    }

    public void UpdateSplinterStatus(bool cooldownActive, TimeSpan remaining, int dropCount, int totalSplinters, bool justDropped)
    {
        // The full-width banner is always visible -- even on first launch with no drops
        // yet, the "READY" state with the icon and label tells the user the feature exists.
        // Three states drive the colours / status text / progress bar; the count text on
        // the right tallies SPLINTERS (not drop events) this session, because each drop can
        // yield 1, 5, 9, 14 splinters depending on loot rolls -- "27 splinters in 3 drops"
        // is way more useful to track than "3 drops".  The smaller drop count is mentioned in
        // parens for context.  Empty string when no drops yet so the banner stays uncluttered.
        SplinterCountText.Text = totalSplinters > 0
            ? $"{totalSplinters} splinters · {dropCount} drop{(dropCount == 1 ? "" : "s")}"
            : string.Empty;

        if (justDropped)
        {
            // Cooldown was just armed -- progress bar starts near zero; bright orange flash.
            // Caption pulls the duration off the tracker constant so a future tuning of
            // CooldownDuration doesn't leave a "7-minute cooldown armed" lie on screen.
            StopReadyPulse();   // we're no longer ready -- restore Opacity to 1.0
            int armMinutes = (int)EternitySplinterTracker.CooldownDuration.TotalMinutes;
            SplinterStatusBig.Text   = "DROPPED!";
            SplinterCaption.Text     = $"{armMinutes}-minute cooldown armed";
            SplinterStatusBig.Foreground = s_splFlash;
            SplinterIcon.Foreground      = s_splFlash;
            SplinterAccent.Fill          = s_splFlashBd;
            SplinterBanner.BorderBrush   = s_splFlashBd;
            SplinterProgress.Foreground  = s_splFlash;
            SplinterProgress.Value       = 0;
        }
        else if (cooldownActive)
        {
            StopReadyPulse();   // counting down is its own visual signal; no pulse needed
            int totalSec = (int)Math.Ceiling(remaining.TotalSeconds);
            int mm = totalSec / 60;
            int ss = totalSec % 60;
            SplinterStatusBig.Text   = $"{mm}:{ss:00}";
            SplinterCaption.Text     = "until next eligible drop";
            SplinterStatusBig.Foreground = s_splCount;
            SplinterIcon.Foreground      = s_splCount;
            SplinterAccent.Fill          = s_splCountBd;
            SplinterBanner.BorderBrush   = s_splCountBd;
            SplinterProgress.Foreground  = s_splCount;
            // Fill bar UP as we approach "ready": 0% just after drop, 100% at ready.
            double total = EternitySplinterTracker.CooldownDuration.TotalSeconds;
            double elapsed = total - remaining.TotalSeconds;
            SplinterProgress.Value = Math.Clamp(elapsed / total * 100.0, 0, 100);
        }
        else
        {
            SplinterStatusBig.Text   = "READY";
            SplinterCaption.Text     = dropCount > 0
                ? "next drop eligible -- go kill something"
                : "no drops yet; tracker armed";
            SplinterStatusBig.Foreground = s_splReady;
            SplinterIcon.Foreground      = s_splReady;
            SplinterAccent.Fill          = s_splReadyBd;
            SplinterBanner.BorderBrush   = s_splReadyBd;
            SplinterProgress.Foreground  = s_splReady;
            SplinterProgress.Value       = 100;
            // Soft 0.8s in / 0.8s out opacity pulse so peripheral vision catches the
            // ready state without it being a hard flash.  Idempotent across 4 Hz ticks.
            StartReadyPulse();
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

    private void ArmSplinterButton_Click(object sender, RoutedEventArgs e)
        => ArmSplinterCooldownRequested?.Invoke();

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
