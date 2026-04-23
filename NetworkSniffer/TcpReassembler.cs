using System.Net;

namespace MarvelHeroesComporator.NetworkSniffer;

/// <summary>Identifies one direction of a TCP flow.</summary>
internal readonly record struct FlowKey(IPAddress SrcIp, ushort SrcPort, IPAddress DstIp, ushort DstPort)
{
    public override string ToString() => $"{SrcIp}:{SrcPort} -> {DstIp}:{DstPort}";
}

/// <summary>
/// Per-direction TCP reassembly buffer: tracks the next expected sequence number, buffers
/// out-of-order segments, and emits contiguous bytes to a per-flow Mux parser.
///
///  - We initialize <see cref="_nextSeq"/> from the first segment we ever see in this direction.
///    Sniffing a connection that started before the sniffer was launched is therefore not perfectly
///    reliable until the next clean handshake — that's fine for our use case (just relog).
///  - We deduplicate retransmissions by seq.
///  - SortedDictionary keeps memory bounded: any out-of-order segment older than a few seconds
///    is dropped to avoid leaking on lossy/buggy connections.
/// </summary>
internal sealed class FlowState
{
    public readonly FlowKey Key;
    public readonly MuxStreamParser Parser = new();
    public readonly Action<FlowState, MuxFrame> OnFrame;
    public DateTime LastActivityUtc = DateTime.UtcNow;
    public bool DiscardThisFlow;
    public string Tag;

    private uint _nextSeq;
    private bool _initialized;
    private readonly SortedDictionary<uint, byte[]> _pending = new();

    /// <summary>
    /// Forcibly advance past a region of the stream whose bytes we will never see (typically
    /// because Npcap captured a TSO/LSO-aggregated segment whose payload was truncated). The Mux
    /// parser is reset because the next byte we feed will almost certainly be mid-frame.
    /// </summary>
    public void SkipAndResync(uint segmentSeq, uint declaredLen)
    {
        LastActivityUtc = DateTime.UtcNow;
        if (!_initialized) Initialize(segmentSeq);
        uint endSeq = segmentSeq + declaredLen;
        if (SeqLessThan(_nextSeq, endSeq))
        {
            _nextSeq = endSeq;
            _pending.Clear();
            Parser.Reset();
        }
    }

    public FlowState(FlowKey key, Action<FlowState, MuxFrame> onFrame, string tag)
    {
        Key = key;
        OnFrame = onFrame;
        Tag = tag;
    }

    public void Initialize(uint firstSeq)
    {
        _nextSeq = firstSeq;
        _initialized = true;
    }

    public int Feed(uint seq, byte[] payload)
    {
        LastActivityUtc = DateTime.UtcNow;
        if (DiscardThisFlow) return 0;
        if (payload.Length == 0) return 0;

        if (!_initialized)
            Initialize(seq);

        if (SeqLessThan(seq + (uint)payload.Length, _nextSeq))
            return 0;

        if (SeqLessThan(seq, _nextSeq))
        {
            uint skip = _nextSeq - seq;
            if (skip >= (uint)payload.Length) return 0;
            byte[] trimmed = new byte[payload.Length - skip];
            Buffer.BlockCopy(payload, (int)skip, trimmed, 0, trimmed.Length);
            payload = trimmed;
            seq = _nextSeq;
        }

        _pending[seq] = payload;

        int contiguous = 0;
        while (_pending.Count > 0)
        {
            var first = _pending.First();
            if (first.Key != _nextSeq) break;
            _pending.Remove(first.Key);

            foreach (var frame in Parser.Feed(first.Value, 0, first.Value.Length))
                OnFrame(this, frame);

            _nextSeq += (uint)first.Value.Length;
            contiguous += first.Value.Length;
        }

        if (_pending.Count > 256)
            _pending.Clear();

        return contiguous;
    }

    private static bool SeqLessThan(uint a, uint b) => unchecked((int)(a - b)) < 0;
}

/// <summary>Holds a state per (srcIp, srcPort, dstIp, dstPort) TCP flow.</summary>
internal sealed class TcpReassembler
{
    private readonly Dictionary<FlowKey, FlowState> _flows = new();
    private readonly Action<FlowState, MuxFrame> _onFrame;
    private readonly Action<string, FlowKey>? _onFlowEvent;
    private readonly object _lock = new();

    public TcpReassembler(Action<FlowState, MuxFrame> onFrame, Action<string, FlowKey>? onFlowEvent = null)
    {
        _onFrame = onFrame;
        _onFlowEvent = onFlowEvent;
    }

    public FlowState GetOrCreate(FlowKey key, string tag)
    {
        lock (_lock)
        {
            if (!_flows.TryGetValue(key, out var st))
            {
                st = new FlowState(key, _onFrame, tag);
                _flows[key] = st;
                _onFlowEvent?.Invoke("OPEN", key);
            }
            return st;
        }
    }

    public void Close(FlowKey key, string reason)
    {
        lock (_lock)
        {
            if (_flows.Remove(key))
                _onFlowEvent?.Invoke($"CLOSE ({reason})", key);
        }
    }

    public int FlowCount { get { lock (_lock) return _flows.Count; } }

    public void EvictIdleOlderThan(TimeSpan ttl)
    {
        var cutoff = DateTime.UtcNow - ttl;
        lock (_lock)
        {
            var stale = _flows.Where(kv => kv.Value.LastActivityUtc < cutoff).Select(kv => kv.Key).ToList();
            foreach (var k in stale)
            {
                _flows.Remove(k);
                _onFlowEvent?.Invoke("EVICT (idle)", k);
            }
        }
    }
}
