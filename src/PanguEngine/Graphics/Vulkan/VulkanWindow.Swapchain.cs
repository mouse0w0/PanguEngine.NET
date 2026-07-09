using Silk.NET.Vulkan;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace PanguEngine.Graphics.Vulkan;

/// <inheritdoc/>
public sealed unsafe partial class VulkanWindow
{
    private bool _framebufferResized;

    private SwapchainKHR _swapchain;
    private Image[]? _images;
    private ImageView[]? _imageViews;
    private VulkanSwapchainTexture[]? _colorOutputs;
    private Semaphore[]? _imageAvailableSemaphores;
    private Semaphore[]? _renderFinishedSemaphores;
    private Fence[]? _inFlightFences;

    /// <summary>The image format selected for the swapchain images.</summary>
    public Format ImageFormat { get; private set; }

    /// <summary>The current extent (width and height) of the swapchain images.</summary>
    public Extent2D Extent { get; private set; }

    /// <summary>The color output textures for each swapchain image.</summary>
    internal VulkanSwapchainTexture[] ColorOutputs => _colorOutputs!;

    /// <summary>The current frame slot within the in-flight frame ring.</summary>
    public uint CurrentFrameSlot { get; private set; }

    /// <summary>Acquires the next swapchain image for rendering.</summary>
    /// <param name="imageIndex">The acquired swapchain image index.</param>
    /// <returns>The Vulkan acquisition result.</returns>
    public Result AcquireNextImage(out uint imageIndex)
    {
        imageIndex = 0;
        var result = VulkanContext.KhrSwapchain.AcquireNextImage(
            VulkanContext.Device, _swapchain, ulong.MaxValue,
            _imageAvailableSemaphores![CurrentFrameSlot], default, ref imageIndex);

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
    /// <param name="imageIndex">The swapchain image index to present.</param>
    public void PresentImage(uint imageIndex)
    {
        var swapChains = stackalloc[] { _swapchain };
        var signalSemaphores = stackalloc[] { _renderFinishedSemaphores![CurrentFrameSlot] };

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
        CurrentFrameSlot = (CurrentFrameSlot + 1) % VulkanContext.MaxFramesInFlight;
    }

    /// <summary>Blocks until the in-flight fence for the current frame is signaled.</summary>
    public void WaitForInFlightFence()
    {
        VulkanContext.Vk.WaitForFences(VulkanContext.Device, 1, in _inFlightFences![CurrentFrameSlot], true,
            ulong.MaxValue);
    }

    /// <summary>Resets the in-flight fence for the current frame back to unsignaled state.</summary>
    public void ResetInFlightFence()
    {
        VulkanContext.Vk.ResetFences(VulkanContext.Device, 1, in _inFlightFences![CurrentFrameSlot]);
    }

    /// <summary>Gets a reference to the image-available semaphore for the current frame.</summary>
    /// <returns>A reference to the image-available semaphore.</returns>
    public ref Semaphore GetImageAvailableSemaphore() => ref _imageAvailableSemaphores![CurrentFrameSlot];

    /// <summary>Gets a reference to the render-finished semaphore for the current frame.</summary>
    /// <returns>A reference to the render-finished semaphore.</returns>
    public ref Semaphore GetRenderFinishedSemaphore() => ref _renderFinishedSemaphores![CurrentFrameSlot];

    /// <summary>Gets a reference to the in-flight fence for the current frame.</summary>
    /// <returns>A reference to the in-flight fence.</returns>
    public ref Fence GetInFlightFence() => ref _inFlightFences![CurrentFrameSlot];

    /// <summary>Initializes swapchain resources for the window.</summary>
    private void InitializeSwapchain()
    {
        CreateSwapchain();
        CreateImageViews();
        CreateSyncObjects();
    }

    /// <summary>Recreates swapchain resources for the current framebuffer size.</summary>
    private void RecreateSwapchain()
    {
        var framebufferSize = _silkWindow.FramebufferSize;

        while (framebufferSize.X == 0 || framebufferSize.Y == 0)
        {
            framebufferSize = _silkWindow.FramebufferSize;
            _silkWindow.DoEvents();
        }

        VulkanContext.Vk.DeviceWaitIdle(VulkanContext.Device);

        DestroyImageViews();
        DestroySwapchain();

        CreateSwapchain();
        CreateImageViews();
    }

    /// <summary>Creates the window swapchain.</summary>
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
            ImageUsage = ImageUsageFlags.ColorAttachmentBit
        };

        if (VulkanContext.GraphicsQueueFamily != VulkanContext.PresentQueueFamily)
        {
            createInfo = createInfo with
            {
                ImageSharingMode = SharingMode.Concurrent,
                QueueFamilyIndexCount = 2,
                PQueueFamilyIndices = queueFamilyIndices
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
            Clipped = true
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

    /// <summary>Creates image views for the current swapchain images.</summary>
    private void CreateImageViews()
    {
        _imageViews = new ImageView[_images!.Length];
        _colorOutputs = new VulkanSwapchainTexture[_images.Length];
        var colorFormat = VulkanMapping.FromVulkanFormat(ImageFormat);

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
                    A = ComponentSwizzle.Identity
                },
                SubresourceRange =
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    BaseMipLevel = 0,
                    LevelCount = 1,
                    BaseArrayLayer = 0,
                    LayerCount = 1
                }
            };

            if (VulkanContext.Vk.CreateImageView(VulkanContext.Device, in createInfo, null, out _imageViews[i]) !=
                Result.Success)
                throw new InvalidOperationException("Failed to create image views.");

            _colorOutputs[i] = new VulkanSwapchainTexture(_images[i], _imageViews[i], colorFormat,
                Extent.Width, Extent.Height);
        }
    }

    /// <summary>Creates frame synchronization objects.</summary>
    private void CreateSyncObjects()
    {
        _imageAvailableSemaphores = new Semaphore[VulkanContext.MaxFramesInFlight];
        _renderFinishedSemaphores = new Semaphore[VulkanContext.MaxFramesInFlight];
        _inFlightFences = new Fence[VulkanContext.MaxFramesInFlight];

        SemaphoreCreateInfo semaphoreInfo = new()
        {
            SType = StructureType.SemaphoreCreateInfo
        };

        FenceCreateInfo fenceInfo = new()
        {
            SType = StructureType.FenceCreateInfo,
            Flags = FenceCreateFlags.SignaledBit
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

    /// <summary>Destroys swapchain image views.</summary>
    private void DestroyImageViews()
    {
        if (_colorOutputs is not null)
        {
            foreach (var colorOutput in _colorOutputs)
                colorOutput.Invalidate();
            _colorOutputs = null;
        }

        if (_imageViews is null)
            return;

        foreach (var imageView in _imageViews)
        {
            if (imageView.Handle != 0)
                VulkanContext.Vk.DestroyImageView(VulkanContext.Device, imageView, null);
        }

        _imageViews = null;
    }

    /// <summary>Destroys the window swapchain.</summary>
    private void DestroySwapchain()
    {
        if (_swapchain.Handle == 0)
            return;

        VulkanContext.KhrSwapchain.DestroySwapchain(VulkanContext.Device, _swapchain, null);
        _swapchain = default;
    }

    /// <summary>Selects a swapchain surface format.</summary>
    /// <param name="availableFormats">The formats supported by the surface.</param>
    /// <returns>The selected surface format.</returns>
    private static SurfaceFormatKHR ChooseSwapSurfaceFormat(SurfaceFormatKHR[] availableFormats)
    {
        foreach (var format in availableFormats)
        {
            if (format is { Format: Format.B8G8R8A8Srgb, ColorSpace: ColorSpaceKHR.SpaceSrgbNonlinearKhr })
                return format;
        }

        return availableFormats[0];
    }

    /// <summary>Selects a swapchain present mode.</summary>
    /// <param name="availablePresentModes">The present modes supported by the surface.</param>
    /// <returns>The selected present mode.</returns>
    private PresentModeKHR ChoosePresentMode(IReadOnlyList<PresentModeKHR> availablePresentModes)
    {
        if (VSync)
            return PresentModeKHR.FifoKhr;

        foreach (var mode in availablePresentModes)
        {
            if (mode == PresentModeKHR.MailboxKhr)
                return mode;
        }

        foreach (var mode in availablePresentModes)
        {
            if (mode == PresentModeKHR.ImmediateKhr)
                return mode;
        }

        return PresentModeKHR.FifoKhr;
    }

    /// <summary>Selects a swapchain extent for the current framebuffer size.</summary>
    /// <param name="capabilities">The surface capabilities reported by Vulkan.</param>
    /// <returns>The selected swapchain extent.</returns>
    private Extent2D ChooseSwapExtent(SurfaceCapabilitiesKHR capabilities)
    {
        if (capabilities.CurrentExtent.Width != uint.MaxValue)
            return capabilities.CurrentExtent;

        var framebufferSize = _silkWindow.FramebufferSize;

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