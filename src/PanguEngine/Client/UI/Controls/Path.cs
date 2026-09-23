using PanguEngine.Client.UI.Drawing;
using PanguEngine.Client.UI.Styling;

namespace PanguEngine.Client.UI.Controls;

/// <summary>
/// Draws geometry described by an immutable path geometry.
/// </summary>
/// <remarks>
/// The geometry is supplied by the caller and participates in the mesh cache by object identity. The natural size
/// includes the effective stroke, and the path is normalized so that its top-left drawing bound maps to the local
/// origin. The stretch mode scales the whole mesh, including the stroke.
/// </remarks>
public sealed class Path : Shape
{
    /// <summary>
    /// Identifies the <see cref="Data"/> property.
    /// </summary>
    public static readonly UiProperty<PathGeometry?> DataProperty =
        UiProperty.Register<Path, PathGeometry?>(
            nameof(Data),
            defaultValue: null,
            UiPropertyInvalidation.Measure | UiPropertyInvalidation.Render);

    /// <summary>
    /// Identifies the <see cref="FillRule"/> property.
    /// </summary>
    public static readonly UiProperty<ShapeFillRule> FillRuleProperty =
        UiProperty.Register<Path, ShapeFillRule>(
            nameof(FillRule),
            ShapeFillRule.NonZero,
            UiPropertyInvalidation.Render);

    /// <summary>
    /// Identifies the <see cref="Stretch"/> property.
    /// </summary>
    public static readonly UiProperty<PathStretch> StretchProperty =
        UiProperty.Register<Path, PathStretch>(
            nameof(Stretch),
            PathStretch.Uniform,
            UiPropertyInvalidation.Measure | UiPropertyInvalidation.Render);

    static Path()
    {
        UiCssRegistry.RegisterElement<Path>("Path");
        UiCssRegistry.RegisterProperty<Path, PathGeometry?>("data", DataProperty, ParsePathData);
        UiCssRegistry.RegisterProperty<Path, ShapeFillRule>("fill-rule", FillRuleProperty, ParseFillRule);
        UiCssRegistry.RegisterProperty<Path, PathStretch>("stretch", StretchProperty, ParsePathStretch);
    }

    /// <summary>
    /// Initializes a path shape.
    /// </summary>
    public Path()
    {
    }

    /// <summary>
    /// Gets or sets the immutable path geometry. A null value describes an empty path.
    /// </summary>
    public PathGeometry? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    /// <summary>
    /// Gets or sets the rule that determines which areas of the path are filled.
    /// </summary>
    public ShapeFillRule FillRule
    {
        get => GetValue(FillRuleProperty);
        set => SetValue(FillRuleProperty, value);
    }

    /// <summary>
    /// Gets or sets how the path and its stroke fit the arranged bounds.
    /// </summary>
    public PathStretch Stretch
    {
        get => GetValue(StretchProperty);
        set => SetValue(StretchProperty, value);
    }

    /// <inheritdoc />
    protected override Size MeasureShapeCore(Size availableSize)
    {
        var bounds = Geometry.NaturalBounds;
        return new Size(bounds.Width, bounds.Height);
    }

    /// <inheritdoc />
    private protected override PathGeometry? GetGeometryData() => Data;

    /// <inheritdoc />
    private protected override ShapeFillRule GetGeometryFillRule() => FillRule;

    /// <inheritdoc />
    private protected override PathStretch GetGeometryStretch() => Stretch;

    /// <inheritdoc />
    private protected override ShapeGeometry BuildGeometry(ShapeGeometryKey key)
    {
        var data = key.Data;
        if (data is null)
            return ShapeGeometry.Empty;

        var hasStroke = key.HasStroke && key.StrokeThickness > 0;
        var estimateBounds = EstimateStrokeBounds(
            data.Bounds,
            hasStroke ? key.StrokeThickness : 0);
        var estimateScale = ComputeStretchScale(
            estimateBounds,
            key.Width,
            key.Height,
            key.Stretch);
        var tolerance = ComputeTolerance(key.Scale * estimateScale);
        var contours = data.Flatten(tolerance);
        var geometry = ComposeGeometry(
            contours,
            data.Bounds,
            tolerance,
            key.Width,
            key.Height,
            key.Stretch,
            key.FillRule,
            hasStroke,
            key.StrokeThickness);

        var actualScale = ComputeStretchScale(
            geometry.NaturalBounds,
            key.Width,
            key.Height,
            key.Stretch);
        var refinedTolerance = ComputeTolerance(key.Scale * actualScale);
        if (refinedTolerance < tolerance)
        {
            contours = data.Flatten(refinedTolerance);
            geometry = ComposeGeometry(
                contours,
                data.Bounds,
                refinedTolerance,
                key.Width,
                key.Height,
                key.Stretch,
                key.FillRule,
                hasStroke,
                key.StrokeThickness);
        }

        return geometry;
    }

    private static Rect EstimateStrokeBounds(Rect bounds, double thickness) =>
        new(
            bounds.X - thickness / 2,
            bounds.Y - thickness / 2,
            bounds.Width + thickness,
            bounds.Height + thickness);

    private static ShapeFillRule ParseFillRule(string value) =>
        value.ToLowerInvariant() switch
        {
            "nonzero" => ShapeFillRule.NonZero,
            "evenodd" => ShapeFillRule.EvenOdd,
            _ => throw new FormatException($"Value '{value}' is not a recognized fill rule.")
        };

    private static PathGeometry? ParsePathData(string value)
    {
        if (value.Length >= 2 &&
            ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
            value = value[1..^1];

        return string.IsNullOrWhiteSpace(value) ? null : PathGeometry.Parse(value);
    }

    private static PathStretch ParsePathStretch(string value) =>
        value.ToLowerInvariant() switch
        {
            "none" => PathStretch.None,
            "fill" => PathStretch.Fill,
            "uniform" => PathStretch.Uniform,
            _ => throw new FormatException($"Value '{value}' is not a recognized path stretch mode.")
        };
}
