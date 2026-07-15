namespace PanguEngine.Graphics;

/// <summary>
/// Describes a texture view resource to be created.
/// </summary>
/// <param name="Dimension">The texture view dimensional shape.</param>
/// <param name="BaseMipLevel">The first mip level included in the view.</param>
/// <param name="MipLevels">The number of mip levels included in the view.</param>
/// <param name="BaseArrayLayer">The first array layer included in the view.</param>
/// <param name="ArrayLayers">The number of array layers included in the view.</param>
public readonly record struct TextureViewDescription(
    TextureViewDimension Dimension,
    uint BaseMipLevel,
    uint MipLevels,
    uint BaseArrayLayer,
    uint ArrayLayers);