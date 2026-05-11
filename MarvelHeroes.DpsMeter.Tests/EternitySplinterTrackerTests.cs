using System;
using MarvelHeroes.DpsMeter.Services;
using Xunit;

namespace DpsMeterTests;

/// <summary>
/// Unit tests for <see cref="EternitySplinterTracker"/>.  The tracker uses real wall-clock
/// time internally (no injectable clock), so timing-sensitive assertions are written as
/// "small-enough-window after the action" rather than precise-second comparisons.  We rely
/// on the public surface only (no reflection); the sniffer is passed as <c>null</c> for these
/// tests since we drive the tracker directly via <see cref="EternitySplinterTracker.AddKnownProtoRef"/>
/// + <see cref="EternitySplinterTracker.ArmFromNow"/> instead of going through real events.
/// </summary>
public sealed class EternitySplinterTrackerTests
{
    // A made-up proto ref we know isn't in DefaultKnownProtoRefs so we can verify both
    // "matched" and "ignored" code paths.  11087194553833821680 IS in the default set --
    // the unknownRef constant below is deliberately different.
    private const ulong KnownSplinterProtoRef = 11087194553833821680uL;
    private const ulong UnknownProtoRef       = 99999999999999999uL;

    [Fact]
    public void DefaultKnownProtoRefs_ContainsTheCanonicalSplinter()
    {
        // Smoke check the hardcoded constant -- if someone bumps it accidentally during a
        // refactor this test will catch the regression before a player sees broken detection.
        Assert.Contains(KnownSplinterProtoRef, EternitySplinterTracker.DefaultKnownProtoRefs);
    }

    [Fact]
    public void CooldownDuration_Is6Minutes()
    {
        // The display countdown is intentionally 1 min shorter than the server-side throttle
        // (~7 min): rather than show "0:00 -- eligible" while the server is still rejecting
        // drop rolls, the timer expires a hair early so the user knows they can start trying.
        // Pinned via test so a future tuning ("let's push it to 5 min") is a deliberate
        // change and not an accidental drift.
        Assert.Equal(TimeSpan.FromMinutes(6), EternitySplinterTracker.CooldownDuration);
    }

    [Fact]
    public void InitialState_NoDropDetected_CooldownInactive()
    {
        using var t = new EternitySplinterTracker(null);
        Assert.False(t.IsCooldownActive);
        Assert.Equal(TimeSpan.Zero, t.RemainingCooldown);
        Assert.Equal(DateTime.MinValue, t.LastDropUtc);
        Assert.Equal(DateTime.MinValue, t.CooldownEndUtc);
        Assert.Equal(0, t.DropCount);
    }

    [Fact]
    public void ArmFromNow_ActivatesCooldown_FiresEvent()
    {
        using var t = new EternitySplinterTracker(null);
        SplinterDroppedEventArgs? captured = null;
        t.SplinterDropped += (_, args) => captured = args;

        var before = DateTime.UtcNow;
        t.ArmFromNow();
        var after  = DateTime.UtcNow;

        Assert.True(t.IsCooldownActive);
        // Remaining should be (almost) the full CooldownDuration -- allow 2s of slack for slow CI.
        Assert.InRange(
            t.RemainingCooldown,
            EternitySplinterTracker.CooldownDuration - TimeSpan.FromSeconds(2),
            EternitySplinterTracker.CooldownDuration + TimeSpan.FromSeconds(1));
        Assert.InRange(t.LastDropUtc, before, after);
        Assert.Equal(1, t.DropCount);
        Assert.NotNull(captured);
        Assert.True(captured!.Manual);
    }

    [Fact]
    public void Reset_ClearsCooldownState()
    {
        using var t = new EternitySplinterTracker(null);
        t.ArmFromNow();
        Assert.True(t.IsCooldownActive);

        t.Reset();
        Assert.False(t.IsCooldownActive);
        Assert.Equal(DateTime.MinValue, t.LastDropUtc);
        // DropCount is a session counter -- intentionally NOT reset so the "5 splinters
        // today" badge keeps counting across a single user-initiated reset.
        Assert.Equal(1, t.DropCount);
    }

    [Fact]
    public void MultipleArms_AccumulateDropCount_RefreshCooldown()
    {
        using var t = new EternitySplinterTracker(null);
        t.ArmFromNow();
        var firstDropUtc = t.LastDropUtc;

        // Spin for a moment so the second drop has a measurably later timestamp.
        System.Threading.Thread.Sleep(20);
        t.ArmFromNow();

        Assert.Equal(2, t.DropCount);
        Assert.True(t.LastDropUtc > firstDropUtc, "second drop should refresh LastDropUtc to a later value");
    }

    [Fact]
    public void Tick_NoDrop_DoesNotFireCooldownExpired()
    {
        using var t = new EternitySplinterTracker(null);
        bool fired = false;
        t.CooldownExpired += (_, _) => fired = true;

        t.Tick();
        t.Tick();
        t.Tick();

        Assert.False(fired);
    }

    [Fact]
    public void AddKnownProtoRef_LetsCallerExtendTheMatchSetAtRuntime()
    {
        using var t = new EternitySplinterTracker(null);
        // No exception, idempotent on duplicates.
        t.AddKnownProtoRef(UnknownProtoRef);
        t.AddKnownProtoRef(UnknownProtoRef);
        // Adding the canonical default again is a no-op -- DefaultKnownProtoRefs has it already.
        t.AddKnownProtoRef(KnownSplinterProtoRef);
    }

    [Fact]
    public void AddKnownProtoIndex_LetsCallerExtendTheEnumIndexMatchSetAtRuntime()
    {
        // Enum-index path mirrors the proto-ref one -- this test is the smoke check that
        // the second extension API doesn't throw / is idempotent.  Real matching is verified
        // empirically once the splinter's enum index is discovered from a live session.
        using var t = new EternitySplinterTracker(null);
        t.AddKnownProtoIndex(12345u);
        t.AddKnownProtoIndex(12345u);
        t.AddKnownProtoIndex(99999u);
    }

    [Fact]
    public void DefaultKnownProtoIndices_ContainsTheEmpiricallyDiscoveredSplinterIndex()
    {
        // Smoke check: 13341 was the strongest-pattern intersection candidate across two
        // discovery-log sessions (lone EntityCreate ~1s after a mob-kill loot burst, present
        // in both sessions, not in any known mob/boss/hero list).  If a refactor accidentally
        // drops or changes this value the tracker silently goes back to never firing on
        // EntityCreated, so we pin it.  Update the constant -- and this test -- if a future
        // session proves a different index is the right one.
        Assert.Contains(13341u, EternitySplinterTracker.DefaultKnownProtoIndices);
    }

    [Fact]
    public void EntityCreate_WhileCooldownActive_IsSuppressedAsFalsePositive()
    {
        // The server's per-player splinter throttle is ~7 min, so any "drop" detection
        // arriving inside the cooldown window we just started has to be a false positive
        // (the proto index is shared with some non-splinter entity).  Verify that the
        // SECOND EntityCreate matching the splinter index, while the cooldown is still
        // active, neither fires SplinterDropped nor bumps DropCount.
        using var t = new EternitySplinterTracker(null);
        int dropEvents = 0;
        t.SplinterDropped += (_, _) => dropEvents++;

        // First create with the canonical index -- this IS the legit drop.
        t.TestInjectEntityCreate(protoIdx: 13341u, entityId: 1, isAvatar: false, utc: DateTime.UtcNow);
        Assert.Equal(1, dropEvents);
        Assert.Equal(1, t.DropCount);
        Assert.True(t.IsCooldownActive);

        // Second create -- 42 seconds later, well inside the cooldown.  This is the user's
        // reported false-positive pattern (16:49:29 → 16:50:11 in the field log).
        t.TestInjectEntityCreate(protoIdx: 13341u, entityId: 2, isAvatar: false, utc: DateTime.UtcNow);
        Assert.Equal(1, dropEvents);   // <-- still 1; suppressed
        Assert.Equal(1, t.DropCount);  // <-- still 1; suppressed
    }

    [Fact]
    public void EntityCreate_AfterCooldownExpires_FiresNormally()
    {
        // Counter-test: once the cooldown window has elapsed, a matching EntityCreate
        // SHOULD fire as normal.  Use Reset() to clear the gate (faster than waiting 6
        // minutes in a unit test); semantically equivalent to "the cooldown timer hit
        // zero".  Without this counter-test the suppression could silently degrade into
        // "never fires at all" and the prior test would still pass.
        using var t = new EternitySplinterTracker(null);
        int dropEvents = 0;
        t.SplinterDropped += (_, _) => dropEvents++;

        t.TestInjectEntityCreate(protoIdx: 13341u, entityId: 1, isAvatar: false, utc: DateTime.UtcNow);
        Assert.Equal(1, dropEvents);
        t.Reset();
        Assert.False(t.IsCooldownActive);

        t.TestInjectEntityCreate(protoIdx: 13341u, entityId: 2, isAvatar: false, utc: DateTime.UtcNow);
        Assert.Equal(2, dropEvents);
    }
}
