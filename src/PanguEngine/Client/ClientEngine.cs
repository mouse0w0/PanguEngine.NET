using PanguEngine.Audio;
using PanguEngine.Client.Game;
using PanguEngine.Client.Rendering;
using PanguEngine.Client.Resources.Models;
using PanguEngine.Client.UI;
using PanguEngine.Graphics;
using PanguEngine.Graphics.Text;
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

    /// <summary>
    /// The client audio system.
    /// </summary>
    public AudioSystem Audio { get; private set; } = null!;

    /// <summary>
    /// Gets the client UI manager.
    /// </summary>
    public UiManager Ui { get; private set; } = null!;

    private ClientGame Game { get; set; } = null!;

    internal ClientRenderer Renderer { get; private set; } = null!;

    private ClientInputBridge InputBridge { get; set; } = null!;

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
        InitializeTextServices();

        GraphicsBackend = GraphicsBackendFactory.Create(GraphicsBackendType.Vulkan, new GraphicsBackendOptions
        {
            EnableValidation = _launchOptions.GpuValidation,
            PrimaryWindow = new WindowOptions { Size = new Vector2D<int>(800, 600), Title = "PanguEngine" }
        });
        Ui = new UiManager();

        Audio = new AudioSystem(
            Engine.ResourceManager,
            BuiltinRegistries.SoundCategory,
            BuiltinRegistries.SoundEvent,
            Log.CreateLogger("Audio"));

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
        Audio.Load();
        Audio.MarkReady();
        Engine.ModManager.RunReady();

        Game = new ClientGame(this);
        Renderer = new ClientRenderer(
            Device,
            PrimaryWindow.Presenter,
            TextServices.FontManager,
            Ui,
            Game.World,
            BlockModelManager);
        InputBridge = new ClientInputBridge(PrimaryWindow, Ui, Game.Input, TryTogglePause);
    }

    private void OnRunning()
    {
        PrimaryWindow.PreRender += (_, alpha) => Game.PrepareFrame(alpha);
        PrimaryWindow.Render += (_, alpha) => Game.DrawFrame(alpha);
        Loop.Run();
    }

    private void OnUpdate()
    {
        Game.Update();
        Audio.Update();
    }

    private bool TryTogglePause()
    {
        if (Game.IsPaused)
        {
            ResumeGame();
            return true;
        }

        if (Ui.CurrentScreen is not null)
            return false;

        Ui.Open(new PauseScreen());
        Game.Pause();
        return true;
    }

    internal void ResumeGame()
    {
        Ui.Close();
        Game.Resume();
    }

    private void OnShutdown()
    {
        Ui.Destroy();
        InputBridge.Destroy();
        Game.Destroy();
        Renderer.Destroy();
        Audio.Destroy();
        TextServices.Dispose();
        GraphicsBackend.Destroy();

        Engine.Shutdown();
    }

    private static void InitializeTextServices()
    {
        TextServices.Initialize();
        try
        {
            TextServices.FontManager.RegisterResources(Engine.ResourceManager);
            TextServices.FontManager.DefaultFont = new Font("Source Han Sans CN");
        }
        catch
        {
            TextServices.Dispose();
            throw;
        }
    }
}
