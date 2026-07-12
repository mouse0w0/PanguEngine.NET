using PanguEngine.Client.Rendering.World;
using PanguEngine.Client.World;
using PanguEngine.Input;

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
    }

    /// <summary>
    /// Draws a frame for the client game.
    /// </summary>
    /// <param name="alpha">The interpolation factor between fixed updates.</param>
    public void DrawFrame(double alpha)
    {
        _renderer.DrawFrame(_camera, alpha);
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