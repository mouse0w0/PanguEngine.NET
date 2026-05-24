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
    uint LayerCount);