using Silk.NET.Maths;

namespace PanguEngine.Windowing;

/// <summary>
/// Describes a display monitor and its available video modes.
/// </summary>
/// <param name="Name">The platform display name.</param>
/// <param name="Index">The platform display index.</param>
/// <param name="Bounds">The monitor bounds in screen coordinates.</param>
/// <param name="VideoMode">The monitor's current video mode.</param>
/// <param name="ContentScale">The operating system content scale for text and UI elements.</param>
/// <param name="VideoModes">The video modes reported by the monitor.</param>
public readonly record struct DisplayMonitor(
    string Name,
    int Index,
    Rectangle<int> Bounds,
    VideoMode VideoMode,
    double ContentScale,
    VideoMode[] VideoModes);
