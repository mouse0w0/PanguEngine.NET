namespace PanguEngine.Client.UI;

/// <summary>
/// Describes a registered UI property independently of its value type.
/// </summary>
public abstract class UiProperty
{
    private static readonly Lock RegistryLock = new();
    private static readonly Dictionary<(Type OwnerType, string Name), UiProperty> Registry = [];

    private protected UiProperty(
        string name,
        Type ownerType,
        Type targetType,
        Type valueType,
        object? defaultValue,
        UiPropertyInvalidation invalidation,
        bool isReadOnly)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (name.Length == 0 || string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A UI property name cannot be empty or whitespace.", nameof(name));

        ArgumentNullException.ThrowIfNull(ownerType);
        ArgumentNullException.ThrowIfNull(targetType);
        ArgumentNullException.ThrowIfNull(valueType);

        Name = name;
        OwnerType = ownerType;
        TargetType = targetType;
        ValueType = valueType;
        DefaultValue = defaultValue;
        Invalidation = invalidation;
        IsReadOnly = isReadOnly;

        lock (RegistryLock)
        {
            if (!Registry.TryAdd((ownerType, name), this))
                throw new InvalidOperationException(
                    $"A UI property named '{name}' is already registered for owner '{ownerType}'.");
        }
    }

    /// <summary>Gets the registered property name.</summary>
    public string Name { get; }

    /// <summary>Gets the exact type that owns the registration.</summary>
    public Type OwnerType { get; }

    /// <summary>Gets the node type on which the property can be stored.</summary>
    public Type TargetType { get; }

    /// <summary>Gets the registered value type.</summary>
    public Type ValueType { get; }

    /// <summary>Gets the value used when a node has no local value.</summary>
    public object? DefaultValue { get; }

    /// <summary>Gets the kinds of UI work that the property may invalidate.</summary>
    public UiPropertyInvalidation Invalidation { get; }

    /// <summary>Gets whether the property can only be written through its registration key.</summary>
    public bool IsReadOnly { get; }

    /// <summary>
    /// Registers a strongly typed property for an owner node type.
    /// </summary>
    /// <typeparam name="TOwner">The node type that owns the property.</typeparam>
    /// <typeparam name="TValue">The property value type.</typeparam>
    /// <param name="name">The unique property name for the owner type.</param>
    /// <param name="defaultValue">The value used when no local value exists.</param>
    /// <param name="invalidation">The UI work that the property may invalidate.</param>
    /// <returns>The registered property descriptor.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the owner/name pair is already registered.</exception>
    public static UiProperty<TValue> Register<TOwner, TValue>(
        string name,
        TValue defaultValue = default!,
        UiPropertyInvalidation invalidation = UiPropertyInvalidation.None)
        where TOwner : UiNode =>
        new(name, typeof(TOwner), typeof(TOwner), defaultValue, invalidation, isReadOnly: false);

    /// <summary>
    /// Registers a strongly typed read-only property for an owner node type.
    /// </summary>
    /// <typeparam name="TOwner">The node type that owns the property.</typeparam>
    /// <typeparam name="TValue">The property value type.</typeparam>
    /// <param name="name">The unique property name for the owner type.</param>
    /// <param name="defaultValue">The value used when no local value exists.</param>
    /// <param name="invalidation">The UI work that the property may invalidate.</param>
    /// <returns>The key that grants owner access to the registered property.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the owner/name pair is already registered.</exception>
    public static UiPropertyKey<TValue> RegisterReadOnly<TOwner, TValue>(
        string name,
        TValue defaultValue = default!,
        UiPropertyInvalidation invalidation = UiPropertyInvalidation.None)
        where TOwner : UiNode
    {
        var property = new UiProperty<TValue>(
            name,
            typeof(TOwner),
            typeof(TOwner),
            defaultValue,
            invalidation,
            isReadOnly: true);
        return new UiPropertyKey<TValue>(property);
    }

    /// <summary>
    /// Registers a strongly typed attached property for a target node type.
    /// </summary>
    /// <typeparam name="TOwner">The node type that defines the property.</typeparam>
    /// <typeparam name="TTarget">The node type on which the property can be stored.</typeparam>
    /// <typeparam name="TValue">The property value type.</typeparam>
    /// <param name="name">The unique property name for the owner type.</param>
    /// <param name="defaultValue">The value used when no local value exists.</param>
    /// <param name="invalidation">The UI work that the property may invalidate.</param>
    /// <returns>The registered property descriptor.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the owner/name pair is already registered.</exception>
    public static UiProperty<TValue> RegisterAttached<TOwner, TTarget, TValue>(
        string name,
        TValue defaultValue = default!,
        UiPropertyInvalidation invalidation = UiPropertyInvalidation.None)
        where TOwner : UiNode
        where TTarget : UiNode =>
        new(name, typeof(TOwner), typeof(TTarget), defaultValue, invalidation, isReadOnly: false);

    internal bool IsOwnedBy(UiNode node) =>
        TargetType.IsInstanceOfType(node);

    internal void VerifyOwner(UiNode node)
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
}

/// <summary>
/// Describes a strongly typed registered UI property.
/// </summary>
/// <typeparam name="T">The property value type.</typeparam>
public sealed class UiProperty<T> : UiProperty
{
    internal UiProperty(
        string name,
        Type ownerType,
        Type targetType,
        T defaultValue,
        UiPropertyInvalidation invalidation,
        bool isReadOnly)
        : base(name, ownerType, targetType, typeof(T), defaultValue, invalidation, isReadOnly)
    {
        DefaultValue = defaultValue;
    }

    /// <summary>Gets the strongly typed default value.</summary>
    public new T DefaultValue { get; }
}
