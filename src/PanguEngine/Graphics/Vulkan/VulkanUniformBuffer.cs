using Silk.NET.Vulkan;
using Vma;

namespace PanguEngine.Graphics.Vulkan;

/// <summary>
/// A persistently mapped uniform buffer with <see cref="VulkanContext.MaxFramesInFlight"/>
/// aligned sub-regions for multi-frame rendering.
/// </summary>
public sealed unsafe class VulkanUniformBuffer
{
    private readonly VulkanBuffer _buffer;
    private readonly byte* _mappedPtr;
    private bool _destroyed;

    /// <summary>
    /// The aligned size for a single frame's uniform data, in bytes.
    /// </summary>
    public ulong AlignedSize { get; }

    /// <summary>
    /// The total buffer size (AlignedSize × MaxFramesInFlight), in bytes.
    /// </summary>
    public ulong TotalSize { get; }

    /// <summary>
    /// Calculates the aligned size for an unmanaged struct type.
    /// </summary>
    /// <typeparam name="T">The unmanaged struct type.</typeparam>
    /// <returns>The aligned size in bytes.</returns>
    public static ulong CalculateAlignedSize<T>() where T : unmanaged
    {
        return CalculateAlignedSize((ulong)sizeof(T));
    }

    /// <summary>
    /// Calculates the aligned size for a given raw size using the minimum uniform buffer offset alignment.
    /// </summary>
    /// <param name="rawSize">The raw, unaligned size in bytes.</param>
    /// <returns>The aligned size in bytes.</returns>
    public static ulong CalculateAlignedSize(ulong rawSize)
    {
        var align = VulkanContext.MinUniformBufferOffsetAlignment;
        if (align == 0)
            throw new InvalidOperationException(
                "VulkanContext.MinUniformBufferOffsetAlignment is 0. Ensure VulkanContext is initialized.");
        return checked(((rawSize + align - 1) / align) * align);
    }

    /// <summary>
    /// Creates a persistently mapped uniform buffer. The buffer holds
    /// <see cref="VulkanContext.MaxFramesInFlight"/> aligned sub-regions.
    /// </summary>
    /// <param name="rawSize">The raw size of a single frame's uniform data, in bytes.</param>
    public VulkanUniformBuffer(ulong rawSize)
    {
        var align = VulkanContext.MinUniformBufferOffsetAlignment;
        if (align == 0)
            throw new InvalidOperationException(
                "VulkanContext.MinUniformBufferOffsetAlignment is 0. Ensure VulkanContext is initialized.");
        if (rawSize == 0)
            throw new ArgumentOutOfRangeException(nameof(rawSize), "Raw size must be greater than zero.");

        AlignedSize = checked(((rawSize + align - 1) / align) * align);
        TotalSize = checked(AlignedSize * VulkanContext.MaxFramesInFlight);

        BufferCreateInfo bufferInfo = new()
        {
            SType = StructureType.BufferCreateInfo,
            Size = TotalSize,
            Usage = BufferUsageFlags.UniformBufferBit,
            SharingMode = SharingMode.Exclusive,
        };

        AllocationCreateInfo allocInfo = new()
        {
            Usage = MemoryUsage.Auto,
            Flags = AllocationCreateFlags.HostAccessSequentialWriteBit,
            RequiredFlags = MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
        };

        _buffer = VulkanAllocator.CreateBuffer(in bufferInfo, in allocInfo);
        _mappedPtr = _buffer.Map<byte>();
    }

    /// <summary>
    /// Returns a typed pointer to the mapped uniform data for the given frame index.
    /// </summary>
    /// <typeparam name="T">The unmanaged struct type.</typeparam>
    /// <param name="frameIndex">The frame index (0 to MaxFramesInFlight-1).</param>
    /// <returns>A typed pointer to the sub-region.</returns>
    public T* GetMappedData<T>(uint frameIndex) where T : unmanaged
    {
        if (_destroyed)
            throw new ObjectDisposedException(nameof(VulkanUniformBuffer));
        if (frameIndex >= VulkanContext.MaxFramesInFlight)
            throw new ArgumentOutOfRangeException(nameof(frameIndex),
                $"Frame index {frameIndex} exceeds MaxFramesInFlight {VulkanContext.MaxFramesInFlight}.");
        if ((ulong)sizeof(T) > AlignedSize)
            throw new InvalidOperationException(
                $"sizeof({typeof(T).Name}) ({sizeof(T)}) exceeds aligned size ({AlignedSize}).");

        return (T*)(_mappedPtr + frameIndex * AlignedSize);
    }

    /// <summary>
    /// Returns the device offset of the sub-region for the given frame index.
    /// </summary>
    /// <param name="frameIndex">The frame index (0 to MaxFramesInFlight-1).</param>
    /// <returns>The offset in bytes from the start of the buffer.</returns>
    public ulong GetOffset(uint frameIndex)
    {
        if (_destroyed)
            throw new ObjectDisposedException(nameof(VulkanUniformBuffer));
        if (frameIndex >= VulkanContext.MaxFramesInFlight)
            throw new ArgumentOutOfRangeException(nameof(frameIndex),
                $"Frame index {frameIndex} exceeds MaxFramesInFlight {VulkanContext.MaxFramesInFlight}.");

        return frameIndex * AlignedSize;
    }

    /// <summary>
    /// Unmaps and destroys the uniform buffer.
    /// </summary>
    public void Destroy()
    {
        if (_destroyed) return;
        _destroyed = true;

        _buffer.Unmap();
        _buffer.Destroy();
    }
}