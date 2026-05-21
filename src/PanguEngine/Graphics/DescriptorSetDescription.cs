namespace PanguEngine.Graphics;

/// <summary>
/// Describes a descriptor set resource to create.
/// </summary>
/// <param name="Layout">The descriptor set layout.</param>
/// <param name="Bindings">The concrete descriptor bindings.</param>
public readonly record struct DescriptorSetDescription(
    DescriptorSetLayout Layout,
    ReadOnlyMemory<DescriptorSetBinding> Bindings);

/// <summary>
/// Describes a single buffer descriptor binding.
/// </summary>
/// <param name="Binding">The shader binding index.</param>
/// <param name="Buffer">The buffer to bind.</param>
/// <param name="Offset">The byte offset into the buffer.</param>
/// <param name="Size">The number of bytes to bind.</param>
public readonly record struct DescriptorSetBinding(uint Binding, Buffer Buffer, ulong Offset, ulong Size);