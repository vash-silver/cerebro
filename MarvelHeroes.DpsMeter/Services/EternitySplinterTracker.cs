using System;
using System.Collections.Generic;
using MarvelHeroesComporator.NetworkSniffer;

namespace MarvelHeroes.DpsMeter.Services;

/// <summary>
/// Watches the sniffer's <see cref="MhMissionSniffer.LootDropped"/> events for Eternity
/// Splinter drops and exposes the resulting 6-minute cooldown countdown to the UI (the
/// server-side throttle is ~7 minutes; see <see cref="CooldownDuration"/> for the gap).
///
/// <para>The server keeps splinter drops on a per-player throttle: at most one drop per
/// <see cref="CooldownDuration"/> per player.  When a splinter spawns on the ground we
/// reset the local timer; the overlay shows a countdown so the user knows when the next
/// drop is eligible (i.e. when killing another mob has a chance of yielding one).</para>
///
/// <para>The Eternity Splinter <c>PrototypeId</c> was identified by decompiling
/// <c>MHServerEmu.Games.dll</c>:
/// <list type="bullet">
///   <item><c>LootCooldownTable.EternitySplinterPrototypeRef =
///         GameDatabase.GetDataRefByPrototypeGuid((PrototypeGuid)14274455345508523748uL);</c>
///         -- this is a runtime-resolved DataRef we can't replicate offline.</item>
///   <item><c>LootInstance.SpawnLootEntities(...)</c> when CombineESStacks is enabled calls
///         <c>lootResultSummary.CombineCurrencyStacks((PrototypeId)11087194553833821680uL,
///         GameDatabase.CurrencyGlobalsPrototype.EternitySplinters);</c> -- a literal
///         PrototypeId hardcoded as the canonical splinter stack item.  This is the value
///         that arrives in <c>NetMessageLootEntity.ItemSpec.ItemProtoRef</c> on the wire.</item>
/// </list>
/// We use the latter (the literal) as the match key.</para>
///
/// <para>Threading: <see cref="OnLootDropped"/> runs on the sniffer's capture thread.  All
/// state mutations are guarded by <see cref="_sync"/>; the public properties / events are
/// safe to read from any thread.  <see cref="Tick"/> is meant to be called by the UI
/// dispatcher's existing decay timer (4 Hz is plenty) so cooldown-expiry events surface on
/// the UI thread without us spinning up a dedicated timer.</para>
/// </summary>
public sealed class EternitySplinterTracker : IDisposable
{
    /// <summary>Standard server-side cooldown between splinter drops.  Empirically the
    /// MHServerEmu throttle resolves a touch under 7 min in practice (network jitter, server
    /// tick alignment), so we run the visible countdown at 6 min -- this means the timer
    /// hits zero a hair before the server is actually ready, but in exchange the user never
    /// sees a "0:00 -- eligible" reading that lies because the server hasn't rolled over
    /// yet.  Favour false-positive eligibility over false-negative.</summary>
    public static readonly TimeSpan CooldownDuration = TimeSpan.FromMinutes(6);

    /// <summary>Known Eternity Splinter <c>PrototypeId</c> values, used by the
    /// <see cref="MhMissionSniffer.LootDropped"/> path (full 64-bit DataRef matching).
    ///
    /// <para><b>Note:</b> MHServerEmu only emits <c>NetMessageLootEntity</c> as a sub-struct
    /// inside <c>NetMessageLootRewardReport</c>, not as a standalone wire message -- so on
    /// MHServerEmu-derived servers the LootDropped event likely never fires for ground drops.
    /// The primary detection path is <see cref="DefaultKnownProtoIndices"/> matching on the
    /// <c>NetMessageEntityCreate</c> enum index instead.  This set is kept as a fallback for
    /// servers that DO send standalone LootEntity (Tahiti, custom forks).</para></summary>
    public static readonly HashSet<ulong> DefaultKnownProtoRefs = new()
    {
        // Source: Entity/Items/CurrencyItems/EternitySplinter.prototype, confirmed in
        // OpenCalligraphy as id=11087194553833821680 / guid=14274455345508523748.  Matches
        // the literal in MHServerEmu.Games.dll's LootInstance.CombineCurrencyStacks call.
        11087194553833821680uL,
    };

    /// <summary>Known Eternity Splinter <c>PrototypeEnumIndex</c> values -- the small 1-based
    /// ordinal that arrives in <c>NetMessageEntityCreate.baseData</c> via
    /// <c>GazillionArchiveReader.ReadPrototypeEnumIndex()</c>.
    ///
    /// <para>Different encoding from <see cref="DefaultKnownProtoRefs"/>: the enum index is a
    /// compact uint, not the full 64-bit PrototypeId you see in OpenCalligraphy.  The mapping
    /// from PrototypeId to enum index is computed at server-load time by walking
    /// <c>DataDirectory._prototypeClassLookupDict[EntityPrototype]</c> sorted by
    /// <c>PrototypeId</c>; we can't replicate it without the .sip data, so this set has to
    /// be populated empirically (run a session with the discovery-log enabled, drop a
    /// splinter in-game, read the enum index out of the log, paste it here).</para>
    ///
    /// <para>Starts empty -- if you see "EternitySplinterTracker: unknown non-avatar entity
    /// created with proto index N" in the log right when an in-game splinter dropped, that's
    /// the value to add.</para></summary>
    public static readonly HashSet<uint> DefaultKnownProtoIndices = new()
    {
        // Discovered empirically via two-session intersection of the discovery-log
        // EntityCreate dumps:
        //   Session 1 (12 unique non-avatar indices): 8813 11533 6714 7041 1542 11745
        //     16014 13341 536 19207 13073 4683
        //   Session 2 (23 unique): ...
        //   Intersection minus already-known mob/boss/hero entries:
        //     536  6714  8813  13073  13341
        //
        // 13341 matches the strongest "splinter drop" pattern: a *lone* EntityCreate
        // that spawns ~1 second after a mob-kill loot burst (rather than inside the
        // same-millisecond cluster of gear / credits / fortune-card entities).  That
        // timing is consistent with how Eternity Splinters visually plop down a beat
        // after the mob actually dies.  Tentative -- if field-testing shows the pill
        // doesn't flash on real splinter drops, try 13073 next (the only other lone-
        // pattern intersection candidate).
        13341u,
    };

    private readonly MhMissionSniffer? _sniffer;
    private readonly HashSet<ulong> _knownProtoRefs;
    private readonly HashSet<uint>  _knownProtoIndices;
    private readonly HashSet<ulong> _unknownProtoRefsLogged;       // de-dup LootDropped diag
    private readonly HashSet<uint>  _unknownProtoIndicesLogged;    // de-dup EntityCreated diag
    private int _unknownProtoIndicesLogCount;                       // session-wide spam cap
    private const int MaxUnknownProtoIndexLogs = 200;
    // De-dup cap for cooldown-suppressed match logs.  If protoIdx 13341 turns out to be
    // shared with a very common entity, we don't want every suppression to spam the log
    // -- one line per (cooldown-window, protoIdx) is enough to confirm the suppression
    // works.  Cleared on each fresh detection so a new cooldown window starts fresh.
    private readonly HashSet<uint> _suppressedProtoIndicesLoggedThisCooldown = new();
    private readonly object _sync = new();

    private DateTime _lastDropUtc      = DateTime.MinValue;
    private DateTime _lastSeenLootUtc  = DateTime.MinValue;
    private bool     _cooldownExpiredFired;

    /// <summary>Optional log sink for diagnostics ("unknown loot proto ref observed",
    /// "splinter dropped", "cooldown expired").  Wire to the same log as the rest of the
    /// meter so a single file captures everything.</summary>
    public Action<string>? Diagnostic { get; set; }

    /// <summary>Fires when a splinter drop is detected.  Runs on the sniffer thread --
    /// the UI should marshal to its dispatcher.</summary>
    public event EventHandler<SplinterDroppedEventArgs>? SplinterDropped;

    /// <summary>Fires exactly once per cooldown when the countdown window expires.  Useful
    /// for an audio cue or toast.  Runs on whichever thread called <see cref="Tick"/>.</summary>
    public event EventHandler? CooldownExpired;

    public EternitySplinterTracker(MhMissionSniffer? sniffer)
    {
        _sniffer = sniffer;
        _knownProtoRefs            = new HashSet<ulong>(DefaultKnownProtoRefs);
        _knownProtoIndices         = new HashSet<uint>(DefaultKnownProtoIndices);
        _unknownProtoRefsLogged    = new HashSet<ulong>();
        _unknownProtoIndicesLogged = new HashSet<uint>();

        if (_sniffer != null)
        {
            // Two parallel detection paths -- whichever fires first wins:
            //   1. LootDropped (NetMessageLootEntity, 64-bit PrototypeId)
            //      -- works on servers that send standalone LootEntity messages.
            //   2. EntityCreated (NetMessageEntityCreate, 32-bit enum index)
            //      -- works on every server, but we need to know the index first.
            // Both are wired so we don't have to guess which the running server uses.
            _sniffer.LootDropped   += OnLootDropped;
            _sniffer.EntityCreated += OnEntityCreated;
        }
    }

    /// <summary>UTC timestamp of the most recent detected splinter drop, or
    /// <see cref="DateTime.MinValue"/> if no drop has been seen this session.</summary>
    public DateTime LastDropUtc
    {
        get { lock (_sync) return _lastDropUtc; }
    }

    /// <summary>UTC timestamp at which the next splinter drop becomes eligible.  Equal to
    /// <c>LastDropUtc + CooldownDuration</c>; <see cref="DateTime.MinValue"/> when no drop
    /// has been seen.</summary>
    public DateTime CooldownEndUtc
    {
        get
        {
            lock (_sync)
            {
                return _lastDropUtc == DateTime.MinValue
                    ? DateTime.MinValue
                    : _lastDropUtc + CooldownDuration;
            }
        }
    }

    /// <summary>True while we're inside the post-drop countdown window.  False before the
    /// first drop and after the window has elapsed.</summary>
    public bool IsCooldownActive => RemainingCooldown > TimeSpan.Zero;

    /// <summary>How much time is left on the active cooldown.  Returns
    /// <see cref="TimeSpan.Zero"/> when no cooldown is active.</summary>
    public TimeSpan RemainingCooldown
    {
        get
        {
            DateTime endUtc = CooldownEndUtc;
            if (endUtc == DateTime.MinValue) return TimeSpan.Zero;
            var remaining = endUtc - DateTime.UtcNow;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }

    /// <summary>Total drops detected since the tracker started.  Useful for the overlay's
    /// "5 splinters today" badge and for logging.  NOTE: this counts drop EVENTS, not
    /// individual splinters -- a single drop can yield 1, 5, 14 splinters depending on
    /// loot rolls.  Use <see cref="TotalSplintersThisSession"/> for the actual splinter
    /// count.</summary>
    public int DropCount { get; private set; }

    /// <summary>Cumulative splinter quantity received this session, summed across every
    /// drop event.  The auto-detect path can't yet read the stack count out of the
    /// EntityCreate archive (the wire format isn't reverse-engineered), so it contributes
    /// a placeholder of 1 splinter per detected drop.  The manual override path
    /// (<see cref="ArmFromNow(int)"/>) lets the user pass the actual quantity they saw
    /// in-game, so the running total stays accurate when the user goes "I got 9, click".
    /// Reset to zero on <see cref="Reset"/>.</summary>
    public int TotalSplintersThisSession { get; private set; }

    /// <summary>Add a runtime-discovered proto ref to the match set.  Persists only for the
    /// current process; if the value turns out to be a real splinter variant, add it to
    /// <see cref="DefaultKnownProtoRefs"/> in source so it survives restarts.</summary>
    public void AddKnownProtoRef(ulong protoRef)
    {
        lock (_sync)
        {
            if (_knownProtoRefs.Add(protoRef))
                Diagnostic?.Invoke($"EternitySplinterTracker: added proto ref {protoRef} to known set at runtime");
        }
    }

    /// <summary>Add a runtime-discovered enum index to the match set.  Same caveats as
    /// <see cref="AddKnownProtoRef"/> -- runtime-only; promote to <see cref="DefaultKnownProtoIndices"/>
    /// in source code to make the addition stick across restarts.</summary>
    public void AddKnownProtoIndex(uint protoIndex)
    {
        lock (_sync)
        {
            if (_knownProtoIndices.Add(protoIndex))
                Diagnostic?.Invoke($"EternitySplinterTracker: added proto index {protoIndex} to known set at runtime");
        }
    }

    /// <summary>Manually arm the cooldown (treats now as a fresh drop).  Useful when the
    /// detection logic missed an actual drop (e.g. the user already saw the splinter
    /// before launching the meter) and the user wants the timer to be accurate.  The
    /// <paramref name="splinterCount"/> parameter records the number of splinters the
    /// user actually received from this drop (the in-game UI shows it as "+9 Eternity
    /// Splinters!" / etc); contributes to <see cref="TotalSplintersThisSession"/>.
    /// Defaults to 1 so callers that don't care about the count still work.</summary>
    public void ArmFromNow(int splinterCount = 1)
    {
        OnSplinterDetected(DateTime.UtcNow, manual: true, splinterCount: Math.Max(1, splinterCount));
    }

    /// <summary>Clear cooldown state.  Useful for testing and for the right-click "Reset
    /// splinter cooldown" menu when the user knows the in-game timer doesn't match (e.g.
    /// after a relog or zone change that reset the server's per-player throttle).</summary>
    public void Reset()
    {
        bool wasActive;
        lock (_sync)
        {
            wasActive = _lastDropUtc != DateTime.MinValue;
            _lastDropUtc = DateTime.MinValue;
            _cooldownExpiredFired = false;
        }
        if (wasActive)
            Diagnostic?.Invoke("EternitySplinterTracker: cooldown reset by user");
    }

    /// <summary>Poll point for the UI dispatcher's decay timer.  Fires
    /// <see cref="CooldownExpired"/> once when the countdown window elapses; idempotent
    /// otherwise.  Call as often as you'd like -- it's a cheap clock comparison.</summary>
    public void Tick()
    {
        bool fire = false;
        lock (_sync)
        {
            if (_lastDropUtc != DateTime.MinValue
                && !_cooldownExpiredFired
                && DateTime.UtcNow >= _lastDropUtc + CooldownDuration)
            {
                _cooldownExpiredFired = true;
                fire = true;
            }
        }
        if (fire)
        {
            Diagnostic?.Invoke("EternitySplinterTracker: cooldown expired -- next splinter eligible to drop");
            try { CooldownExpired?.Invoke(this, EventArgs.Empty); } catch { /* listener exceptions don't kill the tick */ }
        }
    }

    private void OnEntityCreated(object? sender, EntityCreatedEvent e)
    {
        // Splinter is an Item entity; avatars are definitely not splinters.  Filtering on
        // IsAvatar keeps the discovery log from drowning in hero / costume swaps.
        if (e.IsAvatar) return;

        bool matched;
        lock (_sync) matched = _knownProtoIndices.Contains(e.PrototypeEnumIndex);

        if (matched)
        {
            // Trace EVERY splinter-index match before any other decision.  Helps debug "the
            // sniffer received the event but Cerebro didn't react" vs. "the sniffer never saw
            // the entity in the first place" -- the former shows up here, the latter doesn't.
            // Cheap (one log line per detection) and only fires for matches so it's not noisy.
            Diagnostic?.Invoke(
                $"EternitySplinterTracker: matched splinter EntityCreate -- " +
                $"protoIdx={e.PrototypeEnumIndex} entityId={e.EntityId} stackCount={e.StackCount} " +
                $"cooldownActive={IsCooldownActive} (decision: " +
                (IsCooldownActive ? "suppress as false-positive" : "accept as fresh drop") + ")");
            // Cooldown-active suppression: the server-side splinter throttle is ~7 min per
            // player, so a SECOND real drop arriving while our cooldown timer is still
            // running is physically impossible.  If we observe an EntityCreate with a known
            // splinter index inside the cooldown window, that's a false positive -- the
            // proto enum index is shared with some other entity (most likely a generic
            // ground-item / currency-stack reshuffle that the server emits during normal
            // play).  Drop it silently; if the user actually missed a real drop they can
            // hit "Reset Splinter cooldown" to clear the gate.
            //
            // We log the suppression (de-duped per session) so future debugging can tell
            // "the detector saw the right index but suppressed" apart from "the detector
            // never saw anything matching".
            if (IsCooldownActive)
            {
                if (LogSuppressedDetection(e))
                    Diagnostic?.Invoke(
                        $"EternitySplinterTracker: suppressed false-positive EntityCreate -- " +
                        $"protoIdx={e.PrototypeEnumIndex} entityId={e.EntityId} stackCount={e.StackCount} at {e.UtcTime:HH:mm:ss} " +
                        $"(cooldown still has {RemainingCooldown.TotalSeconds:0}s left; the server " +
                        $"can't legitimately drop a 2nd splinter inside the throttle window).  " +
                        $"This proto index probably shares with a non-splinter entity.");
                return;
            }
            // StackCount is extracted from the entity's Property.InventoryStackCount in the
            // sniffer.  Splinters spawn as currency-item entities with the actual quantity in
            // that property.  Fall back to 1 when the parser couldn't find the property (older
            // build, schema drift, or this turned out to not be a stackable entity after all
            // -- in which case "1 splinter" is the closest safe assumption).
            int splinterCount = e.StackCount > 0 ? e.StackCount : 1;
            OnSplinterDetected(e.UtcTime, manual: false, splinterCount: splinterCount);
            return;
        }

        // Discovery path: when the user's known-set is incomplete (almost always, since enum
        // indices are build-specific and have to be observed live), log the first occurrence
        // of each non-avatar entity proto index so the user can correlate "splinter dropped
        // at 14:23:17" with a log line like:
        //   [14:23:17.142] EternitySplinterTracker: unknown non-avatar EntityCreate -- protoIdx=12345 entityId=98765
        // Per-session de-dup so a mob spawn flurry doesn't bury the splinter line.  Hard cap
        // so a region with a thousand unique entities doesn't generate a thousand log lines.
        bool log = false;
        lock (_sync)
        {
            if (_unknownProtoIndicesLogged.Add(e.PrototypeEnumIndex)
                && _unknownProtoIndicesLogCount < MaxUnknownProtoIndexLogs)
            {
                _unknownProtoIndicesLogCount++;
                log = true;
            }
        }
        if (log)
            Diagnostic?.Invoke(
                $"EternitySplinterTracker: unknown non-avatar EntityCreate -- protoIdx={e.PrototypeEnumIndex} " +
                $"entityId={e.EntityId} dbId=0x{e.DatabaseUniqueId:X}  " +
                "(if a splinter just dropped, this might be its index -- add to DefaultKnownProtoIndices)");
    }

    private void OnLootDropped(object? sender, LootDroppedEvent e)
    {
        bool matched;
        lock (_sync) matched = _knownProtoRefs.Contains(e.ItemProtoRef);

        if (matched)
        {
            // Same cooldown-active suppression as OnEntityCreated -- on the off-chance the
            // LootDropped path *also* gets shared between splinters and another currency
            // item, drop in-cooldown matches as false positives.  See the OnEntityCreated
            // comment for the rationale.
            if (IsCooldownActive)
            {
                Diagnostic?.Invoke(
                    $"EternitySplinterTracker: suppressed in-cooldown LootDropped -- " +
                    $"protoRef={e.ItemProtoRef} at {e.UtcTime:HH:mm:ss} " +
                    $"(cooldown {RemainingCooldown.TotalSeconds:0}s left).");
                return;
            }
            OnSplinterDetected(e.UtcTime, manual: false);
            return;
        }

        // Discovery aid: log the first occurrence of each unknown proto ref so the user
        // can correlate "I saw a splinter drop in-game at HH:MM" with a wire-level proto
        // ref.  De-duped per proto ref so we don't spam.  We also rate-limit by tracking
        // the last loot drop time -- if loot is pouring in (boss kill), only the first
        // ~5 unique refs in a 1s window get logged.
        bool log = false;
        lock (_sync)
        {
            _lastSeenLootUtc = e.UtcTime;
            if (_unknownProtoRefsLogged.Add(e.ItemProtoRef))
                log = true;
        }
        if (log)
            Diagnostic?.Invoke(
                $"EternitySplinterTracker: loot dropped with unknown proto ref {e.ItemProtoRef} " +
                $"(itemId={e.ItemId}, level={e.ItemLevel}).  If you just saw a splinter drop in-game " +
                $"around this time, add this id to DefaultKnownProtoRefs.");
    }

    /// <summary>De-dup the cooldown-suppression diagnostic so a frequently-shared proto
    /// index doesn't fill the log with one line per false-positive EntityCreate.  Returns
    /// <c>true</c> the FIRST time we see this index within the current cooldown window.
    /// Reset by <see cref="OnSplinterDetected"/> so the next legit drop starts a fresh
    /// window with its own dedup set.</summary>
    private bool LogSuppressedDetection(EntityCreatedEvent e)
    {
        lock (_sync) return _suppressedProtoIndicesLoggedThisCooldown.Add(e.PrototypeEnumIndex);
    }

    private void OnSplinterDetected(DateTime utc, bool manual, int splinterCount = 1)
    {
        if (splinterCount < 1) splinterCount = 1;
        lock (_sync)
        {
            _lastDropUtc          = utc;
            _cooldownExpiredFired = false;
            DropCount++;
            TotalSplintersThisSession += splinterCount;
            // Reset the suppressed-index log set so the next cooldown window starts fresh
            // (one diagnostic line per unique proto-index per window).
            _suppressedProtoIndicesLoggedThisCooldown.Clear();
        }
        var msg = manual
            ? $"EternitySplinterTracker: cooldown armed manually at {utc:HH:mm:ss} -- recorded {splinterCount} splinter(s) (session total now {TotalSplintersThisSession})"
            : $"EternitySplinterTracker: splinter drop detected at {utc:HH:mm:ss} (drop #{DropCount}, +{splinterCount} = {TotalSplintersThisSession} total) -- {CooldownDuration.TotalMinutes:0} min cooldown started";
        Diagnostic?.Invoke(msg);
        try { SplinterDropped?.Invoke(this, new SplinterDroppedEventArgs(utc, manual, splinterCount)); }
        catch { /* listener exceptions don't kill the parser */ }
    }

    public void Dispose()
    {
        if (_sniffer != null)
        {
            _sniffer.LootDropped   -= OnLootDropped;
            _sniffer.EntityCreated -= OnEntityCreated;
        }
    }

    // ── Test injection hooks (accessible to MarvelHeroes.DpsMeter.Tests via InternalsVisibleTo) ──
    // Lets unit tests drive the detection paths without spinning up an MhMissionSniffer
    // (which needs Npcap, network capture, etc.).  Mirrors the pattern used by DpsMeter's
    // TestInjectDamage / TestInjectEntityKilled.

    /// <summary>Test-only entry point that runs the same OnEntityCreated logic the sniffer
    /// would trigger.  Used by tests to verify proto-index matching, cooldown suppression,
    /// and stack-count accumulation without needing a live capture.</summary>
    internal void TestInjectEntityCreate(uint protoIdx, ulong entityId, bool isAvatar, DateTime utc, int stackCount = 0)
        => OnEntityCreated(this, new EntityCreatedEvent
        {
            PrototypeEnumIndex = protoIdx,
            EntityId           = entityId,
            DatabaseUniqueId   = 0,
            IsAvatar           = isAvatar,
            StackCount         = stackCount,
            UtcTime            = utc,
        });
}

public sealed class SplinterDroppedEventArgs : EventArgs
{
    public SplinterDroppedEventArgs(DateTime utc, bool manual, int splinterCount = 1)
    {
        Utc            = utc;
        Manual         = manual;
        SplinterCount  = splinterCount;
    }
    public DateTime Utc    { get; }
    /// <summary>True when the user armed the cooldown via <see cref="EternitySplinterTracker.ArmFromNow"/>
    /// instead of the sniffer detecting an actual drop.</summary>
    public bool     Manual { get; }
    /// <summary>Number of splinters credited to this drop event.  Auto-detect path always
    /// passes 1 (the wire-level stack count isn't extracted yet); the manual override path
    /// passes whatever quantity the user entered.  Always &gt;= 1.</summary>
    public int      SplinterCount { get; }
}
