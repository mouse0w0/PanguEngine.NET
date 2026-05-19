namespace PanguEngine.Graphics;

/// <summary>
/// Describes sampler filtering, addressing, anisotropy, and LOD behavior.
/// </summary>
/// <param name="MinFilter">The filter used when sampling from a minified texture.</param>
/// <param name="MagFilter">The filter used when sampling from a magnified texture.</param>
/// <param name="MipmapMode">The filter used when selecting between mipmap levels.</param>
/// <param name="AddressU">The addressing mode for the U texture coordinate.</param>
/// <param name="AddressV">The addressing mode for the V texture coordinate.</param>
/// <param name="AddressW">The addressing mode for the W texture coordinate.</param>
/// <param name="MaxAnisotropy">The requested maximum anisotropy level.</param>
/// <param name="MinLod">The minimum mipmap level of detail.</param>
/// <param name="MaxLod">The maximum mipmap level of detail.</param>
/// <param name="MipLodBias">The mipmap level of detail bias.</param>
public readonly record struct SamplerDescription(
    FilterMode MinFilter,
    FilterMode MagFilter,
    MipmapMode MipmapMode,
    WrapMode AddressU,
    WrapMode AddressV,
    WrapMode AddressW,
    float MaxAnisotropy,
    float MinLod,
    float MaxLod,
    float MipLodBias);