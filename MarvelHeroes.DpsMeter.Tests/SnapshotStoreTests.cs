using System;
using System.Linq;
using MarvelHeroes.DpsMeter.Models;
using Xunit;

namespace DpsMeterTests;

/// <summary>
/// Smoke tests for DpsReportStore and PersonalBestStore.
/// These tests perform real disk I/O under %LocalAppData%\MarvelHeroesComporator\ but
/// use GUIDs for IDs / hero names so they never collide with real user data and always
/// clean up after themselves.
/// </summary>
public sealed class SnapshotStoreTests
{
    // ── DpsReportStore ────────────────────────────────────────────────────────────────────

    [Fact]
    public void DpsReportStore_SaveLoadDelete_RoundTrip()
    {
        string id = Guid.NewGuid().ToString("N");
        var snap = new DpsSnapshot
        {
            Id          = id,
            SavedUtc    = DateTime.UtcNow,
            Label       = "unit test fight",
            Mode        = "Boss",
            HeroName    = "Iron Man",
            Dps         = 12_345.6,
            TotalDamage = 1_000_000,
            IsAutoSave  = false,
            Leaderboard = new()
            {
                new DpsSnapshot.HeroEntry
                {
                    Name   = "Iron Man",
                    IsSelf = true,
                    Dps    = 12_345.6,
                    Total  = 1_000_000,
                }
            },
        };

        try
        {
            DpsReportStore.Save(snap);

            var all   = DpsReportStore.LoadAll();
            var found = all.FirstOrDefault(s => s.Id == id);

            Assert.NotNull(found);
            Assert.Equal("unit test fight", found.Label);
            Assert.Equal("Iron Man",        found.HeroName);
            Assert.Equal(12_345.6,          found.Dps);
            Assert.Single(found.Leaderboard);
            Assert.True(found.Leaderboard[0].IsSelf);
        }
        finally
        {
            DpsReportStore.Delete(id);
            // Verify the file was removed
            Assert.DoesNotContain(DpsReportStore.LoadAll(), s => s.Id == id);
        }
    }

    [Fact]
    public void DpsReportStore_UpdateLabel_PersistsChange()
    {
        string id = Guid.NewGuid().ToString("N");
        var snap = new DpsSnapshot
        {
            Id       = id,
            SavedUtc = DateTime.UtcNow,
            Label    = "original label",
            HeroName = "Cyclops",
        };

        try
        {
            DpsReportStore.Save(snap);
            DpsReportStore.UpdateLabel(id, "renamed label");

            var found = DpsReportStore.LoadAll().FirstOrDefault(s => s.Id == id);
            Assert.NotNull(found);
            Assert.Equal("renamed label", found.Label);
        }
        finally
        {
            DpsReportStore.Delete(id);
        }
    }

    // ── PersonalBestStore ─────────────────────────────────────────────────────────────────

    [Fact]
    public void PersonalBestStore_FirstRecord_ReturnsTrue()
    {
        // GUIDed hero name so it's guaranteed to have no prior record.
        string hero = $"TestHero_{Guid.NewGuid():N}";
        bool result = PersonalBestStore.CheckAndUpdate(hero, 50_000.0);
        Assert.True(result);
    }

    [Fact]
    public void PersonalBestStore_HigherScore_ReturnsTrue()
    {
        string hero = $"TestHero_{Guid.NewGuid():N}";
        PersonalBestStore.CheckAndUpdate(hero, 50_000.0);   // establish record
        bool result = PersonalBestStore.CheckAndUpdate(hero, 75_000.0);
        Assert.True(result);
    }

    [Fact]
    public void PersonalBestStore_LowerScore_ReturnsFalse()
    {
        string hero = $"TestHero_{Guid.NewGuid():N}";
        PersonalBestStore.CheckAndUpdate(hero, 50_000.0);   // establish record
        bool result = PersonalBestStore.CheckAndUpdate(hero, 49_999.0);
        Assert.False(result);
    }

    [Fact]
    public void PersonalBestStore_EqualScore_ReturnsFalse()
    {
        string hero = $"TestHero_{Guid.NewGuid():N}";
        PersonalBestStore.CheckAndUpdate(hero, 50_000.0);
        bool result = PersonalBestStore.CheckAndUpdate(hero, 50_000.0);
        Assert.False(result);
    }

    [Fact]
    public void PersonalBestStore_EmptyHeroName_ReturnsFalse()
    {
        bool result = PersonalBestStore.CheckAndUpdate("", 99_999.0);
        Assert.False(result);
    }

    [Fact]
    public void PersonalBestStore_ZeroDps_ReturnsFalse()
    {
        string hero = $"TestHero_{Guid.NewGuid():N}";
        bool result = PersonalBestStore.CheckAndUpdate(hero, 0.0);
        Assert.False(result);
    }
}
