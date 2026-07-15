using Silk.NET.Vulkan;

namespace PanguEngine.Graphics.Vulkan;

/// <summary>
/// Shared helpers for recording Vulkan pipeline barriers and image layout transitions.
/// </summary>
internal static unsafe class VulkanBarrier
{
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
            ImageLayout.ShaderReadOnlyOptimal => PipelineStageFlags2.FragmentShaderBit,
            ImageLayout.TransferDstOptimal => PipelineStageFlags2.TransferBit,
            ImageLayout.TransferSrcOptimal => PipelineStageFlags2.TransferBit,
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
            ImageLayout.Undefined => AccessFlags2.None,
            ImageLayout.ColorAttachmentOptimal =>
                AccessFlags2.ColorAttachmentReadBit | AccessFlags2.ColorAttachmentWriteBit,
            ImageLayout.ShaderReadOnlyOptimal => AccessFlags2.ShaderSampledReadBit,
            ImageLayout.TransferDstOptimal => AccessFlags2.TransferWriteBit,
            ImageLayout.TransferSrcOptimal => AccessFlags2.TransferReadBit,
            ImageLayout.PresentSrcKhr => AccessFlags2.None,
            ImageLayout.DepthStencilAttachmentOptimal =>
                AccessFlags2.DepthStencilAttachmentReadBit | AccessFlags2.DepthStencilAttachmentWriteBit,
            _ => throw new ArgumentOutOfRangeException(nameof(layout), layout, "Unsupported image layout.")
        };
    }
}