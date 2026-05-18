namespace PanguEngine.Graphics;

/// <summary>
/// Base class for all texture resources.
/// </summary>
public abstract class Texture : GraphicsResource
{
    /// <summary>
    /// Gets whether the texture has been destroyed.
    /// </summary>
    public abstract override bool IsDestroyed { get; }

    /// <summary>
    /// Gets the texture pixel format.
    /// </summary>
    public abstract TextureFormat Format { get; }

    /// <summary>
    /// Gets the texture width in pixels.
    /// </summary>
    public abstract uint Width { get; }

    /// <summary>
    /// Gets the texture height in pixels.
    /// </summary>
    public abstract uint Height { get; }

    /// <summary>
    /// Gets the texture depth in pixels.
    /// </summary>
    public abstract uint Depth { get; }

    /// <summary>
    /// Gets the number of mip levels.
    /// </summary>
    public abstract uint MipLevels { get; }

    /// <summary>
    /// Gets the number of array layers.
    /// </summary>
    public abstract uint ArrayLayers { get; }

    /// <summary>
    /// Gets the texture usage flags.
    /// </summary>
    public abstract TextureUsage Usage { get; }
}