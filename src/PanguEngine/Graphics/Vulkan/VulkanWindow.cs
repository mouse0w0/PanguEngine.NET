using Silk.NET.Vulkan;
using PanguEngine.Windowing;
using SDL;
using Window = PanguEngine.Windowing.Window;

namespace PanguEngine.Graphics.Vulkan;

/// <summary>
/// Manages a Vulkan swapchain surface and its associated rendering resources bound to a window.
/// </summary>
public sealed unsafe partial class VulkanWindow : Window
{
    private readonly SdlPlatform _platform;
    private readonly SdlWindowEventState _eventState = new();
    private bool _textInputActive;

    /// <summary>The Vulkan surface created from the window.</summary>
    public SurfaceKHR Surface { get; private set; }

    internal SDL_Window* NativeWindow { get; }

    internal SDL_WindowID WindowId { get; }

    private bool _isDestroyed;

    /// <inheritdoc/>
    // ReSharper disable once ConvertToAutoPropertyWithPrivateSetter
    public override bool IsDestroyed => _isDestroyed;

    /// <inheritdoc/>
    public override bool IsPrimary { get; }

    /// <inheritdoc/>
    public override Presenter Presenter { get; }

    /// <summary>Creates a <see cref="VulkanWindow"/> from an existing window and surface.</summary>
    internal VulkanWindow(
        SdlPlatform platform,
        SDL_Window* window,
        SurfaceKHR surface,
        bool isPrimary,
        WindowOptions options)
    {
        _platform = platform;
        VulkanContext.EnsureRenderThread();
        NativeWindow = window;
        WindowId = SDL3.SDL_GetWindowID(window);
        Surface = surface;
        IsPrimary = isPrimary;
        FramesPerSecond = options.FramesPerSecond;
        _vsync = options.VSync;
        _requestedVideoMode = options.VideoMode;

        try
        {
            _isFocused = (SDL3.SDL_GetWindowFlags(window) & SDL_WindowFlags.SDL_WINDOW_INPUT_FOCUS) != 0;
            InitializeSwapchain();
            InitializeInput();
            Presenter = new VulkanPresenter(this);
            _platform.RegisterWindow(this);
        }
        catch
        {
            Destroy();
            throw;
        }
    }

    /// <inheritdoc/>
    internal override void DoEvents()
    {
    }

    /// <inheritdoc/>
    internal override void DoPreRender(double alpha)
    {
        if (!IsDestroyed)
            PreRender?.Invoke(this, alpha);
    }

    /// <inheritdoc/>
    internal override void DoRender(double alpha)
    {
        if (!IsDestroyed)
            Render?.Invoke(this, alpha);
    }

    /// <inheritdoc/>
    internal override void Destroy()
    {
        VulkanContext.EnsureRenderThread();
        if (_isDestroyed) return;
        _isDestroyed = true;

        if (Presenter is { IsDestroyed: false })
            Presenter.Destroy();

        DestroyRenderFinishedSemaphores();
        DestroyImageViews();
        DestroySwapchain();

        if (_textInputActive)
            SDL3.SDL_StopTextInput(NativeWindow);
        if (Surface.Handle != 0)
            VulkanContext.KhrSurface.DestroySurface(VulkanContext.VkInstance, Surface, null);
        Surface = default;
        _platform.UnregisterAndDestroyWindow(this);
    }
}
