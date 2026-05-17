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

    /// <summary>Last <c>SelfPrototypeIndex()</c> value we observed; used to fire a one-shot
    /// "self-proto resolved: N" log line the first time the avatar identification flow
    /// completes (and on subsequent re-identifications after hero swap / zone change).
    /// Provides a glanceable "is the self filter even going to work" indicator without
    /// requiring the user to grep through dozens of "local avatar registered" / decay-tick
    /// logs to confirm the chain.</summary>
    private uint _lastSelfProtoSeen;

    /// <summary>Last <c>SelfHeroName()</c> value we observed; mirrors
    /// <see cref="_lastSelfProtoSeen"/> but for the hero-name path.  Used to fire one
    /// "self-state changed" diagnostic on each hero transition so the diagnostic log shows
    /// "I now know you are Nightcrawler" exactly once per pin event, not on every drop.</summary>
    private string? _lastSelfHeroSeen;

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

    /// <summary>Function returning the local avatar's entity id, or <c>0</c> when not yet
    /// pinned.  Plumbed from <c>DpsMeter.LikelySelfOwnerId</c>.  Surfaced separately from
    /// <see cref="SelfPrototypeIndex"/> so the diagnostic log can distinguish "meter never
    /// pinned self" (<c>SelfOwnerId == 0</c>) from "meter pinned self but proto cache
    /// missed" (<c>SelfOwnerId != 0 && SelfPrototypeIndex == 0</c>) -- the two failure
    /// modes need different fixes and the combined log lets us tell them apart at a
    /// glance.</summary>
    public Func<ulong> SelfOwnerId { get; set; } = () => 0uL;

    /// <summary>Function returning the local hero's basename (e.g. <c>"Nightcrawler"</c>),
    /// or empty when the hero hasn't been pinned yet.  Plumbed from
    /// <c>DpsMeter.LikelySelfHeroName</c>; this is the authoritative input to the SelfOnly
    /// filter's hero-name-vs-hero-name comparison.  See
    /// <see cref="AvatarNamesByProto"/> and <see cref="PowerHeroByProto"/> for the
    /// translation pipeline.</summary>
    public Func<string> SelfHeroName { get; set; } = () => string.Empty;

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

        // Skip items that live inside a container (inventory / equipped slot) instead of
        // sitting in the world.  Every time a peer player enters your AOI -- say, walking
        // into the HUB surrounded by other players -- the server sends an EntityCreate for
        // each of their equipped items + visible inventory so your client can render
        // costumes and tooltips.  These have the same archive layout as a fresh ground
        // drop, so they parse cleanly as ItemSpecs and would otherwise produce a flood of
        // "Loot drop" diagnostic lines and even fire phantom HUNT MATCH alerts on someone
        // else's gear.  LikelyInInventory uses the locomotion-flags=0 heuristic (items in
        // the world carry a position; items in containers don't) -- see the property doc
        // in MhMissionSniffer for the trade-offs.
        if (ev.LikelyInInventory)
        {
            // Verbose-mode visibility into the filter so a false-positive (real drop
            // suppressed) can be diagnosed by glancing at the log after a session.  Keep
            // the message short -- there can be thousands of these per zone load.
            if (IsVerboseEnabled())
            {
                Diagnostic?.Invoke(
                    $"LootScannerDiagnostic: skipped (in-inventory) entityId={ev.EntityId} "
                  + $"protoIdx={ev.PrototypeEnumIndex} "
                  + $"fieldFlags=0x{ev.RawFieldFlags:X} loco=0x{ev.RawLocoFieldFlags:X}");
            }
            return;
        }

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
        //
        // Gated on verbose-diagnostics: in normal mode this would fire on EVERY non-avatar
        // EntityCreate that parses cleanly as an item -- including equipped gear / inventory
        // contents the server replicates when peer avatars enter your AOI (every player you
        // see in the HUB ships ~20 items worth of wire traffic).  HUNT MATCH below stays
        // ungated so the actual user-facing alert still fires reliably regardless of the
        // verbose toggle.
        // Snapshot the self-state lambdas once per drop so the diagnostic logs and the
        // hunt-match call all see the same values (avoid races where the user pins a hero
        // mid-evaluation and one log line shows the old name and another shows the new).
        uint   selfProto = SelfPrototypeIndex();
        ulong  selfOwner = SelfOwnerId();
        string selfHero  = SelfHeroName();

        // Resolve the item's intended hero via the root-enum table -- this is the value
        // SelfOnly's hero-name comparison gates on.  Null = the drop's EquippableBy isn't a
        // known shipping hero (server-merged custom avatar, or "any hero" item).
        string? itemHero = AvatarNamesByProto.Get(spec.EquippableByEnumIndex);

        // Fire a one-shot diagnostic when the resolved hero name changes -- gives the user
        // a single glanceable "self filter is now armed" signal without grepping through
        // pages of self-owner-pin / self-proto-resolved chatter.  selfOwner + selfProto are
        // surfaced too so the legacy enum-based state stays visible for triage.
        if (!string.Equals(selfHero, _lastSelfHeroSeen, System.StringComparison.OrdinalIgnoreCase))
        {
            Diagnostic?.Invoke(
                $"LootScannerDiagnostic: self-state changed selfOwner={selfOwner} "
              + $"selfProto={selfProto} selfHero '{_lastSelfHeroSeen ?? "<unknown>"}' -> '{selfHero}' "
              + $"({(string.IsNullOrEmpty(selfHero)
                        ? "hero not resolved yet -- fire a power on your active avatar to pin it"
                        : "ready for SelfOnly filter")})");
            _lastSelfHeroSeen = selfHero;
        }
        bool isForSelf = !string.IsNullOrEmpty(selfHero)
                      && !string.IsNullOrEmpty(itemHero)
                      && string.Equals(selfHero, itemHero, System.StringComparison.OrdinalIgnoreCase);
        // selfProto surfaced verbatim in the log so the user can distinguish "avatar not
        // identified yet" (selfProto=0) from "identified but mismatched" (non-zero but
        // != equippableBy).  The two cases need different fixes -- "not identified" is a
        // timing issue (launch Cerebro before logging in, or activate a power), but
        // "mismatched" implies the EquippableBy field uses a different enum than the
        // avatar's EntityCreate prototype, which is a wire-format question we have to
        // investigate.
        if (IsVerboseEnabled())
        {
            Diagnostic?.Invoke(
                $"LootScannerDiagnostic: + Loot drop entityId={ev.EntityId} score={score} "
              + $"IL={spec.ItemLevel} affixes={affixCount} "
              + $"(T1={t1Count} T2={t2Count} T3={t3Count} ?={noneCount}) "
              + $"itemProto={spec.ItemProtoEnumIndex} rarityProto={spec.RarityProtoEnumIndex} "
              + $"equippableBy={spec.EquippableByEnumIndex} itemHero='{itemHero ?? "<unknown>"}' "
              + $"selfOwner={selfOwner} selfProto={selfProto} selfHero='{selfHero}'"
              + $"{(isForSelf ? " [SELF]" : "")} "
              + $"-- [{details}]");
        }

        // Hunt match: does this drop match the user's configured criteria?  Emits a louder
        // log line AND fires HuntMatched so the presenter can play an alert sound.  Avatar-
        // based identity match works regardless of how the server numbers its prototype
        // enums.
        bool huntMatched = HuntCriteria.MatchesHunt(spec, spec.AffixSpecs, selfProto, selfHero, out var huntAffixes);

        // Always emit a one-liner explaining the hunt decision -- it's the most-frequent
        // "why didn't this trigger?" question and the cost is one log line per drop, which
        // is rare-enough traffic that we don't need a verbose gate.  Format mirrors the
        // user's mental model: what config we evaluated against, what we found, what
        // decided.  When a known-good drop fails to match, this line tells you instantly
        // whether the patterns failed to match, MinHits gated it, the rarity gate gated it,
        // or the self gate gated it.
        var cfgNow = LootHuntConfig.Current;
        Diagnostic?.Invoke(
            $"LootScannerDiagnostic: hunt-eval entityId={ev.EntityId} matched={huntMatched} "
          + $"matches=[{string.Join(",", huntAffixes)}] (count={huntAffixes.Count} need>={cfgNow.MinHits}) "
          + $"selfHero='{selfHero}' itemHero='{itemHero ?? "<unknown>"}' selfMatch={isForSelf} "
          + $"cfg.Enabled={cfgNow.Enabled} cfg.SelfOnly={cfgNow.SelfOnly} cfg.Rarity={cfgNow.Rarity} "
          + $"cfg.WantedPatterns=[{string.Join(",", cfgNow.WantedPatterns)}]");

        if (huntMatched)
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
