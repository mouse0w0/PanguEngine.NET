using System.Runtime.ExceptionServices;

namespace PanguEngine.Client.UI;

/// <summary>
/// Provides the base property host for retained-mode UI nodes.
/// </summary>
/// <remarks>
/// A property that invalidates UI work cannot be modified while the owning screen is generating drawing commands.
/// </remarks>
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
    /// Thrown when the property is read-only or is the target of a one-way binding.
    /// </exception>
    public void SetValue<T>(UiProperty<T> property, T value)
    {
        ArgumentNullException.ThrowIfNull(property);
        property.VerifyOwner(this);
        property.VerifyWritable();

        if (TryGetBinding(property, out var binding))
        {
            if (!binding.IsTwoWay)
                throw new InvalidOperationException($"Property '{property.Name}' has a one-way binding.");

            VerifyPropertyAccess(property);
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
    /// <exception cref="InvalidOperationException">Thrown when the property is read-only.</exception>
    public void ClearValue<T>(UiProperty<T> property)
    {
        ArgumentNullException.ThrowIfNull(property);
        property.VerifyOwner(this);
        property.VerifyWritable();
        VerifyPropertyAccess(property);

        if (TryGetBinding(property, out var binding))
        {
            binding.Detach();
            RemoveBinding(property, binding);
        }

        ClearValueCore(property);
    }

    /// <summary>
    /// Sets a local value through a read-only property key owned by this node type.
    /// </summary>
    /// <typeparam name="T">The property value type.</typeparam>
    /// <param name="propertyKey">The property key.</param>
    /// <param name="value">The new local value.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="propertyKey"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the property does not target this node.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the property invalidates an open screen and is written from the wrong thread.
    /// </exception>
    protected void SetValue<T>(UiPropertyKey<T> propertyKey, T value)
    {
        ArgumentNullException.ThrowIfNull(propertyKey);
        var property = propertyKey.Property;
        property.VerifyOwner(this);
        SetValueCore(property, value);
    }

    /// <summary>
    /// Clears a local value through a read-only property key owned by this node type.
    /// </summary>
    /// <typeparam name="T">The property value type.</typeparam>
    /// <param name="propertyKey">The property key.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="propertyKey"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the property does not target this node.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the property invalidates an open screen and is written from the wrong thread.
    /// </exception>
    protected void ClearValue<T>(UiPropertyKey<T> propertyKey)
    {
        ArgumentNullException.ThrowIfNull(propertyKey);
        var property = propertyKey.Property;
        property.VerifyOwner(this);
        VerifyPropertyAccess(property);
        ClearValueCore(property);
    }

    /// <summary>
    /// Raises a property change through the node notification pipeline.
    /// </summary>
    /// <param name="eventArgs">The property change data.</param>
    protected virtual void OnPropertyChanged(UiPropertyChangedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);
        List<Exception>? errors = null;
        if (ReferenceEquals(eventArgs.Property, IsEnabledProperty) &&
            eventArgs is UiPropertyChangedEventArgs<bool> { NewValue: false })
        {
            errors = [];
            try
            {
                Screen?.CommitAndNotifyInputStateAfterNodeDisabled(this);
            }
            catch (Exception exception)
            {
                AddErrors(errors, exception);
            }
        }

        try
        {
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
        catch (Exception exception) when (errors is not null)
        {
            AddErrors(errors, exception);
        }

        if (errors is null || errors.Count == 0)
            return;
        if (errors.Count == 1)
            ExceptionDispatchInfo.Capture(errors[0]).Throw();
        throw new AggregateException(errors);
    }

    private T GetValueCore<T>(UiProperty<T> property)
    {
        if (_localValues is null || !_localValues.TryGetValue(property, out var value))
            return property.DefaultValue;

        return value is null ? default! : (T)value;
    }

    private void SetValueCore<T>(UiProperty<T> property, T value)
    {
        VerifyPropertyAccess(property);
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

    private static void AddErrors(List<Exception> errors, Exception exception)
    {
        if (exception is AggregateException aggregate)
        {
            foreach (var innerException in aggregate.InnerExceptions)
                AddErrors(errors, innerException);
        }
        else
            errors.Add(exception);
    }
}
