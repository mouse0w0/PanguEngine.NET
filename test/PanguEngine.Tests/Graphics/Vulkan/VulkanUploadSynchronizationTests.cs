using PanguEngine.Graphics;
using PanguEngine.Graphics.Vulkan;
using Silk.NET.Vulkan;

namespace PanguEngine.Tests.Graphics.Vulkan;

public sealed class VulkanUploadSynchronizationTests
{
    [Fact]
    public void BufferUploadDestinationIncludesTransferWriteAndVertexRead()
    {
        var scope = VulkanBarrier.GetBufferUploadDestination(
            BufferUsageFlags.TransferDstBit | BufferUsageFlags.VertexBufferBit);

        Assert.True(scope.Stage.HasFlag(PipelineStageFlags2.TransferBit));
        Assert.True(scope.Stage.HasFlag(PipelineStageFlags2.VertexAttributeInputBit));
        Assert.True(scope.Access.HasFlag(AccessFlags2.TransferWriteBit));
        Assert.True(scope.Access.HasFlag(AccessFlags2.VertexAttributeReadBit));
    }

    [Fact]
    public void BufferUploadDestinationCombinesIndexUniformAndTransferSource()
    {
        var scope = VulkanBarrier.GetBufferUploadDestination(
            BufferUsageFlags.TransferSrcBit |
            BufferUsageFlags.TransferDstBit |
            BufferUsageFlags.IndexBufferBit |
            BufferUsageFlags.UniformBufferBit);

        Assert.True(scope.Stage.HasFlag(PipelineStageFlags2.TransferBit));
        Assert.True(scope.Stage.HasFlag(PipelineStageFlags2.IndexInputBit));
        Assert.True(scope.Stage.HasFlag(PipelineStageFlags2.VertexShaderBit));
        Assert.True(scope.Stage.HasFlag(PipelineStageFlags2.FragmentShaderBit));
        Assert.True(scope.Access.HasFlag(AccessFlags2.TransferReadBit));
        Assert.True(scope.Access.HasFlag(AccessFlags2.TransferWriteBit));
        Assert.True(scope.Access.HasFlag(AccessFlags2.IndexReadBit));
        Assert.True(scope.Access.HasFlag(AccessFlags2.UniformReadBit));
    }

    [Fact]
    public void BufferUploadBarrierUsesUploadedDestinationRange()
    {
        var barrier = VulkanBarrier.CreateBufferUploadBarrier(
            default,
            13,
            7,
            BufferUsageFlags.TransferDstBit);

        Assert.Equal(13ul, barrier.Offset);
        Assert.Equal(7ul, barrier.Size);
    }

    [Fact]
    public void BufferUploadWriteBarrierWaitsForPriorConsumersAndTransfers()
    {
        var barrier = VulkanBarrier.CreateBufferUploadWriteBarrier(
            default,
            13,
            7,
            BufferUsageFlags.TransferDstBit |
            BufferUsageFlags.VertexBufferBit |
            BufferUsageFlags.IndexBufferBit);

        Assert.True(barrier.SrcStageMask.HasFlag(PipelineStageFlags2.TransferBit));
        Assert.True(barrier.SrcStageMask.HasFlag(PipelineStageFlags2.VertexAttributeInputBit));
        Assert.True(barrier.SrcStageMask.HasFlag(PipelineStageFlags2.IndexInputBit));
        Assert.True(barrier.SrcAccessMask.HasFlag(AccessFlags2.TransferWriteBit));
        Assert.True(barrier.SrcAccessMask.HasFlag(AccessFlags2.VertexAttributeReadBit));
        Assert.True(barrier.SrcAccessMask.HasFlag(AccessFlags2.IndexReadBit));
        Assert.Equal(PipelineStageFlags2.TransferBit, barrier.DstStageMask);
        Assert.Equal(AccessFlags2.TransferWriteBit, barrier.DstAccessMask);
        Assert.Equal(13ul, barrier.Offset);
        Assert.Equal(7ul, barrier.Size);
    }

    [Fact]
    public void SampledTextureTakesPriorityAndCoversBothShaderStages()
    {
        var state = VulkanBarrier.GetTextureUploadDestination(
            TextureUsage.TransferSource |
            TextureUsage.TransferDestination |
            TextureUsage.Sampled |
            TextureUsage.ColorAttachment);

        Assert.Equal(ImageLayout.ShaderReadOnlyOptimal, state.Layout);
        Assert.Equal(
            PipelineStageFlags2.VertexShaderBit | PipelineStageFlags2.FragmentShaderBit,
            state.Stage);
        Assert.Equal(AccessFlags2.ShaderSampledReadBit, state.Access);
    }

    [Theory]
    [InlineData(ImageLayout.Undefined, PipelineStageFlags2.TopOfPipeBit, AccessFlags2.None)]
    [InlineData(ImageLayout.ColorAttachmentOptimal,
        PipelineStageFlags2.ColorAttachmentOutputBit,
        AccessFlags2.ColorAttachmentReadBit | AccessFlags2.ColorAttachmentWriteBit)]
    [InlineData(ImageLayout.ShaderReadOnlyOptimal,
        PipelineStageFlags2.VertexShaderBit | PipelineStageFlags2.FragmentShaderBit,
        AccessFlags2.ShaderSampledReadBit)]
    [InlineData(ImageLayout.TransferDstOptimal, PipelineStageFlags2.TransferBit, AccessFlags2.TransferWriteBit)]
    [InlineData(ImageLayout.TransferSrcOptimal, PipelineStageFlags2.TransferBit, AccessFlags2.TransferReadBit)]
    [InlineData(ImageLayout.PresentSrcKhr, PipelineStageFlags2.BottomOfPipeBit, AccessFlags2.None)]
    [InlineData(ImageLayout.DepthStencilAttachmentOptimal,
        PipelineStageFlags2.EarlyFragmentTestsBit | PipelineStageFlags2.LateFragmentTestsBit,
        AccessFlags2.DepthStencilAttachmentReadBit | AccessFlags2.DepthStencilAttachmentWriteBit)]
    public void LayoutMappingsExposeDefinedSourceScopes(
        ImageLayout layout,
        PipelineStageFlags2 expectedStage,
        AccessFlags2 expectedAccess)
    {
        Assert.Equal(expectedStage, VulkanBarrier.GetStageForLayout(layout));
        Assert.Equal(expectedAccess, VulkanBarrier.GetAccessForLayout(layout));
    }

    [Theory]
    [InlineData(TextureUsage.TransferDestination | TextureUsage.ColorAttachment,
        ImageLayout.ColorAttachmentOptimal,
        PipelineStageFlags2.ColorAttachmentOutputBit,
        AccessFlags2.ColorAttachmentReadBit | AccessFlags2.ColorAttachmentWriteBit)]
    [InlineData(TextureUsage.TransferDestination | TextureUsage.TransferSource,
        ImageLayout.TransferSrcOptimal,
        PipelineStageFlags2.TransferBit,
        AccessFlags2.TransferReadBit)]
    [InlineData(TextureUsage.TransferDestination,
        ImageLayout.TransferDstOptimal,
        PipelineStageFlags2.TransferBit,
        AccessFlags2.TransferWriteBit)]
    public void TextureDestinationUsesDefinedPriority(
        TextureUsage usage,
        ImageLayout expectedLayout,
        PipelineStageFlags2 expectedStage,
        AccessFlags2 expectedAccess)
    {
        var state = VulkanBarrier.GetTextureUploadDestination(usage);

        Assert.Equal(expectedLayout, state.Layout);
        Assert.Equal(expectedStage, state.Stage);
        Assert.Equal(expectedAccess, state.Access);
    }

    [Theory]
    [InlineData(TextureFormat.R8Unorm, 1ul)]
    [InlineData(TextureFormat.R8G8B8A8Unorm, 4ul)]
    [InlineData(TextureFormat.B8G8R8A8Srgb, 4ul)]
    public void TextureStagingAlignmentUsesTexelBlockSize(TextureFormat format, ulong expected)
    {
        Assert.Equal(expected, VulkanBarrier.GetTextureUploadAlignment(format));
    }
}
