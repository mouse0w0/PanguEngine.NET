namespace PanguEngine.Client.UI;

/// <summary>
/// Provides the base property host for retained-mode UI nodes.
/// </summary>
public abstract partial class UiNode
{
    private Dictionary<UiProperty, object?>? _localValues;

    /// <summary>
    /// Initializes a UI node.
    /// </summary>
    protected UiNode()
    {
    }

    /// <summary>
    /// Occurs when an effective property value changes.
    /// </summary>
    public event EventHandler<UiPropertyChangedEventArgs>? PropertyChanged;

    /// <summary>
    /// Gets the effective value of a registered property.
    /// </summary>
    /// <typeparam name="T">The property value type.</typeparam>
    /// <param name="property">The property descriptor.</param>
    /// <returns>The local value or the descriptor default value.</returns>
    public T GetValue<T>(UiProperty<T> property)
    {
        ArgumentNullException.ThrowIfNull(property);
        property.VerifyOwner(this);
        return GetValueCore(property);
    }

    /// <summary>
    /// Sets a local value for a registered property.
    /// </summary>
    /// <typeparam name="T">The property value type.</typeparam>
    /// <param name="property">The property descriptor.</param>
    /// <param name="value">The new local value.</param>
    /// <remarks>
    /// A one-way binding rejects direct assignment. A two-way binding writes a changed value back to its source.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the property is the target of a one-way binding.
    /// </exception>
    public void SetValue<T>(UiProperty<T> property, T value)
    {
        ArgumentNullException.ThrowIfNull(property);
        property.VerifyOwner(this);

        if (TryGetBinding(property, out var binding))
        {
            if (!binding.IsTwoWay)
                throw new InvalidOperationException($"Property '{property.Name}' has a one-way binding.");

            VerifyLayoutPropertyAccess(property);
            if (EqualityComparer<T>.Default.Equals(GetValueCore(property), value))
                return;

            SetValueCore(property, value);
            if (IsCurrentBinding(property, binding))
                binding.UpdateSource(value);
            return;
        }

        SetValueCore(property, value);
    }

    /// <summary>
    /// Clears a local value and restores the descriptor default value.
    /// </summary>
    /// <typeparam name="T">The property value type.</typeparam>
    /// <param name="property">The property descriptor.</param>
    /// <remarks>
    /// Clearing a bound property removes its binding and restores the descriptor default value.
    /// Use <see cref="Unbind{T}(UiProperty{T})"/> to remove a binding while preserving its current value.
    /// </remarks>
    public void ClearValue<T>(UiProperty<T> property)
    {
        ArgumentNullException.ThrowIfNull(property);
        property.VerifyOwner(this);
        VerifyLayoutPropertyAccess(property);

        if (TryGetBinding(property, out var binding))
        {
            binding.Detach();
            RemoveBinding(property, binding);
        }

        ClearValueCore(property);
    }

    /// <summary>
    /// Raises a property change through the node notification pipeline.
    /// </summary>
    /// <param name="eventArgs">The property change data.</param>
    protected virtual void OnPropertyChanged(UiPropertyChangedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);
        ApplyPropertyInvalidation(eventArgs.Property);

        var globalHandler = PropertyChanged;
        var propertySubscriptions = BeginSubscriptionNotification(eventArgs.Property);

        try
        {
            globalHandler?.Invoke(this, eventArgs);
            NotifySubscriptions(eventArgs, propertySubscriptions);
        }
        finally
        {
            EndSubscriptionNotification(propertySubscriptions);
        }
    }

    private T GetValueCore<T>(UiProperty<T> property)
    {
        if (_localValues is null || !_localValues.TryGetValue(property, out var value))
            return property.DefaultValue;

        return value is null ? default! : (T)value;
    }

    private void SetValueCore<T>(UiProperty<T> property, T value)
    {
        VerifyLayoutPropertyAccess(property);
        var oldValue = GetValueCore(property);
        _localValues ??= [];
        _localValues[property] = value;
        if (EqualityComparer<T>.Default.Equals(oldValue, value))
            return;

        OnPropertyChanged(new UiPropertyChangedEventArgs<T>(property, oldValue, value));
    }

    private void ClearValueCore<T>(UiProperty<T> property)
    {
        if (_localValues is null || !_localValues.Remove(property, out var storedValue))
            return;

        var oldValue = storedValue is null ? default! : (T)storedValue;
        var newValue = property.DefaultValue;
        if (EqualityComparer<T>.Default.Equals(oldValue, newValue))
            return;

        OnPropertyChanged(new UiPropertyChangedEventArgs<T>(property, oldValue, newValue));
    }
}
