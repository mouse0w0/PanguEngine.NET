using PanguEngine.Client.UI.Rendering;

namespace PanguEngine.Tests.Client.UI;

public sealed class UiImageAtlasAllocatorTests
{
    [Fact]
    public void ExactPageAllocationUsesWholePage()
    {
        var allocator = new UiImageAtlasAllocator(8, 8);

        Assert.True(allocator.TryAllocate(8, 8, out var region));
        Assert.Equal(new UiImageAtlasRegion(0, 0, 8, 8), region);
        Assert.False(allocator.TryAllocate(1, 1, out _));
    }

    [Fact]
    public void WiderRemainderKeepsRightRegionAtFullHeight()
    {
        var allocator = new UiImageAtlasAllocator(10, 6);

        Assert.True(allocator.TryAllocate(4, 4, out _));
        Assert.True(allocator.TryAllocate(6, 6, out var region));

        Assert.Equal(new UiImageAtlasRegion(4, 0, 6, 6), region);
    }

    [Fact]
    public void TallerRemainderKeepsBottomRegionAtFullWidth()
    {
        var allocator = new UiImageAtlasAllocator(6, 10);

        Assert.True(allocator.TryAllocate(4, 4, out _));
        Assert.True(allocator.TryAllocate(6, 6, out var region));

        Assert.Equal(new UiImageAtlasRegion(0, 4, 6, 6), region);
    }

    [Fact]
    public void AllocationUsesBestRemainingArea()
    {
        var allocator = new UiImageAtlasAllocator(10, 10);
        Assert.True(allocator.TryAllocate(4, 4, out var first));
        Assert.True(allocator.TryAllocate(6, 4, out var second));
        allocator.Free(first);

        Assert.True(allocator.TryAllocate(4, 4, out var reused));

        Assert.Equal(first, reused);
        Assert.NotEqual(second, reused);
    }

    [Fact]
    public void FreeMergesAdjacentRegionsBackIntoWholePage()
    {
        var allocator = new UiImageAtlasAllocator(8, 8);
        Assert.True(allocator.TryAllocate(4, 8, out var left));
        Assert.True(allocator.TryAllocate(4, 8, out var right));

        allocator.Free(right);
        allocator.Free(left);

        Assert.True(allocator.TryAllocate(8, 8, out var whole));
        Assert.Equal(new UiImageAtlasRegion(0, 0, 8, 8), whole);
    }

    [Fact]
    public void FragmentedSpaceDoesNotOverlapNewAllocation()
    {
        var allocator = new UiImageAtlasAllocator(8, 8);
        Assert.True(allocator.TryAllocate(4, 4, out var topLeft));
        Assert.True(allocator.TryAllocate(4, 4, out var topRight));
        Assert.True(allocator.TryAllocate(4, 4, out var bottomLeft));
        Assert.True(allocator.TryAllocate(4, 4, out var bottomRight));

        allocator.Free(topLeft);
        allocator.Free(bottomRight);

        Assert.False(allocator.TryAllocate(8, 4, out _));
        Assert.True(allocator.TryAllocate(4, 4, out var reused));
        Assert.Equal(topLeft, reused);
        Assert.NotEqual(topRight, reused);
        Assert.NotEqual(bottomLeft, reused);
    }
}
