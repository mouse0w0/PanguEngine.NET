namespace PanguEngine.Client.UI;

/// <summary>
/// Provides data for a routed UI pointer event.
/// </summary>
public class UiPointerEventArgs : UiInputEventArgs
{
    private readonly IReadOnlyList<UiHitPathEntry> _path;

    internal UiPointerEventArgs(
        UiNode source,
        Point screenPosition,
        IReadOnlyList<UiHitPathEntry> path)
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
    /// Gets the pointer position relative to a node.
    /// </summary>
    /// <param name="relativeTo">The node whose local coordinates are requested.</param>
    /// <returns>The event position in the node's local coordinates.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="relativeTo"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the node is not in the event's root tree.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when an active node outside the event path is queried from the wrong thread.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// Thrown when an active node outside the event path belongs to a shut down dispatcher.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when coordinate accumulation produces a non-finite value.
    /// </exception>
    public Point GetPosition(UiNode relativeTo)
    {
        ArgumentNullException.ThrowIfNull(relativeTo);
        foreach (var entry in _path)
        {
            if (ReferenceEquals(entry.Node, relativeTo))
                return entry.LocalPosition;
        }

        relativeTo.ActiveDispatcher?.VerifyAccess();
        var eventRoot = _path[0].Node;
        var ancestors = new List<UiNode>();
        for (UiNode? current = relativeTo; current is not null; current = current.Parent)
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
