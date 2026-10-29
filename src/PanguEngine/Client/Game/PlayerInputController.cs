using Silk.NET.Maths;

namespace PanguEngine.Client.Game;

/// <summary>
/// Applies local mouse input to the displayed player view.
/// </summary>
internal sealed class PlayerInputController(Camera camera)
{
    /// <summary>The mouse rotation sensitivity.</summary>
    internal double MouseSensitivity { get; set; } = 0.08d;

    /// <summary>The minimum vertical view angle in degrees.</summary>
    internal double MinPitch { get; set; } = -89d;

    /// <summary>The maximum vertical view angle in degrees.</summary>
    internal double MaxPitch { get; set; } = 89d;

    /// <summary>
    /// Applies a relative mouse movement to the local camera orientation.
    /// </summary>
    /// <param name="delta">The relative movement in screen coordinates.</param>
    internal void ApplyMouseDelta(Vector2D<float> delta)
    {
        camera.Yaw += delta.X * MouseSensitivity;
        camera.Pitch = Math.Clamp(camera.Pitch - delta.Y * MouseSensitivity, MinPitch, MaxPitch);
    }
}
