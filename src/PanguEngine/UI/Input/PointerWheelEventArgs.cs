using PanguEngine.Input;

namespace PanguEngine.UI.Input;

/// <summary>
/// Provides data for a routed UI pointer wheel event.
/// </summary>
public sealed class PointerWheelEventArgs : PointerEventArgs
{
    internal PointerWheelEventArgs(
        Node source,
        Point screenPosition,
        double deltaX,
        double deltaY,
        KeyModifiers modifiers,
        IReadOnlyList<HitPathEntry> path)
        : base(source, screenPosition, path)
    {
        DeltaX = deltaX;
        DeltaY = deltaY;
        Modifiers = modifiers;
    }

    /// <summary>
    /// Gets the horizontal wheel delta.
    /// </summary>
    public double DeltaX { get; }

    /// <summary>
    /// Gets the vertical wheel delta.
    /// </summary>
    public double DeltaY { get; }

    /// <summary>
    /// Gets the modifier keys active for the whole routed wheel event.
    /// </summary>
    public KeyModifiers Modifiers { get; }
}
