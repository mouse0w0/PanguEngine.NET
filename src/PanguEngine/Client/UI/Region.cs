namespace PanguEngine.Client.UI;

/// <summary>
/// Provides the shared rectangular box model for UI parent nodes.
/// </summary>
public abstract class Region : Parent
{
    /// <summary>
    /// Identifies the <see cref="Padding"/> property.
    /// </summary>
    public static readonly UiProperty<Thickness> PaddingProperty =
        UiProperty.Register<Region, Thickness>(
            nameof(Padding),
            Thickness.Zero,
            UiPropertyInvalidation.Measure);

    /// <summary>
    /// Identifies the <see cref="Background"/> property.
    /// </summary>
    public static readonly UiProperty<Brush?> BackgroundProperty =
        UiProperty.Register<Region, Brush?>(
            nameof(Background),
            defaultValue: null,
            invalidation: UiPropertyInvalidation.Render);

    /// <summary>
    /// Identifies the <see cref="BorderBrush"/> property.
    /// </summary>
    public static readonly UiProperty<Brush?> BorderBrushProperty =
        UiProperty.Register<Region, Brush?>(
            nameof(BorderBrush),
            defaultValue: null,
            invalidation: UiPropertyInvalidation.Render);

    /// <summary>
    /// Identifies the <see cref="BorderThickness"/> property.
    /// </summary>
    public static readonly UiProperty<Thickness> BorderThicknessProperty =
        UiProperty.Register<Region, Thickness>(
            nameof(BorderThickness),
            Thickness.Zero,
            UiPropertyInvalidation.Measure | UiPropertyInvalidation.Render);

    private Rect _committedDecorationBounds;
    private Rect _committedBorderInnerBounds;
    private Rect _committedContentBounds;

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
            borderThickness = UiLayoutHelper.RoundLayoutThickness(borderThickness, scale);
            padding = UiLayoutHelper.RoundLayoutThickness(padding, scale);
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
            borderThickness = UiLayoutHelper.RoundLayoutThickness(borderThickness, scale);
            padding = UiLayoutHelper.RoundLayoutThickness(padding, scale);
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
    protected override void DrawCore(UiDrawingContext context)
    {
        var decorationBounds = _committedDecorationBounds;
        var borderInnerBounds = _committedBorderInnerBounds;
        if (Background is { } background)
            context.FillRectangle(borderInnerBounds, background);

        if (BorderBrush is not { } borderBrush)
            return;

        context.FillRectangle(
            new Rect(
                decorationBounds.X,
                decorationBounds.Y,
                decorationBounds.Width,
                borderInnerBounds.Y - decorationBounds.Y),
            borderBrush);
        context.FillRectangle(
            new Rect(
                borderInnerBounds.X + borderInnerBounds.Width,
                borderInnerBounds.Y,
                decorationBounds.X + decorationBounds.Width - (borderInnerBounds.X + borderInnerBounds.Width),
                borderInnerBounds.Height),
            borderBrush);
        context.FillRectangle(
            new Rect(
                decorationBounds.X,
                borderInnerBounds.Y + borderInnerBounds.Height,
                decorationBounds.Width,
                decorationBounds.Y + decorationBounds.Height -
                (borderInnerBounds.Y + borderInnerBounds.Height)),
            borderBrush);
        context.FillRectangle(
            new Rect(
                decorationBounds.X,
                borderInnerBounds.Y,
                borderInnerBounds.X - decorationBounds.X,
                borderInnerBounds.Height),
            borderBrush);
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
