using PanguEngine.Windowing;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using SilkWindow = Silk.NET.Windowing.IWindow;
using SilkWindowCreator = Silk.NET.Windowing.Window;

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
    public override DisplayManager DisplayManager { get; }

    /// <inheritdoc/>
    public override WindowManager WindowManager { get; }

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

        SilkWindow? silkWindow = null;
        SurfaceKHR surface = default;
        VulkanWindow? primaryWindow = null;
        var instanceInitialized = false;
        var allocatorInitialized = false;
        var uploaderInitialized = false;
        var silkOptions = VulkanWindowFactory.CreateSilkWindowOptions(options.PrimaryWindow);

        try
        {
            silkWindow = SilkWindowCreator.Create(silkOptions);
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
            var displayManager = new VulkanDisplayManager(silkWindow);

            try
            {
                primaryWindow = new VulkanWindow(silkWindow, surface, true, options.PrimaryWindow.FramesPerSecond);
            }
            catch
            {
                surface = default;
                silkWindow = null;
                throw;
            }

            if (options.PrimaryWindow.Icons.Length > 0)
                primaryWindow.SetWindowIcons(options.PrimaryWindow.Icons);

            var windowManager = new WindowManager(primaryWindow, VulkanWindowFactory.CreateWindow);

            Device = device;
            DisplayManager = displayManager;
            PrimaryWindow = primaryWindow;
            WindowManager = windowManager;
        }
        catch
        {
            primaryWindow?.Destroy();

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
        VulkanContext.EnsureRenderThread();
        if (_isDestroyed) return;
        _isDestroyed = true;

        WindowManager.Destroy();
        VulkanUploader.Destroy();
        VulkanDeletionQueue.Drain();
        VulkanAllocator.Destroy();
        VulkanContext.Destroy();
    }

    /// <inheritdoc/>
    internal override void Render(double alpha)
    {
        WindowManager.RenderWindows(alpha);
        VulkanDeletionQueue.Collect();
    }
}
