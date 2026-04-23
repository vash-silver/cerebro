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

    /// <summary>One-shot dedup for the "target prototype isn't a boss" diagnostic path. Keyed by
    /// target <c>prototypeEnumIndex</c> so we log each distinct mob type exactly once per session
    /// (or until region change, which clears it). Without this a sustained AOE on trash packs
    /// would spam the log with hundreds of identical rejection lines per second.</summary>
    private readonly HashSet<uint> _loggedNonBossTargets = new();

    /// <summary>Sibling of <see cref="_loggedNonBossTargets"/> for the "target prototype unknown"
    /// (missed EntityCreate) branch. Keyed by entity id — each spawned target gets exactly one
    /// diagnostic line, so a long fight against an un-observed boss doesn't flood the log.</summary>
    private readonly HashSet<ulong> _loggedUnknownBossTargets = new();

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
    /// <c>TargetEntityId</c> doesn't resolve to a prototype in
    /// <see cref="BossPrototypes.Indices"/> (Boss / GroupBoss / MiniBoss). Trash packs, summons,
    /// world-destructibles etc. no longer contribute to the sliding windows, so the overlay's
    /// numbers reflect encounter-only throughput.
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
                CurrentDps = 0;
                CurrentOwnerTotal60s = 0;
                // Re-arm the boss-fight idle detector so the very next hit doesn't get
                // mis-classified as "post-idle" (the previous _lastBossAdmittedUtc is now
                // meaningless given we just wiped the windows).
                _lastBossAdmittedUtc = DateTime.MinValue;
            }
            Diagnostic?.Invoke($"DpsMeter: BossOnlyMode = {value} (scoring windows cleared)");
            DpsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private bool _bossOnlyMode;

    /// <summary>UTC timestamp of the most recent boss-admitted hit (i.e. a hit that survived the
    /// <see cref="BossOnlyMode"/> filter and was scored).  Used to detect the
    /// "between bosses" idle gap so we can wipe stale leaderboard rows when a new fight starts —
    /// see <see cref="BossFightIdleReset"/>.  <c>DateTime.MinValue</c> means we've never admitted
    /// a hit in the current session (or the meter was just cleared).  Lock <see cref="_sync"/>.</summary>
    private DateTime _lastBossAdmittedUtc = DateTime.MinValue;

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

        LoadMaxHits();
        LoadPlayerIndex();
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
        }

        if (added)
            Diagnostic?.Invoke($"DpsMeter: local avatar registered via power activation (id={e.LocalAvatarEntityId}) — authoritative mode ON");
        if (pinFlipped)
            Diagnostic?.Invoke($"DpsMeter: self-owner pinned {prevPin} -> {e.LocalAvatarEntityId} (from client power-activation)");

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
        }
        Diagnostic?.Invoke($"DpsMeter: local player identified (id={e.LocalPlayerEntityId}) — enabling authoritative self-owner pinning");
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
        if (e.DatabaseUniqueId != 0 || e.IsAvatar)
        {
            lock (_sync)
            {
                if (e.DatabaseUniqueId != 0)
                    _dbIdByPlayerEntityId[e.EntityId] = e.DatabaseUniqueId;

                // Fast path: the sniffer managed to extract the nickname + owner dbId
                // directly from the Avatar's transient archive (see
                // ScanAvatarPlayerName).  This bypasses the entire
                // ModifyCommunityMember temporal correlation and — crucially — works
                // for players already in your Guild/Friends circle, whose
                // community-member broadcast is silent on PlayerName (Community.cs
                // only sets NewlyCreated for members with zero prior circles).  The
                // above fallback path still runs for avatars whose archive the scanner
                // couldn't decode confidently (truncated blob, unusual name shape).
                if (e.IsAvatar && e.OwnerPlayerDbId != 0 && !string.IsNullOrEmpty(e.PlayerName)
                    && !_dbIdByAvatarId.ContainsKey(e.EntityId))
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
            else if (!BossPrototypes.IsBoss(targetProtoIdx))
            {
                if (_loggedNonBossTargets.Add(targetProtoIdx))
                    Diagnostic?.Invoke($"DpsMeter: boss-filter drop — target protoIdx={targetProtoIdx} is not in BossPrototypes set (Boss+GroupBoss+MiniBoss). Add it if you expected it to count.");
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

            // ── Boss-fight auto-reset (boss-only mode) ──────────────────────────────────────
            // We've already passed the boss-filter above — this hit IS going to count.  If
            // the previous boss-admitted hit was more than BossFightIdleReset ago, treat
            // this as the start of a NEW boss fight: wipe the sliding windows so the
            // leaderboard doesn't blend stale data from the *previous* boss into the
            // current encounter.  See field comment on BossFightIdleReset for rationale.
            //
            // Two guards prevent unwanted resets:
            //   • _bossOnlyMode — in all-damage mode continuous decay is the right behavior.
            //   • _lastBossAdmittedUtc != MinValue — first hit of session shouldn't trigger
            //     a "reset" diagnostic; just initialise the timestamp normally below.
            if (_bossOnlyMode
                && _lastBossAdmittedUtc != DateTime.MinValue
                && now - _lastBossAdmittedUtc >= BossFightIdleReset
                && (_scoring.Count > 0 || _totalsPerOwner.Count > 0))
            {
                TimeSpan gap = now - _lastBossAdmittedUtc;
                int rowsCleared = _totalsPerOwner.Count;
                _scoring.Clear();
                _instant.Clear();
                _totalsPerOwner.Clear();
                CurrentDps = 0;
                CurrentOwnerTotal60s = 0;
                Diagnostic?.Invoke($"DpsMeter: boss-fight auto-reset (idle {gap.TotalSeconds:F1}s ≥ {BossFightIdleReset.TotalSeconds:F0}s, cleared {rowsCleared} rows) — starting fresh fight");
            }
            _lastBossAdmittedUtc = now;

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
            if (_likelySelfOwnerId != 0
                && scoringOwner != _likelySelfOwnerId
                && e.PowerPrototypeEnumIndex != 0
                && HeroPowers.TryGetHero(e.PowerPrototypeEnumIndex, out string proxyHero)
                && _heroNameByOwnerId.TryGetValue(_likelySelfOwnerId, out string? pinHero)
                && string.Equals(proxyHero, pinHero, StringComparison.Ordinal))
            {
                scoringOwner = _likelySelfOwnerId;
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
            bool ownerIsSelf = _localAvatarEntityIds.Contains(scoringOwner)
                || (_localPlayerEntityId != 0
                    && (rawOwner == _localPlayerEntityId || wirePow == _localPlayerEntityId));
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

            changed = Math.Abs(newDps - CurrentDps) > 0.5 || newOwner != prevSelfOwner || maxChanged || heroChanged;
            CurrentDps = newDps;
            CurrentOwnerTotal60s = newOwnerTotal;
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

        lock (_sync)
        {
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

            changed = Math.Abs(newDps - CurrentDps) > 0.5
                   || newOwnerTotal != CurrentOwnerTotal60s
                   || ownerAfter != ownerBefore
                   || heroChanged;
            CurrentDps = newDps;
            CurrentOwnerTotal60s = newOwnerTotal;

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
                  .Append(" 60s=").Append(CurrentOwnerTotal60s)
                  .Append(" dps=").Append((long)CurrentDps)
                  .Append(" rows=").Append(_totalsPerOwner.Count)
                  .Append(" localAvatars=[").Append(string.Join(",", _localAvatarEntityIds))
                  .Append("] localPlayer=").Append(_localPlayerEntityId);
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
        if (stateDump != null)
            Diagnostic?.Invoke(stateDump);
        if (changed) DpsChanged?.Invoke(this, EventArgs.Empty);
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
            _instant.Clear();
            _likelySelfOwnerId = 0;
            _likelySelfChosenAt = default;
            CurrentDps = 0;
            CurrentOwnerTotal60s = 0;
            // MaxSingleHit is NOT reset here — it's the hero's all-time personal best, not a
            // per-region number. The next DamageDealt event will re-load it from
            // _maxHitByHeroName once the avatar's display name is re-identified.
            MaxSingleHit = 0;
            CurrentHeroDisplayName = string.Empty;
            _loggedUnknownHeroes.Clear();
            _loggedNonBossTargets.Clear();
            _loggedUnknownBossTargets.Clear();

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
            _lastBossAdmittedUtc = DateTime.MinValue;

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
            // Two-pass: first sum hero-only totals (excluding any non-hero leftovers that may
            // still be in _totalsPerOwner — shouldn't happen after the Section 1 gate in
            // OnDamageDealt, but we guard just in case). Then project to %.
            long totalHeroDamage = 0;
            foreach (var kv in _totalsPerOwner)
            {
                if (_heroNameByOwnerId.ContainsKey(kv.Key))
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
                if (!_heroNameByOwnerId.TryGetValue(kv.Key, out string? name))
                    continue;

                string nickname = ResolveNicknameForOwnerLocked(kv.Key, name, boundDbIds);

                rows.Add(new HeroShareEntry
                {
                    Name       = name,
                    Total60s   = kv.Value,
                    Percent    = kv.Value * 100.0 / totalHeroDamage,
                    IsSelf     = kv.Key == _likelySelfOwnerId,
                    PlayerName = nickname,
                    OwnerId    = kv.Key,
                });
            }
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
        // Final flush on shutdown — picks up any record set since the last write that didn't
        // trigger an intra-session save (belt-and-braces; the in-flight saves already cover it).
        SaveMaxHits();
    }
}
