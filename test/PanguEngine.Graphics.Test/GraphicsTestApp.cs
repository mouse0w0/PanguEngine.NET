using PanguEngine.Graphics.Vulkan;
using Silk.NET.Core.Native;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;
using Window = PanguEngine.Windowing.Window;

namespace PanguEngine.Graphics.Test;

/// <summary>
/// Runs a backend-independent graphics test scene.
/// </summary>
/// <param name="scene">The scene to run.</param>
public sealed unsafe class GraphicsTestApp(IGraphicsTestScene scene)
{
    private Window _window = null!;
    private VulkanPresenter _presenter = null!;
    private bool _sceneInitialized;
    private bool _presenterInitialized;
    private bool _windowInitialized;
    private bool _graphicsContextInitialized;
    private bool _uploaderInitialized;
    private bool _allocatorInitialized;
    private bool _contextInitialized;
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

            _window.Run();
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

        var options = WindowOptions.DefaultVulkan with
        {
            Size = new Vector2D<int>(800, 600),
            Title = scene.Name
        };

        var silkWindow = Silk.NET.Windowing.Window.Create(options);
        silkWindow.Initialize();

        if (silkWindow.VkSurface is null)
            throw new InvalidOperationException("Windowing platform doesn't support Vulkan.");

        var glfwExtensions = silkWindow.VkSurface.GetRequiredExtensions(out var count);
        var requiredExtensions = SilkMarshal.PtrToStringArray((nint)glfwExtensions, (int)count);

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

        _window = new VulkanWindow(silkWindow, surface);
        _windowInitialized = true;

        _presenter = new VulkanPresenter((VulkanWindow)_window);
        _presenterInitialized = true;
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
        if (_contextInitialized)
            VulkanContext.Vk.DeviceWaitIdle(VulkanContext.Device);

        if (_sceneInitialized)
            scene.Destroy();

        if (_presenterInitialized)
            _presenter.Destroy();

        if (_windowInitialized)
            _window.Destroy();

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