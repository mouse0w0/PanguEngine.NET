using PanguEngine.Graphics.Text;

namespace PanguEngine.Tests.Graphics.Text;

public sealed class GlyphAtlasAllocatorTests
{
    [Fact]
    public void AllocationKeepsOnePixelPaddingAndNeverMovesPublishedRegions()
    {
        var allocator = new GlyphAtlasAllocator(16, 16);

        Assert.True(allocator.TryAllocate(4, 5, out var first));
        Assert.Equal(new GlyphAtlasRegion(1, 1, 4, 5), first);
        Assert.True(allocator.TryAllocate(3, 5, out _));

        Assert.Equal(new GlyphAtlasRegion(1, 1, 4, 5), first);
    }

    [Fact]
    public void AllocationStartsAnotherShelfWhenTheCurrentShelfIsFull()
    {
        var allocator = new GlyphAtlasAllocator(10, 12);

        Assert.True(allocator.TryAllocate(6, 3, out var first));
        Assert.True(allocator.TryAllocate(6, 3, out var second));

        Assert.Equal(new GlyphAtlasRegion(1, 1, 6, 3), first);
        Assert.Equal(new GlyphAtlasRegion(1, 6, 6, 3), second);
    }

    [Fact]
    public void AllocationFailsWithoutChangingStateWhenThePageIsFull()
    {
        var allocator = new GlyphAtlasAllocator(8, 8);

        Assert.True(allocator.TryAllocate(6, 6, out var first));
        Assert.False(allocator.TryAllocate(1, 1, out _));

        Assert.Equal(new GlyphAtlasRegion(1, 1, 6, 6), first);
        Assert.False(allocator.TryAllocate(1, 1, out _));
    }

    [Theory]
    [InlineData(7, 1)]
    [InlineData(1, 7)]
    public void AllocationRejectsBitmapWhosePaddingExceedsThePage(
        uint width,
        uint height)
    {
        var allocator = new GlyphAtlasAllocator(8, 8);

        Assert.False(allocator.TryAllocate(width, height, out _));
    }
}
