using Google.ProtocolBuffers;

namespace MarvelHeroesComporator.NetworkSniffer;

/// <summary>
/// Reader for the custom binary archive format used inside many server-to-client and client-to-server
/// Marvel Heroes / MHServerEmu messages (<c>bytes archiveData = 1;</c>).  It is NOT stock protobuf —
/// the server's own <c>MHServerEmu.Core.Serialization.Archive</c> class writes VarInt-encoded integers
/// interleaved with a 5-bool packed bit-buffer.  The subset implemented here is what we need to read
/// <see cref="Gazillion.NetMessagePowerResult"/> (damage/healing/flags) and the <c>baseData</c> of
/// <see cref="Gazillion.NetMessageEntityCreate"/> (entityId + prototype-enum index) — i.e. the parts
/// with no nested <c>ISerialize</c> objects.
/// </summary>
/// <remarks>
/// <para>
/// Encoding rules (reverse-engineered from <c>Archive.cs</c> in EmuSource, replication mode):
/// </para>
/// <list type="bullet">
///   <item>
///     <b>Integers</b> (<c>uint</c>, <c>ulong</c>) — <see cref="CodedInputStream.ReadRawVarint32"/> /
///     <c>ReadRawVarint64</c>. Signed <c>int</c>/<c>long</c> are ZigZag-encoded on top of the VarInt.
///   </item>
///   <item>
///     <b>Booleans</b> — packed up to 5 per byte. Byte layout: <c>bbbbb_nnn</c> where <c>nnn</c> is
///     the count of encoded bools (1–5) in the low 3 bits, <c>bbbbb</c> is the bit payload with
///     bool #0 in bit 7, #1 in bit 6, … MSB-first. Subsequent bool reads keep popping bits from the
///     same byte until the count is exhausted, then the next call fetches a fresh byte.
///   </item>
///   <item>
///     <b>Prototype-enum references</b> (<c>TransferPrototypeEnum&lt;T&gt;</c>) — serialized as a
///     plain VarInt <c>uint</c> index into the client's DataDirectory.  Without the client table we
///     can't map that back to a PrototypeId, but we CAN <see cref="SkipPrototypeEnum"/> the index
///     so the next field stays aligned.
///   </item>
///   <item>
///     <b>Vector3 fixed-point</b> (<c>TransferVectorFixed</c>) — three ZigZag-encoded <c>int</c>
///     VarInts, each divided by <c>2^precision</c> to recover a float.
///   </item>
/// </list>
/// <para>
/// NOT implemented (yet): size-checked <c>ISerialize</c> skipping, orientation fixed-point,
/// string transfer, list transfer, migration-mode quirks. Extend as more archives need reading.
/// </para>
/// </remarks>
internal sealed class GazillionArchiveReader
{
    private readonly CodedInputStream _cis;
    /// <summary>Total length of the source archive (captured at construction, never mutates) — used to
    /// expose a reliable "at end" predicate even though the older <c>Google.ProtocolBuffers</c> build shipped
    /// with EmuSource does not expose <c>IsAtEnd</c> on every overload.</summary>
    private readonly int _totalLength;

    /// <summary>Rolling 1-byte bit-buffer that stores up to 5 packed bool payloads + count in low 3 bits.</summary>
    private byte _bitBuffer;
    /// <summary>How many bool reads we've already consumed from <see cref="_bitBuffer"/>. Reset when the byte is exhausted.</summary>
    private byte _bitsRead;

    public GazillionArchiveReader(byte[] archiveBytes)
    {
        if (archiveBytes is null) throw new ArgumentNullException(nameof(archiveBytes));
        _cis = CodedInputStream.CreateInstance(archiveBytes);
        _totalLength = archiveBytes.Length;
    }

    /// <summary>The replication policy VarInt that <c>Archive.WriteHeader</c> prepends to every
    /// replication-mode archive. Populated by <see cref="ReadReplicationHeader"/>; remains 0 until
    /// then. Example value 1 == <c>AOIChannelProximity</c>.</summary>
    public ulong ReplicationPolicy { get; private set; }

    /// <summary>
    /// Consumes the leading <c>replicationPolicy</c> VarInt that <c>MHServerEmu.Core.Serialization.Archive</c>
    /// emits at the start of every replication-mode archive (see <c>Archive.WriteHeader</c>). Callers
    /// that parse replication archives MUST invoke this before reading any payload fields — otherwise
    /// every subsequent field is offset by one slot.  Migration-mode archives use a different header
    /// and should not call this method.
    /// </summary>
    public void ReadReplicationHeader()
    {
        ReplicationPolicy = _cis.ReadRawVarint64();
    }

    /// <summary>Byte offset of the next byte to be read from the source archive (for diagnostics).</summary>
    public int CurrentOffset => (int)_cis.Position;

    /// <summary>True once every byte in the original archive has been consumed.</summary>
    public bool IsAtEnd => _cis.Position >= _totalLength;

    // ── Integers ─────────────────────────────────────────────────────────────────────────────────
    // All integer reads bypass the bit-buffer entirely — the buffer only exists for bool transfers.
    // That matches EmuSource semantics: a bool write/read stashes into/reads from the 5-bit byte,
    // any other transfer emits/consumes a fresh VarInt on the main CodedInputStream.

    public uint ReadVarUInt32() => _cis.ReadRawVarint32();
    public ulong ReadVarUInt64() => _cis.ReadRawVarint64();

    public int ReadVarInt32()
    {
        uint raw = _cis.ReadRawVarint32();
        // ZigZag: (raw >> 1) XOR -(raw & 1). Converts unsigned VarInt back to signed, so that small
        // negative numbers encode as 1/3/5/… and small positives as 0/2/4/… — one byte each for |x|<64.
        return CodedInputStream.DecodeZigZag32(raw);
    }

    public long ReadVarInt64()
    {
        ulong raw = _cis.ReadRawVarint64();
        return CodedInputStream.DecodeZigZag64(raw);
    }

    // ── Booleans ─────────────────────────────────────────────────────────────────────────────────
    // Mirrors Archive.DecodeBoolFromByte(): on the first call after the previous byte was exhausted
    // we grab a fresh byte from the stream; otherwise we keep peeling bits from the retained buffer.

    /// <summary>
    /// Reads one packed bool from the archive. See class-level remarks for the layout; the first
    /// bool in a byte lives in bit 7, the 5th in bit 3, and the low 3 bits hold the remaining-count.
    /// </summary>
    public bool ReadBool()
    {
        // Fetch a new byte when the buffer is exhausted (or before the very first bool read).
        if (_bitBuffer == 0)
        {
            _bitBuffer = _cis.ReadRawByte();
            _bitsRead = 0;
        }

        int numRemaining = _bitBuffer & 0x7;
        // Extract bit at the next unread slot (MSB-first: first bool at bit 7, second at bit 6, …).
        bool value = (_bitBuffer & (1 << (7 - _bitsRead))) != 0;

        // Decrement the remaining-count in the low 3 bits; if it hits zero the byte is spent and the
        // next ReadBool() triggers a fresh fetch above. _bitsRead advances so the next bit comes
        // from the correct slot.
        numRemaining--;
        _bitBuffer &= 0xF8;
        _bitBuffer |= (byte)numRemaining;
        _bitsRead++;

        if (numRemaining == 0)
        {
            _bitBuffer = 0;
            _bitsRead = 0;
        }

        return value;
    }

    // ── Floats & vectors ─────────────────────────────────────────────────────────────────────────

    public float ReadFloat()
    {
        uint raw = _cis.ReadRawVarint32();
        return BitConverter.Int32BitsToSingle((int)raw);
    }

    /// <summary>
    /// Reads a float encoded as ZigZag-VarInt divided by 2^precision.  Matches
    /// <c>Serializer.TransferFloatFixed(archive, ref f, precision)</c>.
    /// </summary>
    public float ReadFloatFixed(int precision)
    {
        int raw = ReadVarInt32();
        return raw / (float)(1 << precision);
    }

    /// <summary>
    /// Reads a Vector3 as three ZigZag-VarInt-fixed floats (X, Y, Z), matching
    /// <c>Serializer.TransferVectorFixed(archive, ref vec, precision)</c>.
    /// </summary>
    public (float X, float Y, float Z) ReadVector3Fixed(int precision)
    {
        float x = ReadFloatFixed(precision);
        float y = ReadFloatFixed(precision);
        float z = ReadFloatFixed(precision);
        return (x, y, z);
    }

    // ── Prototype enum ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Reads the raw enum-index emitted by <c>TransferPrototypeEnum&lt;T&gt;</c>.  The returned value
    /// is a dense table index into the client's DataDirectory for prototype type <c>T</c> — useless
    /// without that table, but we read it to stay aligned.  Use <see cref="SkipPrototypeEnum"/> when
    /// the value itself is not needed.
    /// </summary>
    public uint ReadPrototypeEnumIndex() => _cis.ReadRawVarint32();

    /// <summary>Skips the prototype-enum field (reads + discards a VarInt).</summary>
    public void SkipPrototypeEnum() => _cis.ReadRawVarint32();

    // ── Raw bytes ────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Reads <paramref name="count"/> raw bytes from the current position. Useful for skipping
    /// opaque tails (e.g. a trailing condition list we don't care about) or for hex-dumping an
    /// unknown archive region during discovery.
    /// </summary>
    public byte[] ReadRawBytes(int count) => _cis.ReadRawBytes(count);
}
