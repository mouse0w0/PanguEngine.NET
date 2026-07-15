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
        layout.ThrowIfDestroyed();

        Layout = layout;

        var bindings = description.Bindings;
        if (bindings.Length == 0)
            throw new ArgumentException("Descriptor set must contain at least one binding.", nameof(description));
        if (bindings.Length != layout.Bindings.Count)
            throw new ArgumentException("Descriptor set bindings must match the descriptor set layout bindings.",
                nameof(description));

        var poolSizes = CreateDescriptorPoolSizes(layout.Bindings);

        fixed (DescriptorPoolSize* pPoolSizes = poolSizes)
        {
            DescriptorPoolCreateInfo poolInfo = new()
            {
                SType = StructureType.DescriptorPoolCreateInfo,
                MaxSets = 1,
                PoolSizeCount = (uint)poolSizes.Length,
                PPoolSizes = pPoolSizes
            };

            if (VulkanContext.Vk.CreateDescriptorPool(VulkanContext.Device, in poolInfo, null, out var pool) !=
                Result.Success)
                throw new InvalidOperationException("Failed to create Vulkan descriptor pool.");

            DescriptorPool = pool;
        }

        try
        {
            var descriptorSetLayout = layout.DescriptorSetLayout;
            DescriptorSetAllocateInfo allocateInfo = new()
            {
                SType = StructureType.DescriptorSetAllocateInfo,
                DescriptorPool = DescriptorPool,
                DescriptorSetCount = 1,
                PSetLayouts = &descriptorSetLayout
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
        var imageInfos = new DescriptorImageInfo[bindings.Length];
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
            if (layoutBinding.Type != bindings[i].Type)
                throw new ArgumentOutOfRangeException(nameof(bindings),
                    "Descriptor set binding type must match the descriptor set layout binding type.");

            writes[i] = new WriteDescriptorSet
            {
                SType = StructureType.WriteDescriptorSet,
                DstSet = Handle,
                DstBinding = bindings[i].Binding,
                DstArrayElement = 0,
                DescriptorCount = 1,
                DescriptorType = ToVulkanDescriptorType(bindings[i].Type)
            };

            switch (bindings[i].Type)
            {
                case DescriptorType.UniformBuffer:
                    WriteUniformBufferDescriptor(bindings[i], bufferInfos, i);
                    break;
                case DescriptorType.CombinedImageSampler:
                    WriteCombinedImageSamplerDescriptor(bindings[i], imageInfos, i);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(bindings), "Unsupported descriptor type.");
            }
        }

        fixed (DescriptorBufferInfo* pBufferInfos = bufferInfos)
        fixed (DescriptorImageInfo* pImageInfos = imageInfos)
        fixed (WriteDescriptorSet* pWrites = writes)
        {
            for (var i = 0; i < bindings.Length; i++)
            {
                switch (bindings[i].Type)
                {
                    case DescriptorType.UniformBuffer:
                        pWrites[i].PBufferInfo = pBufferInfos + i;
                        break;
                    case DescriptorType.CombinedImageSampler:
                        pWrites[i].PImageInfo = pImageInfos + i;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(bindings), "Unsupported descriptor type.");
                }
            }

            VulkanContext.Vk.UpdateDescriptorSets(VulkanContext.Device, (uint)bindings.Length, pWrites, 0, null);
        }
    }

    private static DescriptorPoolSize[] CreateDescriptorPoolSizes(IReadOnlyList<DescriptorSetLayoutBinding> bindings)
    {
        var poolSizes = new List<DescriptorPoolSize>();
        foreach (var binding in bindings)
        {
            var descriptorType = ToVulkanDescriptorType(binding.Type);
            var found = false;
            for (var i = 0; i < poolSizes.Count; i++)
            {
                if (poolSizes[i].Type != descriptorType)
                    continue;

                var poolSize = poolSizes[i];
                poolSize.DescriptorCount++;
                poolSizes[i] = poolSize;
                found = true;
                break;
            }

            if (!found)
            {
                poolSizes.Add(new DescriptorPoolSize
                {
                    Type = descriptorType,
                    DescriptorCount = 1
                });
            }
        }

        return poolSizes.ToArray();
    }

    private static void WriteUniformBufferDescriptor(
        DescriptorSetBinding binding,
        DescriptorBufferInfo[] bufferInfos,
        int index)
    {
        var buffer = binding.Buffer as VulkanBuffer
                     ?? throw new InvalidOperationException(
                         "Descriptor set buffer was not created by the Vulkan backend.");
        buffer.ThrowIfDestroyed();
        if (!buffer.Usage.HasFlag(BufferUsageFlags.UniformBufferBit))
            throw new InvalidOperationException("Descriptor set buffer was not created with Uniform usage.");
        if (binding.Size == 0)
            throw new ArgumentOutOfRangeException(nameof(binding),
                "Descriptor set buffer binding size must be greater than zero.");
        if (binding.Offset > buffer.Size || binding.Size > buffer.Size - binding.Offset)
            throw new ArgumentOutOfRangeException(nameof(binding),
                "Descriptor set buffer offset and size exceed the buffer bounds.");

        var uniformAlignment = VulkanContext.MinUniformBufferOffsetAlignment;
        if (uniformAlignment == 0)
            throw new InvalidOperationException(
                "VulkanContext.MinUniformBufferOffsetAlignment is 0. Ensure VulkanContext is initialized.");
        if (binding.Offset % uniformAlignment != 0)
            throw new ArgumentOutOfRangeException(nameof(binding),
                "Uniform buffer binding offset must satisfy the device uniform buffer alignment requirement.");

        bufferInfos[index] = new DescriptorBufferInfo
        {
            Buffer = buffer.Buffer,
            Offset = binding.Offset,
            Range = binding.Size
        };
    }

    private static void WriteCombinedImageSamplerDescriptor(
        DescriptorSetBinding binding,
        DescriptorImageInfo[] imageInfos,
        int index)
    {
        var textureView = binding.TextureView as VulkanTextureView
                          ?? throw new InvalidOperationException(
                              "Descriptor set texture view was not created by the Vulkan backend.");
        var sampler = binding.Sampler as VulkanSampler
                      ?? throw new InvalidOperationException(
                          "Descriptor set sampler was not created by the Vulkan backend.");
        textureView.ThrowIfDestroyed();
        var texture = textureView.VulkanTexture;
        texture.ThrowIfDestroyed();
        sampler.ThrowIfDestroyed();
        if (!texture.Usage.HasFlag(TextureUsage.Sampled))
            throw new InvalidOperationException("Descriptor set texture was not created with Sampled usage.");

        imageInfos[index] = new DescriptorImageInfo
        {
            ImageView = textureView.ImageView,
            Sampler = sampler.Handle,
            ImageLayout = ImageLayout.ShaderReadOnlyOptimal
        };
    }

    private static VkDescriptorType ToVulkanDescriptorType(DescriptorType type)
    {
        return type switch
        {
            DescriptorType.UniformBuffer => VkDescriptorType.UniformBuffer,
            DescriptorType.CombinedImageSampler => VkDescriptorType.CombinedImageSampler,
            _ => throw new ArgumentOutOfRangeException(nameof(type), "Unsupported descriptor type.")
        };
    }
}