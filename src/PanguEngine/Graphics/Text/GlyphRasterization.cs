namespace PanguEngine.Graphics.Text;

internal enum GlyphRasterizationMode
{
    Grayscale
}

internal readonly record struct GlyphRasterKey(
    FontFace FontFace,
    uint PixelSize,
    uint GlyphId,
    GlyphRasterizationMode Mode);

internal sealed class GlyphBitmap
{
    internal GlyphBitmap(byte[] pixels, int width, int height, int left, int top)
    {
        Pixels = pixels;
        Width = width;
        Height = height;
        Left = left;
        Top = top;
    }

    internal ReadOnlyMemory<byte> Pixels { get; }
    internal int Width { get; }
    internal int Height { get; }
    internal int Left { get; }
    internal int Top { get; }
}

internal static class GlyphRasterization
{
    internal static uint GetPixelSize(double fontSize, double scale)
    {
        if (!double.IsFinite(fontSize) || fontSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(fontSize));
        if (!double.IsFinite(scale) || scale <= 0)
            throw new ArgumentOutOfRangeException(nameof(scale));

        var physicalSize = fontSize * scale;
        var rounded = Math.Round(physicalSize, MidpointRounding.AwayFromZero);
        if (!double.IsFinite(rounded) || rounded > uint.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(fontSize));

        return Math.Max(1u, checked((uint)rounded));
    }
}
