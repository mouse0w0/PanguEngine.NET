using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace PanguEngine.Rendering.Vulkan;

/// <summary>
/// Manages a Vulkan swapchain surface and its associated rendering resources bound to a window.
/// </summary>
public sealed unsafe class VulkanWindow
{
    private readonly VulkanContext _context;

    private bool _framebufferResized;
    private bool _destroyed;

    private SwapchainKHR _swapchain;
    private Image[]? _images;
    private ImageView[]? _imageViews;
    private RenderPass _renderPass;
    private Framebuffer[]? _framebuffers;
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
    /// <summary>The image views for each swapchain image.</summary>
    public ImageView[] ImageViews => _imageViews!;
    /// <summary>The render pass used for rendering to the swapchain.</summary>
    public RenderPass RenderPass => _renderPass;
    /// <summary>The framebuffers corresponding to each swapchain image view.</summary>
    public Framebuffer[] Framebuffers => _framebuffers!;
    /// <summary>The current frame index within the in-flight frame ring.</summary>
    public int CurrentFrame { get; private set; }

    /// <summary>Creates a <see cref="VulkanWindow"/> with default Vulkan window options.</summary>
    internal VulkanWindow(VulkanContext context) : this(context, WindowOptions.DefaultVulkan)
    {
    }

    /// <summary>Creates a <see cref="VulkanWindow"/> with the specified window options.</summary>
    internal VulkanWindow(VulkanContext context, WindowOptions options)
    {
        _context = context;

        Window = Silk.NET.Windowing.Window.Create(options);
        Window.Initialize();
        Surface = Window.VkSurface!.Create<AllocationCallbacks>(_context.VkInstance.ToHandle(), null).ToSurface();

        Initialize();
    }

    /// <summary>Creates a <see cref="VulkanWindow"/> from an existing window and surface.</summary>
    internal VulkanWindow(VulkanContext context, IWindow window, SurfaceKHR surface)
    {
        _context = context;
        Window = window;
        Surface = surface;

        Initialize();
    }

    /// <summary>Acquires the next swapchain image for rendering.</summary>
    public Result AcquireNextImage(out uint imageIndex)
    {
        imageIndex = 0;
        var result = _context.KhrSwapchain.AcquireNextImage(
            _context.Device, _swapchain, ulong.MaxValue,
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

        var result = _context.KhrSwapchain.QueuePresent(_context.PresentQueue, in presentInfo);

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
        CurrentFrame = (CurrentFrame + 1) % _context.MaxFramesInFlight;
    }

    /// <summary>Blocks until the in-flight fence for the current frame is signaled.</summary>
    public void WaitForInFlightFence()
    {
        _context.Vk.WaitForFences(_context.Device, 1, in _inFlightFences![CurrentFrame], true, ulong.MaxValue);
    }

    /// <summary>Resets the in-flight fence for the current frame back to unsignaled state.</summary>
    public void ResetInFlightFence()
    {
        _context.Vk.ResetFences(_context.Device, 1, in _inFlightFences![CurrentFrame]);
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

        for (var i = 0; i < _context.MaxFramesInFlight; i++)
        {
            _context.Vk.DestroySemaphore(_context.Device, _renderFinishedSemaphores![i], null);
            _context.Vk.DestroySemaphore(_context.Device, _imageAvailableSemaphores![i], null);
            _context.Vk.DestroyFence(_context.Device, _inFlightFences![i], null);
        }

        DestroyFramebuffers();
        _context.Vk.DestroyRenderPass(_context.Device, _renderPass, null);
        DestroyImageViews();
        DestroySwapchain();

        _context.KhrSurface.DestroySurface(_context.VkInstance, Surface, null);
        Window.Dispose();
    }

    private void Initialize()
    {
        Window.Resize += OnFramebufferResize;

        CreateSwapchain();
        CreateImageViews();
        CreateRenderPass();
        CreateFramebuffers();
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

        _context.Vk.DeviceWaitIdle(_context.Device);

        DestroyFramebuffers();
        DestroyImageViews();
        DestroySwapchain();

        CreateSwapchain();
        CreateImageViews();
        CreateFramebuffers();
    }

    private void OnFramebufferResize(Vector2D<int> _) => _framebufferResized = true;

    private void CreateSwapchain()
    {
        var swapChainSupport = _context.QuerySwapChainSupport(_context.PhysicalDevice, Surface);
        var surfaceFormat = ChooseSwapSurfaceFormat(swapChainSupport.Formats);
        var presentMode = ChoosePresentMode(swapChainSupport.PresentModes);
        var extent = ChooseSwapExtent(swapChainSupport.Capabilities);

        var imageCount = (uint)_context.MaxFramesInFlight;

        var queueFamilyIndices = stackalloc[] { _context.GraphicsQueueFamily, _context.PresentQueueFamily };

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

        if (_context.GraphicsQueueFamily != _context.PresentQueueFamily)
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

        if (_context.KhrSwapchain.CreateSwapchain(_context.Device, in createInfo, null, out _swapchain) !=
            Result.Success)
            throw new InvalidOperationException("Failed to create swap chain.");

        _context.KhrSwapchain.GetSwapchainImages(_context.Device, _swapchain, ref imageCount, null);
        _images = new Image[imageCount];
        fixed (Image* swapchainImagesPtr = _images)
        {
            _context.KhrSwapchain.GetSwapchainImages(_context.Device, _swapchain, ref imageCount, swapchainImagesPtr);
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

            if (_context.Vk.CreateImageView(_context.Device, in createInfo, null, out _imageViews[i]) !=
                Result.Success)
                throw new InvalidOperationException("Failed to create image views.");
        }
    }

    private void CreateRenderPass()
    {
        AttachmentDescription colorAttachment = new()
        {
            Format = ImageFormat,
            Samples = SampleCountFlags.Count1Bit,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.Store,
            StencilLoadOp = AttachmentLoadOp.DontCare,
            StencilStoreOp = AttachmentStoreOp.DontCare,
            InitialLayout = ImageLayout.Undefined,
            FinalLayout = ImageLayout.PresentSrcKhr,
        };

        AttachmentReference colorAttachmentRef = new()
        {
            Attachment = 0,
            Layout = ImageLayout.ColorAttachmentOptimal,
        };

        SubpassDescription subpass = new()
        {
            PipelineBindPoint = PipelineBindPoint.Graphics,
            ColorAttachmentCount = 1,
            PColorAttachments = &colorAttachmentRef,
        };

        SubpassDependency dependency = new()
        {
            SrcSubpass = Vk.SubpassExternal,
            DstSubpass = 0,
            SrcStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
            SrcAccessMask = 0,
            DstStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
            DstAccessMask = AccessFlags.ColorAttachmentWriteBit,
        };

        RenderPassCreateInfo renderPassInfo = new()
        {
            SType = StructureType.RenderPassCreateInfo,
            AttachmentCount = 1,
            PAttachments = &colorAttachment,
            SubpassCount = 1,
            PSubpasses = &subpass,
            DependencyCount = 1,
            PDependencies = &dependency,
        };

        if (_context.Vk.CreateRenderPass(_context.Device, in renderPassInfo, null, out _renderPass) != Result.Success)
            throw new InvalidOperationException("Failed to create render pass.");
    }

    private void CreateFramebuffers()
    {
        _framebuffers = new Framebuffer[_imageViews!.Length];

        for (var i = 0; i < _imageViews.Length; i++)
        {
            var attachment = _imageViews[i];

            FramebufferCreateInfo framebufferInfo = new()
            {
                SType = StructureType.FramebufferCreateInfo,
                RenderPass = _renderPass,
                AttachmentCount = 1,
                PAttachments = &attachment,
                Width = Extent.Width,
                Height = Extent.Height,
                Layers = 1,
            };

            if (_context.Vk.CreateFramebuffer(_context.Device, in framebufferInfo, null, out _framebuffers[i]) !=
                Result.Success)
                throw new InvalidOperationException("Failed to create framebuffer.");
        }
    }

    private void CreateSyncObjects()
    {
        _imageAvailableSemaphores = new Semaphore[_context.MaxFramesInFlight];
        _renderFinishedSemaphores = new Semaphore[_context.MaxFramesInFlight];
        _inFlightFences = new Fence[_context.MaxFramesInFlight];

        SemaphoreCreateInfo semaphoreInfo = new()
        {
            SType = StructureType.SemaphoreCreateInfo,
        };

        FenceCreateInfo fenceInfo = new()
        {
            SType = StructureType.FenceCreateInfo,
            Flags = FenceCreateFlags.SignaledBit,
        };

        for (var i = 0; i < _context.MaxFramesInFlight; i++)
        {
            if (_context.Vk.CreateSemaphore(_context.Device, in semaphoreInfo, null,
                    out _imageAvailableSemaphores[i]) != Result.Success ||
                _context.Vk.CreateSemaphore(_context.Device, in semaphoreInfo, null,
                    out _renderFinishedSemaphores[i]) != Result.Success ||
                _context.Vk.CreateFence(_context.Device, in fenceInfo, null, out _inFlightFences[i]) != Result.Success)
            {
                throw new InvalidOperationException("Failed to create synchronization objects for a frame.");
            }
        }
    }

    private void DestroyImageViews()
    {
        foreach (var imageView in _imageViews!)
            _context.Vk.DestroyImageView(_context.Device, imageView, null);
    }

    private void DestroySwapchain()
    {
        _context.KhrSwapchain.DestroySwapchain(_context.Device, _swapchain, null);
    }

    private void DestroyFramebuffers()
    {
        if (_framebuffers is null) return;
        foreach (var framebuffer in _framebuffers)
            _context.Vk.DestroyFramebuffer(_context.Device, framebuffer, null);
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