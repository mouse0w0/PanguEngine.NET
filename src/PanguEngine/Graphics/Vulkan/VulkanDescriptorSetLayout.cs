using Silk.NET.Vulkan;
using VKDescriptorSetLayoutBinding = Silk.NET.Vulkan.DescriptorSetLayoutBinding;
using VKDescriptorSetLayout = Silk.NET.Vulkan.DescriptorSetLayout;
using VkDescriptorType = Silk.NET.Vulkan.DescriptorType;

namespace PanguEngine.Graphics.Vulkan;

/// <summary>
/// Vulkan implementation of <see cref="DescriptorSetLayout"/>.
/// </summary>
internal sealed unsafe class VulkanDescriptorSetLayout : DescriptorSetLayout
{
    private bool _destroyed;

    public VulkanDescriptorSetLayout(in DescriptorSetLayoutDescription description)
    {
        var bindings = description.Bindings.Span;
        if (bindings.Length == 0)
            throw new ArgumentException("Descriptor set layout must contain at least one binding.",
                nameof(description));

        var vulkanBindings = new VKDescriptorSetLayoutBinding[bindings.Length];
        for (var i = 0; i < bindings.Length; i++)
        {
            for (var previous = 0; previous < i; previous++)
            {
                if (bindings[previous].Binding == bindings[i].Binding)
                    throw new ArgumentException(
                        "Descriptor set layout bindings must not contain duplicate binding indices.",
                        nameof(description));
            }

            vulkanBindings[i] = new VKDescriptorSetLayoutBinding
            {
                Binding = bindings[i].Binding,
                DescriptorType = ToVulkanDescriptorType(bindings[i].Type),
                DescriptorCount = 1,
                StageFlags = ToVulkanShaderStageFlags(bindings[i].Stages),
            };
        }

        fixed (VKDescriptorSetLayoutBinding* pBindings = vulkanBindings)
        {
            DescriptorSetLayoutCreateInfo layoutInfo = new()
            {
                SType = StructureType.DescriptorSetLayoutCreateInfo,
                BindingCount = (uint)bindings.Length,
                PBindings = pBindings,
            };

            if (VulkanContext.Vk.CreateDescriptorSetLayout(VulkanContext.Device, in layoutInfo, null, out var layout) !=
                Result.Success)
                throw new InvalidOperationException("Failed to create Vulkan descriptor set layout.");

            DescriptorSetLayout = layout;
        }

        Bindings = description.Bindings.ToArray();
    }

    /// <inheritdoc/>
    public override bool IsDestroyed => _destroyed;

    /// <summary>
    /// Gets the Vulkan descriptor set layout handle.
    /// </summary>
    internal VKDescriptorSetLayout DescriptorSetLayout { get; private set; }

    /// <summary>
    /// Gets the layout entries.
    /// </summary>
    internal IReadOnlyList<DescriptorSetLayoutBinding> Bindings { get; }

    /// <inheritdoc/>
    public override void Destroy()
    {
        if (_destroyed)
            return;

        if (DescriptorSetLayout.Handle != 0)
        {
            var descriptorSetLayout = DescriptorSetLayout;
            DescriptorSetLayout = default;
            var retireValue = VulkanContext.GlobalTimelineValue + VulkanContext.MaxFramesInFlight;
            VulkanDeletionQueue.Enqueue(retireValue,
                () => VulkanContext.Vk.DestroyDescriptorSetLayout(VulkanContext.Device, descriptorSetLayout, null));
        }

        _destroyed = true;
    }

    internal DescriptorSetLayoutBinding GetBinding(uint binding)
    {
        foreach (var layoutBinding in Bindings)
        {
            if (layoutBinding.Binding == binding)
                return layoutBinding;
        }

        throw new ArgumentException("Descriptor set binding does not exist in the layout.", nameof(binding));
    }

    private static VkDescriptorType ToVulkanDescriptorType(DescriptorType type)
    {
        return type switch
        {
            DescriptorType.UniformBuffer => VkDescriptorType.UniformBuffer,
            DescriptorType.CombinedImageSampler => VkDescriptorType.CombinedImageSampler,
            _ => throw new ArgumentOutOfRangeException(nameof(type), "Unsupported descriptor type."),
        };
    }

    private static ShaderStageFlags ToVulkanShaderStageFlags(ShaderStage stage)
    {
        var result = default(ShaderStageFlags);
        if (stage.HasFlag(ShaderStage.Vertex))
            result |= ShaderStageFlags.VertexBit;
        if (stage.HasFlag(ShaderStage.Fragment))
            result |= ShaderStageFlags.FragmentBit;

        return result;
    }
}