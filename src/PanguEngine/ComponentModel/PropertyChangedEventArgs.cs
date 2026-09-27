namespace PanguEngine.ComponentModel;

/// <summary>
/// Provides information about an observable property value change.
/// </summary>
public abstract class PropertyChangedEventArgs : EventArgs
{
    private protected PropertyChangedEventArgs(
        Property property,
        object? oldValue,
        object? newValue)
    {
        Property = property;
        OldValue = oldValue;
        NewValue = newValue;
    }

    /// <summary>Gets the property that changed.</summary>
    public Property Property { get; }

    /// <summary>Gets the previous effective value.</summary>
    public object? OldValue { get; }

    /// <summary>Gets the new effective value.</summary>
    public object? NewValue { get; }
}

/// <summary>
/// Provides strongly typed information about an observable property value change.
/// </summary>
/// <typeparam name="T">The property value type.</typeparam>
public sealed class PropertyChangedEventArgs<T> : PropertyChangedEventArgs
{
    internal PropertyChangedEventArgs(Property<T> property, T oldValue, T newValue)
        : base(property, oldValue, newValue)
    {
        Property = property;
        OldValue = oldValue;
        NewValue = newValue;
    }

    /// <summary>Gets the strongly typed property that changed.</summary>
    public new Property<T> Property { get; }

    /// <summary>Gets the previous strongly typed effective value.</summary>
    public new T OldValue { get; }

    /// <summary>Gets the new strongly typed effective value.</summary>
    public new T NewValue { get; }
}
