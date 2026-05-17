using Silk.NET.Vulkan;
using Vma;
using VmaMemoryUsage = Vma.MemoryUsage;

namespace PanguEngine.Graphics.Vulkan;

/// <summary>
/// Vulkan implementation of <see cref="GraphicsDevice"/>.
/// </summary>
internal sealed unsafe class VulkanGraphicsDevice : GraphicsDevice
{
    private sealed class CompletedGraphicsUploadHandle : GraphicsUploadHandle
    {
        public static readonly CompletedGraphicsUploadHandle Instance = new();

        public override bool IsCompleted => true;

        public override bool IsFaulted => false;

        public override Exception? Exception => null;

        public override void Wait()
        {
        }
    }

    /// <inheritdoc/>
    public override Buffer CreateBuffer(in BufferDescription description)
    {
        if (description.Size == 0)
            throw new ArgumentOutOfRangeException(nameof(description.Size), "Buffer size must be greater than zero.");
        if (description.Usage == BufferUsage.None)
            throw new ArgumentException("Buffer usage must not be None.", nameof(description.Usage));

        var vkUsage = BufferUsageFlags.None;
        if (description.Usage.HasFlag(BufferUsage.TransferSource))
            vkUsage |= BufferUsageFlags.TransferSrcBit;
        if (description.Usage.HasFlag(BufferUsage.TransferDestination))
            vkUsage |= BufferUsageFlags.TransferDstBit;
        if (description.Usage.HasFlag(BufferUsage.Uniform))
            vkUsage |= BufferUsageFlags.UniformBufferBit;
        if (description.Usage.HasFlag(BufferUsage.Vertex))
            vkUsage |= BufferUsageFlags.VertexBufferBit;
        if (description.Usage.HasFlag(BufferUsage.Index))
            vkUsage |= BufferUsageFlags.IndexBufferBit;

        BufferCreateInfo bufferInfo = new()
        {
            SType = StructureType.BufferCreateInfo,
            Size = description.Size,
            Usage = vkUsage,
            SharingMode = SharingMode.Exclusive,
        };

        var vmaUsage = description.MemoryUsage.Value switch
        {
            0 => VmaMemoryUsage.AutoPreferDevice,
            1 => VmaMemoryUsage.CpuToGpu,
            2 => VmaMemoryUsage.GpuToCpu,
            _ => VmaMemoryUsage.Auto,
        };

        AllocationCreateInfo allocInfo = new()
        {
            Usage = vmaUsage,
        };

        return VulkanAllocator.CreateBuffer(in bufferInfo, in allocInfo);
    }

    public override GraphicsUploadHandle UploadBuffer<T>(
        Buffer destination,
        ReadOnlySpan<T> data,
        ulong destinationOffset = 0)
    {
        if (destination == null)
            throw new ArgumentNullException(nameof(destination));

        var vulkanBuffer = RequireVulkanBuffer(destination);

        if (vulkanBuffer.IsDestroyed)
            throw new ObjectDisposedException(nameof(VulkanBuffer));

        if (destinationOffset > vulkanBuffer.Size)
            throw new ArgumentOutOfRangeException(nameof(destinationOffset),
                "Destination offset exceeds the buffer bounds.");

        var dataSize = checked((ulong)data.Length * (ulong)sizeof(T));
        if (dataSize > vulkanBuffer.Size - destinationOffset)
            throw new ArgumentOutOfRangeException(nameof(destinationOffset),
                "Destination offset and data size exceed the buffer bounds.");

        if (dataSize == 0)
            return CompletedGraphicsUploadHandle.Instance;

        var handle = VulkanUploader.EnqueueBufferUpload(vulkanBuffer, data, destinationOffset);
        return new VulkanGraphicsUploadHandle(handle);
    }

    private static VulkanBuffer RequireVulkanBuffer(Buffer buffer)
    {
        return buffer as VulkanBuffer
               ?? throw new InvalidOperationException("Graphics buffer was not created by the Vulkan backend.");
    }
}