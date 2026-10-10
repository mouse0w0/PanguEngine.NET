using PanguEngine.Client.UI;

namespace PanguEngine.Tests.Client.UI;

public sealed class UiLayoutHelperTests
{
    [Theory]
    [InlineData(1, 0.5, 0)]
    [InlineData(1, 1.5, 2)]
    [InlineData(1, 2.5, 2)]
    [InlineData(1, -0.5, 0)]
    [InlineData(1.25, 0.4, 0)]
    [InlineData(1.25, 1.2, 1.6)]
    [InlineData(1.5, 1, 2d / 1.5)]
    [InlineData(1.5, -1, -2d / 1.5)]
    public void RoundLayoutValueUsesToEvenPhysicalPixels(
        double scale,
        double value,
        double expected)
    {
        Assert.Equal(expected, UiLayoutHelper.RoundLayoutValue(value, scale), 12);
    }

    [Fact]
    public void RoundLayoutValueUpRemovesFloatingPointTailBeforeCeiling()
    {
        Assert.Equal(
            80,
            UiLayoutHelper.RoundLayoutValueUp(80.00000000000001, 1),
            12);
        Assert.Equal(
            119d / 1.5,
            UiLayoutHelper.RoundLayoutValueUp(79.333333333333343, 1.5),
            12);
    }

    [Theory]
    [InlineData(1.1, 55)]
    [InlineData(2.2, 110)]
    public void RoundLayoutValueUpRemovesTailIntroducedByScaleMultiplication(
        double scale,
        double physicalWidth)
    {
        var rounded = UiLayoutHelper.RoundLayoutValueUp(50, scale);
        Assert.Equal(physicalWidth / scale, rounded);
        Assert.Equal(rounded, UiLayoutHelper.RoundLayoutValueUp(rounded, scale));
    }

    [Fact]
    public void RoundLayoutValueUpPreservesFractionIntroducedByScaleMultiplication()
    {
        Assert.Equal(4, UiLayoutHelper.RoundLayoutValueUp(2.000000015, 0.5));
    }

    [Theory]
    [InlineData(55)]
    [InlineData(524287)]
    public void RoundLayoutValueUpRemovesSingleFloatingPointStep(double physicalWidth)
    {
        var scale = Math.BitIncrement(physicalWidth);
        Assert.Equal(physicalWidth / scale, UiLayoutHelper.RoundLayoutValueUp(1, scale));
    }

    [Fact]
    public void RoundLayoutValueUpPreservesSingleStepAbovePhysicalTolerance()
    {
        var scale = Math.BitIncrement(524288d);
        Assert.Equal(524289d / scale, UiLayoutHelper.RoundLayoutValueUp(1, scale));
    }

    [Fact]
    public void RoundLayoutValueUpPreservesMultipleFloatingPointSteps()
    {
        var scale = Math.BitIncrement(Math.BitIncrement(55d));
        Assert.Equal(56d / scale, UiLayoutHelper.RoundLayoutValueUp(1, scale));
    }

    [Theory]
    [InlineData(1125899906842624.25, 1125899906842625)]
    [InlineData(2251799813685248.5, 2251799813685249)]
    public void RoundLayoutValueUpPreservesLargeFractionalPhysicalSizes(
        double scale,
        double physicalWidth)
    {
        Assert.Equal(physicalWidth / scale, UiLayoutHelper.RoundLayoutValueUp(1, scale));
    }

    [Fact]
    public void RoundLayoutValueUpPreservesExtremelySmallPositiveSize()
    {
        Assert.Equal(0, UiLayoutHelper.RoundLayoutValueUp(0, 1));
        Assert.Equal(1, UiLayoutHelper.RoundLayoutValueUp(1e-9, 1));
        Assert.Equal(1d / 1.5, UiLayoutHelper.RoundLayoutValueUp(1e-9, 1.5));
    }

    [Fact]
    public void CompositeHelpersRoundTheirComponents()
    {
        Assert.Equal(
            new Point(0, 1.6),
            UiLayoutHelper.RoundLayoutPoint(new Point(0.4, 1.2), 1.25));
        Assert.Equal(
            new Size(16d / 1.5, 1d / 1.5),
            UiLayoutHelper.RoundLayoutSizeUp(new Size(10.1, 0.1), 1.5));
        Assert.Equal(
            new Thickness(0, 1.6, 1.6, 3.2),
            UiLayoutHelper.RoundLayoutThickness(new Thickness(0.4, 1.2, 2, 2.8), 1.25));
    }

    [Fact]
    public void RoundLayoutRectRoundsOriginAndSizeInsteadOfFarEdges()
    {
        Assert.Equal(
            new Rect(0.5, -0.5, 1.5, 0.5),
            UiLayoutHelper.RoundLayoutRect(new Rect(0.3, -0.3, 1.4, 0.1), 2));
    }

    [Theory]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(double.NaN)]
    [InlineData(double.MaxValue)]
    public void NonFinitePhysicalCalculationFails(double value)
    {
        Assert.Throws<InvalidOperationException>(() =>
            UiLayoutHelper.RoundLayoutValue(value, 2));
        Assert.Throws<InvalidOperationException>(() =>
            UiLayoutHelper.RoundLayoutValueUp(Math.Abs(value), 2));
    }
}
