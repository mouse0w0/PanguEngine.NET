using PanguEngine.Graphics;
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

    private GraphicsBackend _graphicsBackend = null!;
    private ClientLoop _loop = null!;
    private bool _sceneInitialized;
    private bool _graphicsBackendInitialized;
    private bool _engineInitialized;

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
        Engine.Initialize();
        _engineInitialized = true;

        _graphicsBackend = GraphicsBackendFactory.Create(GraphicsBackendType.Vulkan, new GraphicsBackendOptions
        {
            PrimaryWindow = new WindowOptions
            {
                Size = new Vector2D<int>(800, 600),
                Title = _scene.Name
            }
        });
        _graphicsBackendInitialized = true;

        Window = _graphicsBackend.PrimaryWindow;
        WindowManager = _graphicsBackend.WindowManager;
        _loop = new ClientLoop(
            () => WindowManager.Windows.Count > 0,
            WindowManager.DoEvents,
            () => { },
            WindowManager.RenderWindows);
        _scene.Initialize(Window);
        _sceneInitialized = true;
    }

    private void Shutdown()
    {
        if (_graphicsBackendInitialized)
            Device.WaitIdle();

        if (_sceneInitialized)
            _scene.Destroy();

        if (_graphicsBackendInitialized)
            _graphicsBackend.Destroy();

        if (_engineInitialized)
            Engine.Shutdown();
    }
}