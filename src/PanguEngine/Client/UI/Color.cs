namespace PanguEngine.Client.UI;

/// <summary>
/// Represents a non-premultiplied sRGB color with linear alpha coverage.
/// </summary>
public readonly record struct Color
{
    /// <summary>
    /// Initializes a color from red, green, blue, and alpha channels.
    /// </summary>
    /// <param name="r">The red channel.</param>
    /// <param name="g">The green channel.</param>
    /// <param name="b">The blue channel.</param>
    /// <param name="a">The alpha channel.</param>
    public Color(byte r, byte g, byte b, byte a = byte.MaxValue)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    /// <summary>
    /// Gets the red channel.
    /// </summary>
    public byte R { get; }

    /// <summary>
    /// Gets the green channel.
    /// </summary>
    public byte G { get; }

    /// <summary>
    /// Gets the blue channel.
    /// </summary>
    public byte B { get; }

    /// <summary>
    /// Gets the alpha channel.
    /// </summary>
    public byte A { get; }
}
