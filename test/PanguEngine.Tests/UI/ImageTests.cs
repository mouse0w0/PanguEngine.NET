using PanguEngine.UI.Drawing;

namespace PanguEngine.Tests.UI;

public sealed class ImageTests
{
    [Fact]
    public void FromRgbaStoresDimensionsAndCopiesPixels()
    {
        var pixels = new byte[] { 1, 2, 3, 4 };
        var image = Image.FromRgba(pixels, 1, 1);

        pixels[0] = 9;

        Assert.Equal(1, image.PixelWidth);
        Assert.Equal(1, image.PixelHeight);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, image.Pixels.ToArray());
    }

    [Theory]
    [InlineData(0, 1, "pixelWidth")]
    [InlineData(-1, 1, "pixelWidth")]
    [InlineData(1, 0, "pixelHeight")]
    [InlineData(1, -1, "pixelHeight")]
    public void FromRgbaRejectsNonPositiveDimensions(int width, int height, string parameterName)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            Image.FromRgba(Array.Empty<byte>(), width, height));

        Assert.Equal(parameterName, exception.ParamName);
    }

    [Fact]
    public void FromRgbaRejectsMismatchedPixelLength()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            Image.FromRgba(new byte[3], 1, 1));

        Assert.Equal("rgbaPixels", exception.ParamName);
    }

    [Fact]
    public void FromRgbaRejectsPixelBuffersTooLargeForManagedStorage()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            Image.FromRgba(Array.Empty<byte>(), int.MaxValue, int.MaxValue));

        Assert.Equal("pixelWidth", exception.ParamName);
    }

    [Fact]
    public void FromStreamRejectsNullStream()
    {
        Assert.Throws<ArgumentNullException>(() => Image.FromStream(null!));
    }
}
