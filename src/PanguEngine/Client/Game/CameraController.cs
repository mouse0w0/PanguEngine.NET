using Silk.NET.Maths;

namespace PanguEngine.Client.Game;

/// <summary>
/// Controls a camera using free-moving client input.
/// </summary>
internal sealed class CameraController
{
    private readonly Camera _camera;

    internal CameraController(Camera camera)
    {
        _camera = camera;
    }

    /// <summary>The movement distance applied per fixed update.</summary>
    internal double MoveDistancePerTick { get; set; } = 0.4d;

    /// <summary>The mouse rotation sensitivity.</summary>
    internal double MouseSensitivity { get; set; } = 0.08d;

    /// <summary>The minimum vertical camera angle in degrees.</summary>
    internal double MinPitch { get; set; } = -89d;

    /// <summary>The maximum vertical camera angle in degrees.</summary>
    internal double MaxPitch { get; set; } = 89d;

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