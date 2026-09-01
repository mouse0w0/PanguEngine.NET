using System.Runtime.InteropServices;
using System.Text;
using PanguEngine.Windowing;
using SDL;
using Silk.NET.Maths;
using WindowBorder = PanguEngine.Windowing.WindowBorder;
using WindowState = PanguEngine.Windowing.WindowState;

namespace PanguEngine.Graphics.Vulkan;

/// <inheritdoc/>
public sealed unsafe partial class VulkanWindow
{
    private bool _isFocused;
    private bool _vsync;
    private readonly VideoMode _requestedVideoMode;

    /// <inheritdoc/>
    public override string Title
    {
        get
        {
            if (IsDestroyed)
                return "";
            VulkanContext.EnsureRenderThread();
            return Marshal.PtrToStringUTF8((nint)SDL3.Unsafe_SDL_GetWindowTitle(NativeWindow)) ?? "";
        }
        set
        {
            if (IsDestroyed)
                return;
            VulkanContext.EnsureRenderThread();
            var bytes = Encoding.UTF8.GetBytes(value + "\0");
            fixed (byte* title = bytes)
            {
                if (!SDL3.SDL_SetWindowTitle(NativeWindow, title))
                    throw CreateSdlException("SDL window title update");
            }
            SdlPlatform.SyncWindow(NativeWindow);
        }
    }

    /// <inheritdoc/>
    public override Vector2D<int> Position
    {
        get
        {
            if (IsDestroyed)
                return default;
            VulkanContext.EnsureRenderThread();
            var x = 0;
            var y = 0;
            SDL3.SDL_GetWindowPosition(NativeWindow, &x, &y);
            return new Vector2D<int>(x, y);
        }
        set
        {
            if (IsDestroyed)
                return;
            SdlPlatform.SetWindowPosition(NativeWindow, value);
            SdlPlatform.SyncWindow(NativeWindow);
        }
    }

    /// <inheritdoc/>
    public override Vector2D<int> Size
    {
        get
        {
            if (IsDestroyed)
                return default;
            VulkanContext.EnsureRenderThread();
            var width = 0;
            var height = 0;
            SDL3.SDL_GetWindowSize(NativeWindow, &width, &height);
            return new Vector2D<int>(width, height);
        }
        set
        {
            if (IsDestroyed)
                return;
            VulkanContext.EnsureRenderThread();
            if (!SDL3.SDL_SetWindowSize(NativeWindow, value.X, value.Y))
                throw CreateSdlException("SDL window size update");
            SdlPlatform.SyncWindow(NativeWindow);
        }
    }

    /// <inheritdoc/>
    public override Vector2D<int> FramebufferSize
    {
        get
        {
            if (IsDestroyed)
                return default;
            VulkanContext.EnsureRenderThread();
            var width = 0;
            var height = 0;
            SDL3.SDL_GetWindowSizeInPixels(NativeWindow, &width, &height);
            return new Vector2D<int>(width, height);
        }
    }

    /// <inheritdoc/>
    public override Vector2D<int> FullSize
    {
        get
        {
            var size = Size;
            var border = BorderSize;
            return new Vector2D<int>(size.X + border.Size.X, size.Y + border.Size.Y);
        }
    }

    /// <inheritdoc/>
    public override Rectangle<int> BorderSize
    {
        get
        {
            if (IsDestroyed)
                return default;
            VulkanContext.EnsureRenderThread();
            var top = 0;
            var left = 0;
            var bottom = 0;
            var right = 0;
            if (!SDL3.SDL_GetWindowBordersSize(NativeWindow, &top, &left, &bottom, &right))
                return default;
            return new Rectangle<int>(left, top, left + right, top + bottom);
        }
    }

    /// <inheritdoc/>
    // ReSharper disable once ConvertToAutoPropertyWithPrivateSetter
    public override bool IsFocused => _isFocused;

    /// <inheritdoc/>
    public override WindowState WindowState
    {
        get
        {
            if (IsDestroyed)
                return WindowState.Normal;
            VulkanContext.EnsureRenderThread();
            var flags = SDL3.SDL_GetWindowFlags(NativeWindow);
            if (flags.HasFlag(SDL_WindowFlags.SDL_WINDOW_FULLSCREEN))
                return WindowState.Fullscreen;
            if (flags.HasFlag(SDL_WindowFlags.SDL_WINDOW_MINIMIZED))
                return WindowState.Minimized;
            if (flags.HasFlag(SDL_WindowFlags.SDL_WINDOW_MAXIMIZED))
                return WindowState.Maximized;
            return WindowState.Normal;
        }
        set
        {
            if (IsDestroyed)
                return;
            VulkanContext.EnsureRenderThread();
            switch (value)
            {
                case WindowState.Minimized:
                    if (!SDL3.SDL_MinimizeWindow(NativeWindow))
                        throw CreateSdlException("SDL window minimize");
                    break;
                case WindowState.Maximized:
                    if (!SDL3.SDL_MaximizeWindow(NativeWindow))
                        throw CreateSdlException("SDL window maximize");
                    break;
                case WindowState.Fullscreen:
                    if (_requestedVideoMode != VideoMode.Default)
                        SdlPlatform.SetFullscreenVideoMode(NativeWindow, _requestedVideoMode);
                    if (!SDL3.SDL_SetWindowFullscreen(NativeWindow, true))
                        throw CreateSdlException("SDL fullscreen update");
                    break;
                case WindowState.Normal:
                    if (!SDL3.SDL_RestoreWindow(NativeWindow))
                        throw CreateSdlException("SDL window restore");
                    if (!SDL3.SDL_SetWindowFullscreen(NativeWindow, false))
                        throw CreateSdlException("SDL fullscreen update");
                    break;
                default:
                    return;
            }
            SdlPlatform.SyncWindow(NativeWindow);
        }
    }

    /// <inheritdoc/>
    public override VideoMode VideoMode
    {
        get
        {
            if (IsDestroyed)
                return VideoMode.Default;
            VulkanContext.EnsureRenderThread();
            var fullscreenMode = SDL3.SDL_GetWindowFullscreenMode(NativeWindow);
            if (fullscreenMode is not null)
                return VulkanDisplayManager.FromSdlVideoMode(fullscreenMode);
            var displayId = SDL3.SDL_GetDisplayForWindow(NativeWindow);
            var currentMode = SDL3.SDL_GetCurrentDisplayMode(displayId);
            return currentMode is null
                ? VideoMode.Default
                : VulkanDisplayManager.FromSdlVideoMode(currentMode);
        }
    }

    /// <inheritdoc/>
    public override DisplayMonitor? Monitor
    {
        get
        {
            if (IsDestroyed)
                return null;
            VulkanContext.EnsureRenderThread();
            var displayId = SDL3.SDL_GetDisplayForWindow(NativeWindow);
            return VulkanDisplayManager.FromSdlDisplay(displayId);
        }
    }

    /// <inheritdoc/>
    public override bool IsVisible
    {
        get
        {
            if (IsDestroyed)
                return false;
            VulkanContext.EnsureRenderThread();
            return !SDL3.SDL_GetWindowFlags(NativeWindow).HasFlag(SDL_WindowFlags.SDL_WINDOW_HIDDEN);
        }
        set
        {
            if (IsDestroyed)
                return;
            VulkanContext.EnsureRenderThread();
            var result = value ? SDL3.SDL_ShowWindow(NativeWindow) : SDL3.SDL_HideWindow(NativeWindow);
            if (!result)
                throw CreateSdlException(value ? "SDL window show" : "SDL window hide");
            SdlPlatform.SyncWindow(NativeWindow);
        }
    }

    /// <inheritdoc/>
    public override bool IsClosing { get; set; }

    /// <inheritdoc/>
    public override WindowBorder WindowBorder
    {
        get
        {
            if (IsDestroyed)
                return WindowBorder.Fixed;
            VulkanContext.EnsureRenderThread();
            var flags = SDL3.SDL_GetWindowFlags(NativeWindow);
            if (flags.HasFlag(SDL_WindowFlags.SDL_WINDOW_BORDERLESS))
                return WindowBorder.Hidden;
            return flags.HasFlag(SDL_WindowFlags.SDL_WINDOW_RESIZABLE)
                ? WindowBorder.Resizable
                : WindowBorder.Fixed;
        }
        set
        {
            if (IsDestroyed)
                return;
            VulkanContext.EnsureRenderThread();
            var bordered = value != WindowBorder.Hidden;
            if (!SDL3.SDL_SetWindowBordered(NativeWindow, bordered))
                throw CreateSdlException("SDL window border update");
            if (!SDL3.SDL_SetWindowResizable(NativeWindow, value == WindowBorder.Resizable))
                throw CreateSdlException("SDL window resize policy update");
            SdlPlatform.SyncWindow(NativeWindow);
        }
    }

    /// <inheritdoc/>
    public override double FramesPerSecond { get; set; }

    /// <inheritdoc/>
    public override bool VSync
    {
        get => _vsync;
        set
        {
            if (_vsync == value)
                return;

            _vsync = value;
            if (IsDestroyed)
                return;
            VulkanContext.EnsureRenderThread();
            if (_swapchain.Handle != 0)
                RecreateSwapchain();
        }
    }

    /// <inheritdoc/>
    public override bool TopMost
    {
        get
        {
            if (IsDestroyed)
                return false;
            VulkanContext.EnsureRenderThread();
            return SDL3.SDL_GetWindowFlags(NativeWindow).HasFlag(SDL_WindowFlags.SDL_WINDOW_ALWAYS_ON_TOP);
        }
        set
        {
            if (IsDestroyed)
                return;
            VulkanContext.EnsureRenderThread();
            if (!SDL3.SDL_SetWindowAlwaysOnTop(NativeWindow, value))
                throw CreateSdlException("SDL window top-most update");
            SdlPlatform.SyncWindow(NativeWindow);
        }
    }

    /// <inheritdoc/>
    public override void Show() => IsVisible = true;

    /// <inheritdoc/>
    public override void Hide() => IsVisible = false;

    /// <inheritdoc/>
    public override void CenterOnScreen()
    {
        if (IsDestroyed)
            return;
        var bounds = VulkanDisplayManager.MainUsableBounds();
        if (bounds is null)
            return;

        var fullSize = FullSize;
        Position = new Vector2D<int>(
            bounds.Value.Origin.X + (bounds.Value.Size.X - fullSize.X) / 2,
            bounds.Value.Origin.Y + (bounds.Value.Size.Y - fullSize.Y) / 2);
    }

    /// <inheritdoc/>
    public override void Focus()
    {
        if (IsDestroyed)
            return;
        VulkanContext.EnsureRenderThread();
        if (!SDL3.SDL_RaiseWindow(NativeWindow))
            throw CreateSdlException("SDL window focus request");
    }

    /// <inheritdoc/>
    public override Vector2D<int> PointToClient(Vector2D<int> point)
    {
        var position = Position;
        var border = BorderSize;
        return new Vector2D<int>(point.X - position.X - border.Origin.X, point.Y - position.Y - border.Origin.Y);
    }

    /// <inheritdoc/>
    public override Vector2D<int> PointToScreen(Vector2D<int> point)
    {
        var position = Position;
        var border = BorderSize;
        return new Vector2D<int>(point.X + position.X + border.Origin.X, point.Y + position.Y + border.Origin.Y);
    }

    /// <inheritdoc/>
    public override Vector2D<int> PointToFramebuffer(Vector2D<int> point)
    {
        var size = Size;
        var framebufferSize = FramebufferSize;
        return new Vector2D<int>(
            size.X == 0 ? point.X : point.X * framebufferSize.X / size.X,
            size.Y == 0 ? point.Y : point.Y * framebufferSize.Y / size.Y);
    }

    /// <inheritdoc/>
    public override void SetWindowIcon(WindowIcon icon) => SetWindowIcons([icon]);

    /// <inheritdoc/>
    public override void SetWindowIcons(WindowIcon[] icons)
    {
        if (IsDestroyed)
            return;
        VulkanContext.EnsureRenderThread();
        if (icons.Length == 0)
        {
            SetDefaultIcon();
            return;
        }

        SDL_Surface* mainSurface = null;
        try
        {
            fixed (byte* mainPixels = icons[0].RgbaPixels)
            {
                mainSurface = CreateIconSurface(icons[0], mainPixels);
                for (var i = 1; i < icons.Length; i++)
                {
                    fixed (byte* alternatePixels = icons[i].RgbaPixels)
                    {
                        var alternateSurface = CreateIconSurface(icons[i], alternatePixels);
                        try
                        {
                            if (!SDL3.SDL_AddSurfaceAlternateImage(mainSurface, alternateSurface))
                                throw CreateSdlException("SDL window alternate icon update");
                        }
                        finally
                        {
                            SDL3.SDL_DestroySurface(alternateSurface);
                        }
                    }
                }

                if (!SDL3.SDL_SetWindowIcon(NativeWindow, mainSurface))
                    throw CreateSdlException("SDL window icon update");
            }
        }
        finally
        {
            if (mainSurface is not null)
                SDL3.SDL_DestroySurface(mainSurface);
        }
    }

    /// <summary>Leaves the platform default icon unchanged because SDL3 has no portable reset operation.</summary>
    public override void SetDefaultIcon()
    {
    }

    /// <inheritdoc/>
    public override void CloseWindow()
    {
        if (IsDestroyed)
            return;
        RequestClose();
    }

    private static SDL_Surface* CreateIconSurface(WindowIcon icon, byte* pixels)
    {
        var expectedLength = checked(icon.Width * icon.Height * 4);
        if (icon.RgbaPixels.Length != expectedLength)
            throw new ArgumentException("Window icon pixel data must contain width * height * 4 RGBA bytes.",
                nameof(icon));

        var surface = SDL3.SDL_CreateSurfaceFrom(
            icon.Width,
            icon.Height,
            SDL3.SDL_PIXELFORMAT_RGBA32,
            (nint)pixels,
            checked(icon.Width * 4));
        if (surface is null)
            throw CreateSdlException("SDL window icon surface creation");
        return surface;
    }

}
