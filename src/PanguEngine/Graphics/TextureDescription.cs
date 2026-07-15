namespace PanguEngine.Graphics;

/// <summary>
/// Describes a texture resource to be created.
/// </summary>
public readonly record struct TextureDescription
{
    public TextureDescription()
    {
    }

    /// <summary>
    /// The texture dimensional shape; array textures are represented by <see cref="ArrayLayers"/>.
    /// </summary>
    public required TextureDimension Dimension { get; init; }

    /// <summary>
    /// The texture pixel format.
    /// </summary>
    public required TextureFormat Format { get; init; }

    /// <summary>
    /// The texture width in pixels.
    /// </summary>
    public required uint Width { get; init; }

    /// <summary>
    /// The texture height in pixels; 1D textures use a height of one.
    /// </summary>
    public required uint Height { get; init; }

    /// <summary>
    /// The texture depth in pixels; 1D and 2D textures use a depth of one.
    /// </summary>
    public required uint Depth { get; init; }

    /// <summary>
    /// The number of mip levels.
    /// </summary>
    public required uint MipLevels { get; init; }

    /// <summary>
    /// The number of array layers; 3D textures use one layer.
    /// </summary>
    public required uint ArrayLayers { get; init; }

    /// <summary>
    /// The usage flags for the texture.
    /// </summary>
    public required TextureUsage Usage { get; init; }

    /// <summary>
    /// The texture creation capability flags.
    /// </summary>
    public TextureCreateFlags Flags { get; init; } = TextureCreateFlags.None;
}