using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using PanguEngine.Network.Serialization;

namespace PanguEngine.Network;

internal sealed class TcpConnection
{
    private readonly object _gate = new();
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly FrameQueue _incoming = new();
    private readonly FrameQueue _outgoing = new();
    private readonly SemaphoreSlim _sendSignal = new(0);
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _receiveTask;
    private readonly Task _sendTask;
    private ExceptionDispatchInfo? _fault;
    private bool _closed;
    private bool _draining;
    private bool _heartbeat;
    private string? _reason;

    internal long ConnectedAt { get; } = Stopwatch.GetTimestamp();

    internal TcpConnection(TcpClient client)
    {
        _client = client;
        _client.NoDelay = true;
        _stream = client.GetStream();
        _receiveTask = ReceiveAsync();
        _sendTask = SendAsync();
    }

    internal bool IsClosed { get { lock (_gate) return _closed; } }
    internal int IncomingCount { get { lock (_gate) return _incoming.Count; } }
    internal string CloseReason { get { lock (_gate) return _reason ?? "The connection ended."; } }

    internal void EnableHeartbeat()
    {
        lock (_gate)
            _heartbeat = true;
    }

    internal bool TrySend(ReadOnlyMemory<byte> frame)
    {
        lock (_gate)
        {
            _fault?.Throw();
            if (_closed || _draining)
                return false;
            if (!_outgoing.TryEnqueue(frame))
            {
                Close("The outgoing queue limit was exceeded.");
                return false;
            }
            _sendSignal.Release();
            return true;
        }
    }

    internal bool TryPeek(out ReadOnlyMemory<byte> frame)
    {
        lock (_gate)
        {
            _fault?.Throw();
            if (_incoming.TryPeek(out frame))
                return true;
            if (_closed)
                throw new ConnectionClosedException(_reason ?? "The connection ended.");
            return false;
        }
    }

    internal void Consume()
    {
        lock (_gate)
            _incoming.Dequeue();
    }

    internal void WaitForFrame(TimeSpan timeout)
    {
        var start = Stopwatch.GetTimestamp();
        lock (_gate)
        {
            while (_incoming.Count == 0 && !_closed)
            {
                _fault?.Throw();
                var remaining = timeout - Stopwatch.GetElapsedTime(start);
                if (remaining <= TimeSpan.Zero || !Monitor.Wait(_gate, remaining))
                    throw new ConnectionClosedException("Timed out waiting for server data.");
            }
            _fault?.Throw();
        }
    }

    internal void ThrowIfFaulted()
    {
        lock (_gate)
            _fault?.Throw();
    }

    internal void CompleteSending(string reason)
    {
        lock (_gate)
        {
            if (_closed || _draining)
                return;
            _draining = true;
            _reason ??= reason;
            _stop.CancelAfter(NetworkLimits.FrameTimeout);
            _sendSignal.Release();
        }
    }

    internal void Close(string reason)
    {
        lock (_gate)
        {
            if (_closed)
                return;
            _closed = true;
            _reason ??= reason;
            _stop.Cancel();
            _client.Dispose();
            Monitor.PulseAll(_gate);
        }
    }

    internal void Destroy()
    {
        Close("The connection was closed locally.");
        Task.WhenAll(_receiveTask, _sendTask).GetAwaiter().GetResult();
        _sendSignal.Dispose();
        _stop.Dispose();
        ThrowIfFaulted();
    }

    private async Task ReceiveAsync()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                var frame = await TcpFraming.ReadAsync(_stream, _stop.Token).ConfigureAwait(false);
                if (frame is null)
                {
                    Close("The remote endpoint closed the connection.");
                    return;
                }
                if (PacketCodec.ReadId(frame.Value.Span) == 0)
                {
                    ValidateHeartbeat(frame.Value.Span);
                    continue;
                }
                lock (_gate)
                {
                    if (_closed)
                        return;
                    if (!_incoming.TryEnqueue(frame.Value))
                        throw new InvalidDataException("The incoming queue limit was exceeded.");
                    Monitor.PulseAll(_gate);
                }
            }
        }
        catch (Exception error) { HandleFailure(error); }
    }

    private async Task SendAsync()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                var signaled = await _sendSignal.WaitAsync(NetworkLimits.HeartbeatInterval, _stop.Token).ConfigureAwait(false);
                ReadOnlyMemory<byte> frame;
                bool queued;
                lock (_gate)
                {
                    queued = _outgoing.TryPeek(out frame);
                    if (!queued)
                    {
                        if (_draining)
                        {
                            Close(_reason ?? "The connection ended.");
                            return;
                        }
                        if (signaled || !_heartbeat)
                            continue;
                        frame = new byte[] { 0 };
                    }
                }
                await TcpFraming.WriteAsync(_stream, frame, _stop.Token).ConfigureAwait(false);
                if (queued)
                {
                    lock (_gate)
                        _outgoing.Dequeue();
                }
            }
        }
        catch (Exception error) { HandleFailure(error); }
    }

    private static void ValidateHeartbeat(ReadOnlySpan<byte> frame)
    {
        var reader = new Reader(frame);
        reader.ReadVarUInt32();
        reader.EnsureEnd();
    }

    private void HandleFailure(Exception error)
    {
        lock (_gate)
        {
            if (error is not (IOException or SocketException or InvalidDataException or OperationCanceledException) &&
                !(error is ObjectDisposedException && _closed))
                _fault ??= ExceptionDispatchInfo.Capture(error);
        }
        Close(error is OperationCanceledException ? "Network I/O timed out or was canceled." : error.Message);
    }
}
