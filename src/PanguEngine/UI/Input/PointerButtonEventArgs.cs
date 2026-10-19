using PanguEngine.Input;

namespace PanguEngine.UI.Input;

/// <summary>
/// Provides data for a routed UI pointer button event.
/// </summary>
public sealed class PointerButtonEventArgs : PointerEventArgs
{
    internal PointerButtonEventArgs(
        UIElement source,
        Point scenePosition,
        MouseButton button,
        KeyModifiers modifiers,
        IReadOnlyList<HitPathEntry> path)
        : base(source, scenePosition, path)
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