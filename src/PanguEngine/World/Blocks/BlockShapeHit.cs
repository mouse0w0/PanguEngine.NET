using Silk.NET.Maths;

namespace PanguEngine.World.Blocks;

/// <summary>
/// Describes a ray intersection with a block shape.
/// </summary>
/// <param name="Point">The block-local intersection point.</param>
/// <param name="Face">The intersected face.</param>
/// <param name="Distance">The distance from the ray origin.</param>
/// <param name="IsInside">Whether the ray origin is strictly inside the shape.</param>
public readonly record struct BlockShapeHit(
    Vector3D<double> Point,
    Direction Face,
    double Distance,
    bool IsInside);