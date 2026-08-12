using PanguEngine.Audio;
using PanguEngine.Client.Rendering.World;
using PanguEngine.Client.World;
using PanguEngine.Input;
using PanguEngine.World.Blocks;
using PanguEngine.World.Chunking;
using PanguEngine.World.Interaction;
using Silk.NET.Maths;

namespace PanguEngine.Client.Game;

/// <summary>
/// Represents the local client game instance.
/// </summary>
public sealed class ClientGame
{
    private readonly Camera _camera;
    private readonly CameraController _cameraController;
    private readonly ClientInputState _input;
    private readonly AudioSystem _audio;
    private readonly WorldRenderer _renderer;

    internal ClientGame(ClientEngine engine)
    {
        _camera = new Camera(new Vector3D<double>(8, 22, 24), -90, -20);
        _cameraController = new CameraController(_camera);
        _input = new ClientInputState(engine.PrimaryWindow);
        _audio = engine.Audio;
        _input.MouseDelta += _cameraController.ApplyMouseDelta;
        World = new ClientWorld();
        FlatWorldGenerator.Generate(World);
        _renderer = new WorldRenderer(engine.Device, engine.PrimaryWindow.Presenter, World, engine.BlockModelManager);
    }

    /// <summary>The local client world state.</summary>
    public ClientWorld World { get; }

    /// <summary>The block currently selected by the camera ray.</summary>
    public BlockHit? SelectedBlock { get; private set; }

    /// <summary>
    /// Updates the client game state for the current tick.
    /// </summary>
    public void Update()
    {
        var forward = (_input.IsKeyDown(Key.W) ? 1 : 0)
                      - (_input.IsKeyDown(Key.S) ? 1 : 0);
        var right = (_input.IsKeyDown(Key.D) ? 1 : 0)
                    - (_input.IsKeyDown(Key.A) ? 1 : 0);
        _cameraController.Move(forward, right);
        _audio.SetListener(new AudioListenerState(
            _camera.CurrentPosition,
            _camera.Forward,
            Vector3D<double>.UnitY));

        SelectedBlock = RaycastSelection(_camera.CurrentPosition);
        var breakSelection = SelectedBlock;
        if (_input.ConsumeLeftClickRequest()
            && TryBreakBlock(World, breakSelection))
        {
            _audio.PlayAt(
                BuiltinSoundEvents.BlockBreak,
                GetBlockCenter(breakSelection!.Value.BlockPosition));
            SelectedBlock = RaycastSelection(_camera.CurrentPosition);
        }

        var placeSelection = SelectedBlock;
        if (_input.ConsumeRightClickRequest()
            && TryPlaceBlock(World, placeSelection))
        {
            _audio.PlayAt(
                BuiltinSoundEvents.BlockPlace,
                GetBlockCenter(placeSelection!.Value.BlockPosition.Offset(placeSelection.Value.Face)));
            SelectedBlock = RaycastSelection(_camera.CurrentPosition);
        }
    }

    /// <summary>
    /// Prepares resources for the next client frame.
    /// </summary>
    /// <param name="alpha">The interpolation factor between fixed updates.</param>
    public void PrepareFrame(double alpha)
    {
        _renderer.PrepareFrame(_camera, alpha);
    }

    /// <summary>
    /// Draws a frame for the client game.
    /// </summary>
    /// <param name="alpha">The interpolation factor between fixed updates.</param>
    public void DrawFrame(double alpha)
    {
        var renderSelection = RaycastSelection(_camera.GetInterpolatedPosition(alpha));
        _renderer.DrawFrame(_camera, renderSelection, alpha);
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

    private BlockHit? RaycastSelection(Vector3D<double> position)
    {
        var ray = new Ray3D<double>(position, _camera.Forward);
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
    public void Destroy()
    {
        _input.MouseDelta -= _cameraController.ApplyMouseDelta;
        _input.Destroy();
        _renderer.Destroy();
    }
}
