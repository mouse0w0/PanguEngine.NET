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

    [Fact]
    public void NoneRoundsDestinationOriginAndSizeToThePhysicalGrid()
    {
        var view = CreateView(101, 99);
        view.Stretch = ImageStretch.None;
        var screen = new UiScreen(view) { Scale = 1.5 };
        view.Measure(new Size(100, 100));
        view.Arrange(new Rect(0, 0, 100, 100));

        var command = Assert.IsType<UiDrawImageCommand>(Assert.Single(screen.CreateDrawCommandList()));

        Assert.Equal(-1d / 1.5, command.Bounds.X, 12);
        Assert.Equal(1d / 1.5, command.Bounds.Y, 12);
        Assert.Equal(152d / 1.5, command.Bounds.Width, 12);
        Assert.Equal(149d / 1.5, command.Bounds.Height, 12);
        AssertAlignedToScaleGrid(command.Bounds, 1.5);
        Assert.Equal(new Rect(0, 0, 101, 99), command.SourceRect);
        Assert.Equal(new Rect(0, 0, 100, 100), command.Clip);
        Assert.Equal(ImageSamplingMode.Linear, command.SamplingMode);
    }

    [Fact]
    public void UniformRoundsCenteredDestinationOriginAndSizeToThePhysicalGrid()
    {
        var view = CreateView(200, 100);
        var screen = new UiScreen(view) { Scale = 1.5 };
        view.Measure(new Size(100, 100));
        view.Arrange(new Rect(0, 0, 100, 100));

        var command = Assert.IsType<UiDrawImageCommand>(Assert.Single(screen.CreateDrawCommandList()));

        Assert.Equal(0, command.Bounds.X);
        Assert.Equal(38d / 1.5, command.Bounds.Y, 12);
        Assert.Equal(100, command.Bounds.Width, 12);
        Assert.Equal(50, command.Bounds.Height, 12);
        AssertAlignedToScaleGrid(command.Bounds, 1.5);
        Assert.Equal(new Rect(0, 0, 200, 100), command.SourceRect);
        Assert.Equal(new Rect(0, 0, 100, 100), command.Clip);
        Assert.Equal(ImageSamplingMode.Linear, command.SamplingMode);
    }

    [Fact]
    public void UniformToFillRoundsCenteredDestinationOriginAndSizeToThePhysicalGrid()
    {
        var view = CreateView(101, 100);
        view.Stretch = ImageStretch.UniformToFill;
        var screen = new UiScreen(view) { Scale = 1.5 };
        view.Measure(new Size(100, 100));
        view.Arrange(new Rect(0, 0, 100, 100));

        var command = Assert.IsType<UiDrawImageCommand>(Assert.Single(screen.CreateDrawCommandList()));

        Assert.Equal(-1d / 1.5, command.Bounds.X, 12);
        Assert.Equal(0, command.Bounds.Y);
        Assert.Equal(152d / 1.5, command.Bounds.Width, 12);
        Assert.Equal(100, command.Bounds.Height, 12);
        AssertAlignedToScaleGrid(command.Bounds, 1.5);
        Assert.Equal(new Rect(0, 0, 101, 100), command.SourceRect);
        Assert.Equal(new Rect(0, 0, 100, 100), command.Clip);
        Assert.Equal(ImageSamplingMode.Linear, command.SamplingMode);
    }

    [Fact]
    public void FillKeepsTheFullArrangedViewBounds()
    {
        var view = CreateView(200, 100);
        view.Stretch = ImageStretch.Fill;
        var screen = new UiScreen(view) { Scale = 1.5 };
        view.Measure(new Size(100, 100));
        view.Arrange(new Rect(0, 0, 100, 100));

        var command = Assert.IsType<UiDrawImageCommand>(Assert.Single(screen.CreateDrawCommandList()));

        Assert.Equal(new Rect(0, 0, 100, 100), command.Bounds);
        AssertAlignedToScaleGrid(command.Bounds, 1.5);
        Assert.Equal(new Rect(0, 0, 200, 100), command.SourceRect);
        Assert.Equal(new Rect(0, 0, 100, 100), command.Clip);
    }

    [Fact]
    public void DisabledRoundingKeepsFractionalDestinationBounds()
    {
        var view = CreateView(101, 99);
        view.Stretch = ImageStretch.None;
        var screen = new UiScreen(view) { Scale = 1.5, UseLayoutRounding = false };
        view.Measure(new Size(100, 100));
        view.Arrange(new Rect(0, 0, 100, 100));

        var command = Assert.IsType<UiDrawImageCommand>(Assert.Single(screen.CreateDrawCommandList()));

        Assert.Equal(new Rect(-0.5, 0.5, 101, 99), command.Bounds);
        Assert.Equal(new Rect(0, 0, 101, 99), command.SourceRect);
        Assert.Equal(new Rect(0, 0, 100, 100), command.Clip);

        screen.UseLayoutRounding = true;
        view.Measure(new Size(100, 100));
        view.Arrange(new Rect(0, 0, 100, 100));

        command = Assert.IsType<UiDrawImageCommand>(Assert.Single(screen.CreateDrawCommandList()));
        Assert.Equal(-1d / 1.5, command.Bounds.X, 12);
        Assert.Equal(1d / 1.5, command.Bounds.Y, 12);
    }

    private static void AssertAlignedToScaleGrid(Rect bounds, double scale)
    {
        AssertAlignedToScaleGrid(bounds.X, scale);
        AssertAlignedToScaleGrid(bounds.Y, scale);
        AssertAlignedToScaleGrid(bounds.X + bounds.Width, scale);
        AssertAlignedToScaleGrid(bounds.Y + bounds.Height, scale);
    }

    private static void AssertAlignedToScaleGrid(double value, double scale)
    {
        var physical = value * scale;
        Assert.Equal(Math.Round(physical), physical, 9);
    }

    private static ImageView CreateView(int width, int height) =>
        new()
        {
            Source = UiImage.FromRgba(new byte[checked(width * height * 4)], width, height)
        };
}
