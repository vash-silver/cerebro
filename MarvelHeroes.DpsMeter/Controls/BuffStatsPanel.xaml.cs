using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using MarvelHeroes.DpsMeter.Services;

namespace MarvelHeroes.DpsMeter.Controls;

/// <summary>
/// "Stats from active buffs" tile strip rendered above the buff strip on the Live dashboard.
/// Each tile shows one curated <c>PropertyEnum</c>'s summed contribution from every active
/// buff -- e.g. "+Damage +140%" when Overwatch (+40%) and Empowered (+100%) are both up.
///
/// <para>This is "option A" of the live-stats plan -- delta-from-baseline rather than the
/// absolute character-sheet number.  It answers "is my proc firing right now / am I in burst
/// window" without needing to sniff and mirror the avatar's full <c>PropertyCollection</c>
/// (which would be option B and a much bigger lift).</para>
///
/// <para>Data flow: host calls <see cref="UpdateStats"/> on each decay tick with the
/// <see cref="BuffTracker"/>.  We ask the tracker to sum the curated PropertyEnum set in a
/// single pass, format each non-zero result via <see cref="BuffStatCatalog.FormatValue"/>,
/// and rebuild the ItemsControl.  Zero-valued stats are filtered out so the strip stays
/// compact between procs.</para>
/// </summary>
public partial class BuffStatsPanel : UserControl
{
    public BuffStatsPanel()
    {
        InitializeComponent();
    }

    /// <summary>Rebuild the visible tile set from a fresh aggregate.  Cheap; safe to call
    /// every UI tick.  Pass <c>null</c> (or a tracker with no active buffs) to clear the
    /// strip.</summary>
    public void UpdateStats(BuffTracker? tracker)
    {
        // Empty / null tracker -> hide entirely.  We don't want the row's vertical space
        // taken when there's nothing to show.
        if (tracker is null || tracker.ActiveCount == 0)
        {
            StatTilesRow.ItemsSource = null;
            StatTilesRow.Visibility  = Visibility.Collapsed;
            return;
        }

        // Pull per-buff attribution for every catalog enum in a single pass over the active-
        // buffs list.  Reading breakdowns (not just sums) gives us the per-buff contributions
        // for the tooltip ("Damage +140% from: Overwatch +40, Empowered +100") at the same
        // cost as plain sums -- one nested loop pass over (buffs * deltas-per-buff).
        IReadOnlyList<(string SourceName, double Value)>[] breakdowns =
            tracker.GetActiveStatBreakdowns(BuffStatCatalog.PropertyEnumsToSum);

        // Build the tile list in catalog Order.  We construct then sort rather than relying
        // on Entries already being in order -- defensive against future catalog reshuffles.
        var tiles = new List<StatTileVm>(BuffStatCatalog.Entries.Count);
        for (int i = 0; i < BuffStatCatalog.Entries.Count; i++)
        {
            var e = BuffStatCatalog.Entries[i];
            var breakdown = breakdowns[i];

            // Sum on this side.  Breakdown lists are short (typically 1-3 contributors per
            // active stat), so the second pass is cheaper than allocating a sums[] array.
            double sum = 0.0;
            for (int k = 0; k < breakdown.Count; k++) sum += breakdown[k].Value;

            string valueText = BuffStatCatalog.FormatValue(sum, e.Format);
            // FormatValue returns "" for effectively-zero values -- hide the tile entirely
            // so the strip only shows stats the player can do something about right now.
            if (valueText.Length == 0) continue;

            tiles.Add(new StatTileVm
            {
                Order       = e.Order,
                Label       = e.Label,
                ValueText   = valueText,
                TooltipText = BuildTooltip(e, breakdown),
            });
        }
        tiles.Sort((a, b) => a.Order.CompareTo(b.Order));

        StatTilesRow.ItemsSource = tiles;
        StatTilesRow.Visibility  = tiles.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Compose the hover-tooltip text for one stat tile.  Lists each contributing
    /// buff and its individual contribution, formatted the same way as the tile's headline
    /// number so e.g. a 0.40 damage bonus from Overwatch reads as "Overwatch +40%" in the
    /// tooltip and "+40%" on its own.  Sorted descending by absolute value -- the biggest
    /// contributor reads first, so a glance at the top line of the tooltip immediately
    /// answers "which buff is doing the heavy lifting".</summary>
    private static string BuildTooltip(
        BuffStatCatalog.Entry entry,
        IReadOnlyList<(string SourceName, double Value)> breakdown)
    {
        if (breakdown.Count == 0) return entry.Label;

        // Stable copy + sort.  Don't mutate the input -- it's an IReadOnlyList and the
        // tracker still owns the underlying List<>.
        var sorted = new (string SourceName, double Value)[breakdown.Count];
        for (int i = 0; i < breakdown.Count; i++) sorted[i] = breakdown[i];
        System.Array.Sort(sorted, (a, b) =>
            System.Math.Abs(b.Value).CompareTo(System.Math.Abs(a.Value)));

        var sb = new System.Text.StringBuilder();
        sb.Append(entry.Label).Append(" from:");
        for (int i = 0; i < sorted.Length; i++)
        {
            sb.Append('\n');
            sb.Append("  ");
            sb.Append(sorted[i].SourceName);
            sb.Append("  ");
            sb.Append(BuffStatCatalog.FormatValue(sorted[i].Value, entry.Format));
        }
        return sb.ToString();
    }

    /// <summary>Per-tile data the DataTemplate binds against.  Rebuilt on every tick; do not
    /// add mutable state.</summary>
    public sealed class StatTileVm
    {
        public required int    Order       { get; init; }
        public required string Label       { get; init; }
        public required string ValueText   { get; init; }
        /// <summary>Multi-line "Damage from: ... " string shown when the user hovers a tile.
        /// Built from the per-buff breakdown so the user can see which active buff is
        /// contributing what to the headline number.</summary>
        public required string TooltipText { get; init; }
    }
}
