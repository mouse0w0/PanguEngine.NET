using System.Text;
using PanguEngine.Network;
using PanguEngine.Network.Packets;

namespace PanguEngine.Server.Game;

/// <summary>Owns the server-side packet endpoint and its reserved username.</summary>
internal sealed class RemoteSession
{
    private readonly IServerConnection _connection;
    private readonly PlayerNames _playerNames;
    private bool _closing;
    private bool _nameReleased;

    internal RemoteSession(IServerConnection connection,
        Task<(string? ReservedUsername, bool IsReady)> preparation, PlayerNames playerNames)
    {
        _connection = connection;
        _playerNames = playerNames;
        Preparation = preparation;
    }

    /// <summary>Creates a ready session for an in-process client with a reserved username.</summary>
    internal RemoteSession(IServerConnection connection, string username, PlayerNames playerNames)
        : this(connection, Task.FromResult<(string?, bool)>((username, true)), playerNames)
    {
    }

    /// <summary>Completes when preparation stops reading packets and reports whether the session can enter Play.</summary>
    /// <remarks>A connection can hold a reserved username even if it is not ready to enter Play.</remarks>
    internal Task<(string? ReservedUsername, bool IsReady)> Preparation { get; }
    internal bool IsClosing => _closing || _connection.IsClosed;

    /// <summary>Observes preparation without waiting and returns the username only while ready for Play.</summary>
    internal string? GetReadyUsername()
    {
        _connection.ThrowIfFaulted();
        if (!Preparation.IsCompleted)
            return null;
        var (username, isReady) = Preparation.GetAwaiter().GetResult();
        return isReady && !IsClosing ? username : null;
    }

    /// <summary>Reads the queued Play batch, discarding the entire batch if an expected receive error occurs.</summary>
    /// <remarks>Only the game thread reads Play packets, after preparation reports readiness. An empty batch can succeed.</remarks>
    internal bool TryReceiveBatch(out IReadOnlyList<ServerboundPacket> packets)
    {
        packets = [];
        var count = _connection.IncomingCount;
        if (count == 0)
            return true;
        var received = new List<ServerboundPacket>();
        try
        {
            for (var i = 0; i < count; i++)
            {
                if (!_connection.TryReceive(out var packet))
                    break;
                received.Add(packet);
            }
        }
        catch (Exception error) when (error is InvalidDataException or EndOfStreamException or DecoderFallbackException)
        {
            Disconnect(error.Message);
            return false;
        }
        catch (ConnectionClosedException)
        {
            return false;
        }
        packets = received;
        return true;
    }

    internal bool TrySend(ClientboundPacket packet) => _connection.TrySend(packet);

    /// <summary>Releases a reserved username once the connection is closed and preparation has ended.</summary>
    /// <remarks>Only the game thread releases the reservation, including when Play was never entered.</remarks>
    internal void ReleaseNameIfClosed()
    {
        if (!_nameReleased && _connection.IsClosed && Preparation.IsCompleted &&
            Preparation.GetAwaiter().GetResult().ReservedUsername is { } username)
        {
            _playerNames.Release(username);
            _nameReleased = true;
        }
    }

    internal void Disconnect(string reason)
    {
        if (_closing)
            return;
        _connection.Disconnect(reason);
        _closing = true;
    }
}
