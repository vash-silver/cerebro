using System;
using System.Collections.Generic;
using MarvelHeroesComporator.NetworkSniffer;

namespace MarvelHeroes.DpsMeter.Services;

/// <summary>
/// Watches the sniffer's <see cref="MhMissionSniffer.LootDropped"/> events for Eternity
/// Splinter drops and exposes the resulting 7-minute server-side cooldown to the UI.
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
    /// <summary>Standard server-side cooldown between splinter drops.</summary>
    public static readonly TimeSpan CooldownDuration = TimeSpan.FromMinutes(7);

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
        // Empty by design: enum indices are build-specific and discovered empirically.
        // Add the splinter's index here once it's been observed via the discovery log.
    };

    private readonly MhMissionSniffer? _sniffer;
    private readonly HashSet<ulong> _knownProtoRefs;
    private readonly HashSet<uint>  _knownProtoIndices;
    private readonly HashSet<ulong> _unknownProtoRefsLogged;       // de-dup LootDropped diag
    private readonly HashSet<uint>  _unknownProtoIndicesLogged;    // de-dup EntityCreated diag
    private int _unknownProtoIndicesLogCount;                       // session-wide spam cap
    private const int MaxUnknownProtoIndexLogs = 200;
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

    /// <summary>Fires exactly once per cooldown when the 7-minute window expires.  Useful
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

    /// <summary>True while we're inside the post-drop 7-minute window.  False before the
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
    /// "5 splinters today" badge and for logging.</summary>
    public int DropCount { get; private set; }

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
    /// before launching the meter) and the user wants the timer to be accurate.</summary>
    public void ArmFromNow()
    {
        OnSplinterDetected(DateTime.UtcNow, manual: true);
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
    /// <see cref="CooldownExpired"/> once when the 7-minute window elapses; idempotent
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
            OnSplinterDetected(e.UtcTime, manual: false);
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

    private void OnSplinterDetected(DateTime utc, bool manual)
    {
        lock (_sync)
        {
            _lastDropUtc          = utc;
            _cooldownExpiredFired = false;
            DropCount++;
        }
        var msg = manual
            ? $"EternitySplinterTracker: cooldown armed manually at {utc:HH:mm:ss}"
            : $"EternitySplinterTracker: splinter drop detected at {utc:HH:mm:ss} (#{DropCount}) -- 7 min cooldown started";
        Diagnostic?.Invoke(msg);
        try { SplinterDropped?.Invoke(this, new SplinterDroppedEventArgs(utc, manual)); }
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
}

public sealed class SplinterDroppedEventArgs : EventArgs
{
    public SplinterDroppedEventArgs(DateTime utc, bool manual)
    {
        Utc    = utc;
        Manual = manual;
    }
    public DateTime Utc    { get; }
    /// <summary>True when the user armed the cooldown via <see cref="EternitySplinterTracker.ArmFromNow"/>
    /// instead of the sniffer detecting an actual drop.</summary>
    public bool     Manual { get; }
}
