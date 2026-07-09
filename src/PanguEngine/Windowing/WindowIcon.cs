namespace PanguEngine.Windowing;

/// <summary>
/// Describes an RGBA window icon image.
/// </summary>
/// <param name="Width">The icon width in pixels.</param>
/// <param name="Height">The icon height in pixels.</param>
/// <param name="RgbaPixels">The icon pixel data in RGBA byte order.</param>
public readonly record struct WindowIcon(int Width, int Height, byte[] RgbaPixels);