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
    /// Gets or sets the background fill, or null for no background.
    /// </summary>
    public Brush? Background
    {
        get => GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    /// <summary>
    /// Gets the arranged decoration boundary in local coordinates, or zero when arrangement is invalid.
    /// </summary>
    public Rect DecorationBounds =>
        IsArrangeValid
            ? new Rect(0, 0, LayoutBounds.Width, LayoutBounds.Height)
            : Rect.Zero;

    /// <summary>
    /// Gets the arranged content boundary in local coordinates, or zero when arrangement is invalid.
    /// </summary>
    public Rect ContentBounds =>
        IsArrangeValid
            ? GetContentBounds(new Size(LayoutBounds.Width, LayoutBounds.Height), Padding)
            : Rect.Zero;

    /// <summary>
    /// Measures the content within the size left after padding.
    /// </summary>
    /// <param name="availableSize">The available content size, excluding padding.</param>
    /// <returns>The desired content size, excluding padding and this region's margin.</returns>
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
    /// Arranges the content within the local boundary left after padding.
    /// </summary>
    /// <param name="contentBounds">The local content boundary, excluding padding.</param>
    protected virtual void ArrangeContent(Rect contentBounds)
    {
        foreach (var child in Children)
            child.Arrange(contentBounds);
    }

    /// <inheritdoc />
    protected sealed override Size MeasureCore(Size availableSize)
    {
        var padding = Padding;
        var contentAvailableSize = new Size(
            SubtractPadding(availableSize.Width, padding.Left, padding.Right),
            SubtractPadding(availableSize.Height, padding.Top, padding.Bottom));
        var contentDesiredSize = MeasureContent(contentAvailableSize);
        return new Size(
            AddPadding(contentDesiredSize.Width, padding.Left, padding.Right),
            AddPadding(contentDesiredSize.Height, padding.Top, padding.Bottom));
    }

    /// <inheritdoc />
    protected sealed override void ArrangeCore(Size finalSize) =>
        ArrangeContent(GetContentBounds(finalSize, Padding));

    private static Rect GetContentBounds(Size finalSize, Thickness padding) =>
        new(
            Math.Min(padding.Left, finalSize.Width),
            Math.Min(padding.Top, finalSize.Height),
            SubtractPadding(finalSize.Width, padding.Left, padding.Right),
            SubtractPadding(finalSize.Height, padding.Top, padding.Bottom));

    private static double SubtractPadding(
        double available,
        double leadingPadding,
        double trailingPadding)
    {
        if (double.IsPositiveInfinity(available))
            return double.PositiveInfinity;

        var padding = leadingPadding + trailingPadding;
        return double.IsPositiveInfinity(padding) ? 0 : Math.Max(0, available - padding);
    }

    private static double AddPadding(
        double content,
        double leadingPadding,
        double trailingPadding)
    {
        var result = content + leadingPadding + trailingPadding;
        if (!double.IsFinite(result))
            throw new InvalidOperationException("Measurement produced a non-finite desired size.");

        return result;
    }
}
