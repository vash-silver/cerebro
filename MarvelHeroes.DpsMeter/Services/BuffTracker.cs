using System;
using System.Collections.Generic;
using MarvelHeroesComporator.NetworkSniffer;

namespace MarvelHeroes.DpsMeter.Services;

/// <summary>
/// Live state-of-the-world for buffs / debuffs (the server calls them "Conditions") on the
/// local avatar.  Subscribes to <see cref="MhMissionSniffer.ConditionAdded"/> and
/// <see cref="MhMissionSniffer.ConditionRemoved"/>, filters to events targeting the local
/// self-owner avatar, and maintains a dictionary keyed by the server's per-owner condition
/// slot id so the UI can render the active set as a chip strip + countdown.
///
/// <para>What we deliberately DON'T do at the data layer:</para>
/// <list type="bullet">
///   <item>No filtering on "interesting" buffs.  We track every condition the server applies
///         to us (including the dozen-or-so always-on passive auras from costumes / synergies
///         that have CharacterLevel/CombatLevel/PowerRank but no visible effect).  The UI
///         applies its own filter to surface only the timed / damage-bonus buffs prominently
///         -- separating data and presentation keeps the tracker reusable for future features
///         like "DPS during Empowered" splits or buff-uptime stats.</item>
///   <item>No timer-based eviction.  Buffs end via an explicit
///         <see cref="MhMissionSniffer.ConditionRemoved"/> event from the server; relying on
///         our local clock would drift and we'd evict early.  The <see cref="ActiveBuff.ExpiresUtc"/>
///         is computed for UI countdown display only -- the tracker still waits for the wire
///         event before removing the entry.</item>
/// </list>
///
/// <para><b>Self-owner identification:</b> the host (<c>DpsOverlayPresenter</c>) sets
/// <see cref="SelfOwnerId"/> from the DPS meter's <c>LikelySelfOwnerId</c> property.  When
/// the owner id changes (zone change, hero swap, reconnect), the tracker drops all currently-
/// tracked buffs -- they belonged to the previous avatar.  Set to 0 to disable tracking
/// entirely (no events will be admitted).</para>
///
/// <para><b>Threading:</b> <see cref="OnConditionAdded"/> and <see cref="OnConditionRemoved"/>
/// fire on the sniffer's capture thread.  All state mutations are guarded by
/// <see cref="_sync"/>.  Public read-only accessors (<see cref="GetActiveBuffs"/>,
/// <see cref="ActiveCount"/>) take the same lock so they're safe to call from the UI
/// dispatcher's decay-tick.  The <see cref="BuffChanged"/> event fires on the sniffer thread
/// -- UI subscribers should marshal to their dispatcher before touching WPF.</para>
/// </summary>
public sealed class BuffTracker : IDisposable
{
    private readonly MhMissionSniffer _sniffer;
    private readonly object _sync = new();

    /// <summary>Keyed by the server's conditionId -- which is unique per OWNER, so a single
    /// dictionary is enough as long as we only ever store entries for one owner at a time
    /// (the local self avatar).  When <see cref="SelfOwnerId"/> changes we wipe and start
    /// over.  Each entry is an <see cref="ActiveBuff"/> snapshot of the data we extracted
    /// from <see cref="ConditionAddedEvent"/> at apply time.</summary>
    private readonly Dictionary<ulong, ActiveBuff> _active = new();

    private ulong _selfOwnerId;

    /// <summary>Local self avatar entity id, set by the host as the DPS meter learns it.
    /// Setting to a NEW value clears any tracked buffs from the previous owner -- those
    /// belonged to a different avatar that we're no longer responsible for (typical cause:
    /// zone change, hero swap, reconnect).  Set to 0 to pause tracking entirely.</summary>
    public ulong SelfOwnerId
    {
        get { lock (_sync) return _selfOwnerId; }
        set
        {
            ActiveBuff[] dropped = Array.Empty<ActiveBuff>();
            ulong prev;
            lock (_sync)
            {
                prev = _selfOwnerId;
                if (prev == value) return;
                if (_active.Count > 0)
                {
                    dropped = new ActiveBuff[_active.Count];
                    _active.Values.CopyTo(dropped, 0);
                    _active.Clear();
                }
                _selfOwnerId = value;
            }
            Diagnostic?.Invoke($"BuffTracker: SelfOwnerId {prev} -> {value} ({dropped.Length} buffs cleared)");
            foreach (var b in dropped)
                BuffChanged?.Invoke(b, /*added*/ false);
        }
    }

    /// <summary>Best-effort log sink, wired by the host so each add/remove gets a one-line
    /// summary in the diagnostic log.  Always-on (cheap one-liner); the heavy property-bag
    /// dump from the discovery phase lives in <c>DpsOverlayPresenter</c> behind the
    /// VerboseDiagnostics gate.</summary>
    public Action<string>? Diagnostic { get; set; }

    /// <summary>Fires after a buff is added or removed (post-mutation of the internal
    /// dictionary), so subscribers can re-snapshot via <see cref="GetActiveBuffs"/>.  The
    /// <c>bool</c> argument is <c>true</c> for add, <c>false</c> for remove.  Fires on the
    /// SNIFFER thread; UI listeners must marshal.</summary>
    public event Action<ActiveBuff, bool>? BuffChanged;

    public BuffTracker(MhMissionSniffer sniffer)
    {
        _sniffer = sniffer ?? throw new ArgumentNullException(nameof(sniffer));
        _sniffer.ConditionAdded   += OnConditionAdded;
        _sniffer.ConditionRemoved += OnConditionRemoved;
    }

    /// <summary>How many buffs are currently tracked.  Snapshotted under the lock so it's
    /// consistent with the result of a subsequent <see cref="GetActiveBuffs"/> in the same
    /// frame.</summary>
    public int ActiveCount
    {
        get { lock (_sync) return _active.Count; }
    }

    /// <summary>Sum of every active buff's contribution to a given <see cref="PropertyEnum"/>.
    /// Used by the live-stats panel to answer "what's my current +%damage from buffs"
    /// without making the panel walk per-buff lists itself.
    ///
    /// <para>Reads <see cref="BuffPropertyDelta.FloatValue"/> -- which is correct for the
    /// percentage-bonus / multiplier properties we surface in the UI.  Int-typed properties
    /// (stack counters, level scaling) would need a sibling <c>GetActiveStatBonusInt</c>
    /// overload, but we don't display any of those yet.</para>
    ///
    /// <para>Cost is O(buffs * deltas-per-buff).  Typical fight has ~10 buffs, each with
    /// 1-5 properties, so we're walking ~50 entries per call.  Callers can cache if they
    /// invoke from a tight loop.</para>
    /// </summary>
    public double SumActivePropertyFloat(uint propertyEnum)
    {
        double total = 0.0;
        lock (_sync)
        {
            foreach (var buff in _active.Values)
            {
                var deltas = buff.PropertyDeltas;
                for (int i = 0; i < deltas.Count; i++)
                {
                    if (deltas[i].PropertyEnum == propertyEnum)
                        total += deltas[i].FloatValue;
                }
            }
        }
        return total;
    }

    /// <summary>Bulk variant of <see cref="SumActivePropertyFloat"/>: sums all the enums the
    /// caller cares about in a single pass over the active-buffs list.  Returns a parallel
    /// array of float-summed values, indexed the same way as <paramref name="propertyEnums"/>.
    /// Used by the stats panel which wants ~6 sums per render -- one pass is much cheaper
    /// than 6 passes when the buff list grows.
    ///
    /// <para>Reads <see cref="BuffPropertyDelta.FloatValue"/> for every property -- only
    /// correct for the percentage-bonus / ratio properties.  For integer-typed properties
    /// (rating values) use <see cref="GetActiveStatBreakdowns"/> with the appropriate
    /// <c>isIntegerTyped</c> flag.</para>
    /// </summary>
    public double[] SumActivePropertyFloats(IReadOnlyList<uint> propertyEnums)
    {
        var totals = new double[propertyEnums.Count];
        if (propertyEnums.Count == 0) return totals;
        lock (_sync)
        {
            foreach (var buff in _active.Values)
            {
                var deltas = buff.PropertyDeltas;
                for (int i = 0; i < deltas.Count; i++)
                {
                    uint e = deltas[i].PropertyEnum;
                    // Inner loop over wanted-enums.  propertyEnums is short (~6 entries)
                    // so a linear scan is faster than a Dictionary lookup with its hash
                    // and allocation overhead.
                    for (int j = 0; j < propertyEnums.Count; j++)
                    {
                        if (propertyEnums[j] == e)
                        {
                            totals[j] += deltas[i].FloatValue;
                            break;  // one delta contributes to exactly one wanted enum
                        }
                    }
                }
            }
        }
        return totals;
    }

    /// <summary>Per-buff attribution for each requested <c>PropertyEnum</c>: for every
    /// active buff that contributes to a property, returns its display name and the
    /// individual contribution value.
    ///
    /// <para>Always reads <see cref="BuffPropertyDelta.FloatValue"/> -- every property we
    /// care about for the stats panel is typed <c>Real</c> on the server, even the "rating"
    /// properties that hold integer-looking values like 448 or 1247.  Reading
    /// <see cref="BuffPropertyDelta.IntValue"/> for those would give billions of garbage from
    /// interpreting the IEEE-754 bit pattern as an integer (an early bug we hit -- "+Damage
    /// Rating +1139621888" was just <c>(uint)0x43E00000</c>, the bit pattern of 448.0).</para>
    ///
    /// <para>Returns an array parallel to <paramref name="propertyEnums"/>: <c>result[j]</c>
    /// is the list of <c>(displayName, value)</c> tuples that contributed to the j'th
    /// enum.  Empty list when no active buff modifies that property.</para>
    ///
    /// <para>Used by the stats-panel tooltip to render breakdowns like:
    /// "Damage +140% = Overwatch +40, Empowered +100".  Sums are derivable by the caller
    /// (just add the per-tuple values).  Total cost per call is O(buffs * deltas-per-buff)
    /// -- same as <see cref="SumActivePropertyFloats"/>, since we walk the same nested
    /// loop once and write into per-enum lists instead of summing into per-enum scalars.</para>
    /// </summary>
    public IReadOnlyList<(string SourceName, double Value)>[] GetActiveStatBreakdowns(
        IReadOnlyList<uint> propertyEnums)
    {
        var lists = new List<(string, double)>[propertyEnums.Count];
        for (int i = 0; i < lists.Length; i++) lists[i] = new List<(string, double)>();

        if (propertyEnums.Count != 0)
        {
            lock (_sync)
            {
                foreach (var buff in _active.Values)
                {
                    var deltas = buff.PropertyDeltas;
                    for (int i = 0; i < deltas.Count; i++)
                    {
                        uint e = deltas[i].PropertyEnum;
                        // Linear scan -- propertyEnums is short, the constant factor beats
                        // a Dictionary's hash overhead at this scale.
                        for (int j = 0; j < propertyEnums.Count; j++)
                        {
                            if (propertyEnums[j] == e)
                            {
                                double v = deltas[i].FloatValue;
                                // Skip zero-valued contributions -- they'd clutter the tooltip
                                // with "Foo +0%" lines for buffs that touch the property but
                                // don't actively modify it on this stack.
                                if (v < -0.0005 || v > 0.0005)
                                    lists[j].Add((buff.DisplayName, v));
                                break;
                            }
                        }
                    }
                }
            }
        }

        // Cast to IReadOnlyList for the public surface so callers can't mutate our lists.
        var result = new IReadOnlyList<(string, double)>[lists.Length];
        for (int i = 0; i < lists.Length; i++) result[i] = lists[i];
        return result;
    }

    /// <summary>Snapshot of every currently-tracked buff.  Sorted by ExpiresUtc (closest
    /// expiry first) so the UI can render the most time-sensitive ones first.  Permanent
    /// (duration=0) buffs sort to the end.</summary>
    public IReadOnlyList<ActiveBuff> GetActiveBuffs()
    {
        ActiveBuff[] snapshot;
        lock (_sync)
        {
            if (_active.Count == 0) return Array.Empty<ActiveBuff>();
            snapshot = new ActiveBuff[_active.Count];
            _active.Values.CopyTo(snapshot, 0);
        }
        // Stable sort: timed buffs by ascending expiry, permanent buffs last in apply-order.
        // DateTime.MaxValue sentinel for permanent entries keeps the sort key total.
        Array.Sort(snapshot, (a, b) =>
        {
            var ax = a.ExpiresUtc ?? DateTime.MaxValue;
            var bx = b.ExpiresUtc ?? DateTime.MaxValue;
            int byTime = ax.CompareTo(bx);
            return byTime != 0 ? byTime : a.AppliedUtc.CompareTo(b.AppliedUtc);
        });
        return snapshot;
    }

    private void OnConditionAdded(object? sender, ConditionAddedEvent ev)
    {
        ulong selfId = SelfOwnerId;
        if (selfId == 0 || ev.OwnerEntityId != selfId) return;

        // Resolve the power / condition prototype refs to human-readable names.  Either
        // (or both) can be zero -- the wire format omits the condition proto ref for buffs
        // defined inline in their source power.  Fall back to "<unmapped>" so the entry is
        // still useful in logs and UI even when one of the tables doesn't have the entry
        // (older Calligraphy version, custom server, etc.).
        //
        // IMPORTANT: buff sources are written on the wire via Serializer.Transfer(ref PrototypeId)
        // which uses the ROOT Prototype enum (every prototype type, ~93k entries) -- NOT the
        // Power-specific enum that PowerNames uses for damage events (NetMessagePowerResult
        // goes through TransferPrototypeEnum<PowerPrototype>).  Same power, different number
        // depending on which message it appears in.  Use PowerNamesByProto here.
        string? sourcePower = ev.CreatorPowerPrototypeRef != 0
            ? PowerNamesByProto.Get((uint)ev.CreatorPowerPrototypeRef)
            : null;
        string? conditionName = ev.ConditionPrototypeRef != 0
            ? ConditionNames.Get((uint)ev.ConditionPrototypeRef)
            : null;

        // Pick the best display name available.  Most buffs come from a power that
        // applies a condition; the condition itself usually has no separate prototype on
        // the wire (NoConditionPrototypeRef flag), so the power name is the natural
        // display name.  Falling back to the condition name covers item-applied buffs
        // (XP boosts etc) where the condition IS the named prototype and the power is
        // synthetic.  Last fallback is the raw hex id, ugly but searchable.
        string displayName =
            sourcePower
            ?? conditionName
            ?? $"buff#0x{ev.CreatorPowerPrototypeRef:X}/0x{ev.ConditionPrototypeRef:X}";

        DateTime? expiresUtc = ev.DurationMs > 0
            ? ev.UtcTime + TimeSpan.FromMilliseconds(ev.DurationMs)
            : (DateTime?)null;

        var buff = new ActiveBuff
        {
            ConditionId                = ev.ConditionId,
            OwnerEntityId              = ev.OwnerEntityId,
            CreatorEntityId            = ev.CreatorEntityId,
            UltimateCreatorEntityId    = ev.UltimateCreatorEntityId,
            ConditionPrototypeRef      = ev.ConditionPrototypeRef,
            CreatorPowerPrototypeRef   = ev.CreatorPowerPrototypeRef,
            DisplayName                = displayName,
            SourcePowerName            = sourcePower,
            ConditionPrototypeName     = conditionName,
            DurationMs                 = ev.DurationMs,
            AppliedUtc                 = ev.UtcTime,
            ExpiresUtc                 = expiresUtc,
            // Carry the parsed (PropertyEnum, value) pairs forward so the live-stats panel
            // can sum them across all active buffs without re-walking archives.
            PropertyDeltas             = ev.PropertyDeltas,
        };

        bool replaced;
        lock (_sync)
        {
            replaced = _active.ContainsKey(ev.ConditionId);
            _active[ev.ConditionId] = buff;
        }

        // One-line summary log.  Shows up by default (NOT gated on verbose) because this is
        // a low-volume event -- typical play sees a handful of buffs per minute, and being
        // able to see "+ Overwatch (5.0s)" in the log is the difference between debuggable
        // and not.  The heavy property-bag dump is still available via the
        // DpsOverlayPresenter's discovery hook, gated on verbose-diagnostics.
        string durLabel = ev.DurationMs > 0 ? $"{ev.DurationMs / 1000.0:0.0}s" : "permanent";
        Diagnostic?.Invoke(
            $"BuffTracker: {(replaced ? "~" : "+")} \"{displayName}\" condId={ev.ConditionId} duration={durLabel}");

        BuffChanged?.Invoke(buff, /*added*/ true);
    }

    private void OnConditionRemoved(object? sender, ConditionRemovedEvent ev)
    {
        ulong selfId = SelfOwnerId;
        if (selfId == 0 || ev.OwnerEntityId != selfId) return;

        ActiveBuff? removed;
        lock (_sync)
        {
            if (_active.TryGetValue(ev.ConditionId, out var b))
            {
                _active.Remove(ev.ConditionId);
                removed = b;
            }
            else
            {
                removed = null;
            }
        }

        if (removed != null)
        {
            Diagnostic?.Invoke($"BuffTracker: - \"{removed.DisplayName}\" condId={ev.ConditionId}");
            BuffChanged?.Invoke(removed, /*added*/ false);
        }
        else
        {
            // Stray remove for a condition we never saw added (we connected mid-fight, or
            // the add event was dropped by Npcap).  Not an error -- just log it so we know
            // why a "remove" event didn't produce a UI update.
            Diagnostic?.Invoke($"BuffTracker: - (untracked condId={ev.ConditionId}) -- ignored");
        }
    }

    public void Dispose()
    {
        _sniffer.ConditionAdded   -= OnConditionAdded;
        _sniffer.ConditionRemoved -= OnConditionRemoved;
        lock (_sync) _active.Clear();
    }
}

/// <summary>Snapshot of a single tracked buff.  Immutable; created on
/// <see cref="MhMissionSniffer.ConditionAdded"/> and held until the matching
/// <see cref="MhMissionSniffer.ConditionRemoved"/> arrives.</summary>
public sealed class ActiveBuff
{
    /// <summary>Server-allocated condition slot id, unique per owner.  Pair with
    /// <see cref="OwnerEntityId"/> to identify a specific buff instance globally.</summary>
    public required ulong ConditionId { get; init; }

    /// <summary>Entity wearing the buff -- always equals <c>BuffTracker.SelfOwnerId</c> for
    /// entries that made it into the tracker.</summary>
    public required ulong OwnerEntityId { get; init; }

    /// <summary>Entity that directly applied the buff (could be a pet, an NPC, a teammate).
    /// Equals <see cref="OwnerEntityId"/> for self-buffs.</summary>
    public ulong CreatorEntityId { get; init; }

    /// <summary>Root cause entity for the buff chain (player whose pet's aura buffed us).
    /// Often equals <see cref="CreatorEntityId"/>.</summary>
    public ulong UltimateCreatorEntityId { get; init; }

    /// <summary>Full 64-bit <c>PrototypeId</c>-as-enum-index of the condition itself.  Zero
    /// when the server set <c>NoConditionPrototypeRef</c> (most ability-applied buffs --
    /// the condition is defined inline in the source power).</summary>
    public ulong ConditionPrototypeRef { get; init; }

    /// <summary>Power that applied this buff (e.g. Overwatch).  Zero for rare cases where
    /// the buff has no power source (item-applied auras etc).</summary>
    public ulong CreatorPowerPrototypeRef { get; init; }

    /// <summary>Best display name we could resolve: source power name preferred, then
    /// condition prototype name, then a synthetic hex-id fallback.  Use this for UI; use
    /// <see cref="SourcePowerName"/> / <see cref="ConditionPrototypeName"/> for
    /// disambiguation.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Resolved name of <see cref="CreatorPowerPrototypeRef"/> via
    /// <c>PowerNamesByProto.Get</c> (root-enum table, since buff sources are written with
    /// <c>Serializer.Transfer(ref PrototypeId)</c>), or <c>null</c> when the ref is 0 or
    /// unmapped.</summary>
    public string? SourcePowerName { get; init; }

    /// <summary>Resolved name of <see cref="ConditionPrototypeRef"/> via
    /// <c>ConditionNames.Get</c>, or <c>null</c>.</summary>
    public string? ConditionPrototypeName { get; init; }

    /// <summary>Server-declared duration in milliseconds.  Zero means "permanent" (the
    /// server didn't set <c>HasDuration</c> -- typically passive auras while a source
    /// ability is held, or always-on costume effects).</summary>
    public long DurationMs { get; init; }

    /// <summary>Wall-clock when we observed the <c>ConditionAdded</c> event.  Used as the
    /// anchor for the countdown display; the server's own <c>startTime</c> field is
    /// available on the wire but we don't bother parsing it (the wall-clock-on-receive is
    /// accurate enough for UI countdowns; the server's authoritative expiry comes via the
    /// <c>ConditionRemoved</c> event, not by counting down to <see cref="ExpiresUtc"/>).</summary>
    public required DateTime AppliedUtc { get; init; }

    /// <summary>Computed UTC expiry, or <c>null</c> when <see cref="DurationMs"/> is 0.
    /// UI uses this for the countdown ring -- when it hits zero, the buff visually fades
    /// but stays in the tracker until the server sends <c>ConditionRemoved</c>.</summary>
    public DateTime? ExpiresUtc { get; init; }

    /// <summary>True when this buff has no expiry (passive aura / always-on effect).</summary>
    public bool IsPermanent => DurationMs <= 0;

    /// <summary>Convenience: how much time is left, or <see cref="TimeSpan.Zero"/> for
    /// permanent buffs and expired-but-not-yet-removed entries.  UI bind point.</summary>
    public TimeSpan Remaining(DateTime nowUtc)
    {
        if (ExpiresUtc is null) return TimeSpan.Zero;
        var r = ExpiresUtc.Value - nowUtc;
        return r > TimeSpan.Zero ? r : TimeSpan.Zero;
    }

    /// <summary>The (PropertyEnum, value) pairs this buff contributes -- e.g. one entry with
    /// <c>PropertyEnum=283</c> (DamagePctBonus) and <c>FloatValue=0.40</c> for Overwatch.
    /// Always non-null but may be empty (buff has no property effects, or the parser tripped
    /// on this archive).  The live-stats panel sums these across all active buffs.</summary>
    public IReadOnlyList<BuffPropertyDelta> PropertyDeltas { get; init; }
        = Array.Empty<BuffPropertyDelta>();
}
