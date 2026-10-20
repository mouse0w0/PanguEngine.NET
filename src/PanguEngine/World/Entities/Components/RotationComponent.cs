namespace PanguEngine.World.Entities.Components;

/// <summary>
/// Stores the yaw and pitch of an entity in degrees.
/// </summary>
/// <param name="Yaw">The horizontal angle in degrees.</param>
/// <param name="Pitch">The vertical angle in degrees.</param>
public readonly record struct RotationComponent(double Yaw, double Pitch);
