using PanguEngine.Registries;
using PanguEngine.World;
using Silk.NET.Maths;

namespace PanguEngine.Client.Resources.Models;

internal sealed record ResolvedBlockModel(
    ResourceKey SourceKey,
    IReadOnlyList<ResolvedBlockElement> Elements);

internal sealed record ResolvedBlockElement(
    Vector3D<float> From,
    Vector3D<float> To,
    IReadOnlyDictionary<Direction, ResolvedBlockFace> Faces);

internal sealed record ResolvedBlockFace(
    ResourceKey Texture,
    float[] Uv,
    int Rotation,
    DirectionFlags Cull);