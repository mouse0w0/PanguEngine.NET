using PanguEngine.Registries;
using PanguEngine.World.Entities;
using PanguEngine.World.Entities.Components;
using Silk.NET.Maths;

namespace PanguEngine.Tests.World.Entities;

internal sealed class EntityTestContext : IDisposable
{
    private EntityTestContext(EntityManager entities)
    {
        Entities = entities;
    }

    internal EntityManager Entities { get; }

    internal static EntityTestContext Create() => new(new EntityManager());

    internal static (
        Registry<ComponentType> ComponentTypes,
        Registry<EntityType> EntityTypes) CreateMutableRegistries()
    {
        var componentTypes = new Registry<ComponentType>(
            ResourceKey.Parse("test:component_type"), (current, _, value) => value.ValidateRegistration(current));
        BuiltinEntityComponents.Register(componentTypes);
        var entityTypes = new Registry<EntityType>(
            ResourceKey.Parse("test:entity_type"), (_, _, value) => value.ValidateRegistration(componentTypes));
        BuiltinEntityTypes.Register(entityTypes);
        return (componentTypes, entityTypes);
    }

    internal static EntitySpawnBuilder CreatePlayerValues(
        Vector3D<double> position,
        double yaw = -90,
        double pitch = -20)
    {
        return new EntitySpawnBuilder()
            .Add(BuiltinEntityComponents.Position, new PositionComponent(position, position))
            .Add(BuiltinEntityComponents.Rotation, new RotationComponent(yaw, pitch))
            .Add(BuiltinEntityComponents.MoveIntent, default);
    }

    internal PlayerEntity SpawnPlayer(
        Vector3D<double> position,
        double yaw = -90,
        double pitch = -20)
    {
        return Entities.Spawn(
            BuiltinEntityTypes.Player,
            CreatePlayerValues(position, yaw, pitch));
    }

    internal void SetPosition(PlayerEntity player, PositionComponent position)
    {
        Entities.SetComponent(player.Handle, BuiltinEntityComponents.Position, position);
    }

    public void Dispose()
    {
        Entities.Destroy();
    }
}
