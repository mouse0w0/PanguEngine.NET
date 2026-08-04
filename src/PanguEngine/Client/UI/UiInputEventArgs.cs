namespace PanguEngine.Client.UI;

/// <summary>
/// Provides common data for a routed UI input event.
/// </summary>
public class UiInputEventArgs : EventArgs
{
    internal UiInputEventArgs(UiNode source)
    {
        Source = source;
    }

    /// <summary>
    /// Gets the original deepest node that received the event.
    /// </summary>
    public UiNode Source { get; }

    /// <summary>
    /// Gets or sets whether the current routed event has been handled.
    /// </summary>
    public bool Handled { get; set; }
}
