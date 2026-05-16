using System.Collections.Generic;

namespace MarvelHeroes.DpsMeter.Services;

/// <summary>
/// Curated list of <c>PropertyEnum</c> ids the live-stats panel surfaces, plus how to render
/// each one (display label, value formatting, ordering).  This is "option A" of the live-stats
/// plan: we don't see absolute character-sheet numbers (those would require sniffing the
/// avatar's full PropertyCollection from <c>NetMessageEntityCreate</c>), but we DO see every
/// delta that buffs apply -- and summing those gives the practical answer to "is my proc
/// firing right now / how stacked is my rotation".
///
/// <para>Enum ids cross-referenced from MHServerEmu 1.0.1's
/// <c>MHServerEmu.Games.Properties.PropertyEnum</c>.  If you ship against a different
/// Calligraphy.sip version, regenerate from <c>ilspycmd MHServerEmu.Games.dll -t
/// MHServerEmu.Games.Properties.PropertyEnum</c>.  The numeric values shouldn't shift across
/// builds (PropertyEnum is a stable on-disk format), but it's worth re-checking after a
/// server upgrade.</para>
///
/// <para>Curation principle: surface the stats a player would actively monitor during a fight
/// (rotation buffs, gear procs, defensive buffs).  Hide the long-tail per-keyword breakdowns
/// (<c>DamagePctBonusForPowerKeyword</c> etc) because they appear in the buff property dump
/// but rarely on UI-visible buffs and would clutter the panel.  Verbose-diagnostic users who
/// want everything can still read the raw dump.</para>
/// </summary>
internal static class BuffStatCatalog
{
    /// <summary>How to format a stat's summed value into a UI-ready string.</summary>
    public enum FormatKind
    {
        /// <summary>Multiply by 100 and append "%".  Use for ratio properties where 0.40 means
        /// "+40%" -- e.g. <c>DamagePctBonus</c>, <c>CritChancePctAdd</c>.</summary>
        PercentRatio,

        /// <summary>Integer-looking display, e.g. "+1247".  Use for the "rating" properties
        /// (DamageRating, CritRatingBonusAdd, SuperCritRatingBonusAdd) -- they're typed
        /// <c>Real</c> on the server but hold values that look like integers (448.0, 1247.0,
        /// etc), so we read <c>FloatValue</c> off the wire and round for display.  Reading
        /// <c>IntValue</c> for these would give billions of garbage from interpreting the
        /// IEEE-754 bit pattern as an integer.</summary>
        Integer,
    }

    /// <summary>One catalog entry: enum id + how to label/format it.</summary>
    public sealed class Entry
    {
        public required uint       PropertyEnum { get; init; }
        public required string     Label        { get; init; }
        public required FormatKind Format       { get; init; }
        /// <summary>Sort key.  Lower numbers render first.  Keep the headline-stat block (damage,
        /// crit, brutal) tight at the top; secondary stats below.</summary>
        public int Order { get; init; }
    }

    /// <summary>Curated PropertyEnum -> display metadata.  Order matters: the panel renders in
    /// <see cref="Entry.Order"/> order so the most important stats are at the top.</summary>
    public static readonly IReadOnlyList<Entry> Entries = new Entry[]
    {
        // ── HEADLINE OFFENSIVE STATS ─────────────────────────────────────────────────────
        // The four numbers a player tracks on every cooldown rotation: am I "+%damage'd up",
        // is my crit-chance proc up, is my brutal proc up.  These are the ones that drive
        // "press cooldowns NOW" decisions.
        new Entry { PropertyEnum = 283, Label = "Damage",          Format = FormatKind.PercentRatio, Order =  10 },  // DamagePctBonus
        new Entry { PropertyEnum = 223, Label = "Crit Chance",     Format = FormatKind.PercentRatio, Order =  20 },  // CritChancePctAdd
        new Entry { PropertyEnum = 916, Label = "Brutal Chance",   Format = FormatKind.PercentRatio, Order =  30 },  // SuperCritChancePctAdd
        new Entry { PropertyEnum = 225, Label = "Crit Damage",     Format = FormatKind.PercentRatio, Order =  40 },  // CritDamageMult
        new Entry { PropertyEnum = 918, Label = "Brutal Damage",   Format = FormatKind.PercentRatio, Order =  50 },  // SuperCritDamageMult

        // ── RATING STATS ─────────────────────────────────────────────────────────────────
        // Raw rating values -- not percentages, but useful to see go up/down as gear procs
        // fire.  Integer formatting.
        new Entry { PropertyEnum = 308, Label = "Damage Rating",   Format = FormatKind.Integer,      Order = 100 },  // DamageRating
        new Entry { PropertyEnum = 228, Label = "Crit Rating",     Format = FormatKind.Integer,      Order = 110 },  // CritRatingBonusAdd
        new Entry { PropertyEnum = 921, Label = "Brutal Rating",   Format = FormatKind.Integer,      Order = 120 },  // SuperCritRatingBonusAdd

        // ── UTILITY / DEFENSIVE STATS ────────────────────────────────────────────────────
        // Less rotation-relevant but useful for "did my speed proc fire" / "did my defensive
        // cooldown trigger" awareness.
        new Entry { PropertyEnum = 667, Label = "Move Speed",      Format = FormatKind.PercentRatio, Order = 200 },  // MovementSpeedIncrPct
        new Entry { PropertyEnum = 344, Label = "Defense Pen",     Format = FormatKind.PercentRatio, Order = 210 },  // DefensePenetrationPct
        new Entry { PropertyEnum = 294, Label = "Damage Resist",   Format = FormatKind.PercentRatio, Order = 220 },  // DamagePctResist
        new Entry { PropertyEnum = 470, Label = "Healing Received",Format = FormatKind.PercentRatio, Order = 230 },  // HealingReceivedMult
    };

    /// <summary>Parallel uint array of every PropertyEnum we want to sum.  Cached so the
    /// per-tick aggregator doesn't allocate -- it just hands this list to
    /// <c>BuffTracker.GetActiveStatBreakdowns</c> and reads positional results back.
    ///
    /// <para>Every property in the catalog is float-typed on the wire (even the "rating"
    /// properties -- they're <c>Real</c> on the server but happen to hold integer-looking
    /// values).  So the aggregator always reads <see cref="BuffPropertyDelta.FloatValue"/>;
    /// we don't need a parallel "is integer" flags array.</para>
    /// </summary>
    public static readonly IReadOnlyList<uint> PropertyEnumsToSum = BuildEnumList();

    private static uint[] BuildEnumList()
    {
        var arr = new uint[Entries.Count];
        for (int i = 0; i < Entries.Count; i++) arr[i] = Entries[i].PropertyEnum;
        return arr;
    }

    /// <summary>Format a summed value into the user-facing string.  Returns empty string
    /// when the value is effectively zero so the panel can skip rendering that row instead
    /// of showing meaningless "+0%" / "+0" rows.  Threshold is generous (0.0005 for ratio,
    /// 0.5 for integer) so float-rounding noise doesn't show up as ghost +0 entries.</summary>
    public static string FormatValue(double value, FormatKind kind)
    {
        switch (kind)
        {
            case FormatKind.PercentRatio:
                if (value > -0.0005 && value < 0.0005) return string.Empty;
                // Round to nearest 0.1% -- finer than that is noise (server values are typically
                // 0.05 / 0.10 / 0.40 etc, and float-rounding gives us 0.39999... that we don't
                // want to display as "39.999%").
                double pct = value * 100.0;
                return (pct >= 0 ? "+" : "") + pct.ToString("0.#") + "%";
            case FormatKind.Integer:
                if (value > -0.5 && value < 0.5) return string.Empty;
                long iv = (long)System.Math.Round(value);
                return (iv >= 0 ? "+" : "") + iv.ToString();
            default:
                return value.ToString("0.###");
        }
    }
}
