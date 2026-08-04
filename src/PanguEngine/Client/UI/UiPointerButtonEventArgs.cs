using PanguEngine.Input;

namespace PanguEngine.Client.UI;

/// <summary>
/// Provides data for a routed UI pointer button event.
/// </summary>
public sealed class UiPointerButtonEventArgs : UiPointerEventArgs
{
    internal UiPointerButtonEventArgs(
        UiNode source,
        Point screenPosition,
        MouseButton button,
        KeyModifiers modifiers,
        IReadOnlyList<UiHitPathEntry> path)
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
