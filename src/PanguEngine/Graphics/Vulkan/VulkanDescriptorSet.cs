using Silk.NET.Vulkan;
using VkDescriptorSet = Silk.NET.Vulkan.DescriptorSet;

namespace PanguEngine.Graphics.Vulkan;

/// <summary>
/// Vulkan implementation of <see cref="DescriptorSet"/>.
/// </summary>
internal sealed unsafe class VulkanDescriptorSet : DescriptorSet
{
    public VulkanDescriptorSet(in DescriptorSetDescription description)
    {
        VulkanContext.EnsureRenderThread();
        var layout = description.Layout as VulkanDescriptorSetLayout
                     ?? throw new InvalidOperationException(
                         "Descriptor set layout was not created by the Vulkan backend.");
        layout.ThrowIfDestroyed();

        Layout = layout;

        var bindings = description.Bindings;
        _bindingState = new VulkanDescriptorSetBindingState(layout.Bindings, bindings);

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
            UpdateDescriptorSet(bindings);
            ReferencedResources = GetReferencedResources(bindings);
            var descriptorPool = DescriptorPool;
            Lifetime = new VulkanResourceLifetime(
                this,
                () => VulkanContext.Vk.DestroyDescriptorPool(VulkanContext.Device, descriptorPool, null),
                VulkanDeletionQueue.Enqueue);
        }
        catch
        {
            VulkanContext.Vk.DestroyDescriptorPool(VulkanContext.Device, DescriptorPool, null);
            throw;
        }
    }

    /// <summary>
    /// Gets the Vulkan descriptor set handle.
    /// </summary>
    internal VkDescriptorSet Handle { get; }

    /// <summary>
    /// Gets the descriptor set layout used to create this descriptor set.
    /// </summary>
    internal VulkanDescriptorSetLayout Layout { get; }

    internal VulkanResourceLifetime Lifetime { get; }

    internal IReadOnlyList<VulkanResourceLifetime> ReferencedResources { get; private set; }

    private readonly VulkanDescriptorSetBindingState _bindingState;

    private DescriptorPool DescriptorPool { get; }

    /// <inheritdoc/>
    public override void Destroy()
    {
        VulkanContext.EnsureRenderThread();
        if (IsDestroyed)
            return;
        MarkDestroyed();
        Lifetime.RequestDestroy();
    }

    /// <inheritdoc/>
    public override void Update(DescriptorSetBinding[] bindings)
    {
        VulkanContext.EnsureRenderThread();
        ThrowIfDestroyed();
        ArgumentNullException.ThrowIfNull(bindings);
        var candidate = _bindingState.CreateUpdatedBindings(bindings);
        UpdateDescriptorSet(bindings);
        _bindingState.Commit(candidate);
        ReferencedResources = GetReferencedResources(candidate);
    }

    private static VulkanResourceLifetime[] GetReferencedResources(ReadOnlySpan<DescriptorSetBinding> bindings)
    {
        var resources = new HashSet<VulkanResourceLifetime>(ReferenceEqualityComparer.Instance);
        foreach (var binding in bindings)
        {
            switch (binding.Type)
            {
                case DescriptorType.UniformBuffer:
                case DescriptorType.StorageBuffer:
                    resources.Add(((VulkanBuffer)binding.Buffer!).Lifetime);
                    break;
                case DescriptorType.CombinedImageSampler:
                {
                    var view = (VulkanTextureView)binding.TextureView!;
                    resources.Add(view.Lifetime);
                    resources.Add(view.VulkanTexture.Lifetime);
                    resources.Add(((VulkanSampler)binding.Sampler!).Lifetime);
                    break;
                }
                case DescriptorType.SampledImage:
                {
                    var view = (VulkanTextureView)binding.TextureView!;
                    resources.Add(view.Lifetime);
                    resources.Add(view.VulkanTexture.Lifetime);
                    break;
                }
                case DescriptorType.Sampler:
                    resources.Add(((VulkanSampler)binding.Sampler!).Lifetime);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(bindings), "Unsupported descriptor type.");
            }
        }

        return [.. resources];
    }

    private void UpdateDescriptorSet(ReadOnlySpan<DescriptorSetBinding> bindings)
    {
        var bufferInfos = new DescriptorBufferInfo[bindings.Length];
        var imageInfos = new DescriptorImageInfo[bindings.Length];
        var writes = new WriteDescriptorSet[bindings.Length];

        for (var i = 0; i < bindings.Length; i++)
        {
            writes[i] = new WriteDescriptorSet
            {
                SType = StructureType.WriteDescriptorSet,
                DstSet = Handle,
                DstBinding = bindings[i].Binding,
                DstArrayElement = bindings[i].ArrayElement,
                DescriptorCount = 1,
                DescriptorType = VulkanMapping.ToVulkanDescriptorType(bindings[i].Type)
            };

            switch (bindings[i].Type)
            {
                case DescriptorType.UniformBuffer:
                    WriteUniformBufferDescriptor(bindings[i], bufferInfos, i);
                    break;
                case DescriptorType.StorageBuffer:
                    WriteStorageBufferDescriptor(bindings[i], bufferInfos, i);
                    break;
                case DescriptorType.CombinedImageSampler:
                    WriteCombinedImageSamplerDescriptor(bindings[i], imageInfos, i);
                    break;
                case DescriptorType.SampledImage:
                    WriteSampledImageDescriptor(bindings[i], imageInfos, i);
                    break;
                case DescriptorType.Sampler:
                    WriteSamplerDescriptor(bindings[i], imageInfos, i);
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
                    case DescriptorType.StorageBuffer:
                        pWrites[i].PBufferInfo = pBufferInfos + i;
                        break;
                    case DescriptorType.CombinedImageSampler:
                    case DescriptorType.SampledImage:
                    case DescriptorType.Sampler:
                        pWrites[i].PImageInfo = pImageInfos + i;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(bindings), "Unsupported descriptor type.");
                }
            }

            VulkanContext.Vk.UpdateDescriptorSets(VulkanContext.Device, (uint)bindings.Length, pWrites, 0, null);
        }
    }

    internal static DescriptorPoolSize[] CreateDescriptorPoolSizes(IReadOnlyList<DescriptorSetLayoutBinding> bindings)
    {
        var poolSizes = new List<DescriptorPoolSize>();
        foreach (var binding in bindings)
        {
            var descriptorType = VulkanMapping.ToVulkanDescriptorType(binding.Type);
            var found = false;
            for (var i = 0; i < poolSizes.Count; i++)
            {
                if (poolSizes[i].Type != descriptorType)
                    continue;

                var poolSize = poolSizes[i];
                poolSize.DescriptorCount += binding.DescriptorCount;
                poolSizes[i] = poolSize;
                found = true;
                break;
            }

            if (!found)
            {
                poolSizes.Add(new DescriptorPoolSize
                {
                    Type = descriptorType,
                    DescriptorCount = binding.DescriptorCount
                });
            }
        }

        return [.. poolSizes];
    }

    private static void WriteUniformBufferDescriptor(
        DescriptorSetBinding binding,
        DescriptorBufferInfo[] bufferInfos,
        int index)
    {
        WriteBufferDescriptor(
            binding,
            bufferInfos,
            index,
            BufferUsageFlags.UniformBufferBit,
            VulkanContext.MinUniformBufferOffsetAlignment,
            "Uniform");
    }

    private static void WriteStorageBufferDescriptor(
        DescriptorSetBinding binding,
        DescriptorBufferInfo[] bufferInfos,
        int index)
    {
        WriteBufferDescriptor(
            binding,
            bufferInfos,
            index,
            BufferUsageFlags.StorageBufferBit,
            VulkanContext.MinStorageBufferOffsetAlignment,
            "Storage");
    }

    private static void WriteBufferDescriptor(
        DescriptorSetBinding binding,
        DescriptorBufferInfo[] bufferInfos,
        int index,
        BufferUsageFlags requiredUsage,
        ulong requiredAlignment,
        string usageName)
    {
        var buffer = binding.Buffer as VulkanBuffer
                     ?? throw new InvalidOperationException(
                          "Descriptor set buffer was not created by the Vulkan backend.");
        buffer.ThrowIfDestroyed();
        if (!buffer.Usage.HasFlag(requiredUsage))
            throw new InvalidOperationException(
                $"Descriptor set buffer was not created with {usageName} usage.");
        if (binding.Size == 0)
            throw new ArgumentOutOfRangeException(nameof(binding),
                "Descriptor set buffer binding size must be greater than zero.");
        if (binding.Offset > buffer.Size || binding.Size > buffer.Size - binding.Offset)
            throw new ArgumentOutOfRangeException(nameof(binding),
                "Descriptor set buffer offset and size exceed the buffer bounds.");

        if (requiredAlignment == 0)
            throw new InvalidOperationException(
                $"The Vulkan {usageName.ToLowerInvariant()} buffer alignment is 0. Ensure VulkanContext is initialized.");
        if (binding.Offset % requiredAlignment != 0)
            throw new ArgumentOutOfRangeException(nameof(binding),
                $"{usageName} buffer binding offset must satisfy the device {usageName.ToLowerInvariant()} buffer alignment requirement.");

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

    private static void WriteSampledImageDescriptor(
        DescriptorSetBinding binding,
        DescriptorImageInfo[] imageInfos,
        int index)
    {
        var textureView = binding.TextureView as VulkanTextureView
                          ?? throw new InvalidOperationException(
                              "Descriptor set texture view was not created by the Vulkan backend.");
        textureView.ThrowIfDestroyed();
        var texture = textureView.VulkanTexture;
        texture.ThrowIfDestroyed();
        if (!texture.Usage.HasFlag(TextureUsage.Sampled))
            throw new InvalidOperationException("Descriptor set texture was not created with Sampled usage.");

        imageInfos[index] = new DescriptorImageInfo
        {
            ImageView = textureView.ImageView,
            ImageLayout = ImageLayout.ShaderReadOnlyOptimal
        };
    }

    private static void WriteSamplerDescriptor(
        DescriptorSetBinding binding,
        DescriptorImageInfo[] imageInfos,
        int index)
    {
        var sampler = binding.Sampler as VulkanSampler
                      ?? throw new InvalidOperationException(
                          "Descriptor set sampler was not created by the Vulkan backend.");
        sampler.ThrowIfDestroyed();

        imageInfos[index] = new DescriptorImageInfo
        {
            Sampler = sampler.Handle
        };
    }
}
