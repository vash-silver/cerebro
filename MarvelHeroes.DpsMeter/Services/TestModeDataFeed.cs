using System;
using System.Threading;

namespace MarvelHeroes.DpsMeter.Services;

/// <summary>
/// Injects synthetic boss-fight data into the two live DpsMeter instances so the overlay
/// is fully exercisable without the game running.  Activated by the <c>--test-mode</c>
/// command-line flag — the presenter and overlay work exactly as in production; only the
/// data source changes from the network sniffer to this feed.
///
/// <para>Simulates a looping pattern:
///   30-tick active fight (7.5 s) → 1 boss kill → 10-tick idle (2.5 s) → repeat.
/// "Iron Man" is self; "Cyclops" is a peer.  Both hit the same boss entity.</para>
/// </summary>
internal sealed class TestModeDataFeed : IDisposable
{
    // ── Fake entity IDs ──────────────────────────────────────────────────────────────────
    private const ulong SelfAvatarId  = 100_001;
    private const ulong PeerAvatarId  = 100_002;
    private const ulong BossEntityId  = 200_001;

    // 59u = StarktechSentinelEnc1B — a real [Boss] prototype index in BossPrototypes.Indices.
    private const uint BossProtoIdx   = 59u;

    // Arbitrary power indices (non-zero so the power-breakdown path exercises).
    private const uint SelfPowerIdx   = 101u;
    private const uint PeerPowerIdx   = 202u;

    // ── Tuning ───────────────────────────────────────────────────────────────────────────
    // 250 ms tick → 4 Hz, matching the presenter's decay timer.
    private static readonly TimeSpan TickInterval  = TimeSpan.FromMilliseconds(250);
    private const int FightTicks  = 30;   // 7.5 s active fight
    private const int IdleTicks   = 10;   // 2.5 s between fights
    private const int CycleTicks  = FightTicks + IdleTicks + 1; // +1 for the kill tick

    private const uint SelfDamagePerTick = 10_000;   // ~40 k/s
    private const uint PeerDamagePerTick = 5_000;    // ~20 k/s

    private readonly DpsMeter _meter;
    private readonly DpsMeter _bossMeter;
    private Timer? _timer;
    private int _tick;

    internal TestModeDataFeed(DpsMeter meter, DpsMeter bossMeter)
    {
        _meter     = meter     ?? throw new ArgumentNullException(nameof(meter));
        _bossMeter = bossMeter ?? throw new ArgumentNullException(nameof(bossMeter));

        // Wire up identities so the leaderboard shows named rows.
        foreach (var m in new[] { _meter, _bossMeter })
        {
            m.TestSetSelfOwner(SelfAvatarId);
            m.TestRegisterHero(SelfAvatarId, "Iron Man");
            m.TestRegisterHero(PeerAvatarId, "Cyclops");
            m.TestRegisterEntity(BossEntityId, BossProtoIdx);
        }
    }

    internal void Start()
    {
        _timer = new Timer(OnTick, null, TimeSpan.Zero, TickInterval);
    }

    private void OnTick(object? _)
    {
        int phase = _tick % CycleTicks;
        var now   = DateTime.UtcNow;

        if (phase < FightTicks)
        {
            // Active fight: both players deal damage to the boss.
            _bossMeter.TestInjectDamage(BossEntityId, SelfAvatarId, SelfPowerIdx, SelfDamagePerTick, now);
            _bossMeter.TestInjectDamage(BossEntityId, PeerAvatarId, PeerPowerIdx, PeerDamagePerTick, now);
            _meter.TestInjectDamage(BossEntityId, SelfAvatarId, SelfPowerIdx, SelfDamagePerTick, now);
        }
        else if (phase == FightTicks)
        {
            // Kill tick: end the encounter and re-arm the entity cache for the next fight.
            _bossMeter.TestInjectEntityKilled(BossEntityId, now);
            // Re-register so the next fight's first hit is admitted by the boss filter.
            _bossMeter.TestRegisterEntity(BossEntityId, BossProtoIdx);
        }
        // Remaining ticks are the idle gap — no events, overlay shows frozen leaderboard.

        _tick++;
    }

    public void Dispose() => _timer?.Dispose();
}
