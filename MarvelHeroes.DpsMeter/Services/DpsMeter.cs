using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MarvelHeroesComporator.NetworkSniffer;

namespace MarvelHeroes.DpsMeter.Services;

/// <summary>
/// Real-time per-player DPS aggregator fed by the passive network sniffer.
///
/// Pipeline:
///   <see cref="MhMissionSniffer"/> parses every <c>NetMessagePowerResult</c> into a
///   <see cref="DamageDealtEvent"/>, we bucket those events by <c>ultimateOwnerEntityId</c>
///   (the canonical "who gets credit") and expose a sliding 5-second damage rate for the
///   entity we believe is the local avatar.
///
/// Self-detection heuristic (good enough for single-player Tahiti which is the 99% case):
///   over a rolling 60-second window we compare total damage-by-owner and designate the top
///   dealer as "you". That owner's 5s rate is the number shown on the UI. The heuristic is
///   self-correcting — if the wrong entity briefly takes the crown (e.g. a heavy-hitting
///   enemy mob while you're low-level), the display will lag by a few seconds until your
///   cumulative damage overtakes theirs. A stricter mode (filter by avatar prototype) can be
///   layered on later once the EntityPrototype enum table is imported.
///
/// Threading: sniffer callbacks fire on its capture thread. Every mutation of internal state
/// happens under <see cref="_sync"/>, and <see cref="DpsChanged"/> fires from inside the lock
/// - subscribers that touch the UI must marshal to the dispatcher themselves.
/// </summary>
public sealed class DpsMeter : IDisposable
{
    // Sliding window length that determines the DPS number's "smoothness". 5 s is the industry
    // default (WoW Details, ACT) — snappy enough to react to burst phases without spiking wildly
    // on individual crits. If we ever expose a knob for this, keep it between 3 s and 10 s.
    private static readonly TimeSpan InstantWindow = TimeSpan.FromSeconds(5);

    // Leader-board window for self-avatar detection. Longer than the instant window so that
    // short burst encounters against a strong mob don't momentarily flip the "this is you"
    // attribution to the enemy. 60 s reliably stabilises on the player in every test I've run.
    private static readonly TimeSpan OwnerScoringWindow = TimeSpan.FromSeconds(60);

    // ── Boss-fight idle reset (boss-only mode only) ──────────────────────────────────────
    // In boss-only mode, fights are *discrete* events with idle gaps between them (run to
    // next boss spawn, talk to vendor, swap powers, …).  The 60s sliding window is great
    // for sustained farming but creates confusing UX between boss kills: previous fight's
    // numbers stay on the leaderboard for almost a full minute even though the player has
    // visibly moved on to a new encounter — observed in production where Bandit + Shephiron
    // remained pinned with 12.45M total during the first ~30s of a fresh Elektra fight,
    // because Elektra's hits hadn't yet accumulated enough to dominate the decaying tail.
    //
    // Solution: when a boss-admitted hit lands AFTER an idle period of >= this window,
    // reset all scoring containers so the new fight starts clean.  20s is short enough to
    // catch typical between-boss gaps but long enough to NOT trigger during normal lulls
    // within a single boss fight (mechanic phases, brief untargetability windows).
    //
    // Only applies in BossOnlyMode — in all-damage mode the continuous decay is the
    // intended behavior (you're farming, not killing discrete bosses).
    private static readonly TimeSpan BossFightIdleReset = TimeSpan.FromSeconds(20);

    /// <summary>How long the encounter accumulator can sit without a single damage event from
    /// ANY player before <see cref="Tick"/> declares the fight effectively over and ends it.
    /// Backstop for situations where the natural EntityKilled / EntityDestroyed path doesn't
    /// fire — boss despawns, the user moves out of AOI mid-fight, party wipes, the killer is
    /// off-screen and the kill packet was never broadcast to us, etc.  Without this the
    /// encounter would stay "active" indefinitely with stale totals on screen.
    ///
    /// <para>Why 30 s and not 20 s (the <see cref="BossFightIdleReset"/> safety net): boss
    /// fights routinely have brief untargetability phases (mechanic transitions, immunity
    /// windows, off-screen movement to the next platform) where 5–15 s of total silence is
    /// normal.  20 s would false-positive on those.  30 s of zero damage from EVERY player
    /// in AOI means the fight is genuinely dead — no peer is engaging either.</para>
    ///
    /// <para>Distinct from <see cref="BossFightIdleReset"/>: that one watches SELF only and
    /// wipes the 60 s sliding window; this one watches the encounter accumulator (all owners)
    /// and emits a proper "encounter ended" with the standard freeze / peer-only-clear branch.</para></summary>
    private static readonly TimeSpan EncounterStallTimeout = TimeSpan.FromSeconds(30);

    /// <summary>How long an individual engaged boss can go without a single damage event hitting
    /// it before <see cref="Tick"/> evicts it from <see cref="_engagedBossEntityIds"/> as
    /// "presumed dead".  When eviction drops the engaged set to empty, the encounter ends
    /// through the same freeze / auto-clear branch that natural kill events trigger.
    ///
    /// <para>Why this exists alongside the 30 s stall watchdog: in busy public areas (Avengers
    /// Tower, BUE, Holo-Sim) the user routinely chains bosses with &lt; 30 s between fights.
    /// Without per-boss eviction, a missed kill event leaves boss A in the engaged set forever;
    /// the user then hits boss B, B's id joins A in the engaged set, damage piles onto A's
    /// totals, and the leaderboard shows ever-growing numbers across what the user perceives
    /// as separate fights — exactly the "counters do not reset" complaint this addresses.
    /// The 30 s stall watchdog only triggers when EVERYONE goes silent for 30 s, which never
    /// happens during a back-to-back boss chain.</para>
    ///
    /// <para>Why 15 s: fits comfortably between typical mechanic-phase untargetability windows
    /// (≤ 10 s in MH content) and the average inter-boss travel time (15–60 s).  Boss going
    /// silent for 15 s is a strong "this fight ended" signal; bumping to 20–30 s would let
    /// the missed-kill case continue to leak into subsequent fights.  If a real boss with a
    /// &gt; 15 s untargetable phase ever shipped, raise this — but the corresponding kill
    /// event would also resolve the issue naturally.</para></summary>
    private static readonly TimeSpan EngagedBossIdleEviction = TimeSpan.FromSeconds(15);

    // Note (Apr 2026, second iteration of "numbers reducing after fight ended" fix):
    // the previous EncounterFrozenFastClearGrace (5 s grace before a non-engaged-target
    // hit would discard the freeze) was REMOVED along with FastClearFrozenIfStale.  Even
    // with the grace, in practice a single hit on a loot crate / medallion / mini-boss /
    // patrol mob 5–10 s after the kill — i.e. exactly when the user is reading the
    // breakdown — would wipe it and drop the UI back to the 60 s sliding window (which
    // decays).  The freeze now persists indefinitely until one of these explicit
    // "user moved on" events fires:
    //   • hit on a real boss-classified target → encounter cleared in OnDamageDealt
    //   • OnRegionChanged                       → full reset
    //   • boss-only mode toggle                 → full reset
    //   • Reset() / external state wipe
    // The first two cover every realistic "user finished reading and moved on" case;
    // the cost is that fighting a long sequence of unmapped mobs after a real boss
    // kill leaves the breakdown on screen — the user can dismiss it by toggling boss
    // mode or zoning, but most of the time they WANT to keep reading it.

    private readonly MhMissionSniffer _sniffer;
    private readonly object _sync = new();

    // ── Hero identification & per-hero max-hit tracker ──────────────────────────────────────────
    // Two independent identification channels, tried in order:
    //   (1) EntityCreate announces (entityId, entityPrototypeEnumIdx). We cache it and look up by
    //       the self-owner entity id. Most accurate — it's literally the avatar's prototype.
    //   (2) Every damaging player power lives at Powers/Player/<HeroName>/…, so the power's enum
    //       index alone is enough to tell the hero. Used when we missed the EntityCreate (app
    //       launched mid-region, region pre-loaded).  See HeroPowers.cs for the map.
    //
    // Both channels resolve to the same string display name ("Iron Man", "Blade", …) via
    // HeroPrototypes.Names / HeroPowers.Names, which is what the overlay title uses AND what we
    // key the personal-best dictionary on — so persistence is stable no matter which path fired.
    //
    // ConcurrentDictionary because sniffer writes from capture thread while DpsMeter reads the
    // same map under its own lock during OnDamageDealt / Tick — avoiding a shared lock here keeps
    // the hot path cheap and lets the sniffer keep up even at 300+ PowerResult/sec bursts.
    private readonly ConcurrentDictionary<ulong, uint> _prototypeByEntityId = new();

    /// <summary>Hero proto / power indices we've already reported as "unknown — add to
    /// HeroPrototypes.cs" so the diagnostic log doesn't fill up with repeat lines for the same
    /// hero. Reset on region change so a re-entered region still logs once if the mapping is
    /// still missing.  Both entity-proto and power-proto indices share this set because their
    /// spaces don't overlap (they live in different enum tables) and the union is small.</summary>
    private readonly HashSet<uint> _loggedUnknownHeroes = new();

    /// <summary>Pet/summon entity → chain-root player avatar entity. Built lazily from
    /// <c>PowerResult</c> events: every <c>(rawOwner = X, wireUlt = Y)</c> pair where Y
    /// resolves to a confirmed player avatar (directly via <c>_heroNameByOwnerId</c> or
    /// <c>_localAvatarEntityIds</c>, or transitively through an existing entry in this
    /// dict) records <c>X → root</c>. Used by <see cref="OnDamageDealt"/> to redirect the
    /// pet's damage onto the summoner's leaderboard row, and by the render-time coalesce
    /// pass in <see cref="GetTopHeroesByEncounterShare"/> / <see cref="GetTopHeroesBy60sShare"/>
    /// as a safety net for the first few sub-second hits that fire BEFORE the chain edge
    /// has been established (the pet's pre-fold damage stays under its own entity id and
    /// gets merged into the root row at render time).
    ///
    /// <para>Why "chain-root" and not just "wireUlt": Magik-style multi-tier summons
    /// (Magik avatar → demon → demon's sub-pet) have <c>wireUlt</c> pointing at the
    /// IMMEDIATE parent, not the avatar. Walking the dict transitively at edge-record
    /// time collapses the chain to the root once, so per-hit fold is O(1) instead of
    /// O(chain depth). The trade-off is that an edge can only be recorded once we've
    /// observed the parent's own edge (or the parent IS a confirmed player avatar) —
    /// in practice this just means the very first hits from a deeply-nested sub-pet may
    /// fall through to the render-time coalesce pass instead of being folded at scoring
    /// time, which is fine.</para>
    ///
    /// <para>Reset on region change: pet entity ids don't survive region transitions
    /// (server destroys all summons on zone) and re-using stale mappings against a
    /// re-allocated entity id in the new region would mis-attribute damage. Cleared by
    /// <see cref="OnRegionChanged"/> alongside the other entity-keyed maps that ARE
    /// per-region (<c>_prototypeByEntityId</c>, <c>_localAvatarEntityIds</c>, etc.).</para></summary>
    private readonly Dictionary<ulong, ulong> _petRootOwnerByEntity = new();

    /// <summary>One-shot diagnostic dedup for the peer-pet fold path in
    /// <see cref="OnDamageDealt"/>. Keyed by the pet/summon entity id so the log gets
    /// exactly one line per distinct summon entity per session — useful for verifying
    /// which pet ids were folded into which avatars without spamming the log on every
    /// pet hit. Cleared on region change because pet entity ids restart per region.</summary>
    private readonly HashSet<ulong> _loggedPeerPetFolds = new();

    /// <summary>One-shot dedup for <see cref="CoalesceAnonymousRowsByHeroName"/> diagnostics.
    /// Without this, every leaderboard rebuild (~4 Hz) emits identical fold lines — tens of
    /// thousands per long session. Cleared on region change with the pet map.</summary>
    private readonly HashSet<(ulong PetOrProxyOwner, ulong NamedAvatarOwner)> _loggedAnonByHeroFolds = new();

    /// <summary>One-shot dedup for the "target prototype isn't a boss" diagnostic path. Keyed by
    /// target <c>prototypeEnumIndex</c> so we log each distinct mob type exactly once per session
    /// (or until region change, which clears it). Without this a sustained AOE on trash packs
    /// would spam the log with hundreds of identical rejection lines per second.</summary>
    private readonly HashSet<uint> _loggedNonBossTargets = new();

    /// <summary>Sibling of <see cref="_loggedNonBossTargets"/> for the "target prototype unknown"
    /// (missed EntityCreate) branch. Keyed by entity id — each spawned target gets exactly one
    /// diagnostic line, so a long fight against an un-observed boss doesn't flood the log.</summary>
    private readonly HashSet<ulong> _loggedUnknownBossTargets = new();

    /// <summary>Sibling of <see cref="_loggedNonBossTargets"/> for the "boss admit only succeeded
    /// because of the dumper off-by-one fallback" branch (see <see cref="BossPrototypes.IsBoss"/>
    /// remarks for the underlying bug).  Keyed by the live <c>prototypeEnumIndex</c> so we surface
    /// every distinct off-by-one prototype exactly once per session — that lets users grep the
    /// diagnostic log to find which entries to add when manually patching the boss list, and
    /// makes it visible when the workaround is what's keeping a fight working.</summary>
    private readonly HashSet<uint> _loggedOffByOneBossAdmits = new();

    /// <summary>One-shot dedup for the "non-combatant filter dropped this hit" diagnostic in
    /// normal DPS mode.  Keyed by target <c>prototypeEnumIndex</c> so each distinct breakable
    /// prop / item / non-agent record gets exactly one log line per region (cleared on region
    /// change with the rest of the per-region dedup state).  Without this, sustained AOEs that
    /// splash on environmental destructibles would emit a rejection line on every PowerResult.
    /// See <see cref="CombatantPrototypes"/> for what counts as a combatant; see the filter
    /// branch in <see cref="OnDamageDealt"/> for when the diagnostic fires.</summary>
    private readonly HashSet<uint> _loggedNonCombatantTargets = new();

    /// <summary>Sibling of <see cref="_loggedNonCombatantTargets"/> for the "unknown prototype"
    /// branch of the normal-mode non-combatant filter — entities whose <c>EntityCreate</c> was
    /// never observed by the sniffer (or arrived after the first hit on them).  Keyed by target
    /// <c>entityId</c> rather than <c>prototypeEnumIndex</c> because by definition we have NO
    /// prototype index here; entity ids restart per region so the per-region clear below keeps
    /// the dedup set bounded.  Capped at <see cref="UnknownTargetLogCap"/> per region to prevent
    /// log flood when a fresh region rains EntityCreates that the sniffer hasn't processed yet.
    ///
    /// <para>The unknown-target branch deliberately DROPS in normal mode (unlike boss mode which
    /// admits optimistically): production logs show that crate / vase / breakable-door
    /// EntityCreates are by far the most common cache misses (their EntityCreates often get lost
    /// in the post-region-change burst) and admitting them defeats the entire purpose of the
    /// non-combatant filter.  Real mob hits virtually always have a cached prototype because mobs
    /// have a non-trivial spawn animation that gives the sniffer plenty of time to process the
    /// EntityCreate before the first hit lands on them.</para></summary>
    private readonly HashSet<ulong> _loggedUnknownNormalTargets = new();

    /// <summary>Cap for <see cref="_loggedUnknownNormalTargets"/> — once we've emitted this many
    /// unknown-target diagnostics in a single region, stop logging until the next region change.
    /// Prevents log flood during the EntityCreate burst that follows a region transition while
    /// still surfacing enough samples for the user to grep for legitimate mob entity ids that
    /// got dropped (and in turn add their prototypes to <see cref="CombatantPrototypes"/>).</summary>
    private const int UnknownTargetLogCap = 25;

    /// <summary>One-shot dedup for the "non-combatant filter ADMITTED this hit" diagnostic in
    /// normal DPS mode.  Keyed by target <c>prototypeEnumIndex</c>: the FIRST time a given
    /// prototype is admitted as a combatant during the current region, we emit one log line
    /// with the protoIdx, the entityId of the hit, and the damage value.  This makes "what's
    /// actually being counted as DPS" trivially answerable from the log: grep for
    /// <c>combatant filter admit</c> after a fight, cross-reference each protoIdx against the
    /// dumper output (or <c>HeroPrototypes.Names</c> for hero protos) and you can see exactly
    /// which prototypes the meter is admitting.  If any of them are obviously wrong
    /// (XP orb, projectile, environmental destructible smart-prop), they belong on a
    /// to-exclude list at dumper-generation time, not at runtime.
    ///
    /// <para>Capped at <see cref="AdmittedTargetLogCap"/> per region for the same reason as the
    /// unknown-drop cap above — a busy mob fight can hit 50+ distinct prototypes inside the first
    /// 30 s and we'd rather lose the long tail than flood the log.  Cleared on region change with
    /// the rest of the per-region dedup state.</para></summary>
    private readonly HashSet<uint> _loggedAdmittedNormalTargets = new();

    /// <summary>Cap for <see cref="_loggedAdmittedNormalTargets"/>.  See remarks on that field
    /// for rationale.  Sized larger than the unknown-drop cap (25) because a busy patrol /
    /// terminal can legitimately hit 30–60 distinct mob prototypes inside the first minute of
    /// a fight (chapter / patrol mob mixes are diverse), and we want enough headroom to capture
    /// the long tail of "this one weird prototype" without spamming the log on common mobs.</summary>
    private const int AdmittedTargetLogCap = 100;

    /// <summary>Per-hero all-time max single-hit, keyed by the hero's display name
    /// (e.g. "Iron Man", "Blade").  Keying by display name rather than an enum index means the
    /// record survives the entity-id namespace change on region transitions AND stays consistent
    /// regardless of whether we identified the hero via entity-proto or via a power-proto hit.
    /// Persisted to <see cref="MaxHitsPath"/> so records survive app restarts.
    /// Lock <see cref="_sync"/> when mutating.</summary>
    private readonly Dictionary<string, uint> _maxHitByHeroName = new(StringComparer.Ordinal);

    /// <summary>Per-owner-entity hero-name cache.  Populated lazily from Channel A / Channel B
    /// resolution on every incoming <see cref="DamageDealtEvent"/>, regardless of whether the
    /// owner is the current self. Keyed by <c>ultimateOwnerEntityId</c>.
    ///
    /// Why a cache and not a point-of-use lookup? When the self-owner flips (on avatar swap,
    /// teammate respawn, re-elect on <see cref="Tick"/>, …) we need to instantly push the new
    /// hero name into <see cref="CurrentHeroDisplayName"/> WITHOUT waiting for the next damage
    /// event from the new owner — otherwise the UI sits on the old hero's name (and the old
    /// hero's MaxSingleHit) until the next attack lands, which can be many seconds.
    ///
    /// Entries accumulate until the next region change (which invalidates the whole entity-id
    /// namespace), so in practice this dict stays very small.  Guarded by <see cref="_sync"/>.</summary>
    private readonly Dictionary<ulong, string> _heroNameByOwnerId = new();

    // ── Authoritative local-player identification ───────────────────────────────────────────────
    // The two fields below are populated from server-pushed signals that are unambiguous about
    // "this id is YOU":
    //   • _localPlayerEntityId: from NetMessageLocalPlayer. It's the Player container id, NOT
    //     an avatar id. Never emits damage itself (the Player entity doesn't have powers).
    //   • _localAvatarEntityIds: avatars the server has slotted into the local Player's
    //     inventory (observed via NetMessageInventoryMove where container == _localPlayerEntityId).
    //     These ARE the ids that will appear as UltimateOwnerEntityId in PowerResult events
    //     when *you* hit something. Usually only one avatar is in AvatarInPlay at a time, but
    //     we also accept AvatarLibrary entries so a hero swap doesn't briefly un-identify you.
    //
    // When this set is non-empty we disable the "top damager = you" heuristic entirely and
    // pin _likelySelfOwnerId to the avatar that actually fired the latest hit. That eliminates
    // the party-play misattribution where another player on the same hero (or a higher-DPS
    // hero like Storm) would previously steal the "DPS - <your hero>" slot.
    //
    // Both guarded by _sync.
    private ulong _localPlayerEntityId;
    private readonly HashSet<ulong> _localAvatarEntityIds = new();

    // ── Player-nickname resolution chain (all guarded by _sync) ─────────────────────────────────
    //
    //      avatarEntityId          playerEntityId             dbId             playerName
    //   ┌──────────────────┐    ┌───────────────────┐    ┌────────────┐    ┌───────────────┐
    //   │                  │    │                   │    │            │    │               │
    //   │ PowerResult      │───►│ InventoryMove     │───►│ EntityCreate    │ NetMessageModify
    //   │ UltimateOwnerId  │    │ (container = ...) │    │ (HasDbId)  │    │ CommunityMember
    //   └──────────────────┘    └───────────────────┘    └────────────┘    └───────────────┘
    //
    //   _playerEntityIdByAvatarId        _dbIdByPlayerEntityId          _playerNameByDbId
    //
    // Each hop is populated by a different sniffer event and lives in its own map so we can
    // compose them at leaderboard-resolution time without coupling the event handlers.  Entries
    // survive the entity-id namespace (dbId ↔ name) across region changes — only the per-entity
    // maps reset on OnRegionChanged, because their ids are no longer valid.
    private readonly Dictionary<ulong, ulong> _playerEntityIdByAvatarId = new();
    private readonly Dictionary<ulong, ulong> _dbIdByPlayerEntityId    = new();
    private readonly Dictionary<ulong, string> _playerNameByDbId       = new();

    /// <summary>Direct <c>avatarEntityId → dbId</c> binding used for REMOTE players, where the
    /// player-container EntityCreate is never pushed to the local client (only the avatar is
    /// proximity-interesting).  Populated via temporal correlation: the server always emits
    /// <c>NetMessageEntityCreate</c> for a nearby avatar immediately followed by
    /// <c>NetMessageModifyCommunityMember</c> with <c>IsInitial == true</c> — see
    /// <c>AreaOfInterest.AddEntity</c> → <c>SetEntityInterestPolicies</c> →
    /// <c>UpdateNearbyCommunity</c>.  We queue each hero-prototype EntityCreate and dequeue
    /// one entry on every initial CommunityMember broadcast that arrives within
    /// <see cref="AvatarBindingWindow"/>.  FIFO pairing holds even when several avatars
    /// enter AOI in the same tick because the server processes them sequentially.</summary>
    private readonly Dictionary<ulong, ulong> _dbIdByAvatarId = new();
    private readonly Queue<(ulong AvatarEntityId, DateTime UtcTime)> _pendingAvatarBindings = new();
    /// <summary>
    /// Mid-session-launch fallback map: <c>dbId → heroDisplayName</c> (e.g. Blade, War Machine)
    /// built from <c>NetMessageModifyCommunityMember</c> <c>broadcast.slots[0].avatarRefId</c>.
    /// Populated on every CommunityMember broadcast that carries a slot, so it stays current
    /// across in-region hero swaps by other players. The leaderboard uses this as the ONLY
    /// signal for resolving nicknames when the app started after the avatar's
    /// <c>NetMessageEntityCreate</c> — i.e. we have a damage-producing <c>avatarEntityId</c> with
    /// a known hero name but no <c>_dbIdByAvatarId</c> entry. If exactly one nearby dbId plays
    /// that hero, we can infer the pairing at render time (ambiguous when two players share a
    /// hero — in that case we fall through to the <c>#XXXX</c> tag).  Never cleared on region
    /// change because dbIds are account-scoped and stable, but entries get overwritten whenever
    /// the server re-broadcasts the member with a different avatar (exactly what we want).
    /// </summary>
    private readonly Dictionary<ulong, string> _currentHeroNameByDbId = new();
    /// <summary>
    /// Session-local set of dbIds that the server has told us are currently in the
    /// <c>Nearby</c> community circle.  Populated from <c>ModifyCommunityMember</c>
    /// broadcasts whose <c>SystemCirclesBitSet</c> carries the Nearby bit (0x08).
    /// <para>
    /// Why this exists even though we already have <see cref="_currentHeroNameByDbId"/>:
    /// <c>_currentHeroNameByDbId</c> is populated from EVERY community broadcast the
    /// server sends us, which includes friends and guildmates who are logged in but nowhere
    /// near us — on a busy shard that can add up to 150+ dbIds, many of whom play the same
    /// popular hero as an actual nearby peer (observed: 10 friends simultaneously on Rogue
    /// while one nearby Oxodius was also on Rogue → 11-way tie → unique-hero fallback
    /// refused to resolve).  Restricting the fallback to the handful of dbIds the server
    /// explicitly tagged as Nearby cuts that disambiguation set down to 2-5 members and
    /// recovers nicknames for the exact case the meter was built for.
    /// </para>
    /// <para>
    /// Add-only within a region: a departure broadcast (<c>SystemCirclesBitSet == 0</c>) is
    /// reliably sent by the server when a peer leaves AOI, but sometimes the server emits
    /// a follow-up "0" broadcast right after the initial "nearby" add (an artifact of the
    /// delta encoding inside <c>CommunityMember.SendUpdateToOwner</c>) — treating that as a
    /// real removal causes flapping and drops the very binding we just acquired.  So we
    /// lean on the region-change reset (AOI rotates wholesale) to garbage-collect stale
    /// Nearby entries instead of chasing per-member departures.  Ambiguity cost of the
    /// resulting "slightly stale set" is self-healing: as the local player zones around,
    /// the set rebuilds from scratch each region, and false-positive matches almost always
    /// fail the follow-up hero-name equality check anyway.
    /// </para>
    /// </summary>
    private readonly HashSet<ulong> _nearbyDbIds = new();
    /// <summary>Reverse queue for the case where the ModifyCommunityMember broadcast is
    /// flushed to the client BEFORE the avatar's EntityCreate — we've observed this in
    /// the wild at sub-10ms offsets, probably because the server batches the outbound
    /// mux frame and the two messages end up reordered on the wire.  A newly-learned
    /// dbId+name pair that can't find a queued avatar goes here and waits for the next
    /// hero EntityCreate to consume it.</summary>
    private readonly Queue<(ulong DbId, DateTime UtcTime)> _pendingDbIdBindings = new();
    private static readonly TimeSpan AvatarBindingWindow = TimeSpan.FromSeconds(10);

    private static readonly string MaxHitsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MarvelHeroesComporator", "dps-max-hits.json");

    /// <summary>On-disk cache of everything we've ever learned about remote players — keyed
    /// by account-level dbId, which is stable across sessions and region changes.  Merged back
    /// into <see cref="_playerNameByDbId"/> and <see cref="_currentHeroNameByDbId"/> on startup
    /// so the mid-session-launch fallback (see <see cref="GetTopHeroesBy60sShare"/>) has enough
    /// data to resolve nicknames for avatars whose EntityCreate we missed. Without persistence
    /// the fallback can only use what the server re-broadcasts within the first minute of the
    /// session, which on a quiet instance is essentially nothing.</summary>
    private static readonly string PlayerIndexPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MarvelHeroesComporator", "player-index.json");

    /// <summary>Tiny sidecar file holding ONLY our own account dbId. Sole purpose: survive
    /// "DPS meter restarted mid-region" — when the user closes &amp; relaunches the meter without
    /// zoning, the server does NOT replay <see cref="EntityCreatedEvent"/> for the local Player
    /// container or local avatar, so the in-memory <see cref="_dbIdByPlayerEntityId"/> /
    /// <see cref="_dbIdByAvatarId"/> bindings stay empty and <see cref="ResolveNicknameForOwnerLocked"/>
    /// falls through to the two-pass disambiguator. With multiple peers playing the same hero in
    /// the same AOI (two Blades, two Storms, …) the disambiguator can — and observably does —
    /// pick the wrong peer's nickname for the self row, merging your damage with theirs.
    ///
    /// Persisting the self-dbId once and reapplying the binding on the next power-activation
    /// after restart fixes this end-to-end without depending on the server resending the
    /// EntityCreate (which it never does until the next region transition).</summary>
    private static readonly string SelfIdentityPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MarvelHeroesComporator", "dps-self.json");

    /// <summary>Account dbId of the local player, persisted across sessions via
    /// <see cref="SelfIdentityPath"/>. Set by any of the three "this binding belongs to us"
    /// signal sources (local Player container EntityCreate, LocalPlayerIdentified follow-up,
    /// LocalAvatarObserved follow-up) and read by the restart-without-zone restore path in
    /// <see cref="OnLocalAvatarObserved"/> and the "self never falls through to disambiguator"
    /// short-circuit in <see cref="ResolveNicknameForOwnerLocked"/>. Lock <see cref="_sync"/>
    /// for writes; reads from the resolver are already on the same lock.</summary>
    private ulong _selfDbId;

    private sealed class SelfIdentityFile
    {
        public string? DbId { get; set; }
        public string? Nick { get; set; }
        public DateTime LastUpdatedUtc { get; set; }
    }

    /// <summary>In-memory guard so we don't hammer the disk on every CommunityMember broadcast.
    /// Flipped on any mutation to <see cref="_playerNameByDbId"/> / <see cref="_currentHeroNameByDbId"/>;
    /// a background tick consumes it and writes the file at most every
    /// <see cref="PlayerIndexSaveInterval"/>.  Throwaway sessions still flush via the
    /// <see cref="FlushPlayerIndexIfDirty"/> call path if the app exits cleanly.</summary>
    private bool _playerIndexDirty;
    private DateTime _playerIndexLastSavedUtc = DateTime.MinValue;
    private static readonly TimeSpan PlayerIndexSaveInterval = TimeSpan.FromSeconds(5);
    /// <summary>Drop cache entries older than this on load — a dbId that's been quiet for weeks
    /// almost certainly isn't the player our current damage stream belongs to, and keeping stale
    /// entries around only inflates ambiguity-rejection in the hero-match fallback.</summary>
    private static readonly TimeSpan PlayerIndexTtl = TimeSpan.FromDays(30);

    /// <summary>Per-player bundle written to <see cref="PlayerIndexPath"/>.  Flat on purpose so
    /// future fields (last-seen region, preferred avatar, …) can be added without a schema
    /// migration.  <see cref="LastSeenUtc"/> drives the TTL sweep on load.</summary>
    private sealed class PlayerIndexEntry
    {
        public string? Name { get; set; }
        public string? Hero { get; set; }
        public DateTime LastSeenUtc { get; set; }
    }
    /// <summary>Latest-seen timestamp per dbId, kept in memory so the save file's
    /// <see cref="PlayerIndexEntry.LastSeenUtc"/> reflects when the binding was last refreshed
    /// rather than when the app started.  Not used for any lookup logic beyond persistence.</summary>
    private readonly Dictionary<ulong, DateTime> _playerIndexLastSeen = new();

    // One queue of every damage event inside the ownership-scoring window, in arrival order.
    // On each incoming event we evict entries older than OwnerScoringWindow from the head,
    // subtract their damage from _totalsPerOwner, and append the new one. O(1) amortized per event.
    private readonly Queue<(DateTime Ts, ulong Owner, uint Damage)> _scoring = new();
    private readonly Dictionary<ulong, long> _totalsPerOwner = new();

    // Cumulative per-owner totals that grow monotonically until region change or boss-mode
    // toggle.  Mirrors _totalsPerOwner on every admit but is NOT decayed by Tick — that's the
    // whole point: in normal (non-boss) mode the leaderboard should show "what each player
    // contributed this region", not "what they contributed in the last 60 s" (which decays
    // mid-fight and produces the recurring "numbers reducing after I stopped attacking"
    // perception bug).  Boss mode has its own equivalent (_encounterTotalsPerOwner) bounded by
    // fight start/end; this dict is the normal-mode equivalent bounded by region change.
    //
    // Why a separate dict (instead of repurposing _encounterTotalsPerOwner with a different
    // gate): the encounter accumulator's lifecycle is entangled with engaged-boss tracking,
    // freeze/auto-clear logic, and stall watchdogs — semantics that don't apply outside a
    // bounded fight.  Cleanest split: each mode owns its own accumulator with a coherent
    // lifecycle.  Both share the 60s sliding window for the "live DPS" big number (rate),
    // which is mode-agnostic and never wants the cumulative totals.
    private readonly Dictionary<ulong, long> _sessionTotalsPerOwner = new();

    // Separate (shorter) queue for the instant 5 s DPS number.  We could derive this from
    // `_scoring` by scanning it on every tick, but keeping a dedicated queue avoids the scan
    // and lets the scoring queue stay decoupled (different window size, different semantics).
    private readonly Queue<(DateTime Ts, ulong Owner, uint Damage)> _instant = new();

    private ulong _likelySelfOwnerId;
    private DateTime _likelySelfChosenAt;

    /// <summary>Entity id currently treated as "you" (the local avatar).  <c>0</c> until we've
    /// seen enough traffic to pick a leader.</summary>
    public ulong LikelySelfOwnerId { get { lock (_sync) return _likelySelfOwnerId; } }

    /// <summary>Instantaneous DPS over <see cref="InstantWindow"/> for the
    /// <see cref="LikelySelfOwnerId"/> entity.  <c>0</c> when there's no data yet.</summary>
    public double CurrentDps { get; private set; }

    /// <summary>Total damage by <see cref="LikelySelfOwnerId"/> inside the
    /// <see cref="OwnerScoringWindow"/> — useful for a secondary "60s encounter" display.</summary>
    public long CurrentOwnerTotal60s { get; private set; }

    /// <summary>Cumulative damage by <see cref="LikelySelfOwnerId"/> since the last region change
    /// or boss-mode toggle — drives the <c>Total:</c> number shown beneath the live DPS in normal
    /// (non-boss) mode.  Unlike <see cref="CurrentOwnerTotal60s"/> this value never decays: it
    /// only grows during a region session and resets atomically with
    /// <see cref="_sessionTotalsPerOwner"/> on region change / mode flip.  Boss mode keeps using
    /// <see cref="EncounterSnapshot.SelfTotal"/> for its <c>Fight:</c> label — session totals are
    /// purely the normal-mode equivalent (an encounter without a fight boundary).
    /// Lock <see cref="_sync"/>.</summary>
    public long CurrentOwnerSessionTotal { get; private set; }

    /// <summary>All-time personal-best single hit for <see cref="CurrentHeroDisplayName"/>.
    /// Reads from <see cref="_maxHitByHeroName"/>.  When the hero is not yet identified this
    /// stays 0 and rises the first time the avatar lands a hit we can attribute (either via
    /// entity-proto or via power-proto — see the two-channel comment on the field block).</summary>
    public uint MaxSingleHit { get; private set; }

    /// <summary>Human-readable display name of the currently-credited avatar ("Iron Man", "Blade",
    /// …).  Empty string until identified. Populated from <see cref="HeroPrototypes.Names"/> when
    /// <see cref="EntityCreatedEvent"/> observed for <see cref="LikelySelfOwnerId"/>, or from
    /// <see cref="HeroPowers.Names"/> when only a damage event is available.</summary>
    public string CurrentHeroDisplayName { get; private set; } = string.Empty;

    /// <summary>One row in the "heroes in AOI sorted by 60s damage" leaderboard surfaced on the
    /// overlay. <see cref="Percent"/> is the share of damage-done-by-heroes in the last
    /// <see cref="OwnerScoringWindow"/> (so all <c>Percent</c> fields in a snapshot sum to 100).
    /// <see cref="IsSelf"/> is <c>true</c> for the row whose owner id equals the current
    /// <see cref="LikelySelfOwnerId"/>; the UI uses it to visually highlight "you" in the list.</summary>
    public readonly struct HeroShareEntry
    {
        public string Name      { get; init; }
        public double Percent   { get; init; }
        public long   Total60s  { get; init; }
        public bool   IsSelf    { get; init; }
        /// <summary>Account-level nickname of the player behind this avatar ("SomeGuy42"), when
        /// we've managed to walk the <c>avatar → player → dbId → name</c> chain for them.
        /// Empty when any hop is missing — common for avatars we've only ever seen via damage
        /// events (no InventoryMove observed) or players whose CommunityMember broadcast didn't
        /// include a name.  UI is expected to render the row without a nickname suffix in that
        /// case rather than printing an empty placeholder.</summary>
        public string PlayerName { get; init; }

        /// <summary>Avatar entity id that produced this row.  Exposed so the caller (or the
        /// ctor of this struct during post-processing) can derive a short stable suffix for
        /// UI disambiguation when two rows would otherwise be visually identical — same
        /// hero, both nickname-less.  Not meant for display on its own; keep it out of the
        /// overlay unless you need a debug readout.</summary>
        public ulong OwnerId   { get; init; }
    }

    /// <summary>Fired every time <see cref="CurrentDps"/> changes. Fires from the sniffer's
    /// capture thread — marshal to the UI dispatcher in the subscriber.</summary>
    public event EventHandler? DpsChanged;

    /// <summary>Optional sink for low-volume diagnostic strings (avatar swap detected, region
    /// change reset, …). Wire to a log file from the hosting app.</summary>
    public Action<string>? Diagnostic { get; set; }

    /// <summary>When <c>true</c>, <see cref="OnDamageDealt"/> silently drops hits whose
    /// <c>TargetEntityId</c> doesn't resolve to a prototype admitted by
    /// <see cref="BossPrototypes.IsBoss"/> (Boss + GroupBoss only — <see cref="BossPrototypes.MiniBossIndices"/>
    /// is excluded by design, see field doc on that set).  Trash packs, summons, world-destructibles
    /// AND named mini-bosses (patrol named mobs, terminal "elite" enemies, chapter-trash gates) no
    /// longer contribute to the sliding windows, so the overlay's numbers reflect real encounter-only
    /// throughput.
    ///
    /// <para>Corner cases:</para>
    /// <list type="bullet">
    ///   <item>Target whose <c>EntityCreate</c> we missed (app launched mid-fight) → dropped.
    ///         We could admit unknown targets optimistically, but that would leak trash damage
    ///         during the first ~minute after launch which defeats the purpose of the filter.</item>
    ///   <item>Damage windows still decay normally for owners who haven't landed a qualifying
    ///         hit recently — <see cref="_scoring"/> purges on time, not on event count.</item>
    ///   <item>Hero-identification side-effects (<see cref="_heroNameByOwnerId"/>,
    ///         <c>scoringOwner</c> folding) happen AFTER the filter: non-boss hits don't even
    ///         register an owner, so they can't accidentally pin self-owner or update MaxHit.</item>
    /// </list>
    ///
    /// Toggled at runtime from the overlay's right-click menu; not persisted (intentionally —
    /// users typically want all-damage by default when they reopen the app).</summary>
    public bool BossOnlyMode
    {
        get { lock (_sync) return _bossOnlyMode; }
        set
        {
            lock (_sync)
            {
                if (_bossOnlyMode == value) return;
                _bossOnlyMode = value;
                // Clear per-owner totals on a mode flip so the 60s leaderboard doesn't display
                // stale trash-damage numbers for one window. Instant DPS will rebuild on the
                // next qualifying hit. MaxSingleHit / per-hero PB are intentionally preserved —
                // those are personal records independent of the current filter.
                _scoring.Clear();
                _instant.Clear();
                _totalsPerOwner.Clear();
                // Session totals are also wiped — toggling the filter is a deliberate "I'm
                // changing what damage gets counted" act, so carrying forward a Total: number
                // built from the previous filter would be misleading (e.g. boss-only ON would
                // suddenly drop the leaderboard to a fraction of its previous total because
                // trash damage is now excluded).  Cleanest semantic: a mode flip starts fresh.
                _sessionTotalsPerOwner.Clear();
                CurrentDps = 0;
                CurrentOwnerTotal60s = 0;
                CurrentOwnerSessionTotal = 0;
                // Re-arm the boss-fight idle detector so the very next hit doesn't get
                // mis-classified as "post-idle" (the previous _lastSelfBossHitUtc is now
                // meaningless given we just wiped the windows).
                _lastSelfBossHitUtc = DateTime.MinValue;

                // Wipe the encounter accumulator — totals only make sense in boss mode,
                // and the previous fight's totals are necessarily stale by the time the
                // user toggles modes (mode flip is a manual UI act, not a kill event).
                _encounterTotalsPerOwner.Clear();
                _engagedBossEntityIds.Clear();
                _lastHitPerEngagedBoss.Clear();
                _encounterStartUtc = DateTime.MinValue;
                _encounterEndedUtc = DateTime.MinValue;
                _lastEncounterDamageUtc = DateTime.MinValue;
            }
            Diagnostic?.Invoke($"DpsMeter: BossOnlyMode = {value} (scoring windows cleared)");
            DpsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private bool _bossOnlyMode;

    /// <summary>UTC timestamp of the most recent boss-admitted hit whose scoring owner was
    /// <em>self</em> (the local avatar / pinned self-owner, not a peer).  Used to detect the
    /// "between bosses" idle gap so we can wipe stale leaderboard rows when WE start a new
    /// fight — see <see cref="BossFightIdleReset"/>.
    ///
    /// <para>Self-only is critical: in busy public areas (Avengers Tower, BUE, Holo-Sim,
    /// shared waypoints) peer avatars constantly hit bosses every few hundred ms, so a
    /// timer that any boss-admitted hit refreshes never opens a 20-second gap and the
    /// reset effectively never fires.  Gating on self means peer activity keeps the
    /// leaderboard alive (good UX — you can still see what teammates are doing) while
    /// YOUR own return to combat after 20s of personal idle starts a clean window.</para>
    ///
    /// <c>DateTime.MinValue</c> means we've never landed a self boss hit this session (or
    /// the meter was just cleared by a region change / mode flip).  Lock <see cref="_sync"/>.</summary>
    private DateTime _lastSelfBossHitUtc = DateTime.MinValue;

    // ── Encounter accumulator (boss-only mode) ────────────────────────────────────────
    // Fight-scoped per-owner totals: persists from the FIRST boss hit until ALL engaged
    // bosses die (then "freezes" until the next boss hit clears it).  Replaces the 60s
    // sliding window for the leaderboard / title number while in boss mode — that window
    // produced the recurring "stale data" complaint where a peer who one-shot a boss for
    // 20M kept ranking #1 for the next ~minute as their slice decayed linearly.
    //
    // Why both _totalsPerOwner (60s) AND _encounterTotalsPerOwner (fight-scoped) coexist:
    //   • Normal (non-boss) mode has no natural encounter boundary — trash combat is a
    //     continuum, the 60s window is the right summarisation there.
    //   • Boss-only mode IS bounded by EntityKilled / EntityDestroyed of the engaged
    //     bosses; for that mode we want a Recount/Skada-style "this fight" breakdown.
    //   • Self-owner election (section 3 in OnDamageDealt) still uses the 60s window in
    //     the heuristic-fallback path — ripping that out would destabilise mid-session
    //     attaches.  Cheaper to feed both windows than to refactor the election logic.
    //
    /// <summary>Sum of admitted boss damage per scoring-owner since the encounter started.
    /// Includes ALL participants (self + peers) so the leaderboard percent reflects actual
    /// fight breakdown, not "your share of the last 60 s".  Cleared on encounter clear
    /// (next boss hit after a frozen fight, region change, boss-only toggle).
    /// Lock <see cref="_sync"/>.</summary>
    private readonly Dictionary<ulong, long> _encounterTotalsPerOwner = new();

    /// <summary>Set of boss <c>TargetEntityId</c>s that have been hit at least once during
    /// the current encounter and have NOT yet emitted an EntityKilled / EntityDestroyed.
    /// Encounter is considered "ended" the moment this drops to empty after having held at
    /// least one entry (i.e. last engaged boss died — naturally via kill events, OR via
    /// <see cref="EngagedBossIdleEviction"/> sweep in <see cref="Tick"/>).
    /// Lock <see cref="_sync"/>.</summary>
    private readonly HashSet<ulong> _engagedBossEntityIds = new();

    /// <summary>Last-hit UTC timestamp per engaged boss target.  Mirrors
    /// <see cref="_engagedBossEntityIds"/> in size — every entry in the HashSet has a
    /// corresponding entry here, refreshed on each <see cref="OnDamageDealt"/> hit whose
    /// <c>TargetEntityId</c> is the boss.  <see cref="Tick"/> sweeps this dict each ~250 ms
    /// and evicts entries older than <see cref="EngagedBossIdleEviction"/>; if the eviction
    /// drops <see cref="_engagedBossEntityIds"/> to empty AND the encounter is still active,
    /// it ends through the standard freeze / auto-clear branch.
    ///
    /// <para>Why a separate dict instead of changing the HashSet to a Dictionary: the
    /// HashSet check is on the OnDamageDealt hot path (every accumulator add touches it).
    /// Keeping it a HashSet preserves O(1) Add / Contains with the cheapest possible per-call
    /// cost; the per-target timestamp update is the second op on the hot path but a
    /// Dictionary indexer assignment is the same cost as a HashSet.Add anyway, so the split
    /// is purely organizational (timestamp logic stays out of the HashSet's invariant).</para>
    ///
    /// <para>Lock <see cref="_sync"/>.</para></summary>
    private readonly Dictionary<ulong, DateTime> _lastHitPerEngagedBoss = new();

    /// <summary>UTC time of the first boss hit of the current encounter, or MinValue when
    /// no encounter is active.  Used for the encounter-duration diagnostic and to gate the
    /// "previously frozen" check (so a region-change-triggered clear doesn't also fire the
    /// "encounter ended" diagnostic).  Lock <see cref="_sync"/>.</summary>
    private DateTime _encounterStartUtc = DateTime.MinValue;

    /// <summary>UTC time the last engaged boss died, or MinValue while the encounter is
    /// still active (or never started).  Non-MinValue means the leaderboard is FROZEN
    /// (final breakdown on screen, awaiting the next boss hit to clear and start fresh).
    /// Lock <see cref="_sync"/>.</summary>
    private DateTime _encounterEndedUtc = DateTime.MinValue;

    /// <summary>UTC time of the last damage event (any owner — self OR peer) that flowed into
    /// <see cref="_encounterTotalsPerOwner"/>.  Updated on every accumulator add in
    /// <see cref="OnDamageDealt"/>; consulted by <see cref="Tick"/> to detect the
    /// <see cref="EncounterStallTimeout"/> stall condition.
    ///
    /// <para>MinValue means "no encounter is active" (no one has dealt boss damage yet, or the
    /// encounter was cleared).  Reset alongside <see cref="_encounterStartUtc"/> /
    /// <see cref="_encounterEndedUtc"/> at every encounter-state-clear site.</para>
    ///
    /// <para>Why ANY owner and not self-only: the user phrased this as "if total damage for
    /// all players is not changed" — this watchdog measures whether the fight is collectively
    /// alive, not whether SELF is contributing.  A peer soloing the last 5 % of a boss while
    /// you're picking up loot still means the fight is in progress and the leaderboard
    /// shouldn't be torn down.</para>
    ///
    /// <para>Lock <see cref="_sync"/>.</para></summary>
    private DateTime _lastEncounterDamageUtc = DateTime.MinValue;

    public DpsMeter(MhMissionSniffer sniffer)
    {
        _sniffer = sniffer ?? throw new ArgumentNullException(nameof(sniffer));
        _sniffer.DamageDealt += OnDamageDealt;
        _sniffer.RegionChanged += OnRegionChanged;
        _sniffer.EntityCreated += OnEntityCreated;
        _sniffer.LocalPlayerIdentified += OnLocalPlayerIdentified;
        _sniffer.InventoryMoved += OnInventoryMoved;
        _sniffer.LocalAvatarObserved += OnLocalAvatarObserved;
        _sniffer.CommunityMemberUpdated += OnCommunityMemberUpdated;
        // Encounter end detection — both events fire for boss death:
        //   • EntityKilled: explicit kill notification (most bosses, has killer id)
        //   • EntityDestroyed: catch-all entity removal (despawn / world cleanup, fires
        //     even when the kill message was missed or the boss vanished without one)
        // Subscribing to both avoids missing encounter-ends when only one path fires.
        _sniffer.EntityKilled += OnEntityKilled;
        _sniffer.EntityDestroyed += OnEntityDestroyed;

        LoadMaxHits();
        LoadPlayerIndex();
        LoadSelfIdentity();
    }

    private void OnCommunityMemberUpdated(object? sender, CommunityMemberUpdatedEvent e)
    {
        if (e.PlayerDbId == 0)
            return;

        // Bit positions of CircleId in SystemCirclesBitSet — see CommunityCircle.cs:
        //   __None=0  __Friends=1  __Ignore=2  __Nearby=3  __Party=4  __Guild=5
        // We only care about Nearby (bit 3) — that's the AOI-add circle that temporally
        // follows the preceding avatar EntityCreate.
        const ulong NearbyCircleBit = 1UL << 3;   // 0x08

    bool firstTimeName = false;
    ulong pairedAvatarId = 0;
    bool alreadyPaired;
    bool hasNearbyBit = e.HasCircles && (e.Circles & NearbyCircleBit) != 0;
    string currentHeroName = string.Empty;
    bool heroChanged = false;
    bool nearbyAdded = false;
    int nearbyCountSnapshot = 0;

    lock (_sync)
    {
        // ── Nearby-AOI bookkeeping (feeds the fallback nickname resolver) ────────────────
        // Add any dbId that the server has explicitly tagged as "Nearby" to our session-local
        // AOI set.  This is additive only (see field doc on _nearbyDbIds for rationale — the
        // server's delta-encoded "circles=0x0" follow-ups are unreliable as departure
        // signals, so we don't remove here).  The set is garbage-collected on region change.
        if (hasNearbyBit && _nearbyDbIds.Add(e.PlayerDbId))
            nearbyAdded = true;
        nearbyCountSnapshot = _nearbyDbIds.Count;
            // Keep the name only when the server actually sent one.  On pure delta updates
            // (region / difficulty / status change) the server omits the nickname string and
            // we don't want to clobber the previously-broadcast one with an empty value.
            if (!string.IsNullOrEmpty(e.PlayerName))
            {
                firstTimeName = !_playerNameByDbId.ContainsKey(e.PlayerDbId)
                                || _playerNameByDbId[e.PlayerDbId] != e.PlayerName;
                _playerNameByDbId[e.PlayerDbId] = e.PlayerName;
                if (firstTimeName) MarkPlayerIndexDirty(e.PlayerDbId);
            }

            // Mid-session fallback map: when the server included slots[0].avatarRefId on this
            // update, resolve it to a hero name and cache it against the dbId.  This is our
            // only signal for "which hero is dbId X currently on" when we missed the avatar's
            // EntityCreate (app launched after region load). Skip writes when the ref doesn't
            // resolve to a known shipping hero — NamesByDataRef only covers the 63 shipping
            // avatars, and returning an empty string on an unknown ref protects us from
            // silently overwriting a valid earlier mapping.
            if (e.CurrentAvatarRefId != 0)
            {
                string resolved = HeroPrototypes.GetDisplayNameByDataRef(e.CurrentAvatarRefId);
                if (!string.IsNullOrEmpty(resolved))
                {
                    if (!_currentHeroNameByDbId.TryGetValue(e.PlayerDbId, out string? prev) || prev != resolved)
                        heroChanged = true;
                    _currentHeroNameByDbId[e.PlayerDbId] = resolved;
                    currentHeroName = resolved;
                    if (heroChanged) MarkPlayerIndexDirty(e.PlayerDbId);
                }
            }

            // Pairing criterion — only consume a queued avatar when this broadcast is the
            // authoritative "new nearby member" signal:
            //
            //   1. IsInitial (= top-level msg.playerName was set) — server only emits that on
            //      the "NewlyCreated" path, which fires once per brand-new CommunityMember.
            //   2. The Nearby circle bit is set — so we ignore guild / friend / party adds
            //      that happen concurrently (those would otherwise "steal" the queued avatar
            //      and pair it with a dbId that doesn't belong to any nearby player).
            //
            // Both conditions together are exactly "strangers entering AOI" — the common
            // leaderboard case.  For guildmates / friends who are ALSO nearby the server
            // skips NewlyCreated (they were created when the friends list loaded, so their
            // NumCircles is already > 0) — we lose the name for those, but at least we
            // don't produce wildly-wrong pairings anymore.
            alreadyPaired = _dbIdByAvatarId.ContainsValue(e.PlayerDbId);

            if (e.IsInitial && hasNearbyBit && !alreadyPaired)
            {
                DateTime cutoff = e.UtcTime - AvatarBindingWindow;
                while (_pendingAvatarBindings.Count > 0 && _pendingAvatarBindings.Peek().UtcTime < cutoff)
                    _pendingAvatarBindings.Dequeue();

                if (_pendingAvatarBindings.Count > 0)
                {
                    var head = _pendingAvatarBindings.Dequeue();
                    _dbIdByAvatarId[head.AvatarEntityId] = e.PlayerDbId;
                    pairedAvatarId = head.AvatarEntityId;
                }
                else
                {
                    // Avatar EntityCreate hasn't arrived yet — park this dbId in the
                    // reverse queue so OnEntityCreated can pair with us when it does.
                    _pendingDbIdBindings.Enqueue((e.PlayerDbId, e.UtcTime));
                    while (_pendingDbIdBindings.Count > 32)
                        _pendingDbIdBindings.Dequeue();
                }
            }
        }

        if (firstTimeName)
            Diagnostic?.Invoke($"DpsMeter: learned nickname '{e.PlayerName}' for dbId 0x{e.PlayerDbId:X}");
        if (heroChanged)
            Diagnostic?.Invoke($"DpsMeter: community-slot hero for dbId 0x{e.PlayerDbId:X} = '{currentHeroName}' (ref=0x{e.CurrentAvatarRefId:X16})");
        if (nearbyAdded)
            Diagnostic?.Invoke($"DpsMeter: AOI-nearby add dbId=0x{e.PlayerDbId:X} (|nearby|={nearbyCountSnapshot})");
        if (pairedAvatarId != 0)
            Diagnostic?.Invoke($"DpsMeter: paired avatar entityId={pairedAvatarId} with dbId=0x{e.PlayerDbId:X} (nickname='{e.PlayerName}')");
        else if (e.IsInitial && hasNearbyBit && !alreadyPaired)
            Diagnostic?.Invoke($"DpsMeter: Nearby+NewlyCreated for dbId=0x{e.PlayerDbId:X} enqueued in reverse-pairing queue (avatar EntityCreate expected shortly)");

        // Debounced disk flush — no-op unless _playerIndexDirty was set under _sync above and
        // the last save was >= PlayerIndexSaveInterval ago.  Runs outside the lock since
        // File.WriteAllText can block on a busy disk; keeping it out of _sync avoids starving
        // the power-result / damage hot path that also wants the lock.
        SavePlayerIndex();
    }

    private void OnLocalAvatarObserved(object? sender, LocalAvatarObservedEvent e)
    {
        // Power-activation messages are the gold-standard local-avatar signal: only the local
        // client sends them, so idUserEntity is by construction YOUR current avatar id.  One
        // event per key press, so this is both fast to arrive (first combat input pins us) and
        // survives mid-session app launches where we missed NetMessageLocalPlayer.
        bool added;
        bool pinFlipped = false;
        ulong prevPin = 0;
        bool selfDbCaptured = false;
        bool selfDbRestored = false;
        ulong selfDbForLog = 0;

        lock (_sync)
        {
            added = _localAvatarEntityIds.Add(e.LocalAvatarEntityId);

            // Immediately flip the self-owner pin to this avatar — the user is OBVIOUSLY
            // playing it (they just pressed a key) so there's zero benefit to waiting for the
            // next DamageDealt event before switching.  OnDamageDealt will re-confirm, but
            // doing it here means the UI lights up with the right hero name / PB as soon as
            // the swap animation starts.
            if (_likelySelfOwnerId != e.LocalAvatarEntityId)
            {
                prevPin = _likelySelfOwnerId;
                _likelySelfOwnerId = e.LocalAvatarEntityId;
                _likelySelfChosenAt = e.UtcTime;
                pinFlipped = true;
            }

            // Back-fill the hero-name cache from the prototype map if the EntityCreate already
            // arrived for this avatar; otherwise the name will be populated lazily by the next
            // DamageDealt event that references this owner.
            if (!_heroNameByOwnerId.ContainsKey(e.LocalAvatarEntityId)
                && _prototypeByEntityId.TryGetValue(e.LocalAvatarEntityId, out uint proto)
                && proto != 0
                && HeroPrototypes.Names.TryGetValue(proto, out string? heroName))
            {
                _heroNameByOwnerId[e.LocalAvatarEntityId] = heroName;
            }

            // ── Self-dbId capture & restart-without-zone restore ────────────────────────
            // Two cases here, in priority order:
            //   1. Capture: the avatar's EntityCreate already arrived this session and bound
            //      its dbId in _dbIdByAvatarId — promote that to _selfDbId so future restarts
            //      can restore it from disk. Also covers Player-container path:
            //      _playerEntityIdByAvatarId → _dbIdByPlayerEntityId.
            //   2. Restore: this is a restart-without-zone scenario — the avatar's EntityCreate
            //      was NOT replayed by the server post-restart, so _dbIdByAvatarId is empty for
            //      this avatar. If we have a persisted _selfDbId from the last clean session,
            //      reapply it: _dbIdByAvatarId[localAvatarId] = _selfDbId. After this assignment
            //      the resolver's direct lookup at line ~2825 succeeds and the self row gets
            //      its real nickname instead of falling through to the disambiguator (which
            //      would mis-match a peer playing the same hero — see the "Blade misattribution"
            //      writeup in dps-meter-diagnostics.mdc).
            ulong knownDb = 0;
            if (_dbIdByAvatarId.TryGetValue(e.LocalAvatarEntityId, out knownDb) && knownDb != 0)
            {
                if (TryCaptureSelfDbIdLocked(knownDb))
                {
                    selfDbCaptured = true;
                    selfDbForLog = knownDb;
                }
            }
            else if (_playerEntityIdByAvatarId.TryGetValue(e.LocalAvatarEntityId, out ulong containerId)
                     && _dbIdByPlayerEntityId.TryGetValue(containerId, out knownDb)
                     && knownDb != 0)
            {
                if (TryCaptureSelfDbIdLocked(knownDb))
                {
                    selfDbCaptured = true;
                    selfDbForLog = knownDb;
                }
            }
            else if (_selfDbId != 0)
            {
                _dbIdByAvatarId[e.LocalAvatarEntityId] = _selfDbId;
                selfDbRestored = true;
                selfDbForLog = _selfDbId;
            }
        }

        if (added)
            Diagnostic?.Invoke($"DpsMeter: local avatar registered via power activation (id={e.LocalAvatarEntityId}) — authoritative mode ON");
        if (pinFlipped)
            Diagnostic?.Invoke($"DpsMeter: self-owner pinned {prevPin} -> {e.LocalAvatarEntityId} (from client power-activation)");
        if (selfDbCaptured)
        {
            Diagnostic?.Invoke($"DpsMeter: self-dbId captured 0x{selfDbForLog:X16} (avatar {e.LocalAvatarEntityId}) — persisting for cross-restart binding restore");
            SaveSelfIdentity();
        }
        if (selfDbRestored)
            Diagnostic?.Invoke($"DpsMeter: self-dbId 0x{selfDbForLog:X16} restored from disk to avatar {e.LocalAvatarEntityId} — restart-without-zone binding repaired (no peer mis-attribution this session)");

        // Immediately refresh UI-visible fields from the new pin so a hero-swap is reflected
        // without waiting for the next damage event to arrive.
        if (pinFlipped)
        {
            RefreshSelfAfterPinFlip();
            DpsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Re-reads <see cref="CurrentHeroDisplayName"/>, <see cref="MaxSingleHit"/>, and
    /// <see cref="CurrentOwnerTotal60s"/> from the pinned <c>_likelySelfOwnerId</c>. Used when
    /// the pin flips from a client-side observation (power activation / avatar swap) so the
    /// UI updates before the next damage tick.</summary>
    private void RefreshSelfAfterPinFlip()
    {
        lock (_sync)
        {
            string? selfHeroName = null;
            if (_likelySelfOwnerId != 0)
                _heroNameByOwnerId.TryGetValue(_likelySelfOwnerId, out selfHeroName);

            CurrentHeroDisplayName = selfHeroName ?? string.Empty;

            uint seeded = 0;
            if (!string.IsNullOrEmpty(selfHeroName))
                _maxHitByHeroName.TryGetValue(selfHeroName, out seeded);
            MaxSingleHit = seeded;

            CurrentOwnerTotal60s = _totalsPerOwner.TryGetValue(_likelySelfOwnerId, out long t) ? t : 0;
        }
    }

    private void OnLocalPlayerIdentified(object? sender, LocalPlayerIdentifiedEvent e)
    {
        bool selfDbCaptured = false;
        bool selfDbRestored = false;
        ulong selfDbForLog = 0;

        lock (_sync)
        {
            if (_localPlayerEntityId == e.LocalPlayerEntityId) return;
            ulong prevPlayerId = _localPlayerEntityId;
            _localPlayerEntityId = e.LocalPlayerEntityId;
            // Only clear avatar pins when we already had a *different* Player container id — i.e.
            // a real reconnect / session swap.  The very first LocalPlayer in a fresh DpsMeter's
            // life goes 0 → id; clearing here used to wipe ids that TryActivatePower had JUST
            // registered a few packets earlier (event order is not guaranteed), which made
            // ownerIsSelf false for every self hit until the user pressed another key.
            if (prevPlayerId != 0)
                _localAvatarEntityIds.Clear();

            // Self-dbId capture/restore via the Player-container path (mirror of the avatar
            // path in OnLocalAvatarObserved — see the comment block there for the full
            // motivation). NetMessageLocalPlayer fires once per session at sniffer attach,
            // and crucially does NOT re-fire on a meter restart-without-zone (the server only
            // sends LocalPlayer on the initial connect), so this branch handles the "fresh
            // session, container EntityCreate already arrived" capture case. The restore
            // branch is here for symmetry — in practice OnLocalAvatarObserved restores first
            // because power-activation arrives on the very next combat input, while
            // LocalPlayer only re-fires on a hard reconnect.
            if (_dbIdByPlayerEntityId.TryGetValue(e.LocalPlayerEntityId, out ulong knownDb)
                && knownDb != 0)
            {
                if (TryCaptureSelfDbIdLocked(knownDb))
                {
                    selfDbCaptured = true;
                    selfDbForLog = knownDb;
                }
            }
            else if (_selfDbId != 0)
            {
                _dbIdByPlayerEntityId[e.LocalPlayerEntityId] = _selfDbId;
                selfDbRestored = true;
                selfDbForLog = _selfDbId;
            }
        }
        Diagnostic?.Invoke($"DpsMeter: local player identified (id={e.LocalPlayerEntityId}) — enabling authoritative self-owner pinning");
        if (selfDbCaptured)
        {
            Diagnostic?.Invoke($"DpsMeter: self-dbId captured 0x{selfDbForLog:X16} (player container {e.LocalPlayerEntityId}) — persisting for cross-restart binding restore");
            SaveSelfIdentity();
        }
        if (selfDbRestored)
            Diagnostic?.Invoke($"DpsMeter: self-dbId 0x{selfDbForLog:X16} restored from disk to player container {e.LocalPlayerEntityId}");
    }

    private void OnInventoryMoved(object? sender, InventoryMovedEvent e)
    {
        // Two orthogonal concerns are handled here:
        //   (1) Authoritative self-identification for the LOCAL player (must match
        //       _localPlayerEntityId).  Self-identification is the critical path for
        //       disambiguating "which avatar is YOU" and drives the whole main-window
        //       DPS pin — see OnDamageDealt.
        //   (2) Nickname resolution for OTHER nearby players: we record
        //       `avatar → containingPlayer` for every hero-prototype entity whose container
        //       we know, so the leaderboard can later translate it to a nickname via
        //       dbId → name.  This populates strictly more pairs than (1) does, hence two
        //       separate branches.
        bool added = false;
        bool isHeroProto = false;
        string? heroName = null;

        lock (_sync)
        {
            // Branch (2): nickname-resolution book-keeping.  Any hero-prototype entity that
            // moves into a container we've observed before is presumed to be an avatar whose
            // ultimate damage credit will belong to that container.  We DON'T require the
            // container to be the local player — that's how we resolve other players on the
            // leaderboard.  We DO require the moved entity to resolve to a known hero
            // prototype, to avoid polluting the map with equipment-move edges.
            if (_prototypeByEntityId.TryGetValue(e.EntityId, out uint proto)
                && proto != 0
                && HeroPrototypes.Names.TryGetValue(proto, out heroName))
            {
                isHeroProto = true;
                _playerEntityIdByAvatarId[e.EntityId] = e.ContainerEntityId;
            }

            // Branch (1): self-avatar registration only fires when the container is the local
            // Player.  If we haven't seen NetMessageLocalPlayer yet we silently fall back to the
            // heuristic mode (OnDamageDealt uses _localAvatarEntityIds.Count to gate it).
            if (_localPlayerEntityId != 0
                && e.ContainerEntityId == _localPlayerEntityId
                && isHeroProto
                && heroName is not null)
            {
                added = _localAvatarEntityIds.Add(e.EntityId);
                // Also seed the hero-name cache so the overlay title can fill in before the
                // first damage event from this avatar arrives.
                _heroNameByOwnerId[e.EntityId] = heroName;
            }
        }

        if (added)
            Diagnostic?.Invoke($"DpsMeter: local avatar registered (id={e.EntityId}, hero='{heroName}', inventory {e.InventoryPrototypeId}, slot {e.Slot})");
    }

    private void OnEntityCreated(object? sender, EntityCreatedEvent e)
    {
        // Tracked on a separate concurrent map rather than under _sync to keep this callback
        // lock-free — it runs on the sniffer's capture thread and can fire thousands of times
        // during a map transition. Reads happen inside OnDamageDealt/Tick which hold _sync, but
        // ConcurrentDictionary makes that race-free without needing to upgrade this write path.
        _prototypeByEntityId[e.EntityId] = e.PrototypeEnumIndex;

        // Two concurrent book-keeping actions, both guarded by _sync:
        //   (a) Player containers carry HasDbId in the EntityCreate header — that's our LOCAL
        //       player most of the time (remote Player containers are never proximity-pushed).
        //       Record (playerEntityId → dbId) for the local-player resolution path.
        //   (b) Hero-prototype avatars are the things that emit damage.  Queue them for
        //       temporal pairing with the upcoming ModifyCommunityMember(IsInitial=true)
        //       broadcast, which the server always sends immediately after the avatar's
        //       EntityCreate (see AreaOfInterest.AddEntity).  This is the REMOTE-player
        //       resolution path — the local-player path above doesn't help here because we
        //       never receive the remote Player's EntityCreate.
        bool enqueued = false;
        ulong pairedDbId = 0;
        bool directBind  = false;          // set when we resolved nick straight from the archive
        bool selfDbCapturedFromContainer = false;
        ulong selfDbForLog = 0;
        // Self-dbId healing — set when an authoritative EntityCreate proves the persisted
        // _selfDbId loaded from dps-self.json was poisoned by a peer Player container during
        // a previous busy-hub session. Two healing channels:
        //   (a) selfDbHealedFromLocalAvatar — local avatar's OwnerPlayerDbId disagrees with
        //       _selfDbId. Local avatar's OwnerPlayerDbId is server-authoritative, so it
        //       wins. Used post-zone (avatar EntityCreate replays after region change).
        //   (b) selfDbPoisonDetected — peer's avatar OwnerPlayerDbId == _selfDbId, proving
        //       _selfDbId actually belongs to the peer (we restored a poisoned value last
        //       session). _selfDbId is wiped and the on-disk dps-self.json is deleted; any
        //       local-avatar bindings to the poisoned value are also cleared so the resolver
        //       falls back to its disambiguator instead of returning the peer's nickname.
        bool selfDbHealedFromLocalAvatar = false;
        ulong selfDbHealedOldValue = 0;
        bool selfDbPoisonDetected = false;
        ulong selfDbPoisonValue = 0;
        ulong selfDbPoisonPeerEntityId = 0;
        if (e.DatabaseUniqueId != 0 || e.IsAvatar)
        {
            lock (_sync)
            {
                if (e.DatabaseUniqueId != 0)
                {
                    _dbIdByPlayerEntityId[e.EntityId] = e.DatabaseUniqueId;
                    // Self-dbId capture from Player-container EntityCreate. Only safe when
                    // NetMessageLocalPlayer has ALREADY identified this exact entity as ours.
                    //
                    // The previous "speculative first-write fallback" (which captured anything
                    // when _localPlayerEntityId == 0) was REMOVED after live evidence in
                    // dps-meter.log lines 58501-58503: three peer Player container EntityCreates
                    // arrived within 10 ms in a busy hub, all three captured as _selfDbId in
                    // sequence, the last one persisted to dps-self.json, then restored to the
                    // local avatar across multiple subsequent restarts. Result: every damage hit
                    // for hours was credited to the wrong peer (see "Apok mis-recognition" in
                    // dps-meter-diagnostics.mdc). The original assumption — "remote Player
                    // containers are never proximity-pushed" — was simply wrong; the server
                    // does broadcast peer Player containers in social areas / hubs.
                    //
                    // Without this branch, _selfDbId is captured only from server-authoritative
                    // signals: NetMessageLocalPlayer (matches a known-local container) and the
                    // local avatar's OwnerPlayerDbId at EntityCreate time (Branch A in
                    // OnLocalAvatarObserved). The lazy capture path in OnLocalPlayerIdentified
                    // covers any reverse-order case where the LocalPlayer message arrives before
                    // its matching container EntityCreate (rare in practice).
                    if (_localPlayerEntityId != 0 && _localPlayerEntityId == e.EntityId)
                    {
                        if (TryCaptureSelfDbIdLocked(e.DatabaseUniqueId))
                        {
                            selfDbCapturedFromContainer = true;
                            selfDbForLog = e.DatabaseUniqueId;
                        }
                    }
                }

                // Fast path: the sniffer managed to extract the nickname + owner dbId
                // directly from the Avatar's transient archive (see
                // ScanAvatarPlayerName).  This bypasses the entire
                // ModifyCommunityMember temporal correlation and — crucially — works
                // for players already in your Guild/Friends circle, whose
                // community-member broadcast is silent on PlayerName (Community.cs
                // only sets NewlyCreated for members with zero prior circles).  The
                // above fallback path still runs for avatars whose archive the scanner
                // couldn't decode confidently (truncated blob, unusual name shape).
                if (e.IsAvatar && e.OwnerPlayerDbId != 0 && !string.IsNullOrEmpty(e.PlayerName))
                {
                    // OwnerPlayerDbId is server-authoritative — it ALWAYS wins over any
                    // existing _dbIdByAvatarId binding, even if the existing one came from
                    // the persisted-restore path in OnLocalAvatarObserved (that path could
                    // have applied a poisoned _selfDbId to this exact avatar before the
                    // server's EntityCreate arrived). Overwriting on disagreement is the
                    // primary healing channel for the "Apok mis-recognition" failure mode.
                    bool wasBound = _dbIdByAvatarId.TryGetValue(e.EntityId, out ulong existingDb);
                    bool overwrite = !wasBound || existingDb != e.OwnerPlayerDbId;
                    if (overwrite)
                    {
                        _dbIdByAvatarId[e.EntityId]          = e.OwnerPlayerDbId;
                        _playerNameByDbId[e.OwnerPlayerDbId] = e.PlayerName;
                        // Also record the hero name so the persisted index can help future
                        // mid-session launches — dbId → hero is exactly the pairing we need
                        // for the _currentHeroNameByDbId fallback in GetTopHeroesBy60sShare.
                        if (HeroPrototypes.Names.TryGetValue(e.PrototypeEnumIndex, out string? scannedHero))
                            _currentHeroNameByDbId[e.OwnerPlayerDbId] = scannedHero;
                        MarkPlayerIndexDirty(e.OwnerPlayerDbId);
                        pairedDbId = e.OwnerPlayerDbId;
                        directBind = true;
                    }

                    // Self-dbId healing channel (a): if this avatar is one of OUR registered
                    // local avatars and its server-authoritative OwnerPlayerDbId differs
                    // from the persisted _selfDbId, the persisted value is stale/poisoned.
                    // Replace it from the authoritative source and re-persist so the next
                    // restart-without-zone restores the correct value.
                    if (_localAvatarEntityIds.Contains(e.EntityId)
                        && _selfDbId != 0
                        && _selfDbId != e.OwnerPlayerDbId)
                    {
                        selfDbHealedOldValue = _selfDbId;
                        _selfDbId = e.OwnerPlayerDbId;
                        selfDbHealedFromLocalAvatar = true;
                        selfDbForLog = e.OwnerPlayerDbId;
                    }

                    // Self-dbId healing channel (b): a PEER's avatar EntityCreate proves
                    // their account dbId is exactly the value we restored from dps-self.json
                    // — i.e. the persisted file was written during a previous session by the
                    // (now-removed) speculative capture branch grabbing this peer's Player
                    // container EntityCreate. Wipe _selfDbId, drop any local-avatar bindings
                    // to it (so the resolver doesn't keep returning the peer's nickname for
                    // our row), and delete the poisoned file. The user's own dbId will be
                    // re-captured on the next zone via channel (a) above.
                    if (e.OwnerPlayerDbId == _selfDbId
                        && _selfDbId != 0
                        && !_localAvatarEntityIds.Contains(e.EntityId))
                    {
                        selfDbPoisonValue = _selfDbId;
                        selfDbPoisonPeerEntityId = e.EntityId;
                        selfDbPoisonDetected = true;
                        _selfDbId = 0;
                        // Scrub any local-avatar bindings that came from the poisoned restore
                        // path — these would otherwise keep returning the peer's nick after
                        // we wipe _selfDbId, since the binding itself still survives.
                        if (_localAvatarEntityIds.Count > 0)
                        {
                            foreach (ulong localAvatarId in _localAvatarEntityIds)
                            {
                                if (_dbIdByAvatarId.TryGetValue(localAvatarId, out ulong boundDb)
                                    && boundDb == selfDbPoisonValue)
                                {
                                    _dbIdByAvatarId.Remove(localAvatarId);
                                }
                            }
                        }
                    }
                }

                // Queue EVERY avatar — whether we recognize its prototype index or not.
                // HeroPrototypes.Names is a best-effort static dump; missing entries (newer
                // heroes, costume-variant protos, etc.) used to cause the queue to stay
                // empty exactly when ModifyCommunityMember wanted to pair with it.  The
                // server-authoritative IsAvatar flag is the correct signal.
                if (e.IsAvatar && !directBind && !_dbIdByAvatarId.ContainsKey(e.EntityId))
                {
                    // First: look for a recently-learned dbId+name that was waiting for
                    // its avatar (reverse-order case).  Evict stale entries before
                    // peeking so a long gap doesn't mis-pair a fresh avatar with an old
                    // name.
                    DateTime cutoff = e.UtcTime - AvatarBindingWindow;
                    while (_pendingDbIdBindings.Count > 0 && _pendingDbIdBindings.Peek().UtcTime < cutoff)
                        _pendingDbIdBindings.Dequeue();

                    if (_pendingDbIdBindings.Count > 0)
                    {
                        var head = _pendingDbIdBindings.Dequeue();
                        _dbIdByAvatarId[e.EntityId] = head.DbId;
                        pairedDbId = head.DbId;
                    }
                    else
                    {
                        // No waiting dbId — enqueue this avatar for a future
                        // ModifyCommunityMember to consume.
                        _pendingAvatarBindings.Enqueue((e.EntityId, e.UtcTime));
                        enqueued = true;
                        // Keep the queue bounded — a burst AOI update (teleport to social
                        // hub) could otherwise stack dozens of entries. 32 is comfortably
                        // above the realistic number of nearby players in any game mode.
                        while (_pendingAvatarBindings.Count > 32)
                            _pendingAvatarBindings.Dequeue();
                    }
                }
            }
        }

        if (enqueued)
        {
            string heroName = HeroPrototypes.Names.TryGetValue(e.PrototypeEnumIndex, out var n) ? n : $"<protoIdx {e.PrototypeEnumIndex}>";
            // protoIdx is logged alongside the resolved name so we can diff against the
            // dumper output when a mis-identification is reported (e.g. "War Machine shown as
            // Kitty Pryde"). Without it, there's no way to tell whether the wrong index came
            // off the wire or whether HeroPrototypes.Names is stale.
            Diagnostic?.Invoke($"DpsMeter: queued hero avatar for nickname pairing - entityId={e.EntityId}, protoIdx={e.PrototypeEnumIndex}, hero='{heroName}', dbId={e.DatabaseUniqueId} (own player: {(e.DatabaseUniqueId != 0 ? "yes" : "no")})");
        }
        if (pairedDbId != 0)
        {
            string heroName = HeroPrototypes.Names.TryGetValue(e.PrototypeEnumIndex, out var n) ? n : $"<protoIdx {e.PrototypeEnumIndex}>";
            string nick;
            lock (_sync) _playerNameByDbId.TryGetValue(pairedDbId, out nick!);
            string via = directBind ? "archive fast-path" : "reverse-order queue";
            Diagnostic?.Invoke($"DpsMeter: paired avatar entityId={e.EntityId} ('{heroName}') with dbId=0x{pairedDbId:X} (nickname='{nick ?? ""}') via {via}");
            SavePlayerIndex();
        }
        if (selfDbCapturedFromContainer)
        {
            Diagnostic?.Invoke($"DpsMeter: self-dbId captured 0x{selfDbForLog:X16} (player container EntityCreate {e.EntityId}) — persisting for cross-restart binding restore");
            SaveSelfIdentity();
        }
        if (selfDbHealedFromLocalAvatar)
        {
            Diagnostic?.Invoke($"DpsMeter: self-dbId healed 0x{selfDbHealedOldValue:X16} -> 0x{selfDbForLog:X16} (local avatar {e.EntityId} OwnerPlayerDbId disagrees with persisted value — previous dps-self.json was stale or poisoned by a peer Player container in an earlier session)");
            SaveSelfIdentity();
        }
        if (selfDbPoisonDetected)
        {
            Diagnostic?.Invoke($"DpsMeter: POISON DETECTED — persisted _selfDbId 0x{selfDbPoisonValue:X16} matches peer avatar {selfDbPoisonPeerEntityId} (proto {e.PrototypeEnumIndex}, nick='{e.PlayerName}') OwnerPlayerDbId. Wiping in-memory _selfDbId, scrubbing local-avatar bindings, deleting dps-self.json — next zone will repopulate from authoritative source. (Root cause: a previous session's speculative Player-container capture branch — removed in this build — grabbed this peer's container as our own.)");
            TryDeleteSelfIdentityFile();
        }
    }

    /// <summary>Best-effort delete of <see cref="SelfIdentityPath"/>. Called when a peer's
    /// avatar EntityCreate proves the persisted self-dbId actually belongs to that peer
    /// (poison detection in <see cref="OnEntityCreated"/>). Failures are silent — worst case
    /// the next session re-detects the same poison and re-deletes; no functional harm.</summary>
    private void TryDeleteSelfIdentityFile()
    {
        try
        {
            if (File.Exists(SelfIdentityPath))
                File.Delete(SelfIdentityPath);
        }
        catch (Exception ex)
        {
            Diagnostic?.Invoke($"DpsMeter: failed to delete poisoned self-identity file: {ex.Message}");
        }
    }

    private void LoadMaxHits()
    {
        try
        {
            if (!File.Exists(MaxHitsPath)) return;
            var json = File.ReadAllText(MaxHitsPath);
            var loaded = JsonSerializer.Deserialize<Dictionary<string, uint>>(json);
            if (loaded is null) return;

            // The file format used to key entries by numeric prototype-enum index; we migrated to
            // hero display names for robustness across the two identification channels (see the
            // two-channel comment above).  To preserve records captured pre-migration, resolve any
            // all-digit keys through HeroPrototypes.Names.  Unknown numeric keys are silently
            // dropped — they were for test/dev avatars the user is unlikely to be tracking.
            int migrated = 0;
            lock (_sync)
            {
                _maxHitByHeroName.Clear();
                foreach (var kv in loaded)
                {
                    string key = kv.Key;
                    if (uint.TryParse(key, out uint legacyProtoIdx)
                        && HeroPrototypes.Names.TryGetValue(legacyProtoIdx, out string? migratedName))
                    {
                        key = migratedName;
                        migrated++;
                    }
                    // Last-writer-wins if both legacy numeric AND string entries exist for the
                    // same hero in the same file (shouldn't happen but harmless if it does).
                    _maxHitByHeroName[key] = kv.Value;
                }
            }
            Diagnostic?.Invoke($"DpsMeter: loaded {_maxHitByHeroName.Count} hero max-hit records from {MaxHitsPath} (migrated {migrated} legacy numeric keys)");
        }
        catch (Exception ex)
        {
            Diagnostic?.Invoke($"DpsMeter: failed to load max-hits file: {ex.Message}");
        }
    }

    private void SaveMaxHits()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(MaxHitsPath)!);
            Dictionary<string, uint> snapshot;
            lock (_sync)
            {
                snapshot = new Dictionary<string, uint>(_maxHitByHeroName, StringComparer.Ordinal);
            }
            File.WriteAllText(MaxHitsPath, JsonSerializer.Serialize(snapshot));
        }
        catch (Exception ex)
        {
            Diagnostic?.Invoke($"DpsMeter: failed to save max-hits file: {ex.Message}");
        }
    }

    /// <summary>Merge the disk cache into <see cref="_playerNameByDbId"/> and
    /// <see cref="_currentHeroNameByDbId"/> at startup.  Entries older than
    /// <see cref="PlayerIndexTtl"/> are dropped — for a dbId that's been inactive for a month
    /// the cached hero is almost certainly wrong (players rotate avatars all the time) and the
    /// cached name is unlikely to be meaningful in the current encounter.</summary>
    private void LoadPlayerIndex()
    {
        try
        {
            if (!File.Exists(PlayerIndexPath)) return;
            var json = File.ReadAllText(PlayerIndexPath);
            // String-keyed on disk so the hex dbId is human-readable — the file is small enough
            // that reserializing on every write is cheap, and debugging is much nicer when the
            // key matches what the wire logs print.
            var loaded = JsonSerializer.Deserialize<Dictionary<string, PlayerIndexEntry>>(json);
            if (loaded is null) return;

            DateTime cutoff = DateTime.UtcNow - PlayerIndexTtl;
            int nameCount = 0, heroCount = 0, expired = 0;
            lock (_sync)
            {
                foreach (var kv in loaded)
                {
                    if (kv.Value == null) continue;
                    if (kv.Value.LastSeenUtc < cutoff) { expired++; continue; }

                    // Accept both "0x..." and raw decimal forms — earlier builds might have
                    // written either, and users occasionally hand-edit the file.
                    string keyStr = kv.Key.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                        ? kv.Key.Substring(2) : kv.Key;
                    if (!ulong.TryParse(keyStr, System.Globalization.NumberStyles.HexNumber,
                            System.Globalization.CultureInfo.InvariantCulture, out ulong dbId)
                        && !ulong.TryParse(kv.Key, out dbId))
                        continue;
                    if (dbId == 0) continue;

                    if (!string.IsNullOrEmpty(kv.Value.Name))
                    {
                        _playerNameByDbId[dbId] = kv.Value.Name!;
                        nameCount++;
                    }
                    if (!string.IsNullOrEmpty(kv.Value.Hero))
                    {
                        _currentHeroNameByDbId[dbId] = kv.Value.Hero!;
                        heroCount++;
                    }
                    _playerIndexLastSeen[dbId] = kv.Value.LastSeenUtc;
                }
            }
            Diagnostic?.Invoke($"DpsMeter: loaded player-index from {PlayerIndexPath}: {nameCount} names, {heroCount} heroes, {expired} expired entries dropped");
        }
        catch (Exception ex)
        {
            Diagnostic?.Invoke($"DpsMeter: failed to load player-index: {ex.Message}");
        }
    }

    /// <summary>Flush <see cref="_playerNameByDbId"/> + <see cref="_currentHeroNameByDbId"/> +
    /// <see cref="_playerIndexLastSeen"/> to disk.  Serialized union of the three maps — a dbId
    /// shows up in the file as long as at least one of (name, hero) is known. Debounce via
    /// <paramref name="force"/>: non-forced calls are no-ops if the last save is recent, so
    /// we don't hammer the disk during the initial CommunityMember burst on region load.</summary>
    private void SavePlayerIndex(bool force = false)
    {
        try
        {
            DateTime nowUtc = DateTime.UtcNow;
            lock (_sync)
            {
                if (!_playerIndexDirty) return;
                if (!force && (nowUtc - _playerIndexLastSavedUtc) < PlayerIndexSaveInterval) return;

                Directory.CreateDirectory(Path.GetDirectoryName(PlayerIndexPath)!);

                var snapshot = new Dictionary<string, PlayerIndexEntry>(StringComparer.Ordinal);
                // Emit the union of dbIds present in any of the three maps — partial knowledge
                // is still useful for the fallback (e.g. we know the name but not the hero,
                // or vice versa).
                var allKeys = new HashSet<ulong>(_playerNameByDbId.Keys);
                foreach (var k in _currentHeroNameByDbId.Keys) allKeys.Add(k);
                foreach (var k in _playerIndexLastSeen.Keys)    allKeys.Add(k);

                foreach (ulong dbId in allKeys)
                {
                    _playerNameByDbId.TryGetValue(dbId, out string? name);
                    _currentHeroNameByDbId.TryGetValue(dbId, out string? hero);
                    _playerIndexLastSeen.TryGetValue(dbId, out DateTime seen);
                    if (seen == default) seen = nowUtc;

                    snapshot[$"0x{dbId:X16}"] = new PlayerIndexEntry
                    {
                        Name = name,
                        Hero = hero,
                        LastSeenUtc = seen,
                    };
                }

                File.WriteAllText(PlayerIndexPath,
                    JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));

                _playerIndexDirty = false;
                _playerIndexLastSavedUtc = nowUtc;
            }
        }
        catch (Exception ex)
        {
            Diagnostic?.Invoke($"DpsMeter: failed to save player-index: {ex.Message}");
        }
    }

    /// <summary>Mark the index dirty and bump <c>LastSeenUtc</c> for this dbId.  Must be called
    /// under <c>_sync</c> — callers in <see cref="OnCommunityMemberUpdated"/> already hold it.
    /// The actual write happens on the next <see cref="SavePlayerIndex"/> call (debounced).</summary>
    private void MarkPlayerIndexDirty(ulong dbId)
    {
        if (dbId == 0) return;
        _playerIndexLastSeen[dbId] = DateTime.UtcNow;
        _playerIndexDirty = true;
    }

    /// <summary>Public hook for <c>DpsOverlayPresenter</c> to trigger the debounced save on each
    /// decay tick, so the file converges even during calm community-broadcast periods.</summary>
    public void FlushPlayerIndexIfDirty() => SavePlayerIndex(force: false);

    /// <summary>Synchronous flush used on clean shutdown: bypasses the
    /// <see cref="PlayerIndexSaveInterval"/> debounce so in-flight mutations don't get lost
    /// when the host app is closing within a few seconds of the last community broadcast.</summary>
    public void FlushPlayerIndexNow() => SavePlayerIndex(force: true);

    /// <summary>Load the persisted self-dbId from <see cref="SelfIdentityPath"/>, if any.
    /// Failures are silent — a missing or corrupt file is no different from "first run on this
    /// box", which is a normal state on day one. The file is one-shot per session: we read it
    /// at startup, then any new self-dbId observation overwrites it.</summary>
    private void LoadSelfIdentity()
    {
        try
        {
            if (!File.Exists(SelfIdentityPath)) return;
            var json = File.ReadAllText(SelfIdentityPath);
            var loaded = JsonSerializer.Deserialize<SelfIdentityFile>(json);
            if (loaded?.DbId is null) return;

            string keyStr = loaded.DbId.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? loaded.DbId.Substring(2) : loaded.DbId;
            if (!ulong.TryParse(keyStr, System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out ulong dbId)
                && !ulong.TryParse(loaded.DbId, out dbId))
                return;
            if (dbId == 0) return;

            lock (_sync) _selfDbId = dbId;
            Diagnostic?.Invoke($"DpsMeter: loaded self-dbId 0x{dbId:X16} from {SelfIdentityPath} (nick='{loaded.Nick ?? string.Empty}', last updated {loaded.LastUpdatedUtc:O}) — restart-without-zone bindings will be restored on first self-pin");
        }
        catch (Exception ex)
        {
            Diagnostic?.Invoke($"DpsMeter: failed to load self-identity: {ex.Message}");
        }
    }

    /// <summary>Persist <see cref="_selfDbId"/> + the currently-resolved self-nick to
    /// <see cref="SelfIdentityPath"/>. Cheap (single-shot tiny JSON) so we don't bother
    /// debouncing — every "we just learned the self-dbId" signal calls this directly.</summary>
    private void SaveSelfIdentity()
    {
        try
        {
            ulong dbIdSnapshot;
            string? nickSnapshot;
            lock (_sync)
            {
                dbIdSnapshot = _selfDbId;
                nickSnapshot = null;
                if (dbIdSnapshot != 0)
                    _playerNameByDbId.TryGetValue(dbIdSnapshot, out nickSnapshot);
            }
            if (dbIdSnapshot == 0) return;

            Directory.CreateDirectory(Path.GetDirectoryName(SelfIdentityPath)!);
            var snapshot = new SelfIdentityFile
            {
                DbId = $"0x{dbIdSnapshot:X16}",
                Nick = nickSnapshot,
                LastUpdatedUtc = DateTime.UtcNow,
            };
            File.WriteAllText(SelfIdentityPath,
                JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            Diagnostic?.Invoke($"DpsMeter: failed to save self-identity: {ex.Message}");
        }
    }

    /// <summary>Centralised "we just learned our own account dbId" capture point. Idempotent —
    /// no-op if the value matches the existing <see cref="_selfDbId"/>. Returns true on first
    /// capture or change so callers can emit a one-shot diagnostic + persist. MUST be called
    /// under <c>_sync</c>.</summary>
    private bool TryCaptureSelfDbIdLocked(ulong dbId)
    {
        if (dbId == 0) return false;
        if (_selfDbId == dbId) return false;
        _selfDbId = dbId;
        return true;
    }

    private void OnDamageDealt(object? sender, DamageDealtEvent e)
    {
        // Ignore events with no credited owner or zero damage (these are typically healing ticks
        // or "unaffected"/dodged hits that report 0 / 0 / 0).  The 0 check also throws away
        // non-damage rows that still emit PowerResult (e.g. pure healing on allies).
        uint dmg = e.TotalDamage;
        ulong wireUlt = e.UltimateOwnerEntityId;
        ulong wirePow = e.PowerOwnerEntityId;
        // Some PowerResult archives omit ultimate owner (NoUltimateOwnerEntityId) but still carry
        // a valid power owner — previously we dropped those entirely (60s stayed at 0).
        ulong rawOwner = wireUlt != 0 ? wireUlt : wirePow;
        if (dmg == 0 || rawOwner == 0)
            return;

        // ── Non-combatant filter (NORMAL DPS MODE ONLY, runs BEFORE the lock) ─────────────────
        // Drop hits on environmental destructibles (PropPrototype / DestructiblePropPrototype —
        // crates, vases, breakable doors), world Item entities, and any other WorldEntityPrototype
        // descendant that the game does NOT classify as an AgentPrototype (see
        // CombatantPrototypes for the full agent hierarchy: avatars, mobs, bosses, team-ups, orbs,
        // missiles, smart props).  Without this filter, AOEs that splash on crates inflate the
        // user's DPS — every crate hit shows up as ~21k physical damage in the live PowerResult
        // archive, and a single big AOE on a loot room can dump millions of fake DPS into the
        // 60-second window.  Boss-only mode has its own narrower filter via BossPrototypes
        // (which already excludes everything that's not a boss target), so we explicitly skip
        // this gate when boss-only is on to avoid double-filtering.
        //
        // <b>Unknown-target policy: DROP (opposite of boss mode).</b>  Earlier iterations of this
        // filter admitted unknown targets to mirror boss-mode's optimistic policy, but production
        // logs showed that crate / vase / breakable-door EntityCreates are by far the most common
        // cache misses (their EntityCreates often get lost in the post-region-change burst) and
        // admitting them was defeating the entire purpose of the filter.  Real mob hits virtually
        // always have a cached prototype because mobs have a non-trivial spawn animation that
        // gives the sniffer plenty of time to process the EntityCreate before the first hit lands.
        // The trade-off (a real mob hitting before its EntityCreate is processed gets dropped) is
        // bounded to the first ~1s of any fight and is barely visible in the 60s sliding window.
        // Boss mode keeps admit-unknown because hit volume is low and the cost of dropping a real
        // boss is much higher than the cost of admitting an unknown environmental hit.
        if (!_bossOnlyMode && e.TargetEntityId != 0)
        {
            if (_prototypeByEntityId.TryGetValue(e.TargetEntityId, out uint normalTargetProtoIdx))
            {
                if (!CombatantPrototypes.IsCombatant(normalTargetProtoIdx))
                {
                    if (_loggedNonCombatantTargets.Add(normalTargetProtoIdx))
                        Diagnostic?.Invoke($"DpsMeter: non-combatant filter drop — target protoIdx={normalTargetProtoIdx} is not in CombatantPrototypes set (PropPrototype / DestructiblePropPrototype / item / non-agent record); damage dropped from normal-mode DPS so crates, vases, breakable doors, etc. don't inflate the 60-second window");
                    return;
                }

                // Admit path — emit a one-shot diagnostic per distinct protoIdx so we have full
                // visibility into "what is actually being counted as DPS" without needing to
                // change code or re-build.  Without this, drops are logged but admits are silent
                // and a user reporting "objects damage still being counted" can't be diagnosed
                // because we don't know which prototype is the culprit.  See
                // <see cref="_loggedAdmittedNormalTargets"/> for the dedup contract; the cap
                // exists so a 60-mob patrol pull doesn't dump 60 lines into the log.
                if (_loggedAdmittedNormalTargets.Count < AdmittedTargetLogCap
                    && _loggedAdmittedNormalTargets.Add(normalTargetProtoIdx))
                {
                    Diagnostic?.Invoke($"DpsMeter: combatant filter admit — target protoIdx={normalTargetProtoIdx} (entityId={e.TargetEntityId}, dmg={e.TotalDamage}). First admit for this prototype this region; this is what's being counted toward your normal-mode DPS. If this looks wrong (orb pickup, projectile, environmental smart-prop), paste this line and we'll move the prototype out of CombatantPrototypes.");
                }
            }
            else
            {
                if (_loggedUnknownNormalTargets.Count < UnknownTargetLogCap
                    && _loggedUnknownNormalTargets.Add(e.TargetEntityId))
                {
                    Diagnostic?.Invoke($"DpsMeter: non-combatant filter drop (unknown prototype) — target entityId={e.TargetEntityId} (EntityCreate not yet observed; dropping conservatively because cache misses on this branch are dominated by environmental destructibles whose EntityCreates were lost in the post-region-change burst). If a real mob is being dropped here, its prototype is missing from CombatantPrototypes — paste this entityId into a probe report and we'll add it.");
                }
                return;
            }
        }

        // ── Boss-only filter (runs BEFORE the lock so we don't churn window state for hits we
        // immediately throw away).  We resolve target → prototypeEnumIndex via the same cache the
        // main resolver uses; if the target's EntityCreate was missed, its prototype is unknown
        // and we conservatively drop the hit rather than admit a possibly-trash mob.
        //
        // The rejection paths ALSO emit a one-shot diagnostic per unique target so "I'm hitting a
        // named mob but the meter stays at 0" is trivially debuggable: the user pastes the log,
        // we see the exact protoIdx, and we can either widen the filter (e.g. add Elite) or
        // retroactively mark a specific mob as boss if the game-data classification disagrees with
        // player intuition (named mobs with yellow `!` aren't always rank==Boss on the server).
        if (_bossOnlyMode)
        {
            if (e.TargetEntityId == 0) return;
            if (!_prototypeByEntityId.TryGetValue(e.TargetEntityId, out uint targetProtoIdx))
            {
                // Unknown prototype (we missed this target's EntityCreate — usually because the
                // app attached mid-fight or we lost a packet). Empirically this is the most
                // common reason a real boss fails the strict classification: the boss spawned
                // during patrol events BEFORE we got its protoIdx cached. Admit optimistically —
                // the worst case is a few seconds of inflated numbers during region ramp-up,
                // which is still closer to user intent ("I'm hitting this important thing") than
                // dropping a whole boss encounter silently.
                if (_loggedUnknownBossTargets.Add(e.TargetEntityId))
                    Diagnostic?.Invoke($"DpsMeter: boss-filter admit (unknown prototype) — target entityId={e.TargetEntityId} (EntityCreate missed; counting optimistically)");
                // fall through to the normal scoring path
            }
            else if (BossPrototypes.TryClassifyBoss(targetProtoIdx, out bool admittedViaShift))
            {
                // Boss admitted — fall through to scoring.  If the only reason the lookup
                // matched was the dumper off-by-one workaround (see BossPrototypes.IsBoss
                // remarks), surface that fact in the log exactly once per distinct prototype
                // so users can grep "off-by-one" admits and know which entries to add to the
                // canonical list when the dumper is eventually fixed and regenerated.
                if (admittedViaShift && _loggedOffByOneBossAdmits.Add(targetProtoIdx))
                    Diagnostic?.Invoke($"DpsMeter: boss-filter admit (off-by-one fallback) — target protoIdx={targetProtoIdx} matched as protoIdx={targetProtoIdx - 1u} via dumper-bug compensation; treat the literal value as canonical when regenerating BossPrototypes");
            }
            else
            {
                if (_loggedNonBossTargets.Add(targetProtoIdx))
                {
                    // Differentiate "rank=MiniBoss (excluded by design)" from "not in any boss set".
                    // The MiniBoss path is the common one in patrol zones (Hand Ninja named mobs,
                    // terminal elites) and is intentional — the diagnostic spells that out so users
                    // don't think their hit was lost to a bug.
                    if (BossPrototypes.IsMiniBoss(targetProtoIdx))
                        Diagnostic?.Invoke($"DpsMeter: boss-filter drop — target protoIdx={targetProtoIdx} is rank=MiniBoss (excluded from boss-only mode by design; see BossPrototypes.MiniBossIndices)");
                    else
                        Diagnostic?.Invoke($"DpsMeter: boss-filter drop — target protoIdx={targetProtoIdx} is not in BossPrototypes set (Boss+GroupBoss). Add it if you expected it to count.");
                }
                // Drop the hit and exit.  We deliberately do NOT touch the frozen
                // leaderboard here.  The fast-clear path that used to fire on the first
                // dropped hit ≥ 5 s after the freeze was removed (Apr 2026, second fix
                // for "numbers reducing after fight ended"): in practice it discarded the
                // breakdown the user was actively reading, because the post-kill window
                // is FULL of incidental hits on loot crates, mini-bosses, medallions,
                // patrol mobs, etc. — every one of which counted as "user resumed combat"
                // and wiped the freeze, dropping the UI back to the 60 s sliding window
                // (which decays).  The freeze now persists until ONE of:
                //   • a hit on a real boss-classified target → encounter cleared (line ~1465)
                //   • OnRegionChanged                       → full reset
                //   • boss-only mode toggle                 → full reset
                //   • Reset() / external state wipe
                // i.e. only events that unambiguously mean "user moved on to a new fight".
                return;
            }
        }

        bool changed;
        bool newRecord = false;
        double newDps;
        ulong newOwner;
        long newOwnerTotal;

        lock (_sync)
        {
            DateTime now = e.UtcTime;

            // Boss-fight auto-reset is deferred until AFTER scoringOwner is finalized and
            // we know whether this hit belongs to *self* (vs. a peer).  Refreshing the
            // idle timer on peer hits would defeat the whole purpose in busy public
            // areas — see the block below the pet/summon fold.

            // Canonical entity id used for hero resolution + sliding windows.  Start from the
            // wire owner, then:
            //   • If the server credited the local *Player* container instead of the Avatar,
            //     fold that into the pinned avatar id so totals line up with TryActivatePower /
            //     InventoryMove (we saw entityId=36066 for avatar vs LocalPlayer id=36003).
            //   • If a summon/clone dealt damage under its own entity id but the power enum is
            //     unmistakably the same hero we've already pinned, merge into the avatar row so
            //     Clea-style Astral Clone damage still shows up on the main meter.
            ulong scoringOwner = rawOwner;
            if (_localPlayerEntityId != 0 && rawOwner == _localPlayerEntityId && _likelySelfOwnerId != 0)
                scoringOwner = _likelySelfOwnerId;

            // ── Pet/summon chain-root tracking + fold (peer summons too, not just self) ─────
            // The server gives us (PowerOwnerEntityId = wirePow, UltimateOwnerEntityId =
            // wireUlt) for every PowerResult.  When wirePow is a summon/pet/proxy and
            // wireUlt is its parent, we want the summon's damage to credit the chain-root
            // player avatar — so a peer Magik with three demons shows ONE row in the
            // leaderboard ("Jiujitsu  4.7M") instead of four ("Jiujitsu 4.7M / #F8B0 366k
            // / #F8B1 291k / #F8B2 247k").
            //
            // CRITICAL — DO NOT compare wireUlt against rawOwner:
            //   Line ~1406 already collapsed `rawOwner = wireUlt != 0 ? wireUlt : wirePow`,
            //   so when wireUlt is set (the only case we'd want to fold) `rawOwner == wireUlt`
            //   ALWAYS.  An earlier version of this block had `if (wireUlt != 0 && wireUlt
            //   != rawOwner)` which was therefore dead code — every "peer-pet fold" log line
            //   was missing because the branch never ran (verified empirically: a 22 MB
            //   dps-meter.log spanning multiple multi-Beast / multi-Magik fights had ZERO
            //   `peer-pet fold` entries).  The correct comparison is wireUlt vs wirePow,
            //   the actual sub-entity id BEFORE the collapse.
            //
            // Three-way edge classification, in priority order:
            //   1. wireUlt itself is a confirmed player avatar (in _heroNameByOwnerId or
            //      _localAvatarEntityIds) → root = wireUlt.  Covers single-tier summons
            //      (most pets in the game).
            //   2. wireUlt has an existing entry in _petRootOwnerByEntity → root = THAT
            //      entry's value.  Covers multi-tier summons (e.g. avatar → demon →
            //      demon's sub-pet) by walking the dict transitively at edge-record time
            //      so per-hit fold stays O(1).
            //   3. wireUlt is the local Player CONTAINER (rare — same mirror case the
            //      _localPlayerEntityId branch above handles for rawOwner) → root =
            //      _likelySelfOwnerId.
            //
            // We DON'T fold:
            //   • when wireUlt == 0 (no ult info; rawOwner already collapsed to wirePow).
            //   • when wireUlt == wirePow (self-credited power; not an indirection).
            //   • when neither rule above resolves a root (wireUlt is unknown — likely a
            //     mob spawner, system entity, or a peer whose avatar we haven't seen yet).
            //     The pet's damage stays under its own entity id; the player gate at the
            //     bottom of this method will drop it as non-player noise.  If the parent
            //     LATER becomes a known player, subsequent edges from this pet will fold
            //     correctly and the render-time coalesce pass migrates the stale row.
            //
            // NOTE on multi-tier summon depth limit (Beast / Magik / Doctor Strange etc.):
            //   even with this fold working correctly, the wire only carries proxy → pet —
            //   never pet → player.  For a 2-tier chain (player → pet → proxy) we record
            //   `proxy → pet` here but the pet itself never gets a `pet → player` edge from
            //   any PowerResult.  That residual gap is closed at render time by
            //   CoalesceAnonymousRowsByHeroName, which merges anonymous Beast/Magik rows
            //   into the unique named row playing that hero on the current leaderboard.
            //
            // The fold is a no-op for self-pets that already get redirected via the existing
            // Section-2 power-proto matchers (line ~1303 / ~1319 / ~1333) — both paths
            // produce scoringOwner = _likelySelfOwnerId.  Running this BEFORE those matchers
            // means peer-pet attribution never depends on power-proto coverage in
            // HeroPowers.Names, which is incomplete for custom-server hero kits.
            if (wireUlt != 0 && wirePow != 0 && wireUlt != wirePow)
            {
                ulong effectiveUlt = wireUlt;
                if (_localPlayerEntityId != 0 && wireUlt == _localPlayerEntityId && _likelySelfOwnerId != 0)
                    effectiveUlt = _likelySelfOwnerId;

                ulong root = 0;
                if (_heroNameByOwnerId.ContainsKey(effectiveUlt) || _localAvatarEntityIds.Contains(effectiveUlt))
                    root = effectiveUlt;
                else if (_petRootOwnerByEntity.TryGetValue(effectiveUlt, out ulong cachedRoot))
                    root = cachedRoot;

                // Never record wirePow → root when wirePow is a known local-player avatar id.
                // Some archives credit wirePow=<your avatar> with wireUlt=<peer>; treating the
                // avatar as a "pet" of the peer poisons render-time pet-chain coalesce and
                // anon-by-hero (stale encounter rows under the old entity id fold into the
                // peer's nickname — title still says your hero from the live pin).
                if (root != 0 && root != wirePow
                    && wirePow != _likelySelfOwnerId
                    && !_localAvatarEntityIds.Contains(wirePow))
                {
                    if (!_petRootOwnerByEntity.TryGetValue(wirePow, out ulong existingRoot) || existingRoot != root)
                    {
                        _petRootOwnerByEntity[wirePow] = root;
                        if (_loggedPeerPetFolds.Add(wirePow))
                            Diagnostic?.Invoke($"DpsMeter: peer-pet fold — pet/summon entity {wirePow} → chain-root avatar {root} (wireUlt={wireUlt}). All future hits from this pet credit the avatar.");
                    }
                }
            }

            // Apply the fold cache to scoringOwner.  Note rawOwner == wireUlt (post-collapse),
            // so this hits when the wireUlt has an entry (multi-tier case).  For the proxy
            // case (rawOwner already == wirePow because wireUlt was 0) the cache lookup is
            // also relevant.
            if (_petRootOwnerByEntity.TryGetValue(rawOwner, out ulong petChainRoot)
                && petChainRoot != 0
                && petChainRoot != scoringOwner)
            {
                scoringOwner = petChainRoot;
            }

            // Minions the wire fully credits to themselves (wireUlt==wirePow or ult omitted) never
            // hit peer-pet fold. If the power maps to hero H in HeroPowers and exactly ONE
            // player-bound avatar in AOI has display name H (_dbIdByAvatarId — excludes summons),
            // roll credit to that avatar (crogg's Ultron drones → crogg). Two human Ultron
            // players → two dbId rows with "Ultron" → no match.
            //
            // Blade's own Ultron drones use the same power indices; their wire typically carries
            // wireUlt=Blade while sole "Ultron" player is crogg — folding to a *peer* sole
            // requires wireUlt==soleAvatar or wireUlt==0 so we never steal Bandit's drones onto
            // crogg when wireUlt already points at the Blade avatar.
            if (e.PowerPrototypeEnumIndex != 0
                && HeroPowers.TryGetHero(e.PowerPrototypeEnumIndex, out string powerHeroForSole)
                && TryGetSolePlayerHeroAvatarForPowerHeroLocked(powerHeroForSole, out ulong solePowerHeroAvatar)
                && solePowerHeroAvatar != scoringOwner
                && !IsForeignAccountAvatarLocked(scoringOwner)
                && wirePow == scoringOwner
                && (wireUlt == 0 || wireUlt == scoringOwner))
            {
                bool foldToPeer = _likelySelfOwnerId != 0 && solePowerHeroAvatar != _likelySelfOwnerId;
                if (!foldToPeer
                    || wireUlt == solePowerHeroAvatar
                    || wireUlt == 0)
                {
                    scoringOwner = solePowerHeroAvatar;
                }
            }

            // ── 0. Hero resolution (runs FIRST, gates scoring) ──────────────────────────────
            // We resolve the event's ultimate-owner entity id to a hero display name BEFORE
            // updating the scoring window. The reason is correctness of self-owner election:
            //
            //   A naïve "top damage in 60s == you" scan can elect an enemy mob / DoT source /
            //   pet whose ultimateOwner is itself — we saw owner ids 505821 / 512701 win the
            //   leaderboard during burst phases, blanking the hero title and wiping MaxHit.
            //
            // By gating _scoring on "owner is a player-confirmed hero", enemy damage still
            // flows through this handler (we need it to keep the damage timeline coherent),
            // but it can never become a self-election candidate. Net result: overlay stays
            // locked to the real avatar even if a rare mob crit briefly out-DPSes the player.
            if (!_heroNameByOwnerId.ContainsKey(scoringOwner))
            {
                string? resolved = null;
                bool channelBSaysPlayer = false;

                if (_prototypeByEntityId.TryGetValue(scoringOwner, out uint heroProto) && heroProto != 0)
                {
                    HeroPrototypes.Names.TryGetValue(heroProto, out resolved);
                }

                if (string.IsNullOrEmpty(resolved)
                    && e.PowerPrototypeEnumIndex != 0
                    && HeroPowers.TryGetHero(e.PowerPrototypeEnumIndex, out string powerHero))
                {
                    resolved = powerHero;
                    channelBSaysPlayer = true;
                }

                if (!string.IsNullOrEmpty(resolved))
                {
                    _heroNameByOwnerId[scoringOwner] = resolved;
                }
                else if (channelBSaysPlayer == false
                         && _prototypeByEntityId.TryGetValue(scoringOwner, out uint entProto)
                         && entProto != 0)
                {
                    // Unknown-hero diagnostic — only log when Channel B ALSO didn't peg this
                    // as a player (otherwise we'd spam the log with mob / environmental
                    // prototypes that have no business being in HeroPrototypes). The one-shot
                    // hash set keeps the log terse even if the same unknown proto keeps
                    // hitting us for many hits in a row.
                    if (_loggedUnknownHeroes.Add(entProto))
                        Diagnostic?.Invoke($"DpsMeter: entity-proto index {entProto} (unknown — add to HeroPrototypes.Names only if this entity fires Powers/Player/* powers)");
                }
            }

            // Summons / Astral Clone etc.: wire owner is the minion entity id, but the power enum
            // matches the hero we've already pinned on the real avatar — fold into the avatar so
            // the main window + 60s window stay coherent (runs after hero resolution so pinHero
            // may have been filled by a prior hit on the avatar itself).
            //
            // CRITICAL — duplicate-hero AOI (Apr 2026): every Blade shares the same power enums in
            // HeroPowers. A *peer* Blade's hits also satisfy proxyHero==pinHero; without the
            // foreign-account guard we'd credit their entire boss encounter onto the local pin
            // (observed: self row "Blade" at 22M with Fight total matching only self while
            // loonypath's Blade stayed at 1.6M — the missing ~20M was the peer folded away).
            //
            // Summon kits (Apr 2026): some heroes spawn pets whose powers live under a different
            // HeroPowers bucket (Blade → Ultron drones → "Ultron"). proxyHero==pinHero never
            // fires; credit to self only when we're the sole avatar on that hero name AND ult
            // is missing or already points at us — avoids stealing a peer Blade's Ultron row
            // when two Blades are present (both map to "Blade", IsOnlyThisAvatarPlayingHeroLocked
            // is false).
            if (_likelySelfOwnerId != 0
                && scoringOwner != _likelySelfOwnerId
                && !IsForeignAccountAvatarLocked(scoringOwner)
                && e.PowerPrototypeEnumIndex != 0
                && HeroPowers.TryGetHero(e.PowerPrototypeEnumIndex, out string proxyHero)
                && _heroNameByOwnerId.TryGetValue(_likelySelfOwnerId, out string? pinHero))
            {
                bool powerMatchesPin = string.Equals(proxyHero, pinHero, StringComparison.Ordinal);
                bool kitMismatchSummon = !powerMatchesPin
                    && IsOnlyThisAvatarPlayingHeroLocked(pinHero)
                    && (wireUlt == _likelySelfOwnerId || wireUlt == 0);
                if (powerMatchesPin || kitMismatchSummon)
                    scoringOwner = _likelySelfOwnerId;
            }

            // Pets / summons / proxies: when the server omits ultimate owner, rawOwner is a minion
            // entity id that is never listed in _localAvatarEntityIds.  The fold above requires
            // pinHero to exist first — on a cold start after restart the first dozen hits all fail
            // the Section-1 gate and the meter looks "dead".  In authoritative mode, if the power
            // enum is unmistakably a player power and ultimate owner (if present) is not a
            // different entity, roll credit up to the pinned local avatar and seed hero name.
            if (_likelySelfOwnerId != 0
                && scoringOwner != _likelySelfOwnerId
                && !IsForeignAccountAvatarLocked(scoringOwner)
                && _localAvatarEntityIds.Count > 0
                && _localAvatarEntityIds.Contains(_likelySelfOwnerId)
                && !_localAvatarEntityIds.Contains(scoringOwner)
                && e.PowerPrototypeEnumIndex != 0
                && HeroPowers.TryGetHero(e.PowerPrototypeEnumIndex, out string petProxyHero))
            {
                bool ultCreditsSomeoneElse = wireUlt != 0 && wireUlt != _likelySelfOwnerId;
                if (!ultCreditsSomeoneElse)
                {
                    bool havePinName = _heroNameByOwnerId.TryGetValue(_likelySelfOwnerId, out string? pinName);
                    bool powerMatchesPin = !havePinName
                        || string.Equals(pinName, petProxyHero, StringComparison.Ordinal);
                    bool kitMismatchSummon = havePinName
                        && !string.Equals(pinName, petProxyHero, StringComparison.Ordinal)
                        && IsOnlyThisAvatarPlayingHeroLocked(pinName)
                        && (wireUlt == _likelySelfOwnerId || wireUlt == 0);
                    if (powerMatchesPin || kitMismatchSummon)
                    {
                        scoringOwner = _likelySelfOwnerId;
                        if (!havePinName)
                            _heroNameByOwnerId[_likelySelfOwnerId] = petProxyHero;
                    }
                }
            }

            // ── Self-hit predicate (computed early so the boss-fight auto-reset block below
            // can gate on it before we touch the sliding windows).  Mirrors the gate in
            // section 1 — keep both call sites in sync if you tweak the logic. ───────────────
            //
            //   • Authoritative path: scoringOwner is in our confirmed local-avatar set
            //     (built from NetMessageLocalPlayer / InventoryMove / TryActivatePower).
            //   • Pinned-self path: heuristic election already picked this owner as you.
            //   • Container-id path: the server credited the local *Player* container
            //     instead of an Avatar; rawOwner / wirePow match the local player entity id.
            bool ownerIsSelf = _localAvatarEntityIds.Contains(scoringOwner)
                || (_likelySelfOwnerId != 0 && scoringOwner == _likelySelfOwnerId)
                || (_localPlayerEntityId != 0
                    && (rawOwner == _localPlayerEntityId || wirePow == _localPlayerEntityId));

            // ── Boss-fight auto-reset (boss-only mode, self-idle gate) ──────────────────────
            // We've passed the boss-target filter; this hit is going to flow into hero
            // resolution next.  If WE haven't landed a boss-admitted hit ourselves in
            // BossFightIdleReset, treat this as the start of a brand-new fight: wipe the
            // sliding windows so the leaderboard doesn't blend stale data from the
            // *previous* encounter into the current one.
            //
            // CRITICAL — must gate on `ownerIsSelf`, not just "any boss hit":
            //   In a public area (Avengers Tower, BUE, Holo-Sim, shared waypoints) peer
            //   avatars hit bosses several times per second.  An unconditional "any boss
            //   hit refreshes the timer" never opens a 20s gap, so the user's own old
            //   burst from before a region change keeps decaying linearly through the
            //   60s window — exactly the "i absorbe old information" symptom we observed
            //   on Saturday's session log (region change → fresh boss fight → numbers
            //   blended with pre-zone burst for ~50 s).
            //
            // Two guards prevent unwanted resets:
            //   • _bossOnlyMode — in all-damage mode continuous decay is the right behavior.
            //   • _lastSelfBossHitUtc != MinValue — first self hit of session shouldn't
            //     trigger a "reset" diagnostic; just initialise the timestamp normally below.
            if (_bossOnlyMode
                && ownerIsSelf
                && _lastSelfBossHitUtc != DateTime.MinValue
                && now - _lastSelfBossHitUtc >= BossFightIdleReset
                && (_scoring.Count > 0 || _totalsPerOwner.Count > 0))
            {
                TimeSpan gap = now - _lastSelfBossHitUtc;
                int rowsCleared = _totalsPerOwner.Count;
                _scoring.Clear();
                _instant.Clear();
                _totalsPerOwner.Clear();
                CurrentDps = 0;
                CurrentOwnerTotal60s = 0;
                Diagnostic?.Invoke($"DpsMeter: boss-fight auto-reset (self idle {gap.TotalSeconds:F1}s ≥ {BossFightIdleReset.TotalSeconds:F0}s, cleared {rowsCleared} rows) — starting fresh fight");
            }

            // Still advance the 60s window even for non-hero events, so totals for evicted
            // hero hits decay on schedule (otherwise a heal-only period with no self-hits
            // would keep the old totals alive indefinitely).
            DateTime scoringCutoff = now - OwnerScoringWindow;
            while (_scoring.Count > 0 && _scoring.Peek().Ts < scoringCutoff)
            {
                var old = _scoring.Dequeue();
                if (_totalsPerOwner.TryGetValue(old.Owner, out long t))
                {
                    t -= old.Damage;
                    if (t <= 0) _totalsPerOwner.Remove(old.Owner);
                    else         _totalsPerOwner[old.Owner] = t;
                }
            }

            // Same idea for the 5s instant window.
            DateTime instantCutoff0 = now - InstantWindow;
            while (_instant.Count > 0 && _instant.Peek().Ts < instantCutoff0)
                _instant.Dequeue();

            // ── 1. Gate scoring on "owner is a confirmed hero" ───────────────────────────────
            // Enemy / DoT / terrain damage returns here without touching _scoring, so it never
            // becomes a self-election candidate.
            //
            // EXCEPTION: if this owner is OUR confirmed local avatar (authoritative set built
            // from NetMessageLocalPlayer / NetMessageInventoryMove / TryActivatePower), we let
            // the hit through even when hero name hasn't been resolved yet.  This plugs the
            // "launched app mid-session, missed EntityCreate for my own avatar, DPS stays 0"
            // regression:  Channel A (EntityCreate prototype) is impossible in that scenario
            // and Channel B (HeroPowers lookup by power-enum) might also whiff for less-used
            // powers — but we STILL know this is us, so damage must be counted.  The hero
            // name will back-fill on the first Channel-B hit we get (or remain empty; the
            // overlay shows just "DPS" in that case, which is still useful).
            bool ownerIsPlayer = _heroNameByOwnerId.ContainsKey(scoringOwner);
            if (!ownerIsPlayer && !ownerIsSelf)
            {
                // Recompute DPS from the (possibly purged) windows and fire if changed; this
                // keeps the UI decaying smoothly during long enemy-only periods.
                long selfBytes = 0;
                foreach (var hit in _instant)
                    if (hit.Owner == _likelySelfOwnerId) selfBytes += hit.Damage;
                double idleDps = _instant.Count >= 2 && selfBytes > 0
                    ? selfBytes / Math.Max(0.25, (now - _instant.Peek().Ts).TotalSeconds)
                    : 0;
                bool idleChanged = Math.Abs(idleDps - CurrentDps) > 0.5;
                CurrentDps = idleDps;
                if (idleChanged) DpsChanged?.Invoke(this, EventArgs.Empty);
                return;
            }

            // ── 2. Append the hit to the sliding windows ────────────────────────────────────
            _scoring.Enqueue((now, scoringOwner, dmg));
            _totalsPerOwner.TryGetValue(scoringOwner, out long prev);
            _totalsPerOwner[scoringOwner] = prev + dmg;

            // Mirror into the cumulative per-region accumulator that drives the normal-mode
            // leaderboard (Total: …) — same admit gate as the 60s window above, no decay.
            // See _sessionTotalsPerOwner field comment for the rationale.
            _sessionTotalsPerOwner.TryGetValue(scoringOwner, out long sessPrev);
            _sessionTotalsPerOwner[scoringOwner] = sessPrev + dmg;

            // Refresh the self-idle timer ONLY when the hit we just scored is ours.  Peer
            // hits intentionally don't refresh — keeping a peer-driven timer alive would
            // suppress the auto-reset every time we zone into a populated public area.
            if (_bossOnlyMode && ownerIsSelf)
                _lastSelfBossHitUtc = now;

            // ── 2a. Encounter accumulator (boss-only mode) ──────────────────────────────────
            // Fight-scoped per-owner totals — see _encounterTotalsPerOwner field doc for the
            // full rationale.  Three jobs in one block:
            //
            //   (i)   If the previous encounter was FROZEN (last engaged boss died, no new
            //         hit since), this hit is the trigger to clear it and start fresh.  We
            //         also clear when the existing accumulator is non-empty but there are
            //         NO engaged bosses left — that catches the edge case where kill events
            //         arrive after a region change cleared the engaged set but somehow left
            //         totals behind (defensive; shouldn't normally happen).
            //   (ii)  Engage the current target (it just took damage; whether it's a tracked
            //         boss is decided by the boss filter at the top of OnDamageDealt — we
            //         only reach here if that filter passed).
            //   (iii) Add this hit's damage to the per-owner accumulator.  All admitted
            //         scoring-owners count, peers included — that's the whole point of an
            //         encounter leaderboard.
            if (_bossOnlyMode)
            {
                if (_encounterEndedUtc != DateTime.MinValue)
                {
                    int frozenOwners = _encounterTotalsPerOwner.Count;
                    _encounterTotalsPerOwner.Clear();
                    _engagedBossEntityIds.Clear();
                    _lastHitPerEngagedBoss.Clear();
                    _encounterStartUtc = DateTime.MinValue;
                    _encounterEndedUtc = DateTime.MinValue;
                    _lastEncounterDamageUtc = DateTime.MinValue;
                    Diagnostic?.Invoke($"DpsMeter: encounter cleared (new boss hit after frozen fight, {frozenOwners} owners discarded) — starting fresh");
                }

                if (_encounterStartUtc == DateTime.MinValue)
                {
                    _encounterStartUtc = now;
                    Diagnostic?.Invoke($"DpsMeter: encounter started (firstTarget={e.TargetEntityId}, firstOwner={scoringOwner})");
                }

                if (e.TargetEntityId != 0)
                {
                    _engagedBossEntityIds.Add(e.TargetEntityId);
                    // Per-target last-hit timestamp drives the EngagedBossIdleEviction sweep
                    // in Tick — without this, missed kill events would let a long-dead boss
                    // sit in the engaged set indefinitely and prevent the next-fight clear.
                    _lastHitPerEngagedBoss[e.TargetEntityId] = now;
                }

                _encounterTotalsPerOwner.TryGetValue(scoringOwner, out long encPrev);
                _encounterTotalsPerOwner[scoringOwner] = encPrev + dmg;
                // Bump the stall watchdog — Tick uses this to declare the fight over if
                // EVERY player goes silent for EncounterStallTimeout (boss despawn, AOI
                // exit, kill-event lost in transit, etc.).  Any-owner timestamp on purpose;
                // see _lastEncounterDamageUtc field doc.
                _lastEncounterDamageUtc = now;
            }

            // ── 3. Pick "you" ────────────────────────────────────────────────────────────────
            // Two modes:
            //   (a) Authoritative mode (_localAvatarEntityIds non-empty): we already know which
            //       entity ids are YOUR avatars from NetMessageLocalPlayer + NetMessageInventoryMove.
            //       Pin _likelySelfOwnerId to whichever of those is firing the current hit.
            //       Hits from OTHER players never move the pin — that's exactly the bug we saw
            //       in group play where a higher-DPS teammate was stealing the main window.
            //   (b) Heuristic fallback (_localAvatarEntityIds empty): the app was launched mid-
            //       session so we missed the login handshake.  Fall back to "top damager in 60s".
            ulong prevSelfOwner = _likelySelfOwnerId;
            if (_localAvatarEntityIds.Count > 0)
            {
                if (_localAvatarEntityIds.Contains(scoringOwner)
                    && _likelySelfOwnerId != scoringOwner)
                {
                    Diagnostic?.Invoke($"DpsMeter: self-owner pinned {_likelySelfOwnerId} -> {scoringOwner} (authoritative from NetMessageLocalPlayer)");
                    _likelySelfOwnerId = scoringOwner;
                    _likelySelfChosenAt = now;
                }
                // If scoringOwner isn't in our set, leave _likelySelfOwnerId alone —
                // another player's / pet's hit can't influence the pin.
            }
            else
            {
                ulong topOwner = 0; long topTotal = 0;
                foreach (var kv in _totalsPerOwner)
                {
                    if (kv.Value > topTotal) { topTotal = kv.Value; topOwner = kv.Key; }
                }
                if (topOwner != _likelySelfOwnerId)
                {
                    Diagnostic?.Invoke($"DpsMeter: self-owner flipped {_likelySelfOwnerId} -> {topOwner} (60s total = {topTotal}, heuristic)");
                    _likelySelfOwnerId = topOwner;
                    _likelySelfChosenAt = now;
                }
            }

            // ── 4. Append to 5s instant window + compute DPS ────────────────────────────────
            // Instant window was already purged above (section 0); here we just enqueue the
            // current hit and sum the owner's share over the retained window.
            _instant.Enqueue((now, scoringOwner, dmg));

            long lastSelfWindow = 0;
            foreach (var hit in _instant)
                if (hit.Owner == _likelySelfOwnerId)
                    lastSelfWindow += hit.Damage;

            // Divide by the actual elapsed time so the displayed DPS is meaningful during the
            // first few seconds of combat (when the queue's span is < 5s).  Use the real span
            // between the oldest retained event and "now"; fallback to InstantWindow once the
            // queue is full to avoid artificial inflation on single-event spikes.
            double spanSeconds;
            if (_instant.Count >= 2)
                spanSeconds = Math.Max(0.25, (now - _instant.Peek().Ts).TotalSeconds);
            else
                spanSeconds = InstantWindow.TotalSeconds;

            newDps = lastSelfWindow / spanSeconds;
            newOwner = _likelySelfOwnerId;
            newOwnerTotal = _totalsPerOwner.TryGetValue(newOwner, out long t2) ? t2 : 0;

            bool maxChanged = false;
            bool heroChanged = false;

            // Adopt the hero of whoever is the current self-owner.  If we re-elected above, this
            // may be a brand-new hero — re-seed CurrentHeroDisplayName AND reload MaxSingleHit
            // from that hero's stored record so the UI swaps both pieces in lockstep.
            string? selfHeroName = null;
            if (_likelySelfOwnerId != 0)
                _heroNameByOwnerId.TryGetValue(_likelySelfOwnerId, out selfHeroName);

            if (!string.Equals(selfHeroName ?? string.Empty, CurrentHeroDisplayName, StringComparison.Ordinal))
            {
                CurrentHeroDisplayName = selfHeroName ?? string.Empty;
                heroChanged = true;
                // New hero (or hero cleared): reseed MaxSingleHit from the per-hero record so
                // we don't carry the previous hero's PB on the overlay.
                uint seeded = 0;
                if (!string.IsNullOrEmpty(selfHeroName))
                    _maxHitByHeroName.TryGetValue(selfHeroName, out seeded);
                if (seeded != MaxSingleHit)
                {
                    MaxSingleHit = seeded;
                    maxChanged = true;
                }
                if (heroChanged)
                    Diagnostic?.Invoke($"DpsMeter: hero identified/changed -> '{CurrentHeroDisplayName}' (owner {_likelySelfOwnerId}, seeded max {MaxSingleHit})");
            }

            // Personal-best tracking: ONLY for events credited to the current self-owner — avoid
            // attributing another player's (or a pet's) big hit to the user's record.
            if (scoringOwner == _likelySelfOwnerId && !string.IsNullOrEmpty(CurrentHeroDisplayName))
            {
                _maxHitByHeroName.TryGetValue(CurrentHeroDisplayName, out uint prevBest);
                if (dmg > prevBest)
                {
                    _maxHitByHeroName[CurrentHeroDisplayName] = dmg;
                    MaxSingleHit = dmg;
                    maxChanged = true;
                    newRecord = true;   // flushed to disk after we drop the lock, see below
                }
            }

            long newOwnerSessionTotal = _sessionTotalsPerOwner.TryGetValue(newOwner, out long sessTotal)
                ? sessTotal
                : 0;

            changed = Math.Abs(newDps - CurrentDps) > 0.5 || newOwner != prevSelfOwner || maxChanged || heroChanged;
            CurrentDps = newDps;
            CurrentOwnerTotal60s = newOwnerTotal;
            CurrentOwnerSessionTotal = newOwnerSessionTotal;
        }

        if (newRecord)
        {
            // Fire-and-forget persistence on new PB — frequency is low (only when records break)
            // so we don't need debouncing. Runs on capture thread but JSON I/O is ~1ms.
            SaveMaxHits();
            Diagnostic?.Invoke($"DpsMeter: new personal-best for {CurrentHeroDisplayName}: {MaxSingleHit}");
        }
        if (changed) DpsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Re-evaluates both sliding windows against <paramref name="now"/> and recomputes
    /// <see cref="CurrentDps"/> / <see cref="CurrentOwnerTotal60s"/> without requiring a new
    /// <see cref="DamageDealtEvent"/> to arrive. The presenter's decay timer calls this at ~4 Hz
    /// so the number visibly falls to zero after combat ends — without it, the last burst's DPS
    /// would stick forever (only <see cref="OnDamageDealt"/> evicts expired entries).
    /// </summary>
    /// <remarks>
    /// Re-election rules (important for avatar-swap / respawn cases):
    /// <list type="bullet">
    ///   <item>If the totals dict is NON-empty and someone else is on top with &gt; 1.5× our
    ///         current owner's total, promote them. The 1.5× hysteresis prevents flip-flop
    ///         between two comparable damage dealers during group play.</item>
    ///   <item>If our current owner fully decayed out (removed from dict) but another owner
    ///         is active, take whoever is top. Never reset back to 0 on pure idle — that would
    ///         make the overlay flicker to "locating you…" during peaceful periods.</item>
    ///   <item>If the dict is empty, leave <c>_likelySelfOwnerId</c> sticky (display continuity).</item>
    /// </list>
    /// </remarks>
    public void Tick(DateTime nowUtc)
    {
        bool changed;
        double newDps;
        long newOwnerTotal;
        ulong ownerBefore, ownerAfter;
        long reelectOldTotal = 0, reelectNewTotal = 0;
        bool reelected = false;
        string? stateDump = null;

        // Encounter-stall watchdog outputs (consumed AFTER the lock for the diagnostic /
        // event raise — same pattern the kill-driven encounter-end uses).  Declared here
        // so they stay in scope past the lock block.
        bool stallEndedFrozen = false;
        bool stallEndedAutoCleared = false;
        TimeSpan stallSilence = TimeSpan.Zero;
        TimeSpan stallDuration = TimeSpan.Zero;
        long stallSelfTotal = 0;
        int stallOwnerCount = 0;

        // Engaged-boss eviction outputs (separate from stall outputs because they describe
        // a different end-cause — per-target idle, not total-fight idle — and we want
        // distinct log lines for triage).
        bool evictEndedFrozen = false;
        bool evictEndedAutoCleared = false;
        int evictedCount = 0;
        TimeSpan evictDuration = TimeSpan.Zero;
        TimeSpan evictOldestSilence = TimeSpan.Zero;
        long evictSelfTotal = 0;
        int evictOwnerCount = 0;

        lock (_sync)
        {
            // ── Engaged-boss idle eviction ──────────────────────────────────────────────────
            // Sweep _lastHitPerEngagedBoss; evict any boss that hasn't been hit in
            // EngagedBossIdleEviction (15 s).  Bosses presumed dead via missed kill / destroy
            // packets get cleaned up here so the engaged set can drain, the encounter can
            // freeze, and the next boss hit triggers the standard "frozen → fresh" clear
            // instead of piling damage onto stale totals.
            //
            // Only runs while the encounter is ACTIVE (not already frozen) — frozen
            // encounters intentionally retain their engaged set so a late-arriving kill
            // event doesn't double-emit "encounter ended".
            //
            // Concrete bug this fixes: user finishes boss A in a public area; the kill
            // packet for A is dropped (or A is a mob type whose death the sniffer can't
            // observe).  User immediately moves to boss B, ~5 s later.  Without eviction:
            // engaged set = {A, B}, encounter never freezes, B's damage adds to A's totals.
            // With eviction: at t = A_lastHit + 15 s, A is evicted; if B isn't engaged yet
            // (i.e. user is still in transit) the engaged set drops to empty and the
            // encounter ends naturally.  When B is engaged afterwards, the standard
            // "frozen → fresh" clear fires on the first hit, so B starts from 0.
            if (_bossOnlyMode
                && _encounterStartUtc != DateTime.MinValue
                && _encounterEndedUtc == DateTime.MinValue
                && _lastHitPerEngagedBoss.Count > 0)
            {
                DateTime evictCutoff = nowUtc - EngagedBossIdleEviction;
                List<ulong>? toEvict = null;
                DateTime oldestEvicted = DateTime.MaxValue;
                foreach (var kv in _lastHitPerEngagedBoss)
                {
                    if (kv.Value < evictCutoff)
                    {
                        (toEvict ??= new List<ulong>(_lastHitPerEngagedBoss.Count)).Add(kv.Key);
                        if (kv.Value < oldestEvicted) oldestEvicted = kv.Value;
                    }
                }

                if (toEvict != null)
                {
                    foreach (ulong id in toEvict)
                    {
                        _lastHitPerEngagedBoss.Remove(id);
                        _engagedBossEntityIds.Remove(id);
                    }
                    evictedCount = toEvict.Count;
                    evictOldestSilence = nowUtc - oldestEvicted;

                    // If the eviction emptied the engaged set, end the encounter exactly
                    // like a natural kill-driven end — same freeze-vs-auto-clear branch on
                    // selfTotal so the user sees identical UI behavior regardless of
                    // whether the boss died via kill packet or via "presumed dead".
                    if (_engagedBossEntityIds.Count == 0)
                    {
                        evictDuration = nowUtc - _encounterStartUtc;
                        if (_likelySelfOwnerId != 0)
                            _encounterTotalsPerOwner.TryGetValue(_likelySelfOwnerId, out evictSelfTotal);
                        evictOwnerCount = _encounterTotalsPerOwner.Count;

                        if (evictSelfTotal == 0)
                        {
                            _encounterTotalsPerOwner.Clear();
                            _engagedBossEntityIds.Clear();
                            _lastHitPerEngagedBoss.Clear();
                            _encounterStartUtc = DateTime.MinValue;
                            _encounterEndedUtc = DateTime.MinValue;
                            _lastEncounterDamageUtc = DateTime.MinValue;
                            evictEndedAutoCleared = true;
                        }
                        else
                        {
                            _encounterEndedUtc = nowUtc;
                            evictEndedFrozen = true;
                        }
                    }
                }
            }

            // ── Encounter-stall check ───────────────────────────────────────────────────────
            // Runs BEFORE the sliding-window decay so the post-stall state (cleared encounter
            // accumulator OR `_encounterEndedUtc` set) is reflected in this tick's `newDps`,
            // `newOwnerTotal`, and the state dump.  Branch matches OnEngagedEntityRemoved:
            //   • selfTotal > 0  → freeze (set _encounterEndedUtc; user reads the breakdown).
            //   • selfTotal == 0 → auto-clear (peer-only fight, no value in freezing).
            // Only fires when an encounter is genuinely active (start set, not already ended)
            // AND we have a damage timestamp to compare against AND the silence exceeds the
            // configured threshold.  Without the timestamp guard a tick fired BEFORE the very
            // first hit of a new encounter would spuriously match (MinValue is "ages ago").
            //
            // Now mostly a backstop — per-target eviction above usually catches missed-kill
            // scenarios first (15 s vs 30 s threshold).  Stall still matters for the case
            // where the ENGAGED SET itself is stale (eviction logic broken / dictionaries
            // out of sync) or when no engaged-set entry was ever recorded (shouldn't happen,
            // but defensive).
            if (_bossOnlyMode
                && _encounterStartUtc != DateTime.MinValue
                && _encounterEndedUtc == DateTime.MinValue
                && _lastEncounterDamageUtc != DateTime.MinValue
                && nowUtc - _lastEncounterDamageUtc >= EncounterStallTimeout)
            {
                stallSilence = nowUtc - _lastEncounterDamageUtc;
                stallDuration = nowUtc - _encounterStartUtc;
                if (_likelySelfOwnerId != 0)
                    _encounterTotalsPerOwner.TryGetValue(_likelySelfOwnerId, out stallSelfTotal);
                stallOwnerCount = _encounterTotalsPerOwner.Count;

                if (stallSelfTotal == 0)
                {
                    _encounterTotalsPerOwner.Clear();
                    _engagedBossEntityIds.Clear();
                    _lastHitPerEngagedBoss.Clear();
                    _encounterStartUtc = DateTime.MinValue;
                    _encounterEndedUtc = DateTime.MinValue;
                    _lastEncounterDamageUtc = DateTime.MinValue;
                    stallEndedAutoCleared = true;
                }
                else
                {
                    // Freeze in place — engaged-boss set is intentionally retained so a
                    // late-arriving kill event won't double-emit "encounter ended".  The
                    // user reads the final breakdown until their next boss hit clears it.
                    _encounterEndedUtc = nowUtc;
                    stallEndedFrozen = true;
                }
            }

            // ── Frozen-state retirement ─────────────────────────────────────────────────────
            // No automatic / time-based retirement happens here.  TWO previous attempts at
            // an automatic discard were both removed because they wiped the breakdown the
            // user was actively reading:
            //   • EncounterFrozenMaxAge (20 s wall-clock) — removed Apr 2026 (first iter).
            //   • FastClearFrozenIfStale (non-engaged target hit ≥ 5 s after freeze) —
            //     removed Apr 2026 (second iter).  The post-kill window is FULL of
            //     incidental hits on loot crates / medallions / mini-bosses / patrol mobs,
            //     and the fast-clear was firing on those, instantly dropping the UI back
            //     to GetTopHeroesBy60sShare (whose totals visibly drain as the 60 s window
            //     slides) — exactly the "numbers reducing after fight ended" symptom.
            // The freeze now persists indefinitely until one of these explicit "user moved
            // on" events fires:
            //   • hit on a real boss-classified target → encounter cleared (OnDamageDealt)
            //   • OnRegionChanged                       → full reset
            //   • boss-only mode toggle                 → full reset
            //   • Reset() / external state wipe

            DateTime scoringCutoff = nowUtc - OwnerScoringWindow;
            while (_scoring.Count > 0 && _scoring.Peek().Ts < scoringCutoff)
            {
                var old = _scoring.Dequeue();
                if (_totalsPerOwner.TryGetValue(old.Owner, out long t))
                {
                    t -= old.Damage;
                    if (t <= 0) _totalsPerOwner.Remove(old.Owner);
                    else         _totalsPerOwner[old.Owner] = t;
                }
            }

            DateTime instantCutoff = nowUtc - InstantWindow;
            while (_instant.Count > 0 && _instant.Peek().Ts < instantCutoff)
                _instant.Dequeue();

            // ── Ownership re-election on wall-clock tick ────────────────────────────────────
            // Only runs in heuristic mode (no authoritative local-player signal received yet).
            // When we DO know the real local-player avatar ids, OnDamageDealt already pins the
            // correct one — Tick has no business overriding that with "top damager" logic.
            ownerBefore = _likelySelfOwnerId;
            _totalsPerOwner.TryGetValue(_likelySelfOwnerId, out long currentOwnerTotal);

            if (_localAvatarEntityIds.Count == 0)
            {
                ulong topOwner = 0; long topTotal = 0;
                foreach (var kv in _totalsPerOwner)
                    if (kv.Value > topTotal) { topTotal = kv.Value; topOwner = kv.Key; }

                bool currentHasZero   = _likelySelfOwnerId == 0 || currentOwnerTotal == 0;
                bool topDominates     = topOwner != 0 && topTotal > currentOwnerTotal * 1.5;  // hysteresis
                bool dictIsEmpty      = _totalsPerOwner.Count == 0;

                if (!dictIsEmpty && topOwner != _likelySelfOwnerId && (currentHasZero || topDominates))
                {
                    reelectOldTotal = currentOwnerTotal;
                    reelectNewTotal = topTotal;
                    _likelySelfOwnerId = topOwner;
                    _likelySelfChosenAt = nowUtc;
                    reelected = true;
                }
            }
            ownerAfter = _likelySelfOwnerId;

            long lastSelfWindow = 0;
            foreach (var hit in _instant)
                if (hit.Owner == _likelySelfOwnerId)
                    lastSelfWindow += hit.Damage;

            // When the instant queue is empty (no damage in the last 5s), DPS is simply 0 —
            // don't divide by span, that would just produce NaN/huge numbers.
            if (_instant.Count == 0 || lastSelfWindow == 0)
            {
                newDps = 0;
            }
            else
            {
                // Always divide by the full window length when ticking (as opposed to the
                // in-event code which uses the queue span). Using the full window during idle
                // decay gives the user a smooth visible fall instead of a sudden cliff at 5s.
                newDps = lastSelfWindow / InstantWindow.TotalSeconds;
            }

            newOwnerTotal = _totalsPerOwner.TryGetValue(_likelySelfOwnerId, out long t2) ? t2 : 0;

            // ── Re-resolve hero on owner flip ────────────────────────────────────────────────
            // Without this, a hero-swap that decays the old self's totals below the new hero's
            // (handled above by `reelected = true`) would leave the overlay showing the old
            // hero's name + PB until the next damage event arrived from the new owner. Now we
            // adopt the cached name for the new self-owner immediately — and reseed MaxSingleHit
            // from the new hero's record so the UI swaps name and PB together.
            bool heroChanged = false;
            if (reelected || ownerAfter == 0 && !string.IsNullOrEmpty(CurrentHeroDisplayName))
            {
                string? selfHeroName = null;
                if (ownerAfter != 0)
                    _heroNameByOwnerId.TryGetValue(ownerAfter, out selfHeroName);

                if (!string.Equals(selfHeroName ?? string.Empty, CurrentHeroDisplayName, StringComparison.Ordinal))
                {
                    CurrentHeroDisplayName = selfHeroName ?? string.Empty;
                    heroChanged = true;
                    uint seeded = 0;
                    if (!string.IsNullOrEmpty(selfHeroName))
                        _maxHitByHeroName.TryGetValue(selfHeroName, out seeded);
                    MaxSingleHit = seeded;
                }
            }

            long newOwnerSessionTotal = _sessionTotalsPerOwner.TryGetValue(_likelySelfOwnerId, out long sessTotal)
                ? sessTotal
                : 0;

            changed = Math.Abs(newDps - CurrentDps) > 0.5
                   || newOwnerTotal != CurrentOwnerTotal60s
                   || newOwnerSessionTotal != CurrentOwnerSessionTotal
                   || ownerAfter != ownerBefore
                   || heroChanged;
            CurrentDps = newDps;
            CurrentOwnerTotal60s = newOwnerTotal;
            CurrentOwnerSessionTotal = newOwnerSessionTotal;

            // ── Periodic state dump (~every 10s) ────────────────────────────────────────────
            // Surfaces the resolved owner/hero/total table to the log so that "DPS shows 0
            // mid-combat" can be triaged without the user collecting per-event packet logs.
            // The expected entry for a working session: a row whose Owner == _likelySelfOwnerId,
            // Hero != "", and Total > 0; missing pieces tell us exactly which stage broke
            // (no scoring rows = damage isn't reaching us / all events have total=0; rows
            // present but none match self = our self-pin is wrong; rows + match but Total
            // dropping to 0 = the 60s window is rolling off without new credit).
            if ((nowUtc - _lastStateDumpUtc) >= StateDumpInterval)
            {
                _lastStateDumpUtc = nowUtc;
                var sb = new System.Text.StringBuilder();
                sb.Append("DpsMeter.State: self=").Append(_likelySelfOwnerId)
                  .Append(" hero='").Append(CurrentHeroDisplayName).Append("'")
                  .Append(" bossOnly=").Append(_bossOnlyMode)
                  .Append(" 60s=").Append(CurrentOwnerTotal60s)
                  .Append(" sess=").Append(CurrentOwnerSessionTotal)
                  .Append(" dps=").Append((long)CurrentDps)
                  .Append(" rows=").Append(_totalsPerOwner.Count)
                  .Append(" sessRows=").Append(_sessionTotalsPerOwner.Count)
                  .Append(" localAvatars=[").Append(string.Join(",", _localAvatarEntityIds))
                  .Append("] localPlayer=").Append(_localPlayerEntityId);
                // Encounter dump (only meaningful in boss mode; suppress otherwise to keep the
                // line short).  fightState ∈ {none, active, ended} maps directly onto the
                // boss-mode UI badge so post-hoc log triage can correlate user complaints
                // ("the meter froze on the old fight") with the actual stored state.
                if (_bossOnlyMode)
                {
                    string fightState = _encounterStartUtc == DateTime.MinValue
                        ? "none"
                        : (_encounterEndedUtc == DateTime.MinValue ? "active" : "ended");
                    long fightSelf = 0;
                    if (_likelySelfOwnerId != 0)
                        _encounterTotalsPerOwner.TryGetValue(_likelySelfOwnerId, out fightSelf);
                    sb.Append(" fight=").Append(fightState)
                      .Append(" fightSelf=").Append(fightSelf)
                      .Append(" fightOwners=").Append(_encounterTotalsPerOwner.Count)
                      .Append(" engagedBosses=").Append(_engagedBossEntityIds.Count);
                }
                if (_totalsPerOwner.Count > 0)
                {
                    sb.Append(" top:");
                    int n = 0;
                    foreach (var kv in _totalsPerOwner.OrderByDescending(p => p.Value))
                    {
                        if (n++ >= 5) break;
                        _heroNameByOwnerId.TryGetValue(kv.Key, out string? h);
                        // Resolve the same nickname chain the leaderboard uses so logs reflect
                        // exactly what the user sees on screen.  Two-stage resolution:
                        //   1. Direct: avatar → dbId via EntityCreate pairing, dbId → nick.
                        //   2. Community-slot fallback: match the row's hero against a nearby
                        //      dbId whose current slot hero is the same — useful when we missed
                        //      the peer's EntityCreate at zone-in.
                        string nick = ResolveNicknameForOwner(kv.Key, h);
                        sb.Append(" [").Append(kv.Key).Append('/').Append(h ?? "?");
                        if (!string.IsNullOrEmpty(nick)) sb.Append('@').Append(nick);
                        sb.Append('=').Append(kv.Value).Append(']');
                    }
                    sb.Append(" bindings=").Append(_dbIdByAvatarId.Count)
                      .Append(" nicks=").Append(_playerNameByDbId.Count)
                      .Append(" slotHeroes=").Append(_currentHeroNameByDbId.Count)
                      .Append(" nearby=").Append(_nearbyDbIds.Count);
                }
                stateDump = sb.ToString();
            }
        }

        if (reelected)
            Diagnostic?.Invoke($"DpsMeter.Tick: self-owner re-elected {ownerBefore} (60s={reelectOldTotal}) -> {ownerAfter} (60s={reelectNewTotal})");
        if (evictEndedFrozen)
            Diagnostic?.Invoke($"DpsMeter: encounter ended (engaged-boss eviction — {evictedCount} boss(es) idle for ≥ {EngagedBossIdleEviction.TotalSeconds:F0}s, oldest={evictOldestSilence.TotalSeconds:F1}s, duration={evictDuration.TotalSeconds:F1}s, selfTotal={evictSelfTotal:N0}, owners={evictOwnerCount}) — leaderboard frozen until next boss hit");
        else if (evictEndedAutoCleared)
            Diagnostic?.Invoke($"DpsMeter: encounter ended (engaged-boss eviction — {evictedCount} boss(es) idle for ≥ {EngagedBossIdleEviction.TotalSeconds:F0}s, oldest={evictOldestSilence.TotalSeconds:F1}s, duration={evictDuration.TotalSeconds:F1}s, selfTotal=0, owners={evictOwnerCount}) — peer-only fight, auto-cleared (no freeze)");
        else if (evictedCount > 0)
            Diagnostic?.Invoke($"DpsMeter: engaged-boss eviction ({evictedCount} idle ≥ {EngagedBossIdleEviction.TotalSeconds:F0}s, oldest={evictOldestSilence.TotalSeconds:F1}s) — encounter still active, {_engagedBossEntityIds.Count} engaged remaining");
        if (stallEndedFrozen)
            Diagnostic?.Invoke($"DpsMeter: encounter ended (stalled — no damage for {stallSilence.TotalSeconds:F1}s ≥ {EncounterStallTimeout.TotalSeconds:F0}s, duration={stallDuration.TotalSeconds:F1}s, selfTotal={stallSelfTotal:N0}, owners={stallOwnerCount}) — leaderboard frozen until next boss hit");
        if (stallEndedAutoCleared)
            Diagnostic?.Invoke($"DpsMeter: encounter ended (stalled — no damage for {stallSilence.TotalSeconds:F1}s ≥ {EncounterStallTimeout.TotalSeconds:F0}s, duration={stallDuration.TotalSeconds:F1}s, selfTotal=0, owners={stallOwnerCount}) — peer-only fight, auto-cleared (no freeze)");
        if (stateDump != null)
            Diagnostic?.Invoke(stateDump);
        // Stall-end / eviction-end always counts as a state change for UI repaint purposes —
        // the encounter badge / leaderboard source flips even when the 60s window numbers
        // haven't moved.  Note we deliberately do NOT raise on a partial eviction (engaged
        // set non-empty afterwards): the encounter is still active, totals haven't changed,
        // and the user shouldn't see the overlay flicker just because we cleaned house.
        if (changed || stallEndedFrozen || stallEndedAutoCleared || evictEndedFrozen || evictEndedAutoCleared)
            DpsChanged?.Invoke(this, EventArgs.Empty);
    }

    private DateTime _lastStateDumpUtc = DateTime.MinValue;
    private static readonly TimeSpan StateDumpInterval = TimeSpan.FromSeconds(10);

    private void OnRegionChanged(object? sender, RegionChangedEvent e)
    {
        // Log the region-change first so that post-incident log reads can distinguish
        // "user actually zoned" from "user was in-place the whole session" — the
        // cleared state below would otherwise look identical to a fresh session in
        // post-hoc diagnostics.  Particularly important for triage of the
        // "nicknames don't resolve" case, since the server ONLY re-sends
        // NetMessageEntityCreate for remote avatars when the local player zones, so
        // the absence of this log line is a strong signal that the nickname cache
        // could not have been refreshed no matter what we did on the client side.
        Diagnostic?.Invoke($"DpsMeter: region changed — clearing per-region state");
        lock (_sync)
        {
            _scoring.Clear();
            _totalsPerOwner.Clear();
            // Session totals are bounded by region — zoning ends the "this region" reading
            // unconditionally so the next region starts from zero (matches the encounter
            // accumulator's region-change wipe in boss mode for symmetry).
            _sessionTotalsPerOwner.Clear();
            _instant.Clear();
            _likelySelfOwnerId = 0;
            _likelySelfChosenAt = default;
            CurrentDps = 0;
            CurrentOwnerTotal60s = 0;
            CurrentOwnerSessionTotal = 0;
            // MaxSingleHit is NOT reset here — it's the hero's all-time personal best, not a
            // per-region number. The next DamageDealt event will re-load it from
            // _maxHitByHeroName once the avatar's display name is re-identified.
            MaxSingleHit = 0;
            CurrentHeroDisplayName = string.Empty;
            _loggedUnknownHeroes.Clear();
            _loggedNonBossTargets.Clear();
            _loggedUnknownBossTargets.Clear();
            _loggedNonCombatantTargets.Clear();
            _loggedUnknownNormalTargets.Clear();
            _loggedAdmittedNormalTargets.Clear();
            // Pet → chain-root edges are entity-id-keyed and entity ids restart per region
            // (server destroys all summons on zone), so carrying these forward would mis-
            // attribute damage if a re-allocated pet entity id collided with a stale entry.
            // Clear together with _localAvatarEntityIds + _prototypeByEntityId for the same
            // reason — all three are per-region by definition.
            _petRootOwnerByEntity.Clear();
            _loggedPeerPetFolds.Clear();
            _loggedAnonByHeroFolds.Clear();
            _loggedOffByOneBossAdmits.Clear();

            // ── Entity-id-keyed maps are DELIBERATELY KEPT across region changes ─────────────
            // In MHServer the entity-id namespace is server-global and stable for the lifetime
            // of an entity (your Player container id, avatar ids, peer avatar ids don't change
            // when you zone — the server only sends a RegionChange marker and then re-streams
            // EntityCreate ONLY for entities that just entered your AOI).  Peers who were
            // already near you at zone time do NOT get a fresh EntityCreate, so if we wipe
            // `_heroNameByOwnerId` / `_dbIdByAvatarId` here we PERMANENTLY lose their
            // identification for this session — their damage events still flow through
            // `OnDamageDealt` but hero resolution fails (no cached protoIdx, no re-delivered
            // EntityCreate), the row is dropped from the leaderboard, and the player
            // effectively becomes invisible.  Observed in production: user zoned from hub
            // to Midtown with Boyka(Juggernaut) + Palu(Storm) already in party proximity;
            // their avatar EntityCreates weren't resent, so the meter showed only Blade
            // despite two peers actively dealing damage nearby.
            //
            // Stale entries for entities that later get culled are harmless — we'd never
            // receive damage from them again so the dictionary entry just idles in memory.
            //
            // We DO still clear:
            //   • _prototypeByEntityId  → target-prototype cache is mostly enemies whose
            //     ids rotate per region; keeping it would risk admitting stale trash in
            //     boss-only mode.  Re-populates within a second via EntityCreate.
            //   • pending-binding queues → temporal-pairing timers; any in-flight pairing
            //     that didn't complete before the zone is almost certainly a bad pair.
            //   • _localAvatarEntityIds → only the CURRENTLY in-play avatar is valid per
            //     region (the local player's other avatars are removed from world until
            //     AvatarInPlay swap).  Repopulates immediately via InventoryMove.
            _localAvatarEntityIds.Clear();
            _pendingAvatarBindings.Clear();
            _pendingDbIdBindings.Clear();

            // Re-arm the boss-fight idle detector — we just wiped the scoring windows
            // anyway, so the previous timestamp is meaningless and would either spuriously
            // fire (if the user zoned mid-fight) or suppress a legitimate reset (if a
            // fresh boss spawns within the gap window in the new region).
            _lastSelfBossHitUtc = DateTime.MinValue;

            // Wipe the encounter accumulator — engaged-boss entity ids are per-region and
            // their EntityKilled / EntityDestroyed events may never arrive if we left the
            // region mid-fight (server stops streaming AOI updates for the abandoned
            // entities the moment we zone).  Carrying state forward would either freeze
            // the leaderboard forever (kill events never come) or splice the previous
            // region's totals into the new fight (next boss hit accumulates on top of
            // stale totals).  Cleanest semantic: zoning ends the encounter unconditionally.
            _encounterTotalsPerOwner.Clear();
            _engagedBossEntityIds.Clear();
            _lastHitPerEngagedBoss.Clear();
            _encounterStartUtc = DateTime.MinValue;
            _encounterEndedUtc = DateTime.MinValue;
            _lastEncounterDamageUtc = DateTime.MinValue;

            // Nearby-AOI set rotates wholesale when we zone — every peer left our AOI and
            // we'll get fresh "nearby" broadcasts for whoever is in the new region within a
            // few hundred ms.  Keeping stale entries would defeat the whole point of the
            // nearby-only nickname-resolution pass (too many false candidates on a popular
            // hero like Rogue).
            _nearbyDbIds.Clear();
        }
        // Drop the entity-id cache too: every id in it is stale after a region transition. The
        // avatar's fresh EntityCreate arrives within a second of the region change message, so
        // we'll be re-populated before the next DamageDealt.
        _prototypeByEntityId.Clear();
        DpsChanged?.Invoke(this, EventArgs.Empty);
        Diagnostic?.Invoke($"DpsMeter: region changed (regionProtoId={e.RegionPrototypeId}) — meter reset");
    }

    private void OnEntityKilled(object? sender, EntityKillEvent e)
        => OnEngagedEntityRemoved(e.EntityId, e.UtcTime, sourceTag: "killed");

    private void OnEntityDestroyed(object? sender, EntityDestroyEvent e)
        => OnEngagedEntityRemoved(e.EntityId, e.UtcTime, sourceTag: "destroyed");

    // Note (Apr 2026, second iteration of "numbers reducing after fight ended" fix):
    // FastClearFrozenIfStale was REMOVED.  See the comment at the EncounterFrozenFastClearGrace
    // declaration site for the full rationale — TL;DR: the post-kill window is full of
    // incidental hits on loot crates / medallions / mini-bosses / patrol mobs, and any of
    // those firing the fast-clear discarded the breakdown the user was reading.  The freeze
    // now persists indefinitely and is only retired by explicit "user moved on" events:
    // new boss hit (encounter cleared in OnDamageDealt), region change, mode toggle, Reset().

    /// <summary>Common path for boss-death detection — both <see cref="MhMissionSniffer.EntityKilled"/>
    /// (explicit kill notification) and <see cref="MhMissionSniffer.EntityDestroyed"/> (catch-all
    /// removal) funnel here.  We process the union because empirically some boss types only emit
    /// one or the other (server quirk; better to dedupe here than to miss encounter-ends).
    ///
    /// Cheap fast path: if the entity isn't in <see cref="_engagedBossEntityIds"/> we exit
    /// without touching anything else — most kill / destroy events in a session are irrelevant
    /// (trash mobs, props, projectiles) and we don't want to take the lock + fire DpsChanged
    /// for those.</summary>
    private void OnEngagedEntityRemoved(ulong entityId, DateTime utcTime, string sourceTag)
    {
        if (entityId == 0) return;

        bool encounterJustEnded = false;
        bool autoClearedNoSelf = false;
        TimeSpan duration = TimeSpan.Zero;
        long selfTotal = 0;
        int ownerCount = 0;

        lock (_sync)
        {
            // Cheap miss check first — vast majority of removals are non-boss entities and
            // the HashSet.Remove already does a bucket lookup, so duplicating with Contains
            // would cost more than it saves.
            if (!_engagedBossEntityIds.Remove(entityId))
                return;
            // Keep _lastHitPerEngagedBoss in lockstep with the engaged HashSet — without
            // this, a stale per-target timestamp would linger and the eviction sweep in
            // Tick could spuriously "evict" an already-killed entity.
            _lastHitPerEngagedBoss.Remove(entityId);

            // Last engaged boss just died.  Two outcomes depending on whether SELF actually
            // participated in this fight:
            //
            //   • selfTotal > 0  → freeze the encounter so the user can read the final
            //     breakdown of a fight they were part of.  Cleared on the next boss hit.
            //
            //   • selfTotal == 0 → auto-clear immediately, NO freeze.  This is the case
            //     where a peer engaged and killed a boss while the user was positioning,
            //     loading, in a menu, or just hadn't entered melee range yet.  Without
            //     auto-clear the overlay would sit on "fight ended · Fight: 0" with a
            //     single peer at 100% for as long as it takes the user to land their
            //     own first boss hit (observed in production: 78 seconds in busy public
            //     areas like Avengers Tower BUE, with a peer killing every boss before
            //     the user could engage).  That state reads as "the meter is broken /
            //     stuck / lost connection" because there is literally nothing useful on
            //     screen — the leaderboard is a stranger's contribution to a fight the
            //     user didn't participate in, and the personal number is 0.  Far better
            //     to fall back to the live "waiting for boss…" / 60s rolling state.
            if (_engagedBossEntityIds.Count == 0
                && _encounterStartUtc != DateTime.MinValue
                && _encounterEndedUtc == DateTime.MinValue)
            {
                duration = utcTime - _encounterStartUtc;
                if (_likelySelfOwnerId != 0)
                    _encounterTotalsPerOwner.TryGetValue(_likelySelfOwnerId, out selfTotal);
                ownerCount = _encounterTotalsPerOwner.Count;

                if (selfTotal == 0)
                {
                    _encounterTotalsPerOwner.Clear();
                    _engagedBossEntityIds.Clear();
                    _lastHitPerEngagedBoss.Clear();
                    _encounterStartUtc = DateTime.MinValue;
                    _encounterEndedUtc = DateTime.MinValue;
                    _lastEncounterDamageUtc = DateTime.MinValue;
                    autoClearedNoSelf = true;
                }
                else
                {
                    _encounterEndedUtc = utcTime;
                    encounterJustEnded = true;
                }
            }
        }

        if (encounterJustEnded)
        {
            Diagnostic?.Invoke($"DpsMeter: encounter ended ({sourceTag} entity={entityId}, duration={duration.TotalSeconds:F1}s, selfTotal={selfTotal:N0}, owners={ownerCount}) — leaderboard frozen until next boss hit");
            // Notify listeners so the overlay repaints with the "ended" indicator on the
            // current detail line — without this it'd take up to 250ms (next decay tick)
            // before the user sees the final breakdown freeze.
            DpsChanged?.Invoke(this, EventArgs.Empty);
        }
        else if (autoClearedNoSelf)
        {
            Diagnostic?.Invoke($"DpsMeter: encounter ended ({sourceTag} entity={entityId}, duration={duration.TotalSeconds:F1}s, selfTotal=0, owners={ownerCount}) — peer-only fight, auto-cleared (no freeze)");
            DpsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Snapshot of the current encounter for UI consumption.  Returned by value so the
    /// overlay can read all fields without holding the meter lock.  All fields are MinValue / 0
    /// when no encounter has ever started this session (or the last one was cleared by region
    /// change / mode toggle).</summary>
    /// <param name="IsActive"><c>true</c> when at least one engaged boss is still alive
    /// (accumulator is open and accepting hits).</param>
    /// <param name="IsEnded"><c>true</c> when all engaged bosses have died and the leaderboard
    /// is frozen, awaiting the next boss hit to clear and start a new encounter.  The detail
    /// line should annotate the "fight" total with "(ended)" or similar so the user understands
    /// they're looking at a final breakdown, not a live one.</param>
    /// <param name="SelfTotal">Damage credited to <see cref="LikelySelfOwnerId"/> in this
    /// encounter; 0 if self isn't pinned or has done nothing yet.  Replaces the title's
    /// "60s:" number while in boss mode.</param>
    /// <param name="StartUtc">Time of the first boss hit of this encounter.</param>
    /// <param name="EndUtc">Time the last engaged boss died, or MinValue while active.</param>
    /// <param name="EngagedBossCount">Bosses still alive (0 once frozen).</param>
    public readonly record struct EncounterSnapshot(
        bool IsActive,
        bool IsEnded,
        long SelfTotal,
        DateTime StartUtc,
        DateTime EndUtc,
        int EngagedBossCount);

    /// <summary>Atomic snapshot of the encounter accumulator state for the UI / presenter.
    /// Returns an empty / MinValue snapshot when boss-only mode is OFF — the encounter
    /// concept only applies in boss mode.</summary>
    public EncounterSnapshot GetEncounterSnapshot()
    {
        lock (_sync)
        {
            if (!_bossOnlyMode)
                return new EncounterSnapshot(false, false, 0, DateTime.MinValue, DateTime.MinValue, 0);

            bool isActive = _encounterStartUtc != DateTime.MinValue && _encounterEndedUtc == DateTime.MinValue;
            bool isEnded  = _encounterEndedUtc != DateTime.MinValue;
            long selfTotal = 0;
            if (_likelySelfOwnerId != 0)
                _encounterTotalsPerOwner.TryGetValue(_likelySelfOwnerId, out selfTotal);
            return new EncounterSnapshot(
                IsActive: isActive,
                IsEnded: isEnded,
                SelfTotal: selfTotal,
                StartUtc: _encounterStartUtc,
                EndUtc: _encounterEndedUtc,
                EngagedBossCount: _engagedBossEntityIds.Count);
        }
    }

    /// <summary>Top-N rows by encounter total (boss-only mode), ranked by absolute damage with
    /// percent computed against the sum of admitted owners in the current encounter.  Same
    /// row shape as <see cref="GetTopHeroesBy60sShare"/> so the overlay can render either
    /// without conditional layout — only the source totals differ.
    ///
    /// <para>Returns an empty list when boss-only mode is OFF or when the encounter has no
    /// data yet.  Self row is force-emitted (same rationale as the 60s variant) so the user
    /// is never invisible from their own leaderboard even if hero resolution fails.</para></summary>
    public IReadOnlyList<HeroShareEntry> GetTopHeroesByEncounterShare(int max)
    {
        if (max <= 0) return Array.Empty<HeroShareEntry>();

        lock (_sync)
        {
            if (!_bossOnlyMode || _encounterTotalsPerOwner.Count == 0)
                return Array.Empty<HeroShareEntry>();

            // Sum candidate damage with the same self-inclusion rule as the 60s variant —
            // the user must always see their own contribution, hero name resolved or not.
            long totalEncounterDamage = 0;
            foreach (var kv in _encounterTotalsPerOwner)
            {
                if (_heroNameByOwnerId.ContainsKey(kv.Key)
                    || (_likelySelfOwnerId != 0 && kv.Key == _likelySelfOwnerId))
                    totalEncounterDamage += kv.Value;
            }
            if (totalEncounterDamage <= 0)
                return Array.Empty<HeroShareEntry>();

            var boundDbIds = new HashSet<ulong>(_dbIdByAvatarId.Values);
            foreach (var cv in _dbIdByPlayerEntityId.Values) boundDbIds.Add(cv);

            var rows = new List<HeroShareEntry>(_encounterTotalsPerOwner.Count);
            foreach (var kv in _encounterTotalsPerOwner)
            {
                bool isSelf = _likelySelfOwnerId != 0 && kv.Key == _likelySelfOwnerId;
                _heroNameByOwnerId.TryGetValue(kv.Key, out string? name);
                if (string.IsNullOrEmpty(name) && !isSelf)
                    continue;

                string nickname = ResolveNicknameForOwnerLocked(kv.Key, name, boundDbIds);
                rows.Add(new HeroShareEntry
                {
                    Name       = name ?? string.Empty,
                    Total60s   = kv.Value, // field name is historical — actual value here is encounter total
                    Percent    = kv.Value * 100.0 / totalEncounterDamage,
                    IsSelf     = isSelf,
                    PlayerName = nickname,
                    OwnerId    = kv.Key,
                });
            }

            // First: fold pet/summon rows into their chain-root player avatar row using the
            // edge map populated in OnDamageDealt.  This catches the rows that
            // CoalesceRowsByPlayerName CAN'T handle — anonymous summons (e.g. peer Magik's
            // demons) whose PlayerName is empty so name-based merging is impossible.  Must
            // run BEFORE the name-based pass so a successful pet-fold copies the root's
            // PlayerName into the merged row, giving the name-based pass something to work
            // with on the very rare case a third source (hero-swap proxy) also matches.
            rows = CoalesceRowsByPetChainRoot(rows, totalEncounterDamage);

            // Coalesce same-player rows (peer summons / hero-swap / proxy entities) BEFORE
            // sorting + truncating so the max cap reflects unique players, not entity ids.
            // See CoalesceRowsByPlayerName for the full rationale (the "Rasmis ×4" bug).
            rows = CoalesceRowsByPlayerName(rows, totalEncounterDamage);

            // Final fold: 2-tier-summon anon rows (player → pet → proxy chains where the
            // pet → player edge never appears on the wire) get attributed to the unique
            // named row playing the same hero.  See CoalesceAnonymousRowsByHeroName for
            // the heuristic.  Must run BEFORE the #XXXX synthesis below or the pass
            // never sees an empty PlayerName.
            rows = CoalesceAnonymousRowsByHeroName(rows, totalEncounterDamage);

            rows.Sort((a, b) =>
            {
                int c = b.Total60s.CompareTo(a.Total60s);
                return c != 0 ? c : string.CompareOrdinal(a.Name, b.Name);
            });
            if (rows.Count > max) rows.RemoveRange(max, rows.Count - max);

            // Same anonymous-tag pass as the 60s variant — peers without resolved nicknames
            // get a stable per-owner #XXXX hash so the user can distinguish unnamed players.
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                if (r.IsSelf) continue;
                if (!string.IsNullOrEmpty(r.PlayerName)) continue;

                string tag = "#" + (r.OwnerId & 0xFFFF).ToString("X4");
                rows[i] = new HeroShareEntry
                {
                    Name       = r.Name,
                    Percent    = r.Percent,
                    Total60s   = r.Total60s,
                    IsSelf     = r.IsSelf,
                    OwnerId    = r.OwnerId,
                    PlayerName = tag,
                };
            }
            return rows;
        }
    }

    /// <summary>Top-N rows by cumulative session total (normal / non-boss mode), ranked by
    /// absolute damage with percent computed against the sum of admitted owners since the
    /// region was entered.  Identical row shape and coalescing pipeline to
    /// <see cref="GetTopHeroesByEncounterShare"/> — only the source totals differ
    /// (<see cref="_sessionTotalsPerOwner"/> grows monotonically until region change,
    /// vs the encounter dict which is bounded by boss kill / freeze / clear).
    ///
    /// <para>This is the "DPS for all behaves like boss-only" leaderboard requested by users
    /// who want a Recount/Skada-style fight breakdown in normal mode without having to flip
    /// boss-only ON.  Reset triggers: region change, boss-only mode toggle.</para>
    ///
    /// <para>No mode gate (callers must check <see cref="BossOnlyMode"/> themselves and pick
    /// the right method) — keeping that decision at the call site lets the presenter switch
    /// data sources atomically with the title prefix and detail-line label.  Self row is
    /// force-emitted so the user is never invisible from their own leaderboard even if hero
    /// resolution fails (mid-session attach + custom-server hero whose powers aren't in the
    /// static HeroPowers index — same regression that motivated the equivalent guard on the
    /// 60 s and encounter variants).</para></summary>
    public IReadOnlyList<HeroShareEntry> GetTopHeroesBySessionShare(int max)
    {
        if (max <= 0) return Array.Empty<HeroShareEntry>();

        lock (_sync)
        {
            if (_sessionTotalsPerOwner.Count == 0)
                return Array.Empty<HeroShareEntry>();

            long totalSessionDamage = 0;
            foreach (var kv in _sessionTotalsPerOwner)
            {
                if (_heroNameByOwnerId.ContainsKey(kv.Key)
                    || (_likelySelfOwnerId != 0 && kv.Key == _likelySelfOwnerId))
                    totalSessionDamage += kv.Value;
            }
            if (totalSessionDamage <= 0)
                return Array.Empty<HeroShareEntry>();

            var boundDbIds = new HashSet<ulong>(_dbIdByAvatarId.Values);
            foreach (var cv in _dbIdByPlayerEntityId.Values) boundDbIds.Add(cv);

            var rows = new List<HeroShareEntry>(_sessionTotalsPerOwner.Count);
            foreach (var kv in _sessionTotalsPerOwner)
            {
                bool isSelf = _likelySelfOwnerId != 0 && kv.Key == _likelySelfOwnerId;
                _heroNameByOwnerId.TryGetValue(kv.Key, out string? name);
                if (string.IsNullOrEmpty(name) && !isSelf)
                    continue;

                string nickname = ResolveNicknameForOwnerLocked(kv.Key, name, boundDbIds);
                rows.Add(new HeroShareEntry
                {
                    Name       = name ?? string.Empty,
                    Total60s   = kv.Value, // field name is historical — actual value here is session total
                    Percent    = kv.Value * 100.0 / totalSessionDamage,
                    IsSelf     = isSelf,
                    PlayerName = nickname,
                    OwnerId    = kv.Key,
                });
            }

            // Same three-stage coalesce pipeline as the other two leaderboard methods —
            // pet/summon → chain root, peer-summon by player nickname, anonymous → unique
            // hero match.  Order matters: pet-chain first (fills empty PlayerName fields),
            // then by-name (now has names to match on), then anonymous-by-hero (final
            // catch-all for residual unnamed rows).
            rows = CoalesceRowsByPetChainRoot(rows, totalSessionDamage);
            rows = CoalesceRowsByPlayerName(rows, totalSessionDamage);
            rows = CoalesceAnonymousRowsByHeroName(rows, totalSessionDamage);

            rows.Sort((a, b) =>
            {
                int c = b.Total60s.CompareTo(a.Total60s);
                return c != 0 ? c : string.CompareOrdinal(a.Name, b.Name);
            });
            if (rows.Count > max) rows.RemoveRange(max, rows.Count - max);

            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                if (r.IsSelf) continue;
                if (!string.IsNullOrEmpty(r.PlayerName)) continue;

                string tag = "#" + (r.OwnerId & 0xFFFF).ToString("X4");
                rows[i] = new HeroShareEntry
                {
                    Name       = r.Name,
                    Percent    = r.Percent,
                    Total60s   = r.Total60s,
                    IsSelf     = r.IsSelf,
                    OwnerId    = r.OwnerId,
                    PlayerName = tag,
                };
            }
            return rows;
        }
    }

    /// <summary>Top-N heroes in AOI sorted by 60s damage, each with their share of the total
    /// hero damage in that window. Computed on demand under <see cref="_sync"/> so the caller
    /// always gets a coherent snapshot (no torn totals across concurrent mutations).</summary>
    /// <param name="max">Hard ceiling on the number of rows returned.  Caller-controlled so the
    /// meter doesn't need to know the UI layout — overlay passes 5.</param>
    /// <returns>Rows in descending damage order.  <see cref="HeroShareEntry.Percent"/> values
    /// inside the returned list sum to 100 (modulo FP rounding) and each row's
    /// <see cref="HeroShareEntry.IsSelf"/> flag is set iff the row's owner id equals the
    /// current <see cref="LikelySelfOwnerId"/> at snapshot time.
    /// Empty list when no hero damage has been scored in the window.</returns>
    public IReadOnlyList<HeroShareEntry> GetTopHeroesBy60sShare(int max)
    {
        if (max <= 0) return Array.Empty<HeroShareEntry>();

        lock (_sync)
        {
            // Sum candidate damage.  We include rows that EITHER have a resolved hero name
            // OR are credited to the current local-self avatar — without the second clause
            // the user's own row is dropped from the leaderboard whenever hero resolution
            // fails (cold-start mid-session + a power-enum that isn't in HeroPowers.Names —
            // commonly a custom-server hero that ships before the dump is regenerated).
            // Symptom: the user is dealing 70 % of total damage, sees "peer 100 %" in the
            // bar list, and concludes the meter is broken.  See the regression note in
            // .cursor/rules/dps-meter-diagnostics.mdc → "self row missing from leaderboard".
            long totalHeroDamage = 0;
            foreach (var kv in _totalsPerOwner)
            {
                if (_heroNameByOwnerId.ContainsKey(kv.Key)
                    || (_likelySelfOwnerId != 0 && kv.Key == _likelySelfOwnerId))
                    totalHeroDamage += kv.Value;
            }
            if (totalHeroDamage <= 0)
                return Array.Empty<HeroShareEntry>();

            // Pre-compute the set of dbIds that are ALREADY bound to a specific avatar id so
            // the community-slot fallback below doesn't steal a nickname that's already been
            // authoritatively paired with a different (on-screen) avatar.  This matters when
            // one nearby player joined via the EntityCreate path (binding intact) and another
            // joined mid-session (needs the slot-fallback) — we must never re-use the first
            // one's dbId for the second one's avatar.
            var boundDbIds = new HashSet<ulong>(_dbIdByAvatarId.Values);
            foreach (var cv in _dbIdByPlayerEntityId.Values) boundDbIds.Add(cv);

            var rows = new List<HeroShareEntry>(_totalsPerOwner.Count);
            foreach (var kv in _totalsPerOwner)
            {
                bool isSelf = _likelySelfOwnerId != 0 && kv.Key == _likelySelfOwnerId;
                _heroNameByOwnerId.TryGetValue(kv.Key, out string? name);

                // Same gate as the totalHeroDamage sum: peers without a hero name are still
                // skipped (mob / proxy noise), but the self row is always emitted so the
                // user can see their own contribution and the title can fall back to their
                // account nickname instead of an anonymous "DPS".
                if (string.IsNullOrEmpty(name) && !isSelf)
                    continue;

                string nickname = ResolveNicknameForOwnerLocked(kv.Key, name, boundDbIds);

                rows.Add(new HeroShareEntry
                {
                    Name       = name ?? string.Empty,
                    Total60s   = kv.Value,
                    Percent    = kv.Value * 100.0 / totalHeroDamage,
                    IsSelf     = isSelf,
                    PlayerName = nickname,
                    OwnerId    = kv.Key,
                });
            }
            // First: fold pet/summon rows into their chain-root player avatar row using the
            // edge map populated in OnDamageDealt.  Same rationale as the encounter variant —
            // anonymous summon rows whose PlayerName is empty would otherwise survive
            // CoalesceRowsByPlayerName and pollute the leaderboard with #F8B0/#F8B1/#F8B2
            // duplicates of one peer's pets.
            rows = CoalesceRowsByPetChainRoot(rows, totalHeroDamage);

            // Coalesce same-player rows (peer summons / hero-swap / proxy entities) BEFORE
            // sorting + truncating so the max cap reflects unique players, not entity ids.
            // See CoalesceRowsByPlayerName for the full rationale (the "Rasmis ×4" bug).
            rows = CoalesceRowsByPlayerName(rows, totalHeroDamage);

            // Final fold: 2-tier-summon anon rows (player → pet → proxy chains where the
            // pet → player edge never appears on the wire) get attributed to the unique
            // named row playing the same hero.  See CoalesceAnonymousRowsByHeroName for
            // the heuristic.  Must run BEFORE the #XXXX synthesis below or the pass
            // never sees an empty PlayerName.
            rows = CoalesceAnonymousRowsByHeroName(rows, totalHeroDamage);

            // Largest total first; ties broken by name for stable UI ordering across ticks.
            rows.Sort((a, b) =>
            {
                int c = b.Total60s.CompareTo(a.Total60s);
                return c != 0 ? c : string.CompareOrdinal(a.Name, b.Name);
            });
            if (rows.Count > max) rows.RemoveRange(max, rows.Count - max);

            // Synthesize a short owner-id hash as a pseudo-nickname for every row where
            // the real player name couldn't be resolved.
            //
            // Why every row and not just duplicates: the UI renders the left-aligned
            // portrait plus either the real nickname OR the hero name.  When the nickname
            // is empty for a single-occurrence hero, we used to fall back to the hero name
            // — which looks tidy but collapses meaningfully-different players into an
            // identical label as soon as a second one joins.  The user also reported that
            // "two rows look like the same Wolverine and the third Wolverine has no way
            // to tell it's a separate person".  Attaching the hash unconditionally gives
            // each unpaired row a stable, globally-unique identifier, so the viewer can
            // at least track who-is-who between updates (e.g. watch a specific #0DFF
            // climb the leaderboard) and it's immediately visually obvious which rows are
            // "real names" vs "anonymous".  The own-player row is never hashed — we
            // always know who WE are.  The hash is derived from the final 4 hex digits of
            // the owner entity id, which is stable within a region for the avatar's
            // lifetime.
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                if (r.IsSelf) continue;
                if (!string.IsNullOrEmpty(r.PlayerName)) continue;

                string tag = "#" + (r.OwnerId & 0xFFFF).ToString("X4");
                rows[i] = new HeroShareEntry
                {
                    Name       = r.Name,
                    Percent    = r.Percent,
                    Total60s   = r.Total60s,
                    IsSelf     = r.IsSelf,
                    OwnerId    = r.OwnerId,
                    PlayerName = tag,
                };
            }
            return rows;
        }
    }

    /// <summary>Coalesce leaderboard rows that share the same resolved <see cref="HeroShareEntry.PlayerName"/>
    /// (case-insensitive) into a single row per player.  Anonymous rows (empty
    /// <c>PlayerName</c>) pass through untouched — without a stable identity we can't
    /// safely claim two unnamed peers are the same person.
    ///
    /// <para>Why this is needed: a single peer routinely produces several scoring-owner
    /// entity ids in one encounter, all bearing the same hero icon and resolving to the
    /// same nickname:
    /// <list type="bullet">
    ///   <item>Summons / pets / clones (Storm wind elementals, Squirrel Girl squirrels,
    ///         Astral Form clones).  Each minion has its own ultimateOwnerEntityId, and
    ///         the OnDamageDealt pet-fold path only redirects SELF pets — peer pets stay
    ///         on their summon entity ids by design (we don't have authoritative peer-pet
    ///         ownership data and would risk mis-attributing trash hits).</item>
    ///   <item>Mid-encounter hero swap.  Switching to the same hero again creates a fresh
    ///         avatar entity id; the previous avatar's totals stay in the accumulator
    ///         until the encounter clears.</item>
    ///   <item>Multiple controller / weapon entities for heroes whose powers spawn
    ///         transient damage proxies (visible in production as "Rasmis ×4" with
    ///         splits like 154.9k / 40.4k / 12.2k / 3.9k).</item>
    /// </list>
    /// All of these read as a meter bug to the user — the leaderboard appears to be
    /// "tracking ghosts" — when in fact every row's underlying damage IS correctly attributed,
    /// just spread across entity ids belonging to one player.  Merging at render time is the
    /// least invasive fix: per-event scoring stays exact for owner-pin / re-election
    /// purposes, the user just sees one row per player.</para>
    ///
    /// <para>Identity tie-breaking (when merging two rows of the same player):</para>
    /// <list type="bullet">
    ///   <item><c>Total60s</c> sums.  The merged percent is recomputed against
    ///         <paramref name="denominator"/> so it stays consistent with the rest of the
    ///         board (no FP drift from summing per-row percents).</item>
    ///   <item><c>Name</c> / <c>OwnerId</c> are inherited from whichever source row had the
    ///         higher individual <c>Total60s</c> — that's the player's "primary" entity for
    ///         this fight, the one whose hero icon and entity id the user is most likely to
    ///         associate with them.</item>
    ///   <item><c>IsSelf</c> is OR'd.  If ANY source row was self (e.g. user's own avatar
    ///         got pinned but their summon's separate row also resolved to the user's
    ///         account name), the merged row is self — we never want to hide the user's
    ///         own contribution behind an apparent peer collision.</item>
    /// </list>
    ///
    /// <para>Trade-off: two players who actually share a nickname would be merged.  In
    /// MHServer nicknames are unique per server so this should never happen organically;
    /// even if it did, the user is far better served by one combined row than by four
    /// confusingly-split ones.</para></summary>
    /// <param name="rows">Pre-built row list (will be returned unchanged when nothing
    /// merges).  Caller is responsible for sorting / truncating AFTER this call so the
    /// max-row cap reflects unique players, not raw owner ids.</param>
    /// <param name="denominator">The total-damage figure used to compute the original
    /// per-row percents (60 s total or encounter total).  Required for percent re-computation
    /// after merging.  Must be &gt; 0 — caller has already validated this before constructing
    /// any rows.</param>
    /// <returns>A new list when at least one merge happened, otherwise the input list.
    /// Order is preserved among un-merged rows; merged rows occupy the position of their
    /// first appearance in the input.</returns>
    /// <summary>Coalesce leaderboard rows whose <see cref="HeroShareEntry.OwnerId"/> is a
    /// pet/summon entity into the row of its chain-root player avatar (per the
    /// <see cref="_petRootOwnerByEntity"/> map populated in <see cref="OnDamageDealt"/>).
    ///
    /// <para>This is the render-time complement to the scoring-time fold in OnDamageDealt:
    /// the scoring-time fold redirects NEW pet hits onto the root once the chain edge is
    /// known, but the FIRST few sub-second pet hits typically fire before <c>wireUlt</c>
    /// reveals the chain (the parent hasn't fired its own power yet, or its hero hasn't
    /// resolved into <c>_heroNameByOwnerId</c>).  Those early hits accumulate under the
    /// pet's own entity id, producing a separate row.  Once the chain root is discovered,
    /// the pet row is "stranded" with stale damage that will never grow further (all new
    /// hits go to the root via the scoring-time fold) — but it stays visible on the
    /// leaderboard until either the 60 s window drains it (60s variant) or the encounter
    /// ends (encounter variant).  This pass migrates that stranded damage to the root row
    /// at render time so the user always sees ONE row per player, regardless of when in
    /// the fight the chain was discovered.</para>
    ///
    /// <para>Identity tie-breaking when a pet row merges into a real avatar row that's
    /// also present (the common case: root has its own damage AND its pets have damage):
    /// <list type="bullet">
    ///   <item>Total: SUM of both rows' <c>Total60s</c> values.</item>
    ///   <item><c>Name</c> / <c>OwnerId</c>: from whichever side has the higher individual
    ///         total — that's the player's "primary" row this fight.</item>
    ///   <item><c>PlayerName</c>: prefer non-empty over empty (the anon row is exactly
    ///         what we're trying to collapse).</item>
    ///   <item><c>IsSelf</c>: OR'd, same rationale as <see cref="CoalesceRowsByPlayerName"/>.</item>
    /// </list></para>
    ///
    /// <para>Edge case — pet row exists but root row does NOT exist in the input list (the
    /// avatar never directly damaged a tracked target this fight, all its damage came from
    /// pets): the first pet row encountered carries the merge slot for that root, and any
    /// subsequent pet rows for the same root merge into it.  Result is one combined row
    /// with one of the pets' identities — still anonymous (it'll get a #XXXX tag in the
    /// downstream anonymous-tag pass) but at least it's ONE row instead of N.</para></summary>
    /// <param name="rows">Pre-built row list.  Caller is responsible for sorting / truncating
    /// AFTER this call so the max-row cap reflects unique players, not raw owner ids.</param>
    /// <param name="denominator">The total-damage figure used to compute per-row percents
    /// (60 s total or encounter total).  Required for percent re-computation after merging.
    /// Must be &gt; 0 — caller has already validated this before constructing any rows.</param>
    /// <returns>A new list when at least one merge happened, otherwise the input list.
    /// Order is preserved among un-merged rows; merged rows occupy the position of the
    /// first input row that mapped to a given chain root.</returns>
    private List<HeroShareEntry> CoalesceRowsByPetChainRoot(List<HeroShareEntry> rows, long denominator)
    {
        if (rows.Count <= 1 || denominator <= 0 || _petRootOwnerByEntity.Count == 0)
            return rows;

        // Group rows by their effective merge key:
        //   • for pet rows: chain-root player avatar entity id
        //   • for everyone else: own owner id (no-op)
        var indexByMergeKey = new Dictionary<ulong, int>(rows.Count);
        var coalesced = new List<HeroShareEntry>(rows.Count);
        bool anyMerged = false;

        foreach (var r in rows)
        {
            ulong mergeKey = r.OwnerId;
            if (_petRootOwnerByEntity.TryGetValue(r.OwnerId, out ulong root)
                && root != 0
                && root != r.OwnerId
                && !IsRenderTimeProtectedSelfRowLocked(r.OwnerId))
            {
                mergeKey = root;
            }

            if (indexByMergeKey.TryGetValue(mergeKey, out int idx))
            {
                var existing = coalesced[idx];
                bool existingWins = existing.Total60s >= r.Total60s;
                long sumTotal = existing.Total60s + r.Total60s;
                coalesced[idx] = new HeroShareEntry
                {
                    Name       = existingWins ? existing.Name : r.Name,
                    Total60s   = sumTotal,
                    Percent    = sumTotal * 100.0 / denominator,
                    IsSelf     = existing.IsSelf || r.IsSelf,
                    // Prefer ANY non-empty PlayerName over the anon empty one — collapsing
                    // an anon pet into a named root is the whole point of this pass.
                    PlayerName = !string.IsNullOrEmpty(existing.PlayerName) ? existing.PlayerName
                               : !string.IsNullOrEmpty(r.PlayerName)        ? r.PlayerName
                               : existing.PlayerName,
                    OwnerId    = existingWins ? existing.OwnerId : r.OwnerId,
                };
                anyMerged = true;
            }
            else
            {
                indexByMergeKey[mergeKey] = coalesced.Count;
                coalesced.Add(r);
            }
        }

        return anyMerged ? coalesced : rows;
    }

    /// <summary>Final-stage fold for anonymous summon rows that survived both
    /// <see cref="CoalesceRowsByPetChainRoot"/> and <see cref="CoalesceRowsByPlayerName"/>.
    ///
    /// <para>The earlier two passes can only act on edges that exist in
    /// <c>_petRootOwnerByEntity</c> or on rows that already share a resolved
    /// <see cref="HeroShareEntry.PlayerName"/>.  But the wire only ever carries
    /// proxy → pet via <c>UltimateOwnerEntityId</c> — never pet → player.  For 2-tier
    /// summons (player → pet → proxy, e.g. Beast clones, Magik demons, Doctor Strange
    /// astral copies) we record <c>proxy → pet</c> in <c>_petRootOwnerByEntity</c> but
    /// the pet itself never gets a <c>pet → player</c> edge from any PowerResult, so it
    /// stays an anonymous row with the pet's hero name set (because each summon entity
    /// has its own EntityCreate that resolves to the parent hero's prototype) but with
    /// an empty <c>PlayerName</c> (no dbId binding for a non-avatar entity).</para>
    ///
    /// <para>Render-time heuristic: when a row has empty <c>PlayerName</c> (unresolved
    /// nick — common when <c>_dbIdByAvatarId</c> never bound that avatar this session)
    /// but we've proven via <see cref="OnDamageDealt"/> that its <c>OwnerId</c> is a
    /// pet/proxy (it exists as a key in <see cref="_petRootOwnerByEntity"/>), AND its
    /// hero name matches EXACTLY ONE row on the leaderboard that already has a resolved
    /// nickname, fold the pet's damage into that named row.</para>
    ///
    /// <para>**Hard guards (Apr 2026 take-2):** (a) Never fold <see cref="HeroShareEntry.IsSelf"/>
    /// rows — the local player's avatar often has an empty <c>PlayerName</c> when
    /// bindings=0, but it is NOT a summon; folding it into the only named peer playing
    /// the same hero (e.g. merge self Blade into "Mrbil") was a catastrophic regression.
    /// (b) Primary fold candidates must appear in <c>_petRootOwnerByEntity</c> after
    /// scoring-time pet discovery. (c) **Orphan summon** (Apr 2026 take-3): if the wire
    /// never produced a pet-map edge (e.g. some Doctor Strange projection entities) but
    /// the row has no <c>_dbIdByAvatarId</c> binding, is not <c>_likelySelfOwnerId</c>,
    /// and is not in <c>_localAvatarEntityIds</c>, treat it like a summon when exactly
    /// one named row shares the hero — real peer avatars carry dbIds and stay separate.</para>
    ///
    /// <para>The pass MUST run AFTER the player-name pass (so the "exactly one named
    /// row per hero" check is well-defined) and BEFORE the sort + truncate (so merged
    /// totals get the correct rank when the leader changes).  It MUST also run BEFORE
    /// the <c>#XXXX</c> tag synthesis at the bottom of <c>GetTopHeroes…</c> — once the
    /// anon row has a synthesized tag its <c>PlayerName</c> is no longer empty, which
    /// would prevent the merge.</para>
    ///
    /// <para>Identity tie-breaking when an anon merges into a named row:
    /// <list type="bullet">
    ///   <item><c>Name</c>: kept (already identical).</item>
    ///   <item>Total / Percent: SUM of both rows' totals.</item>
    ///   <item><c>PlayerName</c>: kept from the named row (entire point of the merge).</item>
    ///   <item><c>OwnerId</c>: kept from the named row (the player avatar id, not the
    ///         pet — keeps state-dump diagnostics correlatable across ticks).</item>
    ///   <item><c>IsSelf</c>: OR'd; pet-of-self folds set IsSelf=true on the merged row
    ///         even if the named row was a peer (defensive — should never happen
    ///         because pet-of-self has its own scoring-time fold via
    ///         _localPlayerEntityId / power-proto matchers, but covers the case
    ///         where a custom-server hero ships before HeroPowers.cs is regenerated).</item>
    /// </list></para>
    ///
    /// <para>One-shot diagnostic per (hero, anonOwnerId) pair so post-hoc log analysis
    /// can grep <c>anon-by-hero fold</c> to see exactly which summon entities got
    /// rescued by this pass and verify the heuristic isn't over-folding.</para></summary>
    private List<HeroShareEntry> CoalesceAnonymousRowsByHeroName(List<HeroShareEntry> rows, long denominator)
    {
        if (rows.Count <= 1 || denominator <= 0)
            return rows;

        // Index: hero name → list of (rowIndex, hasName).  Two passes — first build the
        // index, then walk anons looking for exactly-one named match.
        var byHero = new Dictionary<string, (int namedIdx, int namedCount, int anonCount)>(rows.Count, StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            if (string.IsNullOrEmpty(r.Name)) continue;
            byHero.TryGetValue(r.Name, out var st);
            if (!string.IsNullOrEmpty(r.PlayerName))
            {
                st.namedIdx = (st.namedCount == 0) ? i : st.namedIdx; // keep first named idx
                st.namedCount++;
            }
            else
            {
                // Pet-map rows + orphan summons (wire never registered _petRootOwnerByEntity).
                // Self / player avatars: excluded via IsSelf, IsRenderTimeProtectedSelfRowLocked,
                // and IsOrphanSummonRowForAnonHeroFoldLocked.
                if (!r.IsSelf
                    && !IsRenderTimeProtectedSelfRowLocked(r.OwnerId)
                    && (_petRootOwnerByEntity.ContainsKey(r.OwnerId)
                        || IsOrphanSummonRowForAnonHeroFoldLocked(r.OwnerId)))
                    st.anonCount++;
            }
            byHero[r.Name] = st;
        }

        // Quick-out when no pet-proxy-into-named opportunity exists at all.
        bool anyOpportunity = false;
        foreach (var kv in byHero)
        {
            if (kv.Value.anonCount <= 0) continue;
            if (kv.Value.namedCount == 1)
            {
                anyOpportunity = true;
                break;
            }
            // Two+ humans on the same hero: still fold when pet-map picks a unique named target.
            for (int ai = 0; ai < rows.Count && !anyOpportunity; ai++)
            {
                var ar = rows[ai];
                if (ar.IsSelf || IsRenderTimeProtectedSelfRowLocked(ar.OwnerId)) continue;
                if (!string.IsNullOrEmpty(ar.PlayerName) || string.IsNullOrEmpty(ar.Name)) continue;
                if (!string.Equals(ar.Name, kv.Key, StringComparison.OrdinalIgnoreCase)) continue;
                if (!_petRootOwnerByEntity.TryGetValue(ar.OwnerId, out ulong proot)
                    || proot == 0
                    || proot == ar.OwnerId)
                    continue;
                if (TryGetPetMapNamedMergeSourceIndexLocked(ar, rows, proot, out _))
                    anyOpportunity = true;
            }
        }
        if (!anyOpportunity) return rows;

        // Build the merged output.  Anons with exactly-one-named-target get folded into
        // the target's slot; everything else passes through unchanged.
        var coalesced = new List<HeroShareEntry>(rows.Count);
        var addedNamedIdx = new Dictionary<int, int>(); // original index → coalesced index
        bool anyMerged = false;

        for (int i = 0; i < rows.Count; i++)
        {
            var r = rows[i];

            // Pet-map or orphan-summon row eligible for fold?  (Never the local avatar — IsSelf
            // + IsRenderTimeProtectedSelfRowLocked cover pin drift / stale encounter keys.)
            if (!r.IsSelf
                && !IsRenderTimeProtectedSelfRowLocked(r.OwnerId)
                && string.IsNullOrEmpty(r.PlayerName)
                && !string.IsNullOrEmpty(r.Name)
                && (_petRootOwnerByEntity.ContainsKey(r.OwnerId)
                    || IsOrphanSummonRowForAnonHeroFoldLocked(r.OwnerId))
                && byHero.TryGetValue(r.Name, out var st)
                && TryResolveAnonByHeroFoldTargetLocked(r, rows, st, out int targetSrcIdx))
            {
                int targetIdx = EnsureCoalescedNamedTargetForAnonFold(rows, coalesced, addedNamedIdx, targetSrcIdx);

                var existing = coalesced[targetIdx];
                long sumTotal = existing.Total60s + r.Total60s;
                coalesced[targetIdx] = new HeroShareEntry
                {
                    Name       = existing.Name,
                    Total60s   = sumTotal,
                    Percent    = sumTotal * 100.0 / denominator,
                    IsSelf     = existing.IsSelf || r.IsSelf,
                    PlayerName = existing.PlayerName,
                    OwnerId    = existing.OwnerId,
                };
                anyMerged = true;
                if (_loggedAnonByHeroFolds.Add((r.OwnerId, existing.OwnerId)))
                {
                    string via = _petRootOwnerByEntity.ContainsKey(r.OwnerId) ? "pet-map" : "orphan summon (no pet-map edge)";
                    Diagnostic?.Invoke($"DpsMeter: anon-by-hero fold ({via}) — owner={r.OwnerId} (hero='{r.Name}', total={r.Total60s}) → named row owner={existing.OwnerId} player='{existing.PlayerName}'");
                }
                continue;
            }

            if (!string.IsNullOrEmpty(r.PlayerName))
            {
                bool dup = false;
                for (int c = 0; c < coalesced.Count; c++)
                {
                    if (coalesced[c].OwnerId != r.OwnerId) continue;
                    dup = true;
                    break;
                }
                if (dup) continue;
            }

            coalesced.Add(r);
        }

        return anyMerged ? coalesced : rows;
    }

    private static bool TryGetPetMapNamedMergeSourceIndexLocked(
        HeroShareEntry anonRow,
        List<HeroShareEntry> rows,
        ulong root,
        out int namedSourceIndex)
    {
        namedSourceIndex = -1;
        if (string.IsNullOrEmpty(anonRow.Name) || root == 0 || root == anonRow.OwnerId)
            return false;
        for (int j = 0; j < rows.Count; j++)
        {
            var x = rows[j];
            if (string.IsNullOrEmpty(x.PlayerName)) continue;
            if (!string.Equals(x.Name, anonRow.Name, StringComparison.OrdinalIgnoreCase)) continue;
            if (x.OwnerId != root) continue;
            if (namedSourceIndex >= 0)
            {
                namedSourceIndex = -1;
                return false;
            }
            namedSourceIndex = j;
        }
        return namedSourceIndex >= 0;
    }

    private bool TryResolveAnonByHeroFoldTargetLocked(
        HeroShareEntry r,
        List<HeroShareEntry> rows,
        (int namedIdx, int namedCount, int anonCount) st,
        out int targetSourceIndex)
    {
        targetSourceIndex = -1;
        if (st.namedCount == 1)
        {
            targetSourceIndex = st.namedIdx;
            return true;
        }
        if (!_petRootOwnerByEntity.TryGetValue(r.OwnerId, out ulong root)
            || root == 0
            || root == r.OwnerId)
            return false;
        return TryGetPetMapNamedMergeSourceIndexLocked(r, rows, root, out targetSourceIndex);
    }

    private static int EnsureCoalescedNamedTargetForAnonFold(
        List<HeroShareEntry> rows,
        List<HeroShareEntry> coalesced,
        Dictionary<int, int> addedNamedIdx,
        int targetSourceIndex)
    {
        if (addedNamedIdx.TryGetValue(targetSourceIndex, out int targetIdx))
            return targetIdx;
        ulong wantOwner = rows[targetSourceIndex].OwnerId;
        for (int c = 0; c < coalesced.Count; c++)
        {
            if (coalesced[c].OwnerId != wantOwner) continue;
            addedNamedIdx[targetSourceIndex] = c;
            return c;
        }
        targetIdx = coalesced.Count;
        coalesced.Add(rows[targetSourceIndex]);
        addedNamedIdx[targetSourceIndex] = targetIdx;
        return targetIdx;
    }

    private static List<HeroShareEntry> CoalesceRowsByPlayerName(List<HeroShareEntry> rows, long denominator)
    {
        if (rows.Count <= 1 || denominator <= 0)
            return rows;

        var indexByName = new Dictionary<string, int>(rows.Count, StringComparer.OrdinalIgnoreCase);
        var coalesced = new List<HeroShareEntry>(rows.Count);
        bool anyMerged = false;

        foreach (var r in rows)
        {
            // Anonymous rows can't be safely merged — pass through with stable order so the
            // anonymous-tag pass downstream still hands out per-owner #XXXX hashes.
            if (string.IsNullOrEmpty(r.PlayerName))
            {
                coalesced.Add(r);
                continue;
            }

            if (indexByName.TryGetValue(r.PlayerName, out int idx))
            {
                var existing = coalesced[idx];
                bool existingWins = existing.Total60s >= r.Total60s;
                long sumTotal = existing.Total60s + r.Total60s;
                coalesced[idx] = new HeroShareEntry
                {
                    Name       = existingWins ? existing.Name : r.Name,
                    Total60s   = sumTotal,
                    Percent    = sumTotal * 100.0 / denominator,
                    IsSelf     = existing.IsSelf || r.IsSelf,
                    PlayerName = existing.PlayerName,
                    OwnerId    = existingWins ? existing.OwnerId : r.OwnerId,
                };
                anyMerged = true;
            }
            else
            {
                indexByName[r.PlayerName] = coalesced.Count;
                coalesced.Add(r);
            }
        }

        return anyMerged ? coalesced : rows;
    }

    /// <summary>Returns true when <paramref name="ownerEntityId"/> is bound (via
    /// <see cref="OnEntityCreated"/>) to a non-zero account dbId that differs from
    /// <see cref="_selfDbId"/>. Caller MUST hold <c>_sync</c>.</summary>
    /// <remarks>When <c>_selfDbId == 0</c> (never captured to <c>dps-self.json</c> and no
    /// container EntityCreate this session), this always returns false — the same-hero power
    /// merge paths cannot disambiguate two Blades and may still mis-credit until self dbId
    /// is known.</remarks>
    private bool IsForeignAccountAvatarLocked(ulong ownerEntityId)
    {
        if (_selfDbId == 0)
            return false;
        return _dbIdByAvatarId.TryGetValue(ownerEntityId, out ulong db)
            && db != 0
            && db != _selfDbId;
    }

    /// <summary>True when <paramref name="heroName"/> appears in <see cref="_heroNameByOwnerId"/>
    /// for <see cref="_likelySelfOwnerId"/> and for no other entity id. Used to allow
    /// summon/pet powers whose <see cref="HeroPowers"/> bucket differs from the parent hero
    /// (e.g. Blade's Ultron drones use <c>Powers/Player/Ultron/*</c> → hero string "Ultron"
    /// while the avatar row is "Blade") without re-introducing duplicate-hero mis-credit: if
    /// two Blades are in AOI, both map to "Blade" and this returns false.</summary>
    private bool IsOnlyThisAvatarPlayingHeroLocked(string heroName)
    {
        if (_likelySelfOwnerId == 0 || string.IsNullOrEmpty(heroName))
            return false;
        bool seenSelf = false, seenOther = false;
        foreach (var kv in _heroNameByOwnerId)
        {
            if (!string.Equals(kv.Value, heroName, StringComparison.OrdinalIgnoreCase))
                continue;
            if (kv.Key == _likelySelfOwnerId) seenSelf = true;
            else seenOther = true;
        }
        return seenSelf && !seenOther;
    }

    /// <summary>Among entities in <see cref="_heroNameByOwnerId"/> whose value equals
    /// <paramref name="powerHero"/> (case-insensitive), returns true when exactly one key is
    /// player-bound (<see cref="_dbIdByAvatarId"/> non-zero). Summons/minions are not in that
    /// map, so crogg's avatar can be the sole "Ultron" row while his drones are not counted.
    /// Caller MUST hold <c>_sync</c>.</summary>
    private bool TryGetSolePlayerHeroAvatarForPowerHeroLocked(string powerHero, out ulong avatarEntityId)
    {
        avatarEntityId = 0;
        if (string.IsNullOrEmpty(powerHero))
            return false;
        foreach (var kv in _heroNameByOwnerId)
        {
            if (!string.Equals(kv.Value, powerHero, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!_dbIdByAvatarId.TryGetValue(kv.Key, out ulong db) || db == 0)
                continue;
            if (avatarEntityId != 0 && avatarEntityId != kv.Key)
            {
                avatarEntityId = 0;
                return false;
            }
            avatarEntityId = kv.Key;
        }
        return avatarEntityId != 0;
    }

    /// <summary>True when <paramref name="ownerId"/> must never be treated as an anonymous
    /// summon row in <see cref="CoalesceAnonymousRowsByHeroName"/> — current self pin, or any
    /// avatar entity id still bound to <see cref="_selfDbId"/> (stale encounter totals after a
    /// same-region avatar / team-up entity swap).</summary>
    /// <remarks>Peer summons (symbiote hooks, drones, etc.) sometimes inherit a bogus
    /// <c>_dbIdByAvatarId</c> pairing to a nearby player's dbId. If we treat that as
    /// "protected self", <see cref="CoalesceRowsByPetChainRoot"/> refuses to redirect
    /// <c>mergeKey</c> to the chain root — the pet row stays split and anon-by-hero may also
    /// refuse when two humans play the same hero. Keys in <see cref="_petRootOwnerByEntity"/>
    /// are always pet/summon <c>wirePow</c> ids, never stale human avatars, so we exclude them
    /// from dbId-only protection.</remarks>
    private bool IsRenderTimeProtectedSelfRowLocked(ulong ownerId)
    {
        if (ownerId == 0) return false;
        if (_likelySelfOwnerId != 0 && ownerId == _likelySelfOwnerId) return true;
        if (_petRootOwnerByEntity.ContainsKey(ownerId))
            return false;
        return _selfDbId != 0
            && _dbIdByAvatarId.TryGetValue(ownerId, out ulong db)
            && db != 0
            && db == _selfDbId;
    }

    /// <summary>True when <paramref name="ownerId"/> is safe to fold in
    /// <see cref="CoalesceAnonymousRowsByHeroName"/> without a <c>_petRootOwnerByEntity</c>
    /// entry: not the self pin, not a local avatar entity, not account-bound as avatar or
    /// player container — excludes real players missing only a nickname when two humans
    /// share a hero (both have dbIds; <c>namedCount</c> ≥ 2).</summary>
    private bool IsOrphanSummonRowForAnonHeroFoldLocked(ulong ownerId)
    {
        if (ownerId == 0 || ownerId == _likelySelfOwnerId)
            return false;
        if (_petRootOwnerByEntity.ContainsKey(ownerId))
            return false;
        if (_localAvatarEntityIds.Contains(ownerId))
            return false;
        if (_dbIdByAvatarId.TryGetValue(ownerId, out ulong db) && db != 0)
            return false;
        if (_dbIdByPlayerEntityId.ContainsKey(ownerId))
            return false;
        return true;
    }

    /// <summary>
    /// Shared nickname-resolution logic used both by the leaderboard builder and by the
    /// periodic state-dump diagnostic.  Caller MUST hold <c>_sync</c>.  The method mirrors
    /// exactly what <see cref="GetTopHeroesBy60sShare"/> would compute for a given owner/hero
    /// pair, so log lines and on-screen labels can never diverge during triage.
    /// </summary>
    /// <param name="ownerEntityId">Avatar entity id (the key inside <c>_heroNameByOwnerId</c> /
    /// <c>_totalsPerOwner</c>).</param>
    /// <param name="heroName">Hero display name for the row — required for the community-slot
    /// fallback to find exactly-one-match peers.  May be null if hero resolution has failed.</param>
    /// <param name="boundDbIds">Pre-computed set of dbIds that already own a different
    /// on-screen avatar — prevents stealing nicknames across overlapping rows.  Callers in
    /// hot paths pass a pre-built HashSet; callers doing a one-shot lookup pass null and we
    /// compute it on the fly.</param>
    /// <returns>Resolved nickname or empty string when no unambiguous mapping exists.</returns>
    private string ResolveNicknameForOwnerLocked(ulong ownerEntityId, string? heroName, HashSet<ulong>? boundDbIds)
    {
        string nickname = string.Empty;
        ulong dbId = 0;
        if (_dbIdByAvatarId.TryGetValue(ownerEntityId, out dbId)
            || (_playerEntityIdByAvatarId.TryGetValue(ownerEntityId, out ulong containerId)
                && _dbIdByPlayerEntityId.TryGetValue(containerId, out dbId)))
        {
            _playerNameByDbId.TryGetValue(dbId, out string? nameByDb);
            nickname = nameByDb ?? string.Empty;
        }

        // ── Self-row short-circuit ──────────────────────────────────────────────────────────
        // The two-pass disambiguator below works by scanning the ENTIRE _currentHeroNameByDbId
        // cache for peers playing the same hero — that's structurally wrong for the self row.
        // Reasons:
        //   (a) The local player's own dbId is NEVER in _nearbyDbIds (the server doesn't
        //       broadcast a CommunityMember "Nearby" event for us), so pass 1 (nearbyOnly=true)
        //       can't pick our nickname even if we have one cached on disk — it's always going
        //       to skip us.
        //   (b) Pass 2 (wide) then walks the full dict and the FIRST peer whose hero matches
        //       wins. With multiple peers playing the same hero (two Blades, two Storms, …)
        //       this picks one of them, merging our damage row into theirs.
        // The fix is to never run the disambiguator for ownerEntityId == _likelySelfOwnerId.
        // We do try the persisted _selfDbId as a one-shot fallback first — that's the
        // "restart-without-zone" case where the in-memory _dbIdByAvatarId binding is empty
        // because the server didn't replay our EntityCreate, but the disk-persisted self-dbId
        // still has our nick from a prior session. If even THAT is empty, we return empty so
        // the caller falls back to the player nickname / "you" branch in DpsOverlayWindow's
        // RenderTopHeroes (rather than picking up a peer's nickname).
        if (ownerEntityId != 0 && ownerEntityId == _likelySelfOwnerId)
        {
            if (string.IsNullOrEmpty(nickname) && _selfDbId != 0
                && _playerNameByDbId.TryGetValue(_selfDbId, out string? selfNameByDb)
                && !string.IsNullOrEmpty(selfNameByDb))
            {
                nickname = selfNameByDb;
            }
            return nickname;
        }

        if (!string.IsNullOrEmpty(nickname) || string.IsNullOrEmpty(heroName))
            return nickname;

        // Build the bound-dbId filter if the caller didn't supply one.  One allocation per
        // diagnostic tick — negligible compared to the String.Builder churn the caller is
        // already doing.
        if (boundDbIds == null)
        {
            boundDbIds = new HashSet<ulong>(_dbIdByAvatarId.Values);
            foreach (var cv in _dbIdByPlayerEntityId.Values) boundDbIds.Add(cv);
        }

        // Two-pass disambiguation: the persistent _playerNameByDbId cache holds every peer
        // we've ever seen (150+ entries across a multi-session save file), and many popular
        // heroes get played by multiple people on our friends list simultaneously.  If we
        // search the full dbId space we'll almost always get matchCount > 1 on a crowded
        // hero like Rogue / Storm / Squirrel Girl → the resolver bails and the user sees
        // the #XXXX hash tag instead of the real nick.
        //
        //   Pass 1 (tight): restrict candidates to dbIds the server has tagged as Nearby
        //                   this region.  Typical AOI has 2-6 peers, so ambiguity is rare.
        //   Pass 2 (wide):  if pass 1 found ZERO candidates (the peer entered AOI before we
        //                   started listening, or the Nearby broadcast got lost), relax and
        //                   search the whole dict.  Still skips ambiguous cases — no reason
        //                   to guess wrong when a hash tag is an honest alternative.
        //
        // The pass-1 zero-match -> pass-2 semantics matters specifically because users
        // restart the app mid-session; the first Nearby broadcast after restart is often
        // delayed until the peer next swaps heroes, leaving the set temporarily empty.
        string? pass1 = TryUniqueHeroMatch(heroName, boundDbIds, nearbyOnly: true);
        if (!string.IsNullOrEmpty(pass1)) return pass1;
        string? pass2 = TryUniqueHeroMatch(heroName, boundDbIds, nearbyOnly: false);
        if (!string.IsNullOrEmpty(pass2)) return pass2;
        return nickname;
    }

    /// <summary>Walk <see cref="_currentHeroNameByDbId"/> looking for exactly one dbId whose
    /// current slot-hero matches <paramref name="heroName"/>, whose dbId isn't already bound
    /// to a different on-screen avatar, and whose persistent nickname is known.  When
    /// <paramref name="nearbyOnly"/> is true, additionally requires the dbId to be in
    /// <see cref="_nearbyDbIds"/> — see the two-pass call site for why.</summary>
    /// <returns>Nickname on unique match, empty string on zero OR ambiguous matches.</returns>
    private string TryUniqueHeroMatch(string heroName, HashSet<ulong> boundDbIds, bool nearbyOnly)
    {
        ulong matchedDbId = 0;
        int matchCount = 0;
        foreach (var entry in _currentHeroNameByDbId)
        {
            if (!string.Equals(entry.Value, heroName, StringComparison.Ordinal)) continue;
            if (boundDbIds.Contains(entry.Key)) continue;
            if (!_playerNameByDbId.ContainsKey(entry.Key)) continue;
            if (nearbyOnly && !_nearbyDbIds.Contains(entry.Key)) continue;

            matchCount++;
            matchedDbId = entry.Key;
            if (matchCount > 1) break;
        }
        if (matchCount == 1
            && _playerNameByDbId.TryGetValue(matchedDbId, out string? inferredName)
            && !string.IsNullOrEmpty(inferredName))
        {
            return inferredName;
        }
        return string.Empty;
    }

    /// <summary>Convenience wrapper for non-hot-path callers (state dump): builds the
    /// boundDbIds filter itself so callers don't have to.</summary>
    private string ResolveNicknameForOwner(ulong ownerEntityId, string? heroName)
        => ResolveNicknameForOwnerLocked(ownerEntityId, heroName, boundDbIds: null);

    public void Dispose()
    {
        _sniffer.DamageDealt -= OnDamageDealt;
        _sniffer.RegionChanged -= OnRegionChanged;
        _sniffer.EntityCreated -= OnEntityCreated;
        _sniffer.LocalPlayerIdentified -= OnLocalPlayerIdentified;
        _sniffer.InventoryMoved -= OnInventoryMoved;
        _sniffer.LocalAvatarObserved -= OnLocalAvatarObserved;
        _sniffer.CommunityMemberUpdated -= OnCommunityMemberUpdated;
        _sniffer.EntityKilled -= OnEntityKilled;
        _sniffer.EntityDestroyed -= OnEntityDestroyed;
        // Final flush on shutdown — picks up any record set since the last write that didn't
        // trigger an intra-session save (belt-and-braces; the in-flight saves already cover it).
        SaveMaxHits();
    }
}
