using PanguEngine.Input;

namespace PanguEngine.UI.Input;

/// <summary>
/// Provides data for a routed UI pointer button event.
/// </summary>
public sealed class PointerButtonEventArgs : PointerEventArgs
{
    internal PointerButtonEventArgs(
        Node source,
        Point screenPosition,
        MouseButton button,
        KeyModifiers modifiers,
        IReadOnlyList<HitPathEntry> path)
        : base(source, screenPosition, path)
    {
        Button = button;
        Modifiers = modifiers;
    }

    /// <summary>
    /// Gets the mouse button associated with the event.
    /// </summary>
    public MouseButton Button { get; }

    /// <summary>
    /// Gets the modifier keys active when the button event occurred.
    /// </summary>
    public KeyModifiers Modifiers { get; }
}
