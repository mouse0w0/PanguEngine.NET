using PanguEngine.Registries;

namespace PanguEngine.World.Entities;

/// <summary>
/// Describes a component value type.
/// </summary>
public abstract class ComponentType
{
    private protected ComponentType(Type valueType)
    {
        ValueType = valueType;
    }

    /// <summary>The component value type.</summary>
    public Type ValueType { get; }

    internal abstract IComponentColumn CreateColumn();

    internal void ValidateRegistration(IRegistry<ComponentType> registry)
    {
        foreach (var entry in registry.Entries)
        {
            if (entry.Value.ValueType == ValueType)
                throw new InvalidOperationException($"Component value type '{ValueType}' is already registered.");
        }
    }
}

/// <summary>
/// Describes an unmanaged component value type.
/// </summary>
/// <typeparam name="T">The component value type.</typeparam>
public sealed class ComponentType<T> : ComponentType where T : unmanaged
{
    /// <summary>
    /// Creates component metadata for <typeparamref name="T" />.
    /// </summary>
    public ComponentType() : base(typeof(T))
    {
    }

    internal override IComponentColumn CreateColumn() => new ComponentColumn<T>();
}
