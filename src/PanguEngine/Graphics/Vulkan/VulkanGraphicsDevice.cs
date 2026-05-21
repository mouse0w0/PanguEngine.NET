using Silk.NET.Vulkan;
using Vma;
using VKSampler = Silk.NET.Vulkan.Sampler;
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

    public override Texture CreateTexture(in TextureDescription description)
    {
        ValidateTextureDescription(description);

        var imageType = ToImageType(description.Dimension);
        var imageViewType = ToImageViewType(description.Dimension, description.ArrayLayers);
        var imageArrayLayers = GetImageArrayLayers(description);
        var imageCreateFlags = description.Dimension == TextureDimension.CubeMap
            ? ImageCreateFlags.CreateCubeCompatibleBit
            : ImageCreateFlags.None;

        ImageCreateInfo imageInfo = new()
        {
            SType = StructureType.ImageCreateInfo,
            Flags = imageCreateFlags,
            ImageType = imageType,
            Format = ToVulkanFormat(description.Format),
            Extent = new Extent3D
            {
                Width = description.Width,
                Height = description.Height,
                Depth = description.Dimension == TextureDimension.Type3D ? description.Depth : 1,
            },
            MipLevels = description.MipLevels,
            ArrayLayers = imageArrayLayers,
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
                ViewType = imageViewType,
                Format = imageInfo.Format,
                SubresourceRange = new ImageSubresourceRange
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    BaseMipLevel = 0,
                    LevelCount = description.MipLevels,
                    BaseArrayLayer = 0,
                    LayerCount = imageArrayLayers,
                },
            };

            if (VulkanContext.Vk.CreateImageView(VulkanContext.Device, in viewInfo, null, out var imageView) !=
                Result.Success)
                throw new InvalidOperationException("Failed to create texture image view.");

            return new VulkanTexture(image, allocation, imageView, description.Dimension, description.Format,
                description.Width, description.Height, description.Depth, description.MipLevels, imageArrayLayers,
                description.Usage);
        }
        catch
        {
            VulkanAllocator.DestroyImage(image, allocation);
            throw;
        }
    }

    public override GraphicsUploadHandle UploadTexture(Texture destination, ReadOnlySpan<byte> data)
    {
        if (destination == null)
            throw new ArgumentNullException(nameof(destination));

        var texture = RequireVulkanTexture(destination);
        if (texture.IsDestroyed)
            throw new ObjectDisposedException(nameof(VulkanTexture));

        if (!texture.Usage.HasFlag(TextureUsage.TransferDestination))
            throw new InvalidOperationException("Texture was not created with TransferDestination usage.");

        var requiredSize = CalculateTextureDataSize(texture.Width, texture.Height, texture.Depth, texture.ArrayLayers,
            texture.Format);
        if ((ulong)data.Length != requiredSize)
            throw new ArgumentException("Texture upload data size does not match the full base level size.",
                nameof(data));

        texture.MarkUploadQueued();

        var region = new TextureUploadRegion(0, 0, 0, texture.Width, texture.Height, texture.Depth, 0, 0,
            texture.ArrayLayers);
        var handle = VulkanUploader.EnqueueTextureUpload(texture, data, region);
        return new VulkanGraphicsUploadHandle(handle);
    }

    public override Sampler CreateSampler(in SamplerDescription description)
    {
        ValidateSamplerDescription(description);

        var anisotropyEnable = description.MaxAnisotropy > 1;
        SamplerCreateInfo samplerInfo = new()
        {
            SType = StructureType.SamplerCreateInfo,
            MinFilter = ToVulkanFilter(description.MinFilter),
            MagFilter = ToVulkanFilter(description.MagFilter),
            MipmapMode = ToVulkanMipmapMode(description.MipmapMode),
            AddressModeU = ToVulkanAddressMode(description.AddressU),
            AddressModeV = ToVulkanAddressMode(description.AddressV),
            AddressModeW = ToVulkanAddressMode(description.AddressW),
            MipLodBias = description.MipLodBias,
            AnisotropyEnable = anisotropyEnable,
            MaxAnisotropy = anisotropyEnable ? description.MaxAnisotropy : 1,
            MinLod = description.MinLod,
            MaxLod = description.MaxLod,
            BorderColor = BorderColor.FloatTransparentBlack,
            CompareEnable = false,
            CompareOp = CompareOp.Always,
            UnnormalizedCoordinates = false,
        };

        if (VulkanContext.Vk.CreateSampler(VulkanContext.Device, in samplerInfo, null, out VKSampler sampler) !=
            Result.Success)
            throw new InvalidOperationException("Failed to create Vulkan sampler.");

        return new VulkanSampler(sampler);
    }

    /// <inheritdoc/>
    public override Shader CreateShader(in ShaderDescription description)
    {
        ValidateShaderDescription(description);
        return new VulkanShader(description);
    }

    /// <inheritdoc/>
    public override GraphicsPipeline CreateGraphicsPipeline(in GraphicsPipelineDescription description)
    {
        ValidateGraphicsPipelineDescription(description);
        return new VulkanGraphicsPipeline(description);
    }

    /// <summary>
    /// Creates a graphics pipeline with Vulkan descriptor set layouts.
    /// </summary>
    /// <param name="description">The graphics pipeline description.</param>
    /// <param name="descriptorSetLayouts">The Vulkan descriptor set layouts used by the pipeline layout.</param>
    /// <returns>The created graphics pipeline.</returns>
    internal GraphicsPipeline CreateGraphicsPipeline(
        in GraphicsPipelineDescription description,
        ReadOnlySpan<DescriptorSetLayout> descriptorSetLayouts)
    {
        ValidateGraphicsPipelineDescription(description);
        return new VulkanGraphicsPipeline(description, descriptorSetLayouts);
    }

    private static VulkanBuffer RequireVulkanBuffer(Buffer buffer)
    {
        return buffer as VulkanBuffer
               ?? throw new InvalidOperationException("Graphics buffer was not created by the Vulkan backend.");
    }

    private static VulkanTexture RequireVulkanTexture(Texture texture)
    {
        return texture as VulkanTexture
               ?? throw new InvalidOperationException("Texture was not created by the Vulkan backend.");
    }

    private static VulkanShader RequireVulkanShader(Shader shader)
    {
        return shader as VulkanShader
               ?? throw new InvalidOperationException("Shader was not created by the Vulkan backend.");
    }

    private static void ValidateShaderDescription(in ShaderDescription description)
    {
        if (string.IsNullOrWhiteSpace(description.Source))
            throw new ArgumentException("Shader source must not be empty.", nameof(description.Source));
        if (string.IsNullOrWhiteSpace(description.EntryPoint))
            throw new ArgumentException("Shader entry point must not be empty.", nameof(description.EntryPoint));
        if (description.SourceLanguage != ShaderSourceLanguage.Glsl)
            throw new ArgumentOutOfRangeException(nameof(description.SourceLanguage),
                "Only GLSL shader source is currently supported.");
    }

    internal static void ValidateGraphicsPipelineDescription(in GraphicsPipelineDescription description)
    {
        var shaders = description.Shaders.Span;
        if (shaders.Length == 0)
            throw new ArgumentException("Graphics pipeline must contain shaders.", nameof(description.Shaders));

        var hasVertexShader = false;
        var hasFragmentShader = false;
        foreach (var shader in shaders)
        {
            if (shader == null)
                throw new ArgumentException("Graphics pipeline shaders must not contain null entries.",
                    nameof(description.Shaders));

            var vulkanShader = RequireVulkanShader(shader);
            if (vulkanShader.IsDestroyed)
                throw new ObjectDisposedException(nameof(VulkanShader));

            hasVertexShader |= vulkanShader.Stage == ShaderStage.Vertex;
            hasFragmentShader |= vulkanShader.Stage == ShaderStage.Fragment;
        }

        if (!hasVertexShader)
            throw new ArgumentException("Graphics pipeline must contain a vertex shader.", nameof(description.Shaders));
        if (!hasFragmentShader)
            throw new ArgumentException("Graphics pipeline must contain a fragment shader.",
                nameof(description.Shaders));

        _ = ToVulkanFormat(description.ColorAttachmentFormat);

        if (!float.IsFinite(description.Rasterizer.LineWidth) || description.Rasterizer.LineWidth < 0)
            throw new ArgumentOutOfRangeException(nameof(description.Rasterizer.LineWidth),
                "Rasterizer line width must be a non-negative finite value.");

        ValidateVertexInputDescription(description.VertexInput);
    }

    private static void ValidateVertexInputDescription(in VertexInputDescription description)
    {
        var buffers = description.Buffers.Span;
        var attributes = description.Attributes.Span;

        foreach (var buffer in buffers)
        {
            if (buffer.Stride == 0)
                throw new ArgumentOutOfRangeException(nameof(buffer.Stride),
                    "Vertex buffer stride must be greater than zero.");
        }

        foreach (var attribute in attributes)
        {
            var foundBinding = false;
            uint stride = 0;

            foreach (var buffer in buffers)
            {
                if (buffer.Binding != attribute.Binding)
                    continue;

                foundBinding = true;
                stride = buffer.Stride;
                break;
            }

            if (!foundBinding)
                throw new ArgumentException("Vertex attribute binding must reference an existing vertex buffer layout.",
                    nameof(description.Attributes));
            if (attribute.Offset > stride)
                throw new ArgumentOutOfRangeException(nameof(attribute.Offset),
                    "Vertex attribute offset must not exceed the vertex buffer stride.");
        }
    }

    private static void ValidateSamplerDescription(in SamplerDescription description)
    {
        if (!float.IsFinite(description.MaxAnisotropy) || description.MaxAnisotropy < 0)
            throw new ArgumentOutOfRangeException(nameof(description.MaxAnisotropy),
                "MaxAnisotropy must be a non-negative finite value.");
        if (!float.IsFinite(description.MinLod))
            throw new ArgumentOutOfRangeException(nameof(description.MinLod), "MinLod must be a finite value.");
        if (!float.IsFinite(description.MaxLod))
            throw new ArgumentOutOfRangeException(nameof(description.MaxLod), "MaxLod must be a finite value.");
        if (!float.IsFinite(description.MipLodBias))
            throw new ArgumentOutOfRangeException(nameof(description.MipLodBias), "MipLodBias must be a finite value.");

        if (description.MinLod < 0)
            throw new ArgumentOutOfRangeException(nameof(description.MinLod), "MinLod must be non-negative.");
        if (description.MaxLod < 0)
            throw new ArgumentOutOfRangeException(nameof(description.MaxLod), "MaxLod must be non-negative.");
        if (description.MinLod > description.MaxLod)
            throw new ArgumentOutOfRangeException(nameof(description.MinLod), "MinLod must not exceed MaxLod.");

        if (MathF.Abs(description.MipLodBias) > VulkanContext.MaxSamplerLodBias)
            throw new ArgumentOutOfRangeException(nameof(description.MipLodBias),
                "MipLodBias exceeds the device limit.");

        if (description.MaxAnisotropy <= 1)
            return;

        if (!VulkanContext.SamplerAnisotropySupported)
            throw new InvalidOperationException("Anisotropic filtering is not supported by the device.");
        if (description.MaxAnisotropy > VulkanContext.MaxSamplerAnisotropy)
            throw new ArgumentOutOfRangeException(nameof(description.MaxAnisotropy),
                "MaxAnisotropy exceeds the device limit.");
        if (description.MinFilter != FilterMode.Linear || description.MagFilter != FilterMode.Linear)
            throw new InvalidOperationException("Anisotropic filtering requires MinFilter and MagFilter to be Linear.");
    }

    private static void ValidateTextureDescription(in TextureDescription description)
    {
        if (description.Width == 0)
            throw new ArgumentOutOfRangeException(nameof(description.Width),
                "Texture width must be greater than zero.");
        if (description.Height == 0)
            throw new ArgumentOutOfRangeException(nameof(description.Height),
                "Texture height must be greater than zero.");
        if (description.Depth == 0)
            throw new ArgumentOutOfRangeException(nameof(description.Depth),
                "Texture depth must be greater than zero.");
        if (description.ArrayLayers == 0)
            throw new ArgumentOutOfRangeException(nameof(description.ArrayLayers),
                "Texture array layers must be greater than zero.");
        if (description.MipLevels != 1)
            throw new ArgumentOutOfRangeException(nameof(description.MipLevels),
                "Textures currently support exactly one mip level.");
        if (description.Usage == TextureUsage.None)
            throw new ArgumentException("Texture usage must not be None.", nameof(description.Usage));
        if (description.ArrayLayers > VulkanContext.MaxImageArrayLayers)
            throw new ArgumentOutOfRangeException(nameof(description.ArrayLayers),
                "Texture array layers exceed the device limit.");

        switch (description.Dimension)
        {
            case TextureDimension.Type1D:
                if (description.Width > VulkanContext.MaxImageDimension1D)
                    throw new ArgumentOutOfRangeException(nameof(description.Width),
                        "Texture width exceeds the device limit.");
                if (description.Height != 1)
                    throw new ArgumentOutOfRangeException(nameof(description.Height),
                        "1D textures must have a height of one.");
                if (description.Depth != 1)
                    throw new ArgumentOutOfRangeException(nameof(description.Depth),
                        "1D textures must have a depth of one.");
                break;
            case TextureDimension.Type2D:
                if (description.Width > VulkanContext.MaxImageDimension2D)
                    throw new ArgumentOutOfRangeException(nameof(description.Width),
                        "Texture width exceeds the device limit.");
                if (description.Height > VulkanContext.MaxImageDimension2D)
                    throw new ArgumentOutOfRangeException(nameof(description.Height),
                        "Texture height exceeds the device limit.");
                if (description.Depth != 1)
                    throw new ArgumentOutOfRangeException(nameof(description.Depth),
                        "2D textures must have a depth of one.");
                break;
            case TextureDimension.Type3D:
                if (description.Width > VulkanContext.MaxImageDimension3D)
                    throw new ArgumentOutOfRangeException(nameof(description.Width),
                        "Texture width exceeds the device limit.");
                if (description.Height > VulkanContext.MaxImageDimension3D)
                    throw new ArgumentOutOfRangeException(nameof(description.Height),
                        "Texture height exceeds the device limit.");
                if (description.Depth > VulkanContext.MaxImageDimension3D)
                    throw new ArgumentOutOfRangeException(nameof(description.Depth),
                        "Texture depth exceeds the device limit.");
                if (description.ArrayLayers != 1)
                    throw new ArgumentOutOfRangeException(nameof(description.ArrayLayers),
                        "3D textures must have exactly one array layer.");
                break;
            case TextureDimension.CubeMap:
                if (description.Width > VulkanContext.MaxImageDimension2D)
                    throw new ArgumentOutOfRangeException(nameof(description.Width),
                        "Texture width exceeds the device limit.");
                if (description.Height > VulkanContext.MaxImageDimension2D)
                    throw new ArgumentOutOfRangeException(nameof(description.Height),
                        "Texture height exceeds the device limit.");
                if (description.Height != description.Width)
                    throw new ArgumentOutOfRangeException(nameof(description.Height),
                        "Cube map textures must be square.");
                if (description.Depth != 1)
                    throw new ArgumentOutOfRangeException(nameof(description.Depth),
                        "Cube map textures must have a depth of one.");
                if (description.ArrayLayers % 6 != 0)
                    throw new ArgumentOutOfRangeException(nameof(description.ArrayLayers),
                        "Cube map texture array layers must be a multiple of six.");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(description.Dimension), "Unsupported texture dimension.");
        }
    }

    private static ImageType ToImageType(TextureDimension dimension)
    {
        return dimension switch
        {
            TextureDimension.Type1D => ImageType.Type1D,
            TextureDimension.Type2D => ImageType.Type2D,
            TextureDimension.Type3D => ImageType.Type3D,
            TextureDimension.CubeMap => ImageType.Type2D,
            _ => throw new InvalidOperationException("Unsupported texture dimension."),
        };
    }

    private static Filter ToVulkanFilter(FilterMode mode)
    {
        return mode switch
        {
            FilterMode.Nearest => Filter.Nearest,
            FilterMode.Linear => Filter.Linear,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), "Unsupported filter mode."),
        };
    }

    private static SamplerMipmapMode ToVulkanMipmapMode(MipmapMode mode)
    {
        return mode switch
        {
            MipmapMode.Nearest => SamplerMipmapMode.Nearest,
            MipmapMode.Linear => SamplerMipmapMode.Linear,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), "Unsupported mipmap mode."),
        };
    }

    private static SamplerAddressMode ToVulkanAddressMode(WrapMode mode)
    {
        return mode switch
        {
            WrapMode.Repeat => SamplerAddressMode.Repeat,
            WrapMode.MirroredRepeat => SamplerAddressMode.MirroredRepeat,
            WrapMode.ClampToEdge => SamplerAddressMode.ClampToEdge,
            WrapMode.ClampToBorder => SamplerAddressMode.ClampToBorder,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), "Unsupported wrap mode."),
        };
    }

    private static ImageViewType ToImageViewType(TextureDimension dimension, uint arrayLayers)
    {
        return dimension switch
        {
            TextureDimension.Type1D => arrayLayers == 1 ? ImageViewType.Type1D : ImageViewType.Type1DArray,
            TextureDimension.Type2D => arrayLayers == 1 ? ImageViewType.Type2D : ImageViewType.Type2DArray,
            TextureDimension.Type3D => ImageViewType.Type3D,
            TextureDimension.CubeMap => arrayLayers == 6 ? ImageViewType.TypeCube : ImageViewType.TypeCubeArray,
            _ => throw new InvalidOperationException("Unsupported texture dimension."),
        };
    }

    private static uint GetImageArrayLayers(in TextureDescription description)
    {
        return description.Dimension == TextureDimension.Type3D ? 1 : description.ArrayLayers;
    }

    internal static Format ToVulkanFormat(TextureFormat format)
    {
        return format switch
        {
            TextureFormat.R8G8B8A8Unorm => Format.R8G8B8A8Unorm,
            TextureFormat.B8G8R8A8Unorm => Format.B8G8R8A8Unorm,
            TextureFormat.B8G8R8A8Srgb => Format.B8G8R8A8Srgb,
            TextureFormat.R8Unorm => Format.R8Unorm,
            _ => throw new InvalidOperationException("Unsupported texture format."),
        };
    }

    internal static TextureFormat FromVulkanFormat(Format format)
    {
        return format switch
        {
            Format.R8G8B8A8Unorm => TextureFormat.R8G8B8A8Unorm,
            Format.B8G8R8A8Unorm => TextureFormat.B8G8R8A8Unorm,
            Format.B8G8R8A8Srgb => TextureFormat.B8G8R8A8Srgb,
            Format.R8Unorm => TextureFormat.R8Unorm,
            _ => throw new InvalidOperationException("Unsupported Vulkan texture format."),
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
            TextureFormat.B8G8R8A8Srgb => 4,
            TextureFormat.R8Unorm => 1,
            _ => throw new InvalidOperationException("Unsupported texture format."),
        };
    }

    private static ulong CalculateTextureDataSize(uint width, uint height, uint depth, uint arrayLayers,
        TextureFormat format)
    {
        return checked((ulong)width * height * depth * arrayLayers * GetTextureBytesPerPixel(format));
    }
}