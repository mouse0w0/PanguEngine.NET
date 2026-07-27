using PanguEngine.Windowing;
using Silk.NET.Core;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using SilkWindowBorder = Silk.NET.Windowing.WindowBorder;
using SilkWindowState = Silk.NET.Windowing.WindowState;
using WindowBorder = PanguEngine.Windowing.WindowBorder;
using WindowState = PanguEngine.Windowing.WindowState;
using EngineVideoMode = PanguEngine.Windowing.VideoMode;

namespace PanguEngine.Graphics.Vulkan;

/// <inheritdoc/>
public sealed partial class VulkanWindow
{
    private bool _isFocused = true;

    /// <inheritdoc/>
    public override string Title
    {
        get => _silkWindow.Title;
        set => _silkWindow.Title = value;
    }

    /// <inheritdoc/>
    public override Vector2D<int> Position
    {
        get => _silkWindow.Position;
        set => _silkWindow.Position = value;
    }

    /// <inheritdoc/>
    public override Vector2D<int> Size
    {
        get => _silkWindow.Size;
        set => _silkWindow.Size = value;
    }

    /// <inheritdoc/>
    public override Vector2D<int> FramebufferSize => _silkWindow.FramebufferSize;

    /// <inheritdoc/>
    public override Vector2D<int> FullSize => _silkWindow.GetFullSize();

    /// <inheritdoc/>
    public override Rectangle<int> BorderSize => _silkWindow.BorderSize;

    /// <inheritdoc/>
    // ReSharper disable once ConvertToAutoPropertyWithPrivateSetter
    public override bool IsFocused => _isFocused;

    /// <inheritdoc/>
    public override WindowState WindowState
    {
        get => FromSilkWindowState(_silkWindow.WindowState);
        set => _silkWindow.WindowState = ToSilkWindowState(value);
    }

    /// <inheritdoc/>
    public override EngineVideoMode VideoMode => VulkanDisplayManager.FromSilkVideoMode(_silkWindow.VideoMode);

    /// <inheritdoc/>
    public override DisplayMonitor? Monitor => VulkanDisplayManager.FromSilkMonitor(_silkWindow.Monitor);

    /// <inheritdoc/>
    public override bool IsVisible
    {
        get => _silkWindow.IsVisible;
        set => _silkWindow.IsVisible = value;
    }

    /// <inheritdoc/>
    public override bool IsClosing
    {
        get => _silkWindow.IsClosing;
        set => _silkWindow.IsClosing = value;
    }

    /// <inheritdoc/>
    public override WindowBorder WindowBorder
    {
        get => _silkWindow.WindowBorder switch
        {
            SilkWindowBorder.Fixed => WindowBorder.Fixed,
            SilkWindowBorder.Hidden => WindowBorder.Hidden,
            _ => WindowBorder.Resizable
        };
        set => _silkWindow.WindowBorder = value switch
        {
            WindowBorder.Fixed => SilkWindowBorder.Fixed,
            WindowBorder.Hidden => SilkWindowBorder.Hidden,
            _ => SilkWindowBorder.Resizable
        };
    }

    /// <inheritdoc/>
    public override double FramesPerSecond { get; set; }

    /// <inheritdoc/>
    public override bool VSync
    {
        get => _silkWindow.VSync;
        set
        {
            VulkanContext.EnsureRenderThread();
            if (_silkWindow.VSync == value) return;

            _silkWindow.VSync = value;
            if (!IsDestroyed && _swapchain.Handle != 0)
                RecreateSwapchain();
        }
    }

    /// <inheritdoc/>
    public override bool TopMost
    {
        get => _silkWindow.TopMost;
        set => _silkWindow.TopMost = value;
    }

    /// <inheritdoc/>
    public override void Show() => _silkWindow.IsVisible = true;

    /// <inheritdoc/>
    public override void Hide() => _silkWindow.IsVisible = false;

    /// <inheritdoc/>
    public override void CenterOnScreen() => _silkWindow.Center();

    /// <inheritdoc/>
    public override void Focus() => _silkWindow.Focus();

    /// <inheritdoc/>
    public override Vector2D<int> PointToClient(Vector2D<int> point) => _silkWindow.PointToClient(point);

    /// <inheritdoc/>
    public override Vector2D<int> PointToScreen(Vector2D<int> point) => _silkWindow.PointToScreen(point);

    /// <inheritdoc/>
    public override Vector2D<int> PointToFramebuffer(Vector2D<int> point) => _silkWindow.PointToFramebuffer(point);

    /// <inheritdoc/>
    public override void SetWindowIcon(WindowIcon icon)
    {
        RawImage[] rawIcons = [ToRawImage(icon)];
        _silkWindow.SetWindowIcon(rawIcons);
    }

    /// <inheritdoc/>
    public override void SetWindowIcons(WindowIcon[] icons)
    {
        if (icons.Length == 0)
        {
            SetDefaultIcon();
            return;
        }

        var rawIcons = new RawImage[icons.Length];
        for (var i = 0; i < icons.Length; i++)
            rawIcons[i] = ToRawImage(icons[i]);

        _silkWindow.SetWindowIcon(rawIcons);
    }

    /// <inheritdoc/>
    public override void SetDefaultIcon() => _silkWindow.SetDefaultIcon();

    /// <inheritdoc/>
    public override void CloseWindow() => _silkWindow.Close();

    /// <summary>Converts an engine window state to a Silk.NET window state.</summary>
    /// <param name="state">The engine window state.</param>
    /// <returns>The Silk.NET window state.</returns>
    private static SilkWindowState ToSilkWindowState(WindowState state)
    {
        return state switch
        {
            WindowState.Minimized => SilkWindowState.Minimized,
            WindowState.Maximized => SilkWindowState.Maximized,
            WindowState.Fullscreen => SilkWindowState.Fullscreen,
            _ => SilkWindowState.Normal
        };
    }

    /// <summary>Converts a Silk.NET window state to an engine window state.</summary>
    /// <param name="state">The Silk.NET window state.</param>
    /// <returns>The engine window state.</returns>
    private static WindowState FromSilkWindowState(SilkWindowState state)
    {
        return state switch
        {
            SilkWindowState.Minimized => WindowState.Minimized,
            SilkWindowState.Maximized => WindowState.Maximized,
            SilkWindowState.Fullscreen => WindowState.Fullscreen,
            _ => WindowState.Normal
        };
    }

    /// <summary>Converts an engine window state for use in window creation options.</summary>
    /// <param name="state">The engine window state.</param>
    /// <returns>The Silk.NET window state.</returns>
    internal static SilkWindowState ToSilkWindowStateForOptions(WindowState state) => ToSilkWindowState(state);

    /// <summary>Creates a Silk.NET raw image from an engine window icon.</summary>
    /// <param name="icon">The engine window icon.</param>
    /// <returns>The Silk.NET raw image.</returns>
    private static RawImage ToRawImage(WindowIcon icon)
    {
        var expectedLength = checked(icon.Width * icon.Height * 4);
        if (icon.RgbaPixels.Length != expectedLength)
            throw new ArgumentException("Window icon pixel data must contain width * height * 4 RGBA bytes.",
                nameof(icon));

        return new RawImage(icon.Width, icon.Height, icon.RgbaPixels);
    }
}