namespace PanguEngine.Client.UI;

/// <summary>
/// Provides data for a UI focus transition.
/// </summary>
public sealed class UiFocusChangedEventArgs : EventArgs
{
    internal UiFocusChangedEventArgs(UiNode? oldFocus, UiNode? newFocus)
    {
        OldFocus = oldFocus;
        NewFocus = newFocus;
    }

    /// <summary>
    /// Gets the node that lost focus, if any.
    /// </summary>
    public UiNode? OldFocus { get; }

    /// <summary>
    /// Gets the node that received focus, if any.
    /// </summary>
    public UiNode? NewFocus { get; }
}
