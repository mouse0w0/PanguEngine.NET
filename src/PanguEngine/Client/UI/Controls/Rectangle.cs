using PanguEngine.Client.UI.Drawing;
using PanguEngine.Client.UI.Drawing.Geometry;
using PanguEngine.Client.UI.Styling;
using PanguEngine.ComponentModel;

namespace PanguEngine.Client.UI.Controls;

/// <summary>
/// Draws an axis-aligned rectangle with optional elliptical corners using the arranged layout size.
/// </summary>
/// <remarks>
/// The stroke is kept inside the layout box by insetting the outline by half the stroke width.
/// A stroke wider than the box is clamped to the smaller layout dimension.
/// </remarks>
public sealed class Rectangle : Shape
{
    /// <summary>Identifies the <see cref="RadiusX"/> property.</summary>
    public static readonly Property<double> RadiusXProperty = Property.Register<Rectangle, double>(
        nameof(RadiusX), 0, onChanged: static (node, _, _) => node.InvalidateMeasure());

    /// <summary>Identifies the <see cref="RadiusY"/> property.</summary>
    public static readonly Property<double> RadiusYProperty = Property.Register<Rectangle, double>(
        nameof(RadiusY), 0, onChanged: static (node, _, _) => node.InvalidateMeasure());

    /// <summary>Gets or sets the finite non-negative horizontal corner radius in logical pixels.</summary>
    public double RadiusX
    {
        get => GetValue(RadiusXProperty);
        set => SetValue(RadiusXProperty, value);
    }

    /// <summary>Gets or sets the finite non-negative vertical corner radius in logical pixels.</summary>
    public double RadiusY
    {
        get => GetValue(RadiusYProperty);
        set => SetValue(RadiusYProperty, value);
    }

    static Rectangle()
    {
        UiCssRegistry.RegisterElement<Rectangle>("Rectangle");
        UiCssRegistry.RegisterProperty<Rectangle, double>("radius-x", RadiusXProperty, ParseRadius);
        UiCssRegistry.RegisterProperty<Rectangle, double>("radius-y", RadiusYProperty, ParseRadius);
    }

    /// <summary>
    /// Initializes a rectangle shape.
    /// </summary>
    public Rectangle()
    {
    }

    private static double ParseRadius(string value)
    {
        var radius = UiCssValueConverters.ParseLength(value);
        if (radius < 0)
            throw new FormatException("Rectangle radii must be non-negative.");
        return radius;
    }

    /// <inheritdoc />
    protected override Size MeasureShapeCore(Size availableSize)
    {
        if (!double.IsFinite(RadiusX) || RadiusX < 0 || !double.IsFinite(RadiusY) || RadiusY < 0)
            throw new InvalidOperationException("Rectangle radii must be finite non-negative values.");
        return Size.Zero;
    }

    /// <inheritdoc />
    private protected override Point GetGeometryRadii() => new(RadiusX, RadiusY);

    /// <inheritdoc />
    private protected override bool DrawAnalyticFill(UiDrawingContext context, Color color)
    {
        var width = IsArrangeValid ? LayoutBounds.Width : 0;
        var height = IsArrangeValid ? LayoutBounds.Height : 0;
        var thickness = Stroke is not null ? Math.Min(StrokeThickness, Math.Min(width, height)) : 0;
        var bounds = new Rect(thickness / 2, thickness / 2, width - thickness, height - thickness);
        var radius = new Point(RadiusX, RadiusY);
        context.FillRoundedRectangle(new UiRoundedClipGeometry(
            new UiRoundedRectangle(bounds, radius, radius, radius, radius)), new SolidColorBrush(color));
        return true;
    }

    /// <inheritdoc />
    private protected override bool DrawAnalyticShape(UiDrawingContext context, Color? fill, Color? stroke)
    {
        if (stroke is null)
            return false;
        var width = IsArrangeValid ? LayoutBounds.Width : 0;
        var height = IsArrangeValid ? LayoutBounds.Height : 0;
        var thickness = Math.Min(StrokeThickness, Math.Min(width, height));
        if (thickness == 0)
        {
            if (fill is { } color)
                DrawAnalyticFill(context, color);
            return true;
        }
        if (width <= thickness || height <= thickness)
            return false;
        var bounds = new Rect(thickness / 2, thickness / 2, width - thickness, height - thickness);
        var radius = new Point(RadiusX, RadiusY);
        var join = StrokeLineJoin switch
        {
            StrokeLineJoin.Round => StrokeLineJoin.Round,
            StrokeLineJoin.Bevel => StrokeLineJoin.Bevel,
            _ => StrokeMiterLimit >= Math.Sqrt(2) ? StrokeLineJoin.Miter : StrokeLineJoin.Bevel
        };
        context.DrawRoundedShape(new UiRoundedRectangle(bounds, radius, radius, radius, radius),
            thickness, fill ?? default, stroke.Value, join);
        return true;
    }

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
        if (key.RadiusX > 0 && key.RadiusY > 0 && geometryWidth > 0 && geometryHeight > 0)
        {
            var radius = new Point(key.RadiusX, key.RadiusY);
            points = new UiRoundedRectangle(new Rect(inset, inset, geometryWidth, geometryHeight),
                radius, radius, radius, radius).GetContour(key.Scale);
        }
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
