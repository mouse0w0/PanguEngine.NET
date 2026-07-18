using PanguEngine.Graphics;

namespace PanguEngine.Tests.Graphics;

public sealed class TextureAtlasTests
{
    [Fact]
    public void BuildsEmptyAtlas()
    {
        var atlas = new MaxRectsTextureAtlasBuilder<string>(16, 16).Build();

        Assert.Equal(0, atlas.Width);
        Assert.Equal(0, atlas.Height);
        Assert.Empty(atlas.Pixels.ToArray());
    }

    [Fact]
    public void BuildsSingleImageWithRegionAndUv()
    {
        var pixels = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var builder = new MaxRectsTextureAtlasBuilder<string>(16, 16);
        builder.Add("stone", 2, 1, pixels);

        var atlas = builder.Build();

        Assert.Equal(2, atlas.Width);
        Assert.Equal(1, atlas.Height);
        Assert.Equal(
            new TextureAtlasRegion(0, 0, 2, 1, 0f, 0f, 1f, 1f),
            atlas.GetRegion("stone"));
        Assert.Equal(pixels, atlas.Pixels.ToArray());
    }

    [Fact]
    public void CopiesInputPixelsOnAdd()
    {
        var pixels = new byte[] { 1, 2, 3, 4 };
        var builder = new MaxRectsTextureAtlasBuilder<string>(4, 4);
        builder.Add("pixel", 1, 1, pixels);
        pixels[0] = 99;

        Assert.Equal(1, builder.Build().Pixels.Span[0]);
    }

    [Fact]
    public void BuildsDifferentSizesWithoutOverlappingRegions()
    {
        var builder = new MaxRectsTextureAtlasBuilder<string>(8, 8);
        var widePixels = SolidPixels(3, 1, 0xff, 0, 0, 0xff);
        var tallPixels = SolidPixels(1, 3, 0, 0xff, 0, 0xff);
        builder.Add("wide", 3, 1, widePixels);
        builder.Add("tall", 1, 3, tallPixels);

        var atlas = builder.Build();
        var wide = atlas.GetRegion("wide");
        var tall = atlas.GetRegion("tall");

        AssertRegionInsideAtlas(atlas, wide);
        AssertRegionInsideAtlas(atlas, tall);
        Assert.False(Overlaps(wide, tall));
        AssertRegionPixels(atlas, wide, widePixels);
        AssertRegionPixels(atlas, tall, tallPixels);
    }

    [Fact]
    public void GutterCopiesEdgesAndExcludesGutterFromRegion()
    {
        var pixels = new byte[]
        {
            1, 0, 0, 0xff, 2, 0, 0, 0xff,
            3, 0, 0, 0xff, 4, 0, 0, 0xff
        };
        var builder = new MaxRectsTextureAtlasBuilder<string>(8, 8, gutter: 1);
        builder.Add("image", 2, 2, pixels);

        var atlas = builder.Build();
        var region = atlas.GetRegion("image");

        Assert.Equal(4, atlas.Width);
        Assert.Equal(4, atlas.Height);
        Assert.Equal(new TextureAtlasRegion(1, 1, 2, 2, 0.25f, 0.25f, 0.75f, 0.75f), region);
        Assert.Equal((byte)1, GetPixel(atlas, 0, 0)[0]);
        Assert.Equal((byte)1, GetPixel(atlas, 1, 0)[0]);
        Assert.Equal((byte)2, GetPixel(atlas, 2, 0)[0]);
        Assert.Equal((byte)2, GetPixel(atlas, 3, 0)[0]);
        Assert.Equal((byte)1, GetPixel(atlas, 0, 1)[0]);
        Assert.Equal((byte)3, GetPixel(atlas, 0, 2)[0]);
        Assert.Equal((byte)3, GetPixel(atlas, 0, 3)[0]);
        Assert.Equal((byte)2, GetPixel(atlas, 3, 1)[0]);
        Assert.Equal((byte)4, GetPixel(atlas, 3, 2)[0]);
        Assert.Equal((byte)3, GetPixel(atlas, 1, 3)[0]);
        Assert.Equal((byte)4, GetPixel(atlas, 2, 3)[0]);
        Assert.Equal((byte)4, GetPixel(atlas, 3, 3)[0]);
        Assert.Equal((byte)1, GetPixel(atlas, 1, 1)[0]);
        Assert.Equal((byte)4, GetPixel(atlas, 2, 2)[0]);
        Assert.Equal(atlas.Width * atlas.Height * 4, atlas.Pixels.Length);
    }

    [Fact]
    public void GutterCellsStaySeparatedAndUseFinalUvDimensions()
    {
        var builder = new MaxRectsTextureAtlasBuilder<string>(8, 8, gutter: 1);
        builder.Add("red", 1, 1, SolidPixels(1, 1, 0xff, 0, 0, 0xff));
        builder.Add("blue", 1, 1, SolidPixels(1, 1, 0, 0, 0xff, 0xff));

        var atlas = builder.Build();
        var red = atlas.GetRegion("red");
        var blue = atlas.GetRegion("blue");

        Assert.Equal(6, atlas.Width);
        Assert.Equal(3, atlas.Height);
        Assert.False(Overlaps(red, blue));
        Assert.Equal(new TextureAtlasRegion(1, 1, 1, 1, 1f / 6f, 1f / 3f, 2f / 6f, 2f / 3f), red);
        Assert.Equal(new TextureAtlasRegion(4, 1, 1, 1, 4f / 6f, 1f / 3f, 5f / 6f, 2f / 3f), blue);
        AssertRegionPixels(atlas, red, SolidPixels(1, 1, 0xff, 0, 0, 0xff));
        AssertRegionPixels(atlas, blue, SolidPixels(1, 1, 0, 0, 0xff, 0xff));
        Assert.Equal((byte)0xff, GetPixel(atlas, 0, 0)[0]);
        Assert.Equal((byte)0xff, GetPixel(atlas, 2, 1)[0]);
        Assert.Equal((byte)0xff, GetPixel(atlas, 3, 1)[2]);
        Assert.Equal((byte)0xff, GetPixel(atlas, 1, 2)[0]);
        Assert.Equal((byte)0xff, GetPixel(atlas, 4, 2)[2]);
        Assert.Equal((byte)0xff, GetPixel(atlas, 3, 0)[2]);
    }

    [Fact]
    public void ProducesTheSameLayoutForTheSameAddOrder()
    {
        var first = CreateStandardBuilder();
        var second = CreateStandardBuilder();

        var firstAtlas = first.Build();
        var secondAtlas = second.Build();

        Assert.Equal(firstAtlas.Width, secondAtlas.Width);
        Assert.Equal(firstAtlas.Height, secondAtlas.Height);
        Assert.Equal(firstAtlas.Pixels.ToArray(), secondAtlas.Pixels.ToArray());
        Assert.Equal(firstAtlas.GetRegion("a"), secondAtlas.GetRegion("a"));
        Assert.Equal(firstAtlas.GetRegion("b"), secondAtlas.GetRegion("b"));
        Assert.Equal(firstAtlas.GetRegion("c"), secondAtlas.GetRegion("c"));
    }

    [Fact]
    public void GrowsCandidateWhenInitialApproximateSquareCannotFit()
    {
        var builder = new MaxRectsTextureAtlasBuilder<string>(8, 8);
        builder.Add("first", 3, 3, SolidPixels(3, 3, 0xff, 0, 0, 0xff));
        builder.Add("second", 3, 3, SolidPixels(3, 3, 0, 0, 0xff, 0xff));

        var atlas = builder.Build();

        Assert.Equal(6, atlas.Width);
        Assert.Equal(3, atlas.Height);
        var first = atlas.GetRegion("first");
        var second = atlas.GetRegion("second");
        Assert.Equal(new TextureAtlasRegion(0, 0, 3, 3, 0f, 0f, 0.5f, 1f), first);
        Assert.Equal(new TextureAtlasRegion(3, 0, 3, 3, 0.5f, 0f, 1f, 1f), second);
        Assert.False(Overlaps(first, second));
        AssertRegionPixels(atlas, first, SolidPixels(3, 3, 0xff, 0, 0, 0xff));
        AssertRegionPixels(atlas, second, SolidPixels(3, 3, 0, 0, 0xff, 0xff));
    }

    [Fact]
    public void StartsWithMinimumCandidateInsteadOfAlwaysUsingMaximumCapacity()
    {
        var builder = new MaxRectsTextureAtlasBuilder<string>(8, 8);
        builder.Add("first", 3, 1, SolidPixels(3, 1, 0xff, 0, 0, 0xff));
        builder.Add("second", 3, 1, SolidPixels(3, 1, 0, 0, 0xff, 0xff));

        var atlas = builder.Build();

        Assert.Equal(3, atlas.Width);
        Assert.Equal(2, atlas.Height);
        Assert.Equal(new TextureAtlasRegion(0, 0, 3, 1, 0f, 0f, 1f, 0.5f), atlas.GetRegion("first"));
        Assert.Equal(new TextureAtlasRegion(0, 1, 3, 1, 0f, 0.5f, 1f, 1f), atlas.GetRegion("second"));
    }

    [Fact]
    public void ExpandsHeightWhenWidthReachesItsMaximum()
    {
        var builder = new MaxRectsTextureAtlasBuilder<string>(8, 10);
        builder.Add("top", 5, 5, SolidPixels(5, 5, 0xff, 0, 0, 0xff));
        builder.Add("bottom", 5, 5, SolidPixels(5, 5, 0, 0, 0xff, 0xff));

        var atlas = builder.Build();

        Assert.Equal(5, atlas.Width);
        Assert.Equal(10, atlas.Height);
        Assert.Equal(
            new TextureAtlasRegion(0, 0, 5, 5, 0f, 0f, 1f, 0.5f),
            atlas.GetRegion("top"));
        Assert.Equal(
            new TextureAtlasRegion(0, 5, 5, 5, 0f, 0.5f, 1f, 1f),
            atlas.GetRegion("bottom"));
    }

    [Fact]
    public void ExpandsHeightWhenWidthIsAlreadyLonger()
    {
        var builder = new MaxRectsTextureAtlasBuilder<string>(12, 12);
        builder.Add("first", 5, 5, SolidPixels(5, 5, 0xff, 0, 0, 0xff));
        builder.Add("second", 5, 5, SolidPixels(5, 5, 0, 0xff, 0, 0xff));
        builder.Add("third", 5, 5, SolidPixels(5, 5, 0, 0, 0xff, 0xff));

        var atlas = builder.Build();

        Assert.Equal(10, atlas.Width);
        Assert.Equal(10, atlas.Height);
        Assert.Equal(0, atlas.GetRegion("first").X);
        Assert.Equal(5, atlas.GetRegion("second").X);
        Assert.Equal(0, atlas.GetRegion("second").Y);
        Assert.Equal(0, atlas.GetRegion("third").X);
        Assert.Equal(5, atlas.GetRegion("third").Y);
    }

    [Fact]
    public void BuildsImageThatExactlyFillsMaximumCapacity()
    {
        var builder = new MaxRectsTextureAtlasBuilder<string>(4, 4);
        builder.Add("full", 4, 4, SolidPixels(4, 4, 1, 2, 3, 4));

        var atlas = builder.Build();

        Assert.Equal(4, atlas.Width);
        Assert.Equal(4, atlas.Height);
        Assert.Equal(new TextureAtlasRegion(0, 0, 4, 4, 0f, 0f, 1f, 1f), atlas.GetRegion("full"));
    }

    [Theory]
    [InlineData(0, 4, 0, "maxWidth")]
    [InlineData(4, 0, 0, "maxHeight")]
    [InlineData(4, 4, -1, "gutter")]
    public void RejectsInvalidConstructorArguments(
        int maxWidth,
        int maxHeight,
        int gutter,
        string expectedParamName)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new MaxRectsTextureAtlasBuilder<string>(maxWidth, maxHeight, gutter));

        Assert.Equal(expectedParamName, exception.ParamName);
    }

    [Fact]
    public void RejectsNullKey()
    {
        var builder = new MaxRectsTextureAtlasBuilder<string>(4, 4);

        Assert.Throws<ArgumentNullException>(() =>
            builder.Add(null!, 1, 1, SolidPixels(1, 1, 1, 2, 3, 4)));
    }

    [Fact]
    public void RejectsDuplicateKey()
    {
        var builder = new MaxRectsTextureAtlasBuilder<string>(4, 4);
        builder.Add("same", 1, 1, SolidPixels(1, 1, 1, 2, 3, 4));

        var exception = Assert.Throws<ArgumentException>(() =>
            builder.Add("same", 1, 1, SolidPixels(1, 1, 5, 6, 7, 8)));

        Assert.Contains("same", exception.Message);
        Assert.Equal("key", exception.ParamName);
    }

    [Fact]
    public void RejectsInvalidImageDimensionsAndPixelLength()
    {
        var builder = new MaxRectsTextureAtlasBuilder<string>(4, 4);

        var widthException = Assert.Throws<ArgumentOutOfRangeException>(() =>
            builder.Add("zero", 0, 1, []));
        var heightException = Assert.Throws<ArgumentOutOfRangeException>(() =>
            builder.Add("negative", 1, -1, []));
        var exception = Assert.Throws<ArgumentException>(() =>
            builder.Add("length", 2, 2, new byte[3]));

        Assert.Equal("width", widthException.ParamName);
        Assert.Equal("height", heightException.ParamName);
        Assert.Equal("rgbaPixels", exception.ParamName);
    }

    [Theory]
    [InlineData("too-wide", 3, 1, "width")]
    [InlineData("too-tall", 1, 3, "height")]
    [InlineData("too-large", 3, 3, "width")]
    public void RejectsGutterCellExceedingCapacity(
        string key,
        int width,
        int height,
        string expectedParamName)
    {
        var builder = new MaxRectsTextureAtlasBuilder<string>(4, 4, gutter: 1);

        var exception = Assert.Throws<ArgumentException>(() =>
            builder.Add(key, width, height, SolidPixels(width, height, 1, 2, 3, 4)));

        Assert.Equal(expectedParamName, exception.ParamName);
        Assert.Contains(key, exception.Message);
    }

    [Fact]
    public void ReportsLayoutFailureWithCapacityAndKey()
    {
        var firstPixels = SolidPixels(3, 3, 1, 2, 3, 4);
        var secondPixels = SolidPixels(3, 3, 5, 6, 7, 8);
        var gutterBuilder = new MaxRectsTextureAtlasBuilder<string>(6, 6, gutter: 1);
        gutterBuilder.Add("first", 3, 3, firstPixels);
        gutterBuilder.Add("second", 3, 3, secondPixels);

        var exception = Assert.Throws<InvalidOperationException>(() => gutterBuilder.Build());

        Assert.Contains("second", exception.Message);
        Assert.Contains("6x6", exception.Message);
        Assert.Contains("5x5", exception.Message);
        Assert.Contains("3x3", exception.Message);
        Assert.Contains("gutter", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UsesShortSideFitAndAddOrderBeforeKeyOrder()
    {
        var builder = new MaxRectsTextureAtlasBuilder<string>(10, 10);
        builder.Add("z", 4, 6, SolidPixels(4, 6, 1, 2, 3, 4));
        builder.Add("a", 3, 2, SolidPixels(3, 2, 5, 6, 7, 8));
        builder.Add("filler", 10, 1, SolidPixels(10, 1, 9, 10, 11, 12));

        var atlas = builder.Build();

        Assert.Equal(10, atlas.Width);
        Assert.Equal(9, atlas.Height);
        Assert.Equal(
            new TextureAtlasRegion(0, 0, 4, 6, 0f, 0f, 0.4f, 6f / 9f),
            atlas.GetRegion("z"));
        Assert.Equal(
            new TextureAtlasRegion(0, 6, 3, 2, 0f, 6f / 9f, 0.3f, 8f / 9f),
            atlas.GetRegion("a"));
        Assert.Equal(
            new TextureAtlasRegion(0, 8, 10, 1, 0f, 8f / 9f, 1f, 1f),
            atlas.GetRegion("filler"));
        AssertRegionPixels(atlas, atlas.GetRegion("z"), SolidPixels(4, 6, 1, 2, 3, 4));
        AssertRegionPixels(atlas, atlas.GetRegion("a"), SolidPixels(3, 2, 5, 6, 7, 8));
    }

    [Fact]
    public void FailedBuildKeepsBuilderStateAndRepeatsTheSameFailure()
    {
        var builder = new MaxRectsTextureAtlasBuilder<string>(5, 5);
        builder.Add("first", 3, 3, SolidPixels(3, 3, 1, 2, 3, 4));
        builder.Add("second", 3, 3, SolidPixels(3, 3, 5, 6, 7, 8));

        var first = Assert.Throws<InvalidOperationException>(() => builder.Build());
        builder.Add("third", 1, 1, SolidPixels(1, 1, 9, 10, 11, 12));
        var second = Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.Equal(first.Message, second.Message);
    }

    [Fact]
    public void AcceptsCellThatExactlyMatchesCapacityWithGutter()
    {
        var builder = new MaxRectsTextureAtlasBuilder<string>(4, 4, gutter: 1);
        builder.Add("image", 2, 2, SolidPixels(2, 2, 1, 2, 3, 4));

        var atlas = builder.Build();

        Assert.Equal(4, atlas.Width);
        Assert.Equal(4, atlas.Height);
    }

    [Fact]
    public void UsesDefaultEqualityComparerForKeys()
    {
        var builder = new MaxRectsTextureAtlasBuilder<string>(4, 4);
        builder.Add(new string("same".ToCharArray()), 1, 1, SolidPixels(1, 1, 1, 2, 3, 4));

        Assert.Throws<ArgumentException>(() =>
            builder.Add(new string("same".ToCharArray()), 1, 1, SolidPixels(1, 1, 5, 6, 7, 8)));
    }

    [Fact]
    public void CopiesTheSinglePixelAcrossAllOnePixelGutter()
    {
        var builder = new MaxRectsTextureAtlasBuilder<string>(4, 4, gutter: 1);
        builder.Add("pixel", 1, 1, SolidPixels(1, 1, 9, 10, 11, 12));

        var atlas = builder.Build();
        Assert.Equal(3, atlas.Width);
        Assert.Equal(3, atlas.Height);
        var expected = SolidPixels(1, 1, 9, 10, 11, 12);
        for (var y = 0; y < atlas.Height; y++)
        {
            for (var x = 0; x < atlas.Width; x++)
                Assert.Equal(expected, GetPixel(atlas, x, y));
        }
    }

    [Fact]
    public void RejectsOperationsAfterSuccessfulBuild()
    {
        var builder = new MaxRectsTextureAtlasBuilder<string>(4, 4);
        builder.Add("image", 1, 1, SolidPixels(1, 1, 1, 2, 3, 4));
        builder.Build();

        Assert.Throws<InvalidOperationException>(() =>
            builder.Add("another", 1, 1, SolidPixels(1, 1, 5, 6, 7, 8)));
        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void MissingRegionThrowsKeyNotFoundException()
    {
        var atlas = new MaxRectsTextureAtlasBuilder<string>(4, 4).Build();

        var exception = Assert.Throws<KeyNotFoundException>(() => atlas.GetRegion("missing"));

        Assert.Contains("missing", exception.Message);
    }

    private static MaxRectsTextureAtlasBuilder<string> CreateStandardBuilder()
    {
        var builder = new MaxRectsTextureAtlasBuilder<string>(8, 8);
        builder.Add("a", 2, 2, SolidPixels(2, 2, 1, 2, 3, 4));
        builder.Add("b", 3, 1, SolidPixels(3, 1, 5, 6, 7, 8));
        builder.Add("c", 1, 3, SolidPixels(1, 3, 9, 10, 11, 12));
        return builder;
    }

    private static byte[] SolidPixels(int width, int height, byte r, byte g, byte b, byte a)
    {
        var pixels = new byte[width * height * 4];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = r;
            pixels[i + 1] = g;
            pixels[i + 2] = b;
            pixels[i + 3] = a;
        }

        return pixels;
    }

    private static byte[] GetPixel(TextureAtlas<string> atlas, int x, int y)
    {
        var offset = (y * atlas.Width + x) * 4;
        return atlas.Pixels.Span.Slice(offset, 4).ToArray();
    }

    private static void AssertRegionPixels(
        TextureAtlas<string> atlas,
        TextureAtlasRegion region,
        byte[] expected)
    {
        for (var y = 0; y < region.Height; y++)
        {
            for (var x = 0; x < region.Width; x++)
            {
                var expectedOffset = (y * region.Width + x) * 4;
                Assert.Equal(
                    expected.AsSpan(expectedOffset, 4).ToArray(),
                    GetPixel(atlas, region.X + x, region.Y + y));
            }
        }
    }

    private static void AssertRegionInsideAtlas(
        TextureAtlas<string> atlas,
        TextureAtlasRegion region)
    {
        Assert.InRange(region.X, 0, atlas.Width - region.Width);
        Assert.InRange(region.Y, 0, atlas.Height - region.Height);
    }

    private static bool Overlaps(TextureAtlasRegion first, TextureAtlasRegion second)
    {
        return first.X < second.X + second.Width
               && second.X < first.X + first.Width
               && first.Y < second.Y + second.Height
               && second.Y < first.Y + first.Height;
    }
}