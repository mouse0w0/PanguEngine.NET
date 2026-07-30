using PanguEngine.Client.Game;
using PanguEngine.Client.Resources.Models;
using PanguEngine.Graphics;
using PanguEngine.Registries;
using PanguEngine.Windowing;
using Silk.NET.Maths;
using Window = PanguEngine.Windowing.Window;

namespace PanguEngine.Client;

/// <summary>
/// Client engine.
/// </summary>
public sealed class ClientEngine
{
    /// <summary>
    /// The current client engine.
    /// </summary>
    public static ClientEngine Current { get; private set; } = null!;

    private readonly LaunchOptions _launchOptions;

    private ClientEngine(LaunchOptions launchOptions)
    {
        _launchOptions = launchOptions;
    }

    /// <summary>
    /// The primary window.
    /// </summary>
    public Window PrimaryWindow => GraphicsBackend.PrimaryWindow;

    /// <summary>
    /// The window manager.
    /// </summary>
    public WindowManager WindowManager => GraphicsBackend.WindowManager;

    /// <summary>
    /// The graphics device.
    /// </summary>
    public GraphicsDevice Device => GraphicsBackend.Device;

    /// <summary>
    /// The client loop.
    /// </summary>
    public ClientLoop Loop { get; private set; } = null!;

    private ClientGame Game { get; set; } = null!;

    internal BlockModelManager BlockModelManager { get; private set; } = null!;

    private GraphicsBackend GraphicsBackend { get; set; } = null!;

    /// <summary>
    /// Starts the client engine.
    /// </summary>
    internal static void Start(LaunchOptions launchOptions)
    {
        if (Current is not null)
            throw new InvalidOperationException("Client engine is already running.");

        Current = new ClientEngine(launchOptions);
        Current.Run();
    }

    private void Run()
    {
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

    private void OnInit()
    {
        Engine.Initialize(_launchOptions);

        GraphicsBackend = GraphicsBackendFactory.Create(GraphicsBackendType.Vulkan, new GraphicsBackendOptions
        {
            PrimaryWindow = new WindowOptions { Size = new Vector2D<int>(800, 600), Title = "PanguEngine" }
        });

        Loop = new ClientLoop(
            () => WindowManager.Windows.Count > 0,
            WindowManager.DoEvents,
            OnUpdate,
            GraphicsBackend.Render);
        Engine.ModManager.RunClientSetup();
        BlockModelManager = new BlockModelManager(
            Engine.ResourceManager,
            BuiltinRegistries.Block,
            Device.MaxTextureDimension2D,
            Log.CreateLogger("BlockModels"));
        BlockModelManager.Load();
        Engine.ModManager.RunReady();

        Game = new ClientGame(this);
    }

    private void OnRunning()
    {
        PrimaryWindow.Render += (_, alpha) => Game.DrawFrame(alpha);
        Loop.Run();
    }

    private void OnUpdate()
    {
        Game.Update();
    }

    private void OnShutdown()
    {
        Game.Destroy();
        GraphicsBackend.Destroy();

        Engine.Shutdown();
    }
}
