namespace PanguEngine.UI.Input;

/// <summary>
/// Provides common data for a routed UI input event.
/// </summary>
public class InputEventArgs : EventArgs
{
    internal InputEventArgs(UIElement source)
    {
        Source = source;
    }

    /// <summary>
    /// Gets the original deepest element that received the event.
    /// </summary>
    public UIElement Source { get; }

    /// <summary>
    /// Gets or sets whether the current routed event has been handled.
    /// </summary>
    public bool Handled { get; set; }
}