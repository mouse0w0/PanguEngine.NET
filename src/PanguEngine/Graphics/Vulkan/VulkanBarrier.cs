using Silk.NET.Vulkan;
using VkBuffer = Silk.NET.Vulkan.Buffer;

namespace PanguEngine.Graphics.Vulkan;

internal readonly record struct VulkanAccessScope(
    PipelineStageFlags2 Stage,
    AccessFlags2 Access);

internal readonly record struct VulkanImageState(
    ImageLayout Layout,
    PipelineStageFlags2 Stage,
    AccessFlags2 Access);

/// <summary>
/// Shared helpers for recording Vulkan pipeline barriers and image layout transitions.
/// </summary>
internal static unsafe class VulkanBarrier
{
    internal static VulkanAccessScope GetBufferUploadDestination(BufferUsageFlags usage)
    {
        var stage = PipelineStageFlags2.TransferBit;
        var access = AccessFlags2.TransferWriteBit;

        if (usage.HasFlag(BufferUsageFlags.TransferSrcBit))
            access |= AccessFlags2.TransferReadBit;
        if (usage.HasFlag(BufferUsageFlags.VertexBufferBit))
        {
            stage |= PipelineStageFlags2.VertexAttributeInputBit;
            access |= AccessFlags2.VertexAttributeReadBit;
        }

        if (usage.HasFlag(BufferUsageFlags.IndexBufferBit))
        {
            stage |= PipelineStageFlags2.IndexInputBit;
            access |= AccessFlags2.IndexReadBit;
        }

        if (usage.HasFlag(BufferUsageFlags.UniformBufferBit))
        {
            stage |= PipelineStageFlags2.VertexShaderBit | PipelineStageFlags2.FragmentShaderBit;
            access |= AccessFlags2.UniformReadBit;
        }

        return new VulkanAccessScope(stage, access);
    }

    internal static VulkanImageState GetTextureUploadDestination(TextureUsage usage)
    {
        if (usage.HasFlag(TextureUsage.Sampled))
            return new VulkanImageState(
                ImageLayout.ShaderReadOnlyOptimal,
                PipelineStageFlags2.VertexShaderBit | PipelineStageFlags2.FragmentShaderBit,
                AccessFlags2.ShaderSampledReadBit);
        if (usage.HasFlag(TextureUsage.ColorAttachment))
            return new VulkanImageState(
                ImageLayout.ColorAttachmentOptimal,
                PipelineStageFlags2.ColorAttachmentOutputBit,
                AccessFlags2.ColorAttachmentReadBit | AccessFlags2.ColorAttachmentWriteBit);
        if (usage.HasFlag(TextureUsage.TransferSource))
            return new VulkanImageState(
                ImageLayout.TransferSrcOptimal,
                PipelineStageFlags2.TransferBit,
                AccessFlags2.TransferReadBit);

        return new VulkanImageState(
            ImageLayout.TransferDstOptimal,
            PipelineStageFlags2.TransferBit,
            AccessFlags2.TransferWriteBit);
    }

    internal static ulong GetTextureUploadAlignment(TextureFormat format)
    {
        return VulkanMapping.GetTextureBytesPerPixel(format);
    }

    internal static BufferMemoryBarrier2 CreateBufferUploadBarrier(
        VkBuffer buffer,
        ulong offset,
        ulong size,
        BufferUsageFlags usage)
    {
        var destination = GetBufferUploadDestination(usage);
        return new BufferMemoryBarrier2
        {
            SType = StructureType.BufferMemoryBarrier2,
            SrcStageMask = PipelineStageFlags2.TransferBit,
            SrcAccessMask = AccessFlags2.TransferWriteBit,
            DstStageMask = destination.Stage,
            DstAccessMask = destination.Access,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Buffer = buffer,
            Offset = offset,
            Size = size
        };
    }

    internal static void RecordBufferUploadBarrier(
        CommandBuffer commandBuffer,
        VkBuffer buffer,
        ulong offset,
        ulong size,
        BufferUsageFlags usage)
    {
        var barrier = CreateBufferUploadBarrier(buffer, offset, size, usage);
        DependencyInfo dependency = new()
        {
            SType = StructureType.DependencyInfo,
            BufferMemoryBarrierCount = 1,
            PBufferMemoryBarriers = &barrier
        };
        VulkanContext.Vk.CmdPipelineBarrier2(commandBuffer, &dependency);
    }

    internal static BufferMemoryBarrier2 CreateBufferUploadWriteBarrier(
        VkBuffer buffer,
        ulong offset,
        ulong size,
        BufferUsageFlags usage)
    {
        var source = GetBufferUploadDestination(usage);
        return new BufferMemoryBarrier2
        {
            SType = StructureType.BufferMemoryBarrier2,
            SrcStageMask = source.Stage,
            SrcAccessMask = source.Access,
            DstStageMask = PipelineStageFlags2.TransferBit,
            DstAccessMask = AccessFlags2.TransferWriteBit,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Buffer = buffer,
            Offset = offset,
            Size = size
        };
    }

    internal static void RecordBufferUploadWriteBarrier(
        CommandBuffer commandBuffer,
        VkBuffer buffer,
        ulong offset,
        ulong size,
        BufferUsageFlags usage)
    {
        var barrier = CreateBufferUploadWriteBarrier(buffer, offset, size, usage);
        DependencyInfo dependency = new()
        {
            SType = StructureType.DependencyInfo,
            BufferMemoryBarrierCount = 1,
            PBufferMemoryBarriers = &barrier
        };
        VulkanContext.Vk.CmdPipelineBarrier2(commandBuffer, &dependency);
    }

    /// <summary>
    /// Records a single image memory barrier via <c>vkCmdPipelineBarrier2</c>.
    /// </summary>
    public static void RecordImageLayoutTransition(
        CommandBuffer commandBuffer,
        Image image,
        uint mipLevel,
        uint levelCount,
        uint baseArrayLayer,
        uint layerCount,
        ImageAspectFlags aspectMask,
        ImageLayout oldLayout,
        ImageLayout newLayout,
        PipelineStageFlags2 srcStage,
        AccessFlags2 srcAccess,
        PipelineStageFlags2 dstStage,
        AccessFlags2 dstAccess)
    {
        ImageMemoryBarrier2 barrier = new()
        {
            SType = StructureType.ImageMemoryBarrier2,
            SrcStageMask = srcStage,
            SrcAccessMask = srcAccess,
            DstStageMask = dstStage,
            DstAccessMask = dstAccess,
            OldLayout = oldLayout,
            NewLayout = newLayout,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = image,
            SubresourceRange = new ImageSubresourceRange
            {
                AspectMask = aspectMask,
                BaseMipLevel = mipLevel,
                LevelCount = levelCount,
                BaseArrayLayer = baseArrayLayer,
                LayerCount = layerCount
            }
        };

        DependencyInfo dependency = new()
        {
            SType = StructureType.DependencyInfo,
            ImageMemoryBarrierCount = 1,
            PImageMemoryBarriers = &barrier
        };
        VulkanContext.Vk.CmdPipelineBarrier2(commandBuffer, &dependency);
    }

    /// <summary>
    /// Maps an image layout to the source pipeline stage that produces data in that layout.
    /// </summary>
    public static PipelineStageFlags2 GetStageForLayout(ImageLayout layout)
    {
        return layout switch
        {
            ImageLayout.Undefined => PipelineStageFlags2.TopOfPipeBit,
            ImageLayout.ColorAttachmentOptimal => PipelineStageFlags2.ColorAttachmentOutputBit,
            ImageLayout.ShaderReadOnlyOptimal =>
                PipelineStageFlags2.VertexShaderBit | PipelineStageFlags2.FragmentShaderBit,
            ImageLayout.TransferDstOptimal or ImageLayout.TransferSrcOptimal => PipelineStageFlags2.TransferBit,
            ImageLayout.PresentSrcKhr => PipelineStageFlags2.BottomOfPipeBit,
            ImageLayout.DepthStencilAttachmentOptimal =>
                PipelineStageFlags2.EarlyFragmentTestsBit | PipelineStageFlags2.LateFragmentTestsBit,
            _ => throw new ArgumentOutOfRangeException(nameof(layout), layout, "Unsupported image layout.")
        };
    }

    /// <summary>
    /// Maps an image layout to the source access flags expected for data in that layout.
    /// </summary>
    public static AccessFlags2 GetAccessForLayout(ImageLayout layout)
    {
        return layout switch
        {
            ImageLayout.Undefined or ImageLayout.PresentSrcKhr => AccessFlags2.None,
            ImageLayout.ColorAttachmentOptimal =>
                AccessFlags2.ColorAttachmentReadBit | AccessFlags2.ColorAttachmentWriteBit,
            ImageLayout.ShaderReadOnlyOptimal => AccessFlags2.ShaderSampledReadBit,
            ImageLayout.TransferDstOptimal => AccessFlags2.TransferWriteBit,
            ImageLayout.TransferSrcOptimal => AccessFlags2.TransferReadBit,
            ImageLayout.DepthStencilAttachmentOptimal =>
                AccessFlags2.DepthStencilAttachmentReadBit | AccessFlags2.DepthStencilAttachmentWriteBit,
            _ => throw new ArgumentOutOfRangeException(nameof(layout), layout, "Unsupported image layout.")
        };
    }
}
