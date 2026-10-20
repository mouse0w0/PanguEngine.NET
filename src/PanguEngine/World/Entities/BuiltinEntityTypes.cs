using PanguEngine.Registries;

namespace PanguEngine.World.Entities;

/// <summary>
/// Provides the built-in entity type definitions.
/// </summary>
public static class BuiltinEntityTypes
{
    /// <summary>The built-in player entity type.</summary>
    public static EntityType<PlayerEntity> Player { get; } = new(
        [
            BuiltinEntityComponents.Position,
            BuiltinEntityComponents.Rotation,
            BuiltinEntityComponents.MoveIntent
        ],
        static (entities, handle) => new PlayerEntity(entities, handle));

    internal static void Register(IWritableRegistry<EntityType> registry)
    {
        registry.Register(ResourceKey.Create("pangu", "player"), Player);
    }
}
