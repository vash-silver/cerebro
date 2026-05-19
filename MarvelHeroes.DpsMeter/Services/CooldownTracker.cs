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
    /// than (or in addition to) putting the power on cooldown.  Populated via
    /// multi-cast decrement detection -- see <see cref="_pendingChargeCandidates"/>
    /// for the safer two-cast learning model that replaced the v2.10's
    /// single-event approach (which false-positived on incidental counters).</summary>
    private readonly Dictionary<(uint Enum, ulong ParamBits), uint> _chargeSignatures = new();

    /// <summary>Pending charge-candidate buffer: (enum, paramBits) we've observed
    /// once with a small-int value in a learn window but haven't confirmed as a
    /// real charge signature yet.  Promoted to <see cref="_chargeSignatures"/>
    /// when the SAME (enum, paramBits) shows up in a later cast's learn window
    /// with a STRICTLY LOWER value -- charges only ever decrement on cast, so
    /// two consecutive decrements is high-confidence "this is the charge
    /// counter".  Incidental small-int props that fire alongside a cooldown
    /// (Sig's enum 720 counting "something that decrements but isn't charges")
    /// get DISCARDED whenever we learn a cooldown sig for the same proto in the
    /// same window -- the cooldown is the authoritative ready-state gate, so
    /// any small-int counter sharing the window is by definition not the
    /// charges signal.</summary>
    private readonly Dictionary<(uint Enum, ulong ParamBits), PendingChargeCandidate> _pendingChargeCandidates = new();

    /// <summary>One un-confirmed small-int observation.  We keep just the proto
    /// + value + when -- enough to confirm a decrement on the next observation
    /// and prune stale entries on owner change.</summary>
    private sealed class PendingChargeCandidate
    {
        public uint ProtoId;
        public long FirstValue;
        public DateTime FirstSeenUtc;
    }

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
                _pendingChargeCandidates.Clear();
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

        // ── Branch 1: Cooldown duration candidate (value in [50ms, 10min]) ──
        // Cooldown sigs are learned eagerly on first observation -- the value
        // range is narrow enough that false positives are vanishingly unlikely
        // (no incidental property falls between 50ms and 10 minutes as a raw
        // int64).  Learning a cooldown sig ALSO discards any pending charge
        // candidates we've stashed for this same proto: a non-charged ability
        // can have an incidental small-int counter that decrements alongside
        // the cooldown property (the old enum 720 / Sig regression).  By the
        // time we see the cooldown property we know this isn't really a
        // charged ability -- drop the candidates so they can't false-promote
        // on the user's next cast.
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
                // Drop pending charge candidates for THIS proto -- they're
                // incidental counters that just happened to fire during the
                // cooldown's learn window.
                DiscardPendingChargeCandidatesForLocked(matchedProto);
            }
            Diagnostic?.Invoke(
                $"CooldownTracker: LEARNED COOLDOWN sig (enum={e.PropertyEnum}, params=0x{e.ParamBits:X}) "
              + $"-> power #{matchedProto}  cooldown={rotated}ms");
            HandleCooldownSignature(e, matchedProto, rotated);
            return;
        }

        // ── Branch 2: Charge-count candidate (small positive int [1, 30]) ──
        // SAFER multi-cast learning: we don't promote on first sighting.
        //   * If proto already has a cooldown sig: SKIP entirely.  The cooldown
        //     is the ready-state gate; any small-int decrement is incidental.
        //   * If we've stashed a candidate for this (enum, paramBits) before:
        //     compare values.  Strict decrement -> promote (charges only
        //     decrement on cast, so two consecutive decrements is the
        //     unambiguous signal).  Equal-or-greater -> drop the candidate
        //     (false positive; this was probably just a re-broadcast of an
        //     unrelated property).
        //   * Otherwise: stash for next time.
        if (rotated >= 1 && rotated <= ChargesMaxValue)
        {
            bool protoHasCooldownSig;
            lock (_sync) protoHasCooldownSig = HasCooldownSigForLocked(matchedProto);
            if (protoHasCooldownSig)
            {
                // Cooldown gate; small-int is incidental. Reject silently.
                return;
            }

            PendingChargeCandidate? existing;
            lock (_sync) _pendingChargeCandidates.TryGetValue((e.PropertyEnum, e.ParamBits), out existing);

            if (existing == null)
            {
                lock (_sync)
                {
                    _pendingChargeCandidates[(e.PropertyEnum, e.ParamBits)] = new PendingChargeCandidate
                    {
                        ProtoId      = matchedProto,
                        FirstValue   = rotated,
                        FirstSeenUtc = e.UtcTime,
                    };
                    // Same self-replicationId lock-in as cooldown branch.  If
                    // this candidate is ever promoted, we've already proven the
                    // replicationId belongs to us.
                    if (_selfReplicationId is null) _selfReplicationId = e.ReplicationId;
                }
                Diagnostic?.Invoke(
                    $"CooldownTracker: stashed CHARGE candidate (enum={e.PropertyEnum}, params=0x{e.ParamBits:X}) "
                  + $"-> power #{matchedProto}  value={rotated} (awaiting next cast for confirmation)");
                return;
            }

            // We already have a candidate.  Different proto?  That shouldn't
            // happen on the local avatar -- but if it does, prefer the most
            // recent one (the user changed avatars / hero).
            if (existing.ProtoId != matchedProto)
            {
                lock (_sync)
                {
                    _pendingChargeCandidates[(e.PropertyEnum, e.ParamBits)] = new PendingChargeCandidate
                    {
                        ProtoId = matchedProto, FirstValue = rotated, FirstSeenUtc = e.UtcTime,
                    };
                }
                return;
            }

            if (rotated < existing.FirstValue)
            {
                // Confirmed decrement: promote to charge sig.
                lock (_sync)
                {
                    _chargeSignatures[(e.PropertyEnum, e.ParamBits)] = matchedProto;
                    _pendingChargeCandidates.Remove((e.PropertyEnum, e.ParamBits));
                }
                Diagnostic?.Invoke(
                    $"CooldownTracker: LEARNED CHARGE sig (enum={e.PropertyEnum}, params=0x{e.ParamBits:X}) "
                  + $"-> power #{matchedProto}  prev={existing.FirstValue}, now={rotated}");
                // Also seed ChargesMax with the higher value so the UI knows the
                // ability is multi-charge (renders the badge).  We take +1 because
                // the FIRST stashed value was AFTER the first cast (charges had
                // already decremented once); the true max is at least one above.
                lock (_sync)
                {
                    if (_state.TryGetValue(matchedProto, out var s))
                    {
                        int seenMax = (int)Math.Max(existing.FirstValue + 1, rotated);
                        if (seenMax > s.ChargesMax) s.ChargesMax = seenMax;
                        s.ChargesAvailable = (int)Math.Max(0, Math.Min(ChargesMaxValue, rotated));
                    }
                }
                PowerActivated?.Invoke(matchedProto);
                return;
            }

            // Non-decrement: drop the candidate (false positive).
            lock (_sync) _pendingChargeCandidates.Remove((e.PropertyEnum, e.ParamBits));
            Diagnostic?.Invoke(
                $"CooldownTracker: dropped CHARGE candidate (enum={e.PropertyEnum}, params=0x{e.ParamBits:X}) "
              + $"-> power #{matchedProto}  prev={existing.FirstValue}, now={rotated} (not a decrement)");
            return;
        }

        // ── Branch 3: Out-of-range value.  Log once per signature for triage. ──
        if (_loggedRejections.TryAdd((e.PropertyEnum, e.ParamBits), 0))
        {
            Diagnostic?.Invoke(
                $"CooldownTracker: rejected candidate (enum={e.PropertyEnum}, params=0x{e.ParamBits:X}, "
              + $"value=0x{e.ValueBits:X}, int64Rot={rotated}) for power #{matchedProto} -- "
              + $"outside cooldown[{CooldownMinMs},{CooldownMaxMs}]ms and charges[1,{ChargesMaxValue}] ranges");
        }
    }

    /// <summary>Returns true if any (enum, paramBits) -> protoId entry in the
    /// cooldown sig map points at <paramref name="protoId"/>.  Caller must hold
    /// the <see cref="_sync"/> lock.  Linear in the sig map size; the map is
    /// small (one entry per cooldown-having power the user has cast, max ~30
    /// for a heavy session).</summary>
    private bool HasCooldownSigForLocked(uint protoId)
    {
        foreach (var v in _signatures.Values)
            if (v == protoId) return true;
        return false;
    }

    /// <summary>Drop pending charge candidates whose proto matches the supplied
    /// one.  Used after we learn a cooldown sig: a power with a cooldown gate
    /// is non-charged, so any small-int property in the same window is
    /// incidental and shouldn't be promoted on the user's next cast.  Caller
    /// must hold the <see cref="_sync"/> lock.</summary>
    private void DiscardPendingChargeCandidatesForLocked(uint protoId)
    {
        if (_pendingChargeCandidates.Count == 0) return;
        var stale = new List<(uint, ulong)>();
        foreach (var kv in _pendingChargeCandidates)
            if (kv.Value.ProtoId == protoId) stale.Add(kv.Key);
        foreach (var k in stale) _pendingChargeCandidates.Remove(k);
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
            // Keep _signatures, _chargeSignatures, _selfReplicationId -- those
            // represent wire-level knowledge that's still valid even after a UI-
            // only history wipe.  Pending charge candidates DO get dropped
            // because they're orphaned without a state entry to attach to (the
            // user explicitly asked us to forget activations).
            _pendingChargeCandidates.Clear();
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

    /// <summary>Logical "this power can be cast right now" check.  Two flavours:
    /// <list type="bullet">
    ///   <item><b>Charged ability</b> (<see cref="ChargesMax"/> &gt; 0): charges
    ///         are the source of truth.  You can cast any time at least one
    ///         charge is available, regardless of cooldown state -- the cooldown
    ///         IS the regen timer for the NEXT charge, not a gate on casting.</item>
    ///   <item><b>Non-charged ability</b>: cooldown is the gate.  Ready when
    ///         not on cooldown, OR the cooldown has elapsed without us having
    ///         received the RemoveProperty yet.</item>
    /// </list>
    /// <para>The charge-first priority is safe because charge signatures are
    /// only learned via multi-cast decrement detection (in
    /// <c>CooldownTracker</c>'s pending-candidate flow), which won't false-
    /// positive on incidental small-int counters that fire alongside cooldown
    /// properties.</para></summary>
    public bool IsReady(DateTime nowUtc)
    {
        if (ChargesMax > 0) return ChargesAvailable > 0;
        if (!OnCooldown) return true;
        if (CooldownDurationMs <= 0) return true;
        return (nowUtc - CooldownStartUtc).TotalMilliseconds >= CooldownDurationMs;
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
