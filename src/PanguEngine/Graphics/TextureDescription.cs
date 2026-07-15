namespace PanguEngine.Graphics;

/// <summary>
/// Describes a texture resource to be created.
/// </summary>
/// <param name="Dimension">The texture dimensional shape; array textures are represented by <paramref name="ArrayLayers"/>.</param>
/// <param name="Format">The texture pixel format.</param>
/// <param name="Width">The texture width in pixels.</param>
/// <param name="Height">The texture height in pixels; 1D textures use a height of one.</param>
/// <param name="Depth">The texture depth in pixels; 1D and 2D textures use a depth of one.</param>
/// <param name="MipLevels">The number of mip levels.</param>
/// <param name="ArrayLayers">The number of array layers; 3D textures use one layer.</param>
/// <param name="Usage">The usage flags for the texture.</param>
/// <param name="Flags">The texture creation capability flags.</param>
public readonly record struct TextureDescription(
    TextureDimension Dimension,
    TextureFormat Format,
    uint Width,
    uint Height,
    uint Depth,
    uint MipLevels,
    uint ArrayLayers,
    TextureUsage Usage,
    TextureCreateFlags Flags = TextureCreateFlags.None);