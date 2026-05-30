using PanguEngine.Windowing;
using Silk.NET.Vulkan;
using SilkWindow = Silk.NET.Windowing.IWindow;
using SilkWindowBorder = Silk.NET.Windowing.WindowBorder;
using SilkWindowCreator = Silk.NET.Windowing.Window;
using SilkWindowOptions = Silk.NET.Windowing.WindowOptions;
using SilkWindowState = Silk.NET.Windowing.WindowState;

namespace PanguEngine.Graphics.Vulkan;

/// <summary>
/// Creates Vulkan-backed windows after the Vulkan device has been initialized.
/// </summary>
public static unsafe class VulkanWindowFactory
{
    /// <summary>
    /// Creates a non-primary Vulkan-backed window.
    /// </summary>
    /// <param name="options">The engine-level window options.</param>
    /// <returns>The managed window handle.</returns>
    public static Window CreateWindow(WindowOptions options)
    {
        var silkWindow = CreateSilkWindow(options);
        SurfaceKHR surface = default;
        VulkanWindow? window = null;

        try
        {
            silkWindow.Initialize();
            if (silkWindow.VkSurface is null)
                throw new InvalidOperationException("Windowing platform doesn't support Vulkan.");

            surface = silkWindow.VkSurface.Create<AllocationCallbacks>(VulkanContext.VkInstance.ToHandle(), null)
                .ToSurface();

            window = new VulkanWindow(silkWindow, surface, false, options.FramesPerSecond);
            return window;
        }
        catch
        {
            window?.Destroy();
            if (window is null && surface.Handle != 0)
                VulkanContext.KhrSurface.DestroySurface(VulkanContext.VkInstance, surface, null);
            if (window is null)
                silkWindow.Dispose();
            throw;
        }
    }

    private static SilkWindow CreateSilkWindow(WindowOptions options)
    {
        var silkOptions = SilkWindowOptions.DefaultVulkan with
        {
            IsVisible = options.IsVisible,
            Position = options.Position,
            Size = options.Size,
            Title = options.Title,
            WindowBorder = options.WindowBorder switch
            {
                WindowBorder.Fixed => SilkWindowBorder.Fixed,
                WindowBorder.Hidden => SilkWindowBorder.Hidden,
                _ => SilkWindowBorder.Resizable
            },
            WindowState = options.IsFullscreen
                ? SilkWindowState.Fullscreen
                : SilkWindowState.Normal
        };
        return SilkWindowCreator.Create(silkOptions);
    }
}