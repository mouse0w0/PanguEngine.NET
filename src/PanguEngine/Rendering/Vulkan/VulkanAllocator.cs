using Silk.NET.Vulkan;
using Vma;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace PanguEngine.Rendering.Vulkan;

/// <summary>
/// Vulkan Memory Allocator (VMA) wrapper.
/// </summary>
public sealed unsafe class VulkanAllocator
{
    private readonly Allocator* _allocator;
    private readonly Vk _vk;
    private readonly Device _device;
    private bool _destroyed;

    /// <summary>
    /// Initializes the VMA allocator using the Vulkan context.
    /// </summary>
    /// <param name="context">The initialized Vulkan context.</param>
    public VulkanAllocator(VulkanContext context)
    {
        _vk = context.Vk;
        _device = context.Device;

        var createInfo = new AllocatorCreateInfo
        {
            Flags = AllocatorCreateFlags.ExternallySynchronizedBit,
            VulkanApiVersion = Vk.Version13,
            Instance = context.VkInstance,
            PhysicalDevice = context.PhysicalDevice,
            Device = _device,
        };

        Allocator* allocator;
        var result = Apis.CreateAllocator(&createInfo, &allocator);
        if (result != Result.Success)
            throw new InvalidOperationException($"Failed to create VMA allocator: {result}");

        _allocator = allocator;
    }

    /// <summary>
    /// Creates a buffer with bound GPU memory.
    /// </summary>
    /// <param name="bufferInfo">The Vulkan buffer creation info.</param>
    /// <param name="allocInfo">
    /// The VMA allocation creation info. Uses <see cref="MemoryUsage.Auto"/> by default
    /// for optimal memory type selection.
    /// </param>
    /// <returns>The created buffer.</returns>
    public VulkanBuffer CreateBuffer(
        in BufferCreateInfo bufferInfo,
        in AllocationCreateInfo allocInfo = default)
    {
        ObjectDisposedException.ThrowIf(_destroyed, this);

        var actualAllocInfo = allocInfo;
        if (actualAllocInfo.Usage == 0)
            actualAllocInfo.Usage = MemoryUsage.Auto;

        var bufInfo = bufferInfo;
        Buffer buffer = default;
        Allocation* allocation;

        var pAllocInfo = &actualAllocInfo;
        var pBufInfo = &bufInfo;
        var result = Apis.CreateBuffer(_allocator, pBufInfo, pAllocInfo,
            &buffer, &allocation, null);
        if (result != Result.Success)
            throw new InvalidOperationException($"Failed to allocate buffer: {result}");

        return new VulkanBuffer(buffer, this, allocation);
    }

    /// <summary>
    /// Creates an image with bound GPU memory.
    /// </summary>
    /// <param name="imageInfo">The Vulkan image creation info.</param>
    /// <param name="allocationCreateInfo">
    /// The VMA allocation creation info. Uses <see cref="MemoryUsage.Auto"/> by default
    /// for optimal memory type selection.
    /// </param>
    /// <returns>The created image.</returns>
    public VulkanImage CreateImage(
        in ImageCreateInfo imageInfo,
        in AllocationCreateInfo allocationCreateInfo = default)
    {
        ObjectDisposedException.ThrowIf(_destroyed, this);

        var actualAllocInfo = allocationCreateInfo;
        if (actualAllocInfo.Usage == 0)
            actualAllocInfo.Usage = MemoryUsage.Auto;

        var imgInfo = imageInfo;
        Image image = default;
        Allocation* allocation;

        var pAllocInfo = &actualAllocInfo;
        var pImgInfo = &imgInfo;
        var result = Apis.CreateImage(_allocator, pImgInfo, pAllocInfo,
            &image, &allocation, null);
        if (result != Result.Success)
            throw new InvalidOperationException($"Failed to allocate image: {result}");

        return new VulkanImage(image, this, allocation);
    }

    /// <summary>
    /// Maps memory for CPU access.
    /// </summary>
    /// <typeparam name="T">The unmanaged type to map as.</typeparam>
    /// <param name="allocation">The VMA allocation to map.</param>
    /// <returns>A pointer to the mapped memory.</returns>
    internal T* Map<T>(Allocation* allocation) where T : unmanaged
    {
        void* data;
        var result = Apis.MapMemory(_allocator, allocation, &data);
        if (result != Result.Success)
            throw new InvalidOperationException($"Failed to map memory: {result}");

        return (T*)data;
    }

    /// <summary>
    /// Unmaps previously mapped memory.
    /// </summary>
    /// <param name="allocation">The VMA allocation to unmap.</param>
    internal void Unmap(Allocation* allocation)
    {
        Apis.UnmapMemory(_allocator, allocation);
    }

    /// <summary>
    /// Destroys a buffer and frees its GPU memory.
    /// </summary>
    /// <param name="buffer">The buffer handle to destroy.</param>
    /// <param name="allocation">The VMA allocation to free.</param>
    internal void DestroyBuffer(Buffer buffer, Allocation* allocation)
    {
        Apis.DestroyBuffer(_allocator, buffer, allocation);
    }

    /// <summary>
    /// Destroys an image and frees its GPU memory.
    /// </summary>
    /// <param name="image">The image handle to destroy.</param>
    /// <param name="allocation">The VMA allocation to free.</param>
    internal void DestroyImage(Image image, Allocation* allocation)
    {
        Apis.DestroyImage(_allocator, image, allocation);
    }

    /// <summary>
    /// Destroys the VMA allocator and releases all associated resources.
    /// All allocations must be freed before calling this method.
    /// </summary>
    public void Destroy()
    {
        if (_destroyed) return;
        _destroyed = true;

        if (_allocator != null)
        {
            _vk.DeviceWaitIdle(_device);
            Apis.DestroyAllocator(_allocator);
        }
    }
}