namespace PanguEngine.Graphics;

/// <summary>
/// Describes a texture upload target region.
/// </summary>
/// <param name="X">The destination X offset within the mip level.</param>
/// <param name="Y">The destination Y offset within the mip level.</param>
/// <param name="Z">The destination Z offset within the mip level.</param>
/// <param name="Width">The destination region width.</param>
/// <param name="Height">The destination region height.</param>
/// <param name="Depth">The destination region depth.</param>
/// <param name="MipLevel">The destination mip level.</param>
/// <param name="ArrayLayer">The first destination array layer.</param>
/// <param name="LayerCount">The number of destination array layers.</param>
public readonly record struct TextureUploadRegion(
    uint X,
    uint Y,
    uint Z,
    uint Width,
    uint Height,
    uint Depth,
    uint MipLevel,
    uint ArrayLayer,
    uint LayerCount)
{
    /// <summary>
    /// Creates a region covering the entire first mip level of a 1D texture.
    /// </summary>
    public static TextureUploadRegion Full1D(uint width)
        => new(0, 0, 0, width, 1, 1, 0, 0, 1);

    /// <summary>
    /// Creates a region covering the entire area of a specific 1D mip level.
    /// </summary>
    public static TextureUploadRegion Mip1D(uint width, uint mipLevel)
        => new(0, 0, 0, width, 1, 1, mipLevel, 0, 1);

    /// <summary>
    /// Creates a region covering a 1D sub-range within a specific mip level and array layer.
    /// </summary>
    public static TextureUploadRegion Region1D(uint x, uint width, uint mipLevel = 0, uint arrayLayer = 0)
        => new(x, 0, 0, width, 1, 1, mipLevel, arrayLayer, 1);

    /// <summary>
    /// Creates a region covering the entire first mip level of a 2D texture.
    /// </summary>
    public static TextureUploadRegion Full2D(uint width, uint height)
        => new(0, 0, 0, width, height, 1, 0, 0, 1);

    /// <summary>
    /// Creates a region covering the entire area of a specific 2D mip level.
    /// </summary>
    public static TextureUploadRegion Mip2D(uint width, uint height, uint mipLevel)
        => new(0, 0, 0, width, height, 1, mipLevel, 0, 1);

    /// <summary>
    /// Creates a region covering the entire first mip level of a single 2D array layer.
    /// </summary>
    public static TextureUploadRegion Layer2D(uint width, uint height, uint arrayLayer)
        => new(0, 0, 0, width, height, 1, 0, arrayLayer, 1);

    /// <summary>
    /// Creates a region covering all layers of the first mip level of a 2D texture array.
    /// </summary>
    public static TextureUploadRegion Full2DArray(uint width, uint height, uint layerCount)
        => new(0, 0, 0, width, height, 1, 0, 0, layerCount);

    /// <summary>
    /// Creates a 2D sub-region within a specific mip level and array layer.
    /// </summary>
    public static TextureUploadRegion Region2D(
        uint x, uint y, uint width, uint height, uint mipLevel = 0, uint arrayLayer = 0)
        => new(x, y, 0, width, height, 1, mipLevel, arrayLayer, 1);

    /// <summary>
    /// Creates a region covering the entire first mip level of a 3D texture.
    /// </summary>
    public static TextureUploadRegion Full3D(uint width, uint height, uint depth, uint mipLevel = 0)
        => new(0, 0, 0, width, height, depth, mipLevel, 0, 1);

    /// <summary>
    /// Creates a 3D sub-volume within a specific mip level.
    /// </summary>
    public static TextureUploadRegion Region3D(
        uint x, uint y, uint z, uint width, uint height, uint depth, uint mipLevel = 0)
        => new(x, y, z, width, height, depth, mipLevel, 0, 1);
}