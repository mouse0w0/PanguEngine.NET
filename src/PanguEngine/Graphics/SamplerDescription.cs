using System.Diagnostics.CodeAnalysis;

namespace PanguEngine.Graphics;

/// <summary>
/// Describes sampler filtering, addressing, anisotropy, and LOD behavior.
/// </summary>
public readonly record struct SamplerDescription
{
    /// <summary>
    /// Creates a sampler description.
    /// </summary>
    /// <param name="minFilter">The filter used when sampling from a minified texture.</param>
    /// <param name="magFilter">The filter used when sampling from a magnified texture.</param>
    /// <param name="mipmapMode">The filter used when selecting between mipmap levels.</param>
    /// <param name="addressU">The addressing mode for the U texture coordinate.</param>
    /// <param name="addressV">The addressing mode for the V texture coordinate.</param>
    /// <param name="addressW">The addressing mode for the W texture coordinate.</param>
    /// <param name="maxAnisotropy">The requested maximum anisotropy level.</param>
    /// <param name="minLod">The minimum mipmap level of detail.</param>
    /// <param name="maxLod">The maximum mipmap level of detail.</param>
    /// <param name="mipLodBias">The mipmap level of detail bias.</param>
    [SetsRequiredMembers]
    public SamplerDescription(
        FilterMode minFilter,
        FilterMode magFilter,
        MipmapMode mipmapMode,
        WrapMode addressU,
        WrapMode addressV,
        WrapMode addressW,
        float maxAnisotropy,
        float minLod,
        float maxLod,
        float mipLodBias)
    {
        MinFilter = minFilter;
        MagFilter = magFilter;
        MipmapMode = mipmapMode;
        AddressU = addressU;
        AddressV = addressV;
        AddressW = addressW;
        MaxAnisotropy = maxAnisotropy;
        MinLod = minLod;
        MaxLod = maxLod;
        MipLodBias = mipLodBias;
    }

    /// <summary>
    /// The filter used when sampling from a minified texture.
    /// </summary>
    public required FilterMode MinFilter { get; init; }

    /// <summary>
    /// The filter used when sampling from a magnified texture.
    /// </summary>
    public required FilterMode MagFilter { get; init; }

    /// <summary>
    /// The filter used when selecting between mipmap levels.
    /// </summary>
    public required MipmapMode MipmapMode { get; init; }

    /// <summary>
    /// The addressing mode for the U texture coordinate.
    /// </summary>
    public required WrapMode AddressU { get; init; }

    /// <summary>
    /// The addressing mode for the V texture coordinate.
    /// </summary>
    public required WrapMode AddressV { get; init; }

    /// <summary>
    /// The addressing mode for the W texture coordinate.
    /// </summary>
    public required WrapMode AddressW { get; init; }

    /// <summary>
    /// The requested maximum anisotropy level.
    /// </summary>
    public required float MaxAnisotropy { get; init; }

    /// <summary>
    /// The minimum mipmap level of detail.
    /// </summary>
    public required float MinLod { get; init; }

    /// <summary>
    /// The maximum mipmap level of detail.
    /// </summary>
    public required float MaxLod { get; init; }

    /// <summary>
    /// The mipmap level of detail bias.
    /// </summary>
    public required float MipLodBias { get; init; }
}