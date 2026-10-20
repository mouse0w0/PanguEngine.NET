using PanguEngine.World.Entities.Components;
using Silk.NET.Maths;

namespace PanguEngine.World.Entities;

/// <summary>
/// Provides shared direction calculations for entity rotations.
/// </summary>
public static class EntityPoseMath
{
    private const double DirectionComponentEpsilon = 0.000001d;

    /// <summary>
    /// Gets the normalized forward direction for a rotation.
    /// </summary>
    /// <param name="rotation">The entity rotation.</param>
    /// <returns>The normalized forward direction.</returns>
    public static Vector3D<double> GetForward(RotationComponent rotation)
    {
        var yaw = double.DegreesToRadians(rotation.Yaw);
        var pitch = double.DegreesToRadians(rotation.Pitch);
        var direction = Vector3D.Normalize(new Vector3D<double>(
            Math.Cos(pitch) * Math.Cos(yaw),
            Math.Sin(pitch),
            Math.Cos(pitch) * Math.Sin(yaw)));
        return Vector3D.Normalize(new Vector3D<double>(
            SnapDirectionComponent(direction.X),
            SnapDirectionComponent(direction.Y),
            SnapDirectionComponent(direction.Z)));
    }

    /// <summary>
    /// Gets the normalized horizontal right direction for a forward direction.
    /// </summary>
    /// <param name="forward">The normalized forward direction.</param>
    /// <returns>The normalized horizontal right direction.</returns>
    public static Vector3D<double> GetRight(Vector3D<double> forward) =>
        Vector3D.Normalize(Vector3D.Cross(forward, Vector3D<double>.UnitY));

    private static double SnapDirectionComponent(double value) =>
        Math.Abs(value) < DirectionComponentEpsilon ? 0 : value;
}
