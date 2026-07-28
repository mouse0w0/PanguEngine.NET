namespace PanguEngine.Graphics;

/// <summary>
/// Represents a completed single-page RGBA texture atlas.
/// </summary>
/// <typeparam name="TKey">The type used to identify source images.</typeparam>
public sealed class TextureAtlas<TKey> where TKey : notnull
{
    private readonly Dictionary<TKey, TextureAtlasRegion> _regions;
    private readonly byte[][] _mipPixels;

    /// <summary>
    /// Creates a completed texture atlas.
    /// </summary>
    /// <param name="width">The atlas width in pixels.</param>
    /// <param name="height">The atlas height in pixels.</param>
    /// <param name="mipPixels">The row-major RGBA pixel data for each mip level.</param>
    /// <param name="regions">The source image regions.</param>
    internal TextureAtlas(
        int width,
        int height,
        IReadOnlyList<byte[]> mipPixels,
        IReadOnlyDictionary<TKey, TextureAtlasRegion> regions)
    {
        Width = width;
        Height = height;
        _mipPixels = [.. mipPixels];
        Pixels = _mipPixels[0];
        var regionCopy = new Dictionary<TKey, TextureAtlasRegion>(regions.Count);
        foreach (var region in regions)
            regionCopy.Add(region.Key, region.Value);
        _regions = regionCopy;
    }

    /// <summary>
    /// Gets the atlas width in pixels.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the atlas height in pixels.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Gets the number of mip levels stored by the atlas.
    /// </summary>
    public int MipLevels => _mipPixels.Length;

    /// <summary>
    /// Gets the base mip level row-major RGBA pixel data as a read-only memory view.
    /// </summary>
    public ReadOnlyMemory<byte> Pixels { get; }

    /// <summary>
    /// Gets the row-major RGBA pixel data for a mip level.
    /// </summary>
    /// <param name="mipLevel">The zero-based mip level.</param>
    /// <returns>The mip level pixel data.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the mip level is outside the atlas mip range.</exception>
    public ReadOnlyMemory<byte> GetMipPixels(int mipLevel)
    {
        if ((uint)mipLevel >= (uint)_mipPixels.Length)
            throw new ArgumentOutOfRangeException(nameof(mipLevel));

        return _mipPixels[mipLevel];
    }

    /// <summary>
    /// Gets the region associated with a source image key.
    /// </summary>
    /// <param name="key">The source image key.</param>
    /// <returns>The source image region.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the key is not in the atlas.</exception>
    public TextureAtlasRegion GetRegion(TKey key)
    {
        if (_regions.TryGetValue(key, out var region))
            return region;

        throw new KeyNotFoundException($"Texture atlas does not contain key '{key}'.");
    }
}