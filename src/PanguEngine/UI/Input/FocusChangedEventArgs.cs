namespace PanguEngine.UI.Input;

/// <summary>
/// Provides data for a UI focus transition.
/// </summary>
public sealed class FocusChangedEventArgs : EventArgs
{
    internal FocusChangedEventArgs(UIElement? oldFocus, UIElement? newFocus)
    {
        OldFocus = oldFocus;
        NewFocus = newFocus;
    }

    /// <summary>
    /// Gets the element that lost focus, if any.
    /// </summary>
    public UIElement? OldFocus { get; }

    /// <summary>
    /// Gets the element that received focus, if any.
    /// </summary>
    public UIElement? NewFocus { get; }
}