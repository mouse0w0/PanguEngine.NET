using PanguEngine.Input;

namespace PanguEngine.Client.Input;

/// <summary>
/// Describes whether an input action handler consumed an input.
/// </summary>
public enum InputHandling
{
    /// <summary>Allows the input to continue to lower handlers and contexts.</summary>
    Pass,

    /// <summary>
    /// Stops normal candidate routing to later handlers and contexts without blocking required state maintenance.
    /// </summary>
    Handled
}

/// <summary>
/// Provides data for a routed input action.
/// </summary>
/// <param name="Action">The logical action.</param>
/// <param name="Context">The context that routed the action.</param>
/// <param name="Phase">The action phase.</param>
/// <param name="Value">The current action value.</param>
/// <param name="Source">
/// The physical source that caused the state change, or null for a synthetic topology or binding change.
/// </param>
public readonly record struct InputActionEvent(
    InputAction Action,
    InputContext Context,
    InputActionPhase Phase,
    InputActionValue Value,
    InputSource? Source);
