namespace PanguEngine.UI.Input;

/// <summary>
/// Provides data for a routed UI pointer event.
/// </summary>
public class PointerEventArgs : InputEventArgs
{
    private readonly IReadOnlyList<HitPathEntry> _path;

    internal PointerEventArgs(
        UIElement source,
        Point screenPosition,
        IReadOnlyList<HitPathEntry> path)
        : base(source)
    {
        _path = path;
        ScreenPosition = screenPosition;
    }

    /// <summary>
    /// Gets the pointer position in screen coordinates.
    /// </summary>
    public Point ScreenPosition { get; }

    /// <summary>
    /// Gets the pointer position relative to an element.
    /// </summary>
    /// <param name="relativeTo">The element whose local coordinates are requested.</param>
    /// <returns>The event position in the element's local coordinates.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="relativeTo"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the element is not in the event's root tree.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when an active element outside the event path is queried from the wrong thread.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when coordinate accumulation produces a non-finite value.
    /// </exception>
    public Point GetPosition(UIElement relativeTo)
    {
        ArgumentNullException.ThrowIfNull(relativeTo);
        foreach (var entry in _path)
        {
            if (ReferenceEquals(entry.Node, relativeTo))
                return entry.LocalPosition;
        }

        relativeTo.Screen?.VerifyTreeAccess();
        var eventRoot = _path[0].Node;
        var ancestors = new List<UIElement>();
        for (var current = relativeTo; current is not null; current = current.Parent)
        {
            ancestors.Add(current);
            if (ReferenceEquals(current, eventRoot))
                break;
        }

        if (!ReferenceEquals(ancestors[^1], eventRoot))
            throw new ArgumentException("The node does not belong to the event's root tree.", nameof(relativeTo));

        var localPoint = ScreenPosition;
        for (var index = ancestors.Count - 1; index >= 0; index--)
        {
            localPoint = new Point(
                localPoint.X - ancestors[index].LayoutBounds.X,
                localPoint.Y - ancestors[index].LayoutBounds.Y);
        }

        return localPoint;
    }
}