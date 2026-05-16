using System;
using MarvelHeroesComporator.NetworkSniffer;

namespace MarvelHeroes.DpsMeter.Services;

/// <summary>
/// Recon-only verbose-gated diagnostic for rolled loot drops (Uniques + Artifacts).
/// Phase 1 of the loot-filter feature: confirm we can identify high-rarity gear on the wire
/// and dump their property collections, before we invest in building the affix-range table
/// and the roll-quality scorer.
///
/// <para><b>What this does:</b> subscribes to <see cref="MhMissionSniffer.EntityCreated"/>,
/// filters to non-avatar entities whose prototype index appears in
/// <see cref="RolledLootPrototypes"/>, and for each match emits a header line plus a full
/// <c>DumpPropertyCollection</c> walk.  Only fires when verbose diagnostics is enabled --
/// so a normal session has zero overhead and zero log clutter.</para>
///
/// <para><b>What we're hoping to verify from the dump:</b></para>
/// <list type="bullet">
///   <item>The unique-item enum index resolves correctly (path matches what the in-game
///         tooltip suggests for the item that just dropped).</item>
///   <item>The PropertyEnum entries in the collection match the affixes we see in the
///         tooltip (DamageRating, CritRating, attribute bonuses, etc.).</item>
///   <item>The float / int decode of each value matches the displayed numbers.</item>
///   <item>We can spot rarity from the prototype path alone, or whether we need a separate
///         <c>PropertyEnum.ItemRarity</c> lookup off the property collection.</item>
/// </list>
///
/// <para>Once this recon confirms the assumptions, we promote the logic into a proper
/// scoring service that ranks each drop and surfaces it in a UI panel.  This class can be
/// removed at that point -- it has no UI surface, just log lines.</para>
/// </summary>
public sealed class LootScannerDiagnostic : IDisposable
{
    private readonly MhMissionSniffer _sniffer;

    /// <summary>Diagnostic log sink.  Set by the host (presenter) to <c>AppendLog</c>.</summary>
    public Action<string>? Diagnostic { get; set; }

    /// <summary>Function returning <c>true</c> when verbose diagnostics is currently enabled.
    /// Plumbed in from <c>DpsOverlaySettingsFile.IsVerboseDiagnosticsEnabled</c>; we re-read
    /// each event so toggling verbose at runtime takes effect immediately without restart.</summary>
    public Func<bool> IsVerboseEnabled { get; set; } = () => false;

    /// <summary>Function returning the local avatar's prototype enum index, or <c>0</c>
    /// when not yet identified.  Plumbed in from <c>DpsMeter.LikelySelfPrototypeIndex</c>;
    /// used by hunt-mode filter to match items via <c>EquippableBy</c>.  Server-agnostic --
    /// works regardless of items.txt enum table drift (server merges etc.).</summary>
    public Func<uint> SelfPrototypeIndex { get; set; } = () => 0u;

    /// <summary>Fires (on the capture thread) when a drop passes the hunt criteria.  The
    /// presenter subscribes to play the alert sound -- it marshals to the UI dispatcher
    /// before invoking WPF's MediaPlayer because MediaPlayer is Freezable-and-dispatcher-
    /// bound.  Doing the dispatch from the subscriber side keeps this class free of WPF
    /// dependencies.</summary>
    public event EventHandler<HuntMatchEventArgs>? HuntMatched;

    public LootScannerDiagnostic(MhMissionSniffer sniffer)
    {
        _sniffer = sniffer;
        _sniffer.EntityCreated += OnEntityCreated;
    }

    private void OnEntityCreated(object? sender, EntityCreatedEvent ev)
    {
        // Avatars are never items -- filter cheap.
        if (ev.IsAvatar) return;

        // Previously we gated on RolledLootPrototypes.IsTracked here, but that filter
        // relied on items.txt enum indices matching the server's enum.  After a
        // server-merge-style enum reshuffle the indices stop matching -- the user's
        // real Nightcrawler bodysuit reads as "GreenGoblin Unique478" in our table, so
        // it gets dropped from the scanner entirely.  Drop the protoIdx filter; try to
        // parse ItemSpec on every non-avatar EntityCreate and let the parse pass/fail
        // serve as the "is this loot?" signal.  Mobs / missiles / etc fail the parse
        // (their archives have non-zero conditionCount / powerRecordCount / etc.) and
        // bail cheap, so we don't bloat the log with non-loot lines.

        // Parse the ItemSpec out of the archive (the section AFTER the property collection
        // where the rolled affix list lives).  On parse failure we surface the EXACT
        // failure-reason from the parser so we can tell apart non-zero sub-collections
        // (item has attached conditions / powers) from drift / overflow / exception cases.
        // The full property dump only fires under verbose mode -- voluminous (~12 lines
        // per item) but the only way to diagnose unknown formats.
        // Try to parse ItemSpec.  Failure here is the "this isn't loot" signal -- mobs
        // fail because of non-zero condition / power counts, missiles fail because of
        // truncated archives, etc.  Silent on failure (no log line) -- we'd otherwise
        // blast the log with one line per non-loot entity create, which is most of them.
        if (!MhMissionSniffer.TryParseItemSpec(ev.RawArchive, out var spec, out var failureReason))
            return;

        // Score by Tier-weighted affix-presence.  Each affix's path goes through the tier
        // classifier; weights summed give a raw score.  Normalize to 0-100 by dividing by
        // the max possible score for an N-affix item (every affix at T1=1.0 -> N points,
        // so 100% = "every slot rolled a T1 affix").  Score N -> max-possible would mean
        // 100; mixed -> lower.  This is Phase-1 "ship it" -- doesn't account for roll
        // quality, just for whether the rolled affixes are stats you actually care about.
        int affixCount = spec.AffixSpecs.Count;
        double rawScore = 0.0;
        int t1Count = 0, t2Count = 0, t3Count = 0, noneCount = 0;
        var details = new System.Text.StringBuilder();
        for (int i = 0; i < affixCount; i++)
        {
            var a       = spec.AffixSpecs[i];
            var aPath   = AffixNames.GetPath(a.AffixProtoEnumIndex);
            var tier    = AffixTierCatalog.Classify(aPath);
            var weight  = AffixTierCatalog.Weight(tier);
            rawScore   += weight;
            switch (tier)
            {
                case AffixTierCatalog.Tier.T1:   t1Count++;   break;
                case AffixTierCatalog.Tier.T2:   t2Count++;   break;
                case AffixTierCatalog.Tier.T3:   t3Count++;   break;
                default:                          noneCount++; break;
            }
            // Per-affix detail string for the log line.  Short symbolic name + tier tag.
            // Falls back to the raw enum index when the path isn't in our table (newer
            // affix the user has but our generated names.cs lacks).
            string shortName = AffixTierCatalog.ShortName(aPath) is var sn && sn != "?"
                ? sn
                : $"#{a.AffixProtoEnumIndex}";
            if (i > 0) details.Append(", ");
            details.Append($"{shortName}[{tier}]");
        }
        // Normalized 0-100 score.  When an item has no affixes (shouldn't happen but
        // guard the divide), score is 0.
        int score = affixCount > 0 ? (int)System.Math.Round(rawScore * 100.0 / affixCount) : 0;

        // Surface key wire-level fields directly (item/rarity/equippable proto-enum indices)
        // because the items.txt path lookup is unreliable across server-merge enum
        // reshuffles -- the resolved path can claim an item is "GreenGoblin Unique478"
        // when it's actually a Nightcrawler bodysuit.  The numeric protos are at least
        // consistent within a session, so they're the more useful debug signal.
        uint selfProto = SelfPrototypeIndex();
        bool isForSelf = selfProto != 0 && spec.EquippableByEnumIndex == selfProto;
        Diagnostic?.Invoke(
            $"LootScannerDiagnostic: + Loot drop entityId={ev.EntityId} score={score} "
          + $"IL={spec.ItemLevel} affixes={affixCount} "
          + $"(T1={t1Count} T2={t2Count} T3={t3Count} ?={noneCount}) "
          + $"itemProto={spec.ItemProtoEnumIndex} rarityProto={spec.RarityProtoEnumIndex} "
          + $"equippableBy={spec.EquippableByEnumIndex}{(isForSelf ? " [SELF]" : "")} "
          + $"-- [{details}]");

        // Hunt match: does this drop match the user's configured criteria?  Emits a louder
        // log line AND fires HuntMatched so the presenter can play an alert sound.  Avatar-
        // based identity match works regardless of how the server numbers its prototype
        // enums.
        if (HuntCriteria.MatchesHunt(spec, spec.AffixSpecs, selfProto, out var huntAffixes))
        {
            Diagnostic?.Invoke(
                $"LootScannerDiagnostic: *** HUNT MATCH *** entityId={ev.EntityId} "
              + $"IL={spec.ItemLevel} score={score} -- matched [{string.Join(", ", huntAffixes)}] "
              + $"itemProto={spec.ItemProtoEnumIndex} rarityProto={spec.RarityProtoEnumIndex}");
            HuntMatched?.Invoke(this, new HuntMatchEventArgs
            {
                EntityId             = ev.EntityId,
                ItemProtoEnumIndex   = spec.ItemProtoEnumIndex,
                RarityProtoEnumIndex = spec.RarityProtoEnumIndex,
                ItemLevel            = spec.ItemLevel,
                Score                = score,
                MatchedAffixes       = huntAffixes,
            });
        }
    }

    public void Dispose()
    {
        _sniffer.EntityCreated -= OnEntityCreated;
    }
}

/// <summary>Payload for <see cref="LootScannerDiagnostic.HuntMatched"/>.  Carries enough
/// data for the presenter to log / display the match without re-walking the archive.</summary>
public sealed class HuntMatchEventArgs : EventArgs
{
    public required ulong EntityId             { get; init; }
    public required uint  ItemProtoEnumIndex   { get; init; }
    public required uint  RarityProtoEnumIndex { get; init; }
    public required int   ItemLevel            { get; init; }
    public required int   Score                { get; init; }
    public required System.Collections.Generic.IReadOnlyList<string> MatchedAffixes { get; init; }
}
