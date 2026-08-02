using PanguEngine.Graphics.Vulkan;

namespace PanguEngine.Tests.Graphics.Vulkan;

public sealed class VulkanIndirectDrawValidationTests
{
    [Fact]
    public void ZeroDrawCountIgnoresStrideAndCommandRange()
    {
        VulkanCommandList.ValidateIndexedIndirectDraw(0, 16, 0, ulong.MaxValue - 3, 1);
    }

    [Fact]
    public void SingleDrawRequiresOneCommandAtOffset()
    {
        VulkanCommandList.ValidateIndexedIndirectDraw(20, 16, 1, 0, 1);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            VulkanCommandList.ValidateIndexedIndirectDraw(19, 16, 1, 0, 1));
    }

    [Fact]
    public void MultipleDrawsRequireAlignedStrideAndExactRange()
    {
        VulkanCommandList.ValidateIndexedIndirectDraw(60, 16, 3, 0, 20);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            VulkanCommandList.ValidateIndexedIndirectDraw(60, 16, 3, 0, 19));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            VulkanCommandList.ValidateIndexedIndirectDraw(59, 16, 3, 0, 20));
    }

    [Fact]
    public void DrawCountCannotExceedDeviceLimit()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            VulkanCommandList.ValidateIndexedIndirectDraw(40, 1, 2, 0, 20));
    }
}
