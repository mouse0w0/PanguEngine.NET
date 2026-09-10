namespace PanguEngine.Client.UI.Input;

/// <summary>
/// Provides data for a routed UI text input event.
/// </summary>
public sealed class UiTextInputEventArgs : UiInputEventArgs
{
    internal UiTextInputEventArgs(UiNode source, string text)
        : base(source)
    {
        Text = text;
    }

    /// <summary>
    /// Gets the committed text associated with the event.
    /// </summary>
    public string Text { get; }
}