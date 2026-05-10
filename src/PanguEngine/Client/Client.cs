using PanguEngine.Rendering.Vulkan;
using Silk.NET.Core.Native;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;

namespace PanguEngine.Client;

public unsafe class Client
{
    public static Client Instance { get; private set; } = null!;

    public VulkanContext VulkanContext { get; private set; } = null!;
    public VulkanWindow VulkanWindow { get; private set; } = null!;
    public VulkanRenderer Renderer { get; private set; } = null!;

    public void Run()
    {
        Instance = this;

        OnInit();
        OnRunning();
        OnShutdown();
    }

    private void OnInit()
    {
        Engine.Initialize();

        VulkanContext = new VulkanContext();

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

        VulkanContext.InitInstance(requiredExtensions);

        var surface = window.VkSurface.Create<AllocationCallbacks>(VulkanContext.VkInstance.ToHandle(), null)
            .ToSurface();
        VulkanContext.InitDevice(surface);

        VulkanWindow = new VulkanWindow(VulkanContext, window, surface);
        Renderer = new VulkanRenderer(VulkanContext, VulkanWindow);
    }

    private void OnRunning()
    {
        VulkanWindow.Window.Render += dt => Renderer.DrawFrame(dt);
        VulkanWindow.Window.Run();
    }

    private void OnShutdown()
    {
        Renderer.Destroy();
        VulkanWindow.Destroy();
        VulkanContext.Destroy();

        Engine.Shutdown();
    }
}