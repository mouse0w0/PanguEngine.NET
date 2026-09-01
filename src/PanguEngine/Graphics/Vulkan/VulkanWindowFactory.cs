using PanguEngine.Windowing;
using Silk.NET.Vulkan;

namespace PanguEngine.Graphics.Vulkan;

internal sealed unsafe class VulkanWindowFactory
{
    private readonly SdlPlatform _platform;

    internal VulkanWindowFactory(SdlPlatform platform)
    {
        _platform = platform;
    }

    internal Window CreateWindow(WindowOptions options)
    {
        VulkanContext.EnsureRenderThread();

        var nativeWindow = _platform.CreateWindow(options);
        SurfaceKHR surface = default;
        VulkanWindow? window = null;
        var constructionStarted = false;

        try
        {
            surface = SdlPlatform.CreateVulkanSurface(nativeWindow);
            constructionStarted = true;
            window = new VulkanWindow(_platform, nativeWindow, surface, false, options);
            if (options.Icons.Length > 0)
                window.SetWindowIcons(options.Icons);
            return window;
        }
        catch
        {
            if (window is not null)
            {
                window.Destroy();
            }
            else if (!constructionStarted)
            {
                if (surface.Handle != 0)
                    VulkanContext.KhrSurface.DestroySurface(VulkanContext.VkInstance, surface, null);
                _platform.DestroyWindow(nativeWindow);
            }

            throw;
        }
    }
}
