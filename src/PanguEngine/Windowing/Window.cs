using PanguEngine.Graphics;
using PanguEngine.Input;
using Silk.NET.Maths;

namespace PanguEngine.Windowing;

/// <summary>
/// Abstract base class for platform windows, providing properties and events for window management, input, and rendering.
/// </summary>
public abstract class Window
{
    /// <summary>Whether the window has been destroyed.</summary>
    public abstract bool IsDestroyed { get; }

    /// <summary>The window title.</summary>
    public abstract string Title { get; set; }

    /// <summary>The window position in screen coordinates.</summary>
    public abstract Vector2D<int> Position { get; set; }

    /// <summary>The window size in screen coordinates.</summary>
    public abstract Vector2D<int> Size { get; set; }

    /// <summary>The framebuffer size in pixels.</summary>
    public abstract Vector2D<int> FramebufferSize { get; }

    /// <summary>The full window size including platform decorations.</summary>
    public abstract Vector2D<int> FullSize { get; }

    /// <summary>The platform decoration border sizes around the client area.</summary>
    public abstract Rectangle<int> BorderSize { get; }

    /// <summary>Whether the window has input focus.</summary>
    public abstract bool IsFocused { get; }

    /// <summary>The current window state.</summary>
    public abstract WindowState WindowState { get; set; }

    /// <summary>The monitor that currently contains the window, if available.</summary>
    public abstract DisplayMonitor? Monitor { get; }

    /// <summary>The current monitor video mode for the window.</summary>
    public abstract VideoMode VideoMode { get; }

    /// <summary>Whether the window is visible.</summary>
    public abstract bool IsVisible { get; set; }

    /// <summary>Whether the window is closing.</summary>
    public abstract bool IsClosing { get; set; }

    /// <summary>The window border style.</summary>
    public abstract WindowBorder WindowBorder { get; set; }

    /// <summary>The target frames per second for rendering.</summary>
    public abstract double FramesPerSecond { get; set; }

    /// <summary>Whether vertical synchronization is requested for presentation.</summary>
    public abstract bool VSync { get; set; }

    /// <summary>Whether the window should stay above other windows.</summary>
    public abstract bool TopMost { get; set; }

    /// <summary>Whether this window is the primary window.</summary>
    public abstract bool IsPrimary { get; }

    /// <summary>The presenter associated with this window.</summary>
    public abstract Presenter Presenter { get; }

    /// <summary>The cursor visibility and behavior mode.</summary>
    public abstract CursorState CursorState { get; set; }

    /// <summary>The standard cursor shape.</summary>
    public abstract CursorShape CursorShape { get; set; }

    /// <summary>The current mouse position in window coordinates.</summary>
    public abstract Vector2D<float> MousePosition { get; }

    /// <summary>The key modifiers currently pressed.</summary>
    public abstract KeyModifiers KeyModifiers { get; }

    /// <summary>The text currently stored in the platform clipboard.</summary>
    public abstract string ClipboardText { get; set; }

    /// <summary>The keyboard keys supported by the current input backend.</summary>
    public abstract IReadOnlyList<Key> SupportedKeys { get; }

    /// <summary>The mouse buttons supported by the current input backend.</summary>
    public abstract IReadOnlyList<MouseButton> SupportedMouseButtons { get; }

    /// <summary>Raised when the window or framebuffer is resized.</summary>
    public abstract event Action<Window, ResizeEventArgs>? Resize;

    /// <summary>Raised when the framebuffer size changes.</summary>
    public abstract event Action<Window, FramebufferResizeEventArgs>? FramebufferResize;

    /// <summary>Raised when the window position changes.</summary>
    public abstract event Action<Window, Vector2D<int>>? Move;

    /// <summary>Raised when the window state changes.</summary>
    public abstract event Action<Window, WindowState>? StateChanged;

    /// <summary>Raised when files are dropped onto the window.</summary>
    public abstract event Action<Window, FileDropEventArgs>? FileDrop;

    /// <summary>Raised when the window is closing.</summary>
    public abstract event Action<Window>? Close;

    /// <summary>Raised when the window gains or loses focus.</summary>
    public abstract event Action<Window, bool>? FocusChanged;

    /// <summary>Raised when a key is pressed.</summary>
    public abstract event Action<Window, KeyEventArgs>? KeyDown;

    /// <summary>Raised when a key is released.</summary>
    public abstract event Action<Window, KeyEventArgs>? KeyUp;

    /// <summary>Raised when the mouse cursor moves.</summary>
    public abstract event Action<Window, MouseMoveEventArgs>? MouseMove;

    /// <summary>Raised when a mouse button is pressed.</summary>
    public abstract event Action<Window, MouseClickEventArgs>? MouseDown;

    /// <summary>Raised when a mouse button is released.</summary>
    public abstract event Action<Window, MouseClickEventArgs>? MouseUp;

    /// <summary>Raised when the mouse wheel is scrolled.</summary>
    public abstract event Action<Window, ScrollEventArgs>? Scroll;

    /// <summary>Raised when a character is typed.</summary>
    public abstract event Action<Window, char>? CharInput;

    /// <summary>Raised each frame for rendering with the interpolation factor since the last fixed update.</summary>
    public abstract event Action<Window, double>? Render;

    /// <summary>Shows the window.</summary>
    public abstract void Show();

    /// <summary>Hides the window.</summary>
    public abstract void Hide();

    /// <summary>Centers the window on the primary monitor.</summary>
    public abstract void CenterOnScreen();

    /// <summary>Requests input focus for the window.</summary>
    public abstract void Focus();

    /// <summary>Converts a screen-space point to window client coordinates.</summary>
    /// <param name="point">The point in screen coordinates.</param>
    /// <returns>The point in window client coordinates.</returns>
    public abstract Vector2D<int> PointToClient(Vector2D<int> point);

    /// <summary>Converts a window client point to screen coordinates.</summary>
    /// <param name="point">The point in window client coordinates.</param>
    /// <returns>The point in screen coordinates.</returns>
    public abstract Vector2D<int> PointToScreen(Vector2D<int> point);

    /// <summary>Converts a window client point to framebuffer coordinates.</summary>
    /// <param name="point">The point in window client coordinates.</param>
    /// <returns>The point in framebuffer coordinates.</returns>
    public abstract Vector2D<int> PointToFramebuffer(Vector2D<int> point);

    /// <summary>Gets whether the specified key is currently pressed.</summary>
    /// <param name="key">The key to query.</param>
    /// <returns><see langword="true" /> if the key is pressed; otherwise, <see langword="false" />.</returns>
    public abstract bool IsKeyPressed(Key key);

    /// <summary>Gets whether the specified mouse button is currently pressed.</summary>
    /// <param name="button">The mouse button to query.</param>
    /// <returns><see langword="true" /> if the button is pressed; otherwise, <see langword="false" />.</returns>
    public abstract bool IsMouseButtonPressed(MouseButton button);

    /// <summary>Begins platform text input for the window.</summary>
    public abstract void BeginTextInput();

    /// <summary>Ends platform text input for the window.</summary>
    public abstract void EndTextInput();

    /// <summary>Sets a single platform window icon.</summary>
    /// <param name="icon">The icon to assign.</param>
    public abstract void SetWindowIcon(WindowIcon icon);

    /// <summary>Sets the platform window icons, or restores the default icon when empty.</summary>
    /// <param name="icons">The icons to assign.</param>
    public abstract void SetWindowIcons(WindowIcon[] icons);

    /// <summary>Restores the platform default window icon.</summary>
    public abstract void SetDefaultIcon();

    /// <summary>Requests the window to close.</summary>
    public abstract void CloseWindow();

    /// <summary>Destroys the window.</summary>
    internal abstract void Destroy();

    /// <summary>Processes pending platform events for the window.</summary>
    internal abstract void DoEvents();

    /// <summary>Performs a render event for this window.</summary>
    /// <param name="alpha">The interpolation factor since the last fixed update.</param>
    internal abstract void DoRender(double alpha);
}