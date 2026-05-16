using System.Text.RegularExpressions;

namespace MarvelHeroes.DpsMeter.Services;

/// <summary>
/// Heuristic classifier that decides how an <see cref="ActiveBuff"/> should be displayed
/// in the two-tier buff strip on the Live dashboard.  The <see cref="BuffTracker"/> stores
/// every buff the server applies to us -- that's typically 20-40 entries during heavy
/// combat (cosmic channeled abilities + a half-dozen artifact proc stacks each).  Showing
/// them all would dominate the dashboard, so the UI applies this classifier to surface
/// only the gameplay-meaningful subset and groups the rest by category.
///
/// <para>The classifier is intentionally name-pattern based right now -- we infer the
/// category from the prototype basename via <see cref="PowerNames"/> / <see cref="ConditionNames"/>.
/// A more reliable approach would parse the full prototype path (item powers live under
/// <c>Powers/ItemPowers/...</c>) but that requires capturing the path through the buff
/// pipeline, which is a bigger change.  Name-prefix matching is a decent first cut --
/// MHServerEmu's data naming convention is consistent enough that <c>Art\d+</c> always
/// means "artifact proc" and <c>Team Buff</c> always means "ability-applied group buff".</para>
/// </summary>
internal static class BuffDisplayClassifier
{
    public enum Category
    {
        /// <summary>Internal cosmetic / channel state / always-on passive.  Not surfaced in
        /// the buff strip; data layer still tracks them so future features (e.g. "DPS during
        /// channel" stats) can query.</summary>
        Hidden,

        /// <summary>Ability-applied timed buffs the player cares about per-rotation:
        /// Empowered, Unbreakable, Heal Medium Power, summon timers, etc.  Top row, larger
        /// chip, full readable styling.</summary>
        PlayerFacing,

        /// <summary>Gear-fired procs and stacks: artifact powers, unique-item passives,
        /// runeword procs, insignia effects.  Bottom row, smaller chip, slightly muted --
        /// these matter (gear is firing!) but they're "background" relative to the player's
        /// active rotation.</summary>
        ItemProc,
    }

    // ── Patterns ──────────────────────────────────────────────────────────────────────────────
    // Regex literals are anchored or word-bounded where it matters.  Compiled once at static
    // init so per-buff classification is cheap.

    /// <summary>Channel-state / cosmetic buffs the player has no use seeing in a buff strip.
    /// "Self Audio" is an audio-driver timer, "Stack Counter" tracks ability charges
    /// internally, "Phase\d (Loop|Start)" is channeled-ability state machine, "Stats Passive"
    /// churns every few seconds from stat recalcs.  Match against the full display name.</summary>
    private static readonly Regex s_internalPattern = new(
        @"\b(Self Audio|Stack Counter|Phase\d\s+Loop|Buff Phase\d|Stats Passive|^Tumble$)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Gear-source prefixes.  Names from the prototype data start with one of
    /// these followed by a digit (the artifact / unique / insignia number) or a space
    /// (like "Core Proc Invis").  Matched at start of string after optional whitespace.</summary>
    private static readonly Regex s_itemProcPattern = new(
        @"^(Art|Unique|Insignia|Runeword|Leg|Core)(\d|\s)",
        RegexOptions.Compiled);

    public static Category Classify(ActiveBuff buff)
    {
        // Permanent buffs are almost always either:
        //   - Always-on passives (Stats Passive, costume stat bonuses) -- hidden by design
        //   - Persistent auras tied to a held ability -- usually internal state markers
        // For the buff strip we only care about timed ones (durationMs > 0).  A user who
        // really wants to see always-on buffs can flip on Verbose Diagnostics and read the
        // log; the strip stays focused on "what's about to expire that affects my DPS".
        if (buff.IsPermanent) return Category.Hidden;

        // Internal / cosmetic / passive timed effects.  Sequence matters: check internal
        // before item-proc because some channel-internal buffs technically start with a
        // gear-style prefix (rare but possible).
        if (s_internalPattern.IsMatch(buff.DisplayName)) return Category.Hidden;

        // Gear-fired procs.  Item-source prefix at start of name.
        if (s_itemProcPattern.IsMatch(buff.DisplayName)) return Category.ItemProc;

        // Default: player-facing.  This catches Team Buff *, Heal *, Call *, summon timers,
        // and anything we haven't pattern-matched.  Erring on the side of "show it" --
        // false positives in the strip are easier to live with than missing the buff that
        // explains a damage spike.
        return Category.PlayerFacing;
    }

    /// <summary>Shorten a buff display name for chip display.  The raw names come from the
    /// prototype paths, which include a lot of namespace noise ("Team Buff Empowered5 Seconds"
    /// -- the "Team Buff" prefix and "5 Seconds" suffix are noise once the chip ALSO shows
    /// a countdown).  Heuristic cleanup keeps the chip readable at small font sizes.</summary>
    public static string ShortenForChip(string displayName)
    {
        string s = displayName;

        // Drop the "Team Buff " prefix -- the strip-style implies these are buffs, no need
        // to label.
        s = Regex.Replace(s, @"^Team Buff\s+", "");
        // Drop trailing duration markers like "5 Seconds" / "5 Second Combo" -- the chip's
        // countdown already shows the time.
        s = Regex.Replace(s, @"\s*\d+\s*Second\s*(Combo)?$", "", RegexOptions.IgnoreCase);
        // Drop trailing "Power" / "Proc" / "Effect" if the name has more than one word --
        // these are categorical suffixes that don't add information at chip-size.  Keep them
        // if removing would leave an empty / one-word name (e.g. "Heal Medium Power" ->
        // "Heal Medium", which loses meaning; better to leave as-is in that case).
        // Actually skip this -- the names like "Crit Proc Stacking" lose meaning without
        // "Proc".  Let the user see the full short form.

        s = s.Trim();
        return s.Length == 0 ? displayName : s;
    }
}
