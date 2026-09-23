using PanguEngine.Client.UI.Drawing;
using PanguEngine.Client.UI.Drawing.Geometry;
using PanguEngine.Client.UI.Styling;

namespace PanguEngine.Client.UI.Controls;

/// <summary>
/// Draws an axis-aligned rectangle using the arranged layout size.
/// </summary>
/// <remarks>
/// The stroke is kept inside the layout box by insetting the outline by half the stroke width.
/// A stroke wider than the box is clamped to the smaller layout dimension.
/// </remarks>
public sealed class Rectangle : Shape
{
    static Rectangle()
    {
        UiCssRegistry.RegisterElement<Rectangle>("Rectangle");
    }

    /// <summary>
    /// Initializes a rectangle shape.
    /// </summary>
    public Rectangle()
    {
    }

    /// <inheritdoc />
    protected override Size MeasureShapeCore(Size availableSize) => Size.Zero;

    /// <inheritdoc />
    private protected override ShapeGeometry BuildGeometry(ShapeGeometryKey key)
    {
        var width = key.Width;
        var height = key.Height;
        if (width <= 0 || height <= 0)
            return ShapeGeometry.Empty;

        var thickness = key.HasStroke ? Math.Min(key.StrokeThickness, Math.Min(width, height)) : 0;
        var inset = thickness / 2;
        var geometryWidth = width - thickness;
        var geometryHeight = height - thickness;
        var points = new[]
        {
            new Point(inset, inset),
            new Point(inset + geometryWidth, inset),
            new Point(inset + geometryWidth, inset + geometryHeight),
            new Point(inset, inset + geometryHeight)
        };
        var contours = new[] { new FlattenedContour(points, true) };
        return ComposeGeometry(
            contours,
            new Rect(0, 0, width, height),
            ComputeTolerance(key.Scale),
            width,
            height,
            PathStretch.None,
            ShapeFillRule.NonZero,
            key.HasStroke && thickness > 0,
            thickness);
    }
}
