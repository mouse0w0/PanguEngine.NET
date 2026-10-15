namespace PanguEngine.ComponentModel;

/// <summary>
/// Hosts registered property values, change notifications, subscriptions, and bindings.
/// </summary>
/// <remarks>
/// Derived hosts may provide inheritance parents and children, and write-access policies. The
/// base host does not resolve UI styles, invalidate UI work, or impose a thread-affinity policy. The
/// type does not implement <see cref="System.ComponentModel.INotifyPropertyChanged"/>; expression
/// bindings continue to require a source that implements that interface.
/// </remarks>
public abstract partial class ObservableObject
{
    private Dictionary<Property, PropertyValueEntry>? _propertyValues;

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
    /// The direct getter value or the highest-priority local, committed style, inherited, or descriptor default value.
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
    /// Changes raised during synchronization continue propagating until the properties stop notifying.
    /// Source writes that produce no notification do not refresh the target.
    /// Ordinary property values are validated before they are committed.
    /// Ordinary assignments establish a local value even when equal to the fallback value.
    /// Ordinary assignments replace the local value even when the values compare equal.
    /// Notifications are raised only when the effective value changes.
    /// Comparison or notification failures preserve the committed local value.
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
        if (property.ValuesEqual(field, value))
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
    /// Clearing a bound property removes its binding and restores the next available value source,
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
    /// precedes host notifications. Inheritable properties then propagate the current effective value
    /// to descendants. Nested changes complete synchronously. A failure stops subsequent notifications.
    /// </remarks>
    protected void RaisePropertyChanged<T>(Property<T> property, T oldValue, T newValue)
    {
        PublishPropertyChange(property, oldValue, newValue);
        if (property.Inherits)
        {
            var currentValue = GetValueCore(property);
            if (!property.ValuesEqual(oldValue, currentValue))
                PropagateInheritedValue(property, oldValue, currentValue);
        }
    }

    private void PublishPropertyChange<T>(Property<T> property, T oldValue, T newValue)
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

    /// <summary>Gets the parent object from which inheritable property values are resolved.</summary>
    /// <remarks>
    /// Hosts update both sides of their parent and child relationships, then call
    /// <see cref="OnInheritanceParentChanged"/> with the previous parent.
    /// </remarks>
    protected virtual ObservableObject? InheritanceParent => null;

    /// <summary>Gets the direct children that inherit property values from this object.</summary>
    /// <remarks>Hosts maintain this collection consistently with each child's inheritance parent.</remarks>
    protected virtual IReadOnlyList<ObservableObject> InheritanceChildren => [];

    /// <summary>Publishes inherited changes after this object's parent relationship changes.</summary>
    /// <param name="oldParent">The previous inheritance parent, or null if there was none.</param>
    /// <remarks>Call this after updating both sides of the parent and child relationships.</remarks>
    protected void OnInheritanceParentChanged(ObservableObject? oldParent)
    {
        var parent = InheritanceParent;
        var changes = new List<(Property Property, object? Old, object? New)>();
        foreach (var property in Property.GetInheritableProperties())
        {
            if (property.IsOwnedBy(this) && HasInheritanceOverride(property))
                continue;
            var oldValue = TryGetInheritedValue(property, oldParent, out var oldInherited)
                ? oldInherited : property.DefaultValue;
            var newValue = TryGetInheritedValue(property, parent, out var newInherited)
                ? newInherited : property.DefaultValue;
            if (!property.AreEqual(oldValue, newValue))
                changes.Add((property, oldValue, newValue));
        }
        foreach (var (property, oldValue, newValue) in changes)
            ReceiveInheritedValueChange(property, oldValue, newValue);
    }

    /// <summary>Determines whether the host stores a local value for a property.</summary>
    /// <param name="property">The property to inspect.</param>
    /// <returns>Whether a local value is stored, including values supplied by a binding.</returns>
    protected bool HasLocalValue(Property property) =>
        TryGetLocalValue(property, out _);

    internal void RaiseEffectiveValueChanged<T>(Property<T> property, object? oldValue, object? newValue) =>
        RaisePropertyChanged(property,
            oldValue is null ? default! : (T)oldValue,
            newValue is null ? default! : (T)newValue);

    internal void RaiseInheritedValueChanged<T>(Property<T> property, object? oldValue, object? newValue)
    {
        PublishPropertyChange(property,
            oldValue is null ? default! : (T)oldValue,
            newValue is null ? default! : (T)newValue);
        PropagateInheritedValue(property, oldValue, newValue);
    }

    internal static bool TryGetInheritedValue(
        Property property, ObservableObject? parent, out object? value)
    {
        var ancestor = FindInheritanceAncestor(parent, property);
        if (ancestor is null)
        {
            value = null;
            return false;
        }
        value = property.GetBoxedValue(ancestor);
        return true;
    }

    private static ObservableObject? FindInheritanceAncestor(ObservableObject? parent, Property property)
    {
        for (var node = parent; node is not null; node = node.InheritanceParent)
        {
            if (property.IsOwnedBy(node))
                return node;
        }
        return null;
    }

    private bool HasInheritanceOverride(Property property) =>
        HasLocalValue(property) || !property.IsReadOnly && TryGetStyleValue(property, out _);

    private void ReceiveInheritedValueChange(Property property, object? oldValue, object? newValue)
    {
        if (!property.IsOwnedBy(this))
        {
            PropagateInheritedValue(property, oldValue, newValue);
            return;
        }
        if (HasInheritanceOverride(property))
            return;
        property.RaiseInheritedValueChanged(this, oldValue, newValue);
    }

    internal void PropagateInheritedValue(Property property, object? oldValue, object? newValue)
    {
        foreach (var child in InheritanceChildren.ToArray())
        {
            if (ReferenceEquals(child.InheritanceParent, this))
                child.ReceiveInheritedValueChange(property, oldValue, newValue);
        }
    }

    private T GetValueCore<T>(Property<T> property)
    {
        if (property is IDirectProperty<T> direct)
            return direct.GetValue(this);
        if (_propertyValues is not null && _propertyValues.TryGetValue(property, out var entry))
        {
            if (entry.HasLocal)
                return entry.LocalValue is null ? default! : (T)entry.LocalValue;
            if (!property.IsReadOnly && entry.HasStyle)
                return entry.StyleValue is null ? default! : (T)entry.StyleValue;
        }
        if (property.Inherits && TryGetInheritedValue(property, InheritanceParent, out var inherited))
            return inherited is null ? default! : (T)inherited;
        return property.DefaultValue;
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
        SetLocalValue(property, value);
        if (!property.ValuesEqual(oldValue, value))
            RaisePropertyChanged(property, oldValue, value);
    }

    private void ClearValueCore<T>(Property<T> property)
    {
        if (property is IDirectProperty<T> direct)
        {
            direct.SetValue(this, property.DefaultValue);
            return;
        }

        if (!TryClearLocalValue(property, out var storedValue))
            return;

        var oldValue = storedValue is null ? default! : (T)storedValue;
        var newValue = GetValueCore(property);
        if (property.ValuesEqual(oldValue, newValue))
            return;

        RaisePropertyChanged(property, oldValue, newValue);
    }

    private PropertyValueEntry GetOrCreateValueEntry(Property property)
    {
        _propertyValues ??= new(ReferenceEqualityComparer.Instance);
        if (!_propertyValues.TryGetValue(property, out var entry))
            _propertyValues.Add(property, entry = new PropertyValueEntry());
        return entry;
    }

    private bool TryGetLocalValue(Property property, out object? value)
    {
        if (_propertyValues is not null && _propertyValues.TryGetValue(property, out var entry) && entry.HasLocal)
        {
            value = entry.LocalValue;
            return true;
        }
        value = null;
        return false;
    }

    private void SetLocalValue(Property property, object? value)
    {
        var entry = GetOrCreateValueEntry(property);
        entry.HasLocal = true;
        entry.LocalValue = value;
    }

    private bool TryClearLocalValue(Property property, out object? value)
    {
        if (_propertyValues is null || !_propertyValues.TryGetValue(property, out var entry) || !entry.HasLocal)
        {
            value = null;
            return false;
        }
        value = entry.LocalValue;
        entry.HasLocal = false;
        entry.LocalValue = null;
        RemoveEmptyValueEntry(property, entry);
        return true;
    }

    internal bool TryGetStyleValue(Property property, out object? value)
    {
        if (_propertyValues is not null && _propertyValues.TryGetValue(property, out var entry) && entry.HasStyle)
        {
            value = entry.StyleValue;
            return true;
        }
        value = null;
        return false;
    }

    internal void CommitStyleValues(
        IReadOnlyDictionary<Property, object?> values, IEnumerable<Property> previousProperties)
    {
        foreach (var property in previousProperties)
        {
            if (!values.ContainsKey(property))
                ClearStyleValue(property);
        }
        foreach (var (property, value) in values)
        {
            var entry = GetOrCreateValueEntry(property);
            entry.HasStyle = true;
            entry.StyleValue = value;
        }
    }

    private void ClearStyleValue(Property property)
    {
        var entry = _propertyValues![property];
        entry.HasStyle = false;
        entry.StyleValue = null;
        RemoveEmptyValueEntry(property, entry);
    }

    private void RemoveEmptyValueEntry(Property property, PropertyValueEntry entry)
    {
        if (entry.HasLocal || entry.HasStyle)
            return;
        var values = _propertyValues!;
        values.Remove(property);
        if (values.Count == 0)
            _propertyValues = null;
    }

    private sealed class PropertyValueEntry
    {
        internal bool HasLocal;
        internal object? LocalValue;
        internal bool HasStyle;
        internal object? StyleValue;
    }
}
