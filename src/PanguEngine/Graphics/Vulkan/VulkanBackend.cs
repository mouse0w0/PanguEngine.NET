using System.Runtime.InteropServices;
using PanguEngine.Windowing;
using SDL;

namespace PanguEngine.Graphics.Vulkan;

/// <summary>
/// Vulkan implementation of <see cref="GraphicsBackend"/>.
/// </summary>
internal sealed unsafe class VulkanBackend : GraphicsBackend
{
    private readonly VulkanWindowManager _windowManager;
    private bool _isDestroyed;

    /// <inheritdoc/>
    public override GraphicsBackendType Type => GraphicsBackendType.Vulkan;

    /// <inheritdoc/>
    public override GraphicsDevice Device { get; }

    /// <inheritdoc/>
    public override DisplayManager DisplayManager { get; }

    /// <inheritdoc/>
    public override WindowManager WindowManager => _windowManager;

    /// <inheritdoc/>
    public override Window PrimaryWindow { get; }

    /// <inheritdoc/>
    // ReSharper disable once ConvertToAutoPropertyWithPrivateSetter
    public override bool IsDestroyed => _isDestroyed;

    /// <summary>
    /// Creates and initializes a Vulkan graphics backend.
    /// </summary>
    /// <param name="options">The backend initialization options.</param>
    internal VulkanBackend(GraphicsBackendOptions options)
    {
        VulkanContext.BindRenderThread();

        if (!SDL3.SDL_Vulkan_LoadLibrary((byte*)null))
            throw CreateSdlException("SDL Vulkan loader initialization");

        var windowManager = new VulkanWindowManager();
        _windowManager = windowManager;
        var nativeWindow = windowManager.CreateNativeWindow(options.PrimaryWindow);

        var requiredExtensions = GetVulkanInstanceExtensions();
        VulkanContext.InitializeInstance(requiredExtensions, options.EnableValidation);

        var surface = VulkanWindowManager.CreateVulkanSurface(nativeWindow);
        VulkanContext.InitializeDevice(surface);

        VulkanAllocator.Initialize();
        VulkanUploader.Initialize();

        PrimaryWindow = windowManager.CreatePrimaryWindow(nativeWindow, surface, options.PrimaryWindow);
        Device = new VulkanGraphicsDevice();
        DisplayManager = new VulkanDisplayManager();
    }

    /// <inheritdoc/>
    internal override void Destroy()
    {
        VulkanContext.EnsureRenderThread();
        if (_isDestroyed) return;
        _isDestroyed = true;

        WindowManager.Destroy();
        VulkanUploader.Destroy();
        VulkanDeletionQueue.Drain();
        VulkanAllocator.Destroy();
        VulkanContext.Destroy();
        SDL3.SDL_Vulkan_UnloadLibrary();
    }

    /// <inheritdoc/>
    internal override void Render(double alpha)
    {
        _windowManager.DrainFinalized();
        WindowManager.PreRenderWindows(alpha);
        VulkanUploader.Pump();
        WindowManager.RenderWindows(alpha);
        VulkanDeletionQueue.Collect();
    }

    private static string[] GetVulkanInstanceExtensions()
    {
        VulkanContext.EnsureRenderThread();
        uint count = 0;
        var extensions = SDL3.SDL_Vulkan_GetInstanceExtensions(&count);
        if (extensions is null)
            throw CreateSdlException("SDL Vulkan instance extension query");

        var names = new string[count];
        for (var i = 0; i < count; i++)
            names[i] = Marshal.PtrToStringUTF8((nint)extensions[i])!;
        return names;
    }

    private static InvalidOperationException CreateSdlException(string operation) =>
        new($"{operation} failed: {SDL3.SDL_GetError()}");
}
