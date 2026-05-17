using System;
using System.Collections.Generic;

namespace MarvelHeroes.DpsMeter.Services;

/// <summary>
/// Curated set of affix-path substrings users can pick from in the Cosmic Loot Scanner tab to
/// configure their hunt criteria.  Each entry is a (display label, substring pattern,
/// optional category) tuple -- the label is what the checkbox shows, the pattern is what
/// <see cref="HuntCriteria"/> matches against each rolled affix's path.
///
/// <para><b>Why a curated list instead of free-text?</b>  The user shouldn't have to know
/// that "Brutal Strike Rating" maps to the substring "BrutalStrike" in MHServerEmu paths.
/// Pre-defining the friendly-to-internal mapping here keeps the UI approachable while
/// preserving full flexibility (we just add new entries here when something's missing).</para>
///
/// <para><b>Patterns mirror what's known to <see cref="AffixTierCatalog.Classify"/>.</b>
/// When the user wants "Critical Hit Rating", we match by substring "CritRating" -- same
/// rule the tier classifier uses for T1 categorization.  Stays consistent: an affix the
/// hunt cares about is also one the tier scorer rates highly.</para>
/// </summary>
internal static class AffixPatternCatalog
{
    public enum Category
    {
        Offensive,    // damage / crit / brutal -- the headline DPS stats
        Defensive,    // health, defense, resists
        Sustain,      // life-on-hit, healing, lifesteal
        Mobility,     // movement speed, dodge
        Attribute,    // hero attributes (Strength, Speed, Fighting, etc.)
        Specialized,  // niche / activity-specific stats
    }

    public sealed class Pattern
    {
        /// <summary>Human-readable label shown in the Cosmic Loot Scanner tab's checkbox list.</summary>
        public required string Label { get; init; }
        /// <summary>Primary substring that <see cref="HuntCriteria"/> looks for inside the
        /// affix prototype path.  Case-insensitive match; longer / more specific patterns
        /// win because the matcher short-circuits on first hit per affix.  Also the value
        /// persisted into <c>loot-hunt-config.json</c>'s <c>WantedPatterns</c> when the
        /// user ticks this row -- subsequent loads look up the pattern by primary
        /// substring to resolve <see cref="Aliases"/>.</summary>
        public required string Substring { get; init; }
        /// <summary>Optional alternate substrings that should also count as a match for
        /// this row.  Same stat appears under multiple server-internal names: e.g.
        /// "Critical Hit Rating" matches both <c>CritRating</c> (legendary affixes) and
        /// <c>CritChance</c> (modern cosmic affixes); "Brutal Strike Rating" matches
        /// <c>BrutalStrike</c>, <c>BrutalChance</c>, and <c>SuperCritChance</c> (all the
        /// same stat, named differently in different prototype lineages).  Empty by
        /// default -- only set for rows where we know multiple aliases exist on the
        /// wire.</summary>
        public IReadOnlyList<string> Aliases { get; init; } = Array.Empty<string>();
        /// <summary>Grouping for UI rendering -- lets us section the checkbox list by
        /// Offensive / Defensive / etc. instead of one flat 25-entry blob.</summary>
        public required Category Category { get; init; }
        /// <summary>One-line tooltip describing what this stat does in MH-O terms.</summary>
        public required string Description { get; init; }
    }

    /// <summary>Resolve a single tracked substring (the value persisted in
    /// <c>WantedPatterns</c>) to the full set of substrings that should count as a match
    /// for it.  Looks up the catalog entry whose <see cref="Pattern.Substring"/> equals
    /// the input and returns <c>{ Substring } ∪ Aliases</c>; falls back to returning the
    /// input alone when no catalog entry matches (user hand-edited the JSON to add a
    /// custom substring).  Case-insensitive lookup.
    ///
    /// <para>Allocates each call -- caller's expected usage is one call per pattern per
    /// hunt evaluation (rare, single-digit-per-second worst case during loot bursts), so
    /// keeping the helper simple beats caching.</para></summary>
    public static IReadOnlyList<string> ExpandSubstrings(string trackedSubstring)
    {
        for (int i = 0; i < Patterns.Count; i++)
        {
            var p = Patterns[i];
            if (!string.Equals(p.Substring, trackedSubstring, StringComparison.OrdinalIgnoreCase))
                continue;
            if (p.Aliases.Count == 0) return new[] { p.Substring };
            var combined = new string[1 + p.Aliases.Count];
            combined[0] = p.Substring;
            for (int j = 0; j < p.Aliases.Count; j++) combined[j + 1] = p.Aliases[j];
            return combined;
        }
        return new[] { trackedSubstring };
    }

    /// <summary>Full curated list, rendering order = list order so the Offensive block sits
    /// at the top of the UI.</summary>
    public static readonly IReadOnlyList<Pattern> Patterns = new Pattern[]
    {
        // ── Offensive headline stats ────────────────────────────────────────────────
        new() { Label = "Damage Rating",          Substring = "DamageRating",     Category = Category.Offensive,  Description = "+N raw damage (the big offensive stat)" },
        // CritRating + CritChance: same stat, different naming across prototype lineages.
        // Cosmic-tier and recent loot use "CritChance" (e.g. CritChanceT1, CritChanceMark4);
        // legendary / ring / older blessings use "CritRating" (e.g. RingAffixes/CritRating,
        // GungnirCritRating2).  Aliases make the single user-facing row catch both.
        new() { Label = "Critical Hit Rating",    Substring = "CritRating",       Aliases = new[] { "CritChance" },
                Category = Category.Offensive,  Description = "+N crit chance (more crits)" },
        new() { Label = "Critical Damage Rating", Substring = "CritDamage",       Category = Category.Offensive,  Description = "+N crit damage (bigger crits)" },
        // BrutalStrike / BrutalChance / SuperCritChance: three names for the same stat
        // (the "super-crit" / "brutal" mechanic).  SuperCritRating is the legacy/legendary
        // form, BrutalChance / BrutalStrike are the modern naming.  All four count toward
        // a "Brutal Strike Rating" hunt.
        new() { Label = "Brutal Strike Rating",   Substring = "BrutalStrike",     Aliases = new[] { "BrutalChance", "SuperCritChance", "SuperCritRating" },
                Category = Category.Offensive,  Description = "+N brutal-hit chance (more 'BRUTAL!' procs)" },
        // BrutalDamage + SuperCritDamage: same stat, different naming.  SuperCritDmg is
        // a third spelling we see in some legacy affix paths.
        new() { Label = "Brutal Damage Rating",   Substring = "BrutalDamage",     Aliases = new[] { "SuperCritDamage", "SuperCritDmg" },
                Category = Category.Offensive,  Description = "+N brutal damage (bigger brutal hits)" },
        new() { Label = "Attack Speed",           Substring = "AttackSpeed",      Category = Category.Offensive,  Description = "+N% attack speed (faster basic attacks)" },
        // Cosmic Might: the headline cosmic-armor damage multiplier (Entity/Items/Affixes/
        // ArmorAffixes/Loot20/CosmicAffixes/CosmicMight.prototype).  Matches the demon-
        // replacement variant and the legacy proc form (ProcCosmicMight) via the same
        // substring -- they're all the same stat conceptually.
        new() { Label = "Cosmic Might",           Substring = "CosmicMight",      Category = Category.Offensive,  Description = "+N% cosmic damage multiplier (best-in-slot offensive stat on cosmic armor)" },
        new() { Label = "Damage vs Bosses",       Substring = "VsBoss",           Category = Category.Offensive,  Description = "+N% damage against boss-rank enemies" },
        new() { Label = "Damage Penetration",     Substring = "Penetration",      Category = Category.Offensive,  Description = "+N% bypass enemy defense" },

        // ── Defensive ─────────────────────────────────────────────────────────────────
        new() { Label = "Max Health",             Substring = "HealthMax",        Category = Category.Defensive,  Description = "+N max HP" },
        new() { Label = "Damage Reduction",       Substring = "DamageReduction",  Category = Category.Defensive,  Description = "+N% incoming damage reduction" },
        new() { Label = "Defense Rating",         Substring = "Defense",          Category = Category.Defensive,  Description = "+N defense (mitigates incoming damage)" },
        new() { Label = "Dodge Rating",           Substring = "Dodge",            Category = Category.Defensive,  Description = "+N chance to dodge attacks" },
        new() { Label = "Tenacity",               Substring = "Tenacity",         Category = Category.Defensive,  Description = "+N% CC resist (stuns, slows, etc.)" },

        // ── Sustain ───────────────────────────────────────────────────────────────────
        new() { Label = "Health on Hit",          Substring = "HealthOnHit",      Category = Category.Sustain,    Description = "+N HP per hit you land" },
        new() { Label = "Healing Received",       Substring = "HealingReceived",  Category = Category.Sustain,    Description = "+N% bonus from incoming heals" },
        new() { Label = "Life Steal",             Substring = "LifeSteal",        Category = Category.Sustain,    Description = "+N% damage dealt returned as HP" },
        new() { Label = "Spirit on Hit",          Substring = "SpiritOnHit",      Category = Category.Sustain,    Description = "+N spirit (resource) per hit" },

        // ── Mobility ──────────────────────────────────────────────────────────────────
        new() { Label = "Movement Speed",         Substring = "MovementSpeed",    Category = Category.Mobility,   Description = "+N% out-of-combat / walking speed" },

        // ── Attributes (hero stats) ──────────────────────────────────────────────────
        new() { Label = "+Strength",              Substring = "Strength",         Category = Category.Attribute,  Description = "+N Strength (scales physical damage / certain powers)" },
        new() { Label = "+Speed",                 Substring = "/Speed",           Category = Category.Attribute,  Description = "+N Speed (scales movement / certain powers)" },
        new() { Label = "+Fighting",              Substring = "Fighting",         Category = Category.Attribute,  Description = "+N Fighting (scales melee / certain powers)" },
        new() { Label = "+Durability",            Substring = "Durability",       Category = Category.Attribute,  Description = "+N Durability (scales health / mitigation)" },
        new() { Label = "+Intelligence",          Substring = "Intelligence",     Category = Category.Attribute,  Description = "+N Intelligence (scales certain mental / tech powers)" },
        new() { Label = "+Energy Projection",     Substring = "Energy",           Category = Category.Attribute,  Description = "+N Energy Projection (scales ranged / certain powers)" },

        // ── Specialized ──────────────────────────────────────────────────────────────
        new() { Label = "Item Find",              Substring = "FindItem",         Category = Category.Specialized, Description = "+N% rare-item drop rate" },
        new() { Label = "Experience Find",        Substring = "FindExp",          Category = Category.Specialized, Description = "+N% XP gained" },
        new() { Label = "Credit Find",            Substring = "FindCredit",       Category = Category.Specialized, Description = "+N% credits dropped" },
    };
}
