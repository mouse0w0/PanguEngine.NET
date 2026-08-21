using Silk.NET.Vulkan;
using VKDescriptorSetLayoutBinding = Silk.NET.Vulkan.DescriptorSetLayoutBinding;
using VKDescriptorSetLayout = Silk.NET.Vulkan.DescriptorSetLayout;

namespace PanguEngine.Graphics.Vulkan;

/// <summary>
/// Vulkan implementation of <see cref="DescriptorSetLayout"/>.
/// </summary>
internal sealed unsafe class VulkanDescriptorSetLayout : DescriptorSetLayout
{
    public VulkanDescriptorSetLayout(in DescriptorSetLayoutDescription description)
    {
        VulkanContext.EnsureRenderThread();
        var bindings = description.Bindings;
        if (bindings.Length == 0)
            throw new ArgumentException("Descriptor set layout must contain at least one binding.",
                nameof(description));

        var vulkanBindings = new VKDescriptorSetLayoutBinding[bindings.Length];
        for (var i = 0; i < bindings.Length; i++)
        {
            if (bindings[i].DescriptorCount == 0)
                throw new ArgumentOutOfRangeException(
                    nameof(description),
                    "Descriptor set layout binding count must be greater than zero.");
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
                DescriptorType = VulkanMapping.ToVulkanDescriptorType(bindings[i].Type),
                DescriptorCount = bindings[i].DescriptorCount,
                StageFlags = VulkanMapping.ToVulkanShaderStageFlags(bindings[i].StageFlags)
            };
        }

        fixed (VKDescriptorSetLayoutBinding* pBindings = vulkanBindings)
        {
            DescriptorSetLayoutCreateInfo layoutInfo = new()
            {
                SType = StructureType.DescriptorSetLayoutCreateInfo,
                BindingCount = (uint)bindings.Length,
                PBindings = pBindings
            };

            if (VulkanContext.Vk.CreateDescriptorSetLayout(VulkanContext.Device, in layoutInfo, null, out var layout) !=
                Result.Success)
                throw new InvalidOperationException("Failed to create Vulkan descriptor set layout.");

            DescriptorSetLayout = layout;
        }

        Bindings = [.. bindings];
    }

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
        VulkanContext.EnsureRenderThread();
        if (IsDestroyed)
            return;
        MarkDestroyed();

        if (DescriptorSetLayout.Handle != 0)
        {
            VulkanContext.Vk.DestroyDescriptorSetLayout(VulkanContext.Device, DescriptorSetLayout, null);
            DescriptorSetLayout = default;
        }
    }
}
