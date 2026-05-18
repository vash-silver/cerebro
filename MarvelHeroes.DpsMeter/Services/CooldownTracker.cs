using System;
using System.Collections.Generic;
using MarvelHeroesComporator.NetworkSniffer;

namespace MarvelHeroes.DpsMeter.Services;

/// <summary>
/// Live cooldown state for the local player's powers, driven by the server's
/// <c>NetMessageSetProperty</c> / <c>NetMessageRemoveProperty</c> deltas.  CDR
/// (cooldown reduction) procs are handled automatically because the server pushes
/// updated duration deltas when CDR fires.
///
/// <para><b>The hard problem:</b> Marvel Heroes uses MULTIPLE PropertyEnum values
/// for cooldowns depending on the power's category (regular vs charged vs
/// signature, etc.).  In a sample capture against Tahiti's 2.16 build we saw
/// enums 1744 (with paramBits ending <c>FA0300000000</c>) AND 1750 (with paramBits
/// ending <c>540000000000</c>) BOTH carrying cooldown data for different powers.
/// The static PropertyEnumNames table shipped with Cerebro is from an older
/// version where these mapped to different indices, so we can't just hardcode an
/// enum match.</para>
///
/// <para><b>Solution: empirical signature learning.</b>  When the local client
/// sends <c>TryActivatePower(P)</c> we open a 500ms watch window.  The first
/// <c>SetProperty</c> event in that window whose:</para>
/// <list type="bullet">
///   <item>Decoded int64-rotated value falls in <c>[50, 600000]</c> ms (plausible
///         cooldown range -- nothing in the game has a cooldown shorter than 50ms
///         or longer than 10 minutes)</item>
///   <item>ParamBits is non-zero (per-power property, not a global)</item>
/// </list>
/// <para>...is recorded as the cooldown signature for that power:
/// <c>signatures[(propertyEnum, paramBits)] = P</c>.  Future SetProperty events
/// matching that signature update the cooldown for P directly -- this covers CDR
/// procs because the server reuses the same (enum, paramBits) key with a new value.</para>
///
/// <para><b>Self-avatar discovery:</b> Property deltas arrive keyed by a
/// per-entity property-collection <c>replicationId</c>, not an entity id.  We
/// auto-discover ours by the same correlation: the first cooldown delta after a
/// local cast tells us which replicationId is ours, and we filter all subsequent
/// deltas against it.  Mid-session avatar swap (hero change) re-detects via a
/// fresh round of correlation after <see cref="SelfOwnerId"/> is reset.</para>
///
/// <para><b>Threading:</b> Sniffer events fire on the capture thread.  All state
/// mutations are guarded by <see cref="_sync"/>; public readers take the same
/// lock so they're safe to call from the UI dispatcher.</para>
/// </summary>
public sealed class CooldownTracker : IDisposable
{
    /// <summary>Watch-window length after a local TryActivatePower during which
    /// SetProperty events are eligible to be "learned" as cooldown signatures.
    /// 500 ms is generous -- typical server response is sub-100ms; we add headroom
    /// for laggy connections without admitting unrelated deltas.</summary>
    private static readonly TimeSpan SignatureLearnWindow = TimeSpan.FromMilliseconds(500);

    /// <summary>Minimum value (in ms) for a SetProperty value to be considered a
    /// cooldown candidate.  Anything shorter than 50ms isn't a real cooldown
    /// (instant-cast powers don't go on cooldown at all; even animation-locked
    /// abilities are 100ms+).</summary>
    private const long CooldownMinMs = 50;
    /// <summary>Maximum value for a cooldown candidate.  Sig powers are the longest
    /// in MH at ~5 minutes; 10 minutes is comfortable upper-bound that excludes
    /// "this is actually a server timestamp" candidates (which are in the trillions).</summary>
    private const long CooldownMaxMs = 600_000;

    /// <summary>Plausible range for a "charge count" property value.  Charged
    /// abilities (Nightcrawler Teleport, Bamf Bomb, etc.) carry an integer count
    /// in this range; we learn the (enum, paramBits) -> proto mapping the same
    /// way as cooldown durations.  Upper bound of 30 covers every charged ability
    /// in MH (most are 1-3 charges; talent stacks can go higher).</summary>
    private const long ChargesMaxValue = 30;

    private readonly MhMissionSniffer _sniffer;
    private readonly object _sync = new();

    /// <summary>Per-power cooldown state.  Keyed by root prototype enum index.</summary>
    private readonly Dictionary<uint, PowerCooldownState> _state = new();

    /// <summary>Learned cooldown-duration signatures: (enum, paramBits) -> proto.
    /// Populated lazily as the local player casts powers; each (enum, paramBits) is
    /// registered exactly once for the first power that triggered it.  Subsequent
    /// SetProperty events with the same key update THAT power's cooldown,
    /// including CDR procs.</summary>
    private readonly Dictionary<(uint Enum, ulong ParamBits), uint> _signatures = new();

    /// <summary>Learned charge-count signatures.  Charged abilities (Nightcrawler
    /// Teleport, Bamf Bomb, etc.) decrement an integer count on each cast rather
    /// than (or in addition to) putting the power on cooldown.  Same correlation
    /// model as <see cref="_signatures"/>: the first small-int (&lt;= 30) property
    /// delta in a learn window gets bound to the just-cast power, and subsequent
    /// matching deltas update <c>state.ChargesAvailable</c>.</summary>
    private readonly Dictionary<(uint Enum, ulong ParamBits), uint> _chargeSignatures = new();

    /// <summary>Pending cast: the most recent TryActivatePower observation, used
    /// during the SignatureLearnWindow to assign newly-observed cooldown deltas
    /// to a specific power.  Null when no cast is "in flight".</summary>
    private (uint Proto, DateTime CastedAt)? _pendingCast;

    /// <summary>Sticky lock for the local avatar's property-collection replicationId.
    /// Set on the first cooldown delta we successfully correlate with a local cast;
    /// reset to null on owner change.</summary>
    private ulong? _selfReplicationId;

    private ulong _selfOwnerId;

    public ulong SelfOwnerId
    {
        get { lock (_sync) return _selfOwnerId; }
        set
        {
            ulong prev;
            int cleared;
            lock (_sync)
            {
                prev = _selfOwnerId;
                if (prev == value) return;
                cleared = _state.Count;
                _state.Clear();
                _signatures.Clear();
                _chargeSignatures.Clear();
                _selfReplicationId = null;
                _pendingCast = null;
                _selfOwnerId = value;
            }
            Diagnostic?.Invoke($"CooldownTracker: SelfOwnerId {prev} -> {value} (cleared {cleared} cooldowns + learned signatures + replicationId)");
            PowerActivated?.Invoke(0);
        }
    }

    public Action<string>? Diagnostic { get; set; }
    public event Action<uint>? PowerActivated;

    public CooldownTracker(MhMissionSniffer sniffer)
    {
        _sniffer = sniffer ?? throw new ArgumentNullException(nameof(sniffer));
        _sniffer.LocalPowerActivated += OnLocalPowerActivated;
        _sniffer.PropertyChanged     += OnPropertyChanged;
    }

    public void Dispose()
    {
        _sniffer.LocalPowerActivated -= OnLocalPowerActivated;
        _sniffer.PropertyChanged     -= OnPropertyChanged;
    }

    private void OnLocalPowerActivated(object? sender, LocalPowerActivatedEvent e)
    {
        ulong owner;
        lock (_sync) owner = _selfOwnerId;
        if (owner == 0 || e.LocalAvatarEntityId != owner) return;

        uint protoId = (uint)(e.PowerPrototypeId & 0xFFFFFFFFu);
        if (protoId == 0) return;

        lock (_sync)
        {
            // Per-power state: keep activation history for the tab's discovery list.
            if (!_state.TryGetValue(protoId, out var s))
            {
                s = new PowerCooldownState { ProtoId = protoId, FirstSeenUtc = e.UtcTime };
                _state[protoId] = s;
            }
            s.LastFiredUtc = e.UtcTime;
            s.TotalFires++;

            // Open the learning window for this cast.
            _pendingCast = (protoId, e.UtcTime);
        }
        Diagnostic?.Invoke($"CooldownTracker: TryActivatePower #{protoId} (owner=0x{owner:X}) -- learn window open");
        PowerActivated?.Invoke(protoId);
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEvent e)
    {
        ulong  owner;
        ulong? selfRep;
        (uint Proto, DateTime CastedAt)? pending;
        bool   wasCooldownSig;
        bool   wasChargeSig;
        uint   cooldownProto = 0, chargeProto = 0;
        lock (_sync)
        {
            owner = _selfOwnerId;
            selfRep = _selfReplicationId;
            pending = _pendingCast;
            wasCooldownSig = _signatures.TryGetValue((e.PropertyEnum, e.ParamBits), out cooldownProto);
            wasChargeSig   = _chargeSignatures.TryGetValue((e.PropertyEnum, e.ParamBits), out chargeProto);
        }
        if (owner == 0) return;

        long rotated = (long)((e.ValueBits >> 1) | (e.ValueBits << 63));

        // Path A: Known signature -- update state directly.  The two maps are
        // disjoint (we register a (enum, paramBits) into exactly one), so at most
        // one of these is true.
        if (wasCooldownSig) { HandleCooldownSignature(e, cooldownProto, rotated); return; }
        if (wasChargeSig)   { HandleChargeSignature(e, chargeProto, rotated);     return; }

        // Path B: Unknown signature.  Try to learn it from a recent local cast.
        if (e.Removed) return;
        if (selfRep.HasValue && e.ReplicationId != selfRep.Value) return;
        if (!pending.HasValue) return;
        if ((e.UtcTime - pending.Value.CastedAt) > SignatureLearnWindow)
        {
            lock (_sync) _pendingCast = null;
            return;
        }
        if (e.ParamBits == 0) return;  // global property, not per-power

        uint matchedProto = pending.Value.Proto;

        // Cooldown duration candidate?  Plausible range [50, 600000] ms.
        // We ONLY learn cooldown signatures from the learn window.  Charges are
        // tricky to disambiguate from random small-int counters that happen to
        // fire alongside (enum 720 was a "PlayerScalingHealthPctBonus"-style
        // property in the old enum table; in MH 2.16 it's still some small-int
        // value that decrements alongside a cast but isn't actually charges).
        // We'll add charge detection in a separate path that requires
        // multi-cast confirmation -- safer than eagerly grabbing any small int.
        if (rotated >= CooldownMinMs && rotated <= CooldownMaxMs)
        {
            lock (_sync)
            {
                _signatures[(e.PropertyEnum, e.ParamBits)] = matchedProto;
                if (_selfReplicationId is null)
                {
                    _selfReplicationId = e.ReplicationId;
                    Diagnostic?.Invoke($"CooldownTracker: locked self replicationId={e.ReplicationId} via cooldown correlation");
                }
            }
            Diagnostic?.Invoke(
                $"CooldownTracker: LEARNED COOLDOWN sig (enum={e.PropertyEnum}, params=0x{e.ParamBits:X}) "
              + $"-> power #{matchedProto}  cooldown={rotated}ms");
            HandleCooldownSignature(e, matchedProto, rotated);
            return;
        }

        // Otherwise: log a rejection so we can see what's being skipped.  Capped
        // at one log per unique (enum, paramBits) so a noisy session doesn't
        // fill the log.
        if (_loggedRejections.TryAdd((e.PropertyEnum, e.ParamBits), 0))
        {
            Diagnostic?.Invoke(
                $"CooldownTracker: rejected candidate (enum={e.PropertyEnum}, params=0x{e.ParamBits:X}, "
              + $"value=0x{e.ValueBits:X}, int64Rot={rotated}) for power #{matchedProto} -- "
              + $"value outside cooldown[{CooldownMinMs},{CooldownMaxMs}] ms range");
        }
    }

    /// <summary>Bounded set of (enum, paramBits) tuples we've already logged as
    /// "rejected" -- so the diagnostic doesn't repeat for every CDR proc /
    /// peer-update / etc.</summary>
    private readonly System.Collections.Concurrent.ConcurrentDictionary<(uint, ulong), byte> _loggedRejections = new();

    private void HandleCooldownSignature(PropertyChangedEvent e, uint protoId, long rotated)
    {
        bool isRemove = e.Removed || rotated <= 0;
        lock (_sync)
        {
            if (!_state.TryGetValue(protoId, out var s))
            {
                s = new PowerCooldownState { ProtoId = protoId, FirstSeenUtc = e.UtcTime };
                _state[protoId] = s;
            }

            if (isRemove)
            {
                s.CooldownStartUtc   = DateTime.MinValue;
                s.CooldownDurationMs = 0;
                s.OnCooldown         = false;
            }
            else if (rotated > CooldownMaxMs)
            {
                // Looks like a server timestamp (same enum, different paramBits
                // sub-key carries the start time).  Ignore -- wall-clock is our start.
                return;
            }
            else if (!s.OnCooldown
                     || rotated > s.CooldownDurationMs
                     || (e.UtcTime - s.CooldownStartUtc).TotalMilliseconds >= s.CooldownDurationMs)
            {
                s.CooldownStartUtc   = e.UtcTime;
                s.CooldownDurationMs = rotated;
                s.OnCooldown         = true;
            }
            else
            {
                // CDR fired -- shorten the duration, keep the start.
                s.CooldownDurationMs = rotated;
            }
        }
        PowerActivated?.Invoke(protoId);
    }

    private void HandleChargeSignature(PropertyChangedEvent e, uint protoId, long rotated)
    {
        lock (_sync)
        {
            if (!_state.TryGetValue(protoId, out var s))
            {
                s = new PowerCooldownState { ProtoId = protoId, FirstSeenUtc = e.UtcTime };
                _state[protoId] = s;
            }
            // RemoveProperty for the charge key (or value 0) means "no charges
            // available right now" -- the power is on cooldown for at least one
            // charge to regenerate.  Negative values are nonsense; clamp.
            int charges = e.Removed ? 0 : (int)Math.Max(0, Math.Min(ChargesMaxValue, rotated));
            s.ChargesAvailable = charges;
            // Track max-charges (highest observed value).  Lets the UI show "x2/3"
            // style if we ever want it; for v1 we just need "is there at least one".
            if (charges > s.ChargesMax) s.ChargesMax = charges;
        }
        PowerActivated?.Invoke(protoId);
    }

    public IReadOnlyList<PowerCooldownState> GetRecentPowers()
    {
        lock (_sync)
        {
            var copy = new PowerCooldownState[_state.Count];
            int i = 0;
            foreach (var s in _state.Values) copy[i++] = s.Clone();
            Array.Sort(copy, (a, b) => b.LastFiredUtc.CompareTo(a.LastFiredUtc));
            return copy;
        }
    }

    public PowerCooldownState? TryGetState(uint protoId)
    {
        lock (_sync)
        {
            if (_state.TryGetValue(protoId, out var s)) return s.Clone();
            return null;
        }
    }

    public void ClearRecentHistory()
    {
        int cleared;
        lock (_sync)
        {
            cleared = _state.Count;
            _state.Clear();
            // Keep _signatures and _selfReplicationId -- those represent wire-level
            // knowledge that's still valid even after a UI-only history wipe.
        }
        Diagnostic?.Invoke($"CooldownTracker: cleared {cleared} recent powers");
        PowerActivated?.Invoke(0);
    }
}

public sealed class PowerCooldownState
{
    public uint ProtoId { get; set; }
    public DateTime FirstSeenUtc { get; set; }
    public DateTime LastFiredUtc { get; set; }
    public int TotalFires { get; set; }
    public DateTime CooldownStartUtc { get; set; }
    public double CooldownDurationMs { get; set; }
    public bool OnCooldown { get; set; }

    /// <summary>Most recent server-pushed charge count for this power.  <c>0</c>
    /// means "no charges available right now" -- the icon should render dimmed.
    /// For non-charged abilities the value stays at <see cref="ChargesMax"/> = 0
    /// and the renderer falls back to checking <see cref="OnCooldown"/> alone.</summary>
    public int ChargesAvailable { get; set; }
    /// <summary>Highest charge value we've observed for this power -- our best
    /// estimate of the cap (e.g. 3 for Nightcrawler Teleport).  Drives the
    /// charge-count badge on the overlay chip.  Zero means "not a charged
    /// ability as far as we know".</summary>
    public int ChargesMax { get; set; }

    /// <summary>Logical "this power can be cast right now" check.  Cooldown is
    /// the authoritative source of truth: a power is ready when it's not on
    /// cooldown (or the cooldown has elapsed).  Charges (when properly learned)
    /// add an additional gate -- a charged ability with charges = 0 is unusable
    /// even if not currently on cooldown -- but charge tracking is currently
    /// disabled (we don't reliably distinguish real charges from coincidental
    /// counters yet), so this collapses to a pure cooldown check.</summary>
    public bool IsReady(DateTime nowUtc)
    {
        if (OnCooldown && CooldownDurationMs > 0)
        {
            return (nowUtc - CooldownStartUtc).TotalMilliseconds >= CooldownDurationMs;
        }
        if (ChargesMax > 0) return ChargesAvailable > 0;
        return true;
    }

    public PowerCooldownState Clone() => new()
    {
        ProtoId            = ProtoId,
        FirstSeenUtc       = FirstSeenUtc,
        LastFiredUtc       = LastFiredUtc,
        TotalFires         = TotalFires,
        CooldownStartUtc   = CooldownStartUtc,
        CooldownDurationMs = CooldownDurationMs,
        OnCooldown         = OnCooldown,
        ChargesAvailable   = ChargesAvailable,
        ChargesMax         = ChargesMax,
    };
}
