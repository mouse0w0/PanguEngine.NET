namespace PanguEngine.Windowing;

/// <summary>
/// Provides framebuffer resize event data.
/// </summary>
/// <param name="Width">The framebuffer width in pixels.</param>
/// <param name="Height">The framebuffer height in pixels.</param>
public record FramebufferResizeEventArgs(int Width, int Height);