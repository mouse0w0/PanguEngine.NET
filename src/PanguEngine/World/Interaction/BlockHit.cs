using PanguEngine.World.Blocks;
using PanguEngine.World.Chunking;
using Silk.NET.Maths;

namespace PanguEngine.World.Interaction;

/// <summary>
/// Describes a block selection shape hit by a ray.
/// </summary>
/// <param name="BlockPosition">The hit block position.</param>
/// <param name="BlockState">The hit block state.</param>
/// <param name="Point">The world-space hit point.</param>
/// <param name="Face">The selected interaction face.</param>
/// <param name="Distance">The distance from the ray origin.</param>
/// <param name="IsInside">Whether the ray started strictly inside the hit shape.</param>
public readonly record struct BlockHit(
    BlockPos BlockPosition,
    BlockState BlockState,
    Vector3D<double> Point,
    Direction Face,
    double Distance,
    bool IsInside);