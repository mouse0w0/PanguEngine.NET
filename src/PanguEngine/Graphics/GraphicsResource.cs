namespace PanguEngine.Graphics;

/// <summary>
/// Base class for all graphics resources.
/// </summary>
public abstract class GraphicsResource
{
    /// <summary>
    /// Gets whether the resource has been destroyed.
    /// </summary>
    public abstract bool IsDestroyed { get; }

    /// <summary>
    /// Throws an <see cref="ObjectDisposedException"/> if the resource has been destroyed.
    /// </summary>
    public void ThrowIfDestroyed()
    {
        if (IsDestroyed)
            throw new ObjectDisposedException(GetType().Name);
    }

    /// <summary>
    /// Destroys the resource and releases its GPU memory.
    /// </summary>
    public abstract void Destroy();
}