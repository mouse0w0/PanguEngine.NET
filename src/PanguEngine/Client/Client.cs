using PanguEngine.Graphics;
using PanguEngine.Graphics.Vulkan;
using Silk.NET.Core.Native;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;

namespace PanguEngine.Client;

/// <summary>
/// Application client.
/// </summary>
public unsafe class Client
{
    /// <summary>
    /// The singleton instance of the client.
    /// </summary>
    public static Client Instance { get; private set; } = null!;

    /// <summary>
    /// The Vulkan window.
    /// </summary>
    public VulkanWindow VulkanWindow { get; private set; } = null!;

    /// <summary>
    /// The Vulkan renderer.
    /// </summary>
    public VulkanRenderer Renderer { get; private set; } = null!;

    /// <summary>
    /// Runs the application.
    /// </summary>
    public void Run()
    {
        Instance = this;

        OnInit();
        OnRunning();
        OnShutdown();
    }

    /// <summary>
    /// Initializes the client.
    /// </summary>
    private void OnInit()
    {
        Engine.Initialize();

        var options = WindowOptions.DefaultVulkan with
        {
            Size = new Vector2D<int>(800, 600),
            Title = "PanguEngine"
        };
        var window = Window.Create(options);
        window.Initialize();

        if (window.VkSurface is null)
            throw new InvalidOperationException("Windowing platform doesn't support Vulkan.");

        var glfwExtensions = window.VkSurface.GetRequiredExtensions(out var count);
        var requiredExtensions = SilkMarshal.PtrToStringArray((nint)glfwExtensions, (int)count);

        VulkanContext.InitializeInstance(requiredExtensions);

        var surface = window.VkSurface.Create<AllocationCallbacks>(VulkanContext.VkInstance.ToHandle(), null)
            .ToSurface();
        VulkanContext.InitializeDevice(surface);

        VulkanAllocator.Initialize();
        VulkanUploader.Initialize();
        GraphicsContext.Initialize(new VulkanGraphicsDevice());
        VulkanWindow = new VulkanWindow(window, surface);
        Renderer = new VulkanRenderer(VulkanWindow);
    }

    /// <summary>
    /// Enters the main loop.
    /// </summary>
    private void OnRunning()
    {
        VulkanWindow.Window.Render += dt => Renderer.DrawFrame(dt);
        VulkanWindow.Window.Run();
    }

    /// <summary>
    /// Shuts down the client.
    /// </summary>
    private void OnShutdown()
    {
        Renderer.Destroy();
        VulkanWindow.Destroy();
        GraphicsContext.Shutdown();
        VulkanUploader.Destroy();
        VulkanDeletionQueue.Drain();
        VulkanAllocator.Destroy();
        VulkanContext.Destroy();

        Engine.Shutdown();
    }
}