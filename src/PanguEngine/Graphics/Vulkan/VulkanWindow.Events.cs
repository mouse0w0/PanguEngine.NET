using PanguEngine.Windowing;
using Silk.NET.Maths;
using SilkWindowState = Silk.NET.Windowing.WindowState;
using Window = PanguEngine.Windowing.Window;
using WindowState = PanguEngine.Windowing.WindowState;

namespace PanguEngine.Graphics.Vulkan;

/// <inheritdoc/>
public sealed partial class VulkanWindow
{
    /// <inheritdoc/>
    public override event Action<Window, ResizeEventArgs>? Resize;

    /// <inheritdoc/>
    public override event Action<Window, FramebufferResizeEventArgs>? FramebufferResize;

    /// <inheritdoc/>
    public override event Action<Window, Vector2D<int>>? Move;

    /// <inheritdoc/>
    public override event Action<Window, WindowState>? StateChanged;

    /// <inheritdoc/>
    public override event Action<Window, FileDropEventArgs>? FileDrop;

    /// <inheritdoc/>
    public override event Action<Window>? Close;

    /// <inheritdoc/>
    public override event Action<Window, bool>? FocusChanged;

    /// <inheritdoc/>
    public override event Action<Window, KeyEventArgs>? KeyDown;

    /// <inheritdoc/>
    public override event Action<Window, KeyEventArgs>? KeyUp;

    /// <inheritdoc/>
    public override event Action<Window, MouseMoveEventArgs>? MouseMove;

    /// <inheritdoc/>
    public override event Action<Window, MouseClickEventArgs>? MouseDown;

    /// <inheritdoc/>
    public override event Action<Window, MouseClickEventArgs>? MouseUp;

    /// <inheritdoc/>
    public override event Action<Window, ScrollEventArgs>? Scroll;

    /// <inheritdoc/>
    public override event Action<Window, char>? CharInput;

    /// <inheritdoc/>
    public override event Action<Window, double>? PreRender;

    /// <inheritdoc/>
    public override event Action<Window, double>? Render;

    /// <summary>Subscribes to platform window lifecycle events.</summary>
    private void SubscribeEvents()
    {
        _silkWindow.Resize += OnResize;
        _silkWindow.FramebufferResize += OnFramebufferResize;
        _silkWindow.Move += OnMove;
        _silkWindow.StateChanged += OnStateChanged;
        _silkWindow.FileDrop += OnFileDrop;
        _silkWindow.Closing += OnClosing;
        _silkWindow.FocusChanged += OnFocusChanged;
    }

    /// <summary>Handles a platform window resize event.</summary>
    /// <param name="newSize">The new window size.</param>
    private void OnResize(Vector2D<int> newSize)
    {
        _framebufferResized = true;
        var framebufferSize = _silkWindow.FramebufferSize;
        Resize?.Invoke(this, new ResizeEventArgs(newSize.X, newSize.Y, framebufferSize.X, framebufferSize.Y));
    }

    /// <summary>Handles a platform framebuffer resize event.</summary>
    /// <param name="newSize">The new framebuffer size.</param>
    private void OnFramebufferResize(Vector2D<int> newSize)
    {
        _framebufferResized = true;
        FramebufferResize?.Invoke(this, new FramebufferResizeEventArgs(newSize.X, newSize.Y));
    }

    /// <summary>Handles a platform window move event.</summary>
    /// <param name="newPosition">The new window position.</param>
    private void OnMove(Vector2D<int> newPosition)
    {
        Move?.Invoke(this, newPosition);
    }

    /// <summary>Handles a platform window state change event.</summary>
    /// <param name="state">The new platform window state.</param>
    private void OnStateChanged(SilkWindowState state)
    {
        StateChanged?.Invoke(this, FromSilkWindowState(state));
    }

    /// <summary>Handles a platform file drop event.</summary>
    /// <param name="paths">The dropped file paths.</param>
    private void OnFileDrop(string[] paths)
    {
        FileDrop?.Invoke(this, new FileDropEventArgs(paths));
    }

    /// <summary>Handles a platform window closing event.</summary>
    private void OnClosing() => Close?.Invoke(this);

    /// <summary>Handles a platform focus change event.</summary>
    /// <param name="focused">Whether the window has focus.</param>
    private void OnFocusChanged(bool focused)
    {
        _isFocused = focused;
        FocusChanged?.Invoke(this, focused);
    }
}
