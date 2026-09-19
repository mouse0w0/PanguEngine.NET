namespace PanguEngine;

/// <summary>
/// Describes the high-level modes supported when launching a client window.
/// </summary>
public enum WindowMode
{
    /// <summary>The window uses its normal resizable frame.</summary>
    Windowed,

    /// <summary>The window starts maximized.</summary>
    Maximized,

    /// <summary>The window starts fullscreen.</summary>
    Fullscreen,

    /// <summary>The window starts without a platform border.</summary>
    Borderless
}