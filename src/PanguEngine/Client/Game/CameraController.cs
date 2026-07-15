using Silk.NET.Maths;

namespace PanguEngine.Client.Game;

/// <summary>
/// Controls a camera using free-moving client input.
/// </summary>
internal sealed class CameraController
{
    internal const double MoveDistancePerTick = 0.4d;
    internal const double MouseSensitivity = 0.08d;
    internal const double MinPitch = -89d;
    internal const double MaxPitch = 89d;

    private readonly Camera _camera;

    internal CameraController(Camera camera)
    {
        _camera = camera;
    }

    /// <summary>
    /// Applies a relative mouse movement to the controlled camera orientation.
    /// </summary>
    /// <param name="delta">The relative movement in screen coordinates.</param>
    internal void ApplyMouseDelta(Vector2D<float> delta)
    {
        var yaw = _camera.Yaw + delta.X * MouseSensitivity;
        var pitch = Math.Clamp(
            _camera.Pitch - delta.Y * MouseSensitivity,
            MinPitch,
            MaxPitch);
        _camera.SetOrientation(yaw, pitch);
    }

    /// <summary>
    /// Advances the controlled camera position by one fixed update.
    /// </summary>
    /// <param name="forward">The signed forward input.</param>
    /// <param name="right">The signed right input.</param>
    internal void Move(double forward, double right)
    {
        _camera.BeginFixedUpdate();

        var rightDirection = Vector3D.Normalize(Vector3D.Cross(_camera.Forward, Vector3D<double>.UnitY));
        var movement = _camera.Forward * forward + rightDirection * right;
        var lengthSquared = Vector3D.Dot(movement, movement);
        if (lengthSquared <= 0)
            return;

        _camera.SetPosition(
            _camera.CurrentPosition
            + movement / Math.Sqrt(lengthSquared) * MoveDistancePerTick);
    }
}