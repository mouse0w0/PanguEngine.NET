using PanguEngine.Graphics;
using Silk.NET.Maths;

namespace PanguEngine.Windowing;

/// <summary>
/// Abstract base class for platform windows, providing properties and events for window management, input, and rendering.
/// </summary>
public abstract class Window : GraphicsResource
{
    /// <summary>The window title.</summary>
    public abstract string Title { get; set; }

    /// <summary>The window position in screen coordinates.</summary>
    public abstract Vector2D<int> Position { get; set; }

    /// <summary>The window size in screen coordinates.</summary>
    public abstract Vector2D<int> Size { get; set; }

    /// <summary>The framebuffer size in pixels.</summary>
    public abstract Vector2D<int> FramebufferSize { get; }

    /// <summary>Whether the window has input focus.</summary>
    public abstract bool IsFocused { get; }

    /// <summary>Whether the window is minimized.</summary>
    public abstract bool IsMinimized { get; }

    /// <summary>Whether the window is maximized.</summary>
    public abstract bool IsMaximized { get; }

    /// <summary>Whether the window is visible.</summary>
    public abstract bool IsVisible { get; set; }

    /// <summary>Whether the window is closing.</summary>
    public abstract bool IsClosing { get; }

    /// <summary>The window border style.</summary>
    public abstract WindowBorder WindowBorder { get; set; }

    /// <summary>The target frames per second for rendering.</summary>
    public abstract double FramesPerSecond { get; set; }

    /// <summary>Whether this window is the primary window.</summary>
    public abstract bool IsPrimary { get; }

    /// <summary>The presenter associated with this window.</summary>
    public abstract Presenter Presenter { get; }

    /// <summary>The cursor visibility and behavior mode.</summary>
    public abstract CursorState CursorState { get; set; }

    /// <summary>The standard cursor shape.</summary>
    public abstract CursorShape CursorShape { get; set; }

    /// <summary>Raised when the window or framebuffer is resized.</summary>
    public abstract event Action<Window, ResizeEventArgs> Resize;

    /// <summary>Raised when the window is closing.</summary>
    public abstract event Action<Window> Close;

    /// <summary>Raised when the window gains or loses focus.</summary>
    public abstract event Action<Window, bool> FocusChanged;

    /// <summary>Raised when a key is pressed.</summary>
    public abstract event Action<Window, KeyEventArgs> KeyDown;

    /// <summary>Raised when a key is released.</summary>
    public abstract event Action<Window, KeyEventArgs> KeyUp;

    /// <summary>Raised when the mouse cursor moves.</summary>
    public abstract event Action<Window, MouseMoveEventArgs> MouseMove;

    /// <summary>Raised when a mouse button is pressed.</summary>
    public abstract event Action<Window, MouseClickEventArgs> MouseDown;

    /// <summary>Raised when a mouse button is released.</summary>
    public abstract event Action<Window, MouseClickEventArgs> MouseUp;

    /// <summary>Raised when the mouse wheel is scrolled.</summary>
    public abstract event Action<Window, ScrollEventArgs> Scroll;

    /// <summary>Raised when a character is typed.</summary>
    public abstract event Action<Window, char> CharInput;

    /// <summary>Raised each frame for rendering.</summary>
    public abstract event Action<Window, double> Render;

    /// <summary>Shows the window.</summary>
    public abstract void Show();

    /// <summary>Hides the window.</summary>
    public abstract void Hide();

    /// <summary>Centers the window on the primary monitor.</summary>
    public abstract void CenterOnScreen();

    /// <summary>Requests the window to close.</summary>
    public abstract void CloseWindow();

    /// <summary>Processes pending platform events for the window.</summary>
    internal abstract void DoEvents();

    /// <summary>Performs a render event for this window.</summary>
    /// <param name="deltaTime">The elapsed time since the previous render event.</param>
    internal abstract void DoRender(double deltaTime);
}