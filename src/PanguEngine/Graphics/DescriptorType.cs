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
    /// A sampled texture and sampler binding.
    /// </summary>
    CombinedImageSampler
}