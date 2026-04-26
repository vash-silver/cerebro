using System;
using System.Collections.Generic;
using System.Windows;
using MarvelHeroesComporator.NetworkSniffer;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Rectangle = System.Windows.Shapes.Rectangle;
using MarvelHeroes.DpsMeter.Interop;
using MarvelHeroes.DpsMeter.Services;
// `DpsMeter` (the class) collides with the `DpsMeter` namespace segment of this assembly's
// root namespace (`MarvelHeroes.DpsMeter`).  When the C# compiler sees an unqualified
// `DpsMeter.HeroShareEntry`, lexical scoping resolves `DpsMeter` to the namespace first
// and the lookup fails (no nested `HeroShareEntry` namespace exists).  An alias forces the
// type lookup to bind to the `Services.DpsMeter` class regardless of namespace shadowing.
using DpsMeterClass = MarvelHeroes.DpsMeter.Services.DpsMeter;

namespace MarvelHeroes.DpsMeter;

/// <summary>
/// Always-on-top, click-through-optional, draggable DPS number overlay.
///
/// Layout is intentionally tiny: one big number for <c>DpsMeter.CurrentDps</c> with an optional
/// detail line (60s total, debug owner-id). The border itself is the drag grip — Marvel Heroes'
/// own HUD leaves most of the screen usable, so users park this in a corner and forget about it.
/// The window saves its last known position to a local JSON file so it survives app restarts.
/// </summary>
public partial class DpsOverlayWindow : Window
{
    /// <summary>Full disk snapshot (window + sniffer-related keys). Sniffer fields are not edited in UI — users set them in JSON; saves preserve them.</summary>
    private readonly DpsOverlaySettingsFile _persistedSettings;

    /// <summary>Boss-only flag loaded from <c>dps-overlay.json</c> before the presenter wires <see cref="DpsMeter"/>.</summary>
    public bool InitialBossOnlyPreference { get; }

    public DpsOverlayWindow()
    {
        InitializeComponent();

        _persistedSettings = DpsOverlaySettingsFile.Load();
        Left = _persistedSettings.Left;
        Top = _persistedSettings.Top;
        InitialBossOnlyPreference = _persistedSettings.BossDpsOnly;
        _bossOnlyMode = _persistedSettings.BossDpsOnly;
        _suppressBossOnlyMenuEvents = true;
        try { BossOnlyMenuItem.IsChecked = _persistedSettings.BossDpsOnly; }
        finally { _suppressBossOnlyMenuEvents = false; }

        SourceInitialized += OnSourceInitialized;
        LocationChanged += OnLocationChanged;
        Closing += OnClosingSavePosition;
    }

    /// <summary>Thread-safe entry point to update the number.  Safe to call from the DpsMeter's
    /// background thread — we marshal to the UI dispatcher internally.</summary>
    /// <param name="heroDisplayName">Empty string until the hero is identified, then a pretty
    /// label like "Blade" / "Iron Man" sourced from either <see cref="HeroPrototypes.Names"/>
    /// (via EntityCreate) or <see cref="HeroPowers.Names"/> (via a power hit — fallback for when
    /// EntityCreate was missed).</param>
    /// <param name="bossOnlyMode">Must match <see cref="DpsMeterClass.BossOnlyMode"/> — drives the
    /// title prefix and keeps the context menu checkmark in sync every tick (single source of truth).</param>
    /// <param name="totalDamage60s">ALWAYS the 60s sliding-window total for the local self owner.
    /// Drives the big-number fallback (live 5s → 60s avg → "—") regardless of mode — that
    /// behavior must stay invariant or the user perceives the meter as "broken / changed."</param>
    /// <param name="totalDamageSession">Cumulative-since-region total for the local self owner.
    /// Drives the <c>Total: X</c> label on the detail line in normal (non-boss) mode — the
    /// non-decaying mirror of <c>Fight: X</c> in boss mode.  Resets on region change and on
    /// boss-only mode toggle.</param>
    /// <param name="topHeroes">Up to 5 rows of nearby heroes ranked by total damage (descending).
    /// Source depends on mode: encounter accumulator in boss mode (totals from first hit until
    /// last engaged boss dies), cumulative session totals in normal mode.  Pass <c>null</c> or
    /// an empty list to blank the leaderboard (used during region transitions).</param>
    /// <param name="encounter">Encounter snapshot from <see cref="DpsMeterClass.GetEncounterSnapshot"/>.
    /// In boss mode drives the detail line's "Fight: X" / "fight ended · Fight: X" label;
    /// the BIG NUMBER never reads from this — encounter data is only ever shown on the
    /// detail line.  Ignored in normal mode (encounter has no meaning when trash damage is
    /// included).</param>
    public void UpdateDps(
        double dps,
        long totalDamage60s,
        long totalDamageSession,
        ulong ownerEntityId,
        uint maxSingleHit,
        string heroDisplayName,
        bool bossOnlyMode,
        IReadOnlyList<DpsMeterClass.HeroShareEntry>? topHeroes,
        DpsMeterClass.EncounterSnapshot encounter)
    {
        if (!Dispatcher.CheckAccess())
        {
            // Use BeginInvoke instead of Invoke so the capture-thread event handler doesn't block
            // on UI rendering — if the UI thread is busy we just drop the visual update (next
            // event will catch up), which is preferable to stalling packet processing.
            Dispatcher.BeginInvoke(new Action(() => UpdateDps(dps, totalDamage60s, totalDamageSession, ownerEntityId, maxSingleHit, heroDisplayName, bossOnlyMode, topHeroes, encounter)));
            return;
        }

        // Mirror meter state so title / menu cannot drift from the filter actually applied in DpsMeter.
        _bossOnlyMode = bossOnlyMode;
        if (BossOnlyMenuItem.IsChecked != bossOnlyMode)
        {
            _suppressBossOnlyMenuEvents = true;
            try { BossOnlyMenuItem.IsChecked = bossOnlyMode; }
            finally { _suppressBossOnlyMenuEvents = false; }
        }

        // Title: "DPS" until we've identified the avatar, then "DPS - Blade".  Boss-only mode
        // swaps the prefix to "BOSS DPS" as a persistent reminder that trash hits are being
        // filtered out — otherwise a user who toggled the filter and then forgot about it would
        // read a suspiciously-low number and assume the meter broke.
        //
        // Fallback: if the hero name couldn't be resolved (mid-session attach + the user's
        // powers aren't in the HeroPowers index), pull the player nickname from the self row
        // in topHeroes so the title says "DPS - Bandit" instead of just "DPS".  An anonymous
        // bare "DPS" title was the cue that made users assume "the meter doesn't know I exist"
        // even when the 60s number underneath was correct.
        string titlePrefix = bossOnlyMode ? "BOSS DPS" : "DPS";
        string titleSuffix = heroDisplayName ?? string.Empty;
        if (string.IsNullOrEmpty(titleSuffix) && topHeroes != null)
        {
            for (int i = 0; i < topHeroes.Count; i++)
            {
                var r = topHeroes[i];
                if (r.IsSelf && !string.IsNullOrEmpty(r.PlayerName))
                {
                    titleSuffix = r.PlayerName;
                    break;
                }
            }
        }
        HeroTitleText.Text = string.IsNullOrEmpty(titleSuffix)
            ? titlePrefix
            : $"{titlePrefix} - {titleSuffix}";

        // Primary big number — what the user reads as "my DPS right now".  Behavior must
        // stay identical to the pre-encounter version (this is the most-glanced element on
        // the overlay and any change reads as "the meter is broken"):
        //   • Live 5s instant DPS while combat is active (responds within ~1s of new hits).
        //   • Falls back to 60s rolling-average DPS when the 5s window has drained but the
        //     60s window still has data.
        //   • "—" only when truly idle (entire 60s window empty).
        //
        // The encounter accumulator is INTENTIONALLY not consulted here — it drives only
        // the bar list and the "Fight: …" label on the detail line.  Keeping the big
        // number on the same time-windowed math means the user always reads it as a rate
        // ("DPS right now"), not as a fight average ("DPS for this fight"), which would
        // confuse the relationship with Max hit and the bar percentages.
        bool liveActive = dps > 0.1;
        double displayDps = liveActive
            ? dps
            : (totalDamage60s > 0 ? totalDamage60s / 60.0 : 0.0);
        DpsText.Text = displayDps <= 0.1 ? "—" : FormatDps(displayDps);

        MaxHitText.Text = maxSingleHit == 0
            ? ""
            : $"Max hit: {FormatTotal(maxSingleHit)}";

        // Detail line carries two roles:
        //   • a small mode hint so the big number above can never be misread — "live"
        //     while the 5s window is feeding it, "<window> avg" while we're showing the
        //     decaying rolling average, "idle" when both windows are empty, AND
        //   • a context number — `Total: …` in normal mode (cumulative since region change,
        //     mirrors the boss-mode fight total) or `Fight: …` in boss mode (cumulative
        //     encounter total).  Both are read from totals that grow monotonically until
        //     reset by region change / mode flip / fight clear — never from the decaying
        //     60 s sliding window (that one feeds only the big-number fallback above).
        string modeTag;
        if (ownerEntityId == 0)
        {
            modeTag = "locating you…";
        }
        else if (bossOnlyMode)
        {
            // Boss mode: lead with the fight summary so the user always sees the
            // encounter total they care about; secondary tag describes whether the
            // big number above is live or rolling-avg.
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
            // 60 s window may have drained (totalDamage60s == 0) but the cumulative
            // session total is still meaningful — show it with the right "why is the
            // big number not live" tag.  Prefer "60s avg" while the rolling window is
            // still feeding the displayDps fallback; fall through to "idle" once both
            // the 5 s and 60 s windows are empty.
            modeTag = totalDamage60s > 0
                ? $"60s avg · Total: {FormatTotal(totalDamageSession)}"
                : $"idle · Total: {FormatTotal(totalDamageSession)}";
        else
            modeTag = "idle · waiting for damage";
        DetailText.Text = modeTag;

        RenderTopHeroes(topHeroes);
    }

    // Cached row references so we can iterate without reflection and keep the fast path
    // allocation-free during the 4 Hz decay tick.  Arrays are index-aligned — the i-th slot is
    // _topHeroBars[i] (fill), _topHeroImages[i] (portrait), _topHeroRows[i] (name / left text),
    // _topHeroTotals[i] (compact damage total), _topHeroPct[i] (percent, right-aligned column).
    private TextBlock[]? _topHeroRows;
    private TextBlock[]? _topHeroTotals;
    private TextBlock[]? _topHeroPct;
    private Image[]?     _topHeroImages;
    private Rectangle[]? _topHeroBars;

    // Pixel width of the bar track — matches the row Grid's Width in XAML.  Kept here rather
    // than read from ActualWidth so we get the correct value on the first render tick before
    // layout has measured anything (ActualWidth is 0 until the window is visible).
    private const double BarTrackWidthPx = 250.0;

    private void RenderTopHeroes(IReadOnlyList<DpsMeterClass.HeroShareEntry>? rows)
    {
        _topHeroRows   ??= new[] { Top1Text,  Top2Text,  Top3Text,  Top4Text,  Top5Text };
        _topHeroTotals ??= new[] { Top1Total, Top2Total, Top3Total, Top4Total, Top5Total };
        _topHeroPct    ??= new[] { Top1Pct,   Top2Pct,   Top3Pct,   Top4Pct,   Top5Pct   };
        _topHeroImages ??= new[] { Top1Image, Top2Image, Top3Image, Top4Image, Top5Image };
        _topHeroBars   ??= new[] { Top1Bar,   Top2Bar,   Top3Bar,   Top4Bar,   Top5Bar   };

        // Normalise bar widths against the top row's share (WoW-Details / Recount style): the
        // leader is ALWAYS drawn as 100% of the track so the bars are visually useful even when
        // the party is large and absolute shares are small (e.g. 5 players clustered around 20%
        // would otherwise render as five stubby 1/5-long bars).  Everyone else is measured
        // relative to that max — a 29% leader paired with a 24% #2 draws as a full track and a
        // ~83% filled track respectively.  Guard against zero so divide never NaNs.
        double maxPercent = 0.0;
        if (rows != null)
            foreach (var r in rows)
                if (r.Percent > maxPercent) maxPercent = r.Percent;
        if (maxPercent <= 0.01) maxPercent = 1.0;

        // "Self" rows use the same warm orange as the main DPS number; other heroes render in a
        // muted white so the player can find themselves in the list at a glance. Brushes cached
        // locally (not on the window) because XAML resource lookup would cost more than the
        // single-byte allocation of `SolidColorBrush`. Frozen so WPF can share them across
        // threads without copy-on-write locking.
        var selfBrush  = _selfBrush  ??= FreezeBrush(new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xB3, 0x47)));
        var peerBrush  = _peerBrush  ??= FreezeBrush(new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF)));

        // Bar fills use distinct hues — warm red for "you", cool blue for peers — so the
        // player's own row reads instantly at a glance even in a crowded AOI.  Alpha is kept
        // moderate (~60% for self, ~55% for peers) so the portrait and text remain legible on
        // top.  Frozen for same reason as above (cross-thread brush reuse without COW locks).
        var selfBarBrush = _selfBarBrush ??= FreezeBrush(new SolidColorBrush(Color.FromArgb(0x99, 0xD6, 0x2A, 0x2A)));
        var peerBarBrush = _peerBarBrush ??= FreezeBrush(new SolidColorBrush(Color.FromArgb(0x88, 0x3D, 0x8F, 0xD9)));

        for (int i = 0; i < _topHeroRows.Length; i++)
        {
            var tb  = _topHeroRows[i];
            var tot = _topHeroTotals[i];
            var pct = _topHeroPct[i];
            var img = _topHeroImages[i];
            var bar = _topHeroBars[i];
            if (rows != null && i < rows.Count)
            {
                var row = rows[i];

                // Costume portrait replaces the textual hero name when we have one; otherwise the
                // row falls back to text-only (hero / nick in the middle column, percent in its own
                // right column — Details-style).  We hide the Image entirely (Collapsed, not just a
                // null source) when there's no portrait — a 0-width slot keeps the visible layout
                // aligned around the actual content instead of against a phantom indent column.
                var portrait = HeroAvatarImages.TryGet(row.Name);
                bool hasPortrait = portrait != null;
                img.Source     = portrait;
                img.Visibility = hasPortrait ? Visibility.Visible : Visibility.Collapsed;

                // Middle column: name only (no colon).  Right column: "NN%" always flush right.
                //
                // Three signals to combine — portrait (hero glyph), hero name (text), and
                // nickname (player handle).  The self row may arrive with an EMPTY hero name
                // (mid-session attach where Channel-A entity-create was missed AND the user's
                // hero powers aren't in the static HeroPowers index — common for custom-server
                // heroes).  In that case the nickname is the only identifier we have, and
                // collapsing to "" would hide the user from their own leaderboard.
                bool hasNick = !string.IsNullOrEmpty(row.PlayerName);
                bool hasHero = !string.IsNullOrEmpty(row.Name);
                string nameText;
                if (hasPortrait)
                {
                    nameText = hasNick ? Truncate(row.PlayerName, 12)
                             : hasHero ? Truncate(row.Name, 12)
                             : (row.IsSelf ? "you" : "");
                }
                else
                {
                    if (hasHero && hasNick)
                        nameText = $"{Truncate(row.Name, 10)} {Truncate(row.PlayerName, 10)}";
                    else if (hasHero)
                        nameText = Truncate(row.Name, 14);
                    else if (hasNick)
                        nameText = Truncate(row.PlayerName, 14);
                    else
                        nameText = row.IsSelf ? "you" : "?";
                }

                tb.Text = nameText;
                // Compact per-row total wrapped in parens ("(11.0M)" / "(234.5k)" / "(812)") so
                // the visual hierarchy reads "name → percent" first with the absolute damage as
                // a secondary qualifier next to the percent.  Field name on the entry is
                // `Total60s` for historical reasons but actually carries the encounter total
                // when the row came from `GetTopHeroesByEncounterShare` — same units, different
                // window, so a single formatter handles both code paths.
                tot.Text = "(" + FormatRowTotalCompact(row.Total60s) + ")";
                pct.Text = $"{row.Percent:0}%";
                var fg = row.IsSelf ? selfBrush : peerBrush;
                var fw = row.IsSelf ? FontWeights.SemiBold : FontWeights.Normal;
                tb.Foreground = fg;
                tb.FontWeight = fw;
                tot.Foreground = fg;
                tot.FontWeight = fw;
                pct.Foreground = fg;
                pct.FontWeight = fw;

                // Fill proportional to THIS row's share of the leader's share (see maxPercent
                // normalisation above).  The leader always gets the full track, everyone else
                // scales down from there — matches the standard raid-meter convention and makes
                // differences between close-ranked players (e.g. 29% vs 24%) visible at a glance.
                double clamped = Math.Clamp(row.Percent, 0.0, 100.0);
                bar.Width = BarTrackWidthPx * (clamped / maxPercent);
                bar.Fill  = row.IsSelf ? selfBarBrush : peerBarBrush;
                bar.Visibility = bar.Width > 0.5 ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                // Keep the slot laid out (fixed overlay size) but visually inert.
                tb.Text = "";
                tot.Text = "";
                pct.Text = "";
                tb.Foreground = peerBrush;
                tb.FontWeight = FontWeights.Normal;
                tot.Foreground = peerBrush;
                tot.FontWeight = FontWeights.Normal;
                pct.Foreground = peerBrush;
                pct.FontWeight = FontWeights.Normal;
                img.Source = null;
                img.Visibility = Visibility.Collapsed;
                bar.Width = 0;
                bar.Visibility = Visibility.Collapsed;
            }
        }
    }

    private SolidColorBrush? _selfBrush;
    private SolidColorBrush? _peerBrush;
    private SolidColorBrush? _selfBarBrush;
    private SolidColorBrush? _peerBarBrush;

    private static SolidColorBrush FreezeBrush(SolidColorBrush b)
    {
        if (b.CanFreeze) b.Freeze();
        return b;
    }

    // Trim overly-long hero / nickname strings with a trailing ellipsis so the right-hand
    // percent column lines up predictably on every leaderboard row.  Cap is exclusive of the
    // ellipsis — `Truncate("Squirrel Girl", 10)` yields `"Squirrel …"`.
    private static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= max) return s;
        if (max <= 1) return "…";
        return s.Substring(0, max - 1) + "…";
    }

    // ── Click-through / non-activating bits ───────────────────────────────────────────────────
    // Same pattern as FastestRecordToastWindow / MapProgressOverlayWindow: WS_EX_NOACTIVATE so a
    // mouse click anywhere on the overlay doesn't steal focus from the game. We intentionally
    // DON'T set WS_EX_TRANSPARENT — the user needs to be able to drag this thing.

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;

        var exStyle = User32.GetWindowLongPtr(hwnd, User32.GWL_EXSTYLE);
        User32.SetWindowLongPtr(hwnd, User32.GWL_EXSTYLE, exStyle | User32.WS_EX_NOACTIVATE);

        if (HwndSource.FromHwnd(hwnd) is { } source)
            source.AddHook(WndProc);
    }

    private nint WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // WM_MOUSEACTIVATE with MA_NOACTIVATE = "process the click, but don't bring me to the
        // foreground" — letting the user drag us without yanking focus off the game HUD.
        if (msg == User32.WM_MOUSEACTIVATE)
        {
            handled = true;
            return User32.MA_NOACTIVATE;
        }
        return IntPtr.Zero;
    }

    // ── Drag + persist ────────────────────────────────────────────────────────────────────────

    private void RootBorder_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // DragMove throws if called on a right-click or when the window is minimized. Guarding
        // the button explicitly avoids that (ChangedButton is already filtered by the event
        // name, but belt-and-braces in case the XAML is ever retargeted).
        if (e.ChangedButton == MouseButton.Left)
        {
            try { DragMove(); } catch { /* already moving or window disposed */ }
        }
    }

    private void OnLocationChanged(object? sender, EventArgs e)
    {
        // Fire-and-forget save: cheap JSON write (< 50 bytes). Avoids throttling logic at the
        // cost of a handful of extra disk ops per drag — irrelevant on SSDs and harmless on HDDs.
        TrySavePosition();
    }

    private void OnClosingSavePosition(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        TrySavePosition();
    }

    private static string FormatDps(double dps)
    {
        // Human-friendly numeric formatter. Millions and higher collapse to "M" to keep the
        // overlay readable at large font sizes — a 7-digit DPS number would overflow the default
        // window width. Keep one decimal for sub-million values for a bit of precision.
        if (dps >= 1_000_000) return (dps / 1_000_000.0).ToString("0.00") + "M";
        if (dps >= 100_000)   return (dps / 1_000.0).ToString("0") + "k";
        if (dps >= 10_000)    return (dps / 1_000.0).ToString("0.0") + "k";
        return ((int)dps).ToString("N0");
    }

    private static string FormatTotal(long total)
    {
        if (total >= 1_000_000) return (total / 1_000_000.0).ToString("0.00") + "M";
        if (total >= 1_000)     return (total / 1_000.0).ToString("0.0") + "k";
        return total.ToString("N0");
    }

    /// <summary>Compact 1-decimal damage formatter for the leaderboard's per-row total column
    /// ("11.0M", "234.5k", "812").  Distinct from <see cref="FormatTotal"/> which uses 2 decimals
    /// in the megabyte tier — that's appropriate for the headline detail line where a single
    /// number gets the user's full attention, but in the leaderboard column the row needs to
    /// align cleanly next to the percent and 2 decimals would push past the 50 px MinWidth on
    /// 9-figure numbers ("123.45M" is 7 chars vs "123.4M" at 6).  Lowercase suffixes were the
    /// example the user gave ("11.0m") but staying uppercase keeps the leaderboard visually
    /// consistent with `60s:` / `Fight:` in the detail line above.</summary>
    private static string FormatRowTotalCompact(long total)
    {
        if (total >= 1_000_000) return (total / 1_000_000.0).ToString("0.0") + "M";
        if (total >= 1_000)     return (total / 1_000.0).ToString("0.0") + "k";
        return total.ToString("N0");
    }

    private void TrySavePosition()
    {
        _persistedSettings.Left = Left;
        _persistedSettings.Top = Top;
        _persistedSettings.BossDpsOnly = _bossOnlyMode;
        DpsOverlaySettingsFile.Save(_persistedSettings);
    }

    /// <summary>Show without stealing focus from the game (ShowActivated=false + Show).</summary>
    public void ShowWithoutActivating()
    {
        var prev = ShowActivated;
        ShowActivated = false;
        Show();
        ShowActivated = prev;
    }

    // ── Boss-only mode toggle ─────────────────────────────────────────────────────────────────
    // Exposed as an event so the window doesn't take a dependency on DpsMeter directly — the
    // presenter (DpsOverlayPresenter) owns the meter and relays the toggle.  Matches how the
    // rest of the overlay stays isolated from service-layer types.

    /// <summary>Fires when the user clicks the "Boss DPS only" context-menu item. The bool
    /// argument is the menu item's new checked state. Subscribed by
    /// <c>DpsOverlayPresenter</c> which propagates to <see cref="DpsMeter.BossOnlyMode"/>.</summary>
    public event Action<bool>? BossOnlyToggled;

    /// <summary>Local mirror of the meter's boss-only state so the title bar can render the
    /// "BOSS DPS" prefix without needing a reference back to the meter.  Updated both when the
    /// user toggles the menu item and when the presenter confirms an external state change
    /// (e.g. restoring persisted state on startup via <see cref="SetBossOnlyMode"/>).</summary>
    private bool _bossOnlyMode;

    /// <summary>When true, <see cref="BossOnlyMenuItem"/> IsChecked is being set from
    /// <see cref="UpdateDps"/> / <see cref="SetBossOnlyMode"/> — ignore Checked/Unchecked so we
    /// do not re-enter <see cref="BossOnlyToggled"/>.</summary>
    private bool _suppressBossOnlyMenuEvents;

    /// <summary>Presenter hook to sync the UI when the meter's mode is changed from elsewhere
    /// (e.g. loaded from settings). Refreshes the checkbox state AND the title-prefix mirror so
    /// the next <see cref="UpdateDps"/> repaint picks up the new label.</summary>
    public void SetBossOnlyMode(bool enabled)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(() => SetBossOnlyMode(enabled)));
            return;
        }
        _bossOnlyMode = enabled;
        _suppressBossOnlyMenuEvents = true;
        try { BossOnlyMenuItem.IsChecked = enabled; }
        finally { _suppressBossOnlyMenuEvents = false; }
    }

    private void BossOnlyMenuItem_OnChecked(object sender, RoutedEventArgs e)
    {
        if (_suppressBossOnlyMenuEvents) return;
        _bossOnlyMode = true;
        BossOnlyToggled?.Invoke(true);
        TrySavePosition();
    }

    private void BossOnlyMenuItem_OnUnchecked(object sender, RoutedEventArgs e)
    {
        if (_suppressBossOnlyMenuEvents) return;
        _bossOnlyMode = false;
        BossOnlyToggled?.Invoke(false);
        TrySavePosition();
    }

    /// <summary>
    /// Standalone build's only quit path: borderless / no-taskbar / no-close-button window means
    /// the user can't Alt-F4 visually, so the right-click menu's "Exit" item shuts the whole
    /// process down.  We call <see cref="Application.Shutdown()"/> rather than just
    /// <see cref="Window.Close"/> because the App's <c>ShutdownMode</c> is set to
    /// <c>OnExplicitShutdown</c> — closing only this window would leave a hidden dispatcher
    /// (and therefore the tray-less process) alive forever.
    /// </summary>
    private void ExitMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        TrySavePosition();
        Application.Current?.Shutdown();
    }
}
