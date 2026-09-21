using Silk.NET.Maths;

namespace PanguEngine.Input;

/// <summary>
/// Defines a named physical binding within an input action.
/// </summary>
public sealed class InputBinding
{
    private InputBinding(
        string name,
        InputContext context,
        InputSource source,
        KeyModifiers modifiers,
        Vector3D<double> scale,
        int priority)
    {
        Name = name;
        Context = context;
        Source = source;
        Modifiers = modifiers;
        Scale = scale;
        Priority = priority;
    }

    /// <summary>The stable name of this binding within its action.</summary>
    public string Name { get; }

    /// <summary>The context in which this binding is active.</summary>
    public InputContext Context { get; }

    /// <summary>The physical source.</summary>
    public InputSource Source { get; }

    /// <summary>The exact modifier set, or none to match any modifier set.</summary>
    public KeyModifiers Modifiers { get; }

    /// <summary>The source value scale.</summary>
    public Vector3D<double> Scale { get; }

    /// <summary>The priority within the context.</summary>
    public int Priority { get; }

    /// <summary>Creates a button binding for a keyboard key.</summary>
    public static InputBinding Button(
        string name,
        InputContext context,
        Key key,
        KeyModifiers modifiers = KeyModifiers.None,
        int priority = 0) =>
        Create(name, context, InputSource.FromKey(key), modifiers, new Vector3D<double>(1, 0, 0), priority);

    /// <summary>Creates a button binding for a mouse button.</summary>
    public static InputBinding Button(
        string name,
        InputContext context,
        MouseButton button,
        KeyModifiers modifiers = KeyModifiers.None,
        int priority = 0) =>
        Create(name, context, InputSource.FromMouseButton(button), modifiers, new Vector3D<double>(1, 0, 0), priority);

    /// <summary>Creates a one-dimensional axis binding for a keyboard key.</summary>
    public static InputBinding Axis1D(
        string name,
        InputContext context,
        Key key,
        double value,
        KeyModifiers modifiers = KeyModifiers.None,
        int priority = 0) =>
        Create(name, context, InputSource.FromKey(key), modifiers, new Vector3D<double>(value, 0, 0), priority);

    /// <summary>Creates a one-dimensional axis binding for a mouse button.</summary>
    public static InputBinding Axis1D(
        string name,
        InputContext context,
        MouseButton button,
        double value,
        KeyModifiers modifiers = KeyModifiers.None,
        int priority = 0) =>
        Create(name, context, InputSource.FromMouseButton(button), modifiers, new Vector3D<double>(value, 0, 0), priority);

    /// <summary>Creates a one-dimensional axis binding for a transient sample source.</summary>
    public static InputBinding Axis1D(
        string name,
        InputContext context,
        InputSource source,
        Vector2D<double> scale,
        KeyModifiers modifiers = KeyModifiers.None,
        int priority = 0) =>
        Create(name, context, source, modifiers, new Vector3D<double>(scale.X, scale.Y, 0), priority);

    /// <summary>Creates a two-dimensional axis binding for a keyboard key.</summary>
    public static InputBinding Axis2D(
        string name,
        InputContext context,
        Key key,
        Vector2D<double> value,
        KeyModifiers modifiers = KeyModifiers.None,
        int priority = 0) =>
        Create(name, context, InputSource.FromKey(key), modifiers, new Vector3D<double>(value.X, value.Y, 0), priority);

    /// <summary>Creates a two-dimensional axis binding for a mouse button.</summary>
    public static InputBinding Axis2D(
        string name,
        InputContext context,
        MouseButton button,
        Vector2D<double> value,
        KeyModifiers modifiers = KeyModifiers.None,
        int priority = 0) =>
        Create(
            name,
            context,
            InputSource.FromMouseButton(button),
            modifiers,
            new Vector3D<double>(value.X, value.Y, 0),
            priority);

    /// <summary>Creates a two-dimensional axis binding for a transient sample source.</summary>
    public static InputBinding Axis2D(
        string name,
        InputContext context,
        InputSource source,
        Vector2D<double> scale,
        KeyModifiers modifiers = KeyModifiers.None,
        int priority = 0) =>
        Create(name, context, source, modifiers, new Vector3D<double>(scale.X, scale.Y, 0), priority);

    /// <summary>Creates a three-dimensional axis binding for a keyboard key.</summary>
    public static InputBinding Axis3D(
        string name,
        InputContext context,
        Key key,
        Vector3D<double> value,
        KeyModifiers modifiers = KeyModifiers.None,
        int priority = 0) =>
        Create(name, context, InputSource.FromKey(key), modifiers, value, priority);

    /// <summary>Creates a three-dimensional axis binding for a mouse button.</summary>
    public static InputBinding Axis3D(
        string name,
        InputContext context,
        MouseButton button,
        Vector3D<double> value,
        KeyModifiers modifiers = KeyModifiers.None,
        int priority = 0) =>
        Create(name, context, InputSource.FromMouseButton(button), modifiers, value, priority);

    private static InputBinding Create(
        string name,
        InputContext context,
        InputSource source,
        KeyModifiers modifiers,
        Vector3D<double> scale,
        int priority) =>
        new(name, context, source, modifiers, scale, priority);
}
