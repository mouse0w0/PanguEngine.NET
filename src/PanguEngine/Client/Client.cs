using PanguEngine.Graphics;
using PanguEngine.Graphics.Vulkan;
using Silk.NET.Core.Native;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;
using Window = PanguEngine.Windowing.Window;

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
    /// The window.
    /// </summary>
    public Window Window { get; private set; } = null!;

    /// <summary>
    /// The graphics presenter.
    /// </summary>
    public Presenter Presenter { get; private set; } = null!;

    /// <summary>
    /// The Vulkan renderer.
    /// </summary>
    private VulkanRenderer Renderer { get; set; } = null!;

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
        var silkWindow = Silk.NET.Windowing.Window.Create(options);
        silkWindow.Initialize();

        if (silkWindow.VkSurface is null)
            throw new InvalidOperationException("Windowing platform doesn't support Vulkan.");

        var glfwExtensions = silkWindow.VkSurface.GetRequiredExtensions(out var count);
        var requiredExtensions = SilkMarshal.PtrToStringArray((nint)glfwExtensions, (int)count);

        VulkanContext.InitializeInstance(requiredExtensions);

        var surface = silkWindow.VkSurface.Create<AllocationCallbacks>(VulkanContext.VkInstance.ToHandle(), null)
            .ToSurface();
        VulkanContext.InitializeDevice(surface);

        VulkanAllocator.Initialize();
        VulkanUploader.Initialize();
        GraphicsContext.Initialize(new VulkanGraphicsDevice());
        Window = new VulkanWindow(silkWindow, surface);
        var presenter = new VulkanPresenter((VulkanWindow)Window);
        Presenter = presenter;
        Renderer = new VulkanRenderer(presenter);
    }

    /// <summary>
    /// Enters the main loop.
    /// </summary>
    private void OnRunning()
    {
        Window.Render += (_, dt) => Renderer.DrawFrame(dt);
        Window.Run();
    }

    /// <summary>
    /// Shuts down the client.
    /// </summary>
    private void OnShutdown()
    {
        Renderer.Destroy();
        Presenter.Destroy();
        Window.Destroy();
        GraphicsContext.Shutdown();
        VulkanUploader.Destroy();
        VulkanDeletionQueue.Drain();
        VulkanAllocator.Destroy();
        VulkanContext.Destroy();

        Engine.Shutdown();
    }
}