using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
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
        var playerFacing = new List<ChipVm>();
        var itemProc     = new List<ChipVm>();

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

    private static int CompareChips(ChipVm a, ChipVm b)
    {
        int t = a.RemainingSeconds.CompareTo(b.RemainingSeconds);
        return t != 0 ? t : string.CompareOrdinal(a.ShortName, b.ShortName);
    }

    private static ChipVm BuildVm(ChipBuilder b, DateTime nowUtc)
    {
        double remainingSec = (b.LatestExpiry - nowUtc).TotalSeconds;
        if (remainingSec < 0) remainingSec = 0;
        return new ChipVm
        {
            ShortName        = b.ShortName,
            StackSuffix      = b.Count > 1 ? $"x{b.Count}" : "",
            StackVisibility  = b.Count > 1 ? Visibility.Visible : Visibility.Collapsed,
            RemainingText    = FormatRemaining(remainingSec),
            RemainingSeconds = remainingSec,
        };
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
    }
}
