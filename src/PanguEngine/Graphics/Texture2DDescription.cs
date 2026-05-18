namespace PanguEngine.Graphics;

/// <summary>
/// Describes a two-dimensional texture to be created.
/// </summary>
/// <param name="Width">The texture width in pixels.</param>
/// <param name="Height">The texture height in pixels.</param>
/// <param name="Format">The texture pixel format.</param>
/// <param name="Usage">The usage flags for the texture.</param>
/// <param name="MipLevels">The number of mip levels.</param>
public readonly record struct Texture2DDescription(
    uint Width,
    uint Height,
    TextureFormat Format,
    TextureUsage Usage,
    uint MipLevels);