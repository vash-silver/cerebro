using System.Collections.Generic;
using MarvelHeroesComporator.NetworkSniffer;

namespace MarvelHeroes.DpsMeter.Services;

/// <summary>
/// Loot scanner "hunt mode" -- filter rules for highlighting drops that match a specific
/// gear-shopping goal.  When a loot scanner match also passes <see cref="MatchesHunt"/>,
/// the dashboard log emits a louder "HUNT MATCH" line with the matched-affix list so the
/// user can act on the drop while it's still on the ground.
///
/// <para>Configuration lives in <see cref="LootHuntConfig.Current"/> -- the user picks
/// affixes / min-hits / rarity in the Cosmic Loot Scanner tab and we read whatever's published
/// there at evaluation time.  Source no longer hardcodes any of the rules; they're all
/// user-tunable.</para>
///
/// <para><b>Server-agnostic via avatar-prototype match.</b>  We compare the dropped
/// item's <c>EquippableByEnumIndex</c> against the local avatar's prototype enum (both
/// from the same server's runtime), so server-merge-style enum reshuffles don't break
/// the "is this for MY hero" filter.</para>
/// </summary>
internal static class HuntCriteria
{
    /// <summary>Root-enum index of <c>Entity/Items/Rarity/R5Cosmic.prototype</c>.  Items
    /// rolled at lower rarities (Common / Rare / Epic) won't trigger the hunt match when
    /// the config requests Cosmic-only.  Note: this index comes from our local items.txt;
    /// if the user's server reshuffled rarity enums too, the Cosmic gate may need an
    /// override that we'd add to <see cref="LootHuntConfig"/> later.</summary>
    private const uint CosmicRarityEnum = 43721;

    /// <summary>Returns <c>true</c> when the parsed item spec matches every rule from
    /// <see cref="LootHuntConfig.Current"/>: enabled, optional self-equippable, configured
    /// rarity gate, and the affix list contains at least <c>MinHits</c> distinct matches
    /// from the user's <c>WantedPatterns</c> set.  Populates <paramref name="matchedAffixes"/>
    /// with a short symbolic-name list for the log line.</summary>
    public static bool MatchesHunt(
        MhMissionSniffer.LootItemSpec spec,
        IReadOnlyList<MhMissionSniffer.LootAffixSpec> affixes,
        uint selfPrototypeIndex,
        out IReadOnlyList<string> matchedAffixes)
    {
        var cfg = LootHuntConfig.Current;
        matchedAffixes = System.Array.Empty<string>();

        // Master enable: when the user has turned hunt mode off entirely, skip without
        // doing any per-affix comparison.
        if (!cfg.Enabled) return false;

        // No wanted patterns = the user hasn't configured anything yet.  Don't fire on
        // every drop -- silent until they pick something.
        if (cfg.WantedPatterns.Count == 0) return false;

        // Self-only gate: the item must be equippable by the local hero.  Self-proto = 0
        // means we don't know who "self" is yet (login handshake pending, avatar not
        // identified) so we conservatively don't fire either.
        if (cfg.SelfOnly)
        {
            if (selfPrototypeIndex == 0 || spec.EquippableByEnumIndex != selfPrototypeIndex)
                return false;
        }

        // Rarity gate.
        switch (cfg.Rarity)
        {
            case LootHuntConfig.RarityGate.CosmicOnly:
                if (spec.RarityProtoEnumIndex != CosmicRarityEnum) return false;
                break;
            case LootHuntConfig.RarityGate.Any:
                // no rarity restriction
                break;
        }

        // Walk the affix list, collect wanted matches.  Each pattern counts at most once
        // (so two CritRating affixes from different tiers register as one hit, not two --
        // they're the same stat type from the user's perspective).
        var matches = new List<string>();
        var seenPatterns = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < affixes.Count; i++)
        {
            string? affixPath = AffixNames.GetPath(affixes[i].AffixProtoEnumIndex);
            if (string.IsNullOrEmpty(affixPath)) continue;

            foreach (string pattern in cfg.WantedPatterns)
            {
                if (seenPatterns.Contains(pattern)) continue;
                if (affixPath.IndexOf(pattern, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    matches.Add(AffixTierCatalog.ShortName(affixPath));
                    seenPatterns.Add(pattern);
                    break;
                }
            }
        }

        matchedAffixes = matches;
        return matches.Count >= cfg.MinHits;
    }
}
