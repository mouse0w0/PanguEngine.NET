using PanguEngine.ComponentModel;
using PanguEngine.UI.Drawing;
using PanguEngine.UI.Drawing.Geometry;
using PanguEngine.UI.Styling;

namespace PanguEngine.UI.Controls;

/// <summary>
/// Provides the shared rectangular box model for UI parent elements.
/// </summary>
public abstract class Region : Container
{
    /// <summary>
    /// Identifies the <see cref="Padding"/> property.
    /// </summary>
    public static readonly Property<Thickness> PaddingProperty =
        Property.Register<Region, Thickness>(
            nameof(Padding),
            Thickness.Zero,
            onChanged: static (node, _, _) => node.InvalidateMeasure());

    /// <summary>
    /// Identifies the <see cref="Background"/> property.
    /// </summary>
    public static readonly Property<Brush?> BackgroundProperty =
        Property.Register<Region, Brush?>(
            nameof(Background),
            defaultValue: null);

    /// <summary>
    /// Identifies the <see cref="BorderBrush"/> property.
    /// </summary>
    public static readonly Property<Brush?> BorderBrushProperty =
        Property.Register<Region, Brush?>(
            nameof(BorderBrush),
            defaultValue: null);

    /// <summary>
    /// Identifies the <see cref="BorderThickness"/> property.
    /// </summary>
    public static readonly Property<Thickness> BorderThicknessProperty =
        Property.Register<Region, Thickness>(
            nameof(BorderThickness),
            Thickness.Zero,
            onChanged: static (node, _, _) => node.InvalidateMeasure());

    private Rect _committedDecorationBounds;
    private Rect _committedBorderInnerBounds;
    private Rect _committedContentBounds;
    private RoundedClipGeometry? _roundedBackground;
    private RoundedClipGeometry? _roundedBorder;
    private RoundedClipGeometry? _roundedDescendantClip;

    /// <summary>Identifies the <see cref="CornerRadius"/> property.</summary>
    public static readonly Property<CornerRadius> CornerRadiusProperty =
        Property.Register<Region, CornerRadius>(nameof(CornerRadius), CornerRadius.Zero);

    /// <summary>
    /// Gets or sets the outer decoration radii. Descendants follow these corners only when clipping is enabled.
    /// </summary>
    public CornerRadius CornerRadius
    {
        get => GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    static Region()
    {
        CssRegistry.RegisterElement<Region>("Region");
        CssRegistry.RegisterProperty<Region, CornerRadius>("border-radius", CornerRadiusProperty,
            CssValueConverters.ParseCornerRadius);
        CssRegistry.RegisterProperty<Region, Thickness>("padding", PaddingProperty,
            CssValueConverters.ParseThickness);
        CssRegistry.RegisterProperty<Region>("padding-top", value =>
            new[]
            {
                StyleSetter.CreateEdge(PaddingProperty, StyleEdge.Top, CssValueConverters.ParseLength(value))
            });
        CssRegistry.RegisterProperty<Region>("padding-right", value =>
            new[]
            {
                StyleSetter.CreateEdge(PaddingProperty, StyleEdge.Right, CssValueConverters.ParseLength(value))
            });
        CssRegistry.RegisterProperty<Region>("padding-bottom", value =>
            new[]
            {
                StyleSetter.CreateEdge(PaddingProperty, StyleEdge.Bottom, CssValueConverters.ParseLength(value))
            });
        CssRegistry.RegisterProperty<Region>("padding-left", value =>
            new[]
            {
                StyleSetter.CreateEdge(PaddingProperty, StyleEdge.Left, CssValueConverters.ParseLength(value))
            });
        CssRegistry.RegisterProperty<Region, Brush?>("background", BackgroundProperty,
            CssValueConverters.ParseBrush);
        CssRegistry.RegisterProperty<Region, Brush?>(
            "background-color",
            BackgroundProperty,
            CssValueConverters.ParseBrush);
        CssRegistry.RegisterProperty<Region, Brush?>("border-color", BorderBrushProperty,
            CssValueConverters.ParseBrush);
        CssRegistry.RegisterProperty<Region, Thickness>(
            "border-thickness",
            BorderThicknessProperty,
            CssValueConverters.ParseThickness);
    }

    /// <summary>
    /// Initializes a UI region.
    /// </summary>
    protected Region()
    {
    }

    /// <summary>
    /// Gets or sets the non-negative spacing between the decoration and content bounds.
    /// </summary>
    public Thickness Padding
    {
        get => GetValue(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    /// <summary>
    /// Gets or sets the background fill within the inner edge of the border, or null for no background.
    /// </summary>
    public Brush? Background
    {
        get => GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    /// <summary>
    /// Gets or sets the brush used to fill the border, or null for no visible border.
    /// </summary>
    public Brush? BorderBrush
    {
        get => GetValue(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    /// <summary>
    /// Gets or sets the non-negative thickness reserved for the border.
    /// </summary>
    public Thickness BorderThickness
    {
        get => GetValue(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    /// <summary>
    /// Gets the arranged decoration boundary in local coordinates, or zero when arrangement is invalid.
    /// </summary>
    public Rect DecorationBounds =>
        IsArrangeValid ? _committedDecorationBounds : Rect.Zero;

    /// <summary>
    /// Gets the arranged content boundary in local coordinates, or zero when arrangement is invalid.
    /// </summary>
    public Rect ContentBounds =>
        IsArrangeValid ? _committedContentBounds : Rect.Zero;

    /// <summary>
    /// Measures the content within the size left after the border and padding.
    /// </summary>
    /// <param name="availableSize">The available content size, excluding the border and padding.</param>
    /// <returns>The desired content size, excluding the border, padding, and this region's margin.</returns>
    protected virtual Size MeasureContent(Size availableSize)
    {
        var desiredWidth = 0d;
        var desiredHeight = 0d;
        foreach (var child in Children)
        {
            child.Measure(availableSize);
            desiredWidth = Math.Max(desiredWidth, child.DesiredSize.Width);
            desiredHeight = Math.Max(desiredHeight, child.DesiredSize.Height);
        }

        return new Size(desiredWidth, desiredHeight);
    }

    /// <summary>
    /// Arranges the content within the local boundary left after the border and padding.
    /// </summary>
    /// <param name="contentBounds">The local content boundary, excluding the border and padding.</param>
    protected virtual void ArrangeContent(Rect contentBounds)
    {
        foreach (var child in Children)
            child.Arrange(contentBounds);
    }

    /// <inheritdoc />
    protected sealed override Size MeasureCore(Size availableSize)
    {
        var screen = Screen;
        var useLayoutRounding = screen?.UseLayoutRounding ?? true;
        var scale = screen?.Scale ?? 1;
        var borderThickness = BorderThickness;
        var padding = Padding;
        if (useLayoutRounding)
        {
            borderThickness = LayoutHelper.RoundLayoutThickness(borderThickness, scale);
            padding = LayoutHelper.RoundLayoutThickness(padding, scale);
        }

        var innerAvailableSize = SubtractThickness(availableSize, borderThickness);
        var contentAvailableSize = SubtractThickness(innerAvailableSize, padding);
        var contentDesiredSize = MeasureContent(contentAvailableSize);
        var innerDesiredSize = AddThickness(contentDesiredSize, padding);
        return AddThickness(innerDesiredSize, borderThickness);
    }

    /// <inheritdoc />
    protected sealed override void ArrangeCore(Size finalSize)
    {
        var screen = Screen;
        var useLayoutRounding = screen?.UseLayoutRounding ?? true;
        var scale = screen?.Scale ?? 1;
        var borderThickness = BorderThickness;
        var padding = Padding;
        if (useLayoutRounding)
        {
            borderThickness = LayoutHelper.RoundLayoutThickness(borderThickness, scale);
            padding = LayoutHelper.RoundLayoutThickness(padding, scale);
        }

        var decorationBounds = new Rect(0, 0, finalSize);
        var borderInnerBounds = DeflateBounds(decorationBounds, borderThickness);
        var contentBounds = DeflateBounds(borderInnerBounds, padding);
        ArrangeContent(contentBounds);
        _committedDecorationBounds = decorationBounds;
        _committedBorderInnerBounds = borderInnerBounds;
        _committedContentBounds = contentBounds;
    }

    /// <inheritdoc />
    protected override void DrawCore(DrawingContext context)
    {
        var decorationBounds = _committedDecorationBounds;
        var borderInnerBounds = _committedBorderInnerBounds;
        if (CornerRadius != CornerRadius.Zero)
        {
            if (Background is null && BorderBrush is null)
                return;
            EnsureRoundedGeometry();
            if ((Background is null or SolidColorBrush) && (BorderBrush is null or SolidColorBrush))
            {
                context.DrawRoundedDecoration(_roundedBorder!.Outer, _roundedBorder.Inner!.Value,
                    ((SolidColorBrush?)Background)?.Color ?? default,
                    ((SolidColorBrush?)BorderBrush)?.Color ?? default);
                return;
            }

            if (Background is { } roundedBackground)
                context.FillRoundedRectangle(_roundedBackground!, roundedBackground);
            if (BorderBrush is { } roundedBorder)
                context.FillRoundedBorder(_roundedBorder!, roundedBorder);
            return;
        }

        if (Background is { } background)
            context.FillRectangle(borderInnerBounds, background);

        if (BorderBrush is not { } borderBrush)
            return;

        context.FillBorder(
            decorationBounds,
            borderInnerBounds,
            borderBrush);
    }

    internal override DrawCommand CreateDescendantClip()
    {
        if (CornerRadius == CornerRadius.Zero)
            return base.CreateDescendantClip();
        EnsureRoundedGeometry();
        return new PushRoundedClipCommand(_roundedDescendantClip!);
    }

    internal override bool ContainsDescendantClip(Point point) =>
        new RoundedRectangle(new Rect(0, 0, LayoutBounds.Width, LayoutBounds.Height), CornerRadius).Contains(point);

    private void EnsureRoundedGeometry()
    {
        var outer = new RoundedRectangle(_committedDecorationBounds, CornerRadius);
        var inner = outer.Deflate(_committedBorderInnerBounds);
        if (_roundedBorder is { } cached && cached.Outer == outer && cached.Inner == inner)
            return;
        _roundedBackground = new RoundedClipGeometry(inner, constraint: outer);
        _roundedBorder = new RoundedClipGeometry(outer, inner);
        _roundedDescendantClip = new RoundedClipGeometry(outer);
    }

    private static Rect DeflateBounds(Rect bounds, Thickness thickness) =>
        new(
            bounds.X + Math.Min(thickness.Left, bounds.Width),
            bounds.Y + Math.Min(thickness.Top, bounds.Height),
            SubtractThickness(bounds.Width, thickness.Left, thickness.Right),
            SubtractThickness(bounds.Height, thickness.Top, thickness.Bottom));

    private static Size SubtractThickness(Size size, Thickness thickness) =>
        new(
            SubtractThickness(size.Width, thickness.Left, thickness.Right),
            SubtractThickness(size.Height, thickness.Top, thickness.Bottom));

    private static double SubtractThickness(
        double available,
        double leadingThickness,
        double trailingThickness)
    {
        if (double.IsPositiveInfinity(available))
            return double.PositiveInfinity;

        var thickness = leadingThickness + trailingThickness;
        return double.IsPositiveInfinity(thickness) ? 0 : Math.Max(0, available - thickness);
    }

    private static Size AddThickness(Size size, Thickness thickness) =>
        new(
            AddThickness(size.Width, thickness.Left, thickness.Right),
            AddThickness(size.Height, thickness.Top, thickness.Bottom));

    private static double AddThickness(
        double content,
        double leadingThickness,
        double trailingThickness)
    {
        var result = content + leadingThickness + trailingThickness;
        if (!double.IsFinite(result))
            throw new InvalidOperationException("Measurement produced a non-finite desired size.");

        return result;
    }
}