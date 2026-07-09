using PanguEngine.Windowing;

namespace PanguEngine.Graphics;

/// <summary>
/// Base class for graphics backend implementations.
/// </summary>
public abstract class GraphicsBackend
{
    /// <summary>
    /// Gets the graphics backend type.
    /// </summary>
    public abstract GraphicsBackendType Type { get; }

    /// <summary>
    /// Gets the graphics device created by the backend.
    /// </summary>
    public abstract GraphicsDevice Device { get; }

    /// <summary>
    /// Gets the display manager created by the backend.
    /// </summary>
    public abstract DisplayManager DisplayManager { get; }

    /// <summary>
    /// Gets the window manager created by the backend.
    /// </summary>
    public abstract WindowManager WindowManager { get; }

    /// <summary>
    /// Gets the primary window created by the backend.
    /// </summary>
    public abstract Window PrimaryWindow { get; }

    /// <summary>
    /// Gets whether the backend has been destroyed.
    /// </summary>
    public abstract bool IsDestroyed { get; }

    /// <summary>
    /// Destroys the graphics backend.
    /// </summary>
    internal abstract void Destroy();
}