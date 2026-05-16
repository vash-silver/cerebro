using System;
using System.Buffers.Binary;
using System.IO;
using MarvelHeroesComporator.NetworkSniffer;
using Xunit;

namespace DpsMeterTests;

/// <summary>
/// Pins the wire-format math of <c>MhMissionSniffer.TryExtractStackCount</c> -- the parser
/// that reads <c>PropertyEnum.ItemCurrency</c> / <c>InventoryStackCount</c> out of an
/// EntityCreate's archiveData.
///
/// <para>The parser is wire-format-critical: a single bit-shift mistake means every splinter
/// drop reports the wrong quantity (or zero, and falls back to 1).  This file constructs
/// synthetic archives byte-for-byte the way MHServerEmu's <c>PropertyCollection.SerializeWithDefault</c>
/// does, so a regression in the parser fails CI rather than reaching the user.</para>
///
/// <para>Encoding recap (from MHServerEmu 1.0.1 source):
/// <list type="bullet">
///   <item>Replication header: varint <c>replicationPolicy</c> (we use 1 for AOIChannelProximity)</item>
///   <item>Property count: 4-byte LE uint (NOT a varint -- it's back-patched in the writer)</item>
///   <item>Each property: <c>varint(Raw.ReverseBytes())</c> then <c>varint(value bits)</c></item>
/// </list>
/// The byte-reverse on PropertyId is the easy-to-miss bit -- MHServerEmu does it to keep the
/// (typically small) enum value in the low byte where it varint-encodes in 1-3 bytes instead
/// of the 10 bytes needed when bit 63 is set.</para>
/// </summary>
public sealed class StackCountParserTests
{
    // ── PropertyEnum values, mirroring MHServerEmu.Games.Properties/PropertyEnum.cs ──────────
    private const uint ItemCurrencyEnum        = 540;
    private const uint InventoryStackCountEnum = 525;

    /// <summary>Builds a minimal entity archive containing exactly the property bag the
    /// sniffer's parser expects: a replication-policy varint, a 4-byte-LE property count, and
    /// the given (PropertyEnum, value) pairs serialized the same way the server does.</summary>
    private static byte[] BuildArchive(params (uint PropertyEnum, int Value)[] properties)
    {
        using var ms = new MemoryStream();
        // Replication policy varint -- value doesn't matter for the parser, just needs to be
        // a valid varint that ReadReplicationHeader will consume.  Pick 1 (AOIChannelProximity).
        WriteVarint(ms, 1);
        // ReplicatedPropertyCollection._replicationId varint -- ReplicatedPropertyCollection
        // overrides SerializeWithDefault to write this extra ulong before delegating to the
        // base PropertyCollection serialization in replication mode.  See
        // MHServerEmu.Games.Properties/ReplicatedPropertyCollection.cs:80.  The parser skips
        // it, so the synthetic archive needs it for the byte alignment to be correct.  Value
        // is arbitrary (a real server allocates this at bind time).
        WriteVarint(ms, 0x12345);
        // Property count -- fixed-width 4-byte little-endian uint (not a varint).
        Span<byte> countBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(countBytes, (uint)properties.Length);
        ms.Write(countBytes);
        foreach (var (propEnum, value) in properties)
        {
            // Build PropertyId.Raw with no params: top 11 bits hold the enum, low 53 are 0.
            ulong propertyIdRaw = (ulong)propEnum << 53;
            // MHServerEmu's Serializer.Transfer(ref PropertyId) byte-reverses Raw before
            // varint-encoding.  Replicate that exactly so the parser sees what it would on
            // the wire.
            ulong wireValue = BinaryPrimitives.ReverseEndianness(propertyIdRaw);
            WriteVarint(ms, wireValue);
            // Value bits: integer-type properties go through MathHelper.SwizzleSignBit, which
            // for positive ints is value << 1.
            ulong valueBits = unchecked((ulong)(value << 1));
            WriteVarint(ms, valueBits);
        }
        return ms.ToArray();
    }

    private static void WriteVarint(Stream s, ulong v)
    {
        while (v >= 0x80)
        {
            s.WriteByte((byte)(v | 0x80));
            v >>= 7;
        }
        s.WriteByte((byte)v);
    }

    [Fact]
    public void ItemCurrency_RoundTrips_ThroughByteReverseAndVarint()
    {
        // The canonical splinter case: one ItemCurrency property carrying the drop quantity.
        // Tests the byte-reverse path of the parser end-to-end -- if BinaryPrimitives.ReverseEndianness
        // were dropped or replaced with a no-op, this would fail (parser would never match the
        // 540 enum and return stackCount=0, fall-back-to-1 territory).
        byte[] archive = BuildArchive((ItemCurrencyEnum, 10));
        Assert.True(MhMissionSniffer.TestTryExtractStackCount(archive, out int stackCount));
        Assert.Equal(10, stackCount);
    }

    [Fact]
    public void ItemCurrency_PreferredOverInventoryStackCount_WhenBothPresent()
    {
        // Currency drops carry BOTH properties: InventoryStackCount=1 (hardcoded by
        // LootManager.SpawnItemInternal for every currency drop) AND ItemCurrency=<amount>
        // (the actual quantity from CurrencySpec.ApplyCurrency).  The parser must prefer
        // ItemCurrency or it'll report 1 splinter for every drop regardless of amount --
        // which is exactly the bug the user reported.
        byte[] archive = BuildArchive(
            (InventoryStackCountEnum, 1),
            (ItemCurrencyEnum,       14));
        Assert.True(MhMissionSniffer.TestTryExtractStackCount(archive, out int stackCount));
        Assert.Equal(14, stackCount);
    }

    [Fact]
    public void InventoryStackCount_UsedAsFallback_WhenNoItemCurrency()
    {
        // Regular stackable item (a potion etc) -- no currency property, just a stack count.
        // Parser should fall back to InventoryStackCount.
        byte[] archive = BuildArchive((InventoryStackCountEnum, 5));
        Assert.True(MhMissionSniffer.TestTryExtractStackCount(archive, out int stackCount));
        Assert.Equal(5, stackCount);
    }

    [Fact]
    public void NoMatchingProperty_ReturnsFalseAndZero()
    {
        // Entity with properties but neither InventoryStackCount nor ItemCurrency.  Parser
        // should report no extraction and leave the caller free to fall back to "1 splinter".
        // Use arbitrary enum values that aren't 525 or 540.
        byte[] archive = BuildArchive(
            (100u, 42),
            (200u, 99));
        Assert.False(MhMissionSniffer.TestTryExtractStackCount(archive, out int stackCount));
        Assert.Equal(0, stackCount);
    }

    [Fact]
    public void EmptyPropertyCollection_ReturnsFalseAndZero()
    {
        // Archive with zero properties (numProperties = 0) -- parser must handle the empty
        // case cleanly without misreading bytes past the count as a property entry.
        byte[] archive = BuildArchive();
        Assert.False(MhMissionSniffer.TestTryExtractStackCount(archive, out int stackCount));
        Assert.Equal(0, stackCount);
    }

    [Fact]
    public void TruncatedArchive_ReturnsFalseRatherThanCrashing()
    {
        // Anything shorter than the minimum (header + count) is rejected.  This is the
        // sanity-bound short-circuit; truncated archives should never propagate exceptions
        // out of the parser (they'd kill the caller's whole EntityCreate handler).
        Assert.False(MhMissionSniffer.TestTryExtractStackCount(new byte[] { 0x01 }, out int stackCount));
        Assert.Equal(0, stackCount);
    }

    [Fact]
    public void TenSplinters_TheCanonicalUserReportedCase()
    {
        // Pin the exact scenario that drove the byte-reverse fix.  User reported "10 Eternity
        // Splinters" in-game showing as "1 splinters · 1 drop" because the parser kept hitting
        // stackCount=0 and falling back to 1.  This test simulates that exact wire payload:
        // an Item entity with InventoryStackCount=1 (the visual ground-stack) and
        // ItemCurrency=10 (the actual splinter amount).  Parser MUST return 10.
        byte[] archive = BuildArchive(
            (InventoryStackCountEnum, 1),
            (ItemCurrencyEnum,       10));
        Assert.True(MhMissionSniffer.TestTryExtractStackCount(archive, out int stackCount));
        Assert.Equal(10, stackCount);
    }
}
