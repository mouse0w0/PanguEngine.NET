using PanguEngine.Windowing;
using Silk.NET.Vulkan;
using SDL;

namespace PanguEngine.Graphics.Vulkan;

/// <summary>
/// Vulkan implementation of <see cref="GraphicsBackend"/>.
/// </summary>
internal sealed unsafe class VulkanBackend : GraphicsBackend
{
    private readonly SdlPlatform _platform;
    private readonly VulkanWindowManager _windowManager;
    private bool _isDestroyed;

    /// <inheritdoc/>
    public override GraphicsBackendType Type => GraphicsBackendType.Vulkan;

    /// <inheritdoc/>
    public override GraphicsDevice Device { get; }

    /// <inheritdoc/>
    public override DisplayManager DisplayManager { get; }

    /// <inheritdoc/>
    public override WindowManager WindowManager => _windowManager;

    /// <inheritdoc/>
    public override Window PrimaryWindow { get; }

    /// <inheritdoc/>
    // ReSharper disable once ConvertToAutoPropertyWithPrivateSetter
    public override bool IsDestroyed => _isDestroyed;

    /// <summary>
    /// Creates and initializes a Vulkan graphics backend.
    /// </summary>
    /// <param name="options">The backend initialization options.</param>
    internal VulkanBackend(GraphicsBackendOptions options)
    {
        VulkanContext.BindRenderThread();

        var platform = new SdlPlatform();
        _platform = platform;
        var windowManager = new VulkanWindowManager(platform);
        _windowManager = windowManager;
        SDL_Window* nativeWindow = null;
        SurfaceKHR surface = default;
        VulkanWindow? primaryWindow = null;
        var instanceInitialized = false;
        var allocatorInitialized = false;
        var uploaderInitialized = false;
        var windowConstructionStarted = false;

        try
        {
            platform.Initialize();
            nativeWindow = platform.CreateWindow(options.PrimaryWindow);

            var requiredExtensions = SdlPlatform.GetVulkanInstanceExtensions();
            VulkanContext.InitializeInstance(requiredExtensions, options.EnableValidation);
            instanceInitialized = true;

            surface = SdlPlatform.CreateVulkanSurface(nativeWindow);
            VulkanContext.InitializeDevice(surface);

            VulkanAllocator.Initialize();
            allocatorInitialized = true;

            VulkanUploader.Initialize();
            uploaderInitialized = true;

            windowConstructionStarted = true;
            primaryWindow = windowManager.CreatePrimaryWindow(nativeWindow, surface, options.PrimaryWindow);

            var device = new VulkanGraphicsDevice();
            var displayManager = new VulkanDisplayManager();

            Device = device;
            DisplayManager = displayManager;
            PrimaryWindow = primaryWindow;
        }
        catch
        {
            if (!windowConstructionStarted)
            {
                if (surface.Handle != 0 && instanceInitialized)
                    VulkanContext.KhrSurface.DestroySurface(VulkanContext.VkInstance, surface, null);
                if (nativeWindow is not null)
                    platform.DestroyWindow(nativeWindow);
            }

            windowManager.Destroy();
            if (uploaderInitialized)
                VulkanUploader.Destroy();
            if (allocatorInitialized)
            {
                VulkanDeletionQueue.Drain();
                VulkanAllocator.Destroy();
            }

            if (instanceInitialized)
                VulkanContext.Destroy();
            platform.Destroy();
            throw;
        }
    }

    /// <inheritdoc/>
    internal override void Destroy()
    {
        VulkanContext.EnsureRenderThread();
        if (_isDestroyed) return;
        _isDestroyed = true;

        WindowManager.Destroy();
        VulkanUploader.Destroy();
        VulkanDeletionQueue.Drain();
        VulkanAllocator.Destroy();
        VulkanContext.Destroy();
        _platform.Destroy();
    }

    /// <inheritdoc/>
    internal override void Render(double alpha)
    {
        _windowManager.DrainFinalized();
        WindowManager.PreRenderWindows(alpha);
        VulkanUploader.Pump();
        WindowManager.RenderWindows(alpha);
        VulkanDeletionQueue.Collect();
    }
}
