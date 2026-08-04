using PanguEngine.Input;

namespace PanguEngine.Client.UI;

/// <summary>
/// Provides data for a routed UI keyboard event.
/// </summary>
public sealed class UiKeyEventArgs : UiInputEventArgs
{
    internal UiKeyEventArgs(
        UiNode source,
        Key key,
        KeyModifiers modifiers)
        : base(source)
    {
        Key = key;
        Modifiers = modifiers;
    }

    /// <summary>
    /// Gets the key associated with the event.
    /// </summary>
    public Key Key { get; }

    /// <summary>
    /// Gets the modifier keys associated with the event.
    /// </summary>
    public KeyModifiers Modifiers { get; }
}
