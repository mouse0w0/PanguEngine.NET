using PanguEngine.Registries;
using PanguEngine.World.Entities.Components;

namespace PanguEngine.World.Entities;

/// <summary>
/// Provides the built-in entity component definitions.
/// </summary>
public static class BuiltinEntityComponents
{
    /// <summary>The built-in position component type.</summary>
    public static ComponentType<PositionComponent> Position { get; } = new();

    /// <summary>The built-in rotation component type.</summary>
    public static ComponentType<RotationComponent> Rotation { get; } = new();

    /// <summary>The built-in move intent component type.</summary>
    public static ComponentType<MoveIntentComponent> MoveIntent { get; } = new();

    internal static void Register(IWritableRegistry<ComponentType> registry)
    {
        registry.Register(ResourceKey.Create("pangu", "position"), Position);
        registry.Register(ResourceKey.Create("pangu", "rotation"), Rotation);
        registry.Register(ResourceKey.Create("pangu", "move_intent"), MoveIntent);
    }
}
