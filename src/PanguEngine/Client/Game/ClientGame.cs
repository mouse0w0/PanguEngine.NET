using PanguEngine.Client.Rendering.World;
using PanguEngine.Client.World;
using PanguEngine.Input;
using PanguEngine.World.Blocks;
using PanguEngine.World.Interaction;
using Silk.NET.Maths;

namespace PanguEngine.Client.Game;

/// <summary>
/// Represents the local client game instance.
/// </summary>
public sealed class ClientGame
{
    private readonly FreeCamera _camera;
    private readonly ClientInputState _input;
    private readonly WorldRenderer _renderer;

    internal ClientGame(ClientEngine engine)
    {
        _camera = new FreeCamera();
        _input = new ClientInputState(engine.PrimaryWindow);
        _input.MouseDelta += _camera.ApplyMouseDelta;
        World = new ClientWorld();
        _renderer = new WorldRenderer(engine.Device, engine.PrimaryWindow.Presenter, World);
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
        _camera.Move(forward, right);

        SelectedBlock = RaycastSelection(_camera.CurrentPosition);
        if (_input.ConsumeLeftClickRequest()
            && TryBreakBlock(World, SelectedBlock))
        {
            SelectedBlock = RaycastSelection(_camera.CurrentPosition);
        }

        if (_input.ConsumeRightClickRequest()
            && TryPlaceBlock(World, SelectedBlock))
        {
            SelectedBlock = RaycastSelection(_camera.CurrentPosition);
        }
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

    private BlockHit? RaycastSelection(Vector3D<float> position)
    {
        var direction = _camera.Forward;
        var ray = new Ray3D<double>(
            new Vector3D<double>(position.X, position.Y, position.Z),
            new Vector3D<double>(direction.X, direction.Y, direction.Z));
        return BlockRaycaster.TryRaycast(
            World,
            ray,
            5d,
            out var hit)
            ? hit
            : null;
    }

    /// <summary>
    /// Releases resources owned by the client game.
    /// </summary>
    public void Destroy()
    {
        _input.MouseDelta -= _camera.ApplyMouseDelta;
        _input.Destroy();
        _renderer.Destroy();
    }
}