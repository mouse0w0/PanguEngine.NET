using PanguEngine.World.Entities;
using Silk.NET.Maths;

namespace PanguEngine.Client.Game;

/// <summary>
/// Stores camera view state and projection settings.
/// </summary>
public sealed class Camera
{
    /// <summary>
    /// Creates a camera with an initial position and orientation.
    /// </summary>
    /// <param name="position">The initial camera position.</param>
    /// <param name="yaw">The horizontal angle in degrees.</param>
    /// <param name="pitch">The vertical angle in degrees.</param>
    internal Camera(Vector3D<double> position, double yaw, double pitch)
    {
        Position = position;
        Yaw = yaw;
        Pitch = pitch;
    }

    /// <summary>The camera presentation position for the current frame.</summary>
    public Vector3D<double> Position { get; internal set; }

    /// <summary>The horizontal camera angle in degrees.</summary>
    public double Yaw { get; internal set; }

    /// <summary>The vertical camera angle in degrees.</summary>
    public double Pitch { get; internal set; }

    /// <summary>The presentation width divided by height.</summary>
    internal double AspectRatio { get; set; } = 1;

    /// <summary>The vertical field of view in degrees.</summary>
    internal double FieldOfView { get; set; } = 70d;

    /// <summary>The distance to the near clipping plane.</summary>
    internal double NearPlane { get; set; } = 0.05d;

    /// <summary>The distance to the far clipping plane.</summary>
    internal double FarPlane { get; set; } = 1000d;

    /// <summary>The current normalized camera forward direction.</summary>
    internal Vector3D<double> Forward => EntityPoseMath.GetForward(Yaw, Pitch);

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
    /// Creates the right-handed view matrix.
    /// </summary>
    /// <returns>The view matrix.</returns>
    internal Matrix4X4<double> CreateViewMatrix()
    {
        return Matrix4X4.CreateLookAt(Position, Position + Forward, Vector3D<double>.UnitY);
    }

    /// <summary>
    /// Creates the view-projection matrix.
    /// </summary>
    /// <returns>The view-projection matrix.</returns>
    internal Matrix4X4<float> CreateViewProjection()
    {
        return (Matrix4X4<float>)(CreateViewMatrix() * CreateProjectionMatrix());
    }

    /// <summary>
    /// Creates the state used by camera-relative world rendering.
    /// </summary>
    /// <returns>The world rendering state.</returns>
    internal WorldRenderState CreateWorldRenderState()
    {
        var view = Matrix4X4.CreateLookAt(Vector3D<double>.Zero, Forward, Vector3D<double>.UnitY);
        var viewProjection = (Matrix4X4<float>)(view * CreateProjectionMatrix());
        return new WorldRenderState(Position, viewProjection);
    }
}
