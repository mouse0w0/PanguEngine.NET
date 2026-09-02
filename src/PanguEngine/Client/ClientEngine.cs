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
        var monitor = PrimaryWindow.Monitor ?? throw new InvalidOperationException(
            "UI scale initialization requires a current monitor.");
        UiSettings.DefaultScale = monitor.ContentScale;
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
        Ui.CurrentScreenChanged += OnCurrentScreenChanged;
        OnCurrentScreenChanged(null, Ui.CurrentScreen);
        Renderer = new ClientRenderer(
            Device,
            PrimaryWindow.Presenter,
            TextServices.FontManager,
            Ui,
            Game.World,
            BlockModelManager);
        InputBridge = new ClientInputBridge(PrimaryWindow, Ui, Game.Input, TryHandleEscape);
    }

    /// <summary>Requests the client engine to shut down after the current loop iteration.</summary>
    internal void RequestShutdown()
    {
        Loop.RequestStop();
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

    private void OnCurrentScreenChanged(UiScreen? oldScreen, UiScreen? newScreen)
    {
        if (newScreen?.PausesGame == true)
            Game.Pause();
        else
            Game.Resume();
    }

    private bool TryHandleEscape()
    {
        var screen = Ui.CurrentScreen;
        if (screen is null)
        {
            Ui.Open(new PauseScreen());
            return true;
        }

        if (!screen.CloseOnEscape)
            return false;

        Ui.Close();
        return true;
    }

    private void OnShutdown()
    {
        Ui.CurrentScreenChanged -= OnCurrentScreenChanged;
        Ui.Destroy();
        InputBridge.Destroy();
        Game.Destroy();
        Renderer.Destroy();
        Audio.Destroy();
        TextServices.Shutdown();
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
            TextServices.Shutdown();
            throw;
        }
    }
}
