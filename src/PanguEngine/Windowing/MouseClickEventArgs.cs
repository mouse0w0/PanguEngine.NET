using PanguEngine.Input;

namespace PanguEngine.Windowing;

/// <summary>
/// Provides data for mouse button events.
/// </summary>
/// <param name="Button">The mouse button that triggered the event.</param>
/// <param name="X">The cursor X position.</param>
/// <param name="Y">The cursor Y position.</param>
public record MouseClickEventArgs(MouseButton Button, float X, float Y);