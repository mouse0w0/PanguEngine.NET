using PanguEngine.Client.UI.Drawing;
using PanguEngine.Client.UI.Drawing.Geometry;
using PanguEngine.Client.UI.Styling;

namespace PanguEngine.Client.UI.Controls;

/// <summary>
/// Draws an ellipse inscribed in the arranged layout bounds.
/// </summary>
/// <remarks>
/// The stroke is kept inside the layout box by using the layout half dimensions less half the stroke width.
/// A stroke wider than the box is clamped to the smaller layout dimension.
/// </remarks>
public sealed class Ellipse : Shape
{
    static Ellipse()
    {
        UiCssRegistry.RegisterElement<Ellipse>("Ellipse");
    }

    /// <summary>
    /// Initializes an ellipse shape.
    /// </summary>
    public Ellipse()
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
        var radiusX = Math.Max(0, (width - thickness) / 2);
        var radiusY = Math.Max(0, (height - thickness) / 2);
        var tolerance = ComputeTolerance(key.Scale);
        var points = BuildEllipsePoints(width / 2, height / 2, radiusX, radiusY, tolerance);
        var contours = new[] { new FlattenedContour(points, true) };
        return ComposeGeometry(
            contours,
            new Rect(0, 0, width, height),
            tolerance,
            width,
            height,
            PathStretch.None,
            ShapeFillRule.NonZero,
            key.HasStroke && thickness > 0,
            thickness);
    }

    private static Point[] BuildEllipsePoints(
        double centerX,
        double centerY,
        double radiusX,
        double radiusY,
        double tolerance)
    {
        var count = 4;
        var maxRadius = Math.Max(radiusX, radiusY);
        if (maxRadius > tolerance)
        {
            var ratio = Math.Clamp(1 - tolerance / maxRadius, -1, 1);
            count = (int)Math.Ceiling(Math.PI / Math.Acos(ratio));
        }

        count = Math.Clamp(count, 4, 8192);
        var points = new Point[count];
        for (var index = 0; index < count; index++)
        {
            var angle = 2 * Math.PI * index / count;
            points[index] = new Point(
                centerX + radiusX * Math.Cos(angle),
                centerY + radiusY * Math.Sin(angle));
        }

        return points;
    }
}
