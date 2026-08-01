using PanguEngine.Client.UI;

namespace PanguEngine.Tests.Client.UI;

public sealed class UiLayoutValueTests
{
    [Fact]
    public void SizeAcceptsFiniteAndPositiveInfinityConstraints()
    {
        var finite = new Size(12.5, 8.25);

        Assert.Equal(12.5, finite.Width);
        Assert.Equal(8.25, finite.Height);
        Assert.Equal(new Size(0, 0), Size.Zero);
        Assert.Equal(
            new Size(double.PositiveInfinity, double.PositiveInfinity),
            Size.Infinite);
    }

    [Theory]
    [InlineData(double.NaN, 0)]
    [InlineData(0, double.NaN)]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(double.NegativeInfinity, 0)]
    [InlineData(0, double.NegativeInfinity)]
    public void SizeRejectsNaNNegativeAndNegativeInfinity(double width, double height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Size(width, height));
    }

    [Fact]
    public void RectAcceptsNegativeOriginAndRejectsInvalidComponents()
    {
        var rect = new Rect(-4.5, -2.25, new Size(10, 20));

        Assert.Equal(-4.5, rect.X);
        Assert.Equal(-2.25, rect.Y);
        Assert.Equal(10, rect.Width);
        Assert.Equal(20, rect.Height);
        Assert.Equal(new Rect(0, 0, 0, 0), Rect.Zero);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Rect(double.NaN, 0, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Rect(0, double.PositiveInfinity, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Rect(0, 0, -1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Rect(0, 0, 1, double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Rect(0, 0, Size.Infinite));
    }

    [Fact]
    public void ThicknessConstructorsMapUniformHorizontalAndEdges()
    {
        Assert.Equal(new Thickness(3, 3, 3, 3), new Thickness(3));
        Assert.Equal(new Thickness(4, 6, 4, 6), new Thickness(4, 6));

        var edges = new Thickness(1, 2, 3, 4);
        Assert.Equal(1, edges.Left);
        Assert.Equal(2, edges.Top);
        Assert.Equal(3, edges.Right);
        Assert.Equal(4, edges.Bottom);
    }

    [Theory]
    [InlineData(-1, 0, 0, 0)]
    [InlineData(0, double.NaN, 0, 0)]
    [InlineData(0, 0, double.PositiveInfinity, 0)]
    [InlineData(0, 0, 0, double.NegativeInfinity)]
    public void ThicknessRejectsNegativeNaNAndInfinity(
        double left,
        double top,
        double right,
        double bottom)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Thickness(left, top, right, bottom));
    }

    [Fact]
    public void LayoutValuesUseValueEqualityAndZeroDefaults()
    {
        Assert.Equal(new Size(2, 3), new Size(2, 3));
        Assert.True(new Size(2, 3) == new Size(2, 3));
        Assert.True(new Size(2, 3) != new Size(3, 2));

        Assert.Equal(new Rect(1, 2, 3, 4), new Rect(1, 2, 3, 4));
        Assert.True(new Rect(1, 2, 3, 4) == new Rect(1, 2, 3, 4));
        Assert.True(new Rect(1, 2, 3, 4) != new Rect(4, 3, 2, 1));

        Assert.Equal(new Thickness(1, 2, 3, 4), new Thickness(1, 2, 3, 4));
        Assert.True(new Thickness(1, 2, 3, 4) == new Thickness(1, 2, 3, 4));
        Assert.True(new Thickness(1, 2, 3, 4) != new Thickness(4, 3, 2, 1));

        Assert.Equal(Size.Zero, default(Size));
        Assert.Equal(Rect.Zero, default(Rect));
        Assert.Equal(Thickness.Zero, default(Thickness));
    }

    [Fact]
    public void AlignmentEnumsExposeOnlyTheFourLayoutMembers()
    {
        Assert.Equal(
            new[]
            {
                HorizontalAlignment.Left,
                HorizontalAlignment.Center,
                HorizontalAlignment.Right,
                HorizontalAlignment.Stretch
            },
            Enum.GetValues<HorizontalAlignment>());
        Assert.Equal(
            new[]
            {
                VerticalAlignment.Top,
                VerticalAlignment.Center,
                VerticalAlignment.Bottom,
                VerticalAlignment.Stretch
            },
            Enum.GetValues<VerticalAlignment>());
    }
}
