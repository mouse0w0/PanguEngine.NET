using System.Net;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;

namespace PanguEngine.Network;

internal sealed class TcpConnectionListener
{
    private readonly Lock _gate = new();
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _stop = new();
    private readonly HashSet<TcpConnection> _connections = [];
    private readonly Queue<TcpConnection> _accepted = [];
    private readonly Task _acceptTask;
    private ExceptionDispatchInfo? _fault;

    internal TcpConnectionListener(IPEndPoint endpoint)
    {
        _listener = new TcpListener(endpoint);
        _listener.Start();
        _acceptTask = AcceptAsync();
    }

    internal IPEndPoint LocalEndpoint => (IPEndPoint)_listener.LocalEndpoint;

    internal void DrainAccepted(List<TcpConnection> connections)
    {
        lock (_gate)
        {
            _fault?.Throw();
            connections.AddRange(_accepted);
            _accepted.Clear();
        }
    }

    internal void Remove(TcpConnection connection)
    {
        lock (_gate)
            _connections.Remove(connection);
        connection.Destroy();
    }

    internal void StopAccepting()
    {
        _stop.Cancel();
        _listener.Stop();
        _acceptTask.GetAwaiter().GetResult();
        lock (_gate)
            _fault?.Throw();
        _stop.Dispose();
    }

    private async Task AcceptAsync()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(_stop.Token).ConfigureAwait(false);
                lock (_gate)
                {
                    if (_stop.IsCancellationRequested || _connections.Count >= NetworkLimits.MaxConnections)
                    {
                        client.Dispose();
                        continue;
                    }
                    var connection = new TcpConnection(client);
                    _connections.Add(connection);
                    _accepted.Enqueue(connection);
                }
            }
        }
        catch (Exception error) when (_stop.IsCancellationRequested &&
                                      error is OperationCanceledException or SocketException or ObjectDisposedException) { }
        catch (Exception error)
        {
            lock (_gate)
                _fault = ExceptionDispatchInfo.Capture(error);
        }
    }
}
