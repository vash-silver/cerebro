namespace MarvelHeroesComporator.NetworkSniffer;

/// <summary>Mux command byte (matches MHServerEmu / Marvel Heroes client wire protocol).</summary>
internal enum MuxCommand : byte
{
    Invalid = 0,
    Connect = 1,
    ConnectAck = 2,
    Disconnect = 3,
    ConnectWithData = 4,
    Data = 5,
}

/// <summary>One fully-assembled Mux frame: 6-byte header + <see cref="Payload"/> bytes.</summary>
internal readonly struct MuxFrame
{
    public ushort MuxId { get; }
    public MuxCommand Command { get; }
    public byte[] Payload { get; }

    public MuxFrame(ushort muxId, MuxCommand command, byte[] payload)
    {
        MuxId = muxId;
        Command = command;
        Payload = payload;
    }
}

/// <summary>
/// Stateful parser that accepts arbitrary chunks of contiguous TCP-stream bytes and yields
/// complete Mux frames. One instance per direction of one TCP flow.
///
/// Wire format (little-endian, per CoreNetworkChannel::CreateMuxDataHeader):
///   2 bytes muxId | 3 bytes dataSize | 1 byte MuxCommand | dataSize bytes payload
/// </summary>
internal sealed class MuxStreamParser
{
    private const int HeaderSize = 6;

    private byte[] _pending = new byte[16 * 1024];
    private int _pendingLen;
    private bool _resyncing;

    public long FramesDecoded { get; private set; }

    /// <summary>Drop any buffered partial-frame bytes after a known stream gap (e.g. truncated TSO segment).</summary>
    public void Reset()
    {
        _pendingLen = 0;
        _resyncing = true;
    }

    public IEnumerable<MuxFrame> Feed(byte[] buffer, int offset, int length)
    {
        if (length <= 0) yield break;

        EnsureCapacity(_pendingLen + length);
        Buffer.BlockCopy(buffer, offset, _pending, _pendingLen, length);
        _pendingLen += length;

        int cursor = 0;
        while (true)
        {
            if (_pendingLen - cursor < HeaderSize) break;

            ushort muxId = (ushort)(_pending[cursor] | (_pending[cursor + 1] << 8));
            int dataSize = _pending[cursor + 2] | (_pending[cursor + 3] << 8) | (_pending[cursor + 4] << 16);
            byte rawCmd = _pending[cursor + 5];

            if (muxId is not (1 or 2) || rawCmd is < 1 or > 5 || dataSize > 8 * 1024 * 1024)
            {
                if (!_resyncing)
                    _resyncing = true;
                cursor++;
                continue;
            }

            int totalSize = HeaderSize + dataSize;
            if (_pendingLen - cursor < totalSize) break;

            byte[] payload = dataSize == 0 ? Array.Empty<byte>() : new byte[dataSize];
            if (dataSize > 0)
                Buffer.BlockCopy(_pending, cursor + HeaderSize, payload, 0, dataSize);

            cursor += totalSize;
            _resyncing = false;
            FramesDecoded++;
            yield return new MuxFrame(muxId, (MuxCommand)rawCmd, payload);
        }

        if (cursor > 0)
        {
            int remaining = _pendingLen - cursor;
            if (remaining > 0)
                Buffer.BlockCopy(_pending, cursor, _pending, 0, remaining);
            _pendingLen = remaining;
        }
    }

    private void EnsureCapacity(int needed)
    {
        if (_pending.Length >= needed) return;
        int newCap = _pending.Length;
        while (newCap < needed) newCap *= 2;
        var grown = new byte[newCap];
        Buffer.BlockCopy(_pending, 0, grown, 0, _pendingLen);
        _pending = grown;
    }
}
