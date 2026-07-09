namespace PanguEngine.Windowing;

/// <summary>
/// Describes the platform state of a window.
/// </summary>
public enum WindowState
{
    /// <summary>The window is in its normal restored state.</summary>
    Normal,

    /// <summary>The window is minimized.</summary>
    Minimized,

    /// <summary>The window is maximized.</summary>
    Maximized,

    /// <summary>The window is fullscreen.</summary>
    Fullscreen
}