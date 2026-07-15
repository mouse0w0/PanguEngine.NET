using Silk.NET.Maths;

namespace PanguEngine.Client.Game;

/// <summary>
/// Represents a free-moving client camera with fixed-update position state.
/// </summary>
internal sealed class FreeCamera
{
    internal const double MoveDistancePerTick = 0.4d;
    internal const double MouseSensitivity = 0.08d;
    internal const double MinPitch = -89d;
    internal const double MaxPitch = 89d;
    internal const double FieldOfView = 70d;
    internal const double NearPlane = 0.05d;
    internal const double FarPlane = 1000d;

    private const double DegreesToRadians = Math.PI / 180d;
    private const double DirectionComponentEpsilon = 0.000001d;

    internal FreeCamera()
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

    private static double SnapDirectionComponent(double value)
    {
        return Math.Abs(value) < DirectionComponentEpsilon ? 0 : value;
    }

    /// <summary>
    /// Applies a relative mouse movement to the camera orientation.
    /// </summary>
    /// <param name="delta">The relative movement in screen coordinates.</param>
    internal void ApplyMouseDelta(Vector2D<float> delta)
    {
        Yaw += (double)delta.X * MouseSensitivity;
        Pitch = Math.Clamp(Pitch - (double)delta.Y * MouseSensitivity, MinPitch, MaxPitch);
    }

    /// <summary>
    /// Advances the camera position by one fixed update.
    /// </summary>
    /// <param name="forward">The signed forward input.</param>
    /// <param name="right">The signed right input.</param>
    internal void Move(double forward, double right)
    {
        PreviousPosition = CurrentPosition;

        var rightDirection = Vector3D.Normalize(Vector3D.Cross(Forward, Vector3D<double>.UnitY));
        var movement = Forward * forward + rightDirection * right;
        var lengthSquared = Vector3D.Dot(movement, movement);
        if (lengthSquared <= 0)
            return;

        CurrentPosition += movement / Math.Sqrt(lengthSquared) * MoveDistancePerTick;
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
}