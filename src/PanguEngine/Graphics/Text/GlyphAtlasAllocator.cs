namespace PanguEngine.Graphics.Text;

internal readonly record struct GlyphAtlasRegion(
    uint X,
    uint Y,
    uint Width,
    uint Height);

internal sealed class GlyphAtlasAllocator
{
    private readonly uint _width;
    private readonly uint _height;
    private uint _x;
    private uint _y;
    private uint _shelfHeight;

    internal GlyphAtlasAllocator(uint width, uint height)
    {
        _width = width;
        _height = height;
    }

    internal bool TryAllocate(
        uint width,
        uint height,
        out GlyphAtlasRegion region)
    {
        var paddedWidth = checked(width + 2);
        var paddedHeight = checked(height + 2);
        if (paddedWidth > _width || paddedHeight > _height)
        {
            region = default;
            return false;
        }

        var x = _x;
        var y = _y;
        var shelfHeight = _shelfHeight;
        if (x + paddedWidth > _width)
        {
            x = 0;
            y = checked(y + shelfHeight);
            shelfHeight = 0;
        }

        if (y + paddedHeight > _height)
        {
            region = default;
            return false;
        }

        region = new GlyphAtlasRegion(x + 1, y + 1, width, height);
        _x = checked(x + paddedWidth);
        _y = y;
        _shelfHeight = Math.Max(shelfHeight, paddedHeight);
        return true;
    }
}
