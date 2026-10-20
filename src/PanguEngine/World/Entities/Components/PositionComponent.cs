using Silk.NET.Maths;

namespace PanguEngine.World.Entities.Components;

/// <summary>
/// Stores the previous and current fixed-update positions of an entity.
/// </summary>
/// <param name="Previous">The previous fixed-update position.</param>
/// <param name="Current">The current fixed-update position.</param>
public readonly record struct PositionComponent(
    Vector3D<double> Previous,
    Vector3D<double> Current);
