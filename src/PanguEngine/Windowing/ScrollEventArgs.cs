namespace PanguEngine.Windowing;

/// <summary>
/// Provides data for the <see cref="Window.Scroll"/> event.
/// </summary>
/// <param name="X">The horizontal scroll offset.</param>
/// <param name="Y">The vertical scroll offset.</param>
public record ScrollEventArgs(float X, float Y);