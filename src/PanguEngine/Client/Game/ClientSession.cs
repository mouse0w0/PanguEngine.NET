using PanguEngine.Client.World;
using PanguEngine.Client.Input;
using PanguEngine.Input;
using PanguEngine.Network;
using PanguEngine.Network.Packets;
using PanguEngine.Network.Packets.Clientbound;
using PanguEngine.Network.Packets.Serverbound;
using static PanguEngine.Network.Packets.Clientbound.WorldUpdatePacket;

namespace PanguEngine.Client.Game;

internal sealed class ClientSession
{
    private readonly LocalConnection _connection;
    private readonly ClientWorld _world;
    private readonly double _stepSeconds;
    private double _receivedAt;
    private bool _closing;
    private long _inputEpoch;

    internal ClientSession(LocalConnection connection, ClientWorld world, InitialWorldStatePacket initial, double now)
    {
        _connection = connection;
        _world = world;
        _stepSeconds = initial.StepSeconds;
        _receivedAt = now;
        _inputEpoch = connection.RequestState.Epoch;
        _world.ApplyInitial(initial);
    }

    internal bool CanSubmitInput => !_closing && _connection.RequestState.IsAccepting;
    internal event Action<InteractionResult>? OperationCompleted;

    internal double GetInterpolationAlpha(double now) => Math.Clamp((now - _receivedAt) / _stepSeconds, 0, 1);

    internal void RefreshInputAfterResume(InputManager input, double yaw, double pitch)
    {
        var state = _connection.RequestState;
        if (!_closing && state.IsAccepting && state.Epoch != _inputEpoch)
        {
            _inputEpoch = state.Epoch;
            RefreshInput(input, yaw, pitch);
        }
    }

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

    internal void ReceivePending(double now)
    {
        while (_connection.TryReceive(out var packet))
        {
            switch (packet)
            {
                case WorldUpdatePacket update:
                    _receivedAt = now;
                    _world.Apply(update);
                    foreach (var result in update.Results)
                    {
                        if (result.IsSuccess)
                            OperationCompleted?.Invoke(result);
                    }
                    break;
            }
        }
    }

    internal void BeginClose() => _closing = true;
}
