using PanguEngine.World.Entities;
using PanguEngine.World.Entities.Components;
using Silk.NET.Maths;

namespace PanguEngine.Client.Game;

/// <summary>
/// Applies local client input to a player entity.
/// </summary>
internal sealed class PlayerInputController(PlayerEntity player)
{
    private readonly PlayerEntity _player = player;

    /// <summary>The mouse rotation sensitivity.</summary>
    internal double MouseSensitivity { get; set; } = 0.08d;

    /// <summary>The minimum vertical view angle in degrees.</summary>
    internal double MinPitch { get; set; } = -89d;

    /// <summary>The maximum vertical view angle in degrees.</summary>
    internal double MaxPitch { get; set; } = 89d;

    /// <summary>
    /// Applies a relative mouse movement to the player orientation.
    /// </summary>
    /// <param name="delta">The relative movement in screen coordinates.</param>
    internal void ApplyMouseDelta(Vector2D<float> delta)
    {
        var rotation = _player.Rotation;
        _player.SetRotation(new RotationComponent(
            rotation.Yaw + delta.X * MouseSensitivity,
            Math.Clamp(rotation.Pitch - delta.Y * MouseSensitivity, MinPitch, MaxPitch)));
    }

    /// <summary>
    /// Replaces the local movement intent for the next fixed update.
    /// </summary>
    /// <param name="forward">The signed forward input.</param>
    /// <param name="right">The signed right input.</param>
    internal void SetMoveIntent(double forward, double right)
    {
        _player.SetMoveIntent(new MoveIntentComponent(forward, right));
    }
}
