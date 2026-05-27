using PanguEngine.Input;

namespace PanguEngine.Windowing;

/// <summary>
/// Provides data for keyboard events.
/// </summary>
/// <param name="Key">The key that triggered the event.</param>
/// <param name="Action">The action performed on the key.</param>
/// <param name="Modifiers">The modifier keys held during the event.</param>
public record KeyEventArgs(Key Key, KeyAction Action, KeyModifiers Modifiers);