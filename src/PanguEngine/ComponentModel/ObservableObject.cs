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
    /// The direct getter value, or the local or binding value when present, otherwise the host's fallback value.
    /// </returns>
    public T GetValue<T>(Property<T> property)
    {
        ArgumentNullException.ThrowIfNull(property);
        property.VerifyOwner(this);
        return GetValueCore(property);
    }

    /// <summary>
    /// Sets a registered property through its direct setter or local value storage.
    /// </summary>
    /// <typeparam name="T">The property value type.</typeparam>
    /// <param name="property">The property descriptor.</param>
    /// <param name="value">The new property value.</param>
    /// <remarks>
    /// Assignment preserves an existing binding. One-way bindings do not write back; two-way bindings
    /// observe property change notifications and synchronize the current target value to their source.
    /// Ordinary property values are validated before they are committed.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the property is read-only or the host's write-access policy rejects the operation.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when the property validator rejects <paramref name="value"/>.</exception>
    public void SetValue<T>(Property<T> property, T value)
    {
        ArgumentNullException.ThrowIfNull(property);
        property.VerifyOwner(this);
        property.VerifyWritable();

        VerifyMutationAccess();
        SetValueCore(property, value);
    }

    /// <summary>Updates a direct property's backing field and publishes a change when its value differs.</summary>
    /// <typeparam name="TOwner">The observable host type that owns the property.</typeparam>
    /// <typeparam name="TValue">The property value type.</typeparam>
    /// <param name="property">The direct property associated with the field.</param>
    /// <param name="field">The backing field read by the registered getter.</param>
    /// <param name="value">The value to assign.</param>
    /// <returns>True when the field changed; otherwise false.</returns>
    /// <remarks>
    /// Owner code may update read-only direct properties through this method. Writes honor the host's
    /// access policy. Changed values invoke the registered callback and host notifications.
    /// Bindings observe those notifications. Failures preserve the committed field value.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when property is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the property does not target this host.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the host's write-access policy rejects the write.</exception>
    protected bool SetField<TOwner, TValue>(
        DirectProperty<TOwner, TValue> property, ref TValue field, TValue value)
        where TOwner : ObservableObject
    {
        ArgumentNullException.ThrowIfNull(property);
        property.VerifyOwner(this);
        VerifyMutationAccess();
        if (EqualityComparer<TValue>.Default.Equals(field, value))
            return false;

        var oldValue = field;
        field = value;
        RaisePropertyChanged(property, oldValue, value);
        return true;
    }

    /// <summary>
    /// Clears a local value or resets a direct property through its setter.
    /// </summary>
    /// <typeparam name="T">The property value type.</typeparam>
    /// <param name="property">The property descriptor.</param>
    /// <remarks>
    /// Clearing a bound property removes its binding and restores the host's fallback value,
    /// or passes the registered reset value to a direct property's setter.
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
    /// <exception cref="ArgumentException">Thrown when the property does not target this host or its validator rejects <paramref name="value"/>.</exception>
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
    /// <remarks>Overrides must return fallback values accepted by the descriptor's validator.</remarks>
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
        if (property is IDirectProperty<T> direct)
            return direct.GetValue(this);
        if (_localValues is not null && _localValues.TryGetValue(property, out var local))
            return local is null ? default! : (T)local;
        return GetFallbackValue(property);
    }

    private void SetValueCore<T>(Property<T> property, T value)
    {
        if (property is IDirectProperty<T> direct)
        {
            direct.SetValue(this, value);
            return;
        }

        property.ValidateValue(value);
        var oldValue = GetValueCore(property);
        _localValues ??= [];
        _localValues[property] = value;
        if (EqualityComparer<T>.Default.Equals(oldValue, value))
            return;

        RaisePropertyChanged(property, oldValue, value);
    }

    private void ClearValueCore<T>(Property<T> property)
    {
        if (property is IDirectProperty<T> direct)
        {
            direct.SetValue(this, property.DefaultValue);
            return;
        }

        if (_localValues is null || !_localValues.Remove(property, out var storedValue))
            return;

        var oldValue = storedValue is null ? default! : (T)storedValue;
        var newValue = GetValueCore(property);
        if (EqualityComparer<T>.Default.Equals(oldValue, newValue))
            return;

        RaisePropertyChanged(property, oldValue, newValue);
    }
}
