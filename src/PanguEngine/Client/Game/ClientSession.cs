using PanguEngine.Client.Input;
using PanguEngine.Client.World;
using PanguEngine.Input;
using PanguEngine.Network;
using PanguEngine.Network.Packets;
using PanguEngine.Network.Packets.Clientbound;
using PanguEngine.Network.Packets.Serverbound;
using static PanguEngine.Network.Packets.Clientbound.WorldUpdatePacket;

namespace PanguEngine.Client.Game;

internal sealed class ClientSession
{
    private readonly IClientConnection _connection;
    private readonly double _stepSeconds;
    private readonly ClientWorld _world;
    private bool _closing;
    private double _receivedAt;

    internal ClientSession(IClientConnection connection, ClientWorld world, InitialWorldStatePacket initial, double now)
    {
        _connection = connection;
        _world = world;
        _stepSeconds = initial.StepSeconds;
        _receivedAt = now;
    }

    internal bool CanSubmitInput => !_closing && !_connection.IsClosed;
    internal event Action<InteractionResult>? OperationCompleted;

    internal bool TrySendPacket(ServerboundPacket packet) => CanSubmitInput && _connection.TrySend(packet);

    internal double GetInterpolationAlpha(double now) => Math.Clamp((now - _receivedAt) / _stepSeconds, 0, 1);

    internal void RefreshInput(InputManager input, double yaw, double pitch)
    {
        var movement = input.GetValue(BuiltinInputActions.Move).Axis2D;
        SubmitInput(movement.Y, movement.X, yaw, pitch);
    }

    internal void SubmitInput(double forward, double right, double yaw, double pitch)
    {
        if (!_closing)
            _connection.TrySend(new InputStatePacket(forward, right, yaw, pitch));
    }

    internal void RequestInteraction(BlockOperation operation, double yaw, double pitch)
    {
        if (!_closing)
            _connection.TrySend(new BlockInteractionPacket(operation, yaw, pitch));
    }

    internal void ReceivePending(double now, ClientGame? game = null)
    {
        var context = new ClientPacketContext(_world, this, game, now);
        while (_connection.TryReceive(out var packet))
        {
            if (packet is InitialWorldStatePacket or ChunkDataPacket or InitialWorldCompletePacket)
                throw new ConnectionClosedException("Unexpected message after initial world loading.");
            context.Handle(packet);
        }
    }

    internal void ApplyWorldUpdate(WorldUpdatePacket update, double now)
    {
        if (update.Players.All(player => player.NetworkId != _world.PlayerNetworkId))
            throw new ConnectionClosedException("The controlled player is missing from the server update.");
        _receivedAt = now;
        _world.Apply(update);
        foreach (var result in update.Results)
        {
            if (result.IsSuccess)
                OperationCompleted?.Invoke(result);
        }
    }

    internal void BeginClose() => _closing = true;

    internal void CloseConnection() => _connection.Close();
}
