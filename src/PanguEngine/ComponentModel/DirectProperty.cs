namespace PanguEngine.ComponentModel;

/// <summary>Describes a registered property backed by a field on its owner.</summary>
/// <typeparam name="TOwner">The observable host type that owns the property.</typeparam>
/// <typeparam name="TValue">The property value type.</typeparam>
public sealed class DirectProperty<TOwner, TValue> : Property<TValue>, IDirectProperty<TValue>
    where TOwner : ObservableObject
{
    private readonly Func<TOwner, TValue> _getter;
    private readonly Action<TOwner, TValue>? _setter;

    internal DirectProperty(
        string name,
        Func<TOwner, TValue> getter,
        Action<TOwner, TValue>? setter,
        TValue unsetValue,
        Action<TOwner, TValue, TValue>? onChanged)
        : base(name, typeof(TOwner), typeof(TOwner), unsetValue, setter is null,
            onChanged is null ? null : (owner, oldValue, newValue) => onChanged((TOwner)owner, oldValue, newValue))
    {
        _getter = getter;
        _setter = setter;
    }

    /// <inheritdoc />
    public override bool IsDirect => true;

    /// <summary>Gets the value passed to the setter when the property is cleared.</summary>
    public TValue UnsetValue => DefaultValue;

    TValue IDirectProperty<TValue>.GetValue(ObservableObject owner) => _getter((TOwner)owner);

    void IDirectProperty<TValue>.SetValue(ObservableObject owner, TValue value) => _setter!((TOwner)owner, value);
}

internal interface IDirectProperty<T>
{
    T GetValue(ObservableObject owner);
    void SetValue(ObservableObject owner, T value);
}
