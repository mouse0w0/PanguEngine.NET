using PanguEngine.Registries;

namespace PanguEngine.World.Entities;

internal sealed class EntityComponentTypeRegistry(ResourceKey key) : Registry<ComponentType>(key)
{
    private readonly HashSet<Type> _componentValueTypes = [];

    public override RegistryEntry<ComponentType> Register(ResourceKey key, ComponentType value)
    {
        if (!_componentValueTypes.Add(value.ValueType))
            throw new InvalidOperationException($"Component value type '{value.ValueType}' is already registered.");

        try
        {
            return base.Register(key, value);
        }
        catch
        {
            _componentValueTypes.Remove(value.ValueType);
            throw;
        }
    }
}
