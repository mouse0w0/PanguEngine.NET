using PanguEngine.Graphics.Vulkan;

namespace PanguEngine.Tests.Graphics.Vulkan;

public sealed class VulkanStagingPagePoolTests
{
    [Theory]
    [InlineData(0ul, 1ul, 0ul)]
    [InlineData(1ul, 4ul, 4ul)]
    [InlineData(15ul, 4ul, 16ul)]
    public void AlignOffsetUsesRequestedAlignment(
        ulong offset,
        ulong alignment,
        ulong expected)
    {
        Assert.Equal(expected, VulkanStagingPagePool.AlignOffset(offset, alignment));
    }

    [Fact]
    public void OversizedRequestAllocatesDedicatedPage()
    {
        var createdPages = new List<(ulong Capacity, bool Dedicated, int RegularCreationIndex)>();
        var pool = new VulkanStagingPagePool(
            4,
            (capacity, dedicated, regularCreationIndex) =>
            {
                createdPages.Add((capacity, dedicated, regularCreationIndex));
                return new VulkanStagingPage(
                    null!,
                    0,
                    capacity,
                    dedicated,
                    regularCreationIndex,
                    (_, _) => { },
                    () => { });
            });

        var segment = pool.Allocate(5, 1);

        Assert.Equal(-1, segment.PageIdentity);
        Assert.Equal(2, createdPages.Count);
        Assert.Equal((4ul, false, 0), createdPages[0]);
        Assert.Equal((5ul, true, -1), createdPages[1]);

        pool.Destroy();
    }

    [Theory]
    [InlineData(false, 0, true)]
    [InlineData(false, 1, true)]
    [InlineData(false, 2, false)]
    [InlineData(true, -1, false)]
    public void OnlyFirstTwoRegularPagesAreRetained(
        bool dedicated,
        int regularCreationIndex,
        bool expected)
    {
        Assert.Equal(
            expected,
            VulkanStagingPagePool.ShouldRetainPage(dedicated, regularCreationIndex));
    }

    [Fact]
    public void AllocationAlignsAndStaysWithinOnePage()
    {
        Assert.True(VulkanStagingPagePool.TryAllocateInPage(
            8, 3, 2, 4, out var segmentOffset, out var nextOffset));
        Assert.Equal(4ul, segmentOffset);
        Assert.Equal(6ul, nextOffset);
    }

    [Fact]
    public void AllocationPastPageEndDoesNotAdvanceOffset()
    {
        Assert.False(VulkanStagingPagePool.TryAllocateInPage(
            5, 3, 2, 4, out _, out var nextOffset));
        Assert.Equal(3ul, nextOffset);
    }

    [Fact]
    public void ReusesBothCachedRegularPagesBeforeCreatingAnotherPage()
    {
        var createdPages = 0;
        var pool = new VulkanStagingPagePool(
            4,
            (capacity, dedicated, regularCreationIndex) =>
            {
                createdPages++;
                return new VulkanStagingPage(
                    null!,
                    0,
                    capacity,
                    dedicated,
                    regularCreationIndex,
                    (_, _) => { },
                    () => { });
            });

        Assert.Equal(0, pool.Allocate(4, 1).PageIdentity);
        Assert.Equal(1, pool.Allocate(4, 1).PageIdentity);
        pool.CompleteSubmittedBatch();

        Assert.Equal(1, pool.Allocate(4, 1).PageIdentity);
        Assert.Equal(0, pool.Allocate(4, 1).PageIdentity);
        Assert.Equal(2, createdPages);

        pool.Destroy();
    }
}