namespace PanguEngine.Graphics;

/// <summary>
/// Base class for all graphics resources.
/// </summary>
public abstract class GraphicsResource
{
    /// <summary>
    /// Destroys the resource and releases its GPU memory.
    /// </summary>
    public abstract void Destroy();
}