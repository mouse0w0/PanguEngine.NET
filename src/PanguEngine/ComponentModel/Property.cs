namespace PanguEngine.ComponentModel;

/// <summary>
/// Describes a registered observable property independently of its value type.
/// </summary>
public abstract class Property
{
    private static readonly Lock RegistryLock = new();
    private static readonly Dictionary<(Type OwnerType, string Name), Property> Registry = [];

    private protected Property(
        string name,
        Type ownerType,
        Type targetType,
        Type valueType,
        object? defaultValue,
        bool isReadOnly)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (name.Length == 0 || string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A property name cannot be empty or whitespace.", nameof(name));

        ArgumentNullException.ThrowIfNull(ownerType);
        ArgumentNullException.ThrowIfNull(targetType);
        ArgumentNullException.ThrowIfNull(valueType);

        Name = name;
        OwnerType = ownerType;
        TargetType = targetType;
        ValueType = valueType;
        DefaultValue = defaultValue;
        IsReadOnly = isReadOnly;
    }

    /// <summary>Gets the registered property name.</summary>
    public string Name { get; }

    /// <summary>Gets the exact type that owns the registration.</summary>
    public Type OwnerType { get; }

    /// <summary>Gets the host type on which the property can be stored.</summary>
    public Type TargetType { get; }

    /// <summary>Gets the registered value type.</summary>
    public Type ValueType { get; }

    /// <summary>Gets the fallback value, or the reset value for a direct property.</summary>
    public object? DefaultValue { get; }

    /// <summary>Gets whether public writes and bindings targeting this property are prohibited.</summary>
    public bool IsReadOnly { get; }

    /// <summary>Gets whether the property accesses an owner-managed field through delegates.</summary>
    public virtual bool IsDirect => false;

    /// <summary>
    /// Registers a strongly typed property for an owner host type.
    /// </summary>
    /// <typeparam name="TOwner">The observable host type that owns the property.</typeparam>
    /// <typeparam name="TValue">The property value type.</typeparam>
    /// <param name="name">The unique property name for the owner type.</param>
    /// <param name="defaultValue">The value used when no local value exists.</param>
    /// <param name="onChanged">The callback receiving the target host and the old and new effective values before host notifications. Defaults and unchanged effective values do not invoke it.</param>
    /// <param name="validate">The host-independent validator shared by all instances. Returning false rejects the value; callback exceptions propagate.</param>
    /// <param name="validationMessage">The exception message when validation returns false, or null to use the default message.</param>
    /// <param name="comparer">The comparer used to determine equality, or null to compare value types and strings by value and other reference types by identity.</param>
    /// <remarks>
    /// Expose a CLR property whose get accessor calls GetValue with this descriptor and whose set accessor
    /// calls SetValue with this descriptor and the assigned value. Value-change operations belong in onChanged.
    /// The default is validated before registration. Validation failures occur before value commit;
    /// onChanged failures preserve the committed value and skip host notifications.
    /// </remarks>
    /// <returns>The registered property descriptor.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the owner/name pair is already registered.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="defaultValue"/> is rejected by <paramref name="validate"/>.</exception>
    public static Property<TValue> Register<TOwner, TValue>(
        string name,
        TValue defaultValue = default!,
        Action<TOwner, TValue, TValue>? onChanged = null,
        Func<TValue, bool>? validate = null,
        string? validationMessage = null,
        IEqualityComparer<TValue>? comparer = null)
        where TOwner : ObservableObject
    {
        var property = new Property<TValue>(
            name,
            typeof(TOwner),
            typeof(TOwner),
            defaultValue,
            isReadOnly: false,
            onChanged: onChanged is null
                ? null
                : (owner, oldValue, newValue) => onChanged((TOwner)owner, oldValue, newValue),
            validate: validate,
            validationMessage: validationMessage,
            comparer: comparer);
        property.PublishDescriptor();
        return property;
    }

    /// <summary>
    /// Registers a strongly typed read-only property for an owner host type.
    /// </summary>
    /// <typeparam name="TOwner">The observable host type that owns the property.</typeparam>
    /// <typeparam name="TValue">The property value type.</typeparam>
    /// <param name="name">The unique property name for the owner type.</param>
    /// <param name="defaultValue">The value used when no local value exists.</param>
    /// <param name="onChanged">The callback receiving the target host and the old and new effective values before host notifications. Defaults and unchanged effective values do not invoke it.</param>
    /// <param name="validate">The host-independent validator shared by all instances. Returning false rejects the value; callback exceptions propagate.</param>
    /// <param name="validationMessage">The exception message when validation returns false, or null to use the default message.</param>
    /// <param name="comparer">The comparer used to determine equality, or null to compare value types and strings by value and other reference types by identity.</param>
    /// <remarks>The default is validated before registration. Validation failures occur before value commit; onChanged failures preserve the committed value and skip host notifications.</remarks>
    /// <returns>The key that grants owner access to the registered property.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the owner/name pair is already registered.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="defaultValue"/> is rejected by <paramref name="validate"/>.</exception>
    public static PropertyKey<TValue> RegisterReadOnly<TOwner, TValue>(
        string name,
        TValue defaultValue = default!,
        Action<TOwner, TValue, TValue>? onChanged = null,
        Func<TValue, bool>? validate = null,
        string? validationMessage = null,
        IEqualityComparer<TValue>? comparer = null)
        where TOwner : ObservableObject
    {
        var property = new Property<TValue>(
            name,
            typeof(TOwner),
            typeof(TOwner),
            defaultValue,
            isReadOnly: true,
            onChanged: onChanged is null
                ? null
                : (owner, oldValue, newValue) => onChanged((TOwner)owner, oldValue, newValue),
            validate: validate,
            validationMessage: validationMessage,
            comparer: comparer);
        property.PublishDescriptor();
        return new PropertyKey<TValue>(property);
    }

    /// <summary>
    /// Registers a strongly typed attached property for a target host type.
    /// </summary>
    /// <typeparam name="TOwner">The observable host type that defines the property.</typeparam>
    /// <typeparam name="TTarget">The observable host type on which the property can be stored.</typeparam>
    /// <typeparam name="TValue">The property value type.</typeparam>
    /// <param name="name">The unique property name for the owner type.</param>
    /// <param name="defaultValue">The value used when no local value exists.</param>
    /// <param name="onChanged">The callback receiving the target host and the old and new effective values before host notifications.</param>
    /// <param name="validate">The host-independent validator shared by all instances. Returning false rejects the value; callback exceptions propagate.</param>
    /// <param name="validationMessage">The exception message when validation returns false, or null to use the default message.</param>
    /// <param name="comparer">The comparer used to determine equality, or null to compare value types and strings by value and other reference types by identity.</param>
    /// <remarks>
    /// The effective value is committed before <paramref name="onChanged"/> runs; if that callback fails,
    /// the committed value remains and host notifications are skipped. The default is validated before registration;
    /// validation failures happen before value commit.
    /// </remarks>
    /// <returns>The registered property descriptor.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the owner/name pair is already registered.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="defaultValue"/> is rejected by <paramref name="validate"/>.</exception>
    public static Property<TValue> RegisterAttached<TOwner, TTarget, TValue>(
        string name,
        TValue defaultValue = default!,
        Action<TTarget, TValue, TValue>? onChanged = null,
        Func<TValue, bool>? validate = null,
        string? validationMessage = null,
        IEqualityComparer<TValue>? comparer = null)
        where TOwner : ObservableObject
        where TTarget : ObservableObject
    {
        var property = new Property<TValue>(
            name,
            typeof(TOwner),
            typeof(TTarget),
            defaultValue,
            isReadOnly: false,
            onChanged: onChanged is null
                ? null
                : (target, oldValue, newValue) => onChanged((TTarget)target, oldValue, newValue),
            validate: validate,
            validationMessage: validationMessage,
            comparer: comparer);
        property.PublishDescriptor();
        return property;
    }

    /// <summary>Registers a property backed by an owner-managed field.</summary>
    /// <typeparam name="TOwner">The observable host type that owns the property.</typeparam>
    /// <typeparam name="TValue">The property value type.</typeparam>
    /// <param name="name">The unique property name for the owner type.</param>
    /// <param name="getter">The delegate that reads the owner's CLR property, whose get accessor reads its backing field.</param>
    /// <param name="setter">The delegate that assigns the owner's CLR property, whose set accessor calls SetField, or null for a read-only property.</param>
    /// <param name="unsetValue">The value passed to the setter when clearing the property; it does not initialize the field.</param>
    /// <param name="onChanged">The callback receiving the owner and the old and new values before host notifications.</param>
    /// <param name="comparer">The comparer used to determine equality, or null to compare value types and strings by value and other reference types by identity.</param>
    /// <returns>The registered direct property descriptor.</returns>
    /// <remarks>
    /// Getter and setter delegates forward to the owner's CLR property. Its getter reads the backing field,
    /// and its set accessor calls SetField rather than SetValue or a separate SetXxx method.
    /// Normalize or validate values in that CLR set accessor before committing them.
    /// Value-change operations belong in the registered onChanged callback.
    /// Direct properties support notifications and bindings but do not participate in styling or fallback resolution.
    /// A callback failure preserves the committed value and skips subsequent notifications, including binding listeners.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when name or getter is null.</exception>
    /// <exception cref="ArgumentException">Thrown when name is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the owner/name pair is already registered.</exception>
    public static DirectProperty<TOwner, TValue> RegisterDirect<TOwner, TValue>(
        string name,
        Func<TOwner, TValue> getter,
        Action<TOwner, TValue>? setter = null,
        TValue unsetValue = default!,
        Action<TOwner, TValue, TValue>? onChanged = null,
        IEqualityComparer<TValue>? comparer = null)
        where TOwner : ObservableObject
    {
        ArgumentNullException.ThrowIfNull(getter);
        var property = new DirectProperty<TOwner, TValue>(name, getter, setter, unsetValue, onChanged, comparer);
        property.PublishDescriptor();
        return property;
    }

    /// <summary>
    /// Publishes this descriptor.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the owner/name pair is already registered.</exception>
    private protected void PublishDescriptor()
    {
        lock (RegistryLock)
        {
            if (Registry.ContainsKey((OwnerType, Name)))
                throw new InvalidOperationException(
                    $"A property named '{Name}' is already registered for owner '{OwnerType}'.");

            Registry[(OwnerType, Name)] = this;
        }
    }

    internal bool IsOwnedBy(ObservableObject node) =>
        TargetType.IsInstanceOfType(node);

    internal void VerifyOwner(ObservableObject node)
    {
        if (!IsOwnedBy(node))
            throw new ArgumentException(
                $"Property '{Name}' targets '{TargetType}', not '{node.GetType()}'.");
    }

    internal void VerifyWritable()
    {
        if (IsReadOnly)
            throw new InvalidOperationException($"Property '{Name}' is read-only.");
    }

    internal abstract void RaiseEffectiveValueChanged(ObservableObject host, object? oldValue, object? newValue);

    internal abstract bool AreEqual(object? left, object? right);

    internal abstract ArgumentException? GetBoxedValidationError(object? value);
}

/// <summary>
/// Describes a strongly typed registered observable property.
/// </summary>
/// <typeparam name="T">The property value type.</typeparam>
/// <remarks>
/// For ordinary properties, the owner's CLR get accessor calls GetValue with this descriptor,
/// and its set accessor calls SetValue with this descriptor and the assigned value.
/// Value-change operations belong in the registered onChanged callback.
/// </remarks>
/// <example>
/// <code>
/// public static readonly Property&lt;int&gt; ValueProperty =
///     Property.Register&lt;Counter, int&gt;(nameof(Value));
///
/// public int Value
/// {
///     get =&gt; GetValue(ValueProperty);
///     set =&gt; SetValue(ValueProperty, value);
/// }
/// </code>
/// </example>
public class Property<T> : Property
{
    private readonly Action<ObservableObject, T, T>? _onChanged;
    private readonly Func<T, bool>? _validate;
    private readonly string? _validationMessage;
    private readonly IEqualityComparer<T> _comparer;

    internal Property(
        string name,
        Type ownerType,
        Type targetType,
        T defaultValue,
        bool isReadOnly,
        Action<ObservableObject, T, T>? onChanged = null,
        Func<T, bool>? validate = null,
        string? validationMessage = null,
        IEqualityComparer<T>? comparer = null)
        : base(name, ownerType, targetType, typeof(T), defaultValue, isReadOnly)
    {
        DefaultValue = defaultValue;
        _onChanged = onChanged;
        _validate = validate;
        _validationMessage = validationMessage;
        _comparer = comparer ?? (typeof(T).IsValueType || typeof(T) == typeof(string)
            ? EqualityComparer<T>.Default
            : (IEqualityComparer<T>)(object)ReferenceEqualityComparer.Instance);
        ValidateValue(defaultValue, nameof(defaultValue));
    }

    /// <summary>Gets the strongly typed fallback value, or the reset value for a direct property.</summary>
    public new T DefaultValue { get; }

    internal void RaiseChanged(ObservableObject node, T oldValue, T newValue) =>
        _onChanged?.Invoke(node, oldValue, newValue);

    internal override void RaiseEffectiveValueChanged(ObservableObject host, object? oldValue, object? newValue) =>
        host.RaiseEffectiveValueChanged(this, oldValue, newValue);

    internal bool ValuesEqual(T left, T right) => _comparer.Equals(left, right);

    internal override bool AreEqual(object? left, object? right) =>
        ValuesEqual(
            left is null ? default! : (T)left,
            right is null ? default! : (T)right);

    internal void ValidateValue(T value)
    {
        ValidateValue(value, nameof(value));
    }

    private void ValidateValue(T value, string parameterName)
    {
        if (GetValidationError(value, parameterName) is { } error)
            throw error;
    }

    private ArgumentException? GetValidationError(T value, string parameterName) =>
        _validate?.Invoke(value) == false
            ? new ArgumentException(_validationMessage ?? $"Value is not valid for property '{Name}'.", parameterName)
            : null;

    internal override ArgumentException? GetBoxedValidationError(object? value) =>
        GetValidationError((T)value!, nameof(value));
}
