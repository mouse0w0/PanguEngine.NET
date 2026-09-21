using Microsoft.Extensions.Logging;
using PanguEngine.Audio;
using PanguEngine.Client.Game;
using PanguEngine.Client.Input;
using PanguEngine.Client.Rendering;
using PanguEngine.Client.Resources.Models;
using PanguEngine.Client.Screens;
using PanguEngine.Client.UI;
using PanguEngine.Desktop;
using PanguEngine.Desktop.Sdl;
using PanguEngine.Graphics;
using PanguEngine.Graphics.Text;
using PanguEngine.Graphics.Vulkan;
using PanguEngine.Input;
using PanguEngine.Registries;
using PanguEngine.Threading;
using PanguEngine.Windowing;
using SDL;
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
    private EngineWorkQueue _workQueue = null!;
    private bool _shutdownRequested;

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
    /// Gets the system clipboard for this client.
    /// </summary>
    public Clipboard Clipboard { get; private set; } = null!;

    /// <summary>
    /// Gets the platform file and folder dialog service for this client.
    /// </summary>
    public FileDialogService FileDialogs { get; private set; } = null!;

    /// <summary>
    /// Gets the client main-thread dispatcher.
    /// </summary>
    public EngineDispatcher Dispatcher { get; private set; } = null!;

    /// <summary>
    /// Gets the client UI manager.
    /// </summary>
    public UiManager Ui { get; private set; } = null!;

    /// <summary>
    /// The client input manager.
    /// </summary>
    public InputManager Input { get; private set; } = null!;

    private ClientGame Game { get; set; } = null!;

    internal ClientRenderer Renderer { get; private set; } = null!;

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
        var previousContext = SynchronizationContext.Current;
        _workQueue = new EngineWorkQueue();
        Dispatcher = new EngineDispatcher(_workQueue);
        SynchronizationContext.SetSynchronizationContext(new EngineSynchronizationContext(_workQueue));
        try
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
        finally
        {
            try
            {
                _workQueue.Destroy();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previousContext);
            }
        }
    }

    private void OnInit()
    {
        Engine.Initialize(_launchOptions);
        InitializeTextServices();

        if (!SDL3.SDL_InitSubSystem(SDL_InitFlags.SDL_INIT_VIDEO))
            throw new InvalidOperationException($"SDL video initialization failed: {SDL3.SDL_GetError()}");

        var windowMode = _launchOptions.WindowMode;
        GraphicsBackend = new VulkanBackend(
            new WindowOptions
            {
                Size = _launchOptions.WindowSize ?? new Vector2D<int>(800, 600),
                Title = _launchOptions.WindowTitle ?? "PanguEngine",
                WindowState = windowMode switch
                {
                    WindowMode.Maximized => WindowState.Maximized,
                    WindowMode.Fullscreen => WindowState.Fullscreen,
                    _ => WindowState.Normal
                },
                WindowBorder = windowMode == WindowMode.Borderless
                    ? WindowBorder.Hidden
                    : WindowBorder.Resizable
            },
            enableValidation: _launchOptions.GpuValidation);
        if (windowMode is not (WindowMode.Maximized or WindowMode.Fullscreen))
            PrimaryWindow.CenterOnScreen();
        PrimaryWindow.Show();
        FileDialogs = new SdlFileDialogService();
        Clipboard = new SdlClipboard();
        var monitor = PrimaryWindow.Monitor ?? throw new InvalidOperationException(
            "UI scale initialization requires a current monitor.");
        UiSettings.DefaultScale = monitor.ContentScale;
        Ui = new UiManager();
        Ui.InitializeHud(Engine.RegistryManager.Get<HudDefinition>(RegistryKeys.Hud));
        Input = new InputManager(
            PrimaryWindow,
            Ui,
            InputBindingMap.CreateFromRegistries(
                BuiltinRegistries.InputAction,
                BuiltinRegistries.InputContext));

        Audio = new AudioSystem(
            Engine.ResourceManager,
            BuiltinRegistries.SoundCategory,
            BuiltinRegistries.SoundEvent,
            Log.CreateLogger("Audio"));

        Loop = new ClientLoop(
            ShouldContinue,
            PumpClientEvents,
            OnUpdate,
            GraphicsBackend.Render);
        Game = new ClientGame(this);
        Input.RegisterHandler(BuiltinInputActions.TogglePause, HandleTogglePause);
        Ui.CurrentScreenChanged += OnCurrentScreenChanged;
        OnCurrentScreenChanged(null, Ui.CurrentScreen);
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

        Renderer = new ClientRenderer(
            Device,
            PrimaryWindow.Presenter,
            TextServices.FontManager,
            Ui,
            Game.World,
            BlockModelManager);
        Input.Start();
    }

    /// <summary>Requests the client engine to shut down after outstanding native dialogs close.</summary>
    internal void RequestShutdown()
    {
        _shutdownRequested = true;
    }

    private bool ShouldContinue() =>
        (!_shutdownRequested && WindowManager.VisibleWindows.Count > 0) ||
        FileDialogs.HasPendingNativeRequests;

    private void PumpClientEvents()
    {
        WindowManager.DoEvents();
        _workQueue.RunPending();
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
        Ui.Update();
    }

    private void OnCurrentScreenChanged(UiScreen? oldScreen, UiScreen? newScreen)
    {
        if (newScreen?.PausesGame == true)
            Game.Pause();
        else
            Game.Resume();
    }

    private InputHandling HandleTogglePause(InputActionEvent args)
    {
        if (args.Phase != InputActionPhase.Started)
            return InputHandling.Pass;

        if (Ui.CurrentScreen is null)
        {
            Ui.Open(new PauseScreen());
            return InputHandling.Handled;
        }

        if (Ui.CurrentScreen.CloseOnEscape)
        {
            Ui.Close();
            return InputHandling.Handled;
        }

        return InputHandling.Pass;
    }

    private void OnShutdown()
    {
        _workQueue.Destroy();

        FileDialogs.Destroy();
        if (FileDialogs.HasPendingNativeRequests)
            Engine.Logger.LogInformation("Waiting for open native file dialogs to close before shutdown.");
        while (FileDialogs.HasPendingNativeRequests)
        {
            WindowManager.DoEvents();
            Thread.Sleep(1);
        }

        Ui.CurrentScreenChanged -= OnCurrentScreenChanged;
        Ui.Destroy();
        Input.Destroy();
        Game.Destroy();
        Renderer.Destroy();
        Audio.Destroy();
        TextServices.Shutdown();
        Clipboard.Destroy();
        GraphicsBackend.Destroy();
        SDL3.SDL_QuitSubSystem(SDL_InitFlags.SDL_INIT_VIDEO);
        SDL3.SDL_Quit();

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
