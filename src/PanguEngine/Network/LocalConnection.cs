using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;
using PanguEngine.Network.Packets;

namespace PanguEngine.Network;

internal sealed class LocalConnection
{
    private readonly object _gate;
    private readonly Queue<ServerboundPacket> _serverboundPackets = [];
    private readonly Queue<ClientboundPacket> _clientboundPackets = [];
    private long _requestEpoch;
    private bool _completed;
    private bool _acceptingRequests = true;
    private ExceptionDispatchInfo? _fault;

    internal LocalConnection(object? synchronization = null)
    {
        _gate = synchronization ?? new object();
    }

    internal (bool IsAccepting, long Epoch) RequestState
    {
        get
        {
            lock (_gate)
                return (_acceptingRequests && !_completed, _requestEpoch);
        }
    }

    internal bool TrySend(ServerboundPacket packet)
    {
        lock (_gate)
        {
            _fault?.Throw();
            if (_completed || !_acceptingRequests)
                return false;
            _serverboundPackets.Enqueue(packet);
            return true;
        }
    }

    /// <summary>Drains pending serverbound packets into the supplied list.</summary>
    /// <remarks>During concurrent access, the caller must hold the shared synchronization gate.</remarks>
    internal void DrainServerboundPackets(List<ServerboundPacket> packets)
    {
        packets.AddRange(_serverboundPackets);
        _serverboundPackets.Clear();
    }

    /// <summary>Changes request acceptance and optionally discards pending requests.</summary>
    /// <remarks>During concurrent access, the caller must hold the shared synchronization gate.</remarks>
    internal void SetAcceptingRequests(bool accepting, bool clearPending)
    {
        if (_acceptingRequests != accepting || clearPending)
            _requestEpoch++;
        _acceptingRequests = accepting;
        if (clearPending)
            _serverboundPackets.Clear();
    }

    internal void Publish(ClientboundPacket packet)
    {
        lock (_gate)
        {
            _clientboundPackets.Enqueue(packet);
            Monitor.PulseAll(_gate);
        }
    }

    internal bool TryReceive([NotNullWhen(true)] out ClientboundPacket? packet)
    {
        lock (_gate)
        {
            _fault?.Throw();
            return _clientboundPackets.TryDequeue(out packet);
        }
    }

    internal ClientboundPacket WaitForPacket()
    {
        lock (_gate)
        {
            while (_clientboundPackets.Count == 0 && !_completed)
                Monitor.Wait(_gate);
            _fault?.Throw();
            if (_clientboundPackets.TryDequeue(out var packet))
                return packet;
            throw new InvalidOperationException("The local server has stopped.");
        }
    }

    internal void Complete(Exception? error = null)
    {
        lock (_gate)
        {
            _fault = error is null ? null : ExceptionDispatchInfo.Capture(error);
            _completed = true;
            Monitor.PulseAll(_gate);
        }
    }

    internal void ThrowIfFaulted()
    {
        lock (_gate)
            _fault?.Throw();
    }
}
