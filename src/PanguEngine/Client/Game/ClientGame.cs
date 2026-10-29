using System.Diagnostics;
using PanguEngine.Audio;
using PanguEngine.Client.World;
using PanguEngine.Client.Input;
using PanguEngine.Input;
using PanguEngine.Server;
using PanguEngine.World.Chunking;
using PanguEngine.World.Interaction;
using PanguEngine.Network.Packets;
using PanguEngine.Network.Packets.Clientbound;
using Silk.NET.Maths;
using static PanguEngine.Network.Packets.Clientbound.WorldUpdatePacket;

namespace PanguEngine.Client.Game;

/// <summary>
/// Represents the local client game instance.
/// </summary>
public sealed class ClientGame
{
    private readonly ClientEngine _engine;
    private readonly InputManager _input;
    private readonly PlayerInputController _playerInput;
    private readonly ClientSession _session;
    private readonly Stopwatch _displayClock = Stopwatch.StartNew();
    private readonly AudioSystem _audio;
    private readonly IDisposable _gameContextActivation;

    internal ClientGame(ClientEngine engine)
    {
        _engine = engine;
        _input = engine.Input;
        _audio = engine.Audio;
        World = new ClientWorld();
        var connection = ServerEngine.Current.ConnectLocal();
        var initial = (InitialWorldStatePacket)connection.WaitForPacket();
        _session = new ClientSession(connection, World, initial, _displayClock.Elapsed.TotalSeconds);
        _session.OperationCompleted += OnOperationCompleted;
        Camera = new Camera(initial.Player.Position, initial.Player.Yaw, initial.Player.Pitch);
        _playerInput = new PlayerInputController(Camera);

        _gameContextActivation = _input.ActivateContext(BuiltinInputContexts.Game);
        _input.StateInvalidated += ClearInput;
        _input.RegisterHandler(BuiltinInputActions.Move, HandleMove);
        _input.RegisterHandler(BuiltinInputActions.Look, HandleLook);
        _input.RegisterHandler(BuiltinInputActions.BreakBlock, HandleBreakBlock);
        _input.RegisterHandler(BuiltinInputActions.PlaceBlock, HandlePlaceBlock);
        _input.RegisterHandler(
            BuiltinInputActions.CapturePointer,
            HandleCapturePointer);
    }

    /// <summary>The local client world state.</summary>
    public ClientWorld World { get; }

    /// <summary>The local client camera.</summary>
    public Camera Camera { get; }

    /// <summary>The display block selected by the current camera ray, or null when it misses.</summary>
    public BlockHit? SelectedBlock { get; private set; }

    internal void ReceivePending()
    {
        _session.ReceivePending(_displayClock.Elapsed.TotalSeconds);
        _session.RefreshInputAfterResume(_input, Camera.Yaw, Camera.Pitch);
    }

    /// <summary>
    /// Prepares resources for the next client frame.
    /// </summary>
    /// <param name="clientAlpha">The client update interpolation factor for UI presentation.</param>
    internal void PrepareFrame(double clientAlpha)
    {
        var alpha = _session.GetInterpolationAlpha(_displayClock.Elapsed.TotalSeconds);
        var position = World.Player.Position;
        Camera.Position = position.Previous + (position.Current - position.Previous) * alpha;
        SelectedBlock = RaycastSelection(Camera.Position, Camera.Forward);
        _audio.SetListener(new AudioListenerState(Camera.Position, Camera.Forward, Vector3D<double>.UnitY));
        _engine.Renderer.PrepareFrame(Camera, clientAlpha);
    }

    /// <summary>
    /// Draws a frame for the client game.
    /// </summary>
    internal void DrawFrame()
    {
        _engine.Renderer.DrawFrame(Camera, SelectedBlock);
    }

    private void OnOperationCompleted(InteractionResult result)
    {
        _audio.PlayAt(result.Operation == BlockOperation.Break
            ? BuiltinSoundEvents.BlockBreak
            : BuiltinSoundEvents.BlockPlace, GetBlockCenter(result.Position));
    }

    private InputHandling HandleMove(InputActionEvent args)
    {
        if (!_session.CanSubmitInput)
            return InputHandling.Pass;
        SubmitCurrentInput();
        return InputHandling.Handled;
    }

    private InputHandling HandleLook(InputActionEvent args)
    {
        if (args.Phase != InputActionPhase.Updated || !_input.IsPointerCaptured || !_session.CanSubmitInput)
            return InputHandling.Pass;

        var delta = args.Value.Axis2D;
        _playerInput.ApplyMouseDelta(new Vector2D<float>((float)delta.X, (float)delta.Y));
        SubmitCurrentInput();
        return InputHandling.Handled;
    }

    private InputHandling HandleBreakBlock(InputActionEvent args) =>
        HandleInteractionRequest(BlockOperation.Break, args);

    private InputHandling HandlePlaceBlock(InputActionEvent args) =>
        HandleInteractionRequest(BlockOperation.Place, args);

    private InputHandling HandleInteractionRequest(BlockOperation operation, InputActionEvent args)
    {
        if (args.Phase == InputActionPhase.Started && _session.CanSubmitInput)
        {
            _session.RequestInteraction(operation, Camera.Yaw, Camera.Pitch);
            return InputHandling.Handled;
        }
        return InputHandling.Pass;
    }

    private void SubmitCurrentInput()
    {
        _session.RefreshInput(_input, Camera.Yaw, Camera.Pitch);
    }

    private void ClearInput() => SubmitCurrentInput();

    private InputHandling HandleCapturePointer(InputActionEvent args)
    {
        if (args.Phase != InputActionPhase.Started || _input.IsPointerCaptured)
            return InputHandling.Pass;

        return _input.TryCapturePointer()
            ? InputHandling.Handled
            : InputHandling.Pass;
    }

    private BlockHit? RaycastSelection(Vector3D<double> position, Vector3D<double> direction)
    {
        var ray = new Ray3D<double>(position, direction);
        return BlockRaycaster.TryRaycast(
            World,
            ray,
            5d,
            out var hit)
            ? hit
            : null;
    }

    private static Vector3D<double> GetBlockCenter(BlockPos position) =>
        new(position.X + 0.5, position.Y + 0.5, position.Z + 0.5);

    internal void BeginClose() => _session.BeginClose();

    /// <summary>
    /// Releases resources owned by the client game.
    /// </summary>
    internal void Destroy()
    {
        _session.BeginClose();
        _input.StateInvalidated -= ClearInput;
        _gameContextActivation.Dispose();
        World.Destroy();
    }
}
