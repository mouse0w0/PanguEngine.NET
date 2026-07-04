using PanguEngine.Graphics.Vulkan;

namespace PanguEngine.Client.Game;

internal sealed class ClientGame
{
    private readonly VulkanRenderer _renderer;

    internal ClientGame(ClientEngine engine)
    {
        _renderer = new VulkanRenderer(engine.Device, engine.PrimaryWindow.Presenter);
    }

    public void Update()
    {
    }

    public void DrawFrame(double alpha)
    {
        _renderer.DrawFrame(alpha);
    }

    public void Destroy()
    {
        _renderer.Destroy();
    }
}