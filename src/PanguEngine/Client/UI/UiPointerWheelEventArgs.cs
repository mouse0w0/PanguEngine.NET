namespace PanguEngine.Client.UI;

/// <summary>
/// Provides data for a routed UI pointer wheel event.
/// </summary>
public sealed class UiPointerWheelEventArgs : UiPointerEventArgs
{
    internal UiPointerWheelEventArgs(
        UiNode source,
        Point screenPosition,
        double deltaX,
        double deltaY,
        IReadOnlyList<UiHitPathEntry> path)
        : base(source, screenPosition, path)
    {
        DeltaX = deltaX;
        DeltaY = deltaY;
    }

    /// <summary>
    /// Gets the horizontal wheel delta.
    /// </summary>
    public double DeltaX { get; }

    /// <summary>
    /// Gets the vertical wheel delta.
    /// </summary>
    public double DeltaY { get; }
}
