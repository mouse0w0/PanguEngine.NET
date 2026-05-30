using PanguEngine.Graphics;
using PanguEngine.Windowing;
using Silk.NET.Maths;

namespace PanguEngine.Client.Test.MultiWindow;

internal static unsafe class MultiWindowTest
{
    private static GraphicsBackend? _graphicsBackend;
    private static WindowManager? _windowManager;
    private static Window? _primary;
    private static Window? _secondary;
    private static bool _engineInitialized;
    private static bool _graphicsBackendInitialized;

    private static void Main()
    {
        try
        {
            Initialize();
            _primary!.Render += (_, _) => Draw(_primary.Presenter, new ClearColor(0.08f, 0.02f, 0.02f, 1));
            _secondary!.Render += (_, _) => Draw(_secondary.Presenter, new ClearColor(0.02f, 0.08f, 0.02f, 1));
            new ClientLoop(
                () => _windowManager!.Windows.Count > 0,
                _windowManager!.DoEvents,
                () => { },
                _windowManager!.RenderWindows).Run();
        }
        finally
        {
            Shutdown();
        }
    }

    private static void Initialize()
    {
        Engine.Initialize();
        _engineInitialized = true;

        _graphicsBackend = GraphicsBackendFactory.Create(GraphicsBackendType.Vulkan, new GraphicsBackendOptions
        {
            PrimaryWindow = new WindowOptions
            {
                Size = new Vector2D<int>(640, 480),
                Title = "MultiWindow Primary"
            }
        });
        _graphicsBackendInitialized = true;

        _windowManager = _graphicsBackend.WindowManager;
        _primary = _graphicsBackend.PrimaryWindow;
        _secondary = _windowManager.CreateWindow(new WindowOptions
        {
            Title = "MultiWindow Secondary",
            Size = new Vector2D<int>(640, 480),
            FramesPerSecond = 60
        });
    }

    private static void Draw(Presenter presenter, ClearColor clearColor)
    {
        if (!presenter.TryBeginFrame(out var frame))
            return;

        var activeFrame = frame!;
        try
        {
            var commands = activeFrame.CommandList;
            commands.Begin();
            commands.BeginRendering(new RenderingDescription(clearColor));
            commands.EndRendering();
            commands.End();
        }
        finally
        {
            presenter.EndFrame(activeFrame);
        }
    }

    private static void Shutdown()
    {
        if (_graphicsBackendInitialized)
            _graphicsBackend!.Device.WaitIdle();

        if (_graphicsBackendInitialized)
            _graphicsBackend!.Destroy();

        if (_engineInitialized)
            Engine.Shutdown();
    }
}