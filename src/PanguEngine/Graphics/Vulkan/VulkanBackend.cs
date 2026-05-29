using PanguEngine.Windowing;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using SilkWindow = Silk.NET.Windowing.IWindow;
using SilkWindowOptions = Silk.NET.Windowing.WindowOptions;

namespace PanguEngine.Graphics.Vulkan;

/// <summary>
/// Vulkan implementation of <see cref="GraphicsBackend"/>.
/// </summary>
internal sealed unsafe class VulkanBackend : GraphicsBackend
{
    private bool _isDestroyed;

    /// <inheritdoc/>
    public override GraphicsBackendType Type => GraphicsBackendType.Vulkan;

    /// <inheritdoc/>
    public override GraphicsDevice Device { get; }

    /// <inheritdoc/>
    public override WindowManager WindowManager { get; }

    /// <inheritdoc/>
    public override Window PrimaryWindow { get; }

    /// <inheritdoc/>
    public override bool IsDestroyed => _isDestroyed;

    /// <summary>
    /// Creates and initializes a Vulkan graphics backend.
    /// </summary>
    /// <param name="options">The backend initialization options.</param>
    internal VulkanBackend(GraphicsBackendOptions options)
    {
        SilkWindow? silkWindow = null;
        SurfaceKHR surface = default;
        VulkanWindow? primaryWindow = null;
        var instanceInitialized = false;
        var allocatorInitialized = false;
        var uploaderInitialized = false;
        var graphicsContextInitialized = false;

        var silkOptions = SilkWindowOptions.DefaultVulkan with
        {
            IsVisible = options.PrimaryWindow.IsVisible,
            Position = options.PrimaryWindow.Position,
            Size = options.PrimaryWindow.Size,
            Title = options.PrimaryWindow.Title,
            WindowBorder = options.PrimaryWindow.WindowBorder switch
            {
                WindowBorder.Fixed => Silk.NET.Windowing.WindowBorder.Fixed,
                WindowBorder.Hidden => Silk.NET.Windowing.WindowBorder.Hidden,
                _ => Silk.NET.Windowing.WindowBorder.Resizable
            }
        };

        try
        {
            silkWindow = Silk.NET.Windowing.Window.Create(silkOptions);
            silkWindow.Initialize();

            if (silkWindow.VkSurface is null)
                throw new InvalidOperationException("Windowing platform doesn't support Vulkan.");

            var glfwExtensions = silkWindow.VkSurface.GetRequiredExtensions(out var count);
            var requiredExtensions = SilkMarshal.PtrToStringArray((nint)glfwExtensions, (int)count);

            VulkanContext.InitializeInstance(requiredExtensions);
            instanceInitialized = true;

            surface = silkWindow.VkSurface.Create<AllocationCallbacks>(VulkanContext.VkInstance.ToHandle(), null)
                .ToSurface();
            VulkanContext.InitializeDevice(surface);

            VulkanAllocator.Initialize();
            allocatorInitialized = true;

            VulkanUploader.Initialize();
            uploaderInitialized = true;

            var device = new VulkanGraphicsDevice();
            GraphicsContext.Initialize(device);
            graphicsContextInitialized = true;

            primaryWindow = new VulkanWindow(silkWindow, surface, true, options.PrimaryWindow.FramesPerSecond);
            var windowManager = new WindowManager(primaryWindow, VulkanWindowFactory.CreateWindow);

            Device = device;
            PrimaryWindow = primaryWindow;
            WindowManager = windowManager;
        }
        catch
        {
            if (primaryWindow is not null)
                primaryWindow?.Destroy();

            if (graphicsContextInitialized)
                GraphicsContext.Shutdown();
            if (uploaderInitialized)
                VulkanUploader.Destroy();
            if (allocatorInitialized)
            {
                VulkanDeletionQueue.Drain();
                VulkanAllocator.Destroy();
            }

            if (surface.Handle != 0 && primaryWindow is null && instanceInitialized)
                VulkanContext.KhrSurface.DestroySurface(VulkanContext.VkInstance, surface, null);
            if (instanceInitialized)
                VulkanContext.Destroy();
            if (primaryWindow is null)
                silkWindow?.Dispose();

            throw;
        }
    }

    /// <inheritdoc/>
    internal override void Destroy()
    {
        if (_isDestroyed) return;
        _isDestroyed = true;

        WindowManager.Destroy();
        GraphicsContext.Shutdown();
        VulkanUploader.Destroy();
        VulkanDeletionQueue.Drain();
        VulkanAllocator.Destroy();
        VulkanContext.Destroy();
    }
}