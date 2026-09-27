namespace PanguEngine.ComponentModel;

/// <summary>
/// Provides owner access to a read-only registered property.
/// </summary>
/// <typeparam name="T">The property value type.</typeparam>
public sealed class PropertyKey<T>
{
    internal PropertyKey(Property<T> property)
    {
        Property = property;
    }

    /// <summary>Gets the read-only property descriptor associated with this key.</summary>
    public Property<T> Property { get; }
}
