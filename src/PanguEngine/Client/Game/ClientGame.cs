using PanguEngine.Client.World;
using PanguEngine.Graphics.Vulkan;

namespace PanguEngine.Client.Game;

/// <summary>
/// Represents the local client game instance.
/// </summary>
public sealed class ClientGame
{
    private readonly VulkanRenderer _renderer;

    internal ClientGame(ClientEngine engine)
    {
        World = new ClientWorld();
        _renderer = new VulkanRenderer(engine.Device, engine.PrimaryWindow.Presenter);
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