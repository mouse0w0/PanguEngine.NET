namespace PanguEngine.Client.UI;

/// <summary>
/// Provides information about a UI property value change.
/// </summary>
public abstract class UiPropertyChangedEventArgs : EventArgs
{
    private protected UiPropertyChangedEventArgs(
        UiProperty property,
        object? oldValue,
        object? newValue)
    {
        Property = property;
        OldValue = oldValue;
        NewValue = newValue;
    }

    /// <summary>Gets the property that changed.</summary>
    public UiProperty Property { get; }

    /// <summary>Gets the previous effective value.</summary>
    public object? OldValue { get; }

    /// <summary>Gets the new effective value.</summary>
    public object? NewValue { get; }
}

/// <summary>
/// Provides strongly typed information about a UI property value change.
/// </summary>
/// <typeparam name="T">The property value type.</typeparam>
public sealed class UiPropertyChangedEventArgs<T> : UiPropertyChangedEventArgs
{
    internal UiPropertyChangedEventArgs(UiProperty<T> property, T oldValue, T newValue)
        : base(property, oldValue, newValue)
    {
        Property = property;
        OldValue = oldValue;
        NewValue = newValue;
    }

    /// <summary>Gets the strongly typed property that changed.</summary>
    public new UiProperty<T> Property { get; }

    /// <summary>Gets the previous strongly typed effective value.</summary>
    public new T OldValue { get; }

    /// <summary>Gets the new strongly typed effective value.</summary>
    public new T NewValue { get; }
}