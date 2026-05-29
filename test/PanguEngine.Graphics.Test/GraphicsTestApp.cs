using PanguEngine.Client;
using PanguEngine.Windowing;
using Silk.NET.Maths;
using Window = PanguEngine.Windowing.Window;

namespace PanguEngine.Graphics.Test;

/// <summary>
/// Runs a backend-independent graphics test scene.
/// </summary>
/// <param name="scene">The scene to run.</param>
public sealed unsafe class GraphicsTestApp(IGraphicsTestScene scene)
{
    private GraphicsBackend _graphicsBackend = null!;
    private Window _window = null!;
    private Presenter _presenter = null!;
    private WindowManager _windowManager = null!;
    private ClientLoop _loop = null!;
    private bool _sceneInitialized;
    private bool _graphicsBackendInitialized;
    private bool _engineInitialized;

    /// <summary>
    /// Initializes, runs, and shuts down the test application.
    /// </summary>
    public void Run()
    {
        try
        {
            Initialize();

            _window.Render += (_, _) => DrawFrame();

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
                Title = scene.Name
            }
        });
        _graphicsBackendInitialized = true;

        _window = _graphicsBackend.PrimaryWindow;
        _presenter = _window.Presenter;
        _windowManager = _graphicsBackend.WindowManager;
        _loop = new ClientLoop(_windowManager);
        scene.Initialize(_presenter);
        _sceneInitialized = true;
    }

    private void DrawFrame()
    {
        scene.PrepareFrame();

        if (!_presenter.TryBeginFrame(out var frame))
            return;

        var activeFrame = frame!;
        try
        {
            var commands = activeFrame.CommandList;
            commands.Begin();
            scene.Record(activeFrame, commands);
            commands.End();
        }
        finally
        {
            _presenter.EndFrame(activeFrame);
        }
    }

    private void Shutdown()
    {
        if (_graphicsBackendInitialized)
            _graphicsBackend.Device.WaitIdle();

        if (_sceneInitialized)
            scene.Destroy();

        if (_graphicsBackendInitialized)
            _graphicsBackend.Destroy();

        if (_engineInitialized)
            Engine.Shutdown();
    }
}