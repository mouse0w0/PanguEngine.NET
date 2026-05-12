using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace PanguEngine.Graphics.Vulkan;

/// <summary>
/// Manages a Vulkan swapchain surface and its associated rendering resources bound to a window.
/// </summary>
public sealed unsafe class VulkanWindow
{
    private bool _framebufferResized;
    private bool _destroyed;

    private SwapchainKHR _swapchain;
    private Image[]? _images;
    private ImageView[]? _imageViews;
    private Semaphore[]? _imageAvailableSemaphores;
    private Semaphore[]? _renderFinishedSemaphores;
    private Fence[]? _inFlightFences;

    /// <summary>The underlying window used for presentation.</summary>
    public IWindow Window { get; private set; }

    /// <summary>The Vulkan surface created from the window.</summary>
    public SurfaceKHR Surface { get; private set; }

    /// <summary>The swapchain handle.</summary>
    public SwapchainKHR Swapchain => _swapchain;

    /// <summary>The image format selected for the swapchain images.</summary>
    public Format ImageFormat { get; private set; }

    /// <summary>The current extent (width and height) of the swapchain images.</summary>
    public Extent2D Extent { get; private set; }

    /// <summary>The image handles for each swapchain image.</summary>
    public Image[] Images => _images!;

    /// <summary>The image views for each swapchain image.</summary>
    public ImageView[] ImageViews => _imageViews!;

    /// <summary>The current frame index within the in-flight frame ring.</summary>
    public uint CurrentFrame { get; private set; }

    /// <summary>Creates a <see cref="VulkanWindow"/> with default Vulkan window options.</summary>
    internal VulkanWindow() : this(WindowOptions.DefaultVulkan)
    {
    }

    /// <summary>Creates a <see cref="VulkanWindow"/> with the specified window options.</summary>
    internal VulkanWindow(WindowOptions options)
    {
        Window = Silk.NET.Windowing.Window.Create(options);
        Window.Initialize();
        Surface = Window.VkSurface!.Create<AllocationCallbacks>(VulkanContext.VkInstance.ToHandle(), null).ToSurface();

        Initialize();
    }

    /// <summary>Creates a <see cref="VulkanWindow"/> from an existing window and surface.</summary>
    internal VulkanWindow(IWindow window, SurfaceKHR surface)
    {
        Window = window;
        Surface = surface;

        Initialize();
    }

    /// <summary>Acquires the next swapchain image for rendering.</summary>
    public Result AcquireNextImage(out uint imageIndex)
    {
        imageIndex = 0;
        var result = VulkanContext.KhrSwapchain.AcquireNextImage(
            VulkanContext.Device, _swapchain, ulong.MaxValue,
            _imageAvailableSemaphores![CurrentFrame], default, ref imageIndex);

        if (result == Result.ErrorOutOfDateKhr)
        {
            RecreateSwapchain();
            return result;
        }

        if (result != Result.Success && result != Result.SuboptimalKhr)
            throw new InvalidOperationException("Failed to acquire swap chain image.");

        return result;
    }

    /// <summary>Presents the rendered image at the given swapchain image index.</summary>
    public void PresentImage(uint imageIndex)
    {
        var swapChains = stackalloc[] { _swapchain };
        var signalSemaphores = stackalloc[] { _renderFinishedSemaphores![CurrentFrame] };

        PresentInfoKHR presentInfo = new()
        {
            SType = StructureType.PresentInfoKhr,
            WaitSemaphoreCount = 1,
            PWaitSemaphores = signalSemaphores,
            SwapchainCount = 1,
            PSwapchains = swapChains,
            PImageIndices = &imageIndex
        };

        var result = VulkanContext.KhrSwapchain.QueuePresent(VulkanContext.PresentQueue, in presentInfo);

        if (result == Result.ErrorOutOfDateKhr || result == Result.SuboptimalKhr || _framebufferResized)
        {
            _framebufferResized = false;
            RecreateSwapchain();
            return;
        }

        if (result != Result.Success)
            throw new InvalidOperationException("Failed to present swap chain image.");
    }

    /// <summary>Advances the current frame index to the next in-flight frame slot.</summary>
    public void AdvanceFrame()
    {
        CurrentFrame = (CurrentFrame + 1) % VulkanContext.MaxFramesInFlight;
    }

    /// <summary>Blocks until the in-flight fence for the current frame is signaled.</summary>
    public void WaitForInFlightFence()
    {
        VulkanContext.Vk.WaitForFences(VulkanContext.Device, 1, in _inFlightFences![CurrentFrame], true,
            ulong.MaxValue);
    }

    /// <summary>Resets the in-flight fence for the current frame back to unsignaled state.</summary>
    public void ResetInFlightFence()
    {
        VulkanContext.Vk.ResetFences(VulkanContext.Device, 1, in _inFlightFences![CurrentFrame]);
    }

    /// <summary>Gets a reference to the image-available semaphore for the current frame.</summary>
    public ref Semaphore GetImageAvailableSemaphore() => ref _imageAvailableSemaphores![CurrentFrame];

    /// <summary>Gets a reference to the render-finished semaphore for the current frame.</summary>
    public ref Semaphore GetRenderFinishedSemaphore() => ref _renderFinishedSemaphores![CurrentFrame];

    /// <summary>Gets a reference to the in-flight fence for the current frame.</summary>
    public ref Fence GetInFlightFence() => ref _inFlightFences![CurrentFrame];

    /// <summary>Releases all Vulkan resources held by this instance.</summary>
    public void Destroy()
    {
        if (_destroyed) return;
        _destroyed = true;

        for (var i = 0; i < VulkanContext.MaxFramesInFlight; i++)
        {
            VulkanContext.Vk.DestroySemaphore(VulkanContext.Device, _renderFinishedSemaphores![i], null);
            VulkanContext.Vk.DestroySemaphore(VulkanContext.Device, _imageAvailableSemaphores![i], null);
            VulkanContext.Vk.DestroyFence(VulkanContext.Device, _inFlightFences![i], null);
        }

        DestroyImageViews();
        DestroySwapchain();

        VulkanContext.KhrSurface.DestroySurface(VulkanContext.VkInstance, Surface, null);
        Window.Dispose();
    }

    private void Initialize()
    {
        Window.Resize += OnFramebufferResize;

        CreateSwapchain();
        CreateImageViews();
        CreateSyncObjects();
    }

    private void RecreateSwapchain()
    {
        var framebufferSize = Window.FramebufferSize;

        while (framebufferSize.X == 0 || framebufferSize.Y == 0)
        {
            framebufferSize = Window.FramebufferSize;
            Window.DoEvents();
        }

        VulkanContext.Vk.DeviceWaitIdle(VulkanContext.Device);

        DestroyImageViews();
        DestroySwapchain();

        CreateSwapchain();
        CreateImageViews();
    }

    private void OnFramebufferResize(Vector2D<int> _) => _framebufferResized = true;

    private void CreateSwapchain()
    {
        var swapChainSupport = VulkanContext.QuerySwapChainSupport(VulkanContext.PhysicalDevice, Surface);
        var surfaceFormat = ChooseSwapSurfaceFormat(swapChainSupport.Formats);
        var presentMode = ChoosePresentMode(swapChainSupport.PresentModes);
        var extent = ChooseSwapExtent(swapChainSupport.Capabilities);

        var imageCount = VulkanContext.MaxFramesInFlight;

        var queueFamilyIndices = stackalloc[] { VulkanContext.GraphicsQueueFamily, VulkanContext.PresentQueueFamily };

        SwapchainCreateInfoKHR createInfo = new()
        {
            SType = StructureType.SwapchainCreateInfoKhr,
            Surface = Surface,
            MinImageCount = imageCount,
            ImageFormat = surfaceFormat.Format,
            ImageColorSpace = surfaceFormat.ColorSpace,
            ImageExtent = extent,
            ImageArrayLayers = 1,
            ImageUsage = ImageUsageFlags.ColorAttachmentBit,
        };

        if (VulkanContext.GraphicsQueueFamily != VulkanContext.PresentQueueFamily)
        {
            createInfo = createInfo with
            {
                ImageSharingMode = SharingMode.Concurrent,
                QueueFamilyIndexCount = 2,
                PQueueFamilyIndices = queueFamilyIndices,
            };
        }
        else
        {
            createInfo.ImageSharingMode = SharingMode.Exclusive;
        }

        createInfo = createInfo with
        {
            PreTransform = swapChainSupport.Capabilities.CurrentTransform,
            CompositeAlpha = CompositeAlphaFlagsKHR.OpaqueBitKhr,
            PresentMode = presentMode,
            Clipped = true,
        };

        if (VulkanContext.KhrSwapchain.CreateSwapchain(VulkanContext.Device, in createInfo, null, out _swapchain) !=
            Result.Success)
            throw new InvalidOperationException("Failed to create swap chain.");

        VulkanContext.KhrSwapchain.GetSwapchainImages(VulkanContext.Device, _swapchain, ref imageCount, null);
        _images = new Image[imageCount];
        fixed (Image* swapchainImagesPtr = _images)
        {
            VulkanContext.KhrSwapchain.GetSwapchainImages(VulkanContext.Device, _swapchain, ref imageCount,
                swapchainImagesPtr);
        }

        ImageFormat = surfaceFormat.Format;
        Extent = extent;
    }

    private void CreateImageViews()
    {
        _imageViews = new ImageView[_images!.Length];

        for (var i = 0; i < _images.Length; i++)
        {
            ImageViewCreateInfo createInfo = new()
            {
                SType = StructureType.ImageViewCreateInfo,
                Image = _images[i],
                ViewType = ImageViewType.Type2D,
                Format = ImageFormat,
                Components =
                {
                    R = ComponentSwizzle.Identity,
                    G = ComponentSwizzle.Identity,
                    B = ComponentSwizzle.Identity,
                    A = ComponentSwizzle.Identity,
                },
                SubresourceRange =
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    BaseMipLevel = 0,
                    LevelCount = 1,
                    BaseArrayLayer = 0,
                    LayerCount = 1,
                }
            };

            if (VulkanContext.Vk.CreateImageView(VulkanContext.Device, in createInfo, null, out _imageViews[i]) !=
                Result.Success)
                throw new InvalidOperationException("Failed to create image views.");
        }
    }

    private void CreateSyncObjects()
    {
        _imageAvailableSemaphores = new Semaphore[VulkanContext.MaxFramesInFlight];
        _renderFinishedSemaphores = new Semaphore[VulkanContext.MaxFramesInFlight];
        _inFlightFences = new Fence[VulkanContext.MaxFramesInFlight];

        SemaphoreCreateInfo semaphoreInfo = new()
        {
            SType = StructureType.SemaphoreCreateInfo,
        };

        FenceCreateInfo fenceInfo = new()
        {
            SType = StructureType.FenceCreateInfo,
            Flags = FenceCreateFlags.SignaledBit,
        };

        for (var i = 0; i < VulkanContext.MaxFramesInFlight; i++)
        {
            if (VulkanContext.Vk.CreateSemaphore(VulkanContext.Device, in semaphoreInfo, null,
                    out _imageAvailableSemaphores[i]) != Result.Success ||
                VulkanContext.Vk.CreateSemaphore(VulkanContext.Device, in semaphoreInfo, null,
                    out _renderFinishedSemaphores[i]) != Result.Success ||
                VulkanContext.Vk.CreateFence(VulkanContext.Device, in fenceInfo, null, out _inFlightFences[i]) !=
                Result.Success)
            {
                throw new InvalidOperationException("Failed to create synchronization objects for a frame.");
            }
        }
    }

    private void DestroyImageViews()
    {
        foreach (var imageView in _imageViews!)
            VulkanContext.Vk.DestroyImageView(VulkanContext.Device, imageView, null);
    }

    private void DestroySwapchain()
    {
        VulkanContext.KhrSwapchain.DestroySwapchain(VulkanContext.Device, _swapchain, null);
    }

    private SurfaceFormatKHR ChooseSwapSurfaceFormat(IReadOnlyList<SurfaceFormatKHR> availableFormats)
    {
        foreach (var format in availableFormats)
        {
            if (format is { Format: Format.B8G8R8A8Srgb, ColorSpace: ColorSpaceKHR.SpaceSrgbNonlinearKhr })
                return format;
        }

        return availableFormats[0];
    }

    private PresentModeKHR ChoosePresentMode(IReadOnlyList<PresentModeKHR> availablePresentModes)
    {
        foreach (var mode in availablePresentModes)
        {
            if (mode == PresentModeKHR.MailboxKhr)
                return mode;
        }

        return PresentModeKHR.FifoKhr;
    }

    private Extent2D ChooseSwapExtent(SurfaceCapabilitiesKHR capabilities)
    {
        if (capabilities.CurrentExtent.Width != uint.MaxValue)
            return capabilities.CurrentExtent;

        var framebufferSize = Window.FramebufferSize;

        Extent2D actualExtent = new()
        {
            Width = (uint)framebufferSize.X,
            Height = (uint)framebufferSize.Y
        };

        actualExtent.Width = Math.Clamp(actualExtent.Width, capabilities.MinImageExtent.Width,
            capabilities.MaxImageExtent.Width);
        actualExtent.Height = Math.Clamp(actualExtent.Height, capabilities.MinImageExtent.Height,
            capabilities.MaxImageExtent.Height);

        return actualExtent;
    }
}