using System.Diagnostics.CodeAnalysis;

namespace PanguEngine.Graphics;

/// <summary>
/// Describes a descriptor set resource to create.
/// </summary>
public readonly record struct DescriptorSetDescription
{
    /// <summary>
    /// Creates a descriptor set description.
    /// </summary>
    /// <param name="layout">The descriptor set layout.</param>
    /// <param name="bindings">The concrete descriptor bindings.</param>
    [SetsRequiredMembers]
    public DescriptorSetDescription(DescriptorSetLayout layout, DescriptorSetBinding[] bindings)
    {
        Layout = layout;
        Bindings = bindings;
    }

    /// <summary>
    /// The descriptor set layout.
    /// </summary>
    public required DescriptorSetLayout Layout { get; init; }

    /// <summary>
    /// The concrete descriptor bindings.
    /// </summary>
    public required DescriptorSetBinding[] Bindings { get; init; }
}

/// <summary>
/// Describes a single descriptor binding.
/// </summary>
public readonly record struct DescriptorSetBinding
{
    public uint Binding { get; }

    public DescriptorType Type { get; }

    public Buffer? Buffer { get; }

    public ulong Offset { get; }

    public ulong Size { get; }

    public TextureView? TextureView { get; }

    public Sampler? Sampler { get; }

    /// <summary>
    /// Describes a single buffer descriptor binding.
    /// </summary>
    /// <param name="binding">The shader binding index.</param>
    /// <param name="buffer">The buffer to bind.</param>
    /// <param name="offset">The byte offset into the buffer.</param>
    /// <param name="size">The number of bytes to bind.</param>
    public DescriptorSetBinding(uint binding, Buffer buffer, ulong offset, ulong size)
    {
        Binding = binding;
        Type = DescriptorType.UniformBuffer;
        Buffer = buffer;
        Offset = offset;
        Size = size;
        TextureView = null;
        Sampler = null;
    }

    private DescriptorSetBinding(uint binding, TextureView textureView, Sampler sampler)
    {
        Binding = binding;
        Type = DescriptorType.CombinedImageSampler;
        Buffer = null;
        Offset = 0;
        Size = 0;
        TextureView = textureView;
        Sampler = sampler;
    }

    public static DescriptorSetBinding UniformBuffer(uint binding, Buffer buffer, ulong offset, ulong size)
    {
        return new DescriptorSetBinding(binding, buffer, offset, size);
    }

    public static DescriptorSetBinding CombinedImageSampler(uint binding, TextureView textureView, Sampler sampler)
    {
        return new DescriptorSetBinding(binding, textureView, sampler);
    }
}