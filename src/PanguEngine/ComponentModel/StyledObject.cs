namespace PanguEngine.ComponentModel;

/// <summary>Hosts observable properties with style values and optional property inheritance.</summary>
/// <remarks>
/// Local values override style values, followed by inherited values and descriptor defaults.
/// Derived hosts provide inheritance relationships and coordinate style resolution separately.
/// Direct properties do not participate in styling or inheritance fallback.
/// </remarks>
public abstract class StyledObject : ObservableObject
{
    private Dictionary<Property, object?>? _styleValues;

    /// <summary>Gets the parent object from which inheritable property values are resolved.</summary>
    /// <remarks>
    /// Hosts update both sides of their parent and child relationships, then call
    /// <see cref="OnInheritanceParentChanged"/> with the previous parent.
    /// </remarks>
    protected virtual StyledObject? InheritanceParent => null;

    /// <summary>Gets the direct children that inherit property values from this object.</summary>
    /// <remarks>Hosts maintain this collection consistently with each child's inheritance parent.</remarks>
    protected virtual IReadOnlyList<StyledObject> InheritanceChildren => [];

    /// <summary>Publishes inherited changes after this object's parent relationship changes.</summary>
    /// <param name="oldParent">The previous inheritance parent, or null if there was none.</param>
    /// <remarks>Call this after updating both sides of the parent and child relationships.</remarks>
    protected void OnInheritanceParentChanged(StyledObject? oldParent)
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

    /// <summary>Gets the style, inherited, or default value of a property without a local value.</summary>
    /// <typeparam name="T">The property value type.</typeparam>
    /// <param name="property">A non-direct property valid for this host.</param>
    /// <returns>The highest-priority fallback value; read-only properties skip style values.</returns>
    protected override T GetFallbackValue<T>(Property<T> property)
    {
        if (!property.IsReadOnly && TryGetStyleValue(property, out var styleValue))
            return styleValue is null ? default! : (T)styleValue;
        if (property.Inherits && TryGetInheritedValue(property, InheritanceParent, out var inherited))
            return inherited is null ? default! : (T)inherited;
        return base.GetFallbackValue(property);
    }

    /// <summary>Publishes a committed change and propagates its current effective value to descendants.</summary>
    /// <typeparam name="T">The property value type.</typeparam>
    /// <param name="property">A property valid for this host.</param>
    /// <param name="oldValue">The previous effective value.</param>
    /// <param name="newValue">The committed effective value.</param>
    /// <remarks>Propagation follows host notifications and applies only to inheritable properties.</remarks>
    protected override void RaisePropertyChanged<T>(Property<T> property, T oldValue, T newValue)
    {
        base.RaisePropertyChanged(property, oldValue, newValue);
        if (property.Inherits)
        {
            var currentValue = GetValueCore(property);
            if (!property.ValuesEqual(oldValue, currentValue))
                PropagateInheritedValue(property, oldValue, currentValue);
        }
    }

    internal void RaiseInheritedValueChanged<T>(Property<T> property, object? oldValue, object? newValue)
    {
        base.RaisePropertyChanged(property,
            oldValue is null ? default! : (T)oldValue,
            newValue is null ? default! : (T)newValue);
        PropagateInheritedValue(property, oldValue, newValue);
    }

    internal static bool TryGetInheritedValue(
        Property property, StyledObject? parent, out object? value)
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

    private static StyledObject? FindInheritanceAncestor(StyledObject? parent, Property property)
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

    private void PropagateInheritedValue(Property property, object? oldValue, object? newValue)
    {
        foreach (var child in InheritanceChildren.ToArray())
        {
            if (ReferenceEquals(child.InheritanceParent, this))
                child.ReceiveInheritedValueChange(property, oldValue, newValue);
        }
    }

    internal bool TryGetStyleValue(Property property, out object? value)
    {
        if (_styleValues is not null && _styleValues.TryGetValue(property, out value))
            return true;
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
            _styleValues ??= new(ReferenceEqualityComparer.Instance);
            _styleValues[property] = value;
        }
    }

    private void ClearStyleValue(Property property)
    {
        var values = _styleValues!;
        values.Remove(property);
        if (values.Count == 0)
            _styleValues = null;
    }
}
