using PanguEngine.Client.UI.Styling;

using PanguEngine.ComponentModel;

namespace PanguEngine.Client.UI.Controls;

/// <summary>
/// Arranges child nodes sequentially along one axis and wraps them onto new lines.
/// </summary>
public sealed class WrapPanel : Panel
{
    private const double LayoutTolerance = 1e-10;

    /// <summary>
    /// Identifies the <see cref="Orientation"/> property.
    /// </summary>
    public static readonly Property<Orientation> OrientationProperty =
        Property.Register<WrapPanel, Orientation>(
            nameof(Orientation),
            Orientation.Horizontal,
            onChanged: static (node, _, _) => node.InvalidateMeasure());

    /// <summary>
    /// Identifies the <see cref="ItemSpacing"/> property.
    /// </summary>
    public static readonly Property<double> ItemSpacingProperty =
        Property.Register<WrapPanel, double>(
            nameof(ItemSpacing),
            onChanged: static (node, _, _) => node.InvalidateMeasure(),
            validate: IsFiniteNonNegative,
            validationMessage: "ItemSpacing must be a finite non-negative value.");

    /// <summary>
    /// Identifies the <see cref="LineSpacing"/> property.
    /// </summary>
    public static readonly Property<double> LineSpacingProperty =
        Property.Register<WrapPanel, double>(
            nameof(LineSpacing),
            onChanged: static (node, _, _) => node.InvalidateMeasure(),
            validate: IsFiniteNonNegative,
            validationMessage: "LineSpacing must be a finite non-negative value.");

    static WrapPanel()
    {
        UiCssRegistry.RegisterElement<WrapPanel>("WrapPanel");
        UiCssRegistry.RegisterProperty<WrapPanel, Orientation>(
            "orientation",
            OrientationProperty,
            UiCssValueConverters.ParseOrientation);
        UiCssRegistry.RegisterProperty<WrapPanel, double>(
            "item-spacing",
            ItemSpacingProperty,
            UiCssValueConverters.ParseLength);
        UiCssRegistry.RegisterProperty<WrapPanel, double>(
            "line-spacing",
            LineSpacingProperty,
            UiCssValueConverters.ParseLength);
    }

    /// <summary>
    /// Gets or sets the axis along which children are arranged before wrapping.
    /// </summary>
    /// <remarks>
    /// Horizontal orientation wraps onto rows below. Vertical orientation wraps onto columns to the right.
    /// The default is horizontal.
    /// </remarks>
    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>
    /// Gets or sets the finite non-negative spacing between participating children within a row or column.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the value is not finite and non-negative.</exception>
    public double ItemSpacing
    {
        get => GetValue(ItemSpacingProperty);
        set => SetValue(ItemSpacingProperty, value);
    }

    /// <summary>
    /// Gets or sets the finite non-negative spacing between rows or columns.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the value is not finite and non-negative.</exception>
    public double LineSpacing
    {
        get => GetValue(LineSpacingProperty);
        set => SetValue(LineSpacingProperty, value);
    }

    /// <inheritdoc />
    protected override Size MeasureContent(Size availableSize)
    {
        var (orientation, itemSpacing, lineSpacing) = GetLayoutProperties();
        foreach (var child in Children)
            child.Measure(availableSize);

        var lines = BuildLines(GetMain(availableSize, orientation), orientation, itemSpacing);
        var desiredMain = 0d;
        var desiredCross = 0d;
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            desiredMain = Math.Max(desiredMain, line.Main);
            if (index != 0)
            {
                desiredCross = AddFinite(
                    desiredCross,
                    lineSpacing,
                    "WrapPanel measurement overflowed the cross axis.");
            }

            desiredCross = AddFinite(
                desiredCross,
                line.Cross,
                "WrapPanel measurement overflowed the cross axis.");
        }

        return CreateSize(orientation, desiredMain, desiredCross);
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Rect contentBounds)
    {
        var (orientation, itemSpacing, lineSpacing) = GetLayoutProperties();
        var lines = BuildLines(GetMain(contentBounds.Size, orientation), orientation, itemSpacing);
        var mainOrigin = GetMainOrigin(contentBounds, orientation);
        var mainOffset = 0d;
        var crossCursor = GetCrossOrigin(contentBounds, orientation);
        var lineIndex = 0;
        var participantCount = 0;

        foreach (var child in Children)
        {
            if (child.Visibility == Visibility.Collapsed)
            {
                child.Arrange(Rect.Zero);
                continue;
            }

            var line = lines[lineIndex];
            if (participantCount == line.Count)
            {
                crossCursor = AddFinite(
                    crossCursor,
                    line.Cross,
                    "WrapPanel arrangement produced a non-finite child slot origin.");
                crossCursor = AddFinite(
                    crossCursor,
                    lineSpacing,
                    "WrapPanel arrangement produced a non-finite child slot origin.");
                mainOffset = 0;
                participantCount = 0;
                line = lines[++lineIndex];
            }

            if (participantCount != 0)
                mainOffset += itemSpacing;

            var mainCursor = AddFinite(
                mainOrigin,
                mainOffset,
                "WrapPanel arrangement produced a non-finite child slot origin.");
            var childMain = GetMain(child.DesiredSize, orientation);
            child.Arrange(CreateRect(orientation, mainCursor, crossCursor, childMain, line.Cross));
            mainOffset += childMain;
            participantCount++;
        }
    }

    private List<Line> BuildLines(double availableMain, Orientation orientation, double itemSpacing)
    {
        var lines = new List<Line>();
        var count = 0;
        var main = 0d;
        var cross = 0d;

        foreach (var child in Children)
        {
            if (child.Visibility == Visibility.Collapsed)
                continue;

            var childMain = GetMain(child.DesiredSize, orientation);
            var nextMain = count == 0 ? childMain : main + itemSpacing + childMain;

            if (count != 0 && (ExceedsConstraint(main, availableMain)
                || ExceedsConstraint(nextMain, availableMain)))
            {
                lines.Add(new Line(count, main, cross));
                count = 0;
                nextMain = childMain;
                cross = 0;
            }

            if (!double.IsFinite(nextMain))
                throw new InvalidOperationException("WrapPanel line size overflowed the main axis.");

            main = nextMain;
            cross = Math.Max(cross, GetCross(child.DesiredSize, orientation));
            count++;
        }

        if (count != 0)
            lines.Add(new Line(count, main, cross));

        return lines;
    }

    private (Orientation Orientation, double ItemSpacing, double LineSpacing) GetLayoutProperties()
    {
        var orientation = Orientation;
        var itemSpacing = ItemSpacing;
        var lineSpacing = LineSpacing;

        var screen = Screen;
        if (screen?.UseLayoutRounding ?? true)
        {
            var scale = screen?.Scale ?? 1;
            itemSpacing = UiLayoutHelper.RoundLayoutValue(itemSpacing, scale);
            lineSpacing = UiLayoutHelper.RoundLayoutValue(lineSpacing, scale);
        }

        return (orientation, itemSpacing, lineSpacing);
    }

    private static double GetMain(Size size, Orientation orientation) =>
        orientation == Orientation.Vertical ? size.Height : size.Width;

    private static double GetCross(Size size, Orientation orientation) =>
        orientation == Orientation.Vertical ? size.Width : size.Height;

    private static double GetMainOrigin(Rect rect, Orientation orientation) =>
        orientation == Orientation.Vertical ? rect.Y : rect.X;

    private static double GetCrossOrigin(Rect rect, Orientation orientation) =>
        orientation == Orientation.Vertical ? rect.X : rect.Y;

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

    private static bool ExceedsConstraint(double value, double constraint) =>
        value - constraint > LayoutTolerance;

    private readonly record struct Line(int Count, double Main, double Cross);
}
