using System;
using System.Linq;
using MarvelHeroes.DpsMeter.Services;
using Xunit;

namespace DpsMeterTests;

/// <summary>
/// Unit tests for the DpsMeter aggregation engine.
/// Uses the internal test constructor + TestInject* methods so no sniffer or game is required.
/// Boss prototype 59u (StarktechSentinelEnc1B) is a real entry in BossPrototypes.Indices.
/// </summary>
public sealed class DpsMeterCoreTests : IDisposable
{
    // A known [Boss] prototype index from BossPrototypes.Indices.
    private const uint BossProtoIdx = 59u;

    private readonly DpsMeter _meter;

    public DpsMeterCoreTests()
    {
        _meter = new DpsMeter(true);  // test constructor — no sniffer, no disk I/O
        _meter.BossOnlyMode = true;
    }

    public void Dispose() => _meter.Dispose();

    // ── Helpers ─────────────────────────────────────────────────────────────────────────

    private static DateTime T(int secondsFromEpoch)
        => new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(secondsFromEpoch);

    private void RegisterSelf(ulong ownerId, string heroName = "Iron Man")
    {
        _meter.TestSetSelfOwner(ownerId);
        _meter.TestRegisterHero(ownerId, heroName);
    }

    // ── Encounter lifecycle ──────────────────────────────────────────────────────────────

    [Fact]
    public void EncounterLifecycle_NoHits_NotActive()
    {
        var snap = _meter.GetEncounterSnapshot();
        Assert.False(snap.IsActive);
        Assert.False(snap.IsEnded);
    }

    [Fact]
    public void EncounterLifecycle_AfterFirstHit_IsActive()
    {
        ulong self = 1, boss = 9001;
        RegisterSelf(self);
        _meter.TestRegisterEntity(boss, BossProtoIdx);

        _meter.TestInjectDamage(boss, self, 1u, 10_000, T(0));

        var snap = _meter.GetEncounterSnapshot();
        Assert.True(snap.IsActive);
        Assert.False(snap.IsEnded);
    }

    [Fact]
    public void EncounterLifecycle_AfterBossKill_IsEnded()
    {
        ulong self = 1, boss = 9001;
        RegisterSelf(self);
        _meter.TestRegisterEntity(boss, BossProtoIdx);

        _meter.TestInjectDamage(boss, self, 1u, 10_000, T(0));
        _meter.TestInjectEntityKilled(boss, T(30));

        var snap = _meter.GetEncounterSnapshot();
        Assert.False(snap.IsActive);
        Assert.True(snap.IsEnded);
    }

    // ── Active-time DPS accuracy ─────────────────────────────────────────────────────────

    [Fact]
    public void ActiveTimeDps_SinglePlayer_MatchesTotalOverDuration()
    {
        ulong self = 1, boss = 9001;
        RegisterSelf(self);
        _meter.TestRegisterEntity(boss, BossProtoIdx);

        // 60 hits × 10,000 = 600,000 damage over 60 s
        for (int i = 0; i < 60; i++)
            _meter.TestInjectDamage(boss, self, 1u, 10_000, T(i));

        _meter.TestInjectEntityKilled(boss, T(60));

        var rows = _meter.GetTopHeroesByEncounterShare(5);
        Assert.Single(rows);

        var row = rows[0];
        Assert.True(row.IsSelf);
        Assert.Equal(600_000L, row.Total60s);
        // Active-time DPS = 600,000 / 60 s = 10,000 /s  (±2% tolerance for rounding)
        Assert.InRange(row.Dps, 9_800.0, 10_200.0);
    }

    [Fact]
    public void ActiveTimeDps_LateJoiningPeer_HasProportionallyHigherDps()
    {
        ulong self = 1, peer = 2, boss = 9001;
        RegisterSelf(self);
        _meter.TestRegisterHero(peer, "Cyclops");
        _meter.TestRegisterEntity(boss, BossProtoIdx);

        // Self: hits from t=0 to t=59, total 600k → DPS ≈ 10k/s over 60s
        for (int i = 0; i < 60; i++)
            _meter.TestInjectDamage(boss, self, 1u, 10_000, T(i));

        // Peer: joins at t=30, hits from t=30 to t=59, total 600k → DPS ≈ 20k/s over 30s
        for (int i = 30; i < 60; i++)
            _meter.TestInjectDamage(boss, peer, 2u, 20_000, T(i));

        _meter.TestInjectEntityKilled(boss, T(60));

        var rows = _meter.GetTopHeroesByEncounterShare(5);
        Assert.Equal(2, rows.Count);

        var selfRow = rows.First(r => r.IsSelf);
        var peerRow = rows.First(r => !r.IsSelf);

        // Peer joined late but has double self's rate
        Assert.InRange(selfRow.Dps, 9_800.0, 10_200.0);
        Assert.InRange(peerRow.Dps, 19_800.0, 20_200.0);
        Assert.True(peerRow.Dps > selfRow.Dps, "Late-joining peer with higher damage rate should show higher DPS");
    }

    // ── DPS survives coalescing (regression for the "Deadpool/Storm zero DPS" bug) ────────

    [Fact]
    public void AllLeaderboardRows_HavePositiveDps_AfterFight()
    {
        ulong self = 1, peer = 2, boss = 9001;
        RegisterSelf(self);
        _meter.TestRegisterHero(peer, "Cyclops");
        _meter.TestRegisterEntity(boss, BossProtoIdx);

        for (int i = 0; i < 30; i++)
        {
            _meter.TestInjectDamage(boss, self, 1u, 10_000, T(i));
            _meter.TestInjectDamage(boss, peer, 2u,  5_000, T(i));
        }

        _meter.TestInjectEntityKilled(boss, T(30));

        var rows = _meter.GetTopHeroesByEncounterShare(5);
        Assert.Equal(2, rows.Count);
        foreach (var r in rows)
            Assert.True(r.Dps > 0, $"Row '{r.Name}' has Dps={r.Dps} — should be > 0 after fight");
    }

    [Fact]
    public void DpsPostCoalesce_PetDamage_MergedIntoOwner()
    {
        // Regression: when a pet row (powerOwner != ultimateOwner) gets merged into the
        // owner row, the final DPS must be based on the combined total, not zeroed out.
        ulong self = 1, selfPet = 3, boss = 9001;
        RegisterSelf(self);
        _meter.TestRegisterEntity(boss, BossProtoIdx);

        // Avatar hits + pet hits (different powerOwner, same ultimateOwner = self)
        for (int i = 0; i < 30; i++)
        {
            _meter.TestInjectDamage(boss, self,    self, 1u, 10_000, T(i));  // avatar
            _meter.TestInjectDamage(boss, selfPet, self, 1u,  5_000, T(i));  // pet → credited to self
        }

        _meter.TestInjectEntityKilled(boss, T(30));

        var rows = _meter.GetTopHeroesByEncounterShare(5);
        Assert.Single(rows);   // pet folded into self → exactly one row

        var selfRow = rows[0];
        Assert.True(selfRow.IsSelf);
        // Combined total = (10k + 5k) × 30 = 450,000
        Assert.Equal(450_000L, selfRow.Total60s);
        // DPS = 450,000 / 30s = 15,000/s  (±2%)
        Assert.InRange(selfRow.Dps, 14_700.0, 15_300.0);
    }

    // ── Power breakdown ──────────────────────────────────────────────────────────────────

    [Fact]
    public void PowerBreakdown_ForSelf_ShowsHitsByPower()
    {
        ulong self = 1, boss = 9001;
        RegisterSelf(self);
        _meter.TestRegisterEntity(boss, BossProtoIdx);

        // Power 1: 10 hits × 10k = 100k
        for (int i = 0; i < 10; i++)
            _meter.TestInjectDamage(boss, self, 1u, 10_000, T(i));

        // Power 2: 5 hits × 20k = 100k
        for (int i = 10; i < 15; i++)
            _meter.TestInjectDamage(boss, self, 2u, 20_000, T(i));

        _meter.TestInjectEntityKilled(boss, T(15));

        var breakdown = _meter.GetPowerBreakdownForOwner(self, 10);
        Assert.Equal(2, breakdown.Count);
        Assert.All(breakdown, p => Assert.True(p.TotalDamage > 0));
        Assert.Equal(200_000L, breakdown.Sum(p => p.TotalDamage));
    }

    // ── Max-hit scopes (session / fight / record) ────────────────────────────────────────

    [Fact]
    public void MaxSingleHitSession_TracksBiggestHitAcrossAllEvents()
    {
        ulong self = 1, boss = 9001;
        RegisterSelf(self);
        _meter.TestRegisterEntity(boss, BossProtoIdx);

        _meter.TestInjectDamage(boss, self, 1u, 10_000, T(0));
        _meter.TestInjectDamage(boss, self, 1u, 25_000, T(1));   // <- biggest
        _meter.TestInjectDamage(boss, self, 1u, 18_000, T(2));

        Assert.Equal(25_000u, _meter.MaxSingleHitSession);
    }

    [Fact]
    public void MaxSingleHitEncounter_TracksFightLocalMaximum_AndResetsBetweenFights()
    {
        ulong self = 1, boss1 = 9001, boss2 = 9002;
        RegisterSelf(self);
        _meter.TestRegisterEntity(boss1, BossProtoIdx);
        _meter.TestRegisterEntity(boss2, BossProtoIdx);

        // Fight 1: peak 25k.
        _meter.TestInjectDamage(boss1, self, 1u, 10_000, T(0));
        _meter.TestInjectDamage(boss1, self, 1u, 25_000, T(1));
        Assert.Equal(25_000u, _meter.MaxSingleHitEncounter);
        Assert.Equal(25_000u, _meter.MaxSingleHitSession);

        _meter.TestInjectEntityKilled(boss1, T(2));

        // Re-register boss2 (kill purged the prototype cache for boss1).
        _meter.TestRegisterEntity(boss2, BossProtoIdx);

        // Fight 2: peak 15k — encounter scope must reset to 0 first, session must keep 25k.
        _meter.TestInjectDamage(boss2, self, 1u, 15_000, T(10));
        Assert.Equal(15_000u, _meter.MaxSingleHitEncounter);
        Assert.Equal(25_000u, _meter.MaxSingleHitSession);
    }

    [Fact]
    public void ResetSession_ClearsSessionAndEncounter_PreservesRecord()
    {
        ulong self = 1, boss = 9001;
        RegisterSelf(self);
        _meter.TestRegisterEntity(boss, BossProtoIdx);

        // Land a hit that becomes both the session high AND the all-time record.
        _meter.TestInjectDamage(boss, self, 1u, 50_000, T(0));
        Assert.Equal(50_000u, _meter.MaxSingleHit);
        Assert.Equal(50_000u, _meter.MaxSingleHitSession);
        Assert.Equal(50_000u, _meter.MaxSingleHitEncounter);

        _meter.ResetSession();

        // Session + encounter scopes wiped; persisted record re-seeded from disk so the
        // displayed PB doesn't go blank just because the user cleared the session.
        Assert.Equal(0u, _meter.MaxSingleHitSession);
        Assert.Equal(0u, _meter.MaxSingleHitEncounter);
        Assert.Equal(50_000u, _meter.MaxSingleHit);
    }

    [Fact]
    public void ResetSelfMaxHitRecord_ClearsRecordWithoutAffectingOtherScopes()
    {
        ulong self = 1, boss = 9001;
        RegisterSelf(self);
        _meter.TestRegisterEntity(boss, BossProtoIdx);

        _meter.TestInjectDamage(boss, self, 1u, 50_000, T(0));
        Assert.Equal(50_000u, _meter.MaxSingleHit);

        _meter.ResetSelfMaxHitRecord();

        Assert.Equal(0u, _meter.MaxSingleHit);
        // Runtime scopes survive — only the persisted PB was wiped.
        Assert.Equal(50_000u, _meter.MaxSingleHitSession);
        Assert.Equal(50_000u, _meter.MaxSingleHitEncounter);
    }

    [Fact]
    public void ResetSelfMaxHitRecord_NoHero_IsNoOp()
    {
        // No RegisterSelf — CurrentHeroDisplayName stays empty.  The method must short-circuit
        // and not touch the dictionary, fire events, or throw.
        _meter.ResetSelfMaxHitRecord();
        Assert.Equal(0u, _meter.MaxSingleHit);
    }

    // ── Leaderboard is empty when encounter has no boss damage ──────────────────────────

    [Fact]
    public void GetTopHeroesByEncounterShare_KnownNonBossTarget_ReturnsEmpty()
    {
        // Register the target with prototype index 1 which is NOT in BossPrototypes.Indices.
        // The boss filter will see the prototype, confirm it's not a boss, and drop the hit.
        // (Unknown entities are admitted optimistically by the boss filter to avoid missing
        // real bosses when the app attaches mid-fight — so we must use a *known* non-boss.)
        ulong self = 1, trash = 9999;
        const uint TrashProtoIdx = 1u;  // not in BossPrototypes.Indices
        RegisterSelf(self);
        _meter.TestRegisterEntity(trash, TrashProtoIdx);

        _meter.TestInjectDamage(trash, self, 1u, 10_000, T(0));

        var rows = _meter.GetTopHeroesByEncounterShare(5);
        Assert.Empty(rows);
    }
}
