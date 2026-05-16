namespace MarvelHeroes.DpsMeter.Services;

/// <summary>
/// Classifies an affix prototype path into a "how much do we care about this affix" tier.
/// Used by the loot scorer to weight affix-presence-based item quality:
///
/// <code>
///   score = sum_over_affixes( tier.Weight ) * 100 / max_possible_score
/// </code>
///
/// <para>This is Phase 1 ("ship-it intermediate"): we score by AFFIX PRESENCE only, not by
/// roll quality (which would require replicating MH-O's seed -> value RNG).  An item with
/// three T1 affixes scores higher than one with two T1s and a T3, regardless of how each
/// rolled.  Roll-quality scoring is a Phase 2 add-on that uses the same affix list with an
/// extra value-derivation layer on top.</para>
///
/// <para><b>Tier weights</b> (user's tier list, tweakable here):</para>
/// <list type="bullet">
///   <item><c>T1 = 1.0</c> -- Damage Rating, Crit Rating, Brutal (SuperCrit) Rating,
///         +Attributes (Strength, Speed, Fighting, Durability, Intelligence, Energy).</item>
///   <item><c>T2 = 0.5</c> -- per-type damage (DamageMental, DamageEnergy, DamagePhysical),
///         Crit Damage Rating, Brutal Damage Rating, vs-target damage variants, per-keyword
///         damage bonuses, max endurance.</item>
///   <item><c>T3 = 0.2</c> -- Movement Speed, Dodge Rating, Health Max, Healing Received,
///         Resists, anti-CC.</item>
///   <item><c>None = 0.0</c> -- cosmetic VFX, deprecated affixes, anything we can't
///         classify (we'd rather not credit unknown affixes than over-credit them).</item>
/// </list>
///
/// <para>Classification is by case-insensitive substring match against the affix's full
/// prototype path.  Order matters in the matcher: more specific patterns first
/// (e.g. "MovementPowerDamageRating" must match T2 before the generic "DamageRating" T1
/// check) -- otherwise we'd misclassify damage-by-power-type affixes as pure DR.</para>
/// </summary>
internal static class AffixTierCatalog
{
    public enum Tier
    {
        None,   // weight 0
        T3,     // weight 0.2
        T2,     // weight 0.5
        T1,     // weight 1.0
    }

    public static double Weight(Tier tier) => tier switch
    {
        Tier.T1   => 1.0,
        Tier.T2   => 0.5,
        Tier.T3   => 0.2,
        _         => 0.0,
    };

    /// <summary>Classify an affix by its prototype path.  Returns <see cref="Tier.None"/>
    /// when the path is null/empty or doesn't match any known pattern.</summary>
    public static Tier Classify(string? path)
    {
        if (string.IsNullOrEmpty(path)) return Tier.None;

        // Discard / cosmetic categories first -- these paths can contain T1-looking tokens
        // (like "OnSuperCrit" in a VFX name) and we don't want them inflating scores.
        if (Contains(path, "BuiltInVFX"))         return Tier.None;
        if (Contains(path, "ZZZDeprecated"))      return Tier.None;
        if (Contains(path, "VisualOnly"))         return Tier.None;
        if (Contains(path, "DecorativeOnly"))     return Tier.None;

        // ── T2 SPECIFIC PATTERNS FIRST ────────────────────────────────────────────────
        // These match T1-looking tokens (Damage*, Crit*) so we have to short-circuit before
        // falling through to the generic T1 patterns.  Per-power-keyword damage modifiers
        // and per-type damage are useful but narrower than a pure +DamageRating roll, hence
        // T2 weight (0.5).
        if (Contains(path, "DamageMental"))       return Tier.T2;
        if (Contains(path, "DamageEnergy"))       return Tier.T2;
        if (Contains(path, "DamagePhysical"))     return Tier.T2;
        if (Contains(path, "DamageHybrid"))       return Tier.T2;
        if (Contains(path, "ForPowerKeyword"))    return Tier.T2;
        if (Contains(path, "VsBoss"))             return Tier.T2;
        if (Contains(path, "VsRank"))             return Tier.T2;
        if (Contains(path, "VsKeyword"))          return Tier.T2;
        if (Contains(path, "MovementPowerDamageRating")) return Tier.T2;  // narrow DR
        if (Contains(path, "CritDamage"))         return Tier.T2;
        if (Contains(path, "BrutalDamage"))       return Tier.T2;
        if (Contains(path, "SuperCritDamage"))    return Tier.T2;
        if (Contains(path, "MaxEndurance"))       return Tier.T2;
        if (Contains(path, "PenetrationPct"))     return Tier.T2;  // useful but situational

        // ── T1 HEADLINE STATS ─────────────────────────────────────────────────────────
        if (Contains(path, "DamageRating"))       return Tier.T1;   // generic DR
        if (Contains(path, "CritRating"))         return Tier.T1;
        if (Contains(path, "BrutalRating"))       return Tier.T1;
        if (Contains(path, "BrutalStrike"))       return Tier.T1;   // actual affix path name
                                                                     // for "+N Brutal Strike Rating";
                                                                     // the tooltip stat label
        if (Contains(path, "SuperCritRating"))    return Tier.T1;   // server-internal name

        // Attribute affixes -- watch out for path tokens that LOOK like an attribute name
        // but aren't (e.g. "EnergyDamage" already matched T2 above; "EnergyRegen" is HP
        // related).  Match on "/Attributes/" + specific names to avoid false positives.
        if (Contains(path, "/Attributes/"))       return Tier.T1;
        if (Contains(path, "UniqueAffixStrength"))    return Tier.T1;
        if (Contains(path, "UniqueAffixDurability"))  return Tier.T1;
        if (Contains(path, "UniqueAffixFighting"))    return Tier.T1;
        if (Contains(path, "UniqueAffixSpeed"))       return Tier.T1;
        if (Contains(path, "UniqueAffixIntelligence")) return Tier.T1;
        if (Contains(path, "UniqueAffixEnergy"))      return Tier.T1;
        if (Contains(path, "CosmicAttributes"))   return Tier.T1;
        if (Contains(path, "StatDurabilityBonus")) return Tier.T1;
        if (Contains(path, "StatStrengthBonus"))   return Tier.T1;
        if (Contains(path, "StatFightingBonus"))   return Tier.T1;
        if (Contains(path, "StatSpeedBonus"))      return Tier.T1;
        if (Contains(path, "StatIntelligenceBonus")) return Tier.T1;
        if (Contains(path, "StatEnergyBonus"))     return Tier.T1;

        // ── T3 UTILITY / DEFENSIVE STATS ───────────────────────────────────────────────
        if (Contains(path, "MovementSpeed"))      return Tier.T3;
        if (Contains(path, "MoveSpeed"))          return Tier.T3;
        if (Contains(path, "Dodge"))              return Tier.T3;
        if (Contains(path, "HealthMax"))          return Tier.T3;
        if (Contains(path, "HealingReceived"))    return Tier.T3;
        if (Contains(path, "DamageResist"))       return Tier.T3;
        if (Contains(path, "DamageReduction"))    return Tier.T3;
        if (Contains(path, "Defense"))            return Tier.T3;
        if (Contains(path, "ResistAll"))          return Tier.T3;
        if (Contains(path, "CCResist"))           return Tier.T3;
        if (Contains(path, "FindExp"))            return Tier.T3;
        if (Contains(path, "FindCredit"))         return Tier.T3;
        if (Contains(path, "FindItem"))           return Tier.T3;

        // Everything else -- unknown classification.  Logged as "None" so we can audit
        // which affix paths we haven't categorized yet and add patterns over time.
        return Tier.None;
    }

    private static bool Contains(string path, string needle)
        => path.IndexOf(needle, System.StringComparison.OrdinalIgnoreCase) >= 0;

    /// <summary>Short symbolic name extracted from an affix path's last segment, suitable
    /// for log lines.  E.g. <c>"DamageRatingT1.prototype"</c> -> <c>"DamageRatingT1"</c>.
    /// Falls back to "?" for null/empty paths.</summary>
    public static string ShortName(string? path)
    {
        if (string.IsNullOrEmpty(path)) return "?";
        int slash = path.LastIndexOf('/');
        string tail = slash >= 0 ? path[(slash + 1)..] : path;
        if (tail.EndsWith(".prototype", System.StringComparison.OrdinalIgnoreCase))
            tail = tail[..^".prototype".Length];
        return tail;
    }
}
