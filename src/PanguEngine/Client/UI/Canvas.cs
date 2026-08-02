namespace PanguEngine.Client.UI;

/// <summary>
/// Arranges child nodes at absolute positions within a rectangular content area.
/// </summary>
public sealed class Canvas : Panel
{
    /// <summary>
    /// Identifies the attached horizontal position property.
    /// </summary>
    public static readonly UiProperty<double> LeftProperty =
        UiProperty.RegisterAttached<Canvas, UiNode, double>(
            "Left",
            double.NaN,
            UiPropertyInvalidation.Arrange);

    /// <summary>
    /// Identifies the attached vertical position property.
    /// </summary>
    public static readonly UiProperty<double> TopProperty =
        UiProperty.RegisterAttached<Canvas, UiNode, double>(
            "Top",
            double.NaN,
            UiPropertyInvalidation.Arrange);

    /// <summary>
    /// Identifies the attached horizontal position from the right content edge.
    /// </summary>
    public static readonly UiProperty<double> RightProperty =
        UiProperty.RegisterAttached<Canvas, UiNode, double>(
            "Right",
            double.NaN,
            UiPropertyInvalidation.Arrange);

    /// <summary>
    /// Identifies the attached vertical position from the bottom content edge.
    /// </summary>
    public static readonly UiProperty<double> BottomProperty =
        UiProperty.RegisterAttached<Canvas, UiNode, double>(
            "Bottom",
            double.NaN,
            UiPropertyInvalidation.Arrange);

    /// <summary>
    /// Gets the attached horizontal position of a node.
    /// </summary>
    /// <param name="node">The node whose position to read.</param>
    /// <returns>The horizontal position, or NaN when unspecified.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="node"/> is null.</exception>
    public static double GetLeft(UiNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return node.GetValue(LeftProperty);
    }

    /// <summary>
    /// Sets the attached horizontal position of a node.
    /// </summary>
    /// <param name="node">The node whose position to set.</param>
    /// <param name="value">The horizontal position, or NaN to leave it unspecified.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="node"/> is null.</exception>
    public static void SetLeft(UiNode node, double value)
    {
        ArgumentNullException.ThrowIfNull(node);
        node.SetValue(LeftProperty, value);
    }

    /// <summary>
    /// Gets the attached vertical position of a node.
    /// </summary>
    /// <param name="node">The node whose position to read.</param>
    /// <returns>The vertical position, or NaN when unspecified.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="node"/> is null.</exception>
    public static double GetTop(UiNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return node.GetValue(TopProperty);
    }

    /// <summary>
    /// Sets the attached vertical position of a node.
    /// </summary>
    /// <param name="node">The node whose position to set.</param>
    /// <param name="value">The vertical position, or NaN to leave it unspecified.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="node"/> is null.</exception>
    public static void SetTop(UiNode node, double value)
    {
        ArgumentNullException.ThrowIfNull(node);
        node.SetValue(TopProperty, value);
    }

    /// <summary>
    /// Gets the attached horizontal position from the right content edge.
    /// </summary>
    /// <param name="node">The node whose position to read.</param>
    /// <returns>The horizontal position, or NaN when unspecified.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="node"/> is null.</exception>
    public static double GetRight(UiNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return node.GetValue(RightProperty);
    }

    /// <summary>
    /// Sets the attached horizontal position from the right content edge.
    /// </summary>
    /// <param name="node">The node whose position to set.</param>
    /// <param name="value">The horizontal position, or NaN to leave it unspecified.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="node"/> is null.</exception>
    public static void SetRight(UiNode node, double value)
    {
        ArgumentNullException.ThrowIfNull(node);
        node.SetValue(RightProperty, value);
    }

    /// <summary>
    /// Gets the attached vertical position from the bottom content edge.
    /// </summary>
    /// <param name="node">The node whose position to read.</param>
    /// <returns>The vertical position, or NaN when unspecified.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="node"/> is null.</exception>
    public static double GetBottom(UiNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return node.GetValue(BottomProperty);
    }

    /// <summary>
    /// Sets the attached vertical position from the bottom content edge.
    /// </summary>
    /// <param name="node">The node whose position to set.</param>
    /// <param name="value">The vertical position, or NaN to leave it unspecified.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="node"/> is null.</exception>
    public static void SetBottom(UiNode node, double value)
    {
        ArgumentNullException.ThrowIfNull(node);
        node.SetValue(BottomProperty, value);
    }

    /// <inheritdoc />
    protected override Size MeasureContent(Size availableSize)
    {
        foreach (var child in Children)
            child.Measure(Size.Infinite);

        return Size.Zero;
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Rect contentBounds)
    {
        foreach (var child in Children)
            _ = GetChildSlot(contentBounds, child);

        foreach (var child in Children)
            child.Arrange(GetChildSlot(contentBounds, child));
    }

    private static Rect GetChildSlot(Rect contentBounds, UiNode child)
    {
        var x = ResolveAxis(
            contentBounds.X,
            contentBounds.Width,
            child.DesiredSize.Width,
            GetLeft(child),
            GetRight(child),
            "Left",
            "Right");
        var y = ResolveAxis(
            contentBounds.Y,
            contentBounds.Height,
            child.DesiredSize.Height,
            GetTop(child),
            GetBottom(child),
            "Top",
            "Bottom");
        return new Rect(x, y, child.DesiredSize);
    }

    private static double ResolveAxis(
        double origin,
        double extent,
        double childExtent,
        double leading,
        double trailing,
        string leadingName,
        string trailingName)
    {
        if (!double.IsNaN(leading))
            return ResolveLeadingPosition(origin, leading, leadingName);
        if (double.IsNaN(trailing))
            return origin;
        if (!double.IsFinite(trailing))
            throw new InvalidOperationException($"Canvas {trailingName} must be finite or unspecified.");

        var result = origin + extent - trailing - childExtent;
        if (!double.IsFinite(result))
            throw new InvalidOperationException("Canvas positioning produced a non-finite child slot origin.");

        return result;
    }

    private static double ResolveLeadingPosition(double origin, double value, string propertyName)
    {
        if (!double.IsFinite(value))
            throw new InvalidOperationException($"Canvas {propertyName} must be finite or unspecified.");

        var result = origin + value;
        if (!double.IsFinite(result))
            throw new InvalidOperationException("Canvas positioning produced a non-finite child slot origin.");

        return result;
    }
}
