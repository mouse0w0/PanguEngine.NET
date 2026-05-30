using Silk.NET.Maths;

namespace PanguEngine.Windowing;

/// <summary>
/// Describes engine-level options used to create a window.
/// </summary>
public record struct WindowOptions
{
    /// <summary>The window title.</summary>
    public string Title { get; set; } = "";

    /// <summary>The window client size in screen coordinates.</summary>
    public Vector2D<int> Size { get; set; } = new(800, 600);

    /// <summary>The window position in screen coordinates.</summary>
    public Vector2D<int> Position { get; set; } = new(50, 50);

    /// <summary>The window border style.</summary>
    public WindowBorder WindowBorder { get; set; } = WindowBorder.Resizable;

    /// <summary>Whether the window is visible after creation.</summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>The target render events per second. Values less than or equal to zero mean unlimited.</summary>
    public double FramesPerSecond { get; set; } = 60;

    /// <summary>Whether the window starts in exclusive fullscreen mode.</summary>
    public bool IsFullscreen { get; set; }

    /// <summary>Initializes a new instance with default values.</summary>
    public WindowOptions()
    {
    }
}