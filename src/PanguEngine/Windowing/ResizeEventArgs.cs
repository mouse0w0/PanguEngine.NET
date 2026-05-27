namespace PanguEngine.Windowing;

/// <summary>
/// Provides data for the <see cref="Window.Resize"/> event.
/// </summary>
/// <param name="Width">The new window width.</param>
/// <param name="Height">The new window height.</param>
/// <param name="FramebufferWidth">The new framebuffer width in pixels.</param>
/// <param name="FramebufferHeight">The new framebuffer height in pixels.</param>
public record ResizeEventArgs(int Width, int Height, int FramebufferWidth, int FramebufferHeight);