using PanguEngine.Client.UI.Drawing.Geometry;
using PanguEngine.Graphics.Text;

namespace PanguEngine.Client.UI.Drawing;

/// <summary>
/// Represents an immutable backend-independent UI drawing command.
/// </summary>
public abstract class DrawCommand
{
    private protected DrawCommand()
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
public sealed class PushTransformCommand : DrawCommand
{
    internal PushTransformCommand(Point translation, double scale)
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
/// Saves the current drawing state and replaces its translation and uniform scale.
/// </summary>
/// <remarks>
/// A local point maps directly to framebuffer pixels as <c>Translation + Scale * point</c>,
/// independent of enclosing transforms. Established clips and opacity remain unchanged.
/// </remarks>
public sealed class SetTransformCommand : DrawCommand
{
    internal SetTransformCommand(Point translation, double scale)
    {
        Translation = translation;
        Scale = scale;
    }

    /// <summary>
    /// Gets the absolute translation in framebuffer pixels.
    /// </summary>
    public Point Translation { get; }

    /// <summary>
    /// Gets the positive uniform scale from local units to framebuffer pixels.
    /// </summary>
    public double Scale { get; }
}

/// <summary>
/// Saves the current drawing state and replaces only its translation.
/// </summary>
/// <remarks>The current scale, established clips, and opacity remain unchanged.</remarks>
public sealed class SetTranslateCommand : DrawCommand
{
    internal SetTranslateCommand(Point translation) => Translation = translation;

    /// <summary>
    /// Gets the absolute translation in framebuffer pixels.
    /// </summary>
    public Point Translation { get; }
}

/// <summary>
/// Saves the current drawing state and replaces only its uniform scale.
/// </summary>
/// <remarks>The current framebuffer origin, established clips, and opacity remain unchanged.</remarks>
public sealed class SetScaleCommand : DrawCommand
{
    internal SetScaleCommand(double scale) => Scale = scale;

    /// <summary>
    /// Gets the positive uniform scale from local units to framebuffer pixels.
    /// </summary>
    public double Scale { get; }
}

/// <summary>
/// Pushes a rectangular clip established using the transform at this command's position.
/// </summary>
public sealed class PushClipCommand : DrawCommand
{
    internal PushClipCommand(Rect clip) => Clip = clip;

    /// <summary>
    /// Gets the local clip rectangle. A zero-area rectangle suppresses drawing within the scope.
    /// </summary>
    public Rect Clip { get; }
}

/// <summary>Constrains decoration or descendant drawing to an immutable rounded region.</summary>
internal sealed class PushRoundedClipCommand(RoundedClipGeometry geometry, bool snapImageBounds = false) : DrawCommand
{
    internal RoundedClipGeometry Geometry { get; } = geometry;
    internal bool SnapImageBounds { get; } = snapImageBounds;
}

/// <summary>Represents a solid fill constrained to a rounded region.</summary>
internal sealed class FillRoundedRectangleCommand(RoundedClipGeometry geometry, Color color) : DrawCommand
{
    internal RoundedClipGeometry Geometry { get; } = geometry;
    internal Color Color { get; } = color;
}

/// <summary>Represents a solid fill and stroke of a rectangle with optional elliptical corners.</summary>
internal sealed class DrawRoundedShapeCommand(
    RoundedRectangle centerline, double thickness, Color fill, Color stroke, StrokeLineJoin join) : DrawCommand
{
    internal RoundedRectangle Centerline { get; } = centerline;
    internal double Thickness { get; } = thickness;
    internal Color Fill { get; } = fill;
    internal Color Stroke { get; } = stroke;
    internal StrokeLineJoin Join { get; } = join;
}

/// <summary>Represents the complementary solid background and border of a rounded region.</summary>
internal sealed class DrawRoundedDecorationCommand(
    RoundedRectangle outer, RoundedRectangle inner, Color background, Color border) : DrawCommand
{
    internal RoundedRectangle Outer { get; } = outer;
    internal RoundedRectangle Inner { get; } = inner;
    internal Color Background { get; } = background;
    internal Color Border { get; } = border;
}

/// <summary>
/// Pushes an opacity factor multiplied into each draw within the scope.
/// </summary>
public sealed class PushOpacityCommand : DrawCommand
{
    internal PushOpacityCommand(double opacity) => Opacity = opacity;

    /// <summary>
    /// Gets the opacity factor from zero through one.
    /// </summary>
    public double Opacity { get; }
}

/// <summary>
/// Restores the complete drawing state saved by the most recent unmatched state scope.
/// </summary>
public sealed class PopCommand : DrawCommand
{
    internal static readonly PopCommand Instance = new();

    private PopCommand()
    {
    }
}

/// <summary>
/// Represents a solid-color rectangle in local drawing coordinates.
/// </summary>
public sealed class FillRectangleCommand : DrawCommand
{
    internal FillRectangleCommand(
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
/// Represents a solid-color triangle mesh draw in local drawing coordinates.
/// </summary>
internal sealed class DrawGeometryCommand : DrawCommand
{
    internal DrawGeometryCommand(TriangleMesh mesh, Color color)
    {
        Mesh = mesh;
        Color = color;
    }

    internal TriangleMesh Mesh { get; }

    internal Color Color { get; }
}

/// <summary>
/// Represents an image draw in local drawing coordinates.
/// </summary>
public sealed class DrawImageCommand : DrawCommand
{
    internal DrawImageCommand(
        Rect bounds,
        Image image,
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
    public Image Image { get; }

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
public sealed class DrawTextCommand : DrawCommand
{
    internal DrawTextCommand(
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
