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
    public void CooldownDuration_Is7Minutes()
    {
        Assert.Equal(TimeSpan.FromMinutes(7), EternitySplinterTracker.CooldownDuration);
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
        // Remaining should be (almost) the full 7 minutes -- allow 2s of slack for slow CI.
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
}
