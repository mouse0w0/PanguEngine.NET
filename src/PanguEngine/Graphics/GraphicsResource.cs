namespace PanguEngine.Graphics;

/// <summary>
/// Base class for all graphics resources.
/// </summary>
public abstract class GraphicsResource
{
    /// <summary>
    /// Gets whether the resource has been destroyed.
    /// </summary>
    public bool IsDestroyed { get; private set; }

    /// <summary>
    /// Marks the resource as destroyed.
    /// </summary>
    protected void MarkDestroyed()
    {
        IsDestroyed = true;
    }

    /// <summary>
    /// Throws an <see cref="ObjectDisposedException"/> if the resource has been destroyed.
    /// </summary>
    public void ThrowIfDestroyed()
    {
        ObjectDisposedException.ThrowIf(IsDestroyed, this);
    }

    /// <summary>
    /// Destroys the resource and releases its GPU memory.
    /// </summary>
    public abstract void Destroy();
}