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
        var createInfo = new AllocatorCreateInfo
        {
            Flags = 0,
            VulkanApiVersion = Vk.Version13,
            Instance = VulkanContext.VkInstance,
            PhysicalDevice = VulkanContext.PhysicalDevice,
            Device = VulkanContext.Device,
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
        ObjectDisposedException.ThrowIf(_destroyed, typeof(VulkanAllocator));

        var actualAllocInfo = allocInfo;
        if (actualAllocInfo.Usage == 0)
            actualAllocInfo.Usage = VmaMemoryUsage.Auto;

        var bufInfo = bufferInfo;
        VkBuffer buffer = default;
        Allocation* allocation;

        var pAllocInfo = &actualAllocInfo;
        var pBufInfo = &bufInfo;
        var result = Apis.CreateBuffer(_allocator, pBufInfo, pAllocInfo,
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
        ObjectDisposedException.ThrowIf(_destroyed, typeof(VulkanAllocator));

        var actualAllocInfo = allocInfo;
        if (actualAllocInfo.Usage == 0)
            actualAllocInfo.Usage = VmaMemoryUsage.Auto;

        var imgInfo = imageInfo;
        VkImage createdImage = default;
        Allocation* createdAllocation;

        var pAllocInfo = &actualAllocInfo;
        var pImgInfo = &imgInfo;
        var result = Apis.CreateImage(_allocator, pImgInfo, pAllocInfo, &createdImage, &createdAllocation, null);
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
        var result = Apis.MapMemory(_allocator, allocation, &data);
        if (result != Result.Success)
            throw new InvalidOperationException($"Failed to map memory: {result}");

        return (T*)data;
    }

    /// <summary>
    /// Unmaps previously mapped memory.
    /// </summary>
    /// <param name="allocation">The VMA allocation to unmap.</param>
    internal static void Unmap(Allocation* allocation)
    {
        Apis.UnmapMemory(_allocator, allocation);
    }

    /// <summary>
    /// Destroys a buffer and frees its GPU memory.
    /// </summary>
    /// <param name="buffer">The buffer handle to destroy.</param>
    /// <param name="allocation">The VMA allocation to free.</param>
    internal static void DestroyBuffer(VkBuffer buffer, Allocation* allocation)
    {
        Apis.DestroyBuffer(_allocator, buffer, allocation);
    }

    /// <summary>
    /// Destroys an image and frees its GPU memory.
    /// </summary>
    /// <param name="image">The image handle to destroy.</param>
    /// <param name="allocation">The VMA allocation to free.</param>
    internal static void DestroyImage(VkImage image, Allocation* allocation)
    {
        Apis.DestroyImage(_allocator, image, allocation);
    }

    /// <summary>
    /// Destroys the VMA allocator and releases all associated resources.
    /// All allocations must be freed before calling this method.
    /// </summary>
    public static void Destroy()
    {
        if (_destroyed) return;
        _destroyed = true;

        if (_allocator == null) return;
        VulkanContext.Vk.DeviceWaitIdle(VulkanContext.Device);
        Apis.DestroyAllocator(_allocator);
    }
}