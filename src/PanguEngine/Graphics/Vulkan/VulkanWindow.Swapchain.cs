using Silk.NET.Vulkan;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace PanguEngine.Graphics.Vulkan;

/// <inheritdoc/>
public sealed unsafe partial class VulkanWindow
{
    private bool _framebufferResized;

    private SwapchainKHR _swapchain;
    private Image[]? _images;
    private VulkanSwapchainTexture?[]? _colorOutputTextures;
    private VulkanSwapchainTextureView?[]? _colorOutputs;
    private Semaphore[]? _renderFinishedSemaphores;

    /// <summary>The image format selected for the swapchain images.</summary>
    public Format ImageFormat { get; private set; }

    /// <summary>The current extent (width and height) of the swapchain images.</summary>
    public Extent2D Extent { get; private set; }

    /// <summary>The color output texture views for each swapchain image.</summary>
    internal VulkanSwapchainTextureView?[] ColorOutputs => _colorOutputs!;

    /// <summary>Acquires the next swapchain image for rendering.</summary>
    /// <param name="imageAvailableSemaphore">The semaphore to signal when the image is available.</param>
    /// <param name="imageIndex">The acquired swapchain image index.</param>
    /// <returns>The Vulkan acquisition result.</returns>
    public Result AcquireNextImage(Semaphore imageAvailableSemaphore, out uint imageIndex)
    {
        VulkanContext.EnsureRenderThread();

        imageIndex = 0;
        var result = VulkanContext.KhrSwapchain.AcquireNextImage(
            VulkanContext.Device, _swapchain, ulong.MaxValue,
            imageAvailableSemaphore, default, ref imageIndex);

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
    /// <param name="renderFinishedSemaphore">The semaphore signaled when rendering is complete.</param>
    public void PresentImage(uint imageIndex, Semaphore renderFinishedSemaphore)
    {
        VulkanContext.EnsureRenderThread();

        var swapChains = stackalloc[] { _swapchain };
        var signalSemaphores = stackalloc[] { renderFinishedSemaphore };

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

    /// <summary>Gets the render-finished semaphore assigned to a swapchain image.</summary>
    /// <param name="imageIndex">The swapchain image index.</param>
    /// <returns>The render-finished semaphore.</returns>
    public Semaphore GetRenderFinishedSemaphore(uint imageIndex) =>
        _renderFinishedSemaphores![checked((int)imageIndex)];

    /// <summary>Initializes swapchain resources for the window.</summary>
    private void InitializeSwapchain()
    {
        CreateSwapchain();
        CreateImageViews();
        CreateRenderFinishedSemaphores();
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

        if (VulkanContext.Vk.DeviceWaitIdle(VulkanContext.Device) != Result.Success)
            throw new InvalidOperationException("Failed to wait for the device before recreating the swap chain.");

        DestroyRenderFinishedSemaphores();
        DestroyImageViews();
        DestroySwapchain();

        CreateSwapchain();
        CreateImageViews();
        CreateRenderFinishedSemaphores();
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
        var images = _images!;
        var colorOutputTextures = new VulkanSwapchainTexture?[images.Length];
        var colorOutputs = new VulkanSwapchainTextureView?[images.Length];
        _colorOutputTextures = colorOutputTextures;
        _colorOutputs = colorOutputs;
        var colorFormat = VulkanMapping.FromVulkanFormat(ImageFormat);

        try
        {
            for (var i = 0; i < images.Length; i++)
            {
                var texture = new VulkanSwapchainTexture(images[i], colorFormat, Extent.Width, Extent.Height);
                colorOutputTextures[i] = texture;
                ImageViewCreateInfo createInfo = new()
                {
                    SType = StructureType.ImageViewCreateInfo,
                    Image = images[i],
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

                if (VulkanContext.Vk.CreateImageView(VulkanContext.Device, in createInfo, null, out var imageView) !=
                    Result.Success)
                    throw new InvalidOperationException("Failed to create image views.");

                colorOutputs[i] = new VulkanSwapchainTextureView(texture, imageView);
            }
        }
        catch
        {
            DestroyImageViews();
            throw;
        }
    }

    /// <summary>Creates render-finished semaphores for the swapchain images.</summary>
    private void CreateRenderFinishedSemaphores()
    {
        var semaphores = new Semaphore[_images!.Length];
        _renderFinishedSemaphores = semaphores;

        SemaphoreCreateInfo semaphoreInfo = new()
        {
            SType = StructureType.SemaphoreCreateInfo
        };

        try
        {
            for (var i = 0; i < semaphores.Length; i++)
            {
                if (VulkanContext.Vk.CreateSemaphore(VulkanContext.Device, in semaphoreInfo, null,
                        out var semaphore) != Result.Success)
                    throw new InvalidOperationException("Failed to create render-finished semaphore.");
                semaphores[i] = semaphore;
            }
        }
        catch
        {
            DestroyRenderFinishedSemaphores();
            throw;
        }
    }

    /// <summary>Destroys the render-finished semaphores owned by the swapchain.</summary>
    private void DestroyRenderFinishedSemaphores()
    {
        if (_renderFinishedSemaphores is null)
            return;

        foreach (var semaphore in _renderFinishedSemaphores)
        {
            if (semaphore.Handle != 0)
                VulkanContext.Vk.DestroySemaphore(VulkanContext.Device, semaphore, null);
        }

        _renderFinishedSemaphores = null;
    }

    /// <summary>Destroys swapchain image views.</summary>
    private void DestroyImageViews()
    {
        if (_colorOutputs is not null)
        {
            foreach (var colorOutput in _colorOutputs)
            {
                if (colorOutput is null)
                    continue;

                colorOutput.Invalidate();
                if (colorOutput.ImageView.Handle != 0)
                    VulkanContext.Vk.DestroyImageView(VulkanContext.Device, colorOutput.ImageView, null);
            }

            _colorOutputs = null;
        }

        if (_colorOutputTextures is not null)
        {
            foreach (var texture in _colorOutputTextures)
                texture?.Invalidate();
            _colorOutputTextures = null;
        }
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
        if (availableFormats.Length == 1 && availableFormats[0].Format == Format.Undefined)
        {
            if (availableFormats[0].ColorSpace != ColorSpaceKHR.SpaceSrgbNonlinearKhr)
                throw new NotSupportedException("The surface does not support the SRGB nonlinear color space.");

            return new SurfaceFormatKHR
            {
                Format = Format.B8G8R8A8Srgb,
                ColorSpace = availableFormats[0].ColorSpace
            };
        }

        foreach (var format in availableFormats)
        {
            if (format is { Format: Format.B8G8R8A8Srgb, ColorSpace: ColorSpaceKHR.SpaceSrgbNonlinearKhr })
                return format;
        }

        foreach (var format in availableFormats)
        {
            if (format is { Format: Format.R8G8B8A8Srgb, ColorSpace: ColorSpaceKHR.SpaceSrgbNonlinearKhr })
                return format;
        }

        throw new NotSupportedException("The surface does not support an sRGB swapchain format.");
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