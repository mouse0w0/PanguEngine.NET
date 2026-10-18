namespace PanguEngine.UI.Input;

/// <summary>
/// Provides common data for a routed UI input event.
/// </summary>
public class InputEventArgs : EventArgs
{
    internal InputEventArgs(Node source)
    {
        Source = source;
    }

    /// <summary>
    /// Gets the original deepest node that received the event.
    /// </summary>
    public Node Source { get; }

    /// <summary>
    /// Gets or sets whether the current routed event has been handled.
    /// </summary>
    public bool Handled { get; set; }
}
