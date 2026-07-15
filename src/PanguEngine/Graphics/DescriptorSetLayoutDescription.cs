using System.Diagnostics.CodeAnalysis;

namespace PanguEngine.Graphics;

/// <summary>
/// Describes a descriptor set layout resource to create.
/// </summary>
public readonly record struct DescriptorSetLayoutDescription
{
    /// <summary>
    /// Creates a descriptor set layout description.
    /// </summary>
    /// <param name="bindings">The descriptor set layout bindings.</param>
    [SetsRequiredMembers]
    public DescriptorSetLayoutDescription(DescriptorSetLayoutBinding[] bindings)
    {
        Bindings = bindings;
    }

    /// <summary>
    /// The descriptor set layout bindings.
    /// </summary>
    public required DescriptorSetLayoutBinding[] Bindings { get; init; }
}