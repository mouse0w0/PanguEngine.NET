using PanguEngine.World.Entities.Components;

namespace PanguEngine.World.Entities;

/// <summary>
/// Represents a player entity facade.
/// </summary>
public sealed class PlayerEntity : Entity
{
    internal PlayerEntity(EntityManager entities, EntityHandle handle) : base(entities, handle)
    {
    }

    /// <summary>The player's fixed-update position state.</summary>
    public PositionComponent Position =>
        Entities.GetComponent(Handle, BuiltinEntityComponents.Position);

    /// <summary>The player's current rotation.</summary>
    public RotationComponent Rotation =>
        Entities.GetComponent(Handle, BuiltinEntityComponents.Rotation);

    /// <summary>The player's current local movement intent.</summary>
    public MoveIntentComponent MoveIntent =>
        Entities.GetComponent(Handle, BuiltinEntityComponents.MoveIntent);

    internal void SetRotation(RotationComponent value)
    {
        Entities.SetComponent(Handle, BuiltinEntityComponents.Rotation, value);
    }

    internal void SetMoveIntent(MoveIntentComponent value)
    {
        Entities.SetComponent(Handle, BuiltinEntityComponents.MoveIntent, value);
    }
}
