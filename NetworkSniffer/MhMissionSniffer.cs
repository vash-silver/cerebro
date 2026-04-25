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
    public event EventHandler<CommunityMemberUpdatedEvent>? CommunityMemberUpdated;

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
        if (LocalAvatarObserved is null) return;
        try
        {
            var msg = NetMessageTryActivatePower.ParseFrom(body);
            if (msg.HasIdUserEntity) EmitLocalAvatarObserved(msg.IdUserEntity, "TryActivatePower");
        }
        catch (Exception ex)
        {
            Diagnostic?.Invoke($"TryActivatePower parse failed: {ex.Message}");
        }
    }

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
        try
        {
            uint fieldFlags    = (uint)r.ReadVarUInt64();
            fieldFlagsDbg      = fieldFlags;
            uint locoFieldFlags = (uint)r.ReadVarUInt64();
            _ = locoFieldFlags;

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

            EntityCreated?.Invoke(this, new EntityCreatedEvent
            {
                EntityId           = entityId,
                PrototypeEnumIndex = protoIdx,
                DatabaseUniqueId   = dbId,
                IsAvatar           = isAvatar,
                PlayerName         = avatarName,
                OwnerPlayerDbId    = ownerDbId,
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
