namespace PanguEngine.Windowing;

/// <summary>
/// Provides data for the <see cref="Window.MouseMove"/> event.
/// </summary>
/// <param name="X">The cursor X position.</param>
/// <param name="Y">The cursor Y position.</param>
public record MouseMoveEventArgs(float X, float Y);