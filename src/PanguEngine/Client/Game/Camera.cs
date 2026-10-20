using PanguEngine.World.Entities;
using Silk.NET.Maths;

namespace PanguEngine.Client.Game;

/// <summary>
/// Creates client view state from a player entity and projection settings.
/// </summary>
public sealed class Camera
{
    private readonly PlayerEntity _player;

    /// <summary>
    /// Creates a camera that observes a player entity.
    /// </summary>
    /// <param name="player">The observed player.</param>
    internal Camera(PlayerEntity player)
    {
        _player = player ?? throw new ArgumentNullException(nameof(player));
    }

    /// <summary>The position from the current fixed update.</summary>
    public Vector3D<double> CurrentPosition => _player.Position.Current;

    /// <summary>The horizontal camera angle in degrees.</summary>
    public double Yaw => _player.Rotation.Yaw;

    /// <summary>The vertical camera angle in degrees.</summary>
    public double Pitch => _player.Rotation.Pitch;

    /// <summary>The presentation width divided by height.</summary>
    internal double AspectRatio { get; set; } = 1;

    /// <summary>The vertical field of view in degrees.</summary>
    internal double FieldOfView { get; set; } = 70d;

    /// <summary>The distance to the near clipping plane.</summary>
    internal double NearPlane { get; set; } = 0.05d;

    /// <summary>The distance to the far clipping plane.</summary>
    internal double FarPlane { get; set; } = 1000d;

    /// <summary>The current normalized camera forward direction.</summary>
    internal Vector3D<double> Forward => EntityPoseMath.GetForward(_player.Rotation);

    /// <summary>
    /// Gets the camera position interpolated between fixed updates.
    /// </summary>
    /// <param name="alpha">The fixed-update interpolation factor.</param>
    /// <returns>The interpolated position.</returns>
    internal Vector3D<double> GetInterpolatedPosition(double alpha)
    {
        var position = _player.Position;
        var amount = Math.Clamp(alpha, 0, 1);
        return position.Previous + (position.Current - position.Previous) * amount;
    }

    /// <summary>
    /// Creates a right-handed perspective matrix with Vulkan depth range.
    /// </summary>
    /// <returns>The projection matrix.</returns>
    internal Matrix4X4<double> CreateProjectionMatrix()
    {
        var projection = Matrix4X4.CreatePerspectiveFieldOfView(
            double.DegreesToRadians(FieldOfView),
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

    /// <summary>
    /// Creates the state used by camera-relative world rendering.
    /// </summary>
    /// <param name="alpha">The fixed-update interpolation factor.</param>
    /// <returns>The world rendering state.</returns>
    internal WorldRenderState CreateWorldRenderState(double alpha)
    {
        var position = GetInterpolatedPosition(alpha);
        var view = Matrix4X4.CreateLookAt(Vector3D<double>.Zero, Forward, Vector3D<double>.UnitY);
        var viewProjection = (Matrix4X4<float>)(view * CreateProjectionMatrix());
        return new WorldRenderState(position, viewProjection);
    }
}
