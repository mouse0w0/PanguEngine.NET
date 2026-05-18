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

    public override Texture2D CreateTexture2D(in Texture2DDescription description)
    {
        ValidateTexture2DDescription(description);

        ImageCreateInfo imageInfo = new()
        {
            SType = StructureType.ImageCreateInfo,
            ImageType = ImageType.Type2D,
            Format = ToVulkanFormat(description.Format),
            Extent = new Extent3D
            {
                Width = description.Width,
                Height = description.Height,
                Depth = 1,
            },
            MipLevels = 1,
            ArrayLayers = 1,
            Samples = SampleCountFlags.Count1Bit,
            Tiling = ImageTiling.Optimal,
            Usage = ToImageUsage(description.Usage),
            SharingMode = SharingMode.Exclusive,
            InitialLayout = ImageLayout.Undefined,
        };

        AllocationCreateInfo allocInfo = new()
        {
            Usage = VmaMemoryUsage.AutoPreferDevice,
        };

        VulkanAllocator.CreateImage(in imageInfo, in allocInfo, out var image, out var allocation);

        try
        {
            ImageViewCreateInfo viewInfo = new()
            {
                SType = StructureType.ImageViewCreateInfo,
                Image = image,
                ViewType = ImageViewType.Type2D,
                Format = imageInfo.Format,
                SubresourceRange = new ImageSubresourceRange
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    BaseMipLevel = 0,
                    LevelCount = 1,
                    BaseArrayLayer = 0,
                    LayerCount = 1,
                },
            };

            if (VulkanContext.Vk.CreateImageView(VulkanContext.Device, in viewInfo, null, out var imageView) !=
                Result.Success)
                throw new InvalidOperationException("Failed to create texture image view.");

            return new VulkanTexture2D(image, allocation, imageView, description.Format, description.Width,
                description.Height, description.MipLevels, description.Usage);
        }
        catch
        {
            VulkanAllocator.DestroyImage(image, allocation);
            throw;
        }
    }

    public override GraphicsUploadHandle UploadTexture2D(Texture2D destination, ReadOnlySpan<byte> data)
    {
        if (destination == null)
            throw new ArgumentNullException(nameof(destination));

        var texture = RequireVulkanTexture2D(destination);
        if (texture.IsDestroyed)
            throw new ObjectDisposedException(nameof(VulkanTexture2D));

        if (!texture.Usage.HasFlag(TextureUsage.TransferDestination))
            throw new InvalidOperationException("Texture2D was not created with TransferDestination usage.");

        var requiredSize = CalculateTextureDataSize(texture.Width, texture.Height, texture.Format);
        if ((ulong)data.Length != requiredSize)
            throw new ArgumentException("Texture upload data size does not match the full base level size.",
                nameof(data));

        texture.MarkUploadQueued();

        var region = new TextureUploadRegion(0, 0, 0, texture.Width, texture.Height, 1, 0, 0);
        var handle = VulkanUploader.EnqueueTextureUpload(texture, data, region);
        return new VulkanGraphicsUploadHandle(handle);
    }

    private static VulkanBuffer RequireVulkanBuffer(Buffer buffer)
    {
        return buffer as VulkanBuffer
               ?? throw new InvalidOperationException("Graphics buffer was not created by the Vulkan backend.");
    }

    private static VulkanTexture2D RequireVulkanTexture2D(Texture2D texture)
    {
        return texture as VulkanTexture2D
               ?? throw new InvalidOperationException("Texture2D was not created by the Vulkan backend.");
    }

    private static void ValidateTexture2DDescription(in Texture2DDescription description)
    {
        if (description.Width == 0)
            throw new ArgumentOutOfRangeException(nameof(description.Width),
                "Texture width must be greater than zero.");
        if (description.Height == 0)
            throw new ArgumentOutOfRangeException(nameof(description.Height),
                "Texture height must be greater than zero.");
        if (description.MipLevels != 1)
            throw new ArgumentOutOfRangeException(nameof(description.MipLevels),
                "Texture2D currently supports exactly one mip level.");
        if (description.Usage == TextureUsage.None)
            throw new ArgumentException("Texture usage must not be None.", nameof(description.Usage));
        if (description.Width > VulkanContext.MaxImageDimension2D)
            throw new ArgumentOutOfRangeException(nameof(description.Width), "Texture width exceeds the device limit.");
        if (description.Height > VulkanContext.MaxImageDimension2D)
            throw new ArgumentOutOfRangeException(nameof(description.Height),
                "Texture height exceeds the device limit.");
    }

    private static Format ToVulkanFormat(TextureFormat format)
    {
        return format switch
        {
            TextureFormat.R8G8B8A8Unorm => Format.R8G8B8A8Unorm,
            TextureFormat.B8G8R8A8Unorm => Format.B8G8R8A8Unorm,
            TextureFormat.R8Unorm => Format.R8Unorm,
            _ => throw new InvalidOperationException("Unsupported texture format."),
        };
    }

    private static ImageUsageFlags ToImageUsage(TextureUsage usage)
    {
        var result = ImageUsageFlags.None;
        if (usage.HasFlag(TextureUsage.TransferSource))
            result |= ImageUsageFlags.TransferSrcBit;
        if (usage.HasFlag(TextureUsage.TransferDestination))
            result |= ImageUsageFlags.TransferDstBit;
        if (usage.HasFlag(TextureUsage.Sampled))
            result |= ImageUsageFlags.SampledBit;
        return result;
    }

    internal static uint GetTextureBytesPerPixel(TextureFormat format)
    {
        return format switch
        {
            TextureFormat.R8G8B8A8Unorm => 4,
            TextureFormat.B8G8R8A8Unorm => 4,
            TextureFormat.R8Unorm => 1,
            _ => throw new InvalidOperationException("Unsupported texture format."),
        };
    }

    private static ulong CalculateTextureDataSize(uint width, uint height, TextureFormat format)
    {
        return checked((ulong)width * height * GetTextureBytesPerPixel(format));
    }
}