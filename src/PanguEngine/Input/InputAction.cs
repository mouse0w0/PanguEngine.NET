namespace PanguEngine.Input;

/// <summary>
/// Describes the value produced by an input action.
/// </summary>
public enum InputValueType
{
    /// <summary>A digital pressed state.</summary>
    Button,

    /// <summary>A one-dimensional value.</summary>
    Axis1D,

    /// <summary>A two-dimensional value.</summary>
    Axis2D,

    /// <summary>A three-dimensional value.</summary>
    Axis3D
}

/// <summary>
/// Describes a state transition produced by an input action.
/// </summary>
public enum InputActionPhase
{
    /// <summary>The action changed from zero to a non-zero value.</summary>
    Started,

    /// <summary>The action changed while remaining active, or produced a transient sample.</summary>
    Updated,

    /// <summary>The action returned to zero or was cleared.</summary>
    Stopped
}

/// <summary>
/// Defines a logical input action.
/// </summary>
public sealed class InputAction
{
    /// <summary>
    /// Creates an input action with the specified value type.
    /// </summary>
    /// <param name="valueType">The value type produced by the action.</param>
    public InputAction(InputValueType valueType) : this(valueType, [])
    {
    }

    /// <summary>
    /// Creates an input action with the specified value type and bindings.
    /// </summary>
    /// <param name="valueType">The value type produced by the action.</param>
    /// <param name="bindings">The physical binding definitions.</param>
    public InputAction(InputValueType valueType, IEnumerable<InputBinding> bindings)
    {
        ValueType = valueType;
        Bindings = Array.AsReadOnly(bindings.ToArray());
    }

    /// <summary>The value type produced by this action.</summary>
    public InputValueType ValueType { get; }

    /// <summary>The immutable physical binding definitions.</summary>
    public IReadOnlyList<InputBinding> Bindings { get; }
}
