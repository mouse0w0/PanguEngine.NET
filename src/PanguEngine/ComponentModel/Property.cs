namespace PanguEngine.ComponentModel;

/// <summary>
/// Describes a registered observable property independently of its value type.
/// </summary>
public abstract class Property
{
    private static readonly Lock RegistryLock = new();
    private static readonly Dictionary<(Type OwnerType, string Name), Property> Registry = [];
    private static int _registrationOrderCounter;

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

    /// <summary>Gets the zero-based order in which this property was registered.</summary>
    internal int RegistrationOrder { get; private set; }

    /// <summary>
    /// Registers a strongly typed property for an owner host type.
    /// </summary>
    /// <typeparam name="TOwner">The observable host type that owns the property.</typeparam>
    /// <typeparam name="TValue">The property value type.</typeparam>
    /// <param name="name">The unique property name for the owner type.</param>
    /// <param name="defaultValue">The value used when no local value exists.</param>
    /// <param name="onChanged">The callback receiving the target host and the old and new effective values before host notifications. Defaults and unchanged effective values do not invoke it.</param>
    /// <remarks>A callback failure preserves the committed value and skips that change's host notifications.</remarks>
    /// <returns>The registered property descriptor.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the owner/name pair is already registered.</exception>
    public static Property<TValue> Register<TOwner, TValue>(
        string name,
        TValue defaultValue = default!,
        Action<TOwner, TValue, TValue>? onChanged = null)
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
                : (owner, oldValue, newValue) => onChanged((TOwner)owner, oldValue, newValue));
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
    /// <remarks>A callback failure preserves the committed value and skips that change's host notifications.</remarks>
    /// <returns>The key that grants owner access to the registered property.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the owner/name pair is already registered.</exception>
    public static PropertyKey<TValue> RegisterReadOnly<TOwner, TValue>(
        string name,
        TValue defaultValue = default!,
        Action<TOwner, TValue, TValue>? onChanged = null)
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
                : (owner, oldValue, newValue) => onChanged((TOwner)owner, oldValue, newValue));
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
    /// <returns>The registered property descriptor.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the owner/name pair is already registered.</exception>
    public static Property<TValue> RegisterAttached<TOwner, TTarget, TValue>(
        string name,
        TValue defaultValue = default!,
        Action<TTarget, TValue, TValue>? onChanged = null)
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
                : (target, oldValue, newValue) => onChanged((TTarget)target, oldValue, newValue));
        property.PublishDescriptor();
        return property;
    }

    /// <summary>Registers a property backed by an owner-managed field.</summary>
    /// <typeparam name="TOwner">The observable host type that owns the property.</typeparam>
    /// <typeparam name="TValue">The property value type.</typeparam>
    /// <param name="name">The unique property name for the owner type.</param>
    /// <param name="getter">The getter that reads the property's backing field.</param>
    /// <param name="setter">The setter that updates the field through SetField, or null for a read-only property.</param>
    /// <param name="unsetValue">The value passed to the setter when clearing the property; it does not initialize the field.</param>
    /// <param name="onChanged">The callback receiving the owner and the old and new values before host notifications.</param>
    /// <returns>The registered direct property descriptor.</returns>
    /// <remarks>
    /// Setters update their corresponding field through SetField rather than SetValue.
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
        Action<TOwner, TValue, TValue>? onChanged = null)
        where TOwner : ObservableObject
    {
        ArgumentNullException.ThrowIfNull(getter);
        var property = new DirectProperty<TOwner, TValue>(name, getter, setter, unsetValue, onChanged);
        property.PublishDescriptor();
        return property;
    }

    /// <summary>
    /// Publishes this descriptor and assigns its registration order.
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
            RegistrationOrder = _registrationOrderCounter++;
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
}

/// <summary>
/// Describes a strongly typed registered observable property.
/// </summary>
/// <typeparam name="T">The property value type.</typeparam>
public class Property<T> : Property
{
    private readonly Action<ObservableObject, T, T>? _onChanged;

    internal Property(
        string name,
        Type ownerType,
        Type targetType,
        T defaultValue,
        bool isReadOnly,
        Action<ObservableObject, T, T>? onChanged = null)
        : base(name, ownerType, targetType, typeof(T), defaultValue, isReadOnly)
    {
        DefaultValue = defaultValue;
        _onChanged = onChanged;
    }

    /// <summary>Gets the strongly typed fallback value, or the reset value for a direct property.</summary>
    public new T DefaultValue { get; }

    internal void RaiseChanged(ObservableObject node, T oldValue, T newValue) =>
        _onChanged?.Invoke(node, oldValue, newValue);

    internal override void RaiseEffectiveValueChanged(ObservableObject host, object? oldValue, object? newValue) =>
        host.RaiseEffectiveValueChanged(this, oldValue, newValue);

    internal override bool AreEqual(object? left, object? right) =>
        EqualityComparer<T>.Default.Equals(
            left is null ? default! : (T)left,
            right is null ? default! : (T)right);
}
