namespace PanguEngine.ComponentModel;

/// <summary>
/// Hosts registered property values, change notifications, subscriptions, and bindings.
/// </summary>
/// <remarks>
/// Derived hosts may provide fallback values and write-access policies. The base host does not
/// resolve UI styles, invalidate UI work, or impose a thread-affinity policy. The type does not
/// implement <see cref="System.ComponentModel.INotifyPropertyChanged"/>; expression bindings
/// continue to require a source that implements that interface.
/// </remarks>
public abstract partial class ObservableObject
{
    private Dictionary<Property, object?>? _localValues;

    /// <summary>
    /// Occurs when an effective property value changes.
    /// </summary>
    public event EventHandler<PropertyChangedEventArgs>? PropertyChanged;

    /// <summary>
    /// Gets the effective value of a registered property.
    /// </summary>
    /// <typeparam name="T">The property value type.</typeparam>
    /// <param name="property">The property descriptor.</param>
    /// <returns>
    /// The local or binding value when present, otherwise the host's fallback value.
    /// </returns>
    public T GetValue<T>(Property<T> property)
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
    /// Thrown when the property is read-only or is the target of a one-way binding.
    /// </exception>
    public void SetValue<T>(Property<T> property, T value)
    {
        ArgumentNullException.ThrowIfNull(property);
        property.VerifyOwner(this);
        property.VerifyWritable();

        if (TryGetBinding(property, out var binding))
        {
            if (!binding.IsTwoWay)
                throw new InvalidOperationException($"Property '{property.Name}' has a one-way binding.");

            VerifyMutationAccess();
            if (EqualityComparer<T>.Default.Equals(GetValueCore(property), value))
                return;

            VerifyMutationAccess();
            SetValueCore(property, value);
            if (IsCurrentBinding(property, binding))
                binding.UpdateSource(value);
            return;
        }

        VerifyMutationAccess();
        SetValueCore(property, value);
    }

    /// <summary>
    /// Clears a local value and restores the host's fallback value.
    /// </summary>
    /// <typeparam name="T">The property value type.</typeparam>
    /// <param name="property">The property descriptor.</param>
    /// <remarks>
    /// Clearing a bound property removes its binding and restores the host's fallback value.
    /// Use <see cref="Unbind{T}(Property{T})"/> to
    /// remove a binding while preserving its current value.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when the property is read-only.</exception>
    public void ClearValue<T>(Property<T> property)
    {
        ArgumentNullException.ThrowIfNull(property);
        property.VerifyOwner(this);
        property.VerifyWritable();
        VerifyMutationAccess();

        if (TryGetBinding(property, out var binding))
        {
            binding.Detach();
            RemoveBinding(property, binding);
        }

        ClearValueCore(property);
    }

    /// <summary>
    /// Sets a local value through a read-only property key valid for this host.
    /// </summary>
    /// <typeparam name="T">The property value type.</typeparam>
    /// <param name="propertyKey">The property key.</param>
    /// <param name="value">The new local value.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="propertyKey"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the property does not target this host.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the host's write-access policy rejects the operation.
    /// </exception>
    protected void SetValue<T>(PropertyKey<T> propertyKey, T value)
    {
        ArgumentNullException.ThrowIfNull(propertyKey);
        var property = propertyKey.Property;
        property.VerifyOwner(this);
        VerifyMutationAccess();
        SetValueCore(property, value);
    }

    /// <summary>
    /// Clears a local value through a read-only property key valid for this host.
    /// </summary>
    /// <typeparam name="T">The property value type.</typeparam>
    /// <param name="propertyKey">The property key.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="propertyKey"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the property does not target this host.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the host's write-access policy rejects the operation.
    /// </exception>
    protected void ClearValue<T>(PropertyKey<T> propertyKey)
    {
        ArgumentNullException.ThrowIfNull(propertyKey);
        var property = propertyKey.Property;
        property.VerifyOwner(this);
        VerifyMutationAccess();
        ClearValueCore(property);
    }

    /// <summary>
    /// Raises host notifications after the property's registered callback has completed.
    /// </summary>
    /// <param name="eventArgs">The property change data.</param>
    protected virtual void OnPropertyChanged(PropertyChangedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);

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

    /// <summary>Publishes a committed effective-value change through the host's notification lifecycle.</summary>
    /// <typeparam name="T">The property value type.</typeparam>
    /// <param name="property">A property valid for this host.</param>
    /// <param name="oldValue">The previous effective value.</param>
    /// <param name="newValue">The committed effective value.</param>
    /// <remarks>
    /// Callers must supply a valid property and a real committed change. The registered callback
    /// precedes host notifications. A failure stops subsequent notifications.
    /// </remarks>
    protected void RaisePropertyChanged<T>(Property<T> property, T oldValue, T newValue)
    {
        var eventArgs = new PropertyChangedEventArgs<T>(property, oldValue, newValue);
        property.RaiseChanged(this, oldValue, newValue);
        OnPropertyChanged(eventArgs);
    }

    /// <summary>Enforces the host's policy before a property write or binding mutation.</summary>
    /// <remarks>The base implementation imposes no access restriction.</remarks>
    protected virtual void VerifyMutationAccess()
    {
    }

    /// <summary>Gets a property's effective value when no local value is stored.</summary>
    /// <typeparam name="T">The property value type.</typeparam>
    /// <param name="property">The property to resolve.</param>
    /// <returns>The descriptor default unless a derived host supplies another fallback.</returns>
    protected virtual T GetFallbackValue<T>(Property<T> property) =>
        property.DefaultValue;

    /// <summary>Determines whether the host stores a local value for a property.</summary>
    /// <param name="property">The property to inspect.</param>
    /// <returns>Whether a local value is stored, including values supplied by a binding.</returns>
    protected bool HasLocalValue(Property property) =>
        _localValues is not null && _localValues.ContainsKey(property);

    internal void RaiseEffectiveValueChanged<T>(Property<T> property, object? oldValue, object? newValue) =>
        RaisePropertyChanged(property,
            oldValue is null ? default! : (T)oldValue,
            newValue is null ? default! : (T)newValue);

    private T GetValueCore<T>(Property<T> property)
    {
        if (_localValues is not null && _localValues.TryGetValue(property, out var local))
            return local is null ? default! : (T)local;
        return GetFallbackValue(property);
    }

    private void SetValueCore<T>(Property<T> property, T value)
    {
        var oldValue = GetValueCore(property);
        _localValues ??= [];
        _localValues[property] = value;
        if (EqualityComparer<T>.Default.Equals(oldValue, value))
            return;

        RaisePropertyChanged(property, oldValue, value);
    }

    private void ClearValueCore<T>(Property<T> property)
    {
        if (_localValues is null || !_localValues.Remove(property, out var storedValue))
            return;

        var oldValue = storedValue is null ? default! : (T)storedValue;
        var newValue = GetValueCore(property);
        if (EqualityComparer<T>.Default.Equals(oldValue, newValue))
            return;

        RaisePropertyChanged(property, oldValue, newValue);
    }
}
