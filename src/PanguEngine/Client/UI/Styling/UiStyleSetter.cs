namespace PanguEngine.Client.UI.Styling;

/// <summary>
/// Holds a strongly typed property/value pair as a non-generic, immutable style setter.
/// </summary>
public abstract class UiStyleSetter
{
    private protected UiStyleSetter(UiProperty property, object? boxedValue, UiStyleEdge? component = null)
    {
        ArgumentNullException.ThrowIfNull(property);
        Property = property;
        BoxedValue = boxedValue;
        Component = component;
    }

    /// <summary>Gets the property targeted by this setter.</summary>
    public UiProperty Property { get; }

    /// <summary>Gets the boxed property value.</summary>
    public object? BoxedValue { get; }

    /// <summary>Gets the thickness edge targeted by this setter, or null for the entire property.</summary>
    public UiStyleEdge? Component { get; }

    /// <summary>Creates an immutable setter for a strongly typed property.</summary>
    /// <typeparam name="T">The property value type.</typeparam>
    /// <param name="property">The property to set.</param>
    /// <param name="value">The value to assign.</param>
    /// <returns>A new immutable setter.</returns>
    public static UiStyleSetter Create<T>(UiProperty<T> property, T value) => new TypedSetter<T>(property, value);

    /// <summary>Creates an immutable setter for one edge of a thickness property.</summary>
    /// <param name="property">The thickness property to style.</param>
    /// <param name="edge">The edge to style.</param>
    /// <param name="value">The finite non-negative spacing in logical pixels.</param>
    /// <returns>A setter that affects only the specified edge.</returns>
    public static UiStyleSetter CreateEdge(UiProperty<Thickness> property, UiStyleEdge edge, double value) =>
        new TypedSetter<Thickness>(property, new Thickness(value), edge);

    internal IEnumerable<UiStyleSetter> Expand()
    {
        if (Component is not null || Property is not UiProperty<Thickness> property)
        {
            yield return this;
            yield break;
        }

        var value = (Thickness)BoxedValue!;
        yield return new TypedSetter<Thickness>(property, value, UiStyleEdge.Top);
        yield return new TypedSetter<Thickness>(property, value, UiStyleEdge.Right);
        yield return new TypedSetter<Thickness>(property, value, UiStyleEdge.Bottom);
        yield return new TypedSetter<Thickness>(property, value, UiStyleEdge.Left);
    }

    internal object? Apply(object? currentValue)
    {
        if (Component is null)
            return BoxedValue;

        var current = (Thickness)currentValue!;
        var value = (Thickness)BoxedValue!;
        return Component switch
        {
            UiStyleEdge.Top => new Thickness(current.Left, value.Top, current.Right, current.Bottom),
            UiStyleEdge.Right => new Thickness(current.Left, current.Top, value.Right, current.Bottom),
            UiStyleEdge.Bottom => new Thickness(current.Left, current.Top, current.Right, value.Bottom),
            UiStyleEdge.Left => new Thickness(value.Left, current.Top, current.Right, current.Bottom),
            _ => throw new InvalidOperationException($"Unsupported style component '{Component}'.")
        };
    }

    private sealed class TypedSetter<T>(UiProperty<T> property, T value, UiStyleEdge? component = null)
        : UiStyleSetter(property, value, component)
    {
    }
}
