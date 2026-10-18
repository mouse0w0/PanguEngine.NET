namespace PanguEngine.Client.UI.Input;

/// <summary>
/// Provides data for a UI focus transition.
/// </summary>
public sealed class FocusChangedEventArgs : EventArgs
{
    internal FocusChangedEventArgs(Node? oldFocus, Node? newFocus)
    {
        OldFocus = oldFocus;
        NewFocus = newFocus;
    }

    /// <summary>
    /// Gets the node that lost focus, if any.
    /// </summary>
    public Node? OldFocus { get; }

    /// <summary>
    /// Gets the node that received focus, if any.
    /// </summary>
    public Node? NewFocus { get; }
}
