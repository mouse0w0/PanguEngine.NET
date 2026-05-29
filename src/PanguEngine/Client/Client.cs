using PanguEngine.Graphics;
using PanguEngine.Graphics.Vulkan;
using PanguEngine.Windowing;
using Silk.NET.Maths;
using Window = PanguEngine.Windowing.Window;

namespace PanguEngine.Client;

/// <summary>
/// Application client.
/// </summary>
public class Client
{
    /// <summary>
    /// The singleton instance of the client.
    /// </summary>
    public static Client Instance { get; private set; } = null!;

    /// <summary>
    /// The primary window.
    /// </summary>
    public Window PrimaryWindow => GraphicsBackend.PrimaryWindow;

    /// <summary>
    /// The window manager.
    /// </summary>
    public WindowManager WindowManager => GraphicsBackend.WindowManager;

    /// <summary>
    /// The client loop.
    /// </summary>
    public ClientLoop Loop { get; private set; } = null!;

    /// <summary>
    /// The Vulkan renderer.
    /// </summary>
    private VulkanRenderer Renderer { get; set; } = null!;

    /// <summary>
    /// The graphics backend.
    /// </summary>
    private GraphicsBackend GraphicsBackend { get; set; } = null!;

    /// <summary>
    /// Runs the application.
    /// </summary>
    public void Run()
    {
        Instance = this;

        OnInit();
        try
        {
            OnRunning();
        }
        finally
        {
            OnShutdown();
        }
    }

    /// <summary>
    /// Initializes the client.
    /// </summary>
    private void OnInit()
    {
        Engine.Initialize();

        GraphicsBackend = GraphicsBackendFactory.Create(GraphicsBackendType.Vulkan, new GraphicsBackendOptions
        {
            PrimaryWindow = new WindowOptions { Size = new Vector2D<int>(800, 600), Title = "PanguEngine" }
        });

        Loop = new ClientLoop(WindowManager);
        Renderer = new VulkanRenderer(PrimaryWindow.Presenter);
    }

    /// <summary>
    /// Enters the main loop.
    /// </summary>
    private void OnRunning()
    {
        PrimaryWindow.Render += (_, dt) => Renderer.DrawFrame(dt);
        Loop.Run();
    }

    /// <summary>
    /// Shuts down the client.
    /// </summary>
    private void OnShutdown()
    {
        Renderer.Destroy();
        GraphicsBackend.Destroy();

        Engine.Shutdown();
    }
}