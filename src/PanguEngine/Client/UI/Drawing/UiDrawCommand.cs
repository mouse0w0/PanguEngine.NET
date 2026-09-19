using PanguEngine.Graphics.Text;

namespace PanguEngine.Client.UI.Drawing;

/// <summary>
/// Represents an immutable backend-independent UI drawing command.
/// </summary>
public abstract class UiDrawCommand
{
    private protected UiDrawCommand()
    {
    }
}

/// <summary>
/// Pushes a local translation and positive uniform scale onto the drawing state stack.
/// </summary>
/// <remarks>
/// A local point maps to <c>Translation + Scale * point</c> in the enclosing coordinates.
/// With enclosing translation T and scale S, the combined transform has translation
/// <c>T + S * Translation</c> and scale <c>S * Scale</c>.
/// </remarks>
public sealed class UiPushTransformCommand : UiDrawCommand
{
    internal UiPushTransformCommand(Point translation, double scale)
    {
        Translation = translation;
        Scale = scale;
    }

    /// <summary>
    /// Gets the translation in the enclosing coordinate system, applied after the local scale.
    /// </summary>
    public Point Translation { get; }

    /// <summary>
    /// Gets the positive uniform scale relative to the enclosing coordinate system.
    /// </summary>
    public double Scale { get; }
}

/// <summary>
/// Pushes a rectangular clip established using the transform at this command's position.
/// </summary>
public sealed class UiPushClipCommand : UiDrawCommand
{
    internal UiPushClipCommand(Rect clip) => Clip = clip;

    /// <summary>
    /// Gets the local clip rectangle. A zero-area rectangle suppresses drawing within the scope.
    /// </summary>
    public Rect Clip { get; }
}

/// <summary>
/// Pushes an opacity factor multiplied into each draw within the scope.
/// </summary>
public sealed class UiPushOpacityCommand : UiDrawCommand
{
    internal UiPushOpacityCommand(double opacity) => Opacity = opacity;

    /// <summary>
    /// Gets the opacity factor from zero through one.
    /// </summary>
    public double Opacity { get; }
}

/// <summary>
/// Restores the complete drawing state saved by the most recent unmatched push command.
/// </summary>
public sealed class UiPopCommand : UiDrawCommand
{
    internal static readonly UiPopCommand Instance = new();

    private UiPopCommand()
    {
    }
}

/// <summary>
/// Represents a solid-color rectangle in local drawing coordinates.
/// </summary>
public sealed class UiFillRectangleCommand : UiDrawCommand
{
    internal UiFillRectangleCommand(
        Rect bounds,
        Color color)
    {
        Bounds = bounds;
        Color = color;
    }

    /// <summary>
    /// Gets the unclipped rectangle bounds in local drawing coordinates.
    /// </summary>
    public Rect Bounds { get; }

    /// <summary>
    /// Gets the non-premultiplied fill color.
    /// </summary>
    public Color Color { get; }
}

/// <summary>
/// Represents an image draw in local drawing coordinates.
/// </summary>
public sealed class UiDrawImageCommand : UiDrawCommand
{
    internal UiDrawImageCommand(
        Rect bounds,
        UiImage image,
        Rect sourceRect,
        ImageSamplingMode samplingMode)
    {
        Bounds = bounds;
        Image = image;
        SourceRect = sourceRect;
        SamplingMode = samplingMode;
    }

    /// <summary>
    /// Gets the unclipped destination bounds in local drawing coordinates.
    /// </summary>
    public Rect Bounds { get; }

    /// <summary>
    /// Gets the image source retained by this immutable command.
    /// </summary>
    public UiImage Image { get; }

    /// <summary>
    /// Gets the source region in image pixel coordinates.
    /// </summary>
    public Rect SourceRect { get; }

    /// <summary>
    /// Gets the image sampling mode.
    /// </summary>
    public ImageSamplingMode SamplingMode { get; }
}

/// <summary>
/// Represents an immutable laid-out text draw in local drawing coordinates.
/// </summary>
public sealed class UiDrawTextCommand : UiDrawCommand
{
    internal UiDrawTextCommand(
        Point origin,
        TextLayout layout,
        double fontSize,
        Color color)
    {
        Origin = origin;
        Layout = layout;
        FontSize = fontSize;
        Color = color;
    }

    /// <summary>
    /// Gets the text origin in local drawing coordinates.
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
