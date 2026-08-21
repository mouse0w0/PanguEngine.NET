using System.Diagnostics.CodeAnalysis;

namespace PanguEngine.Graphics;

/// <summary>
/// Describes a single descriptor set layout binding.
/// </summary>
public readonly record struct DescriptorSetLayoutBinding
{
    /// <summary>
    /// Creates an empty descriptor set layout binding description.
    /// </summary>
    public DescriptorSetLayoutBinding()
    {
    }

    /// <summary>
    /// Creates a descriptor set layout binding.
    /// </summary>
    /// <param name="binding">The shader binding index.</param>
    /// <param name="type">The descriptor type.</param>
    /// <param name="stageFlags">The shader stages that can access the binding.</param>
    /// <param name="descriptorCount">The number of descriptors in the binding array.</param>
    [SetsRequiredMembers]
    public DescriptorSetLayoutBinding(
        uint binding,
        DescriptorType type,
        ShaderStageFlags stageFlags,
        uint descriptorCount = 1)
    {
        ArgumentOutOfRangeException.ThrowIfZero(descriptorCount);
        Binding = binding;
        Type = type;
        StageFlags = stageFlags;
        DescriptorCount = descriptorCount;
    }

    /// <summary>
    /// The shader binding index.
    /// </summary>
    public required uint Binding { get; init; }

    /// <summary>
    /// The descriptor type.
    /// </summary>
    public required DescriptorType Type { get; init; }

    /// <summary>
    /// The shader stages that can access the binding.
    /// </summary>
    public required ShaderStageFlags StageFlags { get; init; }

    /// <summary>
    /// The number of descriptors in the binding array. Defaults to 1.
    /// </summary>
    public uint DescriptorCount { get; } = 1;
}
