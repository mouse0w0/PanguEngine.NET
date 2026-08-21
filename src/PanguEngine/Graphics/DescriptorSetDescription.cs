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

    /// <summary>
    /// The descriptor array element.
    /// </summary>
    public uint ArrayElement { get; }

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
    /// <param name="arrayElement">The descriptor array element.</param>
    public DescriptorSetBinding(
        uint binding,
        Buffer buffer,
        ulong offset,
        ulong size,
        uint arrayElement = 0)
        : this(binding, arrayElement, DescriptorType.UniformBuffer, buffer, offset, size)
    {
    }

    private DescriptorSetBinding(
        uint binding,
        uint arrayElement,
        DescriptorType type,
        Buffer buffer,
        ulong offset,
        ulong size)
    {
        Binding = binding;
        Type = type;
        ArrayElement = arrayElement;
        Buffer = buffer;
        Offset = offset;
        Size = size;
        TextureView = null;
        Sampler = null;
    }

    private DescriptorSetBinding(uint binding, uint arrayElement, DescriptorType type, TextureView textureView)
    {
        Binding = binding;
        Type = type;
        ArrayElement = arrayElement;
        Buffer = null;
        Offset = 0;
        Size = 0;
        TextureView = textureView;
        Sampler = null;
    }

    private DescriptorSetBinding(uint binding, uint arrayElement, DescriptorType type, Sampler sampler)
    {
        Binding = binding;
        Type = type;
        ArrayElement = arrayElement;
        Buffer = null;
        Offset = 0;
        Size = 0;
        TextureView = null;
        Sampler = sampler;
    }

    private DescriptorSetBinding(uint binding, uint arrayElement, TextureView textureView, Sampler sampler)
    {
        Binding = binding;
        Type = DescriptorType.CombinedImageSampler;
        ArrayElement = arrayElement;
        Buffer = null;
        Offset = 0;
        Size = 0;
        TextureView = textureView;
        Sampler = sampler;
    }

    /// <summary>
    /// Creates a uniform buffer descriptor binding.
    /// </summary>
    /// <param name="binding">The shader binding index.</param>
    /// <param name="buffer">The buffer to bind.</param>
    /// <param name="offset">The byte offset into the buffer.</param>
    /// <param name="size">The number of bytes to bind.</param>
    /// <param name="arrayElement">The descriptor array element.</param>
    /// <returns>The descriptor binding.</returns>
    public static DescriptorSetBinding UniformBuffer(
        uint binding,
        Buffer buffer,
        ulong offset,
        ulong size,
        uint arrayElement = 0)
    {
        return new DescriptorSetBinding(binding, buffer, offset, size, arrayElement);
    }

    /// <summary>
    /// Creates a storage buffer descriptor binding.
    /// </summary>
    /// <param name="binding">The shader binding index.</param>
    /// <param name="buffer">The buffer to bind.</param>
    /// <param name="offset">The byte offset into the buffer.</param>
    /// <param name="size">The number of bytes to bind.</param>
    /// <param name="arrayElement">The descriptor array element.</param>
    /// <returns>The descriptor binding.</returns>
    public static DescriptorSetBinding StorageBuffer(
        uint binding,
        Buffer buffer,
        ulong offset,
        ulong size,
        uint arrayElement = 0)
    {
        return new DescriptorSetBinding(
            binding,
            arrayElement,
            DescriptorType.StorageBuffer,
            buffer,
            offset,
            size);
    }

    /// <summary>
    /// Creates a combined image sampler descriptor binding.
    /// </summary>
    /// <param name="binding">The shader binding index.</param>
    /// <param name="textureView">The texture view to bind.</param>
    /// <param name="sampler">The sampler to bind.</param>
    /// <param name="arrayElement">The descriptor array element.</param>
    /// <returns>The descriptor binding.</returns>
    public static DescriptorSetBinding CombinedImageSampler(
        uint binding,
        TextureView textureView,
        Sampler sampler,
        uint arrayElement = 0)
    {
        return new DescriptorSetBinding(binding, arrayElement, textureView, sampler);
    }

    /// <summary>
    /// Creates a sampled image descriptor binding for a fixed descriptor array element.
    /// </summary>
    /// <param name="binding">The shader binding index.</param>
    /// <param name="arrayElement">The descriptor array element.</param>
    /// <param name="textureView">The texture view to bind.</param>
    /// <returns>The descriptor binding.</returns>
    public static DescriptorSetBinding SampledImage(uint binding, uint arrayElement, TextureView textureView)
    {
        return new DescriptorSetBinding(binding, arrayElement, DescriptorType.SampledImage, textureView);
    }

    /// <summary>
    /// Creates a sampler descriptor binding for a fixed descriptor array element.
    /// </summary>
    /// <param name="binding">The shader binding index.</param>
    /// <param name="sampler">The sampler to bind.</param>
    /// <param name="arrayElement">The descriptor array element.</param>
    /// <returns>The descriptor binding.</returns>
    public static DescriptorSetBinding SamplerDescriptor(uint binding, Sampler sampler, uint arrayElement = 0)
    {
        return new DescriptorSetBinding(binding, arrayElement, DescriptorType.Sampler, sampler);
    }
}
