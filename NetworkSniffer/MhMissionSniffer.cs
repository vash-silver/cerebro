using System.Net;
using Gazillion;
using Google.ProtocolBuffers;
using PacketDotNet;
using SharpPcap;
using SharpPcap.LibPcap;

namespace MarvelHeroesComporator.NetworkSniffer;

/// <summary>
/// Mission progression update parsed off the wire (server -> client).
/// </summary>
public sealed class MissionUpdateEvent
{
    public required ulong MissionPrototypeId { get; init; }
    public required uint State { get; init; }                 // 0=Invalid, 1=Inactive, 2=Available, 3=Active, 4=Completed, 5=Failed
    public required DateTime UtcTime { get; init; }
    public bool HasState { get; init; }
    public int ParticipantCount { get; init; }
    public bool SuppressNotification { get; init; }
    public bool? Suspended { get; init; }
}

/// <summary>
/// Per-objective progress update (server -> client). DC and other multi-stage scenarios push these
/// for every sub-objective (e.g. "kill Kaecilius" is one objective, "enter exit portal" is another).
/// The full <c>NetMessageMissionUpdate state=Completed</c> only fires after EVERY objective wraps,
/// so for boss-kill detection we have to listen to the right objective index instead of waiting on
/// the parent mission's terminal state.
/// </summary>
public sealed class MissionObjectiveUpdateEvent
{
    public required ulong MissionPrototypeId { get; init; }
    public required uint  ObjectiveIndex    { get; init; }
    public required DateTime UtcTime         { get; init; }
    public bool HasState     { get; init; }
    public uint State        { get; init; }   // 0=Invalid, 1=Available, 2=Active, 3=Completed, 4=Failed, 5=Skipped
    public uint CurrentCount { get; init; }
    public uint RequiredCount{ get; init; }
}

/// <summary>Entity-kill notification (server -> client). EntityId may be a player, mob, prop, etc.</summary>
public sealed class EntityKillEvent
{
    public required ulong EntityId { get; init; }
    public required ulong KillerEntityId { get; init; }
    public required uint KillFlags { get; init; }
    public required DateTime UtcTime { get; init; }
}

/// <summary>Entity-destroy notification (server -> client). Carries an optional prototypeId.</summary>
public sealed class EntityDestroyEvent
{
    public required ulong EntityId { get; init; }
    public required DateTime UtcTime { get; init; }
    public ulong? PrototypeId { get; init; }
    public ulong? RegionId { get; init; }
}

/// <summary>
/// Server-pushed region transition. Fires every time the player warps to a new region (terminal
/// entry, hub return, story zone change, etc.). <see cref="RegionPrototypeId"/> is the canonical
/// "which terminal / which level" key used by <see cref="MarvelHeroesComporator.Helpers.TerminalMissionMap"/>.
/// </summary>
public sealed class RegionChangedEvent
{
    public required ulong RegionId { get; init; }
    public required DateTime UtcTime { get; init; }
    /// <summary>May be 0 when the server omits it — most regions populate this.</summary>
    public ulong RegionPrototypeId { get; init; }
    public bool ClearingAllInterest { get; init; }

    /// <summary>
    /// Canonical difficulty-tier prototype id from <c>createRegionParams.difficultyTierProtoId</c>.
    /// 0 when the server omits it (e.g. hub teleports). For terminals this is always populated and
    /// is the *only* protocol-level signal that distinguishes Normal / Heroic / Cosmic when the same
    /// <see cref="RegionPrototypeId"/> is reused across tiers (Kingpin, Hood, Bugle, ...).
    /// </summary>
    public ulong DifficultyTierProtoId { get; init; }

    /// <summary>Region level from <c>createRegionParams.level</c>; 0 when omitted.</summary>
    public uint Level { get; init; }
}

/// <summary>Server-pushed difficulty tier change for the current region.</summary>
public sealed class DifficultyChangedEvent
{
    public required ulong DifficultyIndex { get; init; }
    public required DateTime UtcTime { get; init; }
}

/// <summary>Loading screen open/close — useful to tell "actively playing" from "between regions".</summary>
public sealed class LoadingScreenEvent
{
    public required bool Opening { get; init; }   // true = QueueLoadingScreen, false = Dequeue
    public required DateTime UtcTime { get; init; }
    public ulong RegionPrototypeId { get; init; } // 0 when not provided (Dequeue typically omits)
}

/// <summary>
/// Client -> server: <c>NetMessageRegionRequestQueueCommandClient</c>. Sent when the player presses
/// "Queue" on the MAP terminal panel. Carries the canonical <see cref="DifficultyTierProtoId"/> for
/// the upcoming run — the only protocol-level signal that distinguishes Normal from Heroic when both
/// share the same <see cref="RegionPrototypeId"/> (Kingpin / Hood / etc).
/// </summary>
public sealed class RegionQueueRequestedEvent
{
    public required ulong RegionPrototypeId { get; init; }
    public required ulong DifficultyTierProtoId { get; init; }
    public required DateTime UtcTime { get; init; }
    public uint Command { get; init; }
}

/// <summary>
/// Single damage/healing event decoded from a server-pushed <c>NetMessagePowerResult</c>. Covers every
/// hit/tick the client sees in AOI — your own basic attacks, DoTs, pet contributions, enemy damage
/// against you, NPC-on-NPC brawls, etc.  The DPS meter consumes these, filters to the player's own
/// avatar via <see cref="UltimateOwnerEntityId"/>, and aggregates into a sliding window.
/// </summary>
/// <remarks>
/// <para>
/// Raw server schema (from <c>ArchiveMessageBuilder.BuildPowerResultMessage</c> in EmuSource):
/// <c>messageFlags (VarInt uint) | powerProtoRef (PrototypeEnum VarInt) | targetEntityId (VarInt ulong)</c>
/// followed by conditional <c>powerOwnerId</c> / <c>ultimateOwnerId</c> / <c>resultFlags</c> / the
/// three damage channels / <c>healing</c> / optional asset-ref / optional position / optional
/// <c>transferToEntityId</c>.  We lift only the fields useful for a DPS panel — everything else is
/// either skipped or discarded after the read.
/// </para>
/// <para>
/// <see cref="PowerOwnerEntityId"/> is the direct attacker (e.g. your pet), whereas
/// <see cref="UltimateOwnerEntityId"/> is the "who actually owns this damage" (you, the pet's owner).
/// For self-only DPS filter by <see cref="UltimateOwnerEntityId"/>.
/// </para>
/// </remarks>
public sealed class DamageDealtEvent
{
    public required DateTime UtcTime { get; init; }
    public required ulong TargetEntityId { get; init; }
    /// <summary>Direct attacker id — may be the ultimate owner's pet/summon. <c>0</c> when the server
    /// sent the <c>NoPowerOwnerEntityId</c> or <c>IsSelfTarget</c> flag (environmental / self-damage).</summary>
    public required ulong PowerOwnerEntityId { get; init; }
    /// <summary>Canonical "who gets credit" entity id. Equals <see cref="PowerOwnerEntityId"/> when the
    /// direct attacker is not a pet/summon. <c>0</c> only when the server omitted both owners
    /// (<c>NoUltimateOwnerEntityId</c> flag).</summary>
    public required ulong UltimateOwnerEntityId { get; init; }
    public required uint DamagePhysical { get; init; }
    public required uint DamageEnergy { get; init; }
    public required uint DamageMental { get; init; }
    public required uint Healing { get; init; }
    public required ulong ResultFlags { get; init; }     // PowerResultFlags (Critical=1<<3, Dodged=1<<4, …)
    /// <summary>Client-local enum index of the <c>Power/…</c> prototype that produced this hit.
    /// Lifted from the <c>powerProtoRef</c> field of the archive. 0 when unreadable.
    /// Used as a fallback hero-identification signal: all damaging player powers live under
    /// <c>Powers/Player/&lt;HeroName&gt;/</c>, so a single hit is enough to tell which avatar the
    /// player is currently on — even when we missed the avatar's EntityCreate (app started
    /// mid-session, region already loaded).</summary>
    public uint PowerPrototypeEnumIndex { get; init; }
    public uint TotalDamage => DamagePhysical + DamageEnergy + DamageMental;
    public bool IsCritical => (ResultFlags & (1u << 3)) != 0;
    public bool IsDodged  => (ResultFlags & (1u << 4)) != 0;
    public bool IsInstantKill => (ResultFlags & (1u << 12)) != 0;
}

/// <summary>
/// An entity entered the AOI and announced its prototype.  We use this for two things: (1) identify
/// which entity id belongs to an avatar (so we can guess "that's the local player") and (2) map a
/// killed entity back to its prototype (boss vs. trash) when combined with <see cref="EntityKillEvent"/>.
/// Only the first two fields of <c>NetMessageEntityCreate.baseData</c> are decoded — the rest of the
/// archive contains positioning / inventory / locomotion state we don't need for DPS.
/// </summary>
public sealed class EntityCreatedEvent
{
    public required ulong EntityId { get; init; }
    /// <summary>Raw prototype-enum index from the client's DataDirectory.  Without the client table
    /// this can't be mapped back to a full PrototypeId, but the value is stable across a session and
    /// unique per prototype, so it's still useful for equality ("same entity type") comparisons.</summary>
    public required uint PrototypeEnumIndex { get; init; }
    public required DateTime UtcTime { get; init; }
    /// <summary>Database-unique id of the player this entity represents, when the server flagged
    /// <c>HasDbId</c> in the EntityCreate header (true only for <c>Player</c> container entities —
    /// NOT for avatars, items, or mobs). Zero otherwise.  The DpsMeter keys the
    /// <c>playerEntityId → dbId</c> and, via <c>NetMessageModifyCommunityMember</c>,
    /// <c>dbId → playerName</c> lookup chain off this field so duplicated heroes in the top-N
    /// leaderboard can be disambiguated with the actual player's nickname.</summary>
    public ulong DatabaseUniqueId { get; init; }
    /// <summary>Authoritative "this entity IS an avatar" flag — sourced from the
    /// <c>HasAvatarWorldInstanceId</c> bit (1&lt;&lt;9) in the EntityCreate header, which
    /// <c>ArchiveMessageBuilder.BuildEntityCreateMessage</c> sets only for <c>Avatar</c>
    /// entities (see <c>if (avatar != null) fieldFlags |= HasAvatarWorldInstanceId</c>).
    /// Far more reliable than "prototype index is in our <c>HeroPrototypes.Names</c>
    /// table": the generated hero-prototype list can be incomplete (new heroes, costume
    /// variants), but every avatar — known or not — carries this flag.</summary>
    public bool IsAvatar { get; init; }

    /// <summary>Quantity from the entity's <c>Property.InventoryStackCount</c>, or <c>0</c> if
    /// the entity doesn't carry that property (every non-stackable entity, all mobs, all
    /// avatars).  Populated by <c>MhMissionSniffer.TryExtractStackCount</c> which scans the
    /// <c>archiveData</c> property collection.  Used by <c>EternitySplinterTracker</c> to
    /// surface "how many splinters did this drop give us" -- splinters spawn as currency
    /// items with a stack count of 1..30+, and the alert is much more useful when it reports
    /// the actual quantity rather than just "a splinter dropped".</summary>
    public int StackCount { get; init; }

    /// <summary>Raw <c>archiveData</c> bytes from the NetMessageEntityCreate.  Populated for
    /// non-avatar entities ONLY (avatars don't carry useful stack-count-style payloads and
    /// the per-event allocation would be wasteful in dense crowds).  Consumers like
    /// <c>EternitySplinterTracker</c> use this to dump the entity's full property collection
    /// when actively debugging "the splinter dropped but the count came back as zero" --
    /// a property-by-property dump immediately reveals which <c>PropertyEnum</c> the server
    /// build is using for the currency amount, even if it differs from the value our parser
    /// expects.  Null for avatars and for events older than the dump infrastructure.</summary>
    public byte[]? RawArchive { get; init; }

    /// <summary>Player nickname as broadcast on the Avatar's <c>_playerName</c>
    /// RepString, when we were able to extract it from the archive blob. Unlike the
    /// <c>NetMessageModifyCommunityMember</c> path (which only fires for
    /// <c>NewlyCreated</c> community members and misses players already in your
    /// Guild/Friends lists), this field is carried by the Avatar entity itself on the
    /// <c>AOIChannelProximity</c> channel — the same channel that renders the nickname
    /// above a player's head in-game. Populated only for <see cref="IsAvatar"/> events;
    /// empty when the heuristic scanner couldn't find a confident match (falls back to
    /// the ModifyCommunityMember pairing path in <c>DpsMeter</c>).</summary>
    public string PlayerName { get; init; } = string.Empty;

    /// <summary>Database-unique id of the Player that owns this Avatar, extracted from
    /// the Avatar's transient archive (<c>_ownerPlayerDbId</c> — serialized right after
    /// <c>_playerName</c> in <c>Avatar.Serialize</c>). This is the same id the
    /// community-member pairing uses, so populating it lets us skip the temporal
    /// correlation entirely. Zero when unknown.</summary>
    public ulong OwnerPlayerDbId { get; init; }

    /// <summary>True when the entity carries an <c>ItemCurrency</c> property with the
    /// currency-type code we recognize as Eternity Splinter.  Server-agnostic for the
    /// detection step; tied to a hardcoded params signature for the filter (currently
    /// <c>0x12000000000000</c>, empirically derived).  See <see cref="CurrencyParams"/>
    /// for the raw signature so the tracker can log + adapt.</summary>
    public bool IsCurrencyDrop { get; init; }

    /// <summary>When the entity carries an <c>ItemCurrency</c> property, the raw params
    /// bits of that property (encodes the currency type).  Zero when no <c>ItemCurrency</c>
    /// property was seen.  Surfaced for diagnostic logging so the splinter-tracker can
    /// dump "saw currency type 0x... with N units" lines and we can identify each
    /// server's splinter signature by correlating against confirmed in-game drops.</summary>
    public ulong CurrencyParams { get; init; }

    /// <summary>Raw EntityCreate <c>fieldFlags</c> bitfield (the first varint after the
    /// replication header + entityId + protoIdx in baseData).  Surfaced so downstream
    /// consumers can apply additional filters without re-parsing baseData -- e.g. the
    /// loot scanner uses <see cref="RawLocoFieldFlags"/> to skip peer-equipped /
    /// inventoried items.  Known bits in this codebase: HasNonProximityInterest (1&lt;&lt;5),
    /// HasDbId (1&lt;&lt;8), HasAvatarWorldInstanceId (1&lt;&lt;9).  Other bits are not
    /// currently decoded but are preserved here for empirical inspection.</summary>
    public uint RawFieldFlags { get; init; }

    /// <summary>Raw EntityCreate <c>locoFieldFlags</c> bitfield (the second varint after
    /// <see cref="RawFieldFlags"/>).  Zero for entities with no locomotion state -- which
    /// in practice means "this entity isn't in the world" -- so it doubles as a cheap
    /// "is this in someone's inventory / equipped slot vs on the ground" discriminator.
    /// See <see cref="LikelyInInventory"/> for the convenience accessor.</summary>
    public uint RawLocoFieldFlags { get; init; }

    /// <summary>True when this entity almost certainly lives inside a container
    /// (inventory / equipped slot) rather than in the world.  Heuristic: a non-avatar
    /// entity with empty <see cref="RawLocoFieldFlags"/> has no locomotion state, which
    /// means the server didn't emit a world position for it -- inventoried / equipped
    /// items are the canonical case.  Ground-dropped items DO carry a position (they
    /// sit somewhere in the region) and so have non-zero loco flags.
    ///
    /// <para>Used by <c>LootScannerDiagnostic</c> to skip the noise generated when you
    /// walk into the HUB / a crowded zone and the server fires an <c>EntityCreate</c>
    /// for every peer player's equipped gear so your client can render their costume
    /// and stat hovers.  Without this filter the scanner sees thousands of "items"
    /// per zone load and the hunt-match logic evaluates each of them.</para>
    ///
    /// <para><b>Trade-off:</b> if a future build of the server changes the loco-state
    /// encoding such that ground items also have empty loco flags, this filter would
    /// silently drop real drops.  The loot scanner emits a verbose-mode "filtered" line
    /// so over-filtering can be diagnosed by glancing at the diagnostic log after a
    /// session.</para></summary>
    public bool LikelyInInventory => !IsAvatar && RawLocoFieldFlags == 0;
}

/// <summary>
/// Server-pushed "you are this entity" signal — the one unambiguous source of truth for
/// identifying the local <c>Player</c> entity id.  Sent once per game-server connection, right
/// after login finishes loading the character into the world (see
/// <c>Player.EnterGame</c> → <c>NetMessageLocalPlayer</c> in EmuSource).
/// <para>
/// This is the <c>Player</c>'s entity id (the **container**), NOT the active Avatar's id. The
/// avatar is a separate entity parked inside the player's <c>AvatarInPlay</c> inventory slot;
/// use <see cref="InventoryMovedEvent"/> to find out which entity currently holds that slot.
/// </para>
/// </summary>
public sealed class LocalPlayerIdentifiedEvent
{
    /// <summary>Entity id of the local Player container. Never 0 when the event fires.</summary>
    public required ulong LocalPlayerEntityId { get; init; }
    public required DateTime UtcTime { get; init; }
}

/// <summary>
/// Authoritative "this avatar is ME" signal extracted from a <b>client -> server</b> power
/// activation message (<c>NetMessageTryActivatePower</c> / <c>NetMessagePowerRelease</c> /
/// <c>NetMessageTryCancelPower</c>).  Only the local game client sends these, so the
/// <c>idUserEntity</c> field is by construction the entity id of the avatar YOU are playing
/// right now — no heuristics, no waiting on the login handshake.
/// <para>
/// This is the third (and most reliable) identification channel:
/// <list type="bullet">
///   <item><see cref="LocalPlayerIdentifiedEvent"/> — needs catching the login handshake.</item>
///   <item><see cref="InventoryMovedEvent"/> into the Player container — also needs the
///         handshake to know the container id.</item>
///   <item>This one — works mid-session. Any key press that fires a power produces one.</item>
/// </list>
/// </para>
/// </summary>
public sealed class LocalAvatarObservedEvent
{
    /// <summary>Entity id of the avatar that the local client just asked the server to cast
    /// with. Always non-zero when the event fires.</summary>
    public required ulong LocalAvatarEntityId { get; init; }
    public required DateTime UtcTime { get; init; }
}

/// <summary>
/// Fired when the local client sent a <c>NetMessageTryActivatePower</c> AND the message
/// carried a non-zero <c>PowerPrototypeId</c> field.  Distinct from
/// <see cref="LocalAvatarObservedEvent"/> -- the avatar-observed signal fires on every
/// client power message (try-activate, release, cancel) to confirm "this avatar is me",
/// whereas this event ALSO carries the power that was fired so downstream tools
/// (the CooldownTracker) can know exactly which ability went on cooldown.
///
/// <para>The wire field is <c>ulong</c> (full prototype ref).  Downstream lookups
/// (PowerIconByProto, PowerNamesByProto) use the lower 32 bits as the root-prototype
/// enum index; the consumer is responsible for that conversion.</para>
/// </summary>
public sealed class LocalPowerActivatedEvent
{
    /// <summary>Entity id of the avatar firing the power -- by construction, this is the
    /// local player's avatar (only the local client sends TryActivatePower).</summary>
    public required ulong LocalAvatarEntityId { get; init; }
    /// <summary>Full 64-bit prototype reference for the power that was activated.  Cast
    /// to <c>uint</c> to look up display name / icon via <c>PowerNamesByProto</c> /
    /// <c>PowerIconByProto</c>.  Always non-zero when the event fires.</summary>
    public required ulong PowerPrototypeId { get; init; }
    public required DateTime UtcTime { get; init; }
}

/// <summary>
/// Raw <c>NetMessageSetProperty</c> / <c>NetMessageRemoveProperty</c> parse output.
/// Surfaces every property delta the server pushes to the local client, regardless
/// of which entity it targets -- consumers filter by <see cref="ReplicationId"/>
/// against their own entity↔replicationId mapping.
///
/// <para><b>Wire format</b>: the on-the-wire <c>PropertyId</c> ulong is endianness-
/// swapped from the raw uint64 field; after swap the top 11 bits encode the
/// <see cref="PropertyEnum"/> index and the lower 53 bits hold <see cref="ParamBits"/>
/// (typically a prototype reference or a tuple-packed key, depending on the property's
/// param-spec).  This event provides BOTH the decoded fields and the raw value bits
/// so downstream code can reinterpret as int64 / float / uint32 as the property
/// schema dictates.</para>
///
/// <para><b>Used by</b>: <c>CooldownTracker</c> filters to PropertyEnum 732
/// (<c>PowerCooldownDuration</c>) and 734 (<c>PowerCooldownStartTime</c>) keyed by
/// power proto in <see cref="ParamBits"/>.  Future consumers (stat tracking,
/// resource pool tracking, etc.) subscribe to the same stream.</para>
/// </summary>
public sealed class PropertyChangedEvent
{
    /// <summary>Property-collection replication id (NOT the entity id).  Each entity
    /// has one primary property collection whose id is established at
    /// <c>EntityCreate</c> time.  Consumers maintain a <c>replicationId →
    /// entityId</c> map (or auto-discover via a heuristic) to figure out which
    /// entity this delta targets.</summary>
    public required ulong ReplicationId { get; init; }
    /// <summary>The decoded property enum (top 11 bits of the wire propertyId, after
    /// endianness swap).  Cross-reference with <c>PropertyEnumNames.cs</c>.</summary>
    public required uint PropertyEnum { get; init; }
    /// <summary>Lower 53 bits of the (endianness-swapped) wire propertyId.  Typically
    /// a prototype enum index (uint in the low bits) for per-prototype properties
    /// like power cooldowns; tuple-packed otherwise.</summary>
    public required ulong ParamBits { get; init; }
    /// <summary>Raw value bits as sent on the wire.  Decode according to the
    /// property's type-spec: zigzag-rotated int64 via
    /// <c>(long)((ValueBits >> 1) | (ValueBits &lt;&lt; 63))</c>, or float32 via
    /// <c>BitConverter.UInt32BitsToSingle((uint)ValueBits)</c>.</summary>
    public required ulong ValueBits { get; init; }
    /// <summary>True for <c>NetMessageRemoveProperty</c> (property cleared off the
    /// collection); false for <c>NetMessageSetProperty</c>.  Remove events carry the
    /// same ReplicationId / PropertyEnum / ParamBits keys; <see cref="ValueBits"/>
    /// is meaningless (zero) on removes.</summary>
    public required bool Removed { get; init; }
    public required DateTime UtcTime { get; init; }
}

/// <summary>
/// Payload for <c>NetMessageModifyCommunityMember</c>: the server pushes one of these to the
/// local client every time a community member (friend, party member, *nearby player*, ...) is
/// created, updated, or removed.  Nearby-circle broadcasts arrive automatically when someone
/// enters the local client's AOI — that's what lets us map a <see cref="PlayerDbId"/> to a
/// display name without ever parsing the <c>Player</c> entity's full archive.
/// Paired with the <c>playerEntityId → dbId</c> map we build from
/// <see cref="EntityCreatedEvent.DatabaseUniqueId"/> and the <c>avatarEntityId → playerEntityId</c>
/// map we build from <see cref="InventoryMovedEvent"/>, this gives us a full
/// <c>avatarEntityId → playerName</c> resolver, which the DpsMeter uses to disambiguate
/// duplicate heroes on the top-N leaderboard.
/// </summary>
public sealed class CommunityMemberUpdatedEvent
{
    /// <summary>Database-unique id of the community member. This is the same ulong that the
    /// <c>EntityCreate</c> header carries under <c>HasDbId</c> for Player entities.</summary>
    public required ulong PlayerDbId { get; init; }
    /// <summary>Player's display nickname (e.g. "SomeGuy42"). May be empty if the server
    /// chose not to include it on this particular update (the client-side community cache
    /// holds onto the previously-broadcast name in that case — so we do the same).</summary>
    public required string PlayerName { get; init; }
    /// <summary><c>true</c> when the server set the top-level <c>playerName</c> field on
    /// <c>NetMessageModifyCommunityMember</c>. The server only does that on the "newly
    /// created" path (see <c>CommunityMember.SendUpdateToOwner</c>, guarded by
    /// <c>CommunityMemberUpdateOptionBits.NewlyCreated</c>), which in practice fires exactly
    /// once per nearby-AOI add — right after the corresponding avatar <c>EntityCreate</c>.
    /// Consumers use this as the authoritative signal to pair the preceding avatar
    /// EntityCreate with this dbId without the ambiguity of later status-only updates.</summary>
    public required bool IsInitial { get; init; }
    /// <summary>Raw bitmask of <c>CircleId</c> memberships the server just advertised for
    /// this broadcast. Bit positions mirror the <c>CircleId</c> enum in
    /// <c>CommunityCircle.cs</c>: <c>__Nearby = 1 &lt;&lt; 3</c> (<c>0x08</c>),
    /// <c>__Guild = 1 &lt;&lt; 5</c> (<c>0x20</c>), etc. The <c>HasCircles</c> flag tells you
    /// whether the server actually included the field on this message — a purely-slot
    /// follow-up update carries <c>HasCircles == false</c> so consumers can ignore it.</summary>
    public required bool HasCircles { get; init; }
    public required ulong Circles { get; init; }
    /// <summary>
    /// <c>PrototypeDataRef</c> of the member's currently-selected avatar, pulled from
    /// <c>broadcast.slots[0].avatarRefId</c>. Non-zero only when the server included at least
    /// one slot on this update (broadcasts for "newly created" and "in-nearby-circle" paths
    /// always do; pure circle-membership updates omit it). 64-bit hash — translate to a hero
    /// display name via <c>HeroPrototypes.NamesByDataRef</c>.
    ///
    /// This is the key signal for the DpsMeter's mid-session nickname fallback: when we didn't
    /// see the avatar's <c>NetMessageEntityCreate</c> (app launched after region load), we can
    /// still pair <c>dbId → playerName → currentHero</c> with a damaging
    /// <c>avatarEntityId → heroName</c> by matching on the hero name. Ambiguous only when two
    /// nearby players are on the same hero.
    /// </summary>
    public required ulong CurrentAvatarRefId { get; init; }
    public required DateTime UtcTime { get; init; }
}

/// <summary>
/// An entity's inventory location changed (server -> client <c>NetMessageInventoryMove</c>).
/// Most moves are boring (loot picked up, gear swapped, etc.) but the avatar-swap and
/// avatar-enter-world paths both route through this message — when a <c>Player</c> equips an
/// avatar into its <c>AvatarInPlay</c> slot, that move arrives here with
/// <see cref="ContainerEntityId"/> = the local player id. The DpsMeter uses that correlation
/// to pin "who is YOU" without resorting to the "top damager" heuristic.
/// </summary>
public sealed class InventoryMovedEvent
{
    /// <summary>Entity that moved.</summary>
    public required ulong EntityId { get; init; }
    /// <summary>The entity id of the new container (usually the owning Player or a workbench).</summary>
    public required ulong ContainerEntityId { get; init; }
    /// <summary>Prototype id of the destination inventory (identifies whether this is AvatarInPlay,
    /// AvatarLibrary, ItemBag, ...). Raw <c>PrototypeId</c>, not enum index.</summary>
    public required ulong InventoryPrototypeId { get; init; }
    /// <summary>Slot within the destination inventory. 0-based.</summary>
    public required uint Slot { get; init; }
    public required DateTime UtcTime { get; init; }
}

/// <summary>
/// A loot item just spawned on the ground (server -> client <c>NetMessageLootEntity</c>).
/// Fires for every dropped item the local client observes — credits piles, gear, currency
/// stacks (including Eternity Splinter / Cube Shard / etc.), Cosmic prestige tokens, raid
/// loot, the lot.  The <see cref="ItemProtoRef"/> is the canonical
/// <c>NetStructItemSpec.itemProtoRef</c> — a 64-bit <c>PrototypeId</c> (DataRef), NOT the
/// smaller enum index used elsewhere by this sniffer.  Cross-references with the
/// PrototypeId constants embedded in the server (see
/// <c>MHServerEmu.Games.dll → LootCooldownTable.EternitySplinterPrototypeRef</c> and
/// <c>LootInstance.CombineCurrencyStacks(...)</c>) so a downstream listener can match on
/// the exact item type that landed.
/// </summary>
public sealed class LootDroppedEvent
{
    /// <summary>Entity id of the on-ground loot instance.  Pair with a future
    /// <c>NetMessageEntityDestroy</c> to detect when the item is picked up / despawns —
    /// but consumers tracking drop counts (e.g. the Eternity Splinter tracker) only
    /// need the spawn event.</summary>
    public required ulong ItemId { get; init; }
    /// <summary>Full 64-bit <c>PrototypeId</c> of the dropped item — same encoding the
    /// server uses internally and the same value carried by <c>ItemSpec.itemProtoRef</c>
    /// on the wire.  Stable per game build; safe to compare against hardcoded constants.</summary>
    public required ulong ItemProtoRef { get; init; }
    /// <summary>Item level rolled by the server.  Useful for filtering ("only level-70 drops")
    /// but the splinter tracker doesn't care about this.</summary>
    public uint ItemLevel { get; init; }
    /// <summary>Rarity prototype reference rolled by the server.  Optional / often zero
    /// for currency-style drops.</summary>
    public ulong RarityProtoRef { get; init; }
    public required DateTime UtcTime { get; init; }
}

/// <summary>
/// A buff / debuff (a "Condition" in MHServerEmu) was just applied to an entity.  Decoded
/// from a server-pushed <c>NetMessageAddCondition</c>.  The same envelope carries both buffs
/// (e.g. Cyclops's <c>Overwatch</c> applying <c>Empowered</c>) and debuffs (bleed, stun, etc.) --
/// the distinction is in the <c>ConditionPrototypeRef</c>'s prototype data, which we don't
/// have access to here.  Consumers can filter by <c>OwnerEntityId == LikelySelfOwnerId</c>
/// to limit to "buffs on me", or use <c>CreatorEntityId</c> to track "buffs I applied".
///
/// <para><b>Discovery workflow:</b> the 64-bit prototype refs are server-stable but opaque
/// without the client's prototype-name table.  Capture them in the log, cross-reference
/// against MHServerEmu's <c>ConditionPrototype</c> definitions in <c>Calligraphy.sip</c>,
/// then build a static "known buffs" table the same way <c>BossNames</c> was generated.</para>
/// </summary>
public sealed class ConditionAddedEvent
{
    /// <summary>Entity wearing the buff -- typically a player avatar or NPC.  Match against
    /// <c>DpsMeter.LikelySelfOwnerId</c> to filter to "buffs on me".</summary>
    public required ulong OwnerEntityId { get; init; }

    /// <summary>Server-allocated condition slot id, unique per owner.  Pair this with the
    /// owner id to identify a specific buff instance for later removal -- the
    /// <see cref="ConditionRemovedEvent"/> echoes both ids.</summary>
    public required ulong ConditionId { get; init; }

    /// <summary>Entity that directly applied the buff -- could be a player (an aura you
    /// extended), a pet, an NPC.  Equals <see cref="OwnerEntityId"/> for self-buffs.</summary>
    public ulong CreatorEntityId { get; init; }

    /// <summary>Root cause entity for the buff chain -- if a player's pet's aura buffed a
    /// teammate, the pet is <see cref="CreatorEntityId"/> and the player is here.  Often
    /// equals <see cref="CreatorEntityId"/>.</summary>
    public ulong UltimateCreatorEntityId { get; init; }

    /// <summary>Full 64-bit <c>PrototypeId</c> of the buff -- e.g. <c>Empowered</c>.  Zero
    /// if the wire set <c>NoConditionPrototypeRef</c> (rare; usually only synthetic /
    /// engine-internal conditions omit this).</summary>
    public ulong ConditionPrototypeRef { get; init; }

    /// <summary>Full 64-bit <c>PrototypeId</c> of the power that applied this condition --
    /// e.g. <c>Overwatch</c>.  Zero if not set.  Useful for "this buff came from Overwatch"
    /// labeling without needing the buff's own name.</summary>
    public ulong CreatorPowerPrototypeRef { get; init; }

    /// <summary>Duration in milliseconds.  Zero means "permanent" (most often: an aura
    /// while the source ability is held).  Add to <see cref="UtcTime"/> for an estimated
    /// expiry; the real expiry comes via <see cref="ConditionRemovedEvent"/>.</summary>
    public long DurationMs { get; init; }

    /// <summary>Update interval in milliseconds -- for ticking buffs (DoTs, HoTs).  Zero
    /// means "no ticking; the buff just sits applied for its duration".</summary>
    public int UpdateIntervalMs { get; init; }

    /// <summary>Full archive bytes from the AddCondition payload.  The property collection
    /// is embedded mid-archive (after owner id / flags / proto refs / timing fields), so
    /// callers wanting to dump it should use <see cref="MhMissionSniffer.DumpPropertyCollectionAt"/>
    /// with <see cref="PropertyCollectionOffset"/> as the start.</summary>
    public byte[]? RawProperties { get; init; }

    /// <summary>Byte offset into <see cref="RawProperties"/> where the
    /// <c>PropertyCollection</c> begins (just after the condition's timing fields).
    /// Pass this to <see cref="MhMissionSniffer.DumpPropertyCollectionAt"/> to walk the
    /// stat effects without re-parsing the preamble.</summary>
    public int PropertyCollectionOffset { get; init; }

    /// <summary>Raw bit-mask of <c>ConditionSerializationFlags</c> -- useful for debugging
    /// "why didn't this field decode" issues against the live server.</summary>
    public uint SerializationFlags { get; init; }

    /// <summary>Parsed property collection -- the list of stat deltas this condition applies
    /// (e.g. <c>DamagePctBonus +0.40</c>).  Always present; empty if the property collection
    /// failed to parse or the buff legitimately has no property effects.  Same data as
    /// <see cref="MhMissionSniffer.DumpPropertyCollectionAt"/> emits to the diagnostic log,
    /// but in structured form for downstream aggregation (the live-stats overlay sums these
    /// across active buffs to show "+%damage from buffs").</summary>
    public IReadOnlyList<BuffPropertyDelta> PropertyDeltas { get; init; } = System.Array.Empty<BuffPropertyDelta>();

    public required DateTime UtcTime { get; init; }
}

/// <summary>
/// One property entry from a condition's embedded <c>PropertyCollection</c>.  Pairs the
/// <see cref="PropertyEnum"/> id (e.g. 283 = <c>DamagePctBonus</c>) with the value, decoded
/// both as the raw signed integer (for int-typed properties) and as IEEE-754 float (for the
/// float-typed properties that most damage / crit bonuses are).  The caller picks whichever
/// representation is appropriate based on the enum's declared value type.
/// </summary>
/// <remarks>
/// We carry both <c>IntValue</c> and <c>FloatValue</c> because the wire format doesn't tell us
/// the property's underlying type -- it's encoded in MHServerEmu's PropertyEnum metadata,
/// which we don't (yet) replicate client-side.  The downstream aggregator knows e.g.
/// "DamagePctBonus is a float" and reads <c>FloatValue</c>; for unknown enums we surface both
/// so the diagnostic log can show whichever makes sense.
/// </remarks>
public readonly struct BuffPropertyDelta
{
    /// <summary>PropertyEnum id (top 11 bits of the wire-format <c>PropertyId</c>).  Matches
    /// MHServerEmu's <c>PropertyEnum</c> enum values -- e.g. 283 = DamagePctBonus.</summary>
    public uint PropertyEnum { get; init; }

    /// <summary>Param bits (the lower 53 bits of the <c>PropertyId</c>).  Most simple
    /// properties have 0 here; parameterized properties (e.g. per-keyword bonuses) encode
    /// their parameter prototype refs in these bits.</summary>
    public ulong ParamBits { get; init; }

    /// <summary>Signed-integer interpretation of the value bits (for int-typed properties:
    /// stack counts, level scaling, etc).</summary>
    public long IntValue { get; init; }

    /// <summary>IEEE-754 float interpretation of the value bits (for float-typed properties:
    /// percentage bonuses, damage scalars, etc).  Most stat-bonus properties land here.</summary>
    public float FloatValue { get; init; }

    /// <summary>Raw value bits from the wire, before either decode was applied.  Useful for
    /// diagnostics when a new property type doesn't match either integer or float
    /// interpretations (e.g. some properties store packed prototype refs in the value).</summary>
    public ulong RawValueBits { get; init; }
}

/// <summary>
/// A buff was just removed (expired, dispelled, replaced).  Pair the
/// <c>(OwnerEntityId, ConditionId)</c> with the previous <see cref="ConditionAddedEvent"/>
/// to identify which buff ended.
/// </summary>
public sealed class ConditionRemovedEvent
{
    public required ulong OwnerEntityId { get; init; }
    public required ulong ConditionId { get; init; }
    public required DateTime UtcTime { get; init; }
}

/// <summary>
/// Passive sniffer for the Marvel Heroes / MHServerEmu Mux protocol on the game frontend port (default 4306).
///
/// Reads raw TCP frames via Npcap, reassembles each TCP flow, parses Mux frames + protobuf
/// MessageBuffer envelopes, and raises strongly-typed events for mission/entity updates.
///
/// Designed for use as a background helper inside the WPF app:
///   - Construct, optionally tweak Port / Diagnostic, call <see cref="TryStart"/>.
///   - If TryStart returns false the host can fall back to a memory-only detector. The reason is
///     in <see cref="StartFailureReason"/> (typically: Npcap not installed, or no admin).
///   - Subscribe to <see cref="MissionUpdated"/> for the boss-kill / terminal-completion signal.
///
/// Works identically with a local MHServerEmu and with online servers (Bifrost / etc) — no proxy,
/// no hosts edits, no client/server changes.
/// </summary>
public sealed class MhMissionSniffer : IDisposable
{
    private static readonly Lookup s_serverToClient = Lookup.Build(typeof(GameServerToClientMessage));
    private static readonly Lookup s_clientToServer = Lookup.Build(typeof(ClientToGameServerMessage));

    /// <summary>TCP port the game uses for the frontend / mux channel (default 4306).</summary>
    public int Port { get; set; } = 4306;

    /// <summary>
    /// Optional extra TCP ports merged into the capture BPF as <c>tcp port P or tcp port Q</c>.
    /// Use when the client opens a second socket (e.g. separate game-instance port). Loaded from
    /// <see cref="DpsOverlaySettingsFile.AdditionalTcpPorts"/>.
    /// </summary>
    public int[]? AdditionalCapturePorts { get; set; }

    /// <summary>If set, only adapters whose name or description contains this substring are opened.</summary>
    public string? AdapterFilter { get; set; }

    /// <summary>Optional sink for human-readable diagnostic / debug messages.</summary>
    public Action<string>? Diagnostic { get; set; }

    /// <summary>Reason <see cref="TryStart"/> returned false (Npcap missing, no devices opened, ...).</summary>
    public string? StartFailureReason { get; private set; }

    /// <summary>True after a successful <see cref="TryStart"/> until <see cref="Stop"/>/<see cref="Dispose"/>.</summary>
    public bool IsRunning { get; private set; }

    /// <summary>Number of capture devices currently open and forwarding packets.</summary>
    public int OpenedDeviceCount { get; private set; }

    // Live counters — incremented from the capture thread, read freely from anywhere (long is atomic on x64,
    // and these are diagnostics-only so a torn read on x86 isn't a correctness issue).
    /// <summary>TCP packets the BPF filter let through (primary <see cref="Port"/> plus any <see cref="AdditionalCapturePorts"/>).</summary>
    public long PacketsReceived;
    /// <summary>Mux frames successfully reassembled out of the TCP streams.</summary>
    public long MuxFramesParsed;
    /// <summary>Protobuf MessageBuffer envelopes dispatched to a per-message handler.</summary>
    public long MessagesDispatched;

    /// <summary>
    /// Live counters keyed by client-to-server protobuf message name. Useful for diagnosing why a
    /// specific C->S message (e.g. <c>NetMessageRegionRequestQueueCommandClient</c>) never fires —
    /// dump this from a heartbeat to see exactly which C->S types the wire is carrying.
    /// Capture-thread writes are serialized via the dictionary's internal lock.
    /// </summary>
    public readonly System.Collections.Concurrent.ConcurrentDictionary<string, long> ClientToServerCounts = new();

    /// <summary>
    /// Live counters keyed by server-to-client protobuf message name. Same diagnostic purpose as
    /// <see cref="ClientToServerCounts"/> — lets us tell whether the DPS pipeline is dark because
    /// <c>NetMessagePowerResult</c> never arrives (⇒ nothing to parse) vs. arrives and silently
    /// fails to parse (⇒ look for <c>PowerResult parse failed</c> in the sniffer log).
    /// </summary>
    public readonly System.Collections.Concurrent.ConcurrentDictionary<string, long> ServerToClientCounts = new();

    public event EventHandler<MissionUpdateEvent>? MissionUpdated;
    public event EventHandler<MissionObjectiveUpdateEvent>? MissionObjectiveUpdated;
    public event EventHandler<EntityKillEvent>? EntityKilled;
    public event EventHandler<EntityDestroyEvent>? EntityDestroyed;
    public event EventHandler<RegionChangedEvent>? RegionChanged;
    public event EventHandler<DifficultyChangedEvent>? DifficultyChanged;
    public event EventHandler<LoadingScreenEvent>? LoadingScreenChanged;
    public event EventHandler<RegionQueueRequestedEvent>? RegionQueueRequested;
    /// <summary>Fires for every <c>NetMessagePowerResult</c> seen on the wire — one event per hit/tick.
    /// Used by <c>DpsMeter</c> to compute a sliding-window damage rate for the local avatar.</summary>
    public event EventHandler<DamageDealtEvent>? DamageDealt;
    /// <summary>Fires for every <c>NetMessageEntityCreate</c> — surfaces entity id + prototype-enum
    /// index so downstream code can keep a local "entity id → prototype" map (needed for avatar-vs-mob
    /// classification in DPS and for boss-vs-trash classification in kill detection).</summary>
    public event EventHandler<EntityCreatedEvent>? EntityCreated;
    /// <summary>Fires once when the server announces which <c>Player</c> entity id is the local
    /// client (via <c>NetMessageLocalPlayer</c>).  Consumers use this together with
    /// <see cref="InventoryMoved"/> to pin the actual "this is YOU" avatar id without guessing.</summary>
    public event EventHandler<LocalPlayerIdentifiedEvent>? LocalPlayerIdentified;
    /// <summary>Fires for every <c>NetMessageInventoryMove</c>. The DPS meter listens to find out
    /// when an avatar entity is slotted into the local Player's <c>AvatarInPlay</c> container,
    /// which is the authoritative "this avatar id is the one YOU control right now" signal.</summary>
    public event EventHandler<InventoryMovedEvent>? InventoryMoved;
    /// <summary>Fires every time the local client sends a power-activation message (try-activate,
    /// release, try-cancel). The <c>idUserEntity</c> field of those messages is definitively the
    /// avatar YOU are playing — the sniffer surfaces it as the most reliable "this is ME" signal,
    /// because unlike the login-time <see cref="LocalPlayerIdentified"/> signal this one works
    /// even when the app was started mid-session.</summary>
    public event EventHandler<LocalAvatarObservedEvent>? LocalAvatarObserved;
    /// <summary>Fires when the local client sends a <c>NetMessageTryActivatePower</c> that
    /// includes a power-prototype id.  The CooldownTracker subscribes to start its per-power
    /// timer at the exact moment the player commits to an ability.  Carries (avatarEntityId,
    /// powerProtoId, timestamp) -- see <see cref="LocalPowerActivatedEvent"/>.</summary>
    public event EventHandler<LocalPowerActivatedEvent>? LocalPowerActivated;
    /// <summary>Fires for every <c>NetMessageSetProperty</c> / <c>NetMessageRemoveProperty</c>
    /// the server pushes to the local client.  Carries the raw (replicationId,
    /// propertyEnum, paramBits, valueBits) tuple so downstream code can filter and
    /// decode as needed.  Used by the CooldownTracker to observe per-power cooldown
    /// state on the local avatar (PropertyEnum 732 / 734).  Fires on the sniffer
    /// thread; UI subscribers must marshal.</summary>
    public event EventHandler<PropertyChangedEvent>? PropertyChanged;
    public event EventHandler<CommunityMemberUpdatedEvent>? CommunityMemberUpdated;
    /// <summary>Fires for every <c>NetMessageLootEntity</c> -- a loot item just spawned on the
    /// ground.  The <see cref="LootDroppedEvent.ItemProtoRef"/> is the full 64-bit PrototypeId
    /// of the dropped item; downstream listeners (Eternity Splinter tracker, future loot
    /// trackers) match it against hardcoded constants.</summary>
    public event EventHandler<LootDroppedEvent>? LootDropped;

    /// <summary>Fires for every <c>NetMessageAddCondition</c> -- a buff / debuff was just
    /// applied to an entity.  See <see cref="ConditionAddedEvent"/> for the payload shape and
    /// the discovery workflow for the opaque <c>PrototypeRef</c> fields.</summary>
    public event EventHandler<ConditionAddedEvent>? ConditionAdded;

    /// <summary>Fires for every <c>NetMessageDeleteCondition</c> -- a buff / debuff was just
    /// removed (expired, dispelled, replaced).  Pair the (<see cref="ConditionRemovedEvent.OwnerEntityId"/>,
    /// <see cref="ConditionRemovedEvent.ConditionId"/>) with the previous
    /// <see cref="ConditionAddedEvent"/> to identify which buff ended.</summary>
    public event EventHandler<ConditionRemovedEvent>? ConditionRemoved;

    private TcpReassembler? _reassembler;
    private readonly List<ICaptureDevice> _openDevices = new();
    private System.Threading.Timer? _evictionTimer;
    private bool _disposed;

    /// <summary>
    /// Try to start packet capture. Returns true on success. On failure, <see cref="StartFailureReason"/>
    /// holds a one-line explanation suitable to surface to the user.
    /// </summary>
    public bool TryStart()
    {
        if (IsRunning) return true;
        StartFailureReason = null;

        try { _ = LibPcapLiveDeviceList.Instance; }
        catch (Exception ex)
        {
            StartFailureReason = $"Npcap not installed or not accessible: {ex.Message}";
            return false;
        }

        var devices = CaptureDeviceList.Instance;
        if (devices.Count == 0)
        {
            StartFailureReason = "No capture devices found (Npcap not installed?).";
            return false;
        }

        _reassembler = new TcpReassembler(OnMuxFrame);

        string bpf = DpsOverlaySettingsFile.BuildTcpPortBpf(Port, AdditionalCapturePorts);
        int opened = 0;
        foreach (var dev in devices)
        {
            if (AdapterFilter is not null
                && !dev.Name.Contains(AdapterFilter, StringComparison.OrdinalIgnoreCase)
                && !(dev.Description ?? string.Empty).Contains(AdapterFilter, StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                dev.OnPacketArrival += OnPacket;
                dev.Open(mode: DeviceModes.None, read_timeout: 1000);
                try { dev.Filter = bpf; }
                catch (Exception ex)
                {
                    Diagnostic?.Invoke($"[skip] {dev.Description}: filter rejected ({ex.Message})");
                    dev.Close();
                    continue;
                }
                dev.StartCapture();
                _openDevices.Add(dev);
                opened++;
                Diagnostic?.Invoke($"[open] {dev.Description}");
            }
            catch (Exception ex)
            {
                Diagnostic?.Invoke($"[skip] {dev.Description}: {ex.Message}");
            }
        }

        if (opened == 0)
        {
            StartFailureReason = "No capture devices were successfully opened. Run as Administrator (Npcap requires raw socket access).";
            return false;
        }

        // Drop dead/idle TCP flows so the reassembly map doesn't grow forever.
        _evictionTimer = new System.Threading.Timer(
            _ => _reassembler?.EvictIdleOlderThan(TimeSpan.FromMinutes(5)),
            null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

        IsRunning = true;
        OpenedDeviceCount = opened;
        Diagnostic?.Invoke($"MhMissionSniffer listening on {opened} device(s), BPF: {bpf}");
        return true;
    }

    public void Stop()
    {
        if (!IsRunning) return;
        IsRunning = false;

        _evictionTimer?.Dispose();
        _evictionTimer = null;

        foreach (var dev in _openDevices)
        {
            try { dev.OnPacketArrival -= OnPacket; } catch { }
            try { dev.StopCapture(); } catch { }
            try { dev.Close(); } catch { }
        }
        _openDevices.Clear();
        _reassembler = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }

    // -------------- packet path --------------

    private void OnPacket(object sender, PacketCapture e)
    {
        try
        {
            var raw = e.GetPacket();
            var packet = Packet.ParsePacket(raw.LinkLayerType, raw.Data);

            var ip = packet.Extract<IPPacket>();
            if (ip is null) return;
            var tcp = ip.Extract<TcpPacket>();
            if (tcp is null) return;

            if (tcp.SourcePort != Port && tcp.DestinationPort != Port) return;

            System.Threading.Interlocked.Increment(ref PacketsReceived);

            // Read payload safely. PacketDotNet's PayloadData throws ArgumentException when the
            // declared TCP segment length exceeds the bytes Npcap captured. This happens on Windows
            // loopback because the TCP/IP stack hands the loopback adapter pre-aggregated "virtual"
            // segments (TSO/LSO/GSO) of up to 64 KB — only the first fragment is in the buffer.
            // We take whatever bytes ARE present and still advance the reassembler past the
            // *declared* range so flows don't get stuck waiting for bytes that will never arrive.
            var seg = tcp.PayloadDataSegment;
            int declaredLen = seg?.Length ?? 0;
            int availableLen = 0;
            if (seg != null && seg.Bytes != null)
                availableLen = Math.Max(0, Math.Min(seg.Length, seg.Bytes.Length - seg.Offset));

            bool truncated = declaredLen > availableLen;
            byte[] payload;
            if (availableLen <= 0) payload = Array.Empty<byte>();
            else
            {
                payload = new byte[availableLen];
                Buffer.BlockCopy(seg!.Bytes, seg.Offset, payload, 0, availableLen);
            }

            var key = new FlowKey(ip.SourceAddress, (ushort)tcp.SourcePort,
                                  ip.DestinationAddress, (ushort)tcp.DestinationPort);

            string tag = tcp.SourcePort == Port ? "S->C" : "C->S";
            var flow = _reassembler!.GetOrCreate(key, tag);

            if (tcp.Synchronize) flow.Initialize(tcp.SequenceNumber + 1);

            if (truncated)
                flow.SkipAndResync(tcp.SequenceNumber, (uint)declaredLen);
            else if (payload.Length > 0)
                flow.Feed(tcp.SequenceNumber, payload);

            if (tcp.Finished || tcp.Reset)
                _reassembler.Close(key, tcp.Reset ? "RST" : "FIN");
        }
        catch (Exception ex)
        {
            Diagnostic?.Invoke($"packet error: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void OnMuxFrame(FlowState flow, MuxFrame frame)
    {
        bool isServerToClient = flow.Tag == "S->C";

        if (frame.Command != MuxCommand.Data && frame.Command != MuxCommand.ConnectWithData)
            return;
        if (frame.Payload.Length == 0) return;

        System.Threading.Interlocked.Increment(ref MuxFramesParsed);

        try
        {
            using var ms = new MemoryStream(frame.Payload);
            while (ms.Position < ms.Length)
            {
                long startPos = ms.Position;
                uint messageId;
                int payloadLen;
                try
                {
                    messageId = CodedInputStream.ReadRawVarint32(ms);
                    payloadLen = (int)CodedInputStream.ReadRawVarint32(ms);
                }
                catch
                {
                    Diagnostic?.Invoke($"corrupt MessageBuffer header at offset {startPos}/{ms.Length}");
                    break;
                }

                if (payloadLen < 0 || ms.Position + payloadLen > ms.Length)
                    break;

                byte[] body = new byte[payloadLen];
                ms.Read(body, 0, payloadLen);

                System.Threading.Interlocked.Increment(ref MessagesDispatched);
                HandleMessage(isServerToClient, messageId, body);
            }
        }
        catch (Exception ex)
        {
            Diagnostic?.Invoke($"frame parse error: {ex.Message}");
        }
    }

    private void HandleMessage(bool isServerToClient, uint messageId, byte[] body)
    {
        // MHServerEmu uses muxId == 1 for both login/frontend and game traffic. We dispatch by
        // *direction* and use the game-side enums; the first ~14 ids overlap with frontend during
        // login (LoginDataPB, etc) but we don't care about those for our use-case.
        Lookup lookup = isServerToClient ? s_serverToClient : s_clientToServer;
        string? name = lookup.NameOf(messageId);
        if (name is null) return;

        if (isServerToClient)
        {
            // Track every S->C name we see so the host's heartbeat can surface the distribution.
            // Pre-increment before dispatch so even a mid-parse crash still counts the arrival.
            ServerToClientCounts.AddOrUpdate(name, 1, static (_, c) => c + 1);
            switch (name)
            {
                case "NetMessageMissionUpdate":          ParseMissionUpdate(body); break;
                case "NetMessageMissionObjectiveUpdate": ParseMissionObjectiveUpdate(body); break;
                case "NetMessageEntityKill":             ParseEntityKill(body); break;
                case "NetMessageEntityDestroy":          ParseEntityDestroy(body); break;
                case "NetMessageEntityCreate":           ParseEntityCreate(body); break;
                case "NetMessagePowerResult":            ParsePowerResult(body); break;
                case "NetMessageLocalPlayer":            ParseLocalPlayer(body); break;
                case "NetMessageInventoryMove":          ParseInventoryMove(body); break;
                case "NetMessageLootEntity":             ParseLootEntity(body); break;
                case "NetMessageAddCondition":           ParseAddCondition(body); break;
                case "NetMessageDeleteCondition":        ParseDeleteCondition(body); break;
                case "NetMessageSetProperty":            ParseSetProperty(body); break;
                case "NetMessageRemoveProperty":         ParseRemoveProperty(body); break;
                case "NetMessageActivatePower":          ParseActivatePowerForDiag(body); break;
                case "NetMessagePreActivatePower":       ParsePreActivatePowerForDiag(body); break;
                case "NetMessageModifyCommunityMember":  ParseModifyCommunityMember(body); break;
                case "NetMessageRegionChange":           ParseRegionChange(body); break;
                case "NetMessageRegionDifficultyChange": ParseDifficultyChange(body); break;
                case "NetMessageQueueLoadingScreen":     ParseLoadingScreen(body, opening: true); break;
                case "NetMessageDequeueLoadingScreen":   ParseLoadingScreen(body, opening: false); break;
            }
        }
        else
        {
            ClientToServerCounts.AddOrUpdate(name, 1, static (_, c) => c + 1);
            switch (name)
            {
                case "NetMessageRegionRequestQueueCommandClient": ParseRegionQueueRequest(body); break;
                case "NetMessageTryActivatePower":                ParseTryActivatePower(body); break;
                case "NetMessagePowerRelease":                    ParsePowerRelease(body); break;
                case "NetMessageTryCancelPower":                  ParseTryCancelPower(body); break;
                case "NetMessageUpdateAvatarState":               ParseUpdateAvatarState(body); break;
            }
        }
    }

    private void ParseMissionUpdate(byte[] body)
    {
        if (MissionUpdated is null) return;
        try
        {
            var msg = NetMessageMissionUpdate.ParseFrom(body);
            var ev = new MissionUpdateEvent
            {
                MissionPrototypeId = msg.MissionPrototypeId,
                State = msg.HasMissionState ? msg.MissionState : 0u,
                HasState = msg.HasMissionState,
                ParticipantCount = msg.ParticipantsCount,
                SuppressNotification = msg.HasSuppressNotification && msg.SuppressNotification,
                Suspended = msg.HasSuspendedState ? msg.SuspendedState : null,
                UtcTime = DateTime.UtcNow,
            };
            MissionUpdated?.Invoke(this, ev);
        }
        catch (Exception ex)
        {
            Diagnostic?.Invoke($"MissionUpdate parse failed: {ex.Message}");
        }
    }

    private void ParseMissionObjectiveUpdate(byte[] body)
    {
        if (MissionObjectiveUpdated is null) return;
        try
        {
            var msg = NetMessageMissionObjectiveUpdate.ParseFrom(body);
            MissionObjectiveUpdated?.Invoke(this, new MissionObjectiveUpdateEvent
            {
                MissionPrototypeId = msg.MissionPrototypeId,
                ObjectiveIndex     = msg.ObjectiveIndex,
                HasState           = msg.HasObjectiveState,
                State              = msg.HasObjectiveState ? msg.ObjectiveState : 0u,
                CurrentCount       = msg.HasCurrentCount   ? msg.CurrentCount   : 0u,
                RequiredCount      = msg.HasRequiredCount  ? msg.RequiredCount  : 0u,
                UtcTime            = DateTime.UtcNow,
            });
        }
        catch (Exception ex)
        {
            Diagnostic?.Invoke($"MissionObjectiveUpdate parse failed: {ex.Message}");
        }
    }

    private void ParseEntityKill(byte[] body)
    {
        if (EntityKilled is null) return;
        try
        {
            var msg = NetMessageEntityKill.ParseFrom(body);
            EntityKilled?.Invoke(this, new EntityKillEvent
            {
                EntityId = msg.IdEntity,
                KillerEntityId = msg.IdKillerEntity,
                KillFlags = msg.KillFlags,
                UtcTime = DateTime.UtcNow,
            });
        }
        catch (Exception ex)
        {
            Diagnostic?.Invoke($"EntityKill parse failed: {ex.Message}");
        }
    }

    private void ParseEntityDestroy(byte[] body)
    {
        if (EntityDestroyed is null) return;
        try
        {
            var msg = NetMessageEntityDestroy.ParseFrom(body);
            EntityDestroyed?.Invoke(this, new EntityDestroyEvent
            {
                EntityId = msg.IdEntity,
                PrototypeId = msg.HasPrototypeId ? msg.PrototypeId : null,
                RegionId = msg.HasRegionId ? msg.RegionId : null,
                UtcTime = DateTime.UtcNow,
            });
        }
        catch (Exception ex)
        {
            Diagnostic?.Invoke($"EntityDestroy parse failed: {ex.Message}");
        }
    }

    // ───────────────────────── NetMessageLocalPlayer ─────────────────────────
    // One-shot "YOU are this entity" signal.  Arrives right after EnterGame, once per game
    // server session. No archive data — just a plain protobuf with two fields; we care about
    // field 1 (localPlayerEntityId). The second field (gameOptions) is a nested message whose
    // contents are irrelevant for DPS attribution.
    private void ParseLocalPlayer(byte[] body)
    {
        if (LocalPlayerIdentified is null) return;
        try
        {
            var msg = NetMessageLocalPlayer.ParseFrom(body);
            if (!msg.HasLocalPlayerEntityId || msg.LocalPlayerEntityId == 0) return;
            LocalPlayerIdentified?.Invoke(this, new LocalPlayerIdentifiedEvent
            {
                LocalPlayerEntityId = msg.LocalPlayerEntityId,
                UtcTime = DateTime.UtcNow,
            });
            Diagnostic?.Invoke($"LocalPlayer identified: entityId={msg.LocalPlayerEntityId}");
        }
        catch (Exception ex)
        {
            Diagnostic?.Invoke($"LocalPlayer parse failed: {ex.Message}");
        }
    }

    // ───────────────────────── NetMessageInventoryMove ─────────────────────────
    // Fires for every inventory relocation the client observes. Most are uninteresting (loot
    // pickup, gear swap, crafting ingredient moves) but the avatar-enter-world / avatar-swap
    // paths both surface here with ContainerEntityId == local Player id. DpsMeter filters on
    // that to pin the authoritative "this avatar is YOU right now" id.
    private void ParseInventoryMove(byte[] body)
    {
        if (InventoryMoved is null) return;
        try
        {
            var msg = NetMessageInventoryMove.ParseFrom(body);
            InventoryMoved?.Invoke(this, new InventoryMovedEvent
            {
                EntityId             = msg.EntityId,
                ContainerEntityId    = msg.InvLocContainerEntityId,
                InventoryPrototypeId = msg.InvLocInventoryPrototypeId,
                Slot                 = msg.InvLocSlot,
                UtcTime              = DateTime.UtcNow,
            });
        }
        catch (Exception ex)
        {
            Diagnostic?.Invoke($"InventoryMove parse failed: {ex.Message}");
        }
    }

    // ───────────────────────── NetMessageLootEntity ─────────────────────────
    // Fires every time the server spawns a visible loot entity on the ground in our AOI.
    // The wire payload is `NetMessageLootEntity { ItemId, ItemSpec }`, where ItemSpec is a
    // NetStructItemSpec carrying the full 64-bit PrototypeId (DataRef) of the item, its rolled
    // level, rarity, affixes, etc.  We only forward the bits a downstream tracker actually
    // needs (id + proto ref + level + rarity) so listeners stay decoupled from the protobuf.
    //
    // Used by Services.EternitySplinterTracker to detect Eternity Splinter drops -- matches
    // ItemProtoRef against the hardcoded PrototypeId pulled from MHServerEmu's
    // LootInstance.CombineCurrencyStacks call.  Any future "track when X drops" feature
    // (Cube Shards, Worldstones, raid tokens) plugs into this same event without further
    // sniffer changes.
    private void ParseLootEntity(byte[] body)
    {
        if (LootDropped is null) return;
        try
        {
            var msg  = NetMessageLootEntity.ParseFrom(body);
            var spec = msg.ItemSpec;
            if (spec is null) return;
            LootDropped?.Invoke(this, new LootDroppedEvent
            {
                ItemId         = msg.ItemId,
                ItemProtoRef   = spec.ItemProtoRef,
                ItemLevel      = spec.HasItemLevel      ? spec.ItemLevel      : 0u,
                RarityProtoRef = spec.HasRarityProtoRef ? spec.RarityProtoRef : 0uL,
                UtcTime        = DateTime.UtcNow,
            });
        }
        catch (Exception ex)
        {
            Diagnostic?.Invoke($"LootEntity parse failed: {ex.Message}");
        }
    }

    // ───────────────────────── NetMessageAddCondition / NetMessageDeleteCondition ────────────────
    //
    // The server emits one NetMessageAddCondition per buff/debuff application and a matching
    // NetMessageDeleteCondition when it expires / is dispelled / replaced.  AddCondition carries
    // a single archive blob; DeleteCondition is a plain protobuf with (idEntity, key).
    //
    // AddCondition archiveData layout (post-replication-header; from MHServerEmu's
    // Condition.Serialize and the IsTransient branch -- replication mode IS transient):
    //   [varint ulong:  ownerEntityId]                  // who's wearing the buff
    //   [varint uint:   serializationFlags]             // bitmask, gates the optional fields
    //   [varint ulong:  conditionId]                    // per-owner buff slot id
    //   [varint ulong:  creatorId]                      // omitted if CreatorIsOwner (bit 0)
    //   [varint ulong:  ultimateCreatorId]              // omitted if CreatorIsUltimateCreator (bit 1)
    //   [varint ulong:  conditionPrototypeRef]          // omitted if NoConditionPrototypeRef (bit 2)
    //   [varint ulong:  creatorPowerPrototypeRef]       // omitted if NoCreatorPowerPrototypeRef (bit 3)
    //   [varint uint:   creatorPowerIndex]              // only if HasCreatorPowerIndex (bit 4)
    //   [varint ulong:  ownerAssetRef]                  // only if HasOwnerAssetRefOverride (bit 9)
    //   [zigzag long:   startTime]                      // ms since Game.StartTime
    //   [zigzag long:   pauseTime]                      // only if HasPauseTime (bit 6)
    //   [zigzag long:   durationMs]                     // only if HasDuration (bit 7)
    //   [zigzag int:    updateIntervalMs]               // only if HasUpdateIntervalOverride (bit 10)
    //   [PropertyCollection: properties]                // the stat effects (DamagePercentBonus etc)
    //   [varint uint:   cancelOnFlags]                  // only if HasCancelOnFlagsOverride (bit 11)
    //
    // The PrototypeRefs are full 64-bit DataRef values (same encoding as splinter PrototypeId).
    // Match against hardcoded constants once the buff is identified empirically -- e.g.
    // Cyclops's Empowered buff and the Overwatch power that applies it both have stable Ids.
    private void ParseAddCondition(byte[] body)
    {
        if (ConditionAdded is null) return;
        try
        {
            var msg = NetMessageAddCondition.ParseFrom(body);
            byte[] archive = msg.ArchiveData.ToByteArray();
            var r = new GazillionArchiveReader(archive);
            r.ReadReplicationHeader();

            ulong ownerEntityId       = r.ReadVarUInt64();
            uint  serializationFlags  = r.ReadVarUInt32();
            ulong conditionId         = r.ReadVarUInt64();

            // ConditionSerializationFlags (MHServerEmu.Games.Powers.Conditions/ConditionSerializationFlags.cs):
            //   bit 0 (0x001) CreatorIsOwner                -- omit creatorId
            //   bit 1 (0x002) CreatorIsUltimateCreator      -- omit ultimateCreatorId
            //   bit 2 (0x004) NoConditionPrototypeRef       -- omit conditionPrototypeRef
            //   bit 3 (0x008) NoCreatorPowerPrototypeRef    -- omit creatorPowerPrototypeRef
            //   bit 4 (0x010) HasCreatorPowerIndex
            //   bit 6 (0x040) HasPauseTime
            //   bit 7 (0x080) HasDuration
            //   bit 9 (0x200) HasOwnerAssetRefOverride
            //   bit 10 (0x400) HasUpdateIntervalOverride
            //   bit 11 (0x800) HasCancelOnFlagsOverride
            const uint F_CreatorIsOwner            = 0x001;
            const uint F_CreatorIsUltimateCreator  = 0x002;
            const uint F_NoConditionPrototypeRef   = 0x004;
            const uint F_NoCreatorPowerPrototypeRef= 0x008;
            const uint F_HasCreatorPowerIndex      = 0x010;
            const uint F_HasPauseTime              = 0x040;
            const uint F_HasDuration               = 0x080;
            const uint F_HasOwnerAssetRefOverride  = 0x200;
            const uint F_HasUpdateIntervalOverride = 0x400;

            ulong creatorId             = (serializationFlags & F_CreatorIsOwner)           != 0 ? ownerEntityId : r.ReadVarUInt64();
            ulong ultimateCreatorId     = (serializationFlags & F_CreatorIsUltimateCreator) != 0 ? creatorId    : r.ReadVarUInt64();
            ulong conditionPrototypeRef = (serializationFlags & F_NoConditionPrototypeRef)  != 0 ? 0uL          : r.ReadVarUInt64();
            ulong creatorPowerProtoRef  = (serializationFlags & F_NoCreatorPowerPrototypeRef) != 0 ? 0uL        : r.ReadVarUInt64();
            if ((serializationFlags & F_HasCreatorPowerIndex)     != 0) _ = r.ReadVarUInt32();   // creatorPowerIndex, unused for tracking
            if ((serializationFlags & F_HasOwnerAssetRefOverride) != 0) _ = r.ReadVarUInt64();   // ownerAssetRef, unused for tracking
            _ = r.ReadVarInt64();                                                                // startTime (zigzag long, ms-since-game-start)
            if ((serializationFlags & F_HasPauseTime) != 0) _ = r.ReadVarInt64();                // pauseTime
            long durationMs        = (serializationFlags & F_HasDuration)               != 0 ? r.ReadVarInt64() : 0L;
            int  updateIntervalMs  = (serializationFlags & F_HasUpdateIntervalOverride) != 0 ? r.ReadVarInt32() : 0;

            // Skip the ReplicatedPropertyCollection's _replicationId varint -- the condition's
            // _properties field is typed ReplicatedPropertyCollection (not plain PropertyCollection),
            // and ReplicatedPropertyCollection.SerializeWithDefault writes an extra ulong before
            // delegating to the base class in replication mode (see
            // MHServerEmu.Games.Properties/ReplicatedPropertyCollection.cs:80).  Without this skip
            // the next 4 bytes we'd read as the property count are actually the replication-id's
            // varint bytes, which decode to garbage and trip the >4096 sanity check.
            _ = r.ReadVarUInt64();

            // PropertyCollection starts at the current reader offset.  Capture it -- the host
            // calls DumpPropertyCollectionAt(archive, offset, ...) to walk the properties for
            // discovery logging without us having to do the (potentially heavy) walk on every
            // condition application.
            int propertyCollectionOffset = r.CurrentOffset;

            // Parse the property collection into structured (enum, value) pairs.  Cost is
            // negligible -- conditions carry a handful of properties at most, and AddCondition
            // fires at most a few times per second per character.  Doing it eagerly here means
            // downstream consumers (BuffTracker / live-stats overlay) don't each have to
            // re-walk the archive; they just read .PropertyDeltas off the event.  On parse
            // failure ParsePropertyCollectionAt returns an empty list -- the buff is still
            // useful as a "buff X is active" indicator even when we can't pull its numbers.
            IReadOnlyList<BuffPropertyDelta> propertyDeltas =
                ParsePropertyCollectionAt(archive, propertyCollectionOffset);

            ConditionAdded?.Invoke(this, new ConditionAddedEvent
            {
                OwnerEntityId            = ownerEntityId,
                ConditionId              = conditionId,
                CreatorEntityId          = creatorId,
                UltimateCreatorEntityId  = ultimateCreatorId,
                ConditionPrototypeRef    = conditionPrototypeRef,
                CreatorPowerPrototypeRef = creatorPowerProtoRef,
                DurationMs               = durationMs,
                UpdateIntervalMs         = updateIntervalMs,
                RawProperties            = archive,
                PropertyCollectionOffset = propertyCollectionOffset,
                SerializationFlags       = serializationFlags,
                PropertyDeltas           = propertyDeltas,
                UtcTime                  = DateTime.UtcNow,
            });
        }
        catch (Exception ex)
        {
            Diagnostic?.Invoke($"AddCondition parse failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void ParseDeleteCondition(byte[] body)
    {
        if (ConditionRemoved is null) return;
        try
        {
            var msg = NetMessageDeleteCondition.ParseFrom(body);
            ConditionRemoved?.Invoke(this, new ConditionRemovedEvent
            {
                OwnerEntityId = msg.IdEntity,
                ConditionId   = msg.Key,
                UtcTime       = DateTime.UtcNow,
            });
        }
        catch (Exception ex)
        {
            Diagnostic?.Invoke($"DeleteCondition parse failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    // Server -> client community-state update.  The server fires this every time a community
    // member's state changes from the local client's perspective: friend status updates, party
    // info, and — most importantly for us — the "__Nearby" circle, which auto-populates with
    // every player entity inside the local client's AOI (see AreaOfInterest.cs, search for
    // CircleId.__Nearby).  That broadcast carries (memberPlayerDbId, currentPlayerName), which
    // is exactly what we need to turn a damager's dbId into a display nickname.
    //
    // Name is sometimes absent on update-only deltas (the client caches the previously-seen
    // name).  We mirror that caching in DpsMeter so we never blow away a name that was broadcast
    // once but re-broadcast without the string a second time.
    private void ParseModifyCommunityMember(byte[] body)
    {
        if (CommunityMemberUpdated is null) return;
        try
        {
            var msg = NetMessageModifyCommunityMember.ParseFrom(body);
            if (msg.HasBroadcast == false)
            {
                Diagnostic?.Invoke("ModifyCommunityMember: message has no broadcast, skipping");
                return;
            }
            var b = msg.Broadcast;
            if (b.HasMemberPlayerDbId == false)
            {
                Diagnostic?.Invoke("ModifyCommunityMember: broadcast has no memberPlayerDbId, skipping");
                return;
            }

            // Prefer the name carried on the top-level message (set on "newly created" updates —
            // see CommunityMember.SendUpdateToOwner), then fall back to the name inside the
            // broadcast, then give up and push an empty string (DpsMeter keeps the prior value).
            string name = msg.HasPlayerName        ? msg.PlayerName
                        : b.HasCurrentPlayerName   ? b.CurrentPlayerName
                        : string.Empty;

            // Slot-0 carries the player's currently-selected avatar as a PrototypeDataRef. We
            // only need the first slot (the "current" avatar) — the server populates slots in
            // the order Player.BuildCommunityBroadcast emits them, with the active avatar first.
            // Empty-slots or absent-avatarRefId just gives us 0, which the DpsMeter treats as
            // "no hero currently known for this dbId".
            ulong avatarRefId = 0;
            if (b.SlotsCount > 0)
            {
                var slot = b.SlotsList[0];
                if (slot.HasAvatarRefId) avatarRefId = slot.AvatarRefId;
            }

            // Rich diagnostic so we can debug nickname-resolution issues end-to-end without a
            // protocol dump.  Fields mirror CommunityMember.SendUpdateToOwner options so it's
            // obvious which update-path was taken on the server.  Include the slot-0 avatarRefId
            // so we can trace the community-slot fallback path end-to-end in the log — when
            // mid-session launches end up with unresolved #XXXX entries, the line below tells us
            // whether the ref was even transmitted or whether our NamesByDataRef table (in
            // HeroPrototypes, consumer-side) needs refreshing. We deliberately don't translate
            // the ref to a name here — HeroPrototypes lives in MarvelHeroesComporator, which
            // this sniffer project can't reference without creating a cycle.
            Diagnostic?.Invoke(
                $"ModifyCommunityMember: dbId=0x{b.MemberPlayerDbId:X} "
                + $"topLevelName={(msg.HasPlayerName ? $"'{msg.PlayerName}'" : "<unset>")} "
                + $"broadcastName={(b.HasCurrentPlayerName ? $"'{b.CurrentPlayerName}'" : "<unset>")} "
                + $"slots={b.SlotsCount} "
                + $"avatarRef={(avatarRefId == 0 ? "<unset>" : $"0x{avatarRefId:X16}")} "
                + $"isOnline={(b.HasIsOnline ? b.IsOnline.ToString() : "<unset>")} "
                + $"circles={(msg.HasSystemCirclesBitSet ? $"0x{msg.SystemCirclesBitSet:X}" : "<unset>")}");

            CommunityMemberUpdated?.Invoke(this, new CommunityMemberUpdatedEvent
            {
                PlayerDbId         = b.MemberPlayerDbId,
                PlayerName         = name,
                CurrentAvatarRefId = avatarRefId,
                // NewlyCreated: the server only sets the top-level playerName on the very first
                // SendUpdateToOwner after a member is created (see CommunityMember.cs:461).  That
                // makes msg.HasPlayerName the authoritative "this is a brand-new community
                // member" signal — exactly when we want to pair with the preceding avatar
                // EntityCreate.  (We used to also trigger on SlotsCount>0 as a "repeat nearby"
                // heuristic, but the real-world log showed those slot-only follow-ups arrive
                // strictly AFTER the NewlyCreated broadcast for the same dbId, so they only
                // caused false pairings — removed.)
                IsInitial   = msg.HasPlayerName,
                HasCircles  = msg.HasSystemCirclesBitSet,
                Circles     = msg.HasSystemCirclesBitSet ? msg.SystemCirclesBitSet : 0UL,
                UtcTime     = DateTime.UtcNow,
            });
        }
        catch (Exception ex)
        {
            Diagnostic?.Invoke($"ModifyCommunityMember parse failed: {ex.Message}");
        }
    }

    // ───────────────────────── Client -> server power messages (local-avatar fingerprint) ──────
    // These three carry the authoritative "I'm playing this avatar" fingerprint: they're sent
    // by the LOCAL client, so the `idUserEntity` value can only be YOUR avatar.  Handy because
    // NetMessageLocalPlayer only fires at login — and if the app was launched mid-session we'd
    // never see it.  A single key press hits ParseTryActivatePower and pins self-owner instantly.
    //
    // We keep a one-shot log-dedupe counter so the diagnostic feed isn't flooded.  The id itself
    // flows through `LocalAvatarObserved`, whose subscriber (DpsMeter) de-dupes semantically.
    private ulong _lastObservedLocalAvatarId;

    private void EmitLocalAvatarObserved(ulong userEntityId, string sourceMsgName)
    {
        if (LocalAvatarObserved is null || userEntityId == 0) return;

        // Hot-path dedupe: UpdateAvatarState fires 20+ Hz during movement and almost always
        // reports the same avatar id across consecutive frames. Short-circuit here so we don't
        // allocate an event args object per frame just to have DpsMeter drop it. The id flip on
        // an avatar swap still gets through because _lastObservedLocalAvatarId updates below.
        if (_lastObservedLocalAvatarId == userEntityId)
            return;

        _lastObservedLocalAvatarId = userEntityId;
        LocalAvatarObserved.Invoke(this, new LocalAvatarObservedEvent
        {
            LocalAvatarEntityId = userEntityId,
            UtcTime = DateTime.UtcNow,
        });
        Diagnostic?.Invoke($"Local avatar observed: entityId={userEntityId} (via {sourceMsgName})");
    }

    private void ParseTryActivatePower(byte[] body)
    {
        if (LocalAvatarObserved is null && LocalPowerActivated is null) return;
        try
        {
            var msg = NetMessageTryActivatePower.ParseFrom(body);
            if (msg.HasIdUserEntity) EmitLocalAvatarObserved(msg.IdUserEntity, "TryActivatePower");
            if (LocalPowerActivated is not null
                && msg.HasIdUserEntity
                && msg.HasPowerPrototypeId
                && msg.PowerPrototypeId != 0)
            {
                LocalPowerActivated.Invoke(this, new LocalPowerActivatedEvent
                {
                    LocalAvatarEntityId = msg.IdUserEntity,
                    PowerPrototypeId    = msg.PowerPrototypeId,
                    UtcTime             = DateTime.UtcNow,
                });

                // Arm the SetProperty focus window: log every property delta for
                // the next 2 seconds in full so we can correlate the cast with
                // server-side cooldown updates.
                _setPropertyFocusProto    = (uint)(msg.PowerPrototypeId & 0xFFFFFFFFu);
                _setPropertyFocusUntilUtc = DateTime.UtcNow.AddSeconds(2);
                Diagnostic?.Invoke($"== FOCUS WINDOW armed for power #{_setPropertyFocusProto} (2s) ==");

                // Cooldown-debug dump: when the user fires a power, snapshot the
                // current S2C message-name distribution so we can see WHICH server
                // messages arrived in the same session.  Capped to first ~5 dumps.
                long n = System.Threading.Interlocked.Increment(ref _tryActivateDumpBudget);
                if (n <= 5)
                {
                    var sb = new System.Text.StringBuilder();
                    sb.Append($"TryActivate dump[{n}]: S2C counts so far -> ");
                    foreach (var kv in ServerToClientCounts)
                        sb.Append($"{kv.Key}={kv.Value} ");
                    Diagnostic?.Invoke(sb.ToString());
                }
            }
        }
        catch (Exception ex)
        {
            Diagnostic?.Invoke($"TryActivatePower parse failed: {ex.Message}");
        }
    }

    private long _tryActivateDumpBudget;

    private void ParsePowerRelease(byte[] body)
    {
        if (LocalAvatarObserved is null) return;
        try
        {
            var msg = NetMessagePowerRelease.ParseFrom(body);
            if (msg.HasIdUserEntity) EmitLocalAvatarObserved(msg.IdUserEntity, "PowerRelease");
        }
        catch (Exception ex)
        {
            Diagnostic?.Invoke($"PowerRelease parse failed: {ex.Message}");
        }
    }

    /// <summary>Parse <c>NetMessageSetProperty</c> and surface a <see cref="PropertyChangedEvent"/>.
    /// The message carries (replicationId, propertyId, valueBits) -- we decode the propertyId
    /// into its (enum, paramBits) parts and pass everything raw to the consumer.
    ///
    /// <para><b>Wire endianness:</b> the propertyId field is byte-reversed on the wire (same
    /// gotcha as <c>ParsePropertyCollectionAt</c> and the splinter-currency parser).  Without
    /// the swap the enum value would read as a garbage bit-pattern, e.g. a Cooldown property
    /// (732 = 0x2DC) would appear as the top byte of some unrelated value.</para>
    ///
    /// <para>No filtering happens here -- consumers (CooldownTracker) handle that.  Cheap
    /// when nothing's subscribed (early-out at the top); cheap when consumers don't care
    /// about this particular property (one enum compare and they bail).</para></summary>
    /// <summary>Bounded counter for cooldown-feature diagnostic logging.  We log the
    /// first ~50 SetProperty arrivals (and a sample of distinct enums after) so the
    /// user can confirm whether cooldowns even arrive via SetProperty without the
    /// log getting overwhelmed in a busy session.</summary>
    private long _setPropertyLogBudget;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<uint, byte> _setPropertyEnumsSeen = new();

    private void ParseSetProperty(byte[] body)
    {
        try
        {
            var msg = NetMessageSetProperty.ParseFrom(body);
            if (!msg.HasReplicationId || !msg.HasPropertyId) return;
            ulong propertyIdRaw = System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(msg.PropertyId);
            uint  enumValue     = (uint)(propertyIdRaw >> 53);
            ulong paramBits     = propertyIdRaw & 0x1FFFFFFFFFFFFFul;
            ulong valueBits     = msg.HasValueBits ? msg.ValueBits : 0;

            // Diagnostic logging is now driven by the CooldownTracker's empirical
            // signature learning -- it logs the high-signal "LEARNED signature"
            // line when it observes a new (enum, paramBits) -> power mapping.
            // Per-event SetProperty spam is no longer useful; keep just a single
            // first-N tracer so we can confirm the dispatch path still works at
            // startup.
            long n = System.Threading.Interlocked.Increment(ref _setPropertyLogBudget);
            if (n == 1)
            {
                Diagnostic?.Invoke($"SetProperty dispatch live: first event repId={msg.ReplicationId} enum={enumValue}");
            }

            if (PropertyChanged is null) return;
            PropertyChanged.Invoke(this, new PropertyChangedEvent
            {
                ReplicationId = msg.ReplicationId,
                PropertyEnum  = enumValue,
                ParamBits     = paramBits,
                ValueBits     = valueBits,
                Removed       = false,
                UtcTime       = DateTime.UtcNow,
            });
        }
        catch (Exception ex)
        {
            Diagnostic?.Invoke($"SetProperty parse failed: {ex.Message}");
        }
    }

    /// <summary>Focus-window state: when this is non-MinValue and now &lt; UntilUtc,
    /// every SetProperty event is logged in full regardless of budget.  Set by
    /// ParseTryActivatePower whenever a local power is cast.  2-second window is
    /// generous enough to capture the server's full response (PreActivate +
    /// Activate + cooldown property deltas).</summary>
    private DateTime _setPropertyFocusUntilUtc = DateTime.MinValue;
    private uint     _setPropertyFocusProto;

    /// <summary>Parse <c>NetMessageRemoveProperty</c> -- same wire layout as SetProperty
    /// but the property was CLEARED rather than set.  Fires the same
    /// <see cref="PropertyChangedEvent"/> with <see cref="PropertyChangedEvent.Removed"/>
    /// = true so downstream code knows to mark the relevant state as "absent".
    /// Cooldowns end via this path: the server removes <c>PowerCooldownDuration</c>
    /// from the avatar's property collection when the cooldown elapses.</summary>
    private void ParseRemoveProperty(byte[] body)
    {
        if (PropertyChanged is null) return;
        try
        {
            var msg = NetMessageRemoveProperty.ParseFrom(body);
            if (!msg.HasReplicationId || !msg.HasPropertyId) return;
            ulong propertyIdRaw = System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(msg.PropertyId);
            uint  enumValue     = (uint)(propertyIdRaw >> 53);
            ulong paramBits     = propertyIdRaw & 0x1FFFFFFFFFFFFFul;
            PropertyChanged.Invoke(this, new PropertyChangedEvent
            {
                ReplicationId = msg.ReplicationId,
                PropertyEnum  = enumValue,
                ParamBits     = paramBits,
                ValueBits     = 0,
                Removed       = true,
                UtcTime       = DateTime.UtcNow,
            });
        }
        catch (Exception ex)
        {
            Diagnostic?.Invoke($"RemoveProperty parse failed: {ex.Message}");
        }
    }

    /// <summary>Diagnostic-only handler for <c>NetMessageActivatePower</c> (server-to-
    /// client confirmation of a power activation).  The message carries an
    /// <c>archiveData</c> blob that MAY contain embedded cooldown state -- if our
    /// NetMessageSetProperty hypothesis turns out wrong, this is the most likely
    /// alternate vehicle.  We log the first ~30 arrivals with archive size + a hex
    /// dump of the first 64 bytes so we can post-hoc analyze the layout.</summary>
    private long _activatePowerLogBudget;
    private void ParseActivatePowerForDiag(byte[] body)
    {
        long n = System.Threading.Interlocked.Increment(ref _activatePowerLogBudget);
        if (n > 30) return;
        try
        {
            var msg = NetMessageActivatePower.ParseFrom(body);
            int archiveLen = msg.HasArchiveData ? msg.ArchiveData.Length : 0;
            string preview = archiveLen > 0
                ? Convert.ToHexString(msg.ArchiveData.ToByteArray(),
                                      0, Math.Min(64, archiveLen))
                : "<empty>";
            Diagnostic?.Invoke($"ActivatePower[{n}] archiveLen={archiveLen} first64={preview}");
        }
        catch (Exception ex)
        {
            Diagnostic?.Invoke($"ActivatePower parse failed: {ex.Message}");
        }
    }

    private long _preActivatePowerLogBudget;
    private void ParsePreActivatePowerForDiag(byte[] body)
    {
        long n = System.Threading.Interlocked.Increment(ref _preActivatePowerLogBudget);
        if (n > 30) return;
        try
        {
            var msg = NetMessagePreActivatePower.ParseFrom(body);
            // PreActivatePower has minimal fields; this is just a "we saw the message" tracer.
            Diagnostic?.Invoke($"PreActivatePower[{n}] size={body.Length}");
        }
        catch (Exception ex)
        {
            Diagnostic?.Invoke($"PreActivatePower parse failed: {ex.Message}");
        }
    }

    private void ParseTryCancelPower(byte[] body)
    {
        if (LocalAvatarObserved is null) return;
        try
        {
            var msg = NetMessageTryCancelPower.ParseFrom(body);
            if (msg.HasIdUserEntity) EmitLocalAvatarObserved(msg.IdUserEntity, "TryCancelPower");
        }
        catch (Exception ex)
        {
            Diagnostic?.Invoke($"TryCancelPower parse failed: {ex.Message}");
        }
    }

    // NetMessageUpdateAvatarState is the CONTINUOUS pulse — the client emits it while your avatar
    // is in the world (on move, on state change, periodically while idle in some builds). Archive
    // layout from PlayerConnection.OnUpdateAvatarState in EmuSource:
    //   1) avatarIndex       (ZigZag VarInt int)
    //   2) avatarEntityId    (VarInt ulong)          <── this is ALL we need
    //   3) isUsingGamepadInput (bool)
    //   4) avatarWorldInstanceId (VarInt uint)
    //   5) fieldFlagsRaw     (VarInt uint)
    //   6) syncPosition      (TransferVectorFixed)
    //   … (more locomotion state we don't care about)
    //
    // We only read fields 1-2, so even if the later schema drifts between game versions we won't
    // desync — worst case the next archive's format is wildly different and our ulong read throws,
    // which we swallow (log once, move on).
    //
    // Hot-path optimisation: if we already emitted this avatarEntityId we skip the event entirely.
    // UpdateAvatarState fires 20+ Hz during movement so the delta check matters.
    private void ParseUpdateAvatarState(byte[] body)
    {
        if (LocalAvatarObserved is null) return;
        try
        {
            var msg = NetMessageUpdateAvatarState.ParseFrom(body);
            byte[] archive = msg.ArchiveData.ToByteArray();
            if (archive.Length < 3) return; // minimum: 1 byte header + 1 byte index + 1 byte id

            var r = new GazillionArchiveReader(archive);
            r.ReadReplicationHeader();
            // avatarIndex is transferred as `int` (ZigZag).  We don't need the value itself — the
            // primary avatar is index 0, teamup uses other indices — but we have to consume it to
            // stay aligned for the ulong that follows.
            _ = r.ReadVarInt32();
            ulong avatarEntityId = r.ReadVarUInt64();
            if (avatarEntityId != 0) EmitLocalAvatarObserved(avatarEntityId, "UpdateAvatarState");
        }
        catch
        {
            // Swallow silently — this message can fire 20+ times per second, a parse failure
            // loop would drown the log. If UpdateAvatarState ever breaks we still have
            // TryActivatePower / PowerRelease as backup identification channels.
        }
    }

    // ───────────────────────── NetMessagePowerResult (damage / healing) ─────────────────────────
    // Flag bits in the archive's leading uint32 VarInt, mirrored from EmuSource's
    // ArchiveMessageBuilder.PowerResultMessageFlags.  We keep the consts local (rather than dragging
    // in a reference to MHServerEmu.Games) so this file stays self-contained.
    private const uint PR_NoPowerOwnerEntityId      = 1 << 0;
    private const uint PR_IsSelfTarget              = 1 << 1;
    private const uint PR_NoUltimateOwnerEntityId   = 1 << 2;
    private const uint PR_UltimateOwnerIsPowerOwner = 1 << 3;
    private const uint PR_HasResultFlags            = 1 << 4;
    private const uint PR_HasPowerOwnerPosition     = 1 << 5;
    private const uint PR_HasDamagePhysical         = 1 << 6;
    private const uint PR_HasDamageEnergy           = 1 << 7;
    private const uint PR_HasDamageMental           = 1 << 8;
    private const uint PR_HasHealing                = 1 << 9;
    private const uint PR_HasPowerAssetRefOverride  = 1 << 10;
    private const uint PR_HasTransferToEntityId     = 1 << 11;

    /// <summary>How many of the first <c>NetMessagePowerResult</c> arrivals to verbose-log. After
    /// this many, we stop emitting the per-field dump to keep the log readable; a running total
    /// (<see cref="_powerResultTotal"/>) still tracks the message volume.</summary>
    private const int PowerResultVerboseDumpCount = 30;
    /// <summary>After the verbose head dump exhausts, sample 1-in-N events with the same
    /// per-field log line so the file still shows the active owner / damage distribution
    /// during long sessions.  Keeps log volume bounded (~16 lines/sec at 800/sec wire rate)
    /// while preserving enough signal to triage "DPS stuck at 0" without re-instrumenting.</summary>
    private const int PowerResultSampleEveryN = 50;
    private int _powerResultTotal;
    private int _powerResultParseFailures;
    private int _powerResultNoSubscriber;

    private void ParsePowerResult(byte[] body)
    {
        int seq = System.Threading.Interlocked.Increment(ref _powerResultTotal);
        // Always-verbose for the initial dump, then drop to a thin sampled stream.  The
        // sampled stream is essential for "worked for a minute then stopped" diagnostics:
        // we need to see WHICH owner is getting damage credit during the failure window,
        // and the head-only dump tells us nothing about events 31..N.
        bool verbose = seq <= PowerResultVerboseDumpCount
                       || (seq % PowerResultSampleEveryN) == 0;

        // Early-exit guard kept for throughput, but when verbose we log the fact so we don't get
        // fooled into thinking parsing failed when really nobody was listening.
        if (DamageDealt is null)
        {
            System.Threading.Interlocked.Increment(ref _powerResultNoSubscriber);
            if (verbose) Diagnostic?.Invoke($"PowerResult#{seq}: no DamageDealt subscriber, skipping");
            return;
        }

        try
        {
            // Outer protobuf envelope — just one field (`bytes archiveData`). Pull it out and feed
            // the raw bytes into our archive reader.
            var msg = NetMessagePowerResult.ParseFrom(body);
            byte[] archive = msg.ArchiveData.ToByteArray();
            if (verbose)
                Diagnostic?.Invoke($"PowerResult#{seq}: archiveLen={archive.Length} hex={BitConverter.ToString(archive, 0, Math.Min(archive.Length, 48))}");
            var r = new GazillionArchiveReader(archive);

            // Replication-mode archives ALWAYS begin with a VarInt replicationPolicy header (see
            // MHServerEmu.Core.Serialization.Archive.WriteHeader). Skipping this was the single
            // off-by-one that corrupted every subsequent field — without it `messageFlags` ends up
            // reading the header (0x01 = AOIChannelProximity) and every later field is shifted.
            r.ReadReplicationHeader();

            // Field order must match ArchiveMessageBuilder.BuildPowerResultMessage exactly. If the
            // server ever changes that order, we will silently mis-parse later fields — DPS values
            // going wildly nonsensical (or a truncation exception partway through) is the canary.
            uint messageFlags  = r.ReadVarUInt32();
            // powerProtoRef: we do NOT skip it any more — used as a fallback hero-identification
            // signal by the DpsMeter. Every damaging player power lives at
            // Powers/Player/<HeroName>/… so the enum index uniquely identifies the owning avatar
            // even when we missed its EntityCreate (mid-session app launch, already-loaded region).
            uint powerProtoIdx = r.ReadPrototypeEnumIndex();
            ulong targetId     = r.ReadVarUInt64();

            ulong powerOwnerId;
            if ((messageFlags & PR_IsSelfTarget) != 0)
            {
                // IsSelfTarget ⇒ server skipped powerOwnerEntityId on the wire and we re-derive it
                // from the target (the same entity hit itself, e.g. self-buff DoT tick).
                powerOwnerId = targetId;
            }
            else if ((messageFlags & PR_NoPowerOwnerEntityId) != 0)
            {
                // Environmental / unowned damage (e.g. pylon tick); no attacker id.
                powerOwnerId = 0;
            }
            else
            {
                powerOwnerId = r.ReadVarUInt64();
            }

            ulong ultimateOwnerId;
            if ((messageFlags & PR_UltimateOwnerIsPowerOwner) != 0)
                ultimateOwnerId = powerOwnerId;    // pet-less attack: attacker IS the ultimate owner
            else if ((messageFlags & PR_NoUltimateOwnerEntityId) != 0)
                ultimateOwnerId = 0;
            else
                ultimateOwnerId = r.ReadVarUInt64();

            ulong resultFlags = (messageFlags & PR_HasResultFlags) != 0 ? r.ReadVarUInt64() : 0;
            uint  damPhys     = (messageFlags & PR_HasDamagePhysical) != 0 ? r.ReadVarUInt32() : 0;
            uint  damEner     = (messageFlags & PR_HasDamageEnergy)   != 0 ? r.ReadVarUInt32() : 0;
            uint  damMen      = (messageFlags & PR_HasDamageMental)   != 0 ? r.ReadVarUInt32() : 0;
            uint  healing     = (messageFlags & PR_HasHealing)        != 0 ? r.ReadVarUInt32() : 0;
            // Remaining optional fields (asset ref override, owner position, transferToId) are not
            // needed for a DPS panel — we stop reading here. Trailing bytes stay unread in the
            // archive, which is harmless because each NetMessagePowerResult has a fresh archive.

            if (verbose)
                Diagnostic?.Invoke(
                    $"PowerResult#{seq}: flags=0x{messageFlags:X} target={targetId} owner={powerOwnerId} ult={ultimateOwnerId} " +
                    $"resFlags=0x{resultFlags:X} dam=(phys={damPhys},ener={damEner},men={damMen}) heal={healing} total={damPhys+damEner+damMen}");

            DamageDealt?.Invoke(this, new DamageDealtEvent
            {
                UtcTime                 = DateTime.UtcNow,
                TargetEntityId          = targetId,
                PowerOwnerEntityId      = powerOwnerId,
                UltimateOwnerEntityId   = ultimateOwnerId,
                DamagePhysical          = damPhys,
                DamageEnergy            = damEner,
                DamageMental            = damMen,
                Healing                 = healing,
                ResultFlags             = resultFlags,
                PowerPrototypeEnumIndex = powerProtoIdx,
            });
        }
        catch (Exception ex)
        {
            System.Threading.Interlocked.Increment(ref _powerResultParseFailures);
            // Archive parsing failures are noisy to the user in a hot loop, so only surface them
            // once per session via the counter; actual diagnostic string goes to the log.
            Diagnostic?.Invoke($"PowerResult#{seq} parse failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>Counters the host can surface in its heartbeat: total PowerResult arrivals seen,
    /// how many were dropped due to no subscriber, how many crashed in the archive reader. If
    /// <c>NoSubscriber == Total</c>, the DpsMeter was never wired up.  If <c>ParseFailures &gt; 0</c>,
    /// the archive schema drifted and we need a dump.</summary>
    public (int Total, int NoSubscriber, int ParseFailures) PowerResultStats => (
        System.Threading.Volatile.Read(ref _powerResultTotal),
        System.Threading.Volatile.Read(ref _powerResultNoSubscriber),
        System.Threading.Volatile.Read(ref _powerResultParseFailures));

    // ───────────────────────── NetMessageEntityCreate (baseData prefix only) ─────────────────────
    // baseData schema (from ArchiveMessageBuilder.BuildEntityCreateMessage):
    //   entityId (VarInt ulong)  →  entityPrototypeRef (PrototypeEnum VarInt)  →  fieldFlagsRaw (VarInt uint) ...
    // We read only the first two fields and bail.  Everything after that (flag-gated positions,
    // locomotion state, inventory-location blocks) is expensive to parse and unneeded for DPS.
    private void ParseEntityCreate(byte[] body)
    {
        if (EntityCreated is null) return;
        try
        {
            var msg = NetMessageEntityCreate.ParseFrom(body);
            byte[] archive = msg.BaseData.ToByteArray();
            var r = new GazillionArchiveReader(archive);

            // Same replication-policy header as PowerResult — without it entityId reads as the
            // policy value (usually 1) and the prototype index reads the real entityId. See
            // Archive.WriteHeader for the format.
            r.ReadReplicationHeader();

            ulong entityId = r.ReadVarUInt64();
            uint  protoIdx = r.ReadPrototypeEnumIndex();

            // Mirror ArchiveMessageBuilder.BuildEntityCreateMessage: next comes the field-flags,
            // then loco-flags, then optional interestPolicies, then optional
            // avatarWorldInstanceId, then — only when HasDbId (1 << 8) is set, which the server
            // only toggles for Player container entities — the database-unique-id of the
            // player. That's the single ulong that bridges the avatarEntityId → playerName
            // resolver, so it's the only tail field we actually care about parsing.
        ulong dbId = 0;
        bool isAvatar = false;
        uint fieldFlagsDbg = 0;                     // captured for diagnostic logging below
        uint locoFieldFlagsDbg = 0;                 // captured for LikelyInInventory + diagnostic
        try
        {
            uint fieldFlags    = (uint)r.ReadVarUInt64();
            fieldFlagsDbg      = fieldFlags;
            uint locoFieldFlags = (uint)r.ReadVarUInt64();
            locoFieldFlagsDbg  = locoFieldFlags;

            const uint HasNonProximityInterest  = 1u << 5;
            const uint HasDbId                  = 1u << 8;
            const uint HasAvatarWorldInstanceId = 1u << 9;

            // Flag is server-authoritative "this is an Avatar" marker; see struct doc.
            isAvatar = (fieldFlags & HasAvatarWorldInstanceId) != 0;

            if ((fieldFlags & HasNonProximityInterest) != 0)
                _ = r.ReadVarUInt64();                          // interestPolicies (uint32 varint)

            if ((fieldFlags & HasAvatarWorldInstanceId) != 0)
                _ = r.ReadVarUInt64();                          // avatarWorldInstanceId (uint32 varint)

            if ((fieldFlags & HasDbId) != 0)
                dbId = r.ReadVarUInt64();
        }
        catch
        {
            // Tail parsing is best-effort — if the layout shifts we still want the
            // entityId+prototype pair to propagate normally.  The header byte where the
            // flag lives is always present, though, so we keep whatever `isAvatar` got.
            dbId = 0;
        }

        // For avatars, try to pull the nickname straight out of the archive blob — see
        // ScanAvatarPlayerName below for the full story. This is the ONLY path that can
        // carry player names for users who were already in your Guild/Friends circle
        // before you entered proximity (NetMessageModifyCommunityMember suppresses the
        // PlayerName field in that case — see Community.cs NewlyCreated branch).
        string avatarName = string.Empty;
        ulong  ownerDbId  = 0;
        if (isAvatar)
        {
            try
            {
                byte[] archiveBytes = msg.ArchiveData.ToByteArray();
                (avatarName, ownerDbId) = ScanAvatarPlayerName(archiveBytes);
            }
            catch { /* scanner is best-effort */ }
        }

        // Emit a one-liner for every avatar EntityCreate so we can verify the sniffer is
        // actually seeing the packet.  The name-resolution pipeline enqueues on IsAvatar,
        // so anything that flows through here but never shows up in DpsMeter's
        // "queued hero avatar" log is a DpsMeter-side filter issue — anything that DOESN'T
        // show up here is either the sniffer missing the packet or the flag decode being
        // wrong.  Hidden behind the IsAvatar test so summoned minions / NPCs don't swamp
        // the log (there can be hundreds per second in dense content).
        if (isAvatar)
        {
            Diagnostic?.Invoke(
                $"EntityCreate[Avatar] entityId={entityId} protoIdx={protoIdx} "
              + $"fieldFlags=0x{fieldFlagsDbg:X} dbId=0x{dbId:X} "
              + $"scannedName='{avatarName}' scannedOwnerDbId=0x{ownerDbId:X}");
        }

            // Pull the InventoryStackCount property out of archiveData for non-avatar entities.
            // Avatars don't have a meaningful stack count; saving the parse on the (very common)
            // avatar EntityCreate cuts dead work in dense crowds.  Failures (corrupt archive,
            // schema drift, archive that doesn't lead with a property collection) return 0 and
            // we proceed normally -- the splinter tracker treats 0 as "unknown, default to 1".
            //
            // Also carries the raw archive bytes through on the event so debug consumers can
            // dump the full property collection on-demand (currently the splinter tracker does
            // this on every matched drop -- it's the fastest way to reverse-engineer per-server
            // wire-format differences without adding round-trip telemetry).
            int stackCount = 0;
            bool isCurrencyDrop = false;
            ulong currencyParams = 0ul;
            byte[]? rawArchive = null;
            if (!isAvatar)
            {
                try
                {
                    rawArchive = msg.ArchiveData.ToByteArray();
                    TryExtractStackCount(rawArchive, out stackCount, out isCurrencyDrop, out currencyParams);
                }
                catch { /* swallow; stack-count extraction is best-effort */ }
            }

            EntityCreated?.Invoke(this, new EntityCreatedEvent
            {
                EntityId           = entityId,
                PrototypeEnumIndex = protoIdx,
                DatabaseUniqueId   = dbId,
                IsAvatar           = isAvatar,
                PlayerName         = avatarName,
                OwnerPlayerDbId    = ownerDbId,
                StackCount         = stackCount,
                RawArchive         = rawArchive,
                IsCurrencyDrop     = isCurrencyDrop,
                CurrencyParams     = currencyParams,
                RawFieldFlags      = fieldFlagsDbg,
                RawLocoFieldFlags  = locoFieldFlagsDbg,
                UtcTime            = DateTime.UtcNow,
            });
        }
        catch (Exception ex)
        {
            Diagnostic?.Invoke($"EntityCreate parse failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Heuristic scanner that pulls the Avatar's <c>_playerName</c> and
    /// <c>_ownerPlayerDbId</c> out of the transient archive blob carried by
    /// <c>NetMessageEntityCreate.archiveData</c> — without having to fully deserialize
    /// the Agent/WorldEntity/Entity base state (properties, conditions, etc.) that
    /// precedes them.
    /// </summary>
    /// <remarks>
    /// Why this exists:  The Avatar's <c>_playerName</c> is bound to
    /// <c>AOIChannelProximity</c> (<c>Avatar.BindReplicatedFields</c>), which means it's
    /// sent to every nearby client — the same channel that draws the nickname above the
    /// character's head in-game.  We already receive this data on the wire, we just
    /// weren't parsing it.  The <c>NetMessageModifyCommunityMember</c> path we were
    /// relying on suppresses <c>SetPlayerName</c> for any player already in your
    /// Guild/Friends circle (see <c>Community.AddMember</c> / <c>NewlyCreated</c>), so
    /// for those users the community-member channel is silent and their names never
    /// appear on the leaderboard — exactly what the user was seeing.
    ///
    /// Pattern we're matching (see <c>Avatar.Serialize</c> <c>IsTransient</c> block +
    /// <c>RepString.Serialize</c> in MHServerEmu):
    /// <code>
    ///   [base.Serialize ...]                              // properties / conditions
    ///   [repId varint]  [strlen varint]  [UTF-8 bytes]    // RepString _playerName
    ///   [ownerDbId varint]                                // ulong _ownerPlayerDbId
    ///   [0x00]                                            // empty emptyString ("")
    ///   ...                                               // guild data, key mappings
    /// </code>
    /// The dbId is a database GUID allocated by the server, empirically falling in the
    /// <c>0x2000_0000_0000_0000</c> range — so it encodes to exactly 9 varint bytes and
    /// starts with a specific high nibble.  That, plus the forced <c>0x00</c>
    /// terminator for the trailing empty string, makes the whole block very distinctive
    /// and unlikely to collide with random property payload earlier in the archive.
    /// </remarks>
    /// <returns>
    /// (playerName, ownerDbId) tuple.  Empty string / 0 on no confident match — callers
    /// should fall back to the community-member correlation path in that case.
    /// </returns>
    /// <summary>
    /// Attempts to extract the currency / item quantity from a replication-mode entity
    /// archive.  Returns <c>true</c> and sets <paramref name="stackCount"/> when the entity
    /// carries either an <c>ItemCurrency</c> property (currency-stack drops like Eternity
    /// Splinters) or a non-trivial <c>InventoryStackCount</c> (regular stackable items).
    /// Returns <c>false</c> with <c>stackCount = 0</c> for entities that don't carry either
    /// property, or on parse failure.
    ///
    /// <para><b>Why two properties</b> (confirmed by decompiling MHServerEmu 1.0.1's
    /// <c>LootManager.SpawnItemInternal</c>): currency drops like splinters spawn as Item
    /// entities with <c>InventoryStackCount = 1</c> hardcoded (one ground stack), and the
    /// ACTUAL amount stored on <c>ItemCurrency[currencyProtoRef] = amount</c>.  Reading only
    /// InventoryStackCount would always report 1 splinter regardless of whether the user
    /// got 1 or 100.  We prefer ItemCurrency when present; otherwise fall back to
    /// InventoryStackCount for non-currency stackables.</para>
    ///
    /// <para><b>Wire format</b>: <c>Entity.Serialize</c> just calls
    /// <c>Properties.SerializeWithDefault</c>, which writes:
    /// <code>
    ///   [varint: replication policy]
    ///   [4 bytes LE uint: numProperties]
    ///   for each property:
    ///     [varint ulong: BYTE-REVERSED PropertyId.Raw]  -- see Serializer.Transfer(ref PropertyId)
    ///     [varint ulong: value bits]                     -- Integer type decodes as (bits &gt;&gt; 1) | (bits &lt;&lt; 63)
    /// </code>
    /// The 4-byte-LE count is unusual -- it's a back-patch in the writer that requires fixed
    /// width.</para>
    ///
    /// <para><b>The byte-reversal gotcha</b>: MHServerEmu's <c>Serializer.Transfer(ref PropertyId)</c>
    /// calls <c>ioData.Raw.ReverseBytes()</c> before writing the varint (and reverses again
    /// on read).  This puts the property enum (originally in the high 11 bits of Raw) into
    /// the LOW bits of the on-wire ulong, which makes the small enum values varint-encode
    /// efficiently in 1-3 bytes instead of the 10 bytes a value with bit 63 set would
    /// require.  Side effect: the wire value's "enum" position is the low byte, so to
    /// recover PropertyId.Raw we have to reverse the bytes again after reading.  My v1 and
    /// v2 parsers both got this wrong; the user's "stackCount=0" log line is the smoking
    /// gun -- without the reversal, no property comparison ever matches.</para>
    ///
    /// <para>We match the property enum (top 11 bits of the recovered Raw) rather than the
    /// full Raw value, because <c>ItemCurrency</c> takes a CurrencyRef parameter packed into
    /// the low bits and we don't have the client's prototype-enum table to reconstruct the
    /// exact Raw.  Matching on enum alone is safe for splinter entities -- they carry exactly
    /// one ItemCurrency property (their own currency), so any match IS the value we want.</para>
    /// </summary>
    /// <summary>Test hook -- the regular consumer of this method is <c>ParseEntityCreate</c>
    /// (private to the sniffer).  Exposed as internal so unit tests in
    /// <c>MarvelHeroes.DpsMeter.Tests</c> can pin the wire-format math without spinning
    /// up an MhMissionSniffer instance.</summary>
    internal static bool TestTryExtractStackCount(byte[] archive, out int stackCount)
        => TryExtractStackCount(archive, out stackCount);

    /// <summary>
    /// Decode an entity archive's property collection and emit one diagnostic line per
    /// property: <c>prop[i] enum=N params=0x... value_int=N value_raw=0x...</c>.  Intended
    /// for one-off "what's actually in this archive?" investigations -- the
    /// <c>EternitySplinterTracker</c> calls this on every matched splinter so the log
    /// captures the full property bag, which is the fastest way to reverse-engineer
    /// per-server wire-format differences (different PropertyEnum values, additional
    /// properties before the one we expect, etc.) without adding round-trip telemetry.
    ///
    /// <para>Best-effort: corrupt / truncated / non-property-collection archives emit a
    /// single error line and bail out rather than propagating an exception.</para>
    /// </summary>
    public static void DumpPropertyCollection(byte[]? archive, Action<string>? diagnostic, string contextTag = "", Func<uint, string?>? propertyEnumNameResolver = null)
    {
        if (diagnostic == null) return;
        if (archive == null || archive.Length < 5)
        {
            diagnostic($"DumpPropertyCollection({contextTag}): archive null or too small ({archive?.Length ?? -1} bytes)");
            return;
        }
        try
        {
            var r = new GazillionArchiveReader(archive);
            r.ReadReplicationHeader();
            // Skip the ReplicatedPropertyCollection's _replicationId varint -- Entity.Properties
            // is typed ReplicatedPropertyCollection, not plain PropertyCollection, and the
            // replication-mode override writes an extra ulong before the property count.
            // See MHServerEmu.Games.Properties/ReplicatedPropertyCollection.cs:80.
            _ = r.ReadVarUInt64();
            DumpPropertyCollectionFromReader(r, diagnostic, contextTag, archive, propertyEnumNameResolver);
        }
        catch (Exception ex)
        {
            diagnostic($"DumpPropertyCollection({contextTag}): parse threw {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Dump the <c>PropertyCollection</c> embedded at <paramref name="startOffset"/> in
    /// <paramref name="archive"/>.  Used for buff payloads where the property bag isn't at
    /// the start of the archive -- the condition packet has owner-id / flags / proto refs /
    /// timing fields preceding it.
    ///
    /// <para>The <see cref="ConditionAddedEvent.PropertyCollectionOffset"/> field is
    /// populated by the sniffer's parser specifically to feed this method.</para>
    /// </summary>
    public static void DumpPropertyCollectionAt(byte[]? archive, int startOffset, Action<string>? diagnostic, string contextTag = "", Func<uint, string?>? propertyEnumNameResolver = null)
    {
        if (diagnostic == null) return;
        if (archive == null || startOffset < 0 || startOffset >= archive.Length)
        {
            diagnostic($"DumpPropertyCollectionAt({contextTag}): bad inputs archive={archive?.Length ?? -1} offset={startOffset}");
            return;
        }
        try
        {
            var r = new GazillionArchiveReader(archive);
            if (startOffset > 0) r.ReadRawBytes(startOffset);
            DumpPropertyCollectionFromReader(r, diagnostic, contextTag, archive, propertyEnumNameResolver);
        }
        catch (Exception ex)
        {
            diagnostic($"DumpPropertyCollectionAt({contextTag}): parse threw {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Structured-result variant of <see cref="DumpPropertyCollectionAt"/>: walks the same
    /// property collection and returns the parsed (enum, params, value) tuples instead of
    /// logging them.  Used by the buff tracker to accumulate stat bonuses across all active
    /// buffs ("how much +%damage am I getting right now?") without going through the
    /// diagnostic log + regex roundtrip.
    ///
    /// <para>Returns an empty list on any parse failure -- the buff is still useful as a
    /// generic "Empowered is active" entry even if we can't pull its numeric effects out,
    /// so we degrade gracefully rather than throwing.</para>
    /// </summary>
    public static IReadOnlyList<BuffPropertyDelta> ParsePropertyCollectionAt(byte[]? archive, int startOffset)
    {
        if (archive == null || startOffset < 0 || startOffset >= archive.Length)
            return System.Array.Empty<BuffPropertyDelta>();
        try
        {
            var r = new GazillionArchiveReader(archive);
            if (startOffset > 0) r.ReadRawBytes(startOffset);
            uint numProperties = r.ReadRawUInt32LittleEndian();
            // Sanity bound (same as the dump path): real conditions have a handful of
            // properties at most.  Anything past 4096 is a parse misalignment.
            if (numProperties > 4096) return System.Array.Empty<BuffPropertyDelta>();
            var list = new List<BuffPropertyDelta>((int)numProperties);
            for (uint i = 0; i < numProperties; i++)
            {
                ulong wireRaw   = r.ReadVarUInt64();
                ulong valueBits = r.ReadVarUInt64();
                ulong raw       = System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(wireRaw);
                uint  enumValue = (uint)(raw >> 53);
                ulong paramBits = raw & 0x1FFFFFFFFFFFFFul;
                long  intValue   = (long)((valueBits >> 1) | (valueBits << 63));
                float floatValue = BitConverter.UInt32BitsToSingle((uint)valueBits);
                list.Add(new BuffPropertyDelta
                {
                    PropertyEnum = enumValue,
                    ParamBits    = paramBits,
                    IntValue     = intValue,
                    FloatValue   = floatValue,
                    RawValueBits = valueBits,
                });
            }
            return list;
        }
        catch
        {
            return System.Array.Empty<BuffPropertyDelta>();
        }
    }

    /// <summary>
    /// Inner-loop variant of <see cref="DumpPropertyCollection"/> for callers that have already
    /// advanced the reader past whatever header / preamble precedes the property collection.
    /// The condition wire format embeds a property collection deep inside the archive (after
    /// owner id / flags / proto refs / timing fields), so we can't just point
    /// <see cref="DumpPropertyCollection"/> at the start of the buffer -- we need to dump
    /// from wherever the caller is.
    ///
    /// <para>Identical reader semantics to the full <c>DumpPropertyCollection</c>: 4-byte LE
    /// count, then (PropertyId byte-reversed varint, value-bits varint) pairs.</para>
    /// </summary>
    private static void DumpPropertyCollectionFromReader(
        GazillionArchiveReader r, Action<string>? diagnostic, string contextTag, byte[] archiveForHexFallback,
        Func<uint, string?>? propertyEnumNameResolver = null)
    {
        if (diagnostic == null) return;
        uint numProperties = r.ReadRawUInt32LittleEndian();
        if (numProperties > 4096)
        {
            int hexLen = Math.Min(archiveForHexFallback.Length, 32);
            diagnostic($"DumpPropertyCollection({contextTag}): numProperties={numProperties} > 4096 (likely format mismatch).  archive[0..{hexLen}]={BitConverter.ToString(archiveForHexFallback, 0, hexLen)}");
            return;
        }
        diagnostic($"DumpPropertyCollection({contextTag}): {numProperties} properties, archiveLen={archiveForHexFallback.Length}");
        for (uint i = 0; i < numProperties; i++)
        {
            ulong wireRaw = r.ReadVarUInt64();
            ulong valueBits = r.ReadVarUInt64();

            ulong raw       = System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(wireRaw);
            uint  enumValue = (uint)(raw >> 53);
            ulong paramBits = raw & 0x1FFFFFFFFFFFFFul;
            long  intValue   = (long)((valueBits >> 1) | (valueBits << 63));
            float floatValue = BitConverter.UInt32BitsToSingle((uint)valueBits);
            // Resolve the PropertyEnum to its symbolic name when the caller passed a
            // resolver (DpsOverlayPresenter hands us PropertyEnumNames.Get for verbose dumps).
            // Without this the log shows raw "enum=283" -- you'd have to grep MHServerEmu
            // source every time a new buff shows up.  Falls back to "?" when the resolver is
            // absent or doesn't know the enum (newer server build than our table).
            string enumName = propertyEnumNameResolver != null
                ? (propertyEnumNameResolver(enumValue) ?? "?")
                : "?";
            // Include the contextTag on every prop line so a single PowerShell Select-String
            // (or the in-app Diagnostics filter) catches the header AND all per-property
            // detail with one pattern.  Without this, filtering on "BUFFDBG" hides the
            // body of the dump and the user only sees the count, which is useless for
            // identifying the buff's effects.
            diagnostic(
                $"  ({contextTag}) prop[{i}] enum={enumValue} {enumName} params=0x{paramBits:X14} " +
                $"raw=0x{raw:X16} valueBits=0x{valueBits:X} " +
                $"int={intValue} float={floatValue:0.###}");
        }
    }

    /// <summary>One rolled affix on an item, decoded from the wire's
    /// <c>AffixSpec</c> entry.  Three fields are everything the server replicates -- the
    /// actual rolled value is computed client-side by running the affix prototype's
    /// roll formula against <see cref="Seed"/>.</summary>
    public readonly struct LootAffixSpec
    {
        /// <summary>Root-enum index of the AffixPrototype (lookup via <c>AffixNames</c>).</summary>
        public uint AffixProtoEnumIndex { get; init; }
        /// <summary>Root-enum index of the affix's scope prototype.  For "+DR to Melee
        /// Powers" this points to the Melee keyword.  Zero for unscoped affixes (most pure
        /// stat affixes like +DamageRating).</summary>
        public uint ScopeProtoEnumIndex { get; init; }
        /// <summary>RNG seed used to roll the value.  We don't replicate the roll math
        /// yet (Phase 2); ship-it scoring weighs affix PRESENCE, not roll quality.</summary>
        public int  Seed { get; init; }
    }

    /// <summary>Parsed result of an item's <c>ItemSpec</c>: which item, what rarity, at
    /// what item level, and the list of rolled affixes.  Returned by
    /// <see cref="TryParseItemSpec"/>.</summary>
    public sealed class LootItemSpec
    {
        public uint ItemProtoEnumIndex   { get; init; }
        public uint RarityProtoEnumIndex { get; init; }
        public int  ItemLevel { get; init; }
        public IReadOnlyList<LootAffixSpec> AffixSpecs { get; init; } = System.Array.Empty<LootAffixSpec>();
        public int  ItemSeed { get; init; }
        /// <summary>Prototype enum index of the avatar this item is equippable by.  Same
        /// encoding as <see cref="ItemProtoEnumIndex"/> -- plain varint uint that's the
        /// root-Prototype enum index from the server's data directory.  Server-agnostic
        /// signal: doesn't depend on items.txt enum table matching the server, because the
        /// user's own avatar entity carries the same value and we can match runtime-to-
        /// runtime.  Used by the hunt-mode filter to identify "is this item for MY hero?"
        /// without needing the server's prototype-name mapping.  Zero when not set (e.g.
        /// universal items equippable by any hero).</summary>
        public uint EquippableByEnumIndex { get; init; }
    }

    /// <summary>
    /// Parse an item entity's <c>ItemSpec</c> out of its <c>EntityCreate</c> archive.  The
    /// archive contains the full <c>Item.Serialize</c> output, which is more than just the
    /// property collection -- the WorldEntity base class layers in four extra sub-collections
    /// between the properties and the ItemSpec.  All four are zero-count for ground-dropped
    /// items, but we still have to read their varint zeros to land at the right offset.
    ///
    /// <para>Full wire layout (from <c>Item.Serialize</c> &rarr; <c>WorldEntity.Serialize</c>
    /// &rarr; <c>Entity.Serialize</c>):</para>
    /// <code>
    ///   [Replication header]
    ///   [ReplicatedPropertyCollection._replicationId varint]
    ///   [PropertyCollection: 4-byte LE count + N (key,value) pairs]
    ///   [EntityTrackingContextMap: varint count + entries]      -- IsTransient block
    ///   [ConditionCollection: varint count + entries]           -- IsTransient block
    ///   [PowerCollection: varint count + records]               -- IsReplication+Proximity
    ///   [int32 ioData = 0]                                      -- IsReplication block
    ///   [ItemSpec:
    ///      PrototypeId itemProtoRef       -- varint, byte-reversed; top 11 bits = enum
    ///      PrototypeId rarityProtoRef
    ///      int32 itemLevel                -- zigzag varint
    ///      uint32 creditsAmount           -- plain varint
    ///      uint32 affixListCount          -- plain varint
    ///      for each affix:
    ///        PrototypeId affixProtoRef
    ///        PrototypeId scopeProtoRef
    ///        int32 seed
    ///      int32 itemSeed
    ///      PrototypeId equippableBy ]
    /// </code>
    ///
    /// <para><b>Origin of the bug-and-fix</b>: my v1 parser walked properties then tried to
    /// read ItemSpec immediately, missing the four sub-collections in between.  Result was
    /// 100% parse failures because the first "ItemSpec" varint was actually the
    /// trackingContextMap count -- which for ground items is 0, so the parser read 0 as the
    /// itemProtoRef wire value, then drifted into garbage downstream and tripped the
    /// affixCount > 64 sanity bound on every attempt.</para>
    ///
    /// <para>Returns <c>false</c> on any parse failure (corrupt archive, count out of
    /// sanity bound, non-zero sub-collection counts).  Non-zero sub-collection counts are
    /// surfaced as a specific failure mode -- they would mean a ground item with attached
    /// conditions or powers, which is uncommon and would require us to skip variable-length
    /// records.  For now we bail so the caller knows to fall back to a property dump.</para>
    /// </summary>
    public static bool TryParseItemSpec(byte[]? archive, out LootItemSpec spec)
        => TryParseItemSpec(archive, out spec, out _);

    /// <summary>Parse + return a failure-reason string for diagnostics.  Empty string on
    /// success; the caller surfaces failure-reason in the log so we can tell parse-fail
    /// modes apart at a glance (sub-collection non-zero vs. affix-count overflow vs.
    /// exception).</summary>
    public static bool TryParseItemSpec(byte[]? archive, out LootItemSpec spec, out string failureReason)
    {
        spec = new LootItemSpec();
        failureReason = string.Empty;
        if (archive == null || archive.Length < 10) { failureReason = "archive null or too short"; return false; }

        try
        {
            var r = new GazillionArchiveReader(archive);
            r.ReadReplicationHeader();
            // ReplicatedPropertyCollection's _replicationId varint -- same skip as
            // TryExtractStackCount and ParsePropertyCollectionAt.
            _ = r.ReadVarUInt64();
            uint numProperties = r.ReadRawUInt32LittleEndian();
            if (numProperties > 4096) { failureReason = $"numProperties={numProperties} > 4096 (parse misalignment)"; return false; }
            // Skip the property collection -- we don't need its content here, just need to
            // advance the reader past it.
            for (uint i = 0; i < numProperties; i++)
            {
                _ = r.ReadVarUInt64();  // wire-format property id
                _ = r.ReadVarUInt64();  // value bits
            }

            // Four sub-collections WorldEntity layers in before the ItemSpec.  For
            // ground-dropped items every count is 0, so each is a single 0x00 byte.  Bail
            // when any is non-zero -- handling those would mean walking variable-length
            // sub-records (Condition.Serialize, PowerCollectionRecord.Serialize), which we
            // don't need for the common case.  Report the exact non-zero count so the
            // diagnostic can show which sub-collection blocked us.
            ulong trackingMapCount = r.ReadVarUInt64();
            if (trackingMapCount != 0) { failureReason = $"trackingMapCount={trackingMapCount} != 0 (item has tracking entries)"; return false; }
            uint conditionCount = r.ReadVarUInt32();
            if (conditionCount != 0) { failureReason = $"conditionCount={conditionCount} != 0 (item has attached conditions)"; return false; }
            uint powerRecordCount = r.ReadVarUInt32();
            if (powerRecordCount != 0) { failureReason = $"powerRecordCount={powerRecordCount} != 0 (item has attached powers)"; return false; }
            // Trailing int from WorldEntity.Serialize's IsReplication block -- always 0.
            _ = r.ReadVarInt32();

            // Now positioned at the ItemSpec start.
            //
            // PrototypeId encoding gotcha: in REPLICATION mode (EntityCreate's archive type),
            // Serializer.Transfer(ref PrototypeId) writes a plain uint varint whose value
            // IS the root-Prototype enum index -- no byte reversal, no top-11-bits decode.
            // This is different from PropertyId's encoding (which DOES byte-reverse and
            // top-bit-shift), and different from PrototypeId in PERSISTENT mode (which
            // writes a 64-bit PrototypeGuid).  See MHServerEmu's
            // Serializer.Transfer(ref PrototypeId) -- the !IsPersistent branch is what
            // we're decoding here.
            uint itemProtoEnum   = r.ReadVarUInt32();
            uint rarityProtoEnum = r.ReadVarUInt32();
            int  itemLevel       = r.ReadVarInt32();
            _ = r.ReadVarInt32();   // creditsAmount (int, zigzag); ignored

            // AffixSpec list -- prefixed with a count.  The wire type is `ulong` (see
            // Serializer.Transfer<T>(ref List<T>): `ulong ioData2 = (ulong)ioData.Count`),
            // NOT uint.  For real items this is at most ~10, so varint byte-consumption is
            // the same as uint -- but reading the right type keeps us correct if a future
            // server build ever ships an item with >65k affixes.
            ulong affixCount = r.ReadVarUInt64();
            if (affixCount > 64) { failureReason = $"affixCount={affixCount} > 64 (likely drift)"; return false; }
            var affixes = new List<LootAffixSpec>((int)affixCount);
            for (ulong i = 0; i < affixCount; i++)
            {
                uint affixProto = r.ReadVarUInt32();   // same plain-varint enum-index encoding
                uint scopeProto = r.ReadVarUInt32();
                int  seed       = r.ReadVarInt32();
                affixes.Add(new LootAffixSpec
                {
                    AffixProtoEnumIndex = affixProto,
                    ScopeProtoEnumIndex = scopeProto,
                    Seed                = seed,
                });
            }

            int  itemSeed       = r.ReadVarInt32();
            uint equippableBy   = r.ReadVarUInt32();   // PrototypeId, same encoding as itemProto

            spec = new LootItemSpec
            {
                ItemProtoEnumIndex    = itemProtoEnum,
                RarityProtoEnumIndex  = rarityProtoEnum,
                ItemLevel             = itemLevel,
                AffixSpecs            = affixes,
                ItemSeed              = itemSeed,
                EquippableByEnumIndex = equippableBy,
            };
            return true;
        }
        catch (Exception ex)
        {
            // Reader walked off the end of the buffer or hit malformed bytes.  Either way
            // the parse is unrecoverable; surface the exception type so we can tell read
            // failures (truncated archive) apart from format failures.
            failureReason = $"exception {ex.GetType().Name}: {ex.Message}";
            return false;
        }
    }

    /// <summary>Decode a wire-format <c>PrototypeId</c> to its root-enum index.  The wire
    /// stores it byte-reversed so that small enum values varint-encode efficiently (see
    /// <c>Serializer.Transfer(ref PrototypeId)</c> -- it reverses raw bytes before writing).
    /// To recover the enum we reverse again, then take the top 11 bits.</summary>
    private static uint DecodePrototypeIdEnum(ulong wireRaw)
    {
        ulong raw = System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(wireRaw);
        return (uint)(raw >> 53);
    }

    /// <summary>Backward-compatible overload that drops the extra signal out-params.
    /// New callers should use the 4-arg form so they can tell currency drops from regular
    /// stackable items AND can recover the currency-type code for diagnostic logging.</summary>
    private static bool TryExtractStackCount(byte[] archive, out int stackCount)
        => TryExtractStackCount(archive, out stackCount, out _, out _);

    /// <summary>Extracts the stack count plus two diagnostic signals:
    /// <list type="bullet">
    /// <item><c>isCurrencyDrop</c> = true iff the entity carries an <c>ItemCurrency</c>
    ///       property whose params match the known Eternity Splinter code.  Used by the
    ///       splinter tracker for auto-detect on servers where the canonical splinter
    ///       prototype enum index doesn't match items.txt.</item>
    /// <item><c>currencyParams</c> = the raw params bits of the <c>ItemCurrency</c>
    ///       property (encodes the currency type), or 0 if no <c>ItemCurrency</c> was
    ///       present.  Surfaced regardless of whether the params matched the known
    ///       splinter signature, so callers can log "saw type 0xN" lines and identify
    ///       each server's splinter code empirically by correlating against in-game drops.</item>
    /// </list></summary>
    private static bool TryExtractStackCount(byte[] archive, out int stackCount, out bool isCurrencyDrop, out ulong currencyParams)
    {
        stackCount = 0;
        isCurrencyDrop = false;
        currencyParams = 0ul;
        if (archive == null || archive.Length < 5) return false;  // need at least header + count

        try
        {
            var r = new GazillionArchiveReader(archive);
            r.ReadReplicationHeader();
            // Skip the ReplicatedPropertyCollection's _replicationId varint -- Entity.Properties
            // is typed ReplicatedPropertyCollection (not bare PropertyCollection), and the
            // replication-mode override prepends an extra ulong before delegating to the
            // base property-count + pairs serialization.  Without this skip, the next 4 bytes
            // we'd read as the count are actually the tail of the replication-id varint, which
            // decodes to a huge value, fails the >4096 sanity bound, and returns "no stack count
            // found".  This was the root cause of "10 splinters shows as 1" -- the parser was
            // bailing out at the count check, falling back to 1.  See
            // MHServerEmu.Games.Properties/ReplicatedPropertyCollection.cs:80.
            _ = r.ReadVarUInt64();
            // 4-byte LE uint, NOT a varint -- see XML doc comment for rationale.
            uint numProperties = r.ReadRawUInt32LittleEndian();
            // Sanity bound: real entities have at most a few hundred properties.  Tens of
            // thousands would indicate we've mis-parsed the header / aligned the wrong way,
            // and continuing would just churn varints on garbage bytes.
            if (numProperties > 4096) return false;

            // PropertyEnum values (from MHServerEmu 1.0.1's PropertyEnum.cs):
            //   ItemCurrency = 540        -- the actual currency amount (parameterized by CurrencyRef)
            //   InventoryStackCount = 525 -- the visual stack count (always 1 for currency drops)
            const uint ItemCurrencyEnum        = 540;
            const uint InventoryStackCountEnum = 525;

            // Param bits to match for the Eternity Splinter currency type.  Confirmed by
            // empirical correlation: the user got "+12 Eternity Splinters!" in-game and
            // the diagnostic logged a currency-bearing EntityCreate with
            // currencyParams=0x10000000000000 (proto idx 13073, stackCount=12) at the
            // matching wall-clock time.  Other currency codes seen on this server:
            // 0x12000000000000 fires on Skrull terminal mob spawns (NOT splinters --
            // confirmed false positive).  If a future server uses a different code we'll
            // see it via the diagnostic line that logs every ItemCurrency entity's
            // currencyParams value.
            const ulong SplinterCurrencyParams = 0x10000000000000ul;

            int  itemCurrencyValue        = 0;
            int  inventoryStackCountValue = 0;
            bool sawSplinterCurrency      = false;

            for (uint i = 0; i < numProperties; i++)
            {
                ulong propertyIdWire = r.ReadVarUInt64();
                ulong valueBits      = r.ReadVarUInt64();

                // Undo the byte-reversal MHServerEmu applies to PropertyId on the wire (see
                // class-doc comment).  Without this, propertyIdRaw is byte-flipped garbage
                // relative to the enum-in-top-bits encoding our match expects.
                ulong propertyIdRaw = System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(propertyIdWire);
                uint  enumValue     = (uint)(propertyIdRaw >> 53);
                ulong paramBits     = propertyIdRaw & 0x1FFFFFFFFFFFFFul;

                if (enumValue != ItemCurrencyEnum && enumValue != InventoryStackCountEnum)
                    continue;

                // Integer-type decode: MathHelper.UnswizzleSignBit = (bits >> 1) | (bits << 63).
                // For positive ints (every quantity we care about) the high bit is 0 so this
                // reduces to a plain >> 1.
                long value = (long)((valueBits >> 1) | (valueBits << 63));
                if (value <= 0 || value > 1_000_000) continue;  // sanity-bound, ignore garbage

                if (enumValue == ItemCurrencyEnum)
                {
                    itemCurrencyValue = (int)value;
                    // Capture the raw params bits regardless of value -- used by callers
                    // to identify each server's splinter currency code empirically.
                    currencyParams = paramBits;
                    // Splinter signal: ItemCurrency property AND its params match the
                    // splinter-currency code.  Filtering by currency-type avoids firing on
                    // mobs whose loot tables carry credits / Odin Marks / boss currencies.
                    if (paramBits == SplinterCurrencyParams)
                        sawSplinterCurrency = true;
                }
                else
                {
                    inventoryStackCountValue = (int)value;
                }
            }

            // Prefer ItemCurrency.  This is the value the game UI shows in the
            // "+10 Eternity Splinters!" popup.  Fall back to InventoryStackCount for
            // regular stackable items (potions etc) that don't have a currency property.
            stackCount     = sawSplinterCurrency || itemCurrencyValue > 0
                ? itemCurrencyValue
                : inventoryStackCountValue;
            // Currency-drop signal: ItemCurrency property with splinter-specific params.
            // This intentionally also matches when the entity has combat properties
            // (Health/Rank/CombatLevel) -- splinters often arrive as a hint on the
            // dropping mob, not as a separate ground item.  False-positives on non-splinter
            // currencies are eliminated by the param-bits match.
            isCurrencyDrop = sawSplinterCurrency;
            return stackCount > 0;
        }
        catch
        {
            return false;
        }
    }

    private static (string, ulong) ScanAvatarPlayerName(byte[] archive)
    {
        if (archive == null || archive.Length < 20) return (string.Empty, 0);

        // Forward scan. Stop before the tail so we always have room for:
        //   strlen(1) + name(<=30) + dbId(<=10) + emptyStrMarker(1) = ~42 bytes.
        int end = archive.Length - 16;
        for (int i = 0; i < end; i++)
        {
            // Candidate strlen varint: single-byte varint in [2..30] covers every
            // player name we'll ever see (display-name length cap on the live game is
            // well under 30). Longer names would encode as a multi-byte varint; we
            // don't bother with those — if we miss them, we fall back gracefully.
            byte strlen = archive[i];
            if (strlen < 2 || strlen > 30) continue;

            int nameStart = i + 1;
            int nameEnd   = nameStart + strlen;
            if (nameEnd + 10 >= archive.Length) continue;

            // All name bytes must be printable ASCII. MH nicknames on Gazillion's live
            // game were restricted to letters/digits/underscore; we allow the full
            // printable range for safety (0x20..0x7E) at the cost of occasional false
            // positives that the subsequent dbId/terminator checks will reject.
            bool printable = true;
            for (int j = nameStart; j < nameEnd; j++)
            {
                byte b = archive[j];
                if (b < 0x20 || b > 0x7E) { printable = false; break; }
            }
            if (!printable) continue;

            // Parse the varint immediately after the string as the owner dbId. We
            // require EXACTLY 9 bytes (the natural size for the 0x2000_…-range ids the
            // server allocates) — this is the single most discriminating signal we
            // have against random property bytes matching the "printable string"
            // pattern, since a 9-byte varint ending with a 0x00 terminator byte just
            // doesn't happen by accident in property-value data.
            int vi = nameEnd;
            ulong val = 0;
            int varBytes = 0;
            while (vi < archive.Length && varBytes < 10)
            {
                val |= (ulong)(archive[vi] & 0x7F) << (varBytes * 7);
                varBytes++;
                if ((archive[vi] & 0x80) == 0) { vi++; break; }
                vi++;
            }
            if (varBytes != 9) continue;             // dbId is always 9 varint bytes
            if (val < 0x1000_0000_0000_0000UL) continue; // and always in the high range

            // Next byte must be the 0x00 that marks the trailing empty string field.
            if (vi >= archive.Length) continue;
            if (archive[vi] != 0x00) continue;

            // Everything checks out — return. We intentionally return on the FIRST
            // match: _playerName is the first RepString in the transient section of
            // Avatar.Serialize, so within the avatar blob the earliest valid match is
            // always the real one.
            string name = System.Text.Encoding.UTF8.GetString(archive, nameStart, strlen);
            return (name, val);
        }

        return (string.Empty, 0);
    }

    private void ParseRegionChange(byte[] body)
    {
        if (RegionChanged is null) return;
        try
        {
            var msg = NetMessageRegionChange.ParseFrom(body);

            // createRegionParams is optional in proto but the server populates it on every real
            // region transition — that's where the difficulty tier proto-id lives. Hub teleports
            // sometimes omit it (we just emit 0 and let the consumer keep the previous tier).
            ulong tierProtoId = 0;
            uint level = 0;
            if (msg.HasCreateRegionParams)
            {
                var p = msg.CreateRegionParams;
                if (p.HasDifficultyTierProtoId) tierProtoId = p.DifficultyTierProtoId;
                if (p.HasLevel) level = p.Level;
            }

            RegionChanged?.Invoke(this, new RegionChangedEvent
            {
                RegionId              = msg.RegionId,
                RegionPrototypeId     = msg.HasRegionPrototypeId ? msg.RegionPrototypeId : 0,
                ClearingAllInterest   = msg.ClearingAllInterest,
                DifficultyTierProtoId = tierProtoId,
                Level                 = level,
                UtcTime               = DateTime.UtcNow,
            });
            // Region transitions rotate the entity-id namespace: the avatar we've been pinning
            // will get a brand-new id in the new region, but on rare hub→hub hops the server
            // sometimes reuses the id.  Reset the sniffer-local dedupe so the very first post-
            // transition UpdateAvatarState always forwards the id to DpsMeter (which has just
            // had its own avatar set cleared by its own RegionChanged handler).
            _lastObservedLocalAvatarId = 0;
        }
        catch (Exception ex)
        {
            Diagnostic?.Invoke($"RegionChange parse failed: {ex.Message}");
        }
    }

    private void ParseDifficultyChange(byte[] body)
    {
        if (DifficultyChanged is null) return;
        try
        {
            var msg = NetMessageRegionDifficultyChange.ParseFrom(body);
            DifficultyChanged?.Invoke(this, new DifficultyChangedEvent
            {
                DifficultyIndex = msg.DifficultyIndex,
                UtcTime         = DateTime.UtcNow,
            });
        }
        catch (Exception ex)
        {
            Diagnostic?.Invoke($"DifficultyChange parse failed: {ex.Message}");
        }
    }

    private void ParseRegionQueueRequest(byte[] body)
    {
        if (RegionQueueRequested is null) return;
        try
        {
            var msg = NetMessageRegionRequestQueueCommandClient.ParseFrom(body);
            RegionQueueRequested?.Invoke(this, new RegionQueueRequestedEvent
            {
                RegionPrototypeId     = msg.RegionProtoId,
                DifficultyTierProtoId = msg.DifficultyTierProtoId,
                Command               = (uint)msg.Command,
                UtcTime               = DateTime.UtcNow,
            });
        }
        catch (Exception ex)
        {
            Diagnostic?.Invoke($"RegionQueueRequest parse failed: {ex.Message}");
        }
    }

    private void ParseLoadingScreen(byte[] body, bool opening)
    {
        if (LoadingScreenChanged is null) return;
        try
        {
            ulong proto = 0;
            if (opening)
            {
                var msg = NetMessageQueueLoadingScreen.ParseFrom(body);
                proto = msg.HasRegionPrototypeId ? msg.RegionPrototypeId : 0;
            }
            // Dequeue message has no fields per the proto definition.

            LoadingScreenChanged?.Invoke(this, new LoadingScreenEvent
            {
                Opening           = opening,
                RegionPrototypeId = proto,
                UtcTime           = DateTime.UtcNow,
            });
        }
        catch (Exception ex)
        {
            Diagnostic?.Invoke($"LoadingScreen parse failed: {ex.Message}");
        }
    }

    // -------------- protobuf-id -> name lookup --------------

    private sealed class Lookup
    {
        private readonly Dictionary<uint, string> _names;
        private Lookup(Dictionary<uint, string> names) { _names = names; }
        public string? NameOf(uint id) => _names.TryGetValue(id, out var n) ? n : null;

        // Build via the non-generic Enum API and read the underlying integer through GetValue() so
        // it works regardless of whether the generated enum's underlying type is int or uint.
        public static Lookup Build(Type enumType)
        {
            if (!enumType.IsEnum) throw new ArgumentException($"{enumType.FullName} is not an enum");
            var d = new Dictionary<uint, string>();
            var arr = Enum.GetValues(enumType);
            for (int i = 0; i < arr.Length; i++)
            {
                object? v = arr.GetValue(i);
                if (v is null) continue;
                ulong raw = Convert.ToUInt64(v, System.Globalization.CultureInfo.InvariantCulture);
                if (raw > uint.MaxValue) continue;
                d[(uint)raw] = v.ToString() ?? $"id={raw}";
            }
            return new Lookup(d);
        }
    }
}
