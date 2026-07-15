using Silk.NET.Maths;

namespace PanguEngine.Client.Game;

/// <summary>
/// Represents a client camera with fixed-update position state.
/// </summary>
internal sealed class Camera
{
    private const double DegreesToRadians = Math.PI / 180d;
    private const double DirectionComponentEpsilon = 0.000001d;

    internal Camera()
    {
        PreviousPosition = new Vector3D<double>(8, 6, 24);
        CurrentPosition = PreviousPosition;
        Yaw = -90;
        Pitch = -20;
    }

    /// <summary>The position from the previous fixed update.</summary>
    internal Vector3D<double> PreviousPosition { get; private set; }

    /// <summary>The position from the current fixed update.</summary>
    internal Vector3D<double> CurrentPosition { get; private set; }

    /// <summary>The horizontal camera angle in degrees.</summary>
    internal double Yaw { get; private set; }

    /// <summary>The vertical camera angle in degrees.</summary>
    internal double Pitch { get; private set; }

    /// <summary>The presentation width divided by height.</summary>
    internal double AspectRatio { get; set; } = 1;

    /// <summary>The vertical field of view in degrees.</summary>
    internal double FieldOfView { get; set; } = 70d;

    /// <summary>The distance to the near clipping plane.</summary>
    internal double NearPlane { get; set; } = 0.05d;

    /// <summary>The distance to the far clipping plane.</summary>
    internal double FarPlane { get; set; } = 1000d;

    /// <summary>The current normalized camera forward direction.</summary>
    internal Vector3D<double> Forward
    {
        get
        {
            var yaw = Yaw * DegreesToRadians;
            var pitch = Pitch * DegreesToRadians;
            var direction = Vector3D.Normalize(new Vector3D<double>(
                Math.Cos(pitch) * Math.Cos(yaw),
                Math.Sin(pitch),
                Math.Cos(pitch) * Math.Sin(yaw)));
            return Vector3D.Normalize(new Vector3D<double>(
                SnapDirectionComponent(direction.X),
                SnapDirectionComponent(direction.Y),
                SnapDirectionComponent(direction.Z)));
        }
    }

    /// <summary>
    /// Captures the current position as the previous fixed-update position.
    /// </summary>
    internal void BeginFixedUpdate()
    {
        PreviousPosition = CurrentPosition;
    }

    /// <summary>
    /// Sets the current camera position.
    /// </summary>
    /// <param name="position">The new current position.</param>
    internal void SetPosition(Vector3D<double> position)
    {
        CurrentPosition = position;
    }

    /// <summary>
    /// Sets the current camera orientation.
    /// </summary>
    /// <param name="yaw">The horizontal angle in degrees.</param>
    /// <param name="pitch">The vertical angle in degrees.</param>
    internal void SetOrientation(double yaw, double pitch)
    {
        Yaw = yaw;
        Pitch = pitch;
    }

    /// <summary>
    /// Gets the camera position interpolated between fixed updates.
    /// </summary>
    /// <param name="alpha">The interpolation factor.</param>
    /// <returns>The interpolated position.</returns>
    internal Vector3D<double> GetInterpolatedPosition(double alpha)
    {
        var amount = Math.Clamp(alpha, 0, 1);
        return PreviousPosition + (CurrentPosition - PreviousPosition) * amount;
    }

    /// <summary>
    /// Creates a right-handed perspective matrix with Vulkan depth range.
    /// </summary>
    /// <returns>The projection matrix.</returns>
    internal Matrix4X4<double> CreateProjectionMatrix()
    {
        var projection = Matrix4X4.CreatePerspectiveFieldOfView(
            FieldOfView * DegreesToRadians,
            AspectRatio,
            NearPlane,
            FarPlane);
        projection.M22 = -projection.M22;
        return projection;
    }

    /// <summary>
    /// Creates the interpolated right-handed view matrix.
    /// </summary>
    /// <param name="alpha">The fixed-update interpolation factor.</param>
    /// <returns>The view matrix.</returns>
    internal Matrix4X4<double> CreateViewMatrix(double alpha)
    {
        var position = GetInterpolatedPosition(alpha);
        return Matrix4X4.CreateLookAt(position, position + Forward, Vector3D<double>.UnitY);
    }

    /// <summary>
    /// Creates the interpolated view-projection matrix.
    /// </summary>
    /// <param name="alpha">The fixed-update interpolation factor.</param>
    /// <returns>The view-projection matrix.</returns>
    internal Matrix4X4<float> CreateViewProjection(double alpha)
    {
        return (Matrix4X4<float>)(CreateViewMatrix(alpha) * CreateProjectionMatrix());
    }

    private static double SnapDirectionComponent(double value)
    {
        return Math.Abs(value) < DirectionComponentEpsilon ? 0 : value;
    }
}