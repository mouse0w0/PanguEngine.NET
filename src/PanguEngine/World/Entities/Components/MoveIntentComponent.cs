namespace PanguEngine.World.Entities.Components;

/// <summary>
/// Stores local forward and right movement input for a fixed update.
/// </summary>
/// <param name="Forward">The signed forward input.</param>
/// <param name="Right">The signed right input.</param>
public readonly record struct MoveIntentComponent(double Forward, double Right);
