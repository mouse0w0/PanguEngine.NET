using PanguEngine.World.Entities;
using PanguEngine.World.Entities.Components;
using Silk.NET.Maths;

namespace PanguEngine.Server.World;

/// <summary>
/// Applies free-flight movement to player entity component columns.
/// </summary>
internal sealed class PlayerFreeFlightUpdater
{
    /// <summary>The movement distance applied per fixed update.</summary>
    internal double MoveDistancePerTick { get; set; } = 0.4d;

    /// <summary>
    /// Updates every player row in the entity manager.
    /// </summary>
    /// <param name="entities">The entity manager to update.</param>
    internal void Update(EntityManager entities)
    {
        entities.BeginUpdate();
        try
        {   
            if (!entities.TryGetArchetype(BuiltinEntityTypes.Player, out var archetype))
                return;

            var positions = archetype.GetSpan(BuiltinEntityComponents.Position);
            var rotations = archetype.GetSpan(BuiltinEntityComponents.Rotation);
            var intents = archetype.GetSpan(BuiltinEntityComponents.MoveIntent);
            for (var index = 0; index < positions.Length; index++)
            {
                var position = positions[index];
                var intent = intents[index];
                var forward = EntityPoseMath.GetForward(rotations[index].Yaw, rotations[index].Pitch);
                var right = EntityPoseMath.GetRight(forward);
                var movement = forward * intent.Forward + right * intent.Right;
                var lengthSquared = Vector3D.Dot(movement, movement);
                var delta = lengthSquared <= 0
                    ? Vector3D<double>.Zero
                    : movement / Math.Sqrt(lengthSquared) * MoveDistancePerTick;
                positions[index] = new PositionComponent(position.Current, position.Current + delta);
            }
        }
        finally
        {
            entities.EndUpdate();
        }
    }
}
