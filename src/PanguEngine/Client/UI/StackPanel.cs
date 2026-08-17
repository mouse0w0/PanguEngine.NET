namespace PanguEngine.Client.UI;

/// <summary>
/// Arranges child nodes sequentially along one axis.
/// </summary>
public sealed class StackPanel : Panel
{
    /// <summary>
    /// Identifies the <see cref="Orientation"/> property.
    /// </summary>
    public static readonly UiProperty<Orientation> OrientationProperty =
        UiProperty.Register<StackPanel, Orientation>(
            nameof(Orientation),
            Orientation.Vertical,
            UiPropertyInvalidation.Measure);

    /// <summary>
    /// Identifies the <see cref="Spacing"/> property.
    /// </summary>
    public static readonly UiProperty<double> SpacingProperty =
        UiProperty.Register<StackPanel, double>(
            nameof(Spacing),
            invalidation: UiPropertyInvalidation.Measure);

    /// <summary>
    /// Gets or sets the axis along which children are arranged.
    /// </summary>
    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>
    /// Gets or sets the finite non-negative spacing between participating children.
    /// </summary>
    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    /// <inheritdoc />
    protected override Size MeasureContent(Size availableSize)
    {
        var (orientation, spacing) = GetLayoutProperties();
        var childConstraint = CreateSize(
            orientation,
            double.PositiveInfinity,
            GetCross(availableSize, orientation));
        var desiredMain = 0d;
        var desiredCross = 0d;
        var hasParticipant = false;

        foreach (var child in Children)
        {
            child.Measure(childConstraint);
            if (child.Visibility == Visibility.Collapsed)
                continue;

            if (hasParticipant)
            {
                desiredMain = AddFinite(
                    desiredMain,
                    spacing,
                    "StackPanel measurement overflowed the main axis.");
            }

            desiredMain = AddFinite(
                desiredMain,
                GetMain(child.DesiredSize, orientation),
                "StackPanel measurement overflowed the main axis.");
            desiredCross = Math.Max(desiredCross, GetCross(child.DesiredSize, orientation));
            hasParticipant = true;
        }

        return CreateSize(orientation, desiredMain, desiredCross);
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Rect contentBounds)
    {
        var (orientation, spacing) = GetLayoutProperties();
        var cursor = GetMainOrigin(contentBounds, orientation);
        var crossOrigin = GetCrossOrigin(contentBounds, orientation);
        var crossExtent = GetCrossExtent(contentBounds, orientation);
        var hasParticipant = false;

        foreach (var child in Children)
        {
            if (child.Visibility == Visibility.Collapsed)
            {
                child.Arrange(Rect.Zero);
                continue;
            }

            if (hasParticipant)
            {
                cursor = AddFinite(
                    cursor,
                    spacing,
                    "StackPanel arrangement produced a non-finite child slot origin.");
            }

            var childMain = GetMain(child.DesiredSize, orientation);
            child.Arrange(CreateRect(orientation, cursor, crossOrigin, childMain, crossExtent));
            cursor += childMain;
            hasParticipant = true;
        }
    }

    private (Orientation Orientation, double Spacing) GetLayoutProperties()
    {
        var orientation = Orientation;
        var spacing = Spacing;
        if (!double.IsFinite(spacing) || spacing < 0)
            throw new InvalidOperationException("Spacing must be a finite non-negative value.");

        return (orientation, spacing);
    }

    private static double GetMain(Size size, Orientation orientation) =>
        orientation == Orientation.Vertical ? size.Height : size.Width;

    private static double GetCross(Size size, Orientation orientation) =>
        orientation == Orientation.Vertical ? size.Width : size.Height;

    private static double GetMainOrigin(Rect rect, Orientation orientation) =>
        orientation == Orientation.Vertical ? rect.Y : rect.X;

    private static double GetCrossOrigin(Rect rect, Orientation orientation) =>
        orientation == Orientation.Vertical ? rect.X : rect.Y;

    private static double GetCrossExtent(Rect rect, Orientation orientation) =>
        orientation == Orientation.Vertical ? rect.Width : rect.Height;

    private static Size CreateSize(Orientation orientation, double main, double cross) =>
        orientation == Orientation.Vertical
            ? new Size(cross, main)
            : new Size(main, cross);

    private static Rect CreateRect(
        Orientation orientation,
        double mainOrigin,
        double crossOrigin,
        double mainExtent,
        double crossExtent) =>
        orientation == Orientation.Vertical
            ? new Rect(crossOrigin, mainOrigin, crossExtent, mainExtent)
            : new Rect(mainOrigin, crossOrigin, mainExtent, crossExtent);

    private static double AddFinite(double value, double addition, string message)
    {
        var result = value + addition;
        if (!double.IsFinite(result))
            throw new InvalidOperationException(message);

        return result;
    }
}
