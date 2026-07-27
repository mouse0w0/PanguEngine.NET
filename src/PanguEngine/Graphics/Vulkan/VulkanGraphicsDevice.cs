using Silk.NET.Vulkan;
using Vma;
using VKDescriptorSetLayout = Silk.NET.Vulkan.DescriptorSetLayout;
using VmaMemoryUsage = Vma.MemoryUsage;

namespace PanguEngine.Graphics.Vulkan;

/// <summary>
/// Vulkan implementation of <see cref="GraphicsDevice"/>.
/// </summary>
internal sealed unsafe class VulkanGraphicsDevice : GraphicsDevice
{
    private sealed class CompletedUploadHandle : UploadHandle
    {
        public static readonly CompletedUploadHandle Instance = new();

        public override bool IsCompleted => true;

        public override bool IsFaulted => false;

        public override Exception? Exception => null;
    }

    /// <inheritdoc/>
    public override uint MaxTextureDimension2D => VulkanContext.MaxImageDimension2D;

    /// <inheritdoc/>
    public override void WaitIdle()
    {
        VulkanContext.EnsureRenderThread();
        VulkanContext.Vk.DeviceWaitIdle(VulkanContext.Device);
    }

    /// <inheritdoc/>
    public override Buffer CreateBuffer(in BufferDescription description)
    {
        VulkanContext.EnsureRenderThread();

        if (description.Size == 0)
            throw new ArgumentOutOfRangeException(nameof(description), "Buffer size must be greater than zero.");
        if (description.Usage == BufferUsage.None)
            throw new ArgumentException("Buffer usage must not be None.", nameof(description));

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
            SharingMode = SharingMode.Exclusive
        };

        var vmaUsage = description.MemoryUsage.Value switch
        {
            0 => VmaMemoryUsage.AutoPreferDevice,
            1 => VmaMemoryUsage.CpuToGpu,
            2 => VmaMemoryUsage.GpuToCpu,
            _ => VmaMemoryUsage.Auto
        };

        AllocationCreateInfo allocInfo = new()
        {
            Usage = vmaUsage
        };

        var persistentlyMapped = ShouldPersistentlyMap(description);
        if (persistentlyMapped)
        {
            allocInfo.Usage = VmaMemoryUsage.Auto;
            allocInfo.Flags = AllocationCreateFlags.HostAccessSequentialWriteBit;
            allocInfo.RequiredFlags = MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit;
        }

        var buffer = VulkanAllocator.CreateBuffer(in bufferInfo, in allocInfo);
        if (persistentlyMapped)
            buffer.PersistentlyMapForWrite();

        return buffer;
    }

    internal static bool ShouldPersistentlyMap(in BufferDescription description)
    {
        return description.MemoryUsage == MemoryUsage.CpuToGpu;
    }

    public override UploadHandle UploadBuffer<T>(
        Buffer destination,
        ReadOnlySpan<T> data,
        ulong destinationOffset = 0)
    {
        VulkanContext.EnsureRenderThread();
        ArgumentNullException.ThrowIfNull(destination);

        var vulkanBuffer = RequireVulkanBuffer(destination);

        vulkanBuffer.ThrowIfDestroyed();

        if (destinationOffset > vulkanBuffer.Size)
            throw new ArgumentOutOfRangeException(nameof(destinationOffset),
                "Destination offset exceeds the buffer bounds.");

        var dataSize = checked((ulong)data.Length * (ulong)sizeof(T));
        if (dataSize > vulkanBuffer.Size - destinationOffset)
            throw new ArgumentOutOfRangeException(nameof(destinationOffset),
                "Destination offset and data size exceed the buffer bounds.");

        if (dataSize == 0)
            return CompletedUploadHandle.Instance;

        return VulkanUploader.EnqueueBufferUpload(vulkanBuffer, data, destinationOffset);
    }

    public override Texture CreateTexture(in TextureDescription description)
    {
        VulkanContext.EnsureRenderThread();
        ValidateTextureDescription(description);

        var imageType = VulkanMapping.ToVulkanImageType(description.Dimension);
        var imageArrayLayers = GetImageArrayLayers(description);
        var imageCreateFlags = description.Flags.HasFlag(TextureCreateFlags.CubeCompatible)
            ? ImageCreateFlags.CreateCubeCompatibleBit
            : ImageCreateFlags.None;

        ImageCreateInfo imageInfo = new()
        {
            SType = StructureType.ImageCreateInfo,
            Flags = imageCreateFlags,
            ImageType = imageType,
            Format = VulkanMapping.ToVulkanFormat(description.Format),
            Extent = new Extent3D
            {
                Width = description.Width,
                Height = description.Height,
                Depth = description.Dimension == TextureDimension.Type3D ? description.Depth : 1
            },
            MipLevels = description.MipLevels,
            ArrayLayers = imageArrayLayers,
            Samples = SampleCountFlags.Count1Bit,
            Tiling = ImageTiling.Optimal,
            Usage = VulkanMapping.ToVulkanImageUsage(description.Usage),
            SharingMode = SharingMode.Exclusive,
            InitialLayout = ImageLayout.Undefined
        };

        AllocationCreateInfo allocInfo = new()
        {
            Usage = VmaMemoryUsage.AutoPreferDevice
        };

        VulkanAllocator.CreateImage(in imageInfo, in allocInfo, out var image, out var allocation);
        return new VulkanTexture(image, allocation, description.Dimension, description.Format,
            description.Width, description.Height, description.Depth, description.MipLevels, imageArrayLayers,
            description.Usage, description.Flags);
    }

    public override TextureView CreateTextureView(
        Texture texture,
        in TextureViewDescription description)
    {
        VulkanContext.EnsureRenderThread();
        ArgumentNullException.ThrowIfNull(texture);

        var vulkanTexture = RequireVulkanTexture(texture);
        vulkanTexture.ThrowIfDestroyed();
        ValidateTextureViewDescription(vulkanTexture, description);

        var imageView = vulkanTexture.CreateImageView(description);

        try
        {
            var width = VulkanTexture.GetMipExtent(vulkanTexture.Width, description.BaseMipLevel);
            var height = description.Dimension is TextureViewDimension.Type1D or TextureViewDimension.Type1DArray
                ? 1
                : VulkanTexture.GetMipExtent(vulkanTexture.Height, description.BaseMipLevel);
            var depth = description.Dimension == TextureViewDimension.Type3D
                ? VulkanTexture.GetMipExtent(vulkanTexture.Depth, description.BaseMipLevel)
                : 1;
            return new VulkanTextureView(vulkanTexture, imageView, description, width, height, depth);
        }
        catch
        {
            vulkanTexture.DestroyUnpublishedImageView(imageView);
            throw;
        }
    }

    public override UploadHandle UploadTexture(Texture destination, ReadOnlySpan<byte> data)
    {
        VulkanContext.EnsureRenderThread();
        ArgumentNullException.ThrowIfNull(destination);

        var texture = RequireVulkanTexture(destination);
        var region = texture.Dimension == TextureDimension.Type3D
            ? TextureUploadRegion.Full3D(texture.Width, texture.Height, texture.Depth)
            : TextureUploadRegion.Full2DArray(texture.Width, texture.Height, texture.ArrayLayers);
        return UploadTexture(destination, data, in region);
    }

    public override UploadHandle UploadTexture(
        Texture destination,
        ReadOnlySpan<byte> data,
        in TextureUploadRegion region)
    {
        VulkanContext.EnsureRenderThread();
        ArgumentNullException.ThrowIfNull(destination);

        var texture = RequireVulkanTexture(destination);
        texture.ThrowIfDestroyed();

        if (VulkanMapping.IsDepthStencilFormat(texture.Format))
            throw new InvalidOperationException("Depth/stencil textures do not support texture uploads.");

        if (!texture.Usage.HasFlag(TextureUsage.TransferDestination))
            throw new InvalidOperationException("Texture was not created with TransferDestination usage.");

        ValidateTextureUploadRegion(texture, region);
        var requiredSize = CalculateTextureDataSize(region.Width, region.Height, region.Depth, region.LayerCount,
            texture.Format);
        if ((ulong)data.Length != requiredSize)
            throw new ArgumentException("Texture upload data size does not match the region size.", nameof(data));

        return VulkanUploader.EnqueueTextureUpload(texture, data, region);
    }

    public override UploadHandle GenerateMipmaps(Texture texture)
    {
        VulkanContext.EnsureRenderThread();
        ArgumentNullException.ThrowIfNull(texture);

        var vulkanTexture = RequireVulkanTexture(texture);
        vulkanTexture.ThrowIfDestroyed();

        if (VulkanMapping.IsDepthStencilFormat(vulkanTexture.Format))
            throw new InvalidOperationException("Depth/stencil textures do not support mipmap generation.");

        if (vulkanTexture.MipLevels == 1)
            return CompletedUploadHandle.Instance;

        if (vulkanTexture.Dimension == TextureDimension.Type3D)
            throw new NotSupportedException("3D texture mipmap generation is not supported.");

        if (!vulkanTexture.Usage.HasFlag(TextureUsage.TransferSource))
            throw new InvalidOperationException("Texture was not created with TransferSource usage.");
        if (!vulkanTexture.Usage.HasFlag(TextureUsage.TransferDestination))
            throw new InvalidOperationException("Texture was not created with TransferDestination usage.");

        ValidateMipmapGenerationFormat(vulkanTexture.Format);
        return VulkanUploader.EnqueueMipmapGeneration(vulkanTexture);
    }

    public override Sampler CreateSampler(in SamplerDescription description)
    {
        VulkanContext.EnsureRenderThread();
        ValidateSamplerDescription(description);

        var anisotropyEnable = description.MaxAnisotropy > 1;
        SamplerCreateInfo samplerInfo = new()
        {
            SType = StructureType.SamplerCreateInfo,
            MinFilter = VulkanMapping.ToVulkanFilter(description.MinFilter),
            MagFilter = VulkanMapping.ToVulkanFilter(description.MagFilter),
            MipmapMode = VulkanMapping.ToVulkanMipmapMode(description.MipmapMode),
            AddressModeU = VulkanMapping.ToVulkanAddressMode(description.AddressU),
            AddressModeV = VulkanMapping.ToVulkanAddressMode(description.AddressV),
            AddressModeW = VulkanMapping.ToVulkanAddressMode(description.AddressW),
            MipLodBias = description.MipLodBias,
            AnisotropyEnable = anisotropyEnable,
            MaxAnisotropy = anisotropyEnable ? description.MaxAnisotropy : 1,
            MinLod = description.MinLod,
            MaxLod = description.MaxLod,
            BorderColor = BorderColor.FloatTransparentBlack,
            CompareEnable = false,
            CompareOp = CompareOp.Always,
            UnnormalizedCoordinates = false
        };

        if (VulkanContext.Vk.CreateSampler(VulkanContext.Device, in samplerInfo, null, out var sampler) !=
            Result.Success)
            throw new InvalidOperationException("Failed to create Vulkan sampler.");

        return new VulkanSampler(sampler);
    }

    /// <inheritdoc/>
    public override Shader CreateShader(in ShaderDescription description)
    {
        VulkanContext.EnsureRenderThread();
        ValidateShaderDescription(description);
        return new VulkanShader(description);
    }

    /// <inheritdoc/>
    public override DescriptorSetLayout CreateDescriptorSetLayout(in DescriptorSetLayoutDescription description)
    {
        VulkanContext.EnsureRenderThread();
        return new VulkanDescriptorSetLayout(description);
    }

    /// <inheritdoc/>
    public override DescriptorSet CreateDescriptorSet(in DescriptorSetDescription description)
    {
        VulkanContext.EnsureRenderThread();
        return new VulkanDescriptorSet(description);
    }

    /// <inheritdoc/>
    public override ulong GetAlignedUniformSize(ulong rawSize)
    {
        VulkanContext.EnsureRenderThread();
        if (rawSize == 0)
            throw new ArgumentOutOfRangeException(nameof(rawSize), "Raw size must be greater than zero.");

        var align = VulkanContext.MinUniformBufferOffsetAlignment;
        if (align == 0)
            throw new InvalidOperationException(
                "VulkanContext.MinUniformBufferOffsetAlignment is 0. Ensure VulkanContext is initialized.");
        return checked((rawSize + align - 1) / align * align);
    }

    /// <inheritdoc/>
    public override GraphicsPipeline CreateGraphicsPipeline(in GraphicsPipelineDescription description)
    {
        VulkanContext.EnsureRenderThread();
        ValidateGraphicsPipelineDescription(description);
        var descriptorSetLayouts = description.DescriptorSetLayouts;
        if (descriptorSetLayouts.Length == 0)
            return new VulkanGraphicsPipeline(description);

        var vulkanDescriptorSetLayouts = new VKDescriptorSetLayout[descriptorSetLayouts.Length];
        for (var i = 0; i < descriptorSetLayouts.Length; i++)
            vulkanDescriptorSetLayouts[i] = ((VulkanDescriptorSetLayout)descriptorSetLayouts[i]).DescriptorSetLayout;

        return new VulkanGraphicsPipeline(description, vulkanDescriptorSetLayouts);
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

    private static VulkanDescriptorSetLayout RequireVulkanDescriptorSetLayout(DescriptorSetLayout layout)
    {
        return layout as VulkanDescriptorSetLayout
               ?? throw new InvalidOperationException("Descriptor set layout was not created by the Vulkan backend.");
    }

    private static void ValidateShaderDescription(in ShaderDescription description)
    {
        var bytecode = description.Bytecode;
        if (bytecode is null || bytecode.Length == 0)
            throw new ArgumentException("Shader bytecode must not be empty.", nameof(description));
        if (bytecode.Length % 4 != 0)
            throw new ArgumentException("Shader bytecode length must be a multiple of 4 bytes.",
                nameof(description));
        if (string.IsNullOrWhiteSpace(description.EntryPoint))
            throw new ArgumentException("Shader entry point must not be empty.", nameof(description));
        if (description.Stage != ShaderStage.Vertex && description.Stage != ShaderStage.Fragment)
            throw new ArgumentOutOfRangeException(nameof(description), description.Stage,
                "Shader stage must identify a supported single shader stage.");
    }

    internal static void ValidateGraphicsPipelineDescription(in GraphicsPipelineDescription description)
    {
        var shaders = description.Shaders;
        if (shaders.Length == 0)
            throw new ArgumentException("Graphics pipeline must contain shaders.", nameof(description));

        var hasVertexShader = false;
        var hasFragmentShader = false;
        foreach (var shader in shaders)
        {
            if (shader == null)
                throw new ArgumentException("Graphics pipeline shaders must not contain null entries.",
                    nameof(description));

            var vulkanShader = RequireVulkanShader(shader);
            vulkanShader.ThrowIfDestroyed();

            hasVertexShader |= vulkanShader.Stage == ShaderStage.Vertex;
            hasFragmentShader |= vulkanShader.Stage == ShaderStage.Fragment;
        }

        if (!hasVertexShader)
            throw new ArgumentException("Graphics pipeline must contain a vertex shader.", nameof(description));
        if (!hasFragmentShader)
            throw new ArgumentException("Graphics pipeline must contain a fragment shader.",
                nameof(description));

        var colorFormats = description.ColorAttachmentFormats;
        if (colorFormats.Length == 0)
            throw new InvalidOperationException("At least one color attachment format must be specified.");
        foreach (var colorFormat in colorFormats)
        {
            if (colorFormat == TextureFormat.Undefined)
                throw new InvalidOperationException("Color attachment format must be specified.");
            if (VulkanMapping.IsDepthStencilFormat(colorFormat))
                throw new InvalidOperationException("Color attachment format must not be a depth/stencil format.");
            _ = VulkanMapping.ToVulkanFormat(colorFormat);
        }

        ValidateDepthStencilPipelineDescription(description);

        if (!float.IsFinite(description.Rasterizer.LineWidth) || description.Rasterizer.LineWidth < 0)
            throw new ArgumentOutOfRangeException(nameof(description),
                "Rasterizer line width must be a non-negative finite value.");

        ValidateVertexInputDescription(description.VertexInput);

        var descriptorSetLayouts = description.DescriptorSetLayouts;
        foreach (var layout in descriptorSetLayouts)
        {
            if (layout == null)
                throw new ArgumentException("Graphics pipeline descriptor set layouts must not contain null entries.",
                    nameof(description));

            var vulkanLayout = RequireVulkanDescriptorSetLayout(layout);
            vulkanLayout.ThrowIfDestroyed();
        }
    }

    private static void ValidateDepthStencilPipelineDescription(in GraphicsPipelineDescription description)
    {
        var format = description.DepthStencilAttachmentFormat;
        if (format == TextureFormat.Undefined)
        {
            if (description.DepthStencil.DepthTestEnabled || description.DepthStencil.DepthWriteEnabled)
                throw new InvalidOperationException("Depth testing requires a depth attachment format.");
            if (description.DepthStencil.StencilTestEnabled)
                throw new InvalidOperationException("Stencil testing requires a stencil attachment format.");
            return;
        }

        if (!VulkanMapping.IsDepthStencilFormat(format))
            throw new InvalidOperationException("Depth/stencil attachment format must be a depth/stencil format.");
        if ((description.DepthStencil.DepthTestEnabled || description.DepthStencil.DepthWriteEnabled) &&
            !VulkanMapping.HasDepthAspect(format))
            throw new InvalidOperationException("Depth testing requires an attachment format with a depth aspect.");
        if (description.DepthStencil.StencilTestEnabled && !VulkanMapping.HasStencilAspect(format))
            throw new InvalidOperationException("Stencil testing requires an attachment format with a stencil aspect.");
    }

    private static void ValidateVertexInputDescription(in VertexInputDescription description)
    {
        var buffers = description.Buffers;
        var attributes = description.Attributes;

        foreach (var buffer in buffers)
        {
            if (buffer.Stride == 0)
                throw new ArgumentOutOfRangeException(nameof(description),
                    "Vertex buffer stride must be greater than zero.");
        }

        foreach (var attribute in attributes)
        {
            var foundBinding = false;
            var stride = 0u;

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
                    nameof(description));
            if (attribute.Offset > stride)
                throw new ArgumentOutOfRangeException(nameof(description),
                    "Vertex attribute offset must not exceed the vertex buffer stride.");
        }
    }

    private static void ValidateSamplerDescription(in SamplerDescription description)
    {
        if (!float.IsFinite(description.MaxAnisotropy) || description.MaxAnisotropy < 0)
            throw new ArgumentOutOfRangeException(nameof(description),
                "MaxAnisotropy must be a non-negative finite value.");
        if (!float.IsFinite(description.MinLod))
            throw new ArgumentOutOfRangeException(nameof(description), "MinLod must be a finite value.");
        if (!float.IsFinite(description.MaxLod))
            throw new ArgumentOutOfRangeException(nameof(description), "MaxLod must be a finite value.");
        if (!float.IsFinite(description.MipLodBias))
            throw new ArgumentOutOfRangeException(nameof(description), "MipLodBias must be a finite value.");

        if (description.MinLod < 0)
            throw new ArgumentOutOfRangeException(nameof(description), "MinLod must be non-negative.");
        if (description.MaxLod < 0)
            throw new ArgumentOutOfRangeException(nameof(description), "MaxLod must be non-negative.");
        if (description.MinLod > description.MaxLod)
            throw new ArgumentOutOfRangeException(nameof(description), "MinLod must not exceed MaxLod.");

        if (MathF.Abs(description.MipLodBias) > VulkanContext.MaxSamplerLodBias)
            throw new ArgumentOutOfRangeException(nameof(description),
                "MipLodBias exceeds the device limit.");

        if (description.MaxAnisotropy <= 1)
            return;

        if (!VulkanContext.SamplerAnisotropySupported)
            throw new InvalidOperationException("Anisotropic filtering is not supported by the device.");
        if (description.MaxAnisotropy > VulkanContext.MaxSamplerAnisotropy)
            throw new ArgumentOutOfRangeException(nameof(description),
                "MaxAnisotropy exceeds the device limit.");
        if (description.MinFilter != FilterMode.Linear || description.MagFilter != FilterMode.Linear)
            throw new InvalidOperationException("Anisotropic filtering requires MinFilter and MagFilter to be Linear.");
    }

    private static void ValidateTextureDescription(in TextureDescription description)
    {
        if (description.Width == 0)
            throw new ArgumentOutOfRangeException(nameof(description),
                "Texture width must be greater than zero.");
        if (description.Height == 0)
            throw new ArgumentOutOfRangeException(nameof(description),
                "Texture height must be greater than zero.");
        if (description.Depth == 0)
            throw new ArgumentOutOfRangeException(nameof(description),
                "Texture depth must be greater than zero.");
        if (description.ArrayLayers == 0)
            throw new ArgumentOutOfRangeException(nameof(description),
                "Texture array layers must be greater than zero.");
        if (description.MipLevels == 0)
            throw new ArgumentOutOfRangeException(nameof(description),
                "Texture mip levels must be greater than zero.");
        if (description.MipLevels > GetMaxMipLevels(description.Width, description.Height, description.Depth))
            throw new ArgumentOutOfRangeException(nameof(description),
                "Texture mip levels exceed the maximum allowed by the texture extent.");
        if (description.Usage == TextureUsage.None)
            throw new ArgumentException("Texture usage must not be None.", nameof(description));
        if (description.Format == TextureFormat.Undefined)
            throw new InvalidOperationException("Texture format must be specified.");
        if (description.Usage.HasFlag(TextureUsage.ColorAttachment) &&
            description.Usage.HasFlag(TextureUsage.DepthStencilAttachment))
            throw new InvalidOperationException(
                "Texture cannot be both a color attachment and a depth/stencil attachment.");
        if (description.Usage.HasFlag(TextureUsage.ColorAttachment))
            ValidateColorAttachmentFormat(description.Format);
        if (description.Usage.HasFlag(TextureUsage.DepthStencilAttachment))
            ValidateDepthStencilAttachmentFormat(description.Format);
        else if (VulkanMapping.IsDepthStencilFormat(description.Format))
            throw new InvalidOperationException("Depth/stencil texture formats require DepthStencilAttachment usage.");
        ValidateSampledAndTransferFormat(description);
        if (description.ArrayLayers > VulkanContext.MaxImageArrayLayers)
            throw new ArgumentOutOfRangeException(nameof(description),
                "Texture array layers exceed the device limit.");

        switch (description.Dimension)
        {
            case TextureDimension.Type1D:
                if (description.Width > VulkanContext.MaxImageDimension1D)
                    throw new ArgumentOutOfRangeException(nameof(description),
                        "Texture width exceeds the device limit.");
                if (description.Height != 1)
                    throw new ArgumentOutOfRangeException(nameof(description),
                        "1D textures must have a height of one.");
                if (description.Depth != 1)
                    throw new ArgumentOutOfRangeException(nameof(description),
                        "1D textures must have a depth of one.");
                break;
            case TextureDimension.Type2D:
                if (description.Width > VulkanContext.MaxImageDimension2D)
                    throw new ArgumentOutOfRangeException(nameof(description),
                        "Texture width exceeds the device limit.");
                if (description.Height > VulkanContext.MaxImageDimension2D)
                    throw new ArgumentOutOfRangeException(nameof(description),
                        "Texture height exceeds the device limit.");
                if (description.Depth != 1)
                    throw new ArgumentOutOfRangeException(nameof(description),
                        "2D textures must have a depth of one.");
                break;
            case TextureDimension.Type3D:
                if (description.Width > VulkanContext.MaxImageDimension3D)
                    throw new ArgumentOutOfRangeException(nameof(description),
                        "Texture width exceeds the device limit.");
                if (description.Height > VulkanContext.MaxImageDimension3D)
                    throw new ArgumentOutOfRangeException(nameof(description),
                        "Texture height exceeds the device limit.");
                if (description.Depth > VulkanContext.MaxImageDimension3D)
                    throw new ArgumentOutOfRangeException(nameof(description),
                        "Texture depth exceeds the device limit.");
                if (description.ArrayLayers != 1)
                    throw new ArgumentOutOfRangeException(nameof(description),
                        "3D textures must have exactly one array layer.");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(description), "Unsupported texture dimension.");
        }

        if (description.Flags.HasFlag(TextureCreateFlags.CubeCompatible))
        {
            if (description.Dimension != TextureDimension.Type2D)
                throw new ArgumentException("Cube-compatible textures must be two-dimensional.",
                    nameof(description));
            if (description.Width != description.Height)
                throw new ArgumentException("Cube-compatible textures must be square.", nameof(description));
            if (description.ArrayLayers < 6)
                throw new ArgumentException("Cube-compatible textures must have at least six array layers.",
                    nameof(description));
        }
    }

    private static void ValidateSampledAndTransferFormat(in TextureDescription description)
    {
        var required = FormatFeatureFlags.None;
        if (description.Usage.HasFlag(TextureUsage.Sampled))
            required |= FormatFeatureFlags.SampledImageBit;
        if (description.Usage.HasFlag(TextureUsage.TransferSource))
            required |= FormatFeatureFlags.TransferSrcBit;
        if (description.Usage.HasFlag(TextureUsage.TransferDestination))
            required |= FormatFeatureFlags.TransferDstBit;
        if (required == FormatFeatureFlags.None)
            return;

        var format = VulkanMapping.ToVulkanFormat(description.Format);
        VulkanContext.Vk.GetPhysicalDeviceFormatProperties(
            VulkanContext.PhysicalDevice,
            format,
            out var properties);
        if ((properties.OptimalTilingFeatures & required) != required)
        {
            throw new InvalidOperationException(
                $"Texture format '{description.Format}' does not support the requested sampled/transfer usage.");
        }
    }

    private static void ValidateTextureViewDescription(
        VulkanTexture texture,
        in TextureViewDescription description)
    {
        if (description.MipLevels == 0)
            throw new ArgumentOutOfRangeException(nameof(description),
                "Texture view mip levels must be greater than zero.");
        if (description.ArrayLayers == 0)
            throw new ArgumentOutOfRangeException(nameof(description),
                "Texture view array layers must be greater than zero.");
        if (description.BaseMipLevel >= texture.MipLevels)
            throw new ArgumentOutOfRangeException(nameof(description),
                "Texture view base mip level is out of range.");
        if (description.MipLevels > texture.MipLevels - description.BaseMipLevel)
            throw new ArgumentOutOfRangeException(nameof(description),
                "Texture view mip range exceeds the texture bounds.");
        if (description.BaseArrayLayer >= texture.ArrayLayers)
            throw new ArgumentOutOfRangeException(nameof(description),
                "Texture view base array layer is out of range.");
        if (description.ArrayLayers > texture.ArrayLayers - description.BaseArrayLayer)
            throw new ArgumentOutOfRangeException(nameof(description),
                "Texture view array layer range exceeds the texture bounds.");

        switch (texture.Dimension)
        {
            case TextureDimension.Type1D:
                if (description.Dimension != TextureViewDimension.Type1D &&
                    description.Dimension != TextureViewDimension.Type1DArray)
                    throw new ArgumentException("1D textures require 1D or 1D array texture views.",
                        nameof(description));
                if (description.Dimension == TextureViewDimension.Type1D && description.ArrayLayers != 1)
                    throw new ArgumentException("1D texture views must include exactly one array layer.",
                        nameof(description));
                break;
            case TextureDimension.Type2D:
                if (description.Dimension == TextureViewDimension.Type2D)
                {
                    if (description.ArrayLayers != 1)
                        throw new ArgumentException("2D texture views must include exactly one array layer.",
                            nameof(description));
                    break;
                }

                if (description.Dimension == TextureViewDimension.Type2DArray)
                    break;
                if (description.Dimension != TextureViewDimension.Cube &&
                    description.Dimension != TextureViewDimension.CubeArray)
                    throw new ArgumentException("2D textures require 2D, 2D array, cube, or cube array views.",
                        nameof(description));
                if (!texture.CreateFlags.HasFlag(TextureCreateFlags.CubeCompatible))
                    throw new ArgumentException("Cube texture views require a cube-compatible texture.",
                        nameof(description));
                if (description.BaseArrayLayer % 6 != 0)
                    throw new ArgumentException("Cube texture view base array layers must be aligned to six layers.",
                        nameof(description));
                if (description.Dimension == TextureViewDimension.Cube && description.ArrayLayers != 6)
                    throw new ArgumentException("Cube texture views must include exactly six array layers.",
                        nameof(description));
                if (description.Dimension == TextureViewDimension.CubeArray && description.ArrayLayers % 6 != 0)
                    throw new ArgumentException("Cube array texture view layers must be a multiple of six.",
                        nameof(description));
                break;
            case TextureDimension.Type3D:
                if (description.Dimension != TextureViewDimension.Type3D)
                    throw new ArgumentException("3D textures require 3D texture views.", nameof(description));
                if (description.BaseArrayLayer != 0 || description.ArrayLayers != 1)
                    throw new ArgumentException("3D texture views must include the single texture array layer.",
                        nameof(description));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(texture), "Unsupported texture dimension.");
        }
    }

    private static void ValidateTextureUploadRegion(VulkanTexture texture, in TextureUploadRegion region)
    {
        if (region.MipLevel >= texture.MipLevels)
            throw new ArgumentOutOfRangeException(nameof(region), "Texture upload mip level is out of range.");
        if (region.Width == 0)
            throw new ArgumentOutOfRangeException(nameof(region),
                "Texture upload width must be greater than zero.");
        if (region.Height == 0)
            throw new ArgumentOutOfRangeException(nameof(region),
                "Texture upload height must be greater than zero.");
        if (region.Depth == 0)
            throw new ArgumentOutOfRangeException(nameof(region),
                "Texture upload depth must be greater than zero.");
        if (region.LayerCount == 0)
            throw new ArgumentOutOfRangeException(nameof(region),
                "Texture upload layer count must be greater than zero.");

        var mipWidth = VulkanTexture.GetMipExtent(texture.Width, region.MipLevel);
        var mipHeight = VulkanTexture.GetMipExtent(texture.Height, region.MipLevel);
        var mipDepth = VulkanTexture.GetMipExtent(texture.Depth, region.MipLevel);

        if (region.X > mipWidth || region.Width > mipWidth - region.X)
            throw new ArgumentOutOfRangeException(nameof(region), "Texture upload X range exceeds the mip bounds.");
        if (region.Y > mipHeight || region.Height > mipHeight - region.Y)
            throw new ArgumentOutOfRangeException(nameof(region), "Texture upload Y range exceeds the mip bounds.");
        if (region.Z > mipDepth || region.Depth > mipDepth - region.Z)
            throw new ArgumentOutOfRangeException(nameof(region), "Texture upload Z range exceeds the mip bounds.");

        switch (texture.Dimension)
        {
            case TextureDimension.Type1D:
                if (region.Y != 0 || region.Height != 1 || region.Z != 0 || region.Depth != 1)
                    throw new ArgumentException("1D texture uploads must target a one-dimensional region.",
                        nameof(region));
                ValidateTextureUploadArrayLayers(texture, region);
                break;
            case TextureDimension.Type2D:
                if (region.Z != 0 || region.Depth != 1)
                    throw new ArgumentException("2D texture uploads must target a two-dimensional region.",
                        nameof(region));
                ValidateTextureUploadArrayLayers(texture, region);
                break;
            case TextureDimension.Type3D:
                if (region.ArrayLayer != 0 || region.LayerCount != 1)
                    throw new ArgumentException("3D texture uploads must not target array layers.", nameof(region));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(texture), "Unsupported texture dimension.");
        }
    }

    private static void ValidateTextureUploadArrayLayers(VulkanTexture texture, in TextureUploadRegion region)
    {
        if (region.ArrayLayer >= texture.ArrayLayers)
            throw new ArgumentOutOfRangeException(nameof(region),
                "Texture upload array layer is out of range.");
        if (region.LayerCount > texture.ArrayLayers - region.ArrayLayer)
            throw new ArgumentOutOfRangeException(nameof(region),
                "Texture upload layer range exceeds the texture bounds.");
    }

    private static void ValidateMipmapGenerationFormat(TextureFormat format)
    {
        var vkFormat = VulkanMapping.ToVulkanFormat(format);
        VulkanContext.Vk.GetPhysicalDeviceFormatProperties(VulkanContext.PhysicalDevice, vkFormat,
            out var properties);
        const FormatFeatureFlags required = FormatFeatureFlags.BlitSrcBit
                                            | FormatFeatureFlags.BlitDstBit
                                            | FormatFeatureFlags.SampledImageFilterLinearBit;
        if ((properties.OptimalTilingFeatures & required) != required)
            throw new InvalidOperationException("Texture format does not support linear blit mipmap generation.");
    }

    private static void ValidateDepthStencilAttachmentFormat(TextureFormat format)
    {
        if (!VulkanMapping.IsDepthStencilFormat(format))
            throw new InvalidOperationException(
                "Depth/stencil attachment usage requires a depth/stencil texture format.");

        var vkFormat = VulkanMapping.ToVulkanFormat(format);
        VulkanContext.Vk.GetPhysicalDeviceFormatProperties(VulkanContext.PhysicalDevice, vkFormat,
            out var properties);
        if ((properties.OptimalTilingFeatures & FormatFeatureFlags.DepthStencilAttachmentBit) == 0)
            throw new InvalidOperationException(
                $"Texture format '{format}' does not support depth/stencil attachments.");
    }

    private static void ValidateColorAttachmentFormat(TextureFormat format)
    {
        if (VulkanMapping.IsDepthStencilFormat(format))
            throw new InvalidOperationException("Color attachment usage requires a color texture format.");

        var vkFormat = VulkanMapping.ToVulkanFormat(format);
        VulkanContext.Vk.GetPhysicalDeviceFormatProperties(VulkanContext.PhysicalDevice, vkFormat,
            out var properties);
        if ((properties.OptimalTilingFeatures & FormatFeatureFlags.ColorAttachmentBit) == 0)
            throw new InvalidOperationException($"Texture format '{format}' does not support color attachments.");
    }

    private static uint GetImageArrayLayers(in TextureDescription description)
    {
        return description.Dimension == TextureDimension.Type3D ? 1 : description.ArrayLayers;
    }

    private static uint GetMaxMipLevels(uint width, uint height, uint depth)
    {
        var maxExtent = Math.Max(width, Math.Max(height, depth));
        uint levels = 1;
        while (maxExtent > 1)
        {
            maxExtent >>= 1;
            levels++;
        }

        return levels;
    }

    private static ulong CalculateTextureDataSize(uint width, uint height, uint depth, uint arrayLayers,
        TextureFormat format)
    {
        return checked((ulong)width * height * depth * arrayLayers * VulkanMapping.GetTextureBytesPerPixel(format));
    }
}