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

    /// <summary>Per-distinct-buff-name history: every name we've ever seen applied to the
    /// current self-owner, with first/last-seen timestamps, total fire count, and current
    /// active-stack count.  Drives the "Recently seen" discovery UI in the Buff Tracker
    /// tab so the user can click "Track" on a name they just saw rather than having to
    /// type it in.  Keyed by full <see cref="ActiveBuff.DisplayName"/> (not the chip-shortened
    /// form) so two different buffs that happen to shorten to the same chip label stay
    /// distinct here -- the panel surfaces the short label to the user but stores the
    /// long form internally for unambiguous match.
    ///
    /// <para>Cleared when <see cref="SelfOwnerId"/> changes (the previous owner's history
    /// is no longer relevant).  No size cap currently -- a heavy multi-hour session sees
    /// on the order of ~100 distinct buff names, which is fine for a Dictionary lookup
    /// and a short ListView.  Add a size cap if real-world usage shows otherwise.</para></summary>
    private readonly Dictionary<string, RecentBuffEntry> _seenHistory = new();

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
                // Discovery index is per-owner too -- the previous avatar's recent-buffs
                // history is irrelevant once we know we're tracking a different one.  No
                // need to fire an event; subscribers re-snapshot on the next poll tick.
                _seenHistory.Clear();
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

    /// <summary>Snapshot of every distinct buff name we've seen applied to the current
    /// self-owner since the tracker started (or since the last <see cref="SelfOwnerId"/>
    /// change).  Sorted by most-recent first so the Buff Tracker tab's "Recently seen"
    /// list shows the active rotation's buffs at the top, with stale entries falling
    /// down naturally as new things fire.
    ///
    /// <para>The list includes both currently-active and historical entries -- the UI
    /// distinguishes them via <see cref="RecentBuffSummary.CurrentlyActive"/>.  Caller
    /// renders "actively-applied" (Active > 0) and "recently seen but not active now"
    /// (Active == 0) as separate sections.</para></summary>
    public IReadOnlyList<RecentBuffSummary> GetRecentBuffs()
    {
        RecentBuffSummary[] snapshot;
        lock (_sync)
        {
            if (_seenHistory.Count == 0) return Array.Empty<RecentBuffSummary>();
            snapshot = new RecentBuffSummary[_seenHistory.Count];
            int i = 0;
            foreach (var kvp in _seenHistory)
            {
                snapshot[i++] = new RecentBuffSummary
                {
                    DisplayName       = kvp.Key,
                    ShortName         = BuffDisplayClassifier.ShortenForChip(kvp.Key),
                    FirstSeenUtc      = kvp.Value.FirstSeenUtc,
                    LastSeenUtc       = kvp.Value.LastSeenUtc,
                    TotalFires        = kvp.Value.TotalFires,
                    CurrentlyActive   = kvp.Value.CurrentlyActive,
                    CreatorPowerProto = kvp.Value.CreatorPowerProto,
                };
            }
        }
        // Most-recent first; if two entries fired at the exact same UTC tick fall back to
        // alphabetical for a stable order.
        Array.Sort(snapshot, (a, b) =>
        {
            int byTime = b.LastSeenUtc.CompareTo(a.LastSeenUtc);
            return byTime != 0 ? byTime : string.CompareOrdinal(a.ShortName, b.ShortName);
        });
        return snapshot;
    }

    // ── Derived state: Stealth / Invisible ──────────────────────────────────────────────
    // MH "stealth" and "true invisibility" are property-based -- a buff applies one or both
    // of these property deltas and the avatar's tooltip / damage-multiplier talents key off
    // the derived state, not the buff's name.  Walking ACTIVE buffs' property deltas every
    // tick gives us a derived "are you currently invisible-enough to trigger Surprise Attack
    // damage" boolean that's:
    //   * Resilient to renames -- doesn't matter what the buff is called, only what it does
    //   * Resilient to multiple sources -- artifact procs + Nightcrawler teleport stealth +
    //     costume cores can all set the same property; we OR them together
    //   * Cheap -- one pass over active buffs per check (typically <20 buffs)
    //
    // PropertyEnum 899 (Stealth) -- non-zero value means stealthed.
    // PropertyEnum 993 (Visible) -- zero value means invisible (the inverse boolean).
    private const uint PropertyEnumStealth = 899u;
    private const uint PropertyEnumVisible = 993u;

    /// <summary>Returns the current "is the local avatar in some form of stealth or
    /// invisibility" state by inspecting every active buff's property deltas.  Returns
    /// the kind (text label like "Stealthed", "Invisible", or "Stealthed + Invisible") and
    /// the list of buff names that contributed to the state -- useful for surfacing
    /// "you're stealthed because of: [Teleport Stealth Combo]" in the diagnostic UI.
    ///
    /// <para>This is a buff-property-derived signal only -- if the server changes the
    /// property via a standalone <c>NetMessageSetProperty</c> (no buff wrapper), this
    /// API doesn't see it.  The companion path through <c>NetMessageSetProperty</c>
    /// parsing in <c>MhMissionSniffer</c> covers that case separately.</para></summary>
    public bool TryGetStealthState(out string label, out IReadOnlyList<string> sources)
    {
        var stealthSrc   = new List<string>();
        var invisibleSrc = new List<string>();
        lock (_sync)
        {
            foreach (var buff in _active.Values)
            {
                var deltas = buff.PropertyDeltas;
                for (int i = 0; i < deltas.Count; i++)
                {
                    var d = deltas[i];
                    // Stealth = 1 -> stealthed.  Use RawValueBits so we don't trip over
                    // the IEEE-754 bool-as-tiny-denormal-double false-positive (a bool
                    // property's wire encoding stores the raw 0/1 in the value bits;
                    // FloatValue reinterpretation gives a meaningless denormal).
                    if (d.PropertyEnum == PropertyEnumStealth && d.RawValueBits != 0)
                        stealthSrc.Add(buff.DisplayName);
                    // Visible = 0 -> invisible.  The buff's delta sets Visible to false.
                    else if (d.PropertyEnum == PropertyEnumVisible && d.RawValueBits == 0)
                        invisibleSrc.Add(buff.DisplayName);
                }
            }
        }

        bool hasStealth   = stealthSrc.Count > 0;
        bool hasInvisible = invisibleSrc.Count > 0;
        if (!hasStealth && !hasInvisible)
        {
            label = string.Empty;
            sources = Array.Empty<string>();
            return false;
        }

        label = hasStealth && hasInvisible
            ? "Stealthed + Invisible"
            : hasStealth ? "Stealthed" : "Invisible";
        // Concatenate the two source lists, preserving order, dropping duplicates so a
        // single buff that sets BOTH properties (the common case for proper "stealth"
        // talents) doesn't appear twice.
        var combined = new List<string>(stealthSrc.Count + invisibleSrc.Count);
        var seen     = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        void Add(List<string> from)
        {
            foreach (var s in from) if (seen.Add(s)) combined.Add(s);
        }
        Add(stealthSrc);
        Add(invisibleSrc);
        sources = combined;
        return true;
    }

    /// <summary>Wipe the "recently seen" discovery index without touching the active-buffs
    /// list.  Wired to a "Clear history" button in the Buff Tracker tab so a user can
    /// reset the recent-list after a costume swap / artifact change without having to
    /// restart the app or change zones.</summary>
    public void ClearRecentHistory()
    {
        int cleared;
        lock (_sync)
        {
            cleared = _seenHistory.Count;
            _seenHistory.Clear();
        }
        Diagnostic?.Invoke($"BuffTracker: cleared {cleared} entries from recent-history");
    }

    /// <summary>Mutable accumulator for <see cref="_seenHistory"/>; exposed as the
    /// immutable <see cref="RecentBuffSummary"/> via <see cref="GetRecentBuffs"/>.  Mutations
    /// happen only inside the lock-guarded add/remove handlers above.</summary>
    private sealed class RecentBuffEntry
    {
        public DateTime FirstSeenUtc;
        public DateTime LastSeenUtc;
        public int TotalFires;
        public int CurrentlyActive;
        /// <summary>Root-prototype enum index of the power that applied this buff, or 0
        /// when the condition came without a creator power (rare -- usually item-applied
        /// effects).  Captured on the first observation so the discovery UI can suggest
        /// an in-game icon via <c>PowerIconByProto.Get</c> when the user clicks Track.</summary>
        public uint CreatorPowerProto;
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

            // Mirror into the "ever seen on this owner" discovery index.  Increment
            // TotalFires on every add (even replacements -- a stack refresh is a separate
            // fire from a fresh apply for "did this gear proc?" purposes); increment
            // CurrentlyActive only when this is a new condId (a refresh of an existing
            // condId doesn't change the active-stack count, it just resets the duration).
            if (!_seenHistory.TryGetValue(displayName, out var entry))
            {
                entry = new RecentBuffEntry
                {
                    FirstSeenUtc      = ev.UtcTime,
                    CreatorPowerProto = (uint)ev.CreatorPowerPrototypeRef,
                };
                _seenHistory[displayName] = entry;
            }
            entry.LastSeenUtc = ev.UtcTime;
            entry.TotalFires++;
            if (!replaced) entry.CurrentlyActive++;
            // Late-bind the creator-power if we missed it on the first sighting (e.g. an
            // item-applied buff that fired without a power, followed by an ability that
            // reapplies the same condition with a creator-power attached).
            if (entry.CreatorPowerProto == 0 && ev.CreatorPowerPrototypeRef != 0)
                entry.CreatorPowerProto = (uint)ev.CreatorPowerPrototypeRef;
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
                // Decrement the discovery-index's "currently active" count for this name.
                // History entry stays around indefinitely -- we WANT to remember that this
                // buff fired so the user can click "Track" on it later.
                if (_seenHistory.TryGetValue(b.DisplayName, out var entry) && entry.CurrentlyActive > 0)
                    entry.CurrentlyActive--;
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

/// <summary>Immutable summary of one distinct buff name's history on the current self-owner.
/// Returned by <see cref="BuffTracker.GetRecentBuffs"/> and consumed by the Buff Tracker tab's
/// discovery UI ("Currently active" + "Recently seen" sections).  Keyed conceptually on
/// <see cref="DisplayName"/> (the long-form name from the buff source) but displayed via
/// <see cref="ShortName"/> -- both are surfaced so the panel can show the friendly label and
/// the watchlist can store whichever the user actually clicked.</summary>
public sealed class RecentBuffSummary
{
    /// <summary>Long-form display name -- the chip text BEFORE
    /// <c>BuffDisplayClassifier.ShortenForChip</c>.  Used as the unique key in the
    /// tracker's history dictionary so two different buffs that happen to shorten to the
    /// same chip label don't collide.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Chip-shortened name -- what the user sees in the buff strip.  Also what
    /// the watchlist tracks, so "click Track" / "show in strip" both speak the same
    /// language.</summary>
    public required string ShortName { get; init; }

    /// <summary>UTC time we first saw this name applied to the current owner.</summary>
    public required DateTime FirstSeenUtc { get; init; }

    /// <summary>UTC time we last saw this name applied (most-recent re-apply / stack
    /// refresh).  Drives the sort order in the discovery UI -- most-recent first.</summary>
    public required DateTime LastSeenUtc { get; init; }

    /// <summary>Total number of <c>ConditionAdded</c> events we saw for this name, including
    /// stack refreshes.  Useful as a "how active is this proc" hint: a 50-fire artifact
    /// during a boss fight is the main DPS contributor; a 1-fire team buff was probably a
    /// one-off ability.</summary>
    public required int TotalFires { get; init; }

    /// <summary>How many condIds with this name are active RIGHT NOW.  Zero = the buff has
    /// fired in this session but isn't currently up.  Non-zero = visible in the buff strip
    /// (subject to the panel's category filter).</summary>
    public required int CurrentlyActive { get; init; }

    /// <summary>Root-prototype enum index of the power that applied this buff (the value
    /// the Buff Tracker tab feeds into <c>PowerIconByProto.Get</c> to auto-suggest an
    /// in-game icon when the user clicks Track).  Zero when the buff's first observed
    /// application had no creator power -- which means we can't infer a game icon for
    /// it and the user has to pick one via the file picker if they want imagery.</summary>
    public uint CreatorPowerProto { get; init; }
}
