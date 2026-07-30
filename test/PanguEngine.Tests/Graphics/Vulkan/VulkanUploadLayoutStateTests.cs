using PanguEngine.Graphics.Vulkan;
using Silk.NET.Vulkan;

namespace PanguEngine.Tests.Graphics.Vulkan;

public sealed class VulkanUploadLayoutStateTests
{
    [Fact]
    public void TransactionChangesDoNotMutateOriginalUntilMerged()
    {
        var state = new VulkanUploadLayoutState(
            2,
            2,
            (_, _) => ImageLayout.Undefined);
        var transaction = state.Clone();

        transaction.Set(0, 1, ImageLayout.TransferDstOptimal);

        Assert.Equal(ImageLayout.Undefined, state.Get(0, 1));
        state.Merge(transaction);
        Assert.Equal(ImageLayout.TransferDstOptimal, state.Get(0, 1));
    }

    [Fact]
    public void AllBaseMipsMustBeInitializedAcrossArrayLayers()
    {
        var state = new VulkanUploadLayoutState(
            3,
            2,
            (_, layer) => layer == 0
                ? ImageLayout.ShaderReadOnlyOptimal
                : ImageLayout.Undefined);

        Assert.False(state.AreAllBaseMipsInitialized());
        state.Set(0, 1, ImageLayout.ShaderReadOnlyOptimal);
        Assert.True(state.AreAllBaseMipsInitialized());
    }

    [Fact]
    public void EnumeratesLayoutsInMipThenLayerOrder()
    {
        var state = new VulkanUploadLayoutState(2, 2, (_, _) => ImageLayout.Undefined);
        state.Set(0, 0, ImageLayout.TransferSrcOptimal);
        state.Set(0, 1, ImageLayout.TransferDstOptimal);
        state.Set(1, 0, ImageLayout.ShaderReadOnlyOptimal);
        state.Set(1, 1, ImageLayout.ColorAttachmentOptimal);

        VulkanSubresourceLayout[] expected =
        [
            new VulkanSubresourceLayout(0, 0, ImageLayout.TransferSrcOptimal),
            new VulkanSubresourceLayout(0, 1, ImageLayout.TransferDstOptimal),
            new VulkanSubresourceLayout(1, 0, ImageLayout.ShaderReadOnlyOptimal),
            new VulkanSubresourceLayout(1, 1, ImageLayout.ColorAttachmentOptimal)
        ];

        Assert.Equal(expected, state.EnumerateLayouts());
    }
}