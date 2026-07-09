using PanguEngine.Windowing;
using Silk.NET.Vulkan;
using SilkWindow = Silk.NET.Windowing.IWindow;
using SilkWindowBorder = Silk.NET.Windowing.WindowBorder;
using SilkWindowCreator = Silk.NET.Windowing.Window;
using SilkWindowOptions = Silk.NET.Windowing.WindowOptions;

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
            if (options.Icons.Length > 0)
                window.SetWindowIcons(options.Icons);

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

    /// <summary>Creates Silk.NET window options from engine-level window options.</summary>
    /// <param name="options">The engine-level window options.</param>
    /// <returns>The Silk.NET window options.</returns>
    internal static SilkWindowOptions CreateSilkWindowOptions(WindowOptions options)
    {
        return SilkWindowOptions.DefaultVulkan with
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
            WindowState = VulkanWindow.ToSilkWindowStateForOptions(options.WindowState),
            VSync = options.VSync,
            VideoMode = VulkanDisplayManager.ToSilkVideoMode(options.VideoMode),
            TopMost = options.TopMost
        };
    }

    /// <summary>Creates a Silk.NET window from engine-level window options.</summary>
    /// <param name="options">The engine-level window options.</param>
    /// <returns>The created Silk.NET window.</returns>
    private static SilkWindow CreateSilkWindow(WindowOptions options)
    {
        var silkOptions = CreateSilkWindowOptions(options);
        return SilkWindowCreator.Create(silkOptions);
    }
}