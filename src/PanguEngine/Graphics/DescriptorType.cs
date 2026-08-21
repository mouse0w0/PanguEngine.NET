namespace PanguEngine.Graphics;

/// <summary>
/// Describes the type of a shader-visible descriptor.
/// </summary>
public enum DescriptorType
{
    /// <summary>
    /// A uniform buffer binding.
    /// </summary>
    UniformBuffer,

    /// <summary>
    /// A storage buffer binding.
    /// </summary>
    StorageBuffer,

    /// <summary>
    /// A sampled texture and sampler binding.
    /// </summary>
    CombinedImageSampler,

    /// <summary>
    /// A sampled texture binding without an attached sampler.
    /// </summary>
    SampledImage,

    /// <summary>
    /// A sampler binding used with a separate sampled image.
    /// </summary>
    Sampler
}
