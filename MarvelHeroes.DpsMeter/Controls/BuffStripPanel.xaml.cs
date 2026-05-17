using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MarvelHeroes.DpsMeter.Services;

namespace MarvelHeroes.DpsMeter.Controls;

/// <summary>
/// Two-tier buff display strip used by the Live dashboard.  Top row holds player-facing
/// timed buffs (Empowered, summon timers, healing potions); bottom row holds gear-fired
/// procs (Art / Unique / Insignia / Runeword / Leg / Core).  See
/// <see cref="BuffDisplayClassifier"/> for the category rules and <see cref="ChipVm"/>
/// for the per-chip data shape.
///
/// <para><b>Refresh model:</b> the host (<c>DpsOverlayPresenter</c>) calls
/// <see cref="UpdateBuffs"/> on every decay tick (4 Hz) with a fresh snapshot of
/// <see cref="BuffTracker.GetActiveBuffs"/>.  We classify + dedupe + sort, then rebuild
/// the two <see cref="ItemsControl"/> ItemsSources from scratch.  At typical chip counts
/// (~5-15 player-facing, ~5-15 procs) the layout cost is negligible compared to the
/// avoided complexity of <c>INotifyPropertyChanged</c>-per-countdown.</para>
/// </summary>
public partial class BuffStripPanel : UserControl
{
    public BuffStripPanel()
    {
        InitializeComponent();
    }

    /// <summary>Replace the current chip set with a fresh classification of the supplied
    /// buffs.  Cheap; safe to call every UI tick.</summary>
    /// <param name="active">Snapshot from <see cref="BuffTracker.GetActiveBuffs"/>.  May
    /// be empty (no buffs active) in which case both rows hide themselves.</param>
    /// <param name="nowUtc">Used to compute remaining-time labels for each chip.  Passed
    /// in (rather than reading <c>DateTime.UtcNow</c> internally) so all chips in a single
    /// update see the same reference time -- avoids the chip on the right showing a
    /// fractionally-different countdown than the chip on the left from clock-read skew.</param>
    public void UpdateBuffs(IReadOnlyList<ActiveBuff> active, DateTime nowUtc)
    {
        // Derived state pill (Stealth / Invisible).  See class doc on BuffTracker
        // .TryGetStealthState; we replicate the same walk here so the chip strip can
        // render it inline without holding a reference to the live BuffTracker (the
        // snapshot we already have is enough information).  Cheap: one pass over the
        // active list, typically <20 entries.
        UpdateStealthStatePill(active);

        var playerFacing = new List<ChipVm>();
        var itemProc     = new List<ChipVm>();

        // Watchlist filter: when the user has opted into "Only show tracked buffs", we
        // skip any buff whose short name isn't on their list.  Two-stage check so the
        // common case (filter disabled) costs one bool read per UpdateBuffs call -- no
        // per-buff set lookup or string normalisation in the hot path.  When the filter
        // IS on with an empty watchlist we treat it as "show nothing" so toggling the
        // master switch can't silently re-enable noise the user was trying to suppress.
        var trackedCfg     = TrackedBuffsConfig.Current;
        bool filterActive  = trackedCfg.OnlyShowTracked;
        var trackedSet     = filterActive ? trackedCfg.Tracked : null;

        // Group by display name so multi-stack buffs (same name, multiple condIds) collapse
        // to one chip.  Keep the LONGEST remaining time as the representative -- when the
        // oldest stack falls off, the count drops but the displayed countdown keeps ticking
        // smoothly from the most-recent stack.  Showing shortest would cause a jarring
        // jump-up in the time display every time a stack expired.
        var playerFacingGroups = new Dictionary<string, ChipBuilder>();
        var itemProcGroups     = new Dictionary<string, ChipBuilder>();

        foreach (var buff in active)
        {
            var cat = BuffDisplayClassifier.Classify(buff);
            if (cat == BuffDisplayClassifier.Category.Hidden) continue;

            var target = cat == BuffDisplayClassifier.Category.PlayerFacing
                ? playerFacingGroups
                : itemProcGroups;

            string shortName = BuffDisplayClassifier.ShortenForChip(buff.DisplayName);

            // Watchlist gate.  Filter by the chip-short name -- same string the user
            // clicked "Track" on in the Buff Tracker tab, so the mental model is
            // consistent: "I told it to track Empowered, it shows me Empowered".
            if (filterActive && !trackedSet!.Contains(shortName)) continue;
            if (target.TryGetValue(shortName, out var existing))
            {
                existing.Count++;
                // Track the LONGEST-remaining stack for the countdown.  buff.ExpiresUtc is
                // null for permanent buffs but those got filtered out by Classify already.
                if (buff.ExpiresUtc.HasValue && buff.ExpiresUtc.Value > existing.LatestExpiry)
                    existing.LatestExpiry = buff.ExpiresUtc.Value;
            }
            else
            {
                target[shortName] = new ChipBuilder
                {
                    ShortName     = shortName,
                    Count         = 1,
                    LatestExpiry  = buff.ExpiresUtc ?? DateTime.MaxValue,
                };
            }
        }

        // Build VMs from each bucket; sort by closest expiry so the most-time-pressured
        // buff sits on the left.  Stable secondary sort by name keeps the visual order
        // consistent across ticks for stacks that share an expiry.
        foreach (var b in playerFacingGroups.Values) playerFacing.Add(BuildVm(b, nowUtc));
        foreach (var b in itemProcGroups.Values)     itemProc.Add(BuildVm(b, nowUtc));

        playerFacing.Sort(CompareChips);
        itemProc.Sort(CompareChips);

        PlayerFacingRow.ItemsSource = playerFacing;
        ItemProcRow.ItemsSource     = itemProc;

        // Collapse the rows entirely when they're empty -- the StackPanel margin between
        // the rows would still show as dead pixels otherwise.
        PlayerFacingRow.Visibility = playerFacing.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        ItemProcRow.Visibility     = itemProc.Count     > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    // PropertyEnum constants used to derive the stealth/invisible state from active-buff
    // property deltas.  Mirrors the same constants in BuffTracker; kept inline here so
    // the panel doesn't reach back into the tracker's internals for this purely-display
    // computation.
    private const uint PropertyEnumStealth = 899u;
    private const uint PropertyEnumVisible = 993u;

    /// <summary>Walk the active-buffs snapshot looking for property deltas that grant
    /// stealth or invisibility, and update the state-pill visibility / label accordingly.
    /// Also captures the contributing buff names into a tooltip so the user can see WHY
    /// they're stealthed -- useful when multiple sources could be active simultaneously
    /// (Nightcrawler teleport stealth + an artifact proc, say).</summary>
    private void UpdateStealthStatePill(IReadOnlyList<ActiveBuff> active)
    {
        // Master gate: the user can disable the pill entirely from the Buff Tracker tab.
        // Defaults off so heroes whose talents don't care about stealth state don't get
        // dead pixels above the strip.
        if (!TrackedBuffsConfig.Current.ShowStealthStatePill)
        {
            StealthStatePill.Visibility = Visibility.Collapsed;
            return;
        }

        bool stealth = false, invisible = false;
        List<string>? sources = null;
        for (int i = 0; i < active.Count; i++)
        {
            var deltas = active[i].PropertyDeltas;
            for (int j = 0; j < deltas.Count; j++)
            {
                var d = deltas[j];
                bool matched = false;
                if      (d.PropertyEnum == PropertyEnumStealth && d.RawValueBits != 0) { stealth   = true; matched = true; }
                else if (d.PropertyEnum == PropertyEnumVisible && d.RawValueBits == 0) { invisible = true; matched = true; }
                if (matched)
                {
                    sources ??= new List<string>(2);
                    var name = active[i].DisplayName;
                    if (!sources.Contains(name)) sources.Add(name);
                }
            }
        }

        if (!stealth && !invisible)
        {
            StealthStatePill.Visibility = Visibility.Collapsed;
            return;
        }

        StealthStateLabel.Text = stealth && invisible
            ? "Stealthed + Invisible"
            : stealth ? "Stealthed" : "Invisible";
        StealthStatePill.Visibility = Visibility.Visible;
        StealthStatePill.ToolTip = sources is { Count: > 0 }
            ? $"Active sources: {string.Join(", ", sources)}"
            : "A talent / proc has stealthed or invisible-d you.";
    }

    private static int CompareChips(ChipVm a, ChipVm b)
    {
        int t = a.RemainingSeconds.CompareTo(b.RemainingSeconds);
        return t != 0 ? t : string.CompareOrdinal(a.ShortName, b.ShortName);
    }

    private static ChipVm BuildVm(ChipBuilder b, DateTime nowUtc)
    {
        double remainingSec = (b.LatestExpiry - nowUtc).TotalSeconds;
        if (remainingSec < 0) remainingSec = 0;
        // Resolve the user-configured icon (if any) from the watchlist's IconPaths map.
        // We re-decode on every UpdateBuffs call rather than caching across ticks: at the
        // chip counts we expect (under ~30 chips total even in heavy combat) the cost is
        // sub-millisecond, and we avoid the staleness trap of an LRU cache that doesn't
        // know when the user picked a new file.
        var icon = LoadChipIcon(b.ShortName);
        // Apply the user's display-name alias (if any) so e.g. "Teleport Stealth Combo"
        // renders as "Stealth" when the user has renamed it from the Buff Tracker tab.
        // Grouping still happens by the original short name above, so multi-stack
        // aggregation and the IconPaths / Aliases lookups all key off the same value.
        string displayName = TrackedBuffsConfig.Current.GetDisplayName(b.ShortName);
        return new ChipVm
        {
            ShortName        = displayName,
            StackSuffix      = b.Count > 1 ? $"x{b.Count}" : "",
            StackVisibility  = b.Count > 1 ? Visibility.Visible : Visibility.Collapsed,
            RemainingText    = FormatRemaining(remainingSec),
            RemainingSeconds = remainingSec,
            IconImageSource  = icon,
            IconVisibility   = icon != null ? Visibility.Visible : Visibility.Collapsed,
        };
    }

    /// <summary>Decode the optional user-configured icon for a given chip short-name.
    /// Returns <c>null</c> when no icon is set, when the configured file is missing, or
    /// when decode fails -- in all cases the chip falls back to text-only rendering.
    /// Uses <c>CacheOption.OnLoad</c> so the source file isn't held open between calls.</summary>
    private static BitmapImage? LoadChipIcon(string shortName)
    {
        var path = TrackedBuffsConfig.Current.GetIconPath(shortName);
        if (string.IsNullOrWhiteSpace(path)) return null;
        try
        {
            // Two flavors: pack:// URIs for auto-suggested in-game icons (bundled WPF
            // Resources), or absolute file paths for user-picked custom images.  Only
            // file-path entries get a File.Exists check; pack URIs are resolved by WPF
            // against the embedded resource bundle and we let the decode fail naturally
            // if the resource is missing (caught by the surrounding try/catch).
            bool isPackUri = path.StartsWith("pack://", StringComparison.OrdinalIgnoreCase);
            if (!isPackUri && !File.Exists(path)) return null;

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption      = BitmapCacheOption.OnLoad;
            bmp.CreateOptions    = BitmapCreateOptions.IgnoreColorProfile;
            bmp.UriSource        = new Uri(path, UriKind.Absolute);
            // Decode at a generous chip-sized resolution.  The XAML Image element will
            // letterbox to 24px / 18px as needed -- doing the heavy lifting at decode time
            // means subsequent layout passes are cheap.
            bmp.DecodePixelWidth = 64;
            bmp.EndInit();
            if (bmp.CanFreeze) bmp.Freeze();
            return bmp;
        }
        catch { return null; }
    }

    private static string FormatRemaining(double sec)
    {
        // Sub-second precision when close to expiry so the countdown feels live.  Past
        // 10s we drop the decimal -- a "12s" reading is easier to skim than "12.4s".
        if (sec < 10.0) return $"{sec:0.0}s";
        return $"{sec:0}s";
    }

    /// <summary>Mutable accumulator used while bucketing buffs by name.  Not a binding
    /// source itself; we materialize to <see cref="ChipVm"/> at the end.</summary>
    private sealed class ChipBuilder
    {
        public required string ShortName { get; init; }
        public int Count { get; set; }
        public DateTime LatestExpiry { get; set; }
    }

    /// <summary>Per-chip data the DataTemplate binds against.  Keep this immutable / value-
    /// type-ish -- we rebuild the list on every tick, so any state we need to remember
    /// across ticks should live in <see cref="BuffTracker"/>, not here.</summary>
    public sealed class ChipVm
    {
        public required string ShortName { get; init; }
        /// <summary>Empty string when count is 1, "xN" otherwise.  Bound to a TextBlock that's
        /// only visible when count &gt; 1 (see <see cref="StackVisibility"/>).</summary>
        public required string StackSuffix { get; init; }
        public required Visibility StackVisibility { get; init; }
        public required string RemainingText { get; init; }
        /// <summary>Numeric remaining used for stable sort; not bound to UI directly.</summary>
        public required double RemainingSeconds { get; init; }

        /// <summary>Optional icon image for the chip, sourced from the user's watchlist
        /// IconPaths.  Null when no icon is configured or decode failed -- in either case
        /// the chip falls back to text-only rendering and <see cref="IconVisibility"/>
        /// stays Collapsed.</summary>
        public ImageSource? IconImageSource { get; init; }
        public Visibility IconVisibility { get; init; } = Visibility.Collapsed;
    }
}
