using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using PanguEngine.Network.Packets;

namespace PanguEngine.Network;

/// <summary>Shares a pair of packet queues between the client and integrated server.</summary>
internal sealed class InMemoryConnection : IClientConnection, IServerConnection
{
    private readonly object _gate;
    private readonly Queue<ServerboundPacket> _serverboundPackets = [];
    private readonly Queue<ClientboundPacket> _clientboundPackets = [];
    private bool _completed;
    private bool _acceptingRequests = true;
    private ExceptionDispatchInfo? _fault;

    internal InMemoryConnection(object? synchronization = null)
    {
        _gate = synchronization ?? new object();
    }

    public bool TrySend(ServerboundPacket packet)
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

    public bool IsClosed
    {
        get { lock (_gate) return _completed; }
    }

    public int IncomingCount
    {
        get { lock (_gate) return _serverboundPackets.Count; }
    }

    bool IServerConnection.TryReceive([NotNullWhen(true)] out ServerboundPacket? packet)
    {
        lock (_gate)
            return _serverboundPackets.TryDequeue(out packet);
    }

    /// <summary>Stops accepting new requests and optionally discards queued requests.</summary>
    /// <remarks>The server stops acceptance while holding the shared synchronization gate.</remarks>
    internal void StopAcceptingRequests(bool clearPending)
    {
        _acceptingRequests = false;
        if (clearPending)
            _serverboundPackets.Clear();
    }

    internal bool Publish(ClientboundPacket packet)
    {
        lock (_gate)
        {
            if (_completed)
                return false;
            _clientboundPackets.Enqueue(packet);
            Monitor.PulseAll(_gate);
            return true;
        }
    }

    bool IServerConnection.TrySend(ClientboundPacket packet) => Publish(packet);

    void IServerConnection.CompleteSending(string reason) => Complete();

    public bool TryReceive([NotNullWhen(true)] out ClientboundPacket? packet)
    {
        lock (_gate)
        {
            _fault?.Throw();
            return _clientboundPackets.TryDequeue(out packet);
        }
    }

    public ClientboundPacket WaitForPacket(TimeSpan? timeout = null)
    {
        var start = Stopwatch.GetTimestamp();
        lock (_gate)
        {
            while (_clientboundPackets.Count == 0 && !_completed)
            {
                if (timeout is { } limit)
                {
                    var remaining = limit - Stopwatch.GetElapsedTime(start);
                    if (remaining <= TimeSpan.Zero || !Monitor.Wait(_gate, remaining))
                        throw new ConnectionClosedException("Timed out waiting for the local server.");
                }
                else
                    Monitor.Wait(_gate);
            }
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
            if (error is not null)
                _fault = ExceptionDispatchInfo.Capture(error);
            _completed = true;
            Monitor.PulseAll(_gate);
        }
    }

    public void Close()
    {
        lock (_gate)
        {
            _serverboundPackets.Clear();
            _completed = true;
            Monitor.PulseAll(_gate);
        }
    }

    public void ThrowIfFaulted()
    {
        lock (_gate)
            _fault?.Throw();
    }
}
