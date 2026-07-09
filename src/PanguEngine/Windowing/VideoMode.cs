using Silk.NET.Maths;

namespace PanguEngine.Windowing;

/// <summary>
/// Describes a monitor video mode preference or reported display mode.
/// </summary>
/// <param name="Resolution">The display resolution in pixels, or <see langword="null" /> for the platform default.</param>
/// <param name="RefreshRate">The refresh rate in hertz, or <see langword="null" /> for the platform default.</param>
public readonly record struct VideoMode(Vector2D<int>? Resolution = null, int? RefreshRate = null)
{
    /// <summary>A video mode that lets the platform choose the default resolution and refresh rate.</summary>
    public static VideoMode Default { get; } = new();
}