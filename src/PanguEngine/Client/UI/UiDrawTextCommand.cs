using PanguEngine.Graphics.Text;

namespace PanguEngine.Client.UI;

/// <summary>
/// Represents an immutable laid-out text draw in screen logical coordinates.
/// </summary>
public sealed class UiDrawTextCommand : UiDrawCommand
{
    internal UiDrawTextCommand(
        Point origin,
        TextLayout layout,
        double fontSize,
        Color color,
        Rect? clip,
        double opacity)
        : base(clip, opacity)
    {
        Origin = origin;
        Layout = layout;
        FontSize = fontSize;
        Color = color;
    }

    /// <summary>
    /// Gets the text origin in screen logical coordinates.
    /// </summary>
    public Point Origin { get; }

    /// <summary>
    /// Gets the immutable CPU text layout.
    /// </summary>
    public TextLayout Layout { get; }

    /// <summary>
    /// Gets the font size in logical pixels.
    /// </summary>
    public double FontSize { get; }

    /// <summary>
    /// Gets the non-premultiplied text color.
    /// </summary>
    public Color Color { get; }
}
