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
    public static readonly UiProperty<Brush?> FillProperty =
        UiProperty.Register<Shape, Brush?>(
            nameof(Fill),
            new SolidColorBrush(new Color(0, 0, 0)),
            UiPropertyInvalidation.Render);

    /// <summary>
    /// Identifies the <see cref="Stroke"/> property.
    /// </summary>
    public static readonly UiProperty<Brush?> StrokeProperty =
        UiProperty.Register<Shape, Brush?>(
            nameof(Stroke),
            defaultValue: null,
            invalidation: UiPropertyInvalidation.Measure | UiPropertyInvalidation.Render);

    /// <summary>
    /// Identifies the <see cref="StrokeThickness"/> property.
    /// </summary>
    public static readonly UiProperty<double> StrokeThicknessProperty =
        UiProperty.Register<Shape, double>(
            nameof(StrokeThickness),
            1,
            UiPropertyInvalidation.Measure | UiPropertyInvalidation.Render);

    /// <summary>
    /// Identifies the <see cref="StrokeLineCap"/> property.
    /// </summary>
    public static readonly UiProperty<StrokeLineCap> StrokeLineCapProperty =
        UiProperty.Register<Shape, StrokeLineCap>(
            nameof(StrokeLineCap),
            StrokeLineCap.Butt,
            UiPropertyInvalidation.Measure | UiPropertyInvalidation.Render);

    /// <summary>
    /// Identifies the <see cref="StrokeLineJoin"/> property.
    /// </summary>
    public static readonly UiProperty<StrokeLineJoin> StrokeLineJoinProperty =
        UiProperty.Register<Shape, StrokeLineJoin>(
            nameof(StrokeLineJoin),
            StrokeLineJoin.Miter,
            UiPropertyInvalidation.Measure | UiPropertyInvalidation.Render);

    /// <summary>
    /// Identifies the <see cref="StrokeMiterLimit"/> property.
    /// </summary>
    public static readonly UiProperty<double> StrokeMiterLimitProperty =
        UiProperty.Register<Shape, double>(
            nameof(StrokeMiterLimit),
            4,
            UiPropertyInvalidation.Measure | UiPropertyInvalidation.Render);

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
    public Brush? Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    /// <summary>
    /// Gets or sets the brush used to stroke the shape outline, or null for no stroke.
    /// </summary>
    public Brush? Stroke
    {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    /// <summary>
    /// Gets or sets the non-negative stroke width in logical pixels. A width of zero draws no stroke.
    /// </summary>
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
    public double StrokeMiterLimit
    {
        get => GetValue(StrokeMiterLimitProperty);
        set => SetValue(StrokeMiterLimitProperty, value);
    }

    /// <inheritdoc />
    protected sealed override Size MeasureCore(Size availableSize)
    {
        ValidateStrokeParameters();
        return MeasureShapeCore(availableSize);
    }

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
        var fillColor = ResolveBrushColor(Fill, nameof(Fill));
        var strokeColor = ResolveBrushColor(Stroke, nameof(Stroke));
        if (fillColor is null && strokeColor is null)
            return;

        var geometry = Geometry;
        if (fillColor is { } fill && geometry.FillMesh.Vertices.Length > 0)
            context.DrawGeometry(geometry.FillMesh, fill);
        if (strokeColor is { } stroke && geometry.StrokeMesh.Vertices.Length > 0)
            context.DrawGeometry(geometry.StrokeMesh, stroke);
    }

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
            Screen?.Scale ?? 1);
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

    private static Color? ResolveBrushColor(Brush? brush, string propertyName) =>
        brush switch
        {
            null => null,
            SolidColorBrush solidColorBrush => solidColorBrush.Color,
            _ => throw new NotSupportedException(
                $"Brush type '{brush.GetType().FullName}' is not supported for shape {propertyName}.")
        };

    private void ValidateStrokeParameters()
    {
        if (!double.IsFinite(StrokeThickness) || StrokeThickness < 0)
            throw new InvalidOperationException("StrokeThickness must be a finite non-negative value.");
        if (!double.IsFinite(StrokeMiterLimit) || StrokeMiterLimit < 1)
            throw new InvalidOperationException("StrokeMiterLimit must be a finite value of at least one.");
    }

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
        double Scale);
}
