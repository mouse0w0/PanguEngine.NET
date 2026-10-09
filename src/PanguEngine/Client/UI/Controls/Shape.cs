using PanguEngine.ComponentModel;
using PanguEngine.Client.UI.Drawing;
using PanguEngine.Client.UI.Drawing.Geometry;

namespace PanguEngine.Client.UI.Controls;

/// <summary>
/// Provides the base class for UI nodes that draw filled and stroked vector geometry.
/// </summary>
/// <remarks>
/// Geometry meshes are rebuilt lazily only when the data, stroke parameters, arranged size, or screen scale
/// change. Changing a brush color never rebuilds geometry.
/// </remarks>
public abstract partial class Shape : UiNode
{
    /// <summary>
    /// Identifies the <see cref="Fill"/> property.
    /// </summary>
    public static readonly Property<Brush?> FillProperty =
        Property.Register<Shape, Brush?>(
            nameof(Fill),
            new SolidColorBrush(new Color(0, 0, 0)),
            validate: IsShapeBrushSupported,
            validationMessage: "Shape Fill must be a solid color brush or null.");

    /// <summary>
    /// Identifies the <see cref="Stroke"/> property.
    /// </summary>
    public static readonly Property<Brush?> StrokeProperty =
        Property.Register<Shape, Brush?>(
            nameof(Stroke),
            defaultValue: null,
            onChanged: static (node, _, _) => node.InvalidateMeasure(),
            validate: IsShapeBrushSupported,
            validationMessage: "Shape Stroke must be a solid color brush or null.");

    /// <summary>
    /// Identifies the <see cref="StrokeThickness"/> property.
    /// </summary>
    public static readonly Property<double> StrokeThicknessProperty =
        Property.Register<Shape, double>(
            nameof(StrokeThickness),
            1,
            onChanged: static (node, _, _) => node.InvalidateMeasure(),
            validate: IsFiniteNonNegative,
            validationMessage: "StrokeThickness must be a finite non-negative value.");

    /// <summary>
    /// Identifies the <see cref="StrokeLineCap"/> property.
    /// </summary>
    public static readonly Property<StrokeLineCap> StrokeLineCapProperty =
        Property.Register<Shape, StrokeLineCap>(
            nameof(StrokeLineCap),
            StrokeLineCap.Butt,
            onChanged: static (node, _, _) => node.InvalidateMeasure());

    /// <summary>
    /// Identifies the <see cref="StrokeLineJoin"/> property.
    /// </summary>
    public static readonly Property<StrokeLineJoin> StrokeLineJoinProperty =
        Property.Register<Shape, StrokeLineJoin>(
            nameof(StrokeLineJoin),
            StrokeLineJoin.Miter,
            onChanged: static (node, _, _) => node.InvalidateMeasure());

    /// <summary>
    /// Identifies the <see cref="StrokeMiterLimit"/> property.
    /// </summary>
    public static readonly Property<double> StrokeMiterLimitProperty =
        Property.Register<Shape, double>(
            nameof(StrokeMiterLimit),
            4,
            onChanged: static (node, _, _) => node.InvalidateMeasure(),
            validate: static value => double.IsFinite(value) && value >= 1,
            validationMessage: "StrokeMiterLimit must be a finite value of at least one.");

    private ShapeGeometry? _geometry;
    private ShapeGeometryKey _geometryKey;

    /// <summary>
    /// Initializes a shape.
    /// </summary>
    protected Shape()
    {
    }

    /// <summary>
    /// Gets or sets the brush used to fill the shape geometry, or null for no fill.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the value is neither null nor a solid color brush.</exception>
    public Brush? Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    /// <summary>
    /// Gets or sets the brush used to stroke the shape outline, or null for no stroke.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the value is neither null nor a solid color brush.</exception>
    public Brush? Stroke
    {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    /// <summary>
    /// Gets or sets the non-negative stroke width in logical pixels. A width of zero draws no stroke.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the value is not finite and non-negative.</exception>
    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    /// <summary>
    /// Gets or sets the shape of open stroke endpoints.
    /// </summary>
    public StrokeLineCap StrokeLineCap
    {
        get => GetValue(StrokeLineCapProperty);
        set => SetValue(StrokeLineCapProperty, value);
    }

    /// <summary>
    /// Gets or sets how adjacent stroke segments join.
    /// </summary>
    public StrokeLineJoin StrokeLineJoin
    {
        get => GetValue(StrokeLineJoinProperty);
        set => SetValue(StrokeLineJoinProperty, value);
    }

    /// <summary>
    /// Gets or sets the miter length limit as a multiple of the stroke width. Values below one are rejected.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the value is not finite or is below one.</exception>
    public double StrokeMiterLimit
    {
        get => GetValue(StrokeMiterLimitProperty);
        set => SetValue(StrokeMiterLimitProperty, value);
    }

    /// <inheritdoc />
    protected sealed override Size MeasureCore(Size availableSize) => MeasureShapeCore(availableSize);

    /// <inheritdoc />
    protected sealed override bool ContainsCore(Point localPoint)
    {
        var geometry = Geometry;
        if (Fill is not null && geometry.FillMesh.Contains(localPoint))
            return true;
        return Stroke is not null && geometry.StrokeMesh.Contains(localPoint);
    }

    /// <inheritdoc />
    protected sealed override void DrawCore(UiDrawingContext context)
    {
        var fillColor = ((SolidColorBrush?)Fill)?.Color;
        var strokeColor = ((SolidColorBrush?)Stroke)?.Color;
        if (fillColor is null && strokeColor is null)
            return;
        if (DrawAnalyticShape(context, fillColor, strokeColor))
            return;

        var analyticFill = fillColor is { } color && DrawAnalyticFill(context, color);
        if (!analyticFill && fillColor is { } fill)
        {
            var mesh = Geometry.FillMesh;
            if (mesh.Vertices.Length > 0)
                context.DrawGeometry(mesh, fill);
        }
        if (strokeColor is { } stroke)
        {
            var mesh = Geometry.StrokeMesh;
            if (mesh.Vertices.Length > 0)
                context.DrawGeometry(mesh, stroke);
        }
    }

    /// <summary>Draws a specialized fill and returns whether the fill was handled.</summary>
    private protected virtual bool DrawAnalyticFill(UiDrawingContext context, Color color) => false;

    /// <summary>Draws a specialized fill and stroke and returns whether the shape was handled.</summary>
    private protected virtual bool DrawAnalyticShape(UiDrawingContext context, Color? fill, Color? stroke) => false;

    /// <summary>
    /// Measures this shape's geometry for the available size.
    /// </summary>
    /// <param name="availableSize">The content size available after framework constraints.</param>
    /// <returns>The finite non-negative desired content size, excluding margin.</returns>
    protected abstract Size MeasureShapeCore(Size availableSize);

    /// <summary>
    /// Builds the immutable geometry for an arranged shape.
    /// </summary>
    /// <param name="key">The current geometry inputs.</param>
    /// <returns>The meshes and natural bounds in local drawing coordinates.</returns>
    private protected abstract ShapeGeometry BuildGeometry(ShapeGeometryKey key);

    /// <summary>
    /// Gets the path geometry that participates in the geometry key, or null when the shape has no path data.
    /// </summary>
    private protected virtual PathGeometry? GetGeometryData() => null;

    /// <summary>
    /// Gets the fill rule that participates in the geometry key.
    /// </summary>
    private protected virtual ShapeFillRule GetGeometryFillRule() => ShapeFillRule.NonZero;

    /// <summary>
    /// Gets the stretch mode that participates in the geometry key.
    /// </summary>
    private protected virtual PathStretch GetGeometryStretch() => PathStretch.None;

    /// <summary>Gets the elliptical corner radii that participate in the geometry key.</summary>
    private protected virtual Point GetGeometryRadii() => Point.Zero;

    /// <summary>
    /// Gets the cached geometry for the current inputs, rebuilding it when the key changes.
    /// </summary>
    private protected ShapeGeometry Geometry
    {
        get
        {
            var key = CreateGeometryKey();
            if (_geometry is null || !_geometryKey.Equals(key))
            {
                _geometry = BuildGeometry(key);
                _geometryKey = key;
            }

            return _geometry;
        }
    }

    private ShapeGeometryKey CreateGeometryKey()
    {
        var arranged = IsArrangeValid;
        var radii = GetGeometryRadii();
        return new ShapeGeometryKey(
            GetGeometryData(),
            GetGeometryFillRule(),
            GetGeometryStretch(),
            Stroke is not null,
            StrokeThickness,
            StrokeLineCap,
            StrokeLineJoin,
            StrokeMiterLimit,
            arranged ? LayoutBounds.Width : 0,
            arranged ? LayoutBounds.Height : 0,
            Screen?.Scale ?? 1,
            radii.X,
            radii.Y);
    }

    /// <summary>
    /// Tessellates normalized contours and applies the requested stretch into local drawing coordinates.
    /// </summary>
    private protected ShapeGeometry ComposeGeometry(
        FlattenedContour[] contours,
        Rect sourceBounds,
        double tolerance,
        double targetWidth,
        double targetHeight,
        PathStretch stretch,
        ShapeFillRule fillRule,
        bool hasStroke,
        double strokeThickness)
    {
        var fillMesh = UiShapeTessellator.Fill(contours, fillRule);
        var strokeMesh = hasStroke && strokeThickness > 0
            ? UiShapeTessellator.Stroke(
                contours,
                strokeThickness,
                StrokeLineCap,
                StrokeLineJoin,
                StrokeMiterLimit,
                tolerance)
            : UiTriangleMesh.Empty;
        var bounds = UnionBounds(sourceBounds, strokeMesh);
        var (scaleX, scaleY, translateX, translateY) =
            ComputeStretch(bounds, targetWidth, targetHeight, stretch);
        var originX = translateX - scaleX * bounds.X;
        var originY = translateY - scaleY * bounds.Y;
        return new ShapeGeometry(
            fillMesh.Transform(scaleX, scaleY, originX, originY),
            strokeMesh.Transform(scaleX, scaleY, originX, originY),
            new Rect(0, 0, bounds.Width, bounds.Height));
    }

    /// <summary>
    /// Gets the flattening tolerance in local units for a physical scale factor.
    /// </summary>
    private protected static double ComputeTolerance(double physicalScale) =>
        physicalScale > 0 ? 0.25 / physicalScale : 0.25;

    /// <summary>
    /// Gets the largest stretch scale applied to the natural bounds for the target size.
    /// </summary>
    private protected static double ComputeStretchScale(
        Rect bounds,
        double targetWidth,
        double targetHeight,
        PathStretch stretch)
    {
        if (stretch == PathStretch.None || (targetWidth <= 0 && targetHeight <= 0))
            return 1;

        switch (stretch)
        {
            case PathStretch.Fill:
            {
                var scale = 0d;
                if (bounds.Width > 0)
                    scale = Math.Max(scale, Math.Abs(targetWidth / bounds.Width));
                if (bounds.Height > 0)
                    scale = Math.Max(scale, Math.Abs(targetHeight / bounds.Height));
                return scale;
            }
            case PathStretch.Uniform:
            {
                var scale = double.PositiveInfinity;
                if (bounds.Width > 0)
                    scale = Math.Min(scale, Math.Abs(targetWidth / bounds.Width));
                if (bounds.Height > 0)
                    scale = Math.Min(scale, Math.Abs(targetHeight / bounds.Height));
                return double.IsPositiveInfinity(scale) ? 1 : scale;
            }
            default:
                throw new InvalidOperationException("PathStretch has an undefined value.");
        }
    }

    private static Rect UnionBounds(Rect source, UiTriangleMesh strokeMesh)
    {
        if (strokeMesh.Vertices.Length == 0)
            return source;

        var strokeBounds = strokeMesh.Bounds;
        var minX = Math.Min(source.X, strokeBounds.X);
        var minY = Math.Min(source.Y, strokeBounds.Y);
        var maxX = Math.Max(source.X + source.Width, strokeBounds.X + strokeBounds.Width);
        var maxY = Math.Max(source.Y + source.Height, strokeBounds.Y + strokeBounds.Height);
        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    private static (double ScaleX, double ScaleY, double TranslateX, double TranslateY) ComputeStretch(
        Rect bounds,
        double targetWidth,
        double targetHeight,
        PathStretch stretch)
    {
        switch (stretch)
        {
            case PathStretch.None:
                return (1, 1, 0, 0);
            case PathStretch.Fill:
                return (
                    GetAxisScale(bounds.Width, targetWidth),
                    GetAxisScale(bounds.Height, targetHeight),
                    0,
                    0);
            case PathStretch.Uniform:
            {
                var scaleX = bounds.Width > 0 ? targetWidth / bounds.Width : (double?)null;
                var scaleY = bounds.Height > 0 ? targetHeight / bounds.Height : (double?)null;
                var scale = scaleX.HasValue && scaleY.HasValue
                    ? Math.Min(scaleX.Value, scaleY.Value)
                    : scaleX ?? scaleY ?? 1;
                return (
                    scale,
                    scale,
                    (targetWidth - bounds.Width * scale) / 2,
                    (targetHeight - bounds.Height * scale) / 2);
            }
            default:
                throw new InvalidOperationException("PathStretch has an undefined value.");
        }
    }

    private static double GetAxisScale(double source, double target) =>
        source > 0 ? target / source : 1;

    private static bool IsShapeBrushSupported(Brush? brush) => brush is null or SolidColorBrush;

    /// <summary>
    /// Describes every value that affects a shape's generated geometry.
    /// </summary>
    private protected readonly record struct ShapeGeometryKey(
        PathGeometry? Data,
        ShapeFillRule FillRule,
        PathStretch Stretch,
        bool HasStroke,
        double StrokeThickness,
        StrokeLineCap StrokeLineCap,
        StrokeLineJoin StrokeLineJoin,
        double StrokeMiterLimit,
        double Width,
        double Height,
        double Scale,
        double RadiusX,
        double RadiusY);
}
