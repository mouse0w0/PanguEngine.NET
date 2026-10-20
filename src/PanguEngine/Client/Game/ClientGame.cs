using PanguEngine.Audio;
using PanguEngine.Client.World;
using PanguEngine.Client.Input;
using PanguEngine.Input;
using PanguEngine.World.Blocks;
using PanguEngine.World.Chunking;
using PanguEngine.World.Entities;
using PanguEngine.World.Entities.Components;
using PanguEngine.World.Interaction;
using Silk.NET.Maths;

namespace PanguEngine.Client.Game;

/// <summary>
/// Represents the local client game instance.
/// </summary>
public sealed class ClientGame
{
    private readonly ClientEngine _engine;
    private readonly InputManager _input;
    private readonly PlayerEntity _player;
    private readonly PlayerInputController _playerInput;
    private readonly PlayerFreeFlightUpdater _playerUpdater;
    private readonly AudioSystem _audio;
    private readonly IDisposable _gameContextActivation;
    private bool _leftClickRequested;
    private bool _rightClickRequested;

    internal ClientGame(ClientEngine engine)
    {
        _engine = engine;
        _input = engine.Input;
        _audio = engine.Audio;
        World = new ClientWorld();
        FlatWorldGenerator.Generate(World);
        var initialPosition = new Vector3D<double>(8, 22, 24);
        _player = World.Entities.Spawn(
            BuiltinEntityTypes.Player,
            new EntitySpawnBuilder()
                .Add(BuiltinEntityComponents.Position, new PositionComponent(initialPosition, initialPosition))
                .Add(BuiltinEntityComponents.Rotation, new RotationComponent(-90, -20))
                .Add(BuiltinEntityComponents.MoveIntent, default));
        _playerInput = new PlayerInputController(_player);
        _playerUpdater = new PlayerFreeFlightUpdater();
        Camera = new Camera(_player);

        _gameContextActivation = _input.ActivateContext(BuiltinInputContexts.Game);
        _input.StateInvalidated += ClearInteractionRequests;
        _input.RegisterHandler(BuiltinInputActions.Look, HandleLook);
        _input.RegisterHandler(BuiltinInputActions.BreakBlock, HandleBreakBlock);
        _input.RegisterHandler(BuiltinInputActions.PlaceBlock, HandlePlaceBlock);
        _input.RegisterHandler(
            BuiltinInputActions.CapturePointer,
            HandleCapturePointer);
    }

    internal bool IsPaused { get; private set; }

    /// <summary>The local client world state.</summary>
    public ClientWorld World { get; }

    /// <summary>The local client camera.</summary>
    public Camera Camera { get; }

    /// <summary>The block selected by the latest fixed update, or null when the camera ray misses.</summary>
    public BlockHit? SelectedBlock { get; private set; }

    /// <summary>
    /// Updates the client game state for the current tick.
    /// </summary>
    internal void Update()
    {
        if (IsPaused)
            return;

        var movement = _input.GetValue(BuiltinInputActions.Move).Axis2D;
        _playerInput.SetMoveIntent(movement.Y, movement.X);
        _playerUpdater.Update(World.Entities);
        var position = _player.Position.Current;
        var forwardDirection = EntityPoseMath.GetForward(_player.Rotation);
        _audio.SetListener(new AudioListenerState(
            position,
            forwardDirection,
            Vector3D<double>.UnitY));

        SelectedBlock = RaycastSelection(position, forwardDirection);
        var breakSelection = SelectedBlock;
        if (_leftClickRequested
            && TryBreakBlock(World, breakSelection))
        {
            _leftClickRequested = false;
            _audio.PlayAt(
                BuiltinSoundEvents.BlockBreak,
                GetBlockCenter(breakSelection!.Value.BlockPosition));
            SelectedBlock = RaycastSelection(position, forwardDirection);
        }
        else
        {
            _leftClickRequested = false;
        }

        var placeSelection = SelectedBlock;
        if (_rightClickRequested
            && TryPlaceBlock(World, placeSelection))
        {
            _rightClickRequested = false;
            _audio.PlayAt(
                BuiltinSoundEvents.BlockPlace,
                GetBlockCenter(placeSelection!.Value.BlockPosition.Offset(placeSelection.Value.Face)));
            SelectedBlock = RaycastSelection(position, forwardDirection);
        }
        else
        {
            _rightClickRequested = false;
        }
    }

    internal void Pause()
    {
        IsPaused = true;
        ClearInteractionRequests();
    }

    internal void Resume() => IsPaused = false;

    /// <summary>
    /// Prepares resources for the next client frame.
    /// </summary>
    /// <param name="alpha">The interpolation factor between fixed updates.</param>
    internal void PrepareFrame(double alpha)
    {
        _engine.Renderer.PrepareFrame(Camera, alpha);
    }

    /// <summary>
    /// Draws a frame for the client game.
    /// </summary>
    /// <param name="alpha">The interpolation factor between fixed updates.</param>
    internal void DrawFrame(double alpha)
    {
        _engine.Renderer.DrawFrame(Camera, SelectedBlock, alpha);
    }

    internal static bool TryBreakBlock(ClientWorld world, BlockHit? selection)
    {
        if (selection is not { } hit)
            return false;

        world.SetBlock(hit.BlockPosition, BuiltinBlocks.Air.DefaultState);
        return true;
    }

    internal static bool TryPlaceBlock(ClientWorld world, BlockHit? selection)
    {
        if (selection is not { } hit)
            return false;

        var targetPosition = hit.BlockPosition.Offset(hit.Face);
        if (!world.IsAir(targetPosition))
            return false;

        world.SetBlock(targetPosition, BuiltinBlocks.Stone.DefaultState);
        return true;
    }

    private InputHandling HandleLook(InputActionEvent args)
    {
        if (args.Phase != InputActionPhase.Updated || !_input.IsPointerCaptured)
            return InputHandling.Pass;

        var delta = args.Value.Axis2D;
        _playerInput.ApplyMouseDelta(new Vector2D<float>((float)delta.X, (float)delta.Y));
        return InputHandling.Handled;
    }

    private InputHandling HandleBreakBlock(InputActionEvent args) =>
        HandleInteractionRequest(ref _leftClickRequested, args);

    private InputHandling HandlePlaceBlock(InputActionEvent args) =>
        HandleInteractionRequest(ref _rightClickRequested, args);

    internal static InputHandling HandleInteractionRequest(ref bool requested, InputActionEvent args)
    {
        if (args.Phase == InputActionPhase.Started)
        {
            requested = true;
            return InputHandling.Handled;
        }

        if (args.Phase == InputActionPhase.Stopped && args.Source is null)
            requested = false;
        return InputHandling.Pass;
    }

    private void ClearInteractionRequests()
    {
        _leftClickRequested = false;
        _rightClickRequested = false;
    }

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

    /// <summary>
    /// Releases resources owned by the client game.
    /// </summary>
    internal void Destroy()
    {
        _input.StateInvalidated -= ClearInteractionRequests;
        _gameContextActivation.Dispose();
        ClearInteractionRequests();
        _player.Destroy();
        World.Destroy();
    }
}
