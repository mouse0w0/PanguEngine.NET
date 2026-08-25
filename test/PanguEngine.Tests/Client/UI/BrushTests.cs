using PanguEngine.Client.UI;

namespace PanguEngine.Tests.Client.UI;

public sealed class BrushTests
{
    [Fact]
    public void ImageSliceConstructorsAssignExpectedEdges()
    {
        Assert.Equal(new ImageSlice(3, 3, 3, 3), new ImageSlice(3));
        Assert.Equal(new ImageSlice(4, 5, 4, 5), new ImageSlice(4, 5));
        Assert.Equal(new ImageSlice(1, 2, 3, 4), new ImageSlice(1, 2, 3, 4));
        Assert.Equal(default, ImageSlice.Zero);
    }

    [Theory]
    [InlineData(-1, 0, 0, 0)]
    [InlineData(0, -1, 0, 0)]
    [InlineData(0, 0, -1, 0)]
    [InlineData(0, 0, 0, -1)]
    public void ImageSliceRejectsNegativeEdges(int left, int top, int right, int bottom) =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ImageSlice(left, top, right, bottom));

    [Fact]
    public void ImageBrushUsesExpectedDefaultsAndNormalizesFullSource()
    {
        var image = Image(20, 12);

        var brush = new ImageBrush(image);

        Assert.Same(image, brush.Source);
        Assert.Equal(new Rect(0, 0, 20, 12), brush.SourceRect);
        Assert.Equal(ImageStretch.Fill, brush.Stretch);
        Assert.Equal(ImageSamplingMode.Linear, brush.SamplingMode);
    }

    [Fact]
    public void ImageBrushStoresExplicitConfigurationAndUsesIdentityEquality()
    {
        var image = Image(20, 12);
        var sourceRect = new Rect(2, 3, 10, 6);
        var first = new ImageBrush(
            image,
            sourceRect,
            ImageStretch.UniformToFill,
            ImageSamplingMode.Nearest);
        var equal = new ImageBrush(
            image,
            sourceRect,
            ImageStretch.UniformToFill,
            ImageSamplingMode.Nearest);
        var otherImage = new ImageBrush(
            Image(20, 12),
            sourceRect,
            ImageStretch.UniformToFill,
            ImageSamplingMode.Nearest);

        Assert.Equal(first, equal);
        Assert.Equal(first.GetHashCode(), equal.GetHashCode());
        Assert.NotEqual(first, otherImage);
    }

    [Fact]
    public void ImageBrushRejectsNullAndOutOfRangeSource()
    {
        Assert.Throws<ArgumentNullException>(() => new ImageBrush(null!));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ImageBrush(Image(4, 4), new Rect(3, 0, 2, 1)));
    }

    [Fact]
    public void NineSliceBrushUsesExpectedDefaultsAndNormalizesFullSource()
    {
        var image = Image(20, 12);
        var slice = new ImageSlice(2, 3, 4, 2);

        var brush = new NineSliceImageBrush(image, slice);

        Assert.Same(image, brush.Source);
        Assert.Equal(new Rect(0, 0, 20, 12), brush.SourceRect);
        Assert.Equal(slice, brush.Slice);
        Assert.Equal(ImageSamplingMode.Linear, brush.SamplingMode);
    }

    [Fact]
    public void NineSliceBrushSupportsSubregionAndIdentityEquality()
    {
        var image = Image(30, 20);
        var sourceRect = new Rect(5, 4, 20, 12);
        var slice = new ImageSlice(3, 2, 5, 4);
        var first = new NineSliceImageBrush(
            image,
            sourceRect,
            slice,
            ImageSamplingMode.Nearest);
        var equal = new NineSliceImageBrush(
            image,
            sourceRect,
            slice,
            ImageSamplingMode.Nearest);

        Assert.Equal(first, equal);
        Assert.Equal(first.GetHashCode(), equal.GetHashCode());
        Assert.NotEqual(
            first,
            new NineSliceImageBrush(
                Image(30, 20),
                sourceRect,
                slice,
                ImageSamplingMode.Nearest));
    }

    [Theory]
    [InlineData(10, 0, 10, 0)]
    [InlineData(0, 6, 0, 6)]
    public void NineSliceBrushRequiresPositiveCenter(
        int left,
        int top,
        int right,
        int bottom)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new NineSliceImageBrush(
                Image(20, 12),
                new ImageSlice(left, top, right, bottom)));
    }

    [Fact]
    public void NineSliceBrushRejectsNullAndOutOfRangeSource()
    {
        Assert.Throws<ArgumentNullException>(
            () => new NineSliceImageBrush(null!, new ImageSlice(1)));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new NineSliceImageBrush(
                Image(4, 4),
                new Rect(3, 0, 2, 1),
                ImageSlice.Zero));
    }

    private static UiImage Image(int width, int height) =>
        UiImage.FromRgba(new byte[checked(width * height * 4)], width, height);
}
