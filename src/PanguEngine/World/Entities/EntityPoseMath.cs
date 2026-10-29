using Silk.NET.Maths;

namespace PanguEngine.World.Entities;

/// <summary>
/// Provides shared direction calculations for yaw and pitch angles.
/// </summary>
public static class EntityPoseMath
{
    private const double DirectionComponentEpsilon = 0.000001d;

    /// <summary>
    /// Gets the normalized forward direction for yaw and pitch angles.
    /// </summary>
    /// <param name="yaw">The horizontal angle in degrees.</param>
    /// <param name="pitch">The vertical angle in degrees.</param>
    /// <returns>The normalized forward direction.</returns>
    public static Vector3D<double> GetForward(double yaw, double pitch)
    {
        var yawRadians = double.DegreesToRadians(yaw);
        var pitchRadians = double.DegreesToRadians(pitch);
        var direction = Vector3D.Normalize(new Vector3D<double>(
            Math.Cos(pitchRadians) * Math.Cos(yawRadians),
            Math.Sin(pitchRadians),
            Math.Cos(pitchRadians) * Math.Sin(yawRadians)));
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
