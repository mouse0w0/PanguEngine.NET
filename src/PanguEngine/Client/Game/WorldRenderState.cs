using Silk.NET.Maths;

namespace PanguEngine.Client.Game;

/// <summary>
/// Stores camera data prepared for world rendering.
/// </summary>
internal readonly record struct WorldRenderState(
    Vector3D<double> WorldOrigin,
    Matrix4X4<float> ViewProjection)
{
    internal Vector4D<float> ToTranslatedWorldPosition(Vector3D<double> worldPosition)
    {
        return new Vector4D<float>(
            (float)(worldPosition.X - WorldOrigin.X),
            (float)(worldPosition.Y - WorldOrigin.Y),
            (float)(worldPosition.Z - WorldOrigin.Z),
            0);
    }
}