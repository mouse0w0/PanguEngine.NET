using Silk.NET.Vulkan;
using VkDescriptorSet = Silk.NET.Vulkan.DescriptorSet;
using VkDescriptorType = Silk.NET.Vulkan.DescriptorType;

namespace PanguEngine.Graphics.Vulkan;

/// <summary>
/// Vulkan implementation of <see cref="DescriptorSet"/>.
/// </summary>
internal sealed unsafe class VulkanDescriptorSet : DescriptorSet
{
    private bool _destroyed;

    public VulkanDescriptorSet(in DescriptorSetDescription description)
    {
        var layout = description.Layout as VulkanDescriptorSetLayout
                     ?? throw new InvalidOperationException(
                         "Descriptor set layout was not created by the Vulkan backend.");
        if (layout.IsDestroyed)
            throw new ObjectDisposedException(nameof(VulkanDescriptorSetLayout));

        Layout = layout;

        var bindings = description.Bindings.Span;
        if (bindings.Length == 0)
            throw new ArgumentException("Descriptor set must contain at least one binding.", nameof(description));
        if (bindings.Length != layout.Bindings.Count)
            throw new ArgumentException("Descriptor set bindings must match the descriptor set layout bindings.",
                nameof(description));

        DescriptorPoolSize poolSize = new()
        {
            Type = VkDescriptorType.UniformBuffer,
            DescriptorCount = (uint)bindings.Length,
        };

        DescriptorPoolCreateInfo poolInfo = new()
        {
            SType = StructureType.DescriptorPoolCreateInfo,
            MaxSets = 1,
            PoolSizeCount = 1,
            PPoolSizes = &poolSize,
        };

        if (VulkanContext.Vk.CreateDescriptorPool(VulkanContext.Device, in poolInfo, null, out var pool) !=
            Result.Success)
            throw new InvalidOperationException("Failed to create Vulkan descriptor pool.");

        DescriptorPool = pool;

        try
        {
            var descriptorSetLayout = layout.DescriptorSetLayout;
            DescriptorSetAllocateInfo allocateInfo = new()
            {
                SType = StructureType.DescriptorSetAllocateInfo,
                DescriptorPool = DescriptorPool,
                DescriptorSetCount = 1,
                PSetLayouts = &descriptorSetLayout,
            };

            if (VulkanContext.Vk.AllocateDescriptorSets(VulkanContext.Device, in allocateInfo, out var descriptorSet) !=
                Result.Success)
                throw new InvalidOperationException("Failed to allocate Vulkan descriptor set.");

            Handle = descriptorSet;
            UpdateDescriptorSet(layout, bindings);
        }
        catch
        {
            VulkanContext.Vk.DestroyDescriptorPool(VulkanContext.Device, DescriptorPool, null);
            DescriptorPool = default;
            throw;
        }
    }

    /// <inheritdoc/>
    public override bool IsDestroyed => _destroyed;

    /// <summary>
    /// Gets the Vulkan descriptor set handle.
    /// </summary>
    internal VkDescriptorSet Handle { get; private set; }

    /// <summary>
    /// Gets the descriptor set layout used to create this descriptor set.
    /// </summary>
    internal VulkanDescriptorSetLayout Layout { get; }

    private DescriptorPool DescriptorPool { get; set; }

    /// <inheritdoc/>
    public override void Destroy()
    {
        if (_destroyed)
            return;
        _destroyed = true;

        var descriptorPool = DescriptorPool;
        DescriptorPool = default;
        Handle = default;
        var retireValue = VulkanContext.GlobalTimelineValue + VulkanContext.MaxFramesInFlight;
        VulkanDeletionQueue.Enqueue(retireValue,
            () => VulkanContext.Vk.DestroyDescriptorPool(VulkanContext.Device, descriptorPool, null));
    }

    private void UpdateDescriptorSet(VulkanDescriptorSetLayout layout, ReadOnlySpan<DescriptorSetBinding> bindings)
    {
        var bufferInfos = new DescriptorBufferInfo[bindings.Length];
        var writes = new WriteDescriptorSet[bindings.Length];

        for (var i = 0; i < bindings.Length; i++)
        {
            for (var previous = 0; previous < i; previous++)
            {
                if (bindings[previous].Binding == bindings[i].Binding)
                    throw new ArgumentException("Descriptor set bindings must not contain duplicate binding indices.",
                        nameof(bindings));
            }

            var layoutBinding = layout.GetBinding(bindings[i].Binding);
            if (layoutBinding.Type != DescriptorType.UniformBuffer)
                throw new ArgumentOutOfRangeException(nameof(bindings),
                    "Only uniform buffer descriptors are supported.");

            var buffer = bindings[i].Buffer as VulkanBuffer
                         ?? throw new InvalidOperationException(
                             "Descriptor set buffer was not created by the Vulkan backend.");
            if (buffer.IsDestroyed)
                throw new ObjectDisposedException(nameof(VulkanBuffer));
            if (!buffer.Usage.HasFlag(BufferUsageFlags.UniformBufferBit))
                throw new InvalidOperationException("Descriptor set buffer was not created with Uniform usage.");
            if (bindings[i].Size == 0)
                throw new ArgumentOutOfRangeException(nameof(bindings),
                    "Descriptor set buffer binding size must be greater than zero.");
            if (bindings[i].Offset > buffer.Size || bindings[i].Size > buffer.Size - bindings[i].Offset)
                throw new ArgumentOutOfRangeException(nameof(bindings),
                    "Descriptor set buffer offset and size exceed the buffer bounds.");

            var uniformAlignment = VulkanContext.MinUniformBufferOffsetAlignment;
            if (uniformAlignment == 0)
                throw new InvalidOperationException(
                    "VulkanContext.MinUniformBufferOffsetAlignment is 0. Ensure VulkanContext is initialized.");
            if (bindings[i].Offset % uniformAlignment != 0)
                throw new ArgumentOutOfRangeException(nameof(bindings),
                    "Uniform buffer binding offset must satisfy the device uniform buffer alignment requirement.");

            bufferInfos[i] = new DescriptorBufferInfo
            {
                Buffer = buffer.Buffer,
                Offset = bindings[i].Offset,
                Range = bindings[i].Size,
            };

            writes[i] = new WriteDescriptorSet
            {
                SType = StructureType.WriteDescriptorSet,
                DstSet = Handle,
                DstBinding = bindings[i].Binding,
                DstArrayElement = 0,
                DescriptorCount = 1,
                DescriptorType = VkDescriptorType.UniformBuffer,
            };
        }

        fixed (DescriptorBufferInfo* pBufferInfos = bufferInfos)
        fixed (WriteDescriptorSet* pWrites = writes)
        {
            for (var i = 0; i < bindings.Length; i++)
                pWrites[i].PBufferInfo = pBufferInfos + i;

            VulkanContext.Vk.UpdateDescriptorSets(VulkanContext.Device, (uint)bindings.Length, pWrites, 0, null);
        }
    }
}