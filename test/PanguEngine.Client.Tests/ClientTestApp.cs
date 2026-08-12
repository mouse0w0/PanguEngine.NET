using PanguEngine.Audio;
using PanguEngine.Graphics;
using PanguEngine.Registries;
using PanguEngine.Windowing;
using Silk.NET.Maths;
using Window = PanguEngine.Windowing.Window;

namespace PanguEngine.Client.Tests;

/// <summary>
/// Runs a backend-independent graphics test scene.
/// </summary>
/// <param name="scene">The scene to run.</param>
public sealed class ClientTestApp
{
    /// <summary>
    /// Gets the current running test application.
    /// </summary>
    public static ClientTestApp Current { get; private set; } = null!;

    private readonly IClientTestScene _scene;

    /// <summary>
    /// Gets the primary window.
    /// </summary>
    public Window Window { get; private set; } = null!;

    /// <summary>
    /// Gets the window manager.
    /// </summary>
    public WindowManager WindowManager { get; private set; } = null!;

    /// <summary>
    /// Gets the graphics device.
    /// </summary>
    public GraphicsDevice Device => _graphicsBackend.Device;

    /// <summary>
    /// Gets the audio system when the scene requires audio.
    /// </summary>
    public AudioSystem Audio => _audio!;

    private GraphicsBackend _graphicsBackend = null!;
    private ClientLoop _loop = null!;
    private bool _sceneInitialized;
    private bool _graphicsBackendInitialized;
    private bool _engineInitialized;
    private bool _audioInitialized;
    private AudioSystem? _audio;

    private ClientTestApp(IClientTestScene scene)
    {
        _scene = scene;
    }

    /// <summary>
    /// Initializes, runs, and shuts down the test application with the given scene.
    /// </summary>
    public static void Run(IClientTestScene scene)
    {
        Current = new ClientTestApp(scene);
        Current.RunInternal();
    }

    private void RunInternal()
    {
        try
        {
            Initialize();

            _loop.Run();
        }
        finally
        {
            Shutdown();
        }
    }

    private void Initialize()
    {
        _scene.ConfigureBeforeEngineInitialize();
        Engine.Initialize();
        _engineInitialized = true;

        _graphicsBackend = GraphicsBackendFactory.Create(GraphicsBackendType.Vulkan, new GraphicsBackendOptions
        {
            EnableValidation = true,
            PrimaryWindow = new WindowOptions
            {
                Size = new Vector2D<int>(800, 600),
                Title = _scene.Name
            }
        });
        _graphicsBackendInitialized = true;

        Window = _graphicsBackend.PrimaryWindow;
        WindowManager = _graphicsBackend.WindowManager;
        if (_scene.RequiresAudio)
        {
            _audio = new AudioSystem(
                Engine.ResourceManager,
                BuiltinRegistries.SoundCategory,
                BuiltinRegistries.SoundEvent,
                Log.CreateLogger("AudioTests"));
            _audioInitialized = true;
            _audio.Load();
            _audio.MarkReady();
        }
        _loop = new ClientLoop(
            () => WindowManager.Windows.Count > 0,
            WindowManager.DoEvents,
            Update,
            _graphicsBackend.Render);
        _scene.Initialize(Window);
        _sceneInitialized = true;
    }

    private void Shutdown()
    {
        if (_graphicsBackendInitialized)
            Device.WaitIdle();

        if (_sceneInitialized)
            _scene.Destroy();

        if (_audioInitialized)
            _audio!.Destroy();

        if (_graphicsBackendInitialized)
            _graphicsBackend.Destroy();

        if (_engineInitialized)
            Engine.Shutdown();
    }

    private void Update()
    {
        _audio?.Update();
    }
}
