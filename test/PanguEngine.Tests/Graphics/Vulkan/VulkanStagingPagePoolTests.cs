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
    public void OversizedRequestAllocatesDedicatedPage()
    {
        var pool = CreatePool(4, out var createdPages, out _, out _);
        var lease = pool.BeginBatch();

        var segment = pool.Allocate(lease, 5, 1);

        Assert.Equal(-1, segment.PageIdentity);
        Assert.Equal(2, createdPages.Count);
        Assert.Equal((4ul, false, 0), createdPages[0]);
        Assert.Equal((5ul, true, -1), createdPages[1]);

        pool.Recycle(lease);
        pool.Destroy();
    }

    [Fact]
    public void RecycleResetsRegularPageAndReusesIt()
    {
        var pool = CreatePool(4, out var createdPages, out _, out _);
        var firstLease = pool.BeginBatch();
        Assert.Equal(0, pool.Allocate(firstLease, 4, 1).PageIdentity);
        pool.Recycle(firstLease);

        var secondLease = pool.BeginBatch();
        Assert.Equal(0, pool.Allocate(secondLease, 4, 1).PageIdentity);
        Assert.Single(createdPages);

        pool.Recycle(secondLease);
        pool.Destroy();
    }

    [Fact]
    public void FlushWrittenRangesOnlyFlushesLeasePages()
    {
        var pool = CreatePool(4, out _, out var flushedPages, out _);
        var firstLease = pool.BeginBatch();
        var secondLease = pool.BeginBatch();
        pool.Allocate(firstLease, 4, 1);
        pool.Allocate(secondLease, 4, 1);

        pool.FlushWrittenRanges(firstLease);

        Assert.Equal(new[] { 0 }, flushedPages);
        pool.Recycle(firstLease);
        pool.Recycle(secondLease);
        pool.Destroy();
    }

    [Fact]
    public void DedicatedPageIsDestroyedInsteadOfCached()
    {
        var pool = CreatePool(4, out _, out _, out var destroyedPages);
        var lease = pool.BeginBatch();
        pool.Allocate(lease, 5, 1);

        pool.Recycle(lease);

        Assert.Equal(new[] { -1 }, destroyedPages);
        pool.Destroy();
    }

    [Fact]
    public void RegularCacheKeepsFourMostRecentlyRecycledPages()
    {
        var pool = CreatePool(4, out _, out _, out var destroyedPages);
        var lease = pool.BeginBatch();
        for (var index = 0; index < 5; index++)
            pool.Allocate(lease, 4, 1);

        pool.Recycle(lease);

        Assert.Equal(new[] { 0 }, destroyedPages);

        var newestLease = pool.BeginBatch();
        Assert.Equal(4, pool.Allocate(newestLease, 4, 1).PageIdentity);
        pool.Recycle(newestLease);
        pool.Destroy();
    }

    [Fact]
    public void RecyclingLeaseTwiceIsRejected()
    {
        var pool = CreatePool(4, out _, out _, out _);
        var lease = pool.BeginBatch();
        pool.Allocate(lease, 1, 1);
        pool.Recycle(lease);

        Assert.Throws<InvalidOperationException>(() => pool.Recycle(lease));
        pool.Destroy();
    }

    private static VulkanStagingPagePool CreatePool(
        ulong pageSize,
        out List<(ulong Capacity, bool Dedicated, int Identity)> createdPages,
        out List<int> flushedPages,
        out List<int> destroyedPages)
    {
        var created = new List<(ulong, bool, int)>();
        var flushed = new List<int>();
        var destroyed = new List<int>();
        var pool = new VulkanStagingPagePool(
            pageSize,
            (capacity, dedicated, identity) =>
            {
                created.Add((capacity, dedicated, identity));
                return new VulkanStagingPage(
                    null!,
                    0,
                    capacity,
                    dedicated,
                    identity,
                    (_, _) => flushed.Add(identity),
                    () => destroyed.Add(identity));
            });
        createdPages = created;
        flushedPages = flushed;
        destroyedPages = destroyed;
        return pool;
    }
}
