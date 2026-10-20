using PanguEngine.Registries;

namespace PanguEngine.World.Entities;

internal sealed class EntityTypeRegistry(
    ResourceKey key,
    IRegistry<ComponentType> componentTypeRegistry) : Registry<EntityType>(key)
{
    public override RegistryEntry<EntityType> Register(ResourceKey key, EntityType value)
    {
        var seen = new HashSet<ComponentType>(ReferenceEqualityComparer.Instance);
        var previousId = -1;
        foreach (var componentType in value.RequiredSignature)
        {
            if (!seen.Add(componentType))
                throw new InvalidOperationException("Entity required signature contains a duplicate component type.");
            if (!componentTypeRegistry.TryGetId(componentType, out var componentId))
                throw new InvalidOperationException("Entity required signature contains an unregistered component type.");
            if (componentId <= previousId)
                throw new InvalidOperationException("Entity required signature is not ordered by component registry id.");

            previousId = componentId;
        }

        return base.Register(key, value);
    }
}
