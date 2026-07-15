namespace PanguEngine.Graphics;

/// <summary>
/// Base class for all texture view resources.
/// </summary>
public abstract class TextureView : GraphicsResource
{
    /// <summary>
    /// Gets the texture referenced by the view.
    /// </summary>
    public abstract Texture Texture { get; }

    /// <summary>
    /// Gets the texture view dimensional shape.
    /// </summary>
    public abstract TextureViewDimension Dimension { get; }

    /// <summary>
    /// Gets the texture view pixel format.
    /// </summary>
    public abstract TextureFormat Format { get; }

    /// <summary>
    /// Gets the texture view width at its base mip level.
    /// </summary>
    public abstract uint Width { get; }

    /// <summary>
    /// Gets the texture view height at its base mip level.
    /// </summary>
    public abstract uint Height { get; }

    /// <summary>
    /// Gets the texture view depth at its base mip level.
    /// </summary>
    public abstract uint Depth { get; }

    /// <summary>
    /// Gets the first mip level included in the view.
    /// </summary>
    public abstract uint BaseMipLevel { get; }

    /// <summary>
    /// Gets the number of mip levels included in the view.
    /// </summary>
    public abstract uint MipLevels { get; }

    /// <summary>
    /// Gets the first array layer included in the view.
    /// </summary>
    public abstract uint BaseArrayLayer { get; }

    /// <summary>
    /// Gets the number of array layers included in the view.
    /// </summary>
    public abstract uint ArrayLayers { get; }
}