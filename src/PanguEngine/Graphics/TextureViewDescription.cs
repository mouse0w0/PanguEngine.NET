using System.Diagnostics.CodeAnalysis;

namespace PanguEngine.Graphics;

/// <summary>
/// Describes a texture view resource to be created.
/// </summary>
public readonly record struct TextureViewDescription
{
    /// <summary>
    /// Creates a texture view description.
    /// </summary>
    /// <param name="dimension">The texture view dimensional shape.</param>
    /// <param name="baseMipLevel">The first mip level included in the view.</param>
    /// <param name="mipLevels">The number of mip levels included in the view.</param>
    /// <param name="baseArrayLayer">The first array layer included in the view.</param>
    /// <param name="arrayLayers">The number of array layers included in the view.</param>
    [SetsRequiredMembers]
    public TextureViewDescription(
        TextureViewDimension dimension,
        uint baseMipLevel,
        uint mipLevels,
        uint baseArrayLayer,
        uint arrayLayers)
    {
        Dimension = dimension;
        BaseMipLevel = baseMipLevel;
        MipLevels = mipLevels;
        BaseArrayLayer = baseArrayLayer;
        ArrayLayers = arrayLayers;
    }

    /// <summary>
    /// The texture view dimensional shape.
    /// </summary>
    public required TextureViewDimension Dimension { get; init; }

    /// <summary>
    /// The first mip level included in the view.
    /// </summary>
    public required uint BaseMipLevel { get; init; }

    /// <summary>
    /// The number of mip levels included in the view.
    /// </summary>
    public required uint MipLevels { get; init; }

    /// <summary>
    /// The first array layer included in the view.
    /// </summary>
    public required uint BaseArrayLayer { get; init; }

    /// <summary>
    /// The number of array layers included in the view.
    /// </summary>
    public required uint ArrayLayers { get; init; }
}