using PanguEngine.Client.UI;

namespace PanguEngine.Tests.Client.UI;

public sealed class ImageViewTests
{
    [Fact]
    public void ImagePropertiesUseExpectedMetadataAndDefaults()
    {
        var view = new ImageView();

        Assert.Equal(typeof(ImageView), ImageView.SourceProperty.OwnerType);
        Assert.Equal(typeof(ImageView), ImageView.SourceProperty.TargetType);
        Assert.Equal(UiPropertyInvalidation.Measure | UiPropertyInvalidation.Render,
            ImageView.SourceProperty.Invalidation);
        Assert.Null(view.Source);
        Assert.Equal(ImageStretch.Uniform, view.Stretch);
        Assert.Null(view.SourceRect);
        Assert.Equal(ImageSamplingMode.Linear, view.SamplingMode);
    }

    [Fact]
    public void NoneUsesTheSourceSize()
    {
        var view = CreateView(200, 100);
        view.Stretch = ImageStretch.None;

        view.Measure(new Size(80, 80));

        Assert.Equal(new Size(200, 100), view.DesiredSize);
    }

    [Fact]
    public void FillUsesBothFiniteAvailableDimensions()
    {
        var view = CreateView(200, 100);
        view.Stretch = ImageStretch.Fill;

        view.Measure(new Size(80, 80));

        Assert.Equal(new Size(80, 80), view.DesiredSize);
    }

    [Fact]
    public void UniformPreservesAspectRatioDuringMeasurement()
    {
        var view = CreateView(200, 100);
        view.Stretch = ImageStretch.Uniform;

        view.Measure(new Size(80, 80));

        Assert.Equal(new Size(80, 40), view.DesiredSize);
    }

    [Fact]
    public void UniformToFillCanExceedOneFiniteConstraint()
    {
        var view = CreateView(200, 100);
        view.Stretch = ImageStretch.UniformToFill;

        view.Measure(new Size(80, 80));

        Assert.Equal(new Size(160, 80), view.DesiredSize);
    }

    [Fact]
    public void OneAxisInfiniteUniformUsesTheFiniteAxis()
    {
        var view = CreateView(200, 100);
        view.Stretch = ImageStretch.Uniform;

        view.Measure(new Size(80, double.PositiveInfinity));

        Assert.Equal(new Size(80, 40), view.DesiredSize);
    }

    [Fact]
    public void SourceRectChangesIntrinsicSize()
    {
        var view = CreateView(200, 100);
        view.SourceRect = new Rect(25, 10, 80, 40);

        view.Measure(Size.Infinite);

        Assert.Equal(new Size(80, 40), view.DesiredSize);
    }

    [Fact]
    public void OutOfRangeSourceRectFailsDuringMeasurement()
    {
        var view = CreateView(2, 2);
        view.SourceRect = new Rect(1, 1, 2, 1);

        Assert.Throws<InvalidOperationException>(() => view.Measure(Size.Infinite));
    }

    [Fact]
    public void UniformDrawsCenteredAndClipsToTheImageViewBounds()
    {
        var view = CreateView(200, 100);
        var screen = new UiScreen(view);
        view.Measure(new Size(100, 100));
        view.Arrange(new Rect(0, 0, 100, 100));

        var command = Assert.IsType<UiDrawImageCommand>(Assert.Single(screen.CreateDrawCommandList()));

        Assert.Equal(new Rect(0, 25, 100, 50), command.Bounds);
        Assert.Equal(new Rect(0, 0, 200, 100), command.SourceRect);
        Assert.Equal(new Rect(0, 0, 100, 100), command.Clip);
        Assert.Equal(ImageSamplingMode.Linear, command.SamplingMode);
    }

    [Fact]
    public void UniformToFillDrawsCenteredOverflowForClipping()
    {
        var view = CreateView(200, 100);
        view.Stretch = ImageStretch.UniformToFill;
        var screen = new UiScreen(view);
        view.Measure(new Size(100, 100));
        view.Arrange(new Rect(0, 0, 100, 100));

        var command = Assert.IsType<UiDrawImageCommand>(Assert.Single(screen.CreateDrawCommandList()));

        Assert.Equal(new Rect(-50, 0, 200, 100), command.Bounds);
        Assert.Equal(new Rect(0, 0, 100, 100), command.Clip);
    }

    private static ImageView CreateView(int width, int height) =>
        new()
        {
            Source = UiImage.FromRgba(new byte[checked(width * height * 4)], width, height)
        };
}
