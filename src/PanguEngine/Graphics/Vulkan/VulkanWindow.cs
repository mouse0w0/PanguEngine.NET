using Silk.NET.Vulkan;
using SilkWindow = Silk.NET.Windowing.IWindow;
using Window = PanguEngine.Windowing.Window;

namespace PanguEngine.Graphics.Vulkan;

/// <summary>
/// Manages a Vulkan swapchain surface and its associated rendering resources bound to a window.
/// </summary>
public sealed unsafe partial class VulkanWindow : Window
{
    private bool _isDestroyed;
    private readonly VulkanPresenter _presenter;
    private readonly SilkWindow _silkWindow;

    /// <summary>The Vulkan surface created from the window.</summary>
    public SurfaceKHR Surface { get; }

    /// <inheritdoc/>
    // ReSharper disable once ConvertToAutoPropertyWithPrivateSetter
    public override bool IsDestroyed => _isDestroyed;

    /// <inheritdoc/>
    public override bool IsPrimary { get; }

    /// <inheritdoc/>
    public override Presenter Presenter => _presenter;

    /// <summary>Creates a <see cref="VulkanWindow"/> from an existing window and surface.</summary>
    internal VulkanWindow(SilkWindow window, SurfaceKHR surface, bool isPrimary, double framesPerSecond = 60)
    {
        VulkanContext.EnsureRenderThread();

        _silkWindow = window;
        Surface = surface;
        IsPrimary = isPrimary;
        FramesPerSecond = framesPerSecond;

        try
        {
            SubscribeEvents();
            InitializeSwapchain();
            InitializeInput();
            _presenter = new VulkanPresenter(this);
        }
        catch
        {
            Destroy();
            throw;
        }
    }

    /// <inheritdoc/>
    internal override void DoEvents() => _silkWindow.DoEvents();

    /// <inheritdoc/>
    internal override void DoPreRender(double alpha) => PreRender?.Invoke(this, alpha);

    /// <inheritdoc/>
    internal override void DoRender(double alpha) => Render?.Invoke(this, alpha);

    /// <inheritdoc/>
    internal override void Destroy()
    {
        VulkanContext.EnsureRenderThread();
        if (_isDestroyed) return;
        _isDestroyed = true;

        if (_presenter is { IsDestroyed: false })
            _presenter.Destroy();

        DestroyRenderFinishedSemaphores();
        DestroyImageViews();
        DestroySwapchain();

        _inputContext?.Dispose();
        VulkanContext.KhrSurface.DestroySurface(VulkanContext.VkInstance, Surface, null);
        _silkWindow.Dispose();
    }
}
