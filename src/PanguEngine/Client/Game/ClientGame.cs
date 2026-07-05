using PanguEngine.Client.Rendering.World;
using PanguEngine.Client.World;

namespace PanguEngine.Client.Game;

/// <summary>
/// Represents the local client game instance.
/// </summary>
public sealed class ClientGame
{
    private readonly WorldRenderer _renderer;

    internal ClientGame(ClientEngine engine)
    {
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
    }

    /// <summary>
    /// Draws a frame for the client game.
    /// </summary>
    /// <param name="alpha">The interpolation factor between fixed updates.</param>
    public void DrawFrame(double alpha)
    {
        _renderer.DrawFrame(alpha);
    }

    /// <summary>
    /// Releases resources owned by the client game.
    /// </summary>
    public void Destroy()
    {
        _renderer.Destroy();
    }
}