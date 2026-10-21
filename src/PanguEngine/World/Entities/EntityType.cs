using System.Collections.ObjectModel;
using PanguEngine.Registries;

namespace PanguEngine.World.Entities;

/// <summary>
/// Describes an entity facade and its required component signature.
/// </summary>
public abstract class EntityType
{
    private readonly ReadOnlyCollection<ComponentType> _requiredSignature;

    private protected EntityType(
        Type entityClass,
        IEnumerable<ComponentType> requiredSignature,
        Func<EntityManager, EntityHandle, Entity> factory)
    {
        EntityClass = entityClass;
        _requiredSignature = Array.AsReadOnly(requiredSignature.ToArray());
        Factory = factory;
    }

    /// <summary>The facade class created for instances of this type.</summary>
    public Type EntityClass { get; }

    /// <summary>The components required for every entity of this type.</summary>
    public IReadOnlyList<ComponentType> RequiredSignature => _requiredSignature;

    internal Func<EntityManager, EntityHandle, Entity> Factory { get; }

    internal void ValidateRegistration(IRegistry<ComponentType> componentTypeRegistry)
    {
        var seen = new HashSet<ComponentType>(ReferenceEqualityComparer.Instance);
        foreach (var componentType in RequiredSignature)
        {
            if (!seen.Add(componentType))
                throw new InvalidOperationException("Entity required signature contains a duplicate component type.");
            if (!componentTypeRegistry.TryGetId(componentType, out _))
                throw new InvalidOperationException("Entity required signature contains an unregistered component type.");
        }
    }
}

/// <summary>
/// Describes an entity facade of a specific class.
/// </summary>
/// <typeparam name="TEntity">The entity facade class.</typeparam>
public sealed class EntityType<TEntity> : EntityType where TEntity : Entity
{
    internal EntityType(
        IEnumerable<ComponentType> requiredSignature,
        Func<EntityManager, EntityHandle, TEntity> factory)
        : base(typeof(TEntity), requiredSignature, factory)
    {
    }
}
