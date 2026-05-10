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

    /// <summary>Known Eternity Splinter <c>PrototypeId</c> values.  Public so callers can
    /// surface the configured set in a settings UI; mutable via <see cref="AddKnownProtoRef"/>
    /// for empirical discovery during a session (the user spots an unmapped drop in the log
    /// and adds it without a recompile).</summary>
    public static readonly HashSet<ulong> DefaultKnownProtoRefs = new()
    {
        // Source: MHServerEmu.Games.dll, LootInstance.SpawnLootEntities -> CombineCurrencyStacks.
        // The canonical "splinter stack" item -- same id used by the game-data patches
        // (PatchDataSpecialEvents.json, XmasDailyGift entry).
        11087194553833821680uL,
    };

    private readonly MhMissionSniffer? _sniffer;
    private readonly HashSet<ulong> _knownProtoRefs;
    private readonly HashSet<ulong> _unknownProtoRefsLogged;   // de-dup the diagnostic log
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
        _knownProtoRefs = new HashSet<ulong>(DefaultKnownProtoRefs);
        _unknownProtoRefsLogged = new HashSet<ulong>();

        if (_sniffer != null)
            _sniffer.LootDropped += OnLootDropped;
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
            _sniffer.LootDropped -= OnLootDropped;
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
