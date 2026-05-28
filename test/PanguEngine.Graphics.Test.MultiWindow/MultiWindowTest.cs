using PanguEngine.Client;
using PanguEngine.Graphics.Vulkan;
using PanguEngine.Windowing;
using Silk.NET.Core.Native;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using SilkWindowOptions = Silk.NET.Windowing.WindowOptions;

namespace PanguEngine.Graphics.Test.MultiWindow;

internal static unsafe class MultiWindowTest
{
    private static WindowManager? _windowManager;
    private static Window? _primary;
    private static Window? _secondary;
    private static bool _engineInitialized;
    private static bool _contextInitialized;
    private static bool _allocatorInitialized;
    private static bool _uploaderInitialized;
    private static bool _graphicsContextInitialized;

    private static void Main()
    {
        try
        {
            Initialize();
            _primary!.Render += (_, _) => Draw(_primary.Presenter, new ClearColor(0.08f, 0.02f, 0.02f, 1));
            _secondary!.Render += (_, _) => Draw(_secondary.Presenter, new ClearColor(0.02f, 0.08f, 0.02f, 1));
            new ClientLoop(_windowManager!).Run();
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

        var silkOptions = SilkWindowOptions.DefaultVulkan with
        {
            Size = new Vector2D<int>(640, 480),
            Title = "MultiWindow Primary"
        };

        var silkWindow = Silk.NET.Windowing.Window.Create(silkOptions);
        silkWindow.Initialize();
        if (silkWindow.VkSurface is null)
            throw new InvalidOperationException("Windowing platform doesn't support Vulkan.");

        var extensions = silkWindow.VkSurface.GetRequiredExtensions(out var count);
        var requiredExtensions = SilkMarshal.PtrToStringArray((nint)extensions, (int)count);
        VulkanContext.InitializeInstance(requiredExtensions);
        _contextInitialized = true;

        var surface = silkWindow.VkSurface.Create<AllocationCallbacks>(VulkanContext.VkInstance.ToHandle(), null)
            .ToSurface();
        VulkanContext.InitializeDevice(surface);
        VulkanAllocator.Initialize();
        _allocatorInitialized = true;
        VulkanUploader.Initialize();
        _uploaderInitialized = true;
        GraphicsContext.Initialize(new VulkanGraphicsDevice());
        _graphicsContextInitialized = true;

        var primaryWindow = new VulkanWindow(silkWindow, surface, true);

        _windowManager = new WindowManager(primaryWindow, VulkanWindowFactory.CreateWindow);
        _primary = primaryWindow;
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
        if (_contextInitialized)
            VulkanContext.Vk.DeviceWaitIdle(VulkanContext.Device);

        _windowManager?.Destroy();

        if (_graphicsContextInitialized)
            GraphicsContext.Shutdown();

        if (_uploaderInitialized)
            VulkanUploader.Destroy();

        if (_allocatorInitialized)
        {
            VulkanDeletionQueue.Drain();
            VulkanAllocator.Destroy();
        }

        if (_contextInitialized)
            VulkanContext.Destroy();

        if (_engineInitialized)
            Engine.Shutdown();
    }
}