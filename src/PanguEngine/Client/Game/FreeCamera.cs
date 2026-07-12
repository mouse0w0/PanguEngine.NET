using Silk.NET.Maths;

namespace PanguEngine.Client.Game;

/// <summary>
/// Represents a free-moving client camera with fixed-update position state.
/// </summary>
internal sealed class FreeCamera
{
    internal const float MoveDistancePerTick = 0.4f;
    internal const float MouseSensitivity = 0.08f;
    internal const float MinPitch = -89f;
    internal const float MaxPitch = 89f;
    internal const float FieldOfView = 70f;
    internal const float NearPlane = 0.05f;
    internal const float FarPlane = 1000f;

    private const float DegreesToRadians = MathF.PI / 180f;

    internal FreeCamera()
    {
        PreviousPosition = new Vector3D<float>(8, 6, 24);
        CurrentPosition = PreviousPosition;
        Yaw = -90;
        Pitch = -20;
    }

    /// <summary>The position from the previous fixed update.</summary>
    internal Vector3D<float> PreviousPosition { get; private set; }

    /// <summary>The position from the current fixed update.</summary>
    internal Vector3D<float> CurrentPosition { get; private set; }

    /// <summary>The horizontal camera angle in degrees.</summary>
    internal float Yaw { get; private set; }

    /// <summary>The vertical camera angle in degrees.</summary>
    internal float Pitch { get; private set; }

    /// <summary>The current normalized camera forward direction.</summary>
    internal Vector3D<float> Forward
    {
        get
        {
            var yaw = Yaw * DegreesToRadians;
            var pitch = Pitch * DegreesToRadians;
            return Vector3D.Normalize(new Vector3D<float>(
                MathF.Cos(pitch) * MathF.Cos(yaw),
                MathF.Sin(pitch),
                MathF.Cos(pitch) * MathF.Sin(yaw)));
        }
    }

    /// <summary>
    /// Applies a relative mouse movement to the camera orientation.
    /// </summary>
    /// <param name="delta">The relative movement in screen coordinates.</param>
    internal void ApplyMouseDelta(Vector2D<float> delta)
    {
        Yaw += delta.X * MouseSensitivity;
        Pitch = Math.Clamp(Pitch - delta.Y * MouseSensitivity, MinPitch, MaxPitch);
    }

    /// <summary>
    /// Advances the camera position by one fixed update.
    /// </summary>
    /// <param name="forward">The signed forward input.</param>
    /// <param name="right">The signed right input.</param>
    internal void Move(float forward, float right)
    {
        PreviousPosition = CurrentPosition;

        var rightDirection = Vector3D.Normalize(Vector3D.Cross(Forward, Vector3D<float>.UnitY));
        var movement = Forward * forward + rightDirection * right;
        var lengthSquared = Vector3D.Dot(movement, movement);
        if (lengthSquared <= 0)
            return;

        CurrentPosition += movement / MathF.Sqrt(lengthSquared) * MoveDistancePerTick;
    }

    /// <summary>
    /// Gets the camera position interpolated between fixed updates.
    /// </summary>
    /// <param name="alpha">The interpolation factor.</param>
    /// <returns>The interpolated position.</returns>
    internal Vector3D<float> GetInterpolatedPosition(double alpha)
    {
        var amount = (float)Math.Clamp(alpha, 0, 1);
        return PreviousPosition + (CurrentPosition - PreviousPosition) * amount;
    }

    /// <summary>
    /// Creates the interpolated right-handed view matrix.
    /// </summary>
    /// <param name="alpha">The fixed-update interpolation factor.</param>
    /// <returns>The view matrix.</returns>
    internal Matrix4X4<float> CreateViewMatrix(double alpha)
    {
        var position = GetInterpolatedPosition(alpha);
        return Matrix4X4.CreateLookAt(position, position + Forward, Vector3D<float>.UnitY);
    }

    /// <summary>
    /// Creates a right-handed perspective matrix with Vulkan depth range.
    /// </summary>
    /// <param name="aspectRatio">The presentation width divided by height.</param>
    /// <returns>The projection matrix.</returns>
    internal Matrix4X4<float> CreateProjectionMatrix(float aspectRatio)
    {
        if (!float.IsFinite(aspectRatio) || aspectRatio <= 0)
            throw new ArgumentOutOfRangeException(nameof(aspectRatio));

        var projection = Matrix4X4.CreatePerspectiveFieldOfView(
            FieldOfView * DegreesToRadians,
            aspectRatio,
            NearPlane,
            FarPlane);
        projection.M22 = -projection.M22;
        return projection;
    }

    /// <summary>
    /// Creates the interpolated view-projection matrix.
    /// </summary>
    /// <param name="aspectRatio">The presentation width divided by height.</param>
    /// <param name="alpha">The fixed-update interpolation factor.</param>
    /// <returns>The view-projection matrix.</returns>
    internal Matrix4X4<float> CreateViewProjection(float aspectRatio, double alpha)
    {
        return CreateViewMatrix(alpha) * CreateProjectionMatrix(aspectRatio);
    }
}