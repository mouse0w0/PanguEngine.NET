using Silk.NET.Maths;

namespace PanguEngine.Input;

/// <summary>
/// Stores the value produced by an input action.
/// </summary>
public readonly record struct InputActionValue
{
    private readonly Vector3D<double> _value;

    private InputActionValue(Vector3D<double> value) => _value = value;

    /// <summary>The digital button value.</summary>
    public bool Button => _value.X != 0;

    /// <summary>The one-dimensional value.</summary>
    public double Axis1D => _value.X;

    /// <summary>The two-dimensional value.</summary>
    public Vector2D<double> Axis2D => new(_value.X, _value.Y);

    /// <summary>The three-dimensional value.</summary>
    public Vector3D<double> Axis3D => _value;

    /// <summary>Creates a digital value.</summary>
    /// <param name="value">The digital state.</param>
    /// <returns>The created value.</returns>
    public static InputActionValue FromButton(bool value) =>
        new(new Vector3D<double>(value ? 1 : 0, 0, 0));

    /// <summary>Creates a one-dimensional value.</summary>
    /// <param name="value">The axis value.</param>
    /// <returns>The created value.</returns>
    public static InputActionValue FromAxis1D(double value) =>
        new(new Vector3D<double>(value, 0, 0));

    /// <summary>Creates a two-dimensional value.</summary>
    /// <param name="value">The axis value.</param>
    /// <returns>The created value.</returns>
    public static InputActionValue FromAxis2D(Vector2D<double> value) =>
        new(new Vector3D<double>(value.X, value.Y, 0));

    /// <summary>Creates a three-dimensional value.</summary>
    /// <param name="value">The axis value.</param>
    /// <returns>The created value.</returns>
    public static InputActionValue FromAxis3D(Vector3D<double> value) => new(value);

    /// <summary>The zero input value.</summary>
    public static InputActionValue Zero => default;

    internal bool IsZero => _value == Vector3D<double>.Zero;
}
