using Silk.NET.Vulkan;
using VkFrontFace = Silk.NET.Vulkan.FrontFace;
using VkPrimitiveTopology = Silk.NET.Vulkan.PrimitiveTopology;
using VkVertexInputRate = Silk.NET.Vulkan.VertexInputRate;

namespace PanguEngine.Graphics.Vulkan;

internal static class VulkanMapping
{
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

    internal static Filter ToVulkanFilter(FilterMode mode)
    {
        return mode switch
        {
            FilterMode.Nearest => Filter.Nearest,
            FilterMode.Linear => Filter.Linear,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), "Unsupported filter mode."),
        };
    }

    internal static SamplerMipmapMode ToVulkanMipmapMode(MipmapMode mode)
    {
        return mode switch
        {
            MipmapMode.Nearest => SamplerMipmapMode.Nearest,
            MipmapMode.Linear => SamplerMipmapMode.Linear,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), "Unsupported mipmap mode."),
        };
    }

    internal static SamplerAddressMode ToVulkanAddressMode(WrapMode mode)
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

    internal static ShaderStageFlags ToShaderStageFlags(ShaderStage stage)
    {
        return stage switch
        {
            ShaderStage.Vertex => ShaderStageFlags.VertexBit,
            ShaderStage.Fragment => ShaderStageFlags.FragmentBit,
            _ => throw new ArgumentOutOfRangeException(nameof(stage), "Unsupported shader stage."),
        };
    }

    internal static VkVertexInputRate ToVulkanVertexInputRate(VertexInputRate inputRate)
    {
        return inputRate switch
        {
            VertexInputRate.Vertex => VkVertexInputRate.Vertex,
            VertexInputRate.Instance => VkVertexInputRate.Instance,
            _ => throw new ArgumentOutOfRangeException(nameof(inputRate), "Unsupported vertex input rate."),
        };
    }

    internal static Format ToVulkanVertexAttributeFormat(VertexAttributeFormat format)
    {
        return format switch
        {
            VertexAttributeFormat.Float32x2 => Format.R32G32Sfloat,
            VertexAttributeFormat.Float32x3 => Format.R32G32B32Sfloat,
            VertexAttributeFormat.Float32x4 => Format.R32G32B32A32Sfloat,
            _ => throw new ArgumentOutOfRangeException(nameof(format), "Unsupported vertex attribute format."),
        };
    }

    internal static VkPrimitiveTopology ToVulkanPrimitiveTopology(PrimitiveTopology topology)
    {
        return topology switch
        {
            PrimitiveTopology.TriangleList => VkPrimitiveTopology.TriangleList,
            _ => throw new ArgumentOutOfRangeException(nameof(topology), "Unsupported primitive topology."),
        };
    }

    internal static CullModeFlags ToVulkanCullMode(CullMode cullMode)
    {
        return cullMode switch
        {
            CullMode.None => CullModeFlags.None,
            CullMode.Front => CullModeFlags.FrontBit,
            CullMode.Back => CullModeFlags.BackBit,
            _ => throw new ArgumentOutOfRangeException(nameof(cullMode), "Unsupported cull mode."),
        };
    }

    internal static VkFrontFace ToVulkanFrontFace(FrontFace frontFace)
    {
        return frontFace switch
        {
            FrontFace.Clockwise => VkFrontFace.Clockwise,
            FrontFace.CounterClockwise => VkFrontFace.CounterClockwise,
            _ => throw new ArgumentOutOfRangeException(nameof(frontFace), "Unsupported front face."),
        };
    }

    internal static AttachmentLoadOp ToVulkanLoadOperation(LoadOperation operation)
    {
        return operation switch
        {
            LoadOperation.Load => AttachmentLoadOp.Load,
            LoadOperation.Clear => AttachmentLoadOp.Clear,
            LoadOperation.DontCare => AttachmentLoadOp.DontCare,
            _ => throw new ArgumentOutOfRangeException(nameof(operation), "Unsupported load operation."),
        };
    }

    internal static AttachmentStoreOp ToVulkanStoreOperation(StoreOperation operation)
    {
        return operation switch
        {
            StoreOperation.Store => AttachmentStoreOp.Store,
            StoreOperation.DontCare => AttachmentStoreOp.DontCare,
            _ => throw new ArgumentOutOfRangeException(nameof(operation), "Unsupported store operation."),
        };
    }

    internal static IndexType ToVulkanIndexType(IndexFormat format)
    {
        return format switch
        {
            IndexFormat.UInt16 => IndexType.Uint16,
            IndexFormat.UInt32 => IndexType.Uint32,
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported index format."),
        };
    }

    internal static ImageType ToVulkanImageType(TextureDimension dimension)
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

    internal static ImageViewType ToVulkanImageViewType(TextureDimension dimension, uint arrayLayers)
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

    internal static ImageUsageFlags ToVulkanImageUsage(TextureUsage usage)
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
}