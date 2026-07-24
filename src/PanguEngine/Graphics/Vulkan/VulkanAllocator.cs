using System.Diagnostics;
using Silk.NET.Vulkan;
using Vma;
using VkBuffer = Silk.NET.Vulkan.Buffer;
using VkImage = Silk.NET.Vulkan.Image;
using VmaMemoryUsage = Vma.MemoryUsage;

namespace PanguEngine.Graphics.Vulkan;

/// <summary>
/// Vulkan Memory Allocator (VMA) wrapper.
/// </summary>
public static unsafe class VulkanAllocator
{
    private static Allocator* _allocator;
    private static bool _destroyed;

    /// <summary>
    /// Initializes the VMA allocator using the Vulkan context.
    /// </summary>
    public static void Initialize()
    {
        if (_allocator != null)
            throw new InvalidOperationException("VulkanAllocator is already initialized.");
        if (_destroyed)
            throw new ObjectDisposedException(nameof(VulkanAllocator));

        var createInfo = new AllocatorCreateInfo
        {
            Flags = 0,
            VulkanApiVersion = Vk.Version13,
            Instance = VulkanContext.VkInstance,
            PhysicalDevice = VulkanContext.PhysicalDevice,
            Device = VulkanContext.Device
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
    /// The VMA allocation creation info. Uses <see cref="VmaMemoryUsage.Auto"/> by default
    /// for optimal memory type selection.
    /// </param>
    /// <returns>The created buffer.</returns>
    public static VulkanBuffer CreateBuffer(
        in BufferCreateInfo bufferInfo,
        in AllocationCreateInfo allocInfo = default)
    {
        var allocator = RequireInitialized();
        var actualAllocInfo = allocInfo;
        if (actualAllocInfo.Usage == 0)
            actualAllocInfo.Usage = VmaMemoryUsage.Auto;

        var bufInfo = bufferInfo;
        VkBuffer buffer = default;
        Allocation* allocation;

        var pAllocInfo = &actualAllocInfo;
        var pBufInfo = &bufInfo;
        var result = Apis.CreateBuffer(allocator, pBufInfo, pAllocInfo,
            &buffer, &allocation, null);
        if (result != Result.Success)
            throw new InvalidOperationException($"Failed to allocate buffer: {result}");

        return new VulkanBuffer(buffer, allocation, bufInfo.Size, bufInfo.Usage);
    }

    /// <summary>
    /// Creates an image with bound GPU memory.
    /// </summary>
    /// <param name="imageInfo">The Vulkan image creation info.</param>
    /// <param name="allocInfo">The VMA allocation creation info.</param>
    /// <param name="image">The created image handle.</param>
    /// <param name="allocation">The created VMA allocation.</param>
    internal static void CreateImage(
        in ImageCreateInfo imageInfo,
        in AllocationCreateInfo allocInfo,
        out VkImage image,
        out Allocation* allocation)
    {
        var allocator = RequireInitialized();
        var actualAllocInfo = allocInfo;
        if (actualAllocInfo.Usage == 0)
            actualAllocInfo.Usage = VmaMemoryUsage.Auto;

        var imgInfo = imageInfo;
        VkImage createdImage = default;
        Allocation* createdAllocation;

        var pAllocInfo = &actualAllocInfo;
        var pImgInfo = &imgInfo;
        var result = Apis.CreateImage(allocator, pImgInfo, pAllocInfo, &createdImage, &createdAllocation, null);
        if (result != Result.Success)
            throw new InvalidOperationException($"Failed to allocate image: {result}");

        image = createdImage;
        allocation = createdAllocation;
    }

    /// <summary>
    /// Maps memory for CPU access.
    /// </summary>
    /// <typeparam name="T">The unmanaged type to map as.</typeparam>
    /// <param name="allocation">The VMA allocation to map.</param>
    /// <returns>A pointer to the mapped memory.</returns>
    internal static T* Map<T>(Allocation* allocation) where T : unmanaged
    {
        void* data;
        var result = Apis.MapMemory(RequireInitialized(), allocation, &data);
        if (result != Result.Success)
            throw new InvalidOperationException($"Failed to map memory: {result}");

        return (T*)data;
    }

    /// <summary>
    /// Flushes CPU writes to a mapped allocation so they are visible to the device.
    /// </summary>
    /// <param name="allocation">The VMA allocation to flush.</param>
    /// <param name="offset">The byte offset relative to the allocation.</param>
    /// <param name="size">The number of bytes to flush.</param>
    internal static void Flush(Allocation* allocation, ulong offset, ulong size)
    {
        var result = Apis.FlushAllocation(RequireInitialized(), allocation, offset, size);
        if (result != Result.Success)
            throw new InvalidOperationException($"Failed to flush memory: {result}");
    }

    /// <summary>
    /// Unmaps previously mapped memory.
    /// </summary>
    /// <param name="allocation">The VMA allocation to unmap.</param>
    internal static void Unmap(Allocation* allocation)
    {
        if (!TryGetAllocatorForRelease(out var allocator))
            return;

        Apis.UnmapMemory(allocator, allocation);
    }

    /// <summary>
    /// Destroys a buffer and frees its GPU memory.
    /// </summary>
    /// <param name="buffer">The buffer handle to destroy.</param>
    /// <param name="allocation">The VMA allocation to free.</param>
    internal static void DestroyBuffer(VkBuffer buffer, Allocation* allocation)
    {
        if (!TryGetAllocatorForRelease(out var allocator))
            return;

        Apis.DestroyBuffer(allocator, buffer, allocation);
    }

    /// <summary>
    /// Destroys an image and frees its GPU memory.
    /// </summary>
    /// <param name="image">The image handle to destroy.</param>
    /// <param name="allocation">The VMA allocation to free.</param>
    internal static void DestroyImage(VkImage image, Allocation* allocation)
    {
        if (!TryGetAllocatorForRelease(out var allocator))
            return;

        Apis.DestroyImage(allocator, image, allocation);
    }

    private static Allocator* RequireInitialized()
    {
        if (_destroyed)
            throw new ObjectDisposedException(nameof(VulkanAllocator));
        if (_allocator == null)
            throw new InvalidOperationException("VulkanAllocator is not initialized.");
        return _allocator;
    }

    private static bool TryGetAllocatorForRelease(out Allocator* allocator)
    {
        if (_destroyed)
        {
            Debug.Assert(false, "A Vulkan allocation was released after VulkanAllocator was destroyed.");
            allocator = null;
            return false;
        }

        allocator = _allocator;
        return true;
    }

    /// <summary>
    /// Destroys the VMA allocator and releases all associated resources.
    /// All allocations must be freed before calling this method.
    /// </summary>
    public static void Destroy()
    {
        if (_allocator == null)
            return;

        var allocator = _allocator;
        _allocator = null;
        _destroyed = true;

        VulkanContext.Vk.DeviceWaitIdle(VulkanContext.Device);
        Apis.DestroyAllocator(allocator);
    }
}