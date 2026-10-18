using PanguEngine.ComponentModel;

namespace PanguEngine.Client.UI.Styling;

/// <summary>
/// Holds a strongly typed property/value pair as a non-generic, immutable style setter.
/// </summary>
public abstract class StyleSetter
{
    private protected StyleSetter(Property property, object? boxedValue, StyleEdge? component = null)
    {
        ArgumentNullException.ThrowIfNull(property);
        Property = property;
        BoxedValue = boxedValue;
        Component = component;
    }

    /// <summary>Gets the property targeted by this setter.</summary>
    public Property Property { get; }

    /// <summary>Gets the boxed property value.</summary>
    public object? BoxedValue { get; }

    /// <summary>Gets the thickness edge targeted by this setter, or null for the entire property.</summary>
    public StyleEdge? Component { get; }

    /// <summary>Creates an immutable setter for a strongly typed property.</summary>
    /// <typeparam name="T">The property value type.</typeparam>
    /// <param name="property">The property to set.</param>
    /// <param name="value">The value to assign.</param>
    /// <returns>A new immutable setter.</returns>
    public static StyleSetter Create<T>(Property<T> property, T value) => new TypedSetter<T>(property, value);

    /// <summary>Creates an immutable setter for one edge of a thickness property.</summary>
    /// <param name="property">The thickness property to style.</param>
    /// <param name="edge">The edge to style.</param>
    /// <param name="value">The finite non-negative spacing in logical pixels.</param>
    /// <returns>A setter that affects only the specified edge.</returns>
    public static StyleSetter CreateEdge(Property<Thickness> property, StyleEdge edge, double value) =>
        new TypedSetter<Thickness>(property, new Thickness(value), edge);

    internal IEnumerable<StyleSetter> Expand()
    {
        if (Component is not null || Property is not Property<Thickness> property)
        {
            yield return this;
            yield break;
        }

        var value = (Thickness)BoxedValue!;
        yield return new TypedSetter<Thickness>(property, value, StyleEdge.Top);
        yield return new TypedSetter<Thickness>(property, value, StyleEdge.Right);
        yield return new TypedSetter<Thickness>(property, value, StyleEdge.Bottom);
        yield return new TypedSetter<Thickness>(property, value, StyleEdge.Left);
    }

    internal object? Apply(object? currentValue)
    {
        if (Component is null)
            return BoxedValue;

        var current = (Thickness)currentValue!;
        var value = (Thickness)BoxedValue!;
        return Component switch
        {
            StyleEdge.Top => new Thickness(current.Left, value.Top, current.Right, current.Bottom),
            StyleEdge.Right => new Thickness(current.Left, current.Top, value.Right, current.Bottom),
            StyleEdge.Bottom => new Thickness(current.Left, current.Top, current.Right, value.Bottom),
            StyleEdge.Left => new Thickness(value.Left, current.Top, current.Right, current.Bottom),
            _ => throw new InvalidOperationException($"Unsupported style component '{Component}'.")
        };
    }

    private sealed class TypedSetter<T>(Property<T> property, T value, StyleEdge? component = null)
        : StyleSetter(property, value, component)
    {
    }
}
