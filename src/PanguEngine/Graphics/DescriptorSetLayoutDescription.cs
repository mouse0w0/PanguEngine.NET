namespace PanguEngine.Graphics;

/// <summary>
/// Describes a descriptor set layout resource to create.
/// </summary>
/// <param name="Bindings">The descriptor set layout bindings.</param>
public readonly record struct DescriptorSetLayoutDescription(DescriptorSetLayoutBinding[] Bindings);

/// <summary>
/// Describes a single descriptor set layout binding.
/// </summary>
/// <param name="Binding">The shader binding index.</param>
/// <param name="Type">The descriptor type.</param>
/// <param name="Stages">The shader stages that can access the binding.</param>
public readonly record struct DescriptorSetLayoutBinding(uint Binding, DescriptorType Type, ShaderStage Stages);