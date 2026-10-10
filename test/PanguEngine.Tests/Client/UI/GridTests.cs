using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;

namespace PanguEngine.Tests.Client.UI;

public sealed class GridTests
{
    [Fact]
    public void MixedColumnsAllocateRemainingSpaceByWeight()
    {
        var grid = new Grid
        {
            ColumnDefinitions = new(100, GridLength.Auto, GridLength.Star, new GridLength(2, GridUnitType.Star)),
            ColumnSpacing = 10
        };
        var nodes = AddColumns(grid, 0, 30, 20, 40);

        Layout(grid, 400, 50);

        Assert.Equal(new Rect(0, 0, 100, 50), nodes[0].LayoutBounds);
        Assert.Equal(new Rect(110, 0, 30, 50), nodes[1].LayoutBounds);
        Assert.Equal(new Rect(150, 0, 80, 50), nodes[2].LayoutBounds);
        Assert.Equal(new Rect(240, 0, 160, 50), nodes[3].LayoutBounds);
        Assert.Equal(220, grid.DesiredSize.Width);
    }

    [Theory]
    [InlineData(300, 80, 220)]
    [InlineData(100, 60, 40)]
    [InlineData(50, 60, 0)]
    public void StarBoundsRedistributeSpace(double width, double firstWidth, double secondWidth)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new(new GridDefinition(GridLength.Star, 60, 80), GridLength.Star)
        };
        var nodes = AddColumns(grid, 0, 0);
        Layout(grid, width, 20);
        Assert.Equal(firstWidth, nodes[0].LayoutBounds.Width);
        Assert.Equal(secondWidth, nodes[1].LayoutBounds.Width);
    }

    [Theory]
    [InlineData(70, 60, 10)]
    [InlineData(100, 70, 30)]
    public void SimultaneousMinimumAndMaximumConstraintsResolveTogether(
        double width, double firstWidth, double secondWidth)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new(new GridDefinition(GridLength.Star, 60),
                new GridDefinition(GridLength.Star, maxLength: 30))
        };
        var nodes = AddColumns(grid, 0, 0);
        Layout(grid, width, 20);
        Assert.Equal(firstWidth, nodes[0].LayoutBounds.Width);
        Assert.Equal(secondWidth, nodes[1].LayoutBounds.Width);
    }

    [Fact]
    public void ZeroWeightsAndVeryLargeWeightsRemainFinite()
    {
        var grid = new Grid
        {
            ColumnDefinitions = new(new GridDefinition(new GridLength(0, GridUnitType.Star), 20),
                new GridLength(double.MaxValue, GridUnitType.Star),
                new GridLength(double.MaxValue / 2, GridUnitType.Star))
        };
        var nodes = AddColumns(grid, 0, 0, 0);
        Layout(grid, 320, 10);
        Assert.Equal(20, nodes[0].LayoutBounds.Width);
        Assert.Equal(200, nodes[1].LayoutBounds.Width);
        Assert.Equal(100, nodes[2].LayoutBounds.Width);
    }

    [Fact]
    public void MinimaOverflowAvailableSpaceAndMinimumWinsOverMaximum()
    {
        var grid = new Grid
        {
            ColumnDefinitions = new(new GridDefinition(GridLength.Star, 60, 20),
                new GridDefinition(GridLength.Star, 60)), ColumnSpacing = 5
        };
        var nodes = AddColumns(grid, 0, 0);
        Layout(grid, 100, 10);
        Assert.Equal(new Rect(0, 0, 60, 10), nodes[0].LayoutBounds);
        Assert.Equal(new Rect(65, 0, 60, 10), nodes[1].LayoutBounds);
        Assert.Equal(125, grid.DesiredSize.Width);
    }

    [Fact]
    public void AllMaximumsCanLeaveUnusedSpace()
    {
        var grid = new Grid
        {
            ColumnDefinitions = new(new GridDefinition(GridLength.Star, maxLength: 20),
                new GridDefinition(GridLength.Star, maxLength: 40))
        };
        var nodes = AddColumns(grid, 0, 0);
        Layout(grid, 200, 10);
        Assert.Equal(20, nodes[0].LayoutBounds.Width);
        Assert.Equal(new Rect(20, 0, 40, 10), nodes[1].LayoutBounds);
    }

    [Fact]
    public void InfiniteAxesUseFiniteContentRequirements()
    {
        var grid = new Grid
        {
            ColumnDefinitions = new(GridLength.Star, new GridLength(2, GridUnitType.Star)),
            RowDefinitions = new(GridLength.Star), ColumnSpacing = 5
        };
        var nodes = AddColumns(grid, 20, 40);
        grid.Measure(Size.Infinite);
        Assert.Equal(new Size(65, 10), grid.DesiredSize);
        grid.Arrange(new Rect(0, 0, 65, 10));
        Assert.Equal(20, nodes[0].LayoutBounds.Width);
        Assert.Equal(40, nodes[1].LayoutBounds.Width);
    }

    [Fact]
    public void SpanningRequirementPrefersStarOverAuto()
    {
        var grid = new Grid { ColumnDefinitions = new(GridLength.Auto, GridLength.Star), ColumnSpacing = 10 };
        var label = new ProbeNode(_ => new Size(30, 10));
        var spanning = new ProbeNode(_ => new Size(100, 10));
        grid.Children.Add(spanning);
        Grid.SetColumnSpan(spanning, 2);
        grid.Children.Add(label);
        grid.Measure(Size.Infinite);
        grid.Arrange(new Rect(0, 0, 100, 10));

        Assert.Equal(100, grid.DesiredSize.Width);
        Assert.Equal(30, label.LayoutBounds.Width);
        Assert.Equal(100, spanning.LayoutBounds.Width);
    }

    [Fact]
    public void AutoSpansRespectMaximumsAndKeepFixedTracks()
    {
        var grid = new Grid
        {
            ColumnDefinitions = new(20, new GridDefinition(GridLength.Auto, maxLength: 25), GridLength.Auto),
            ColumnSpacing = 5
        };
        var nodes = AddColumns(grid, 0, 0, 0);
        var spanning = new ProbeNode(_ => new Size(100, 10));
        Grid.SetColumnSpan(spanning, 3);
        grid.Children.Add(spanning);
        grid.Measure(Size.Infinite);
        grid.Arrange(new Rect(0, 0, 100, 10));
        Assert.Equal(20, nodes[0].LayoutBounds.Width);
        Assert.Equal(25, nodes[1].LayoutBounds.Width);
        Assert.Equal(45, nodes[2].LayoutBounds.Width);
        Assert.Equal(100, spanning.LayoutBounds.Width);
    }

    [Fact]
    public void OutOfRangePlacementAndSpansClampWithoutAddingTracks()
    {
        var grid = new Grid { ColumnDefinitions = new(30, 40), RowDefinitions = new(10, 20) };
        var child = new ProbeNode(_ => Size.Zero);
        Grid.SetRow(child, int.MaxValue);
        Grid.SetColumn(child, int.MaxValue);
        Grid.SetRowSpan(child, int.MaxValue);
        Grid.SetColumnSpan(child, int.MaxValue);
        grid.Children.Add(child);
        Layout(grid, 70, 30);
        Assert.Equal(new Rect(30, 10, 40, 20), child.LayoutBounds);
    }

    [Fact]
    public void RowSpansIncludeInternalSpacing()
    {
        var grid = new Grid { RowDefinitions = new(10, 20, 30), RowSpacing = 5 };
        var child = new ProbeNode(_ => Size.Zero);
        Grid.SetRow(child, 1);
        Grid.SetRowSpan(child, 2);
        grid.Children.Add(child);
        Layout(grid, 40, 70);
        Assert.Equal(new Rect(0, 15, 40, 55), child.LayoutBounds);
    }

    [Fact]
    public void AutoRowsUseResolvedStarWidthForWrapping()
    {
        var grid = new Grid { ColumnDefinitions = new(20, GridLength.Star), RowDefinitions = new(GridLength.Auto) };
        var child = new ProbeNode(size => new Size(Math.Min(120, size.Width), Math.Ceiling(120 / size.Width) * 10));
        Grid.SetColumn(child, 1);
        grid.Children.Add(child);
        Layout(grid, 80, 100);
        Assert.Equal(new Size(80, 20), grid.DesiredSize);
        Assert.Equal(new Rect(20, 0, 60, 20), child.LayoutBounds);
        Assert.Equal(new Size(60, 20), child.Constraint);

        Layout(grid, 60, 100);
        Assert.Equal(30, grid.DesiredSize.Height);
        Assert.Equal(new Rect(20, 0, 40, 30), child.LayoutBounds);
    }

    [Fact]
    public void HiddenParticipatesCollapsedDoesNotAndEmptyTracksRetainGaps()
    {
        var grid = new Grid { ColumnDefinitions = new(GridLength.Auto, GridLength.Auto), ColumnSpacing = 5 };
        var nodes = AddColumns(grid, 20, 100);
        nodes[0].Visibility = Visibility.Hidden;
        nodes[1].Visibility = Visibility.Collapsed;
        Layout(grid, 100, 20);
        Assert.Equal(new Size(25, 10), grid.DesiredSize);
        Assert.Equal(new Rect(0, 0, 20, 20), nodes[0].LayoutBounds);
        Assert.Equal(Rect.Zero, nodes[1].LayoutBounds);
        Assert.True(nodes[1].IsArrangeValid);

        grid.Children.Clear();
        grid.Measure(Size.Infinite);
        Assert.Equal(new Size(5, 0), grid.DesiredSize);
    }

    [Fact]
    public void BoxModelAndChildAlignmentApplyWithinCells()
    {
        var grid = new Grid { BorderThickness = new Thickness(2), Padding = new Thickness(3) };
        var child = new ProbeNode(_ => new Size(20, 10))
        {
            Margin = new Thickness(2), HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom
        };
        grid.Children.Add(child);
        Layout(grid, 100, 50);
        Assert.Equal(new Size(34, 24), grid.DesiredSize);
        Assert.Equal(new Rect(73, 33, 20, 10), child.LayoutBounds);
    }

    [Fact]
    public void DefinitionPlacementSpacingAndChildSizeChangesInvalidateMeasure()
    {
        var grid = new Grid { ColumnDefinitions = new(GridLength.Star, GridLength.Star) };
        var child = new Panel();
        grid.Children.Add(child);
        Layout(grid, 100, 20);
        grid.ColumnDefinitions = new(GridLength.Star, GridLength.Star);
        Assert.True(grid.IsMeasureValid);
        grid.ColumnDefinitions = new(20, GridLength.Star);
        Assert.False(grid.IsMeasureValid);
        Layout(grid, 100, 20);
        Grid.SetColumn(child, 1);
        Assert.False(grid.IsMeasureValid);
        Layout(grid, 100, 20);
        Grid.SetColumnSpan(child, 2);
        Assert.False(grid.IsMeasureValid);
        Layout(grid, 100, 20);
        grid.ColumnSpacing = 5;
        Assert.False(grid.IsMeasureValid);
        Layout(grid, 100, 20);
        child.Width = 30;
        Assert.False(grid.IsMeasureValid);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1.5)]
    [InlineData(2)]
    public void RoundedStarCellsMeetAtSharedBoundaries(double scale)
    {
        var grid = new Grid { ColumnDefinitions = new(GridLength.Star, GridLength.Star, GridLength.Star) };
        _ = new UiScreen(grid) { Scale = scale };
        var nodes = AddColumns(grid, 0, 0, 0);
        Layout(grid, 100, 10);
        Assert.Equal(nodes[0].LayoutBounds.X + nodes[0].LayoutBounds.Width, nodes[1].LayoutBounds.X, 8);
        Assert.Equal(nodes[1].LayoutBounds.X + nodes[1].LayoutBounds.Width, nodes[2].LayoutBounds.X, 8);
        Assert.Equal(100, nodes[2].LayoutBounds.X + nodes[2].LayoutBounds.Width, 8);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(102)]
    public void RoundedCellsPreserveSpacingAtHalfPixelBoundaries(double width)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new(GridLength.Star, GridLength.Star),
            RowDefinitions = new(GridLength.Star, GridLength.Star),
            ColumnSpacing = 1, RowSpacing = 1
        };
        var nodes = AddColumns(grid, 0, 0);
        Grid.SetRow(nodes[1], 1);
        Layout(grid, width, width);
        Assert.Equal(1, nodes[1].LayoutBounds.X - nodes[0].LayoutBounds.X - nodes[0].LayoutBounds.Width, 8);
        Assert.Equal(1, nodes[1].LayoutBounds.Y - nodes[0].LayoutBounds.Y - nodes[0].LayoutBounds.Height, 8);
        Assert.Equal(width, nodes[1].LayoutBounds.X + nodes[1].LayoutBounds.Width, 8);
    }

    [Fact]
    public void WrappingUsesRoundedCellWidthDuringMeasurement()
    {
        var grid = new Grid
        {
            ColumnDefinitions = new(GridLength.Star, GridLength.Star), RowDefinitions = new(GridLength.Auto)
        };
        var child = new ProbeNode(size =>
            new Size(Math.Min(100.5, size.Width), Math.Ceiling(100.5 / size.Width) * 10));
        grid.Children.Add(child);
        Layout(grid, 101, 100);
        Assert.Equal(50, child.LayoutBounds.Width);
        Assert.Equal(30, grid.DesiredSize.Height);
        Assert.Equal(30, child.LayoutBounds.Height);
        Assert.Equal(child.LayoutBounds.Width, child.Constraint.Width);
    }

    [Fact]
    public void SpanningCellsUseTheSameRoundedBoundaryAsAdjacentCells()
    {
        var grid = new Grid { ColumnDefinitions = new(0.1, 0.2, 0.3, 1) };
        _ = new UiScreen(grid) { Scale = 7.5 };
        var spanning = new ProbeNode(_ => Size.Zero);
        var adjacent = new ProbeNode(_ => Size.Zero);
        Grid.SetColumn(spanning, 1);
        Grid.SetColumnSpan(spanning, 2);
        Grid.SetColumn(adjacent, 3);
        grid.Children.Add(spanning);
        grid.Children.Add(adjacent);
        Layout(grid, 2, 2);
        Assert.Equal(adjacent.LayoutBounds.X, spanning.LayoutBounds.X + spanning.LayoutBounds.Width, 8);
    }

    [Fact]
    public void FractionalScaleDoesNotExpandAlreadyAlignedCellWidths()
    {
        var grid = new Grid { ColumnDefinitions = new(0.9, 50, 1) };
        _ = new UiScreen(grid) { Scale = 1.1 };
        var nodes = AddColumns(grid, 0, 0, 0);
        Layout(grid, 60, 10);
        Assert.Equal(nodes[1].LayoutBounds.X + nodes[1].LayoutBounds.Width, nodes[2].LayoutBounds.X, 8);
        Assert.Equal(55, nodes[1].LayoutBounds.Width * 1.1, 8);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExtremeWeightRatiosPreserveRepresentableSmallShares(bool spanningContent)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new(new GridLength(1e-100, GridUnitType.Star),
                new GridLength(1e300, GridUnitType.Star))
        };
        _ = new UiScreen(grid) { UseLayoutRounding = false };
        var nodes = AddColumns(grid, 0, 0);
        if (spanningContent)
        {
            var spanning = new ProbeNode(_ => new Size(1e300, 10));
            Grid.SetColumnSpan(spanning, 2);
            grid.Children.Add(spanning);
            grid.Measure(Size.Infinite);
            Assert.InRange(nodes[0].Constraint.Width / 1e-100, 0.999999999999, 1.000000000001);
            grid.Arrange(new Rect(0, 0, 1e300, 20));
        }
        else
        {
            Layout(grid, 1e300, 20);
        }
        Assert.InRange(nodes[0].LayoutBounds.Width / 1e-100, 0.999999999999, 1.000000000001);
    }

    [Fact]
    public void DisabledRoundingKeepsFractionalWidthsAndSpacing()
    {
        var grid = new Grid { ColumnDefinitions = new(GridLength.Star, GridLength.Star), ColumnSpacing = 0.25 };
        _ = new UiScreen(grid) { UseLayoutRounding = false };
        var nodes = AddColumns(grid, 0, 0);
        Layout(grid, 10, 10);
        Assert.Equal(4.875, nodes[0].LayoutBounds.Width);
        Assert.Equal(5.125, nodes[1].LayoutBounds.X);
        Assert.Equal(4.875, nodes[1].LayoutBounds.Width);
    }

    [Fact]
    public void ArrangeReallocatesStarTracksForFinalSize()
    {
        var grid = new Grid { ColumnDefinitions = new(20, GridLength.Star) };
        var nodes = AddColumns(grid, 0, 0);
        grid.Measure(new Size(100, 20));
        grid.Arrange(new Rect(0, 0, 200, 20));
        Assert.Equal(180, nodes[1].LayoutBounds.Width);
        Assert.Equal(new Size(80, 20), nodes[1].Constraint);
    }

    [Fact]
    public void MeasurementFailurePropagatesAndLeavesGridInvalid()
    {
        var expected = new InvalidOperationException("measure failed");
        var grid = new Grid();
        grid.Children.Add(new ProbeNode(_ => throw expected));
        Assert.Same(expected, Assert.Throws<InvalidOperationException>(() => grid.Measure(Size.Infinite)));
        Assert.False(grid.IsMeasureValid);
        Assert.False(grid.IsArrangeValid);
    }

    [Fact]
    public void NonRepresentableTrackTotalFailsMeasurement()
    {
        var grid = new Grid { ColumnDefinitions = new(double.MaxValue, double.MaxValue) };
        _ = new UiScreen(grid) { UseLayoutRounding = false };
        Assert.Throws<InvalidOperationException>(() => grid.Measure(Size.Infinite));
        Assert.False(grid.IsMeasureValid);
    }

    [Fact]
    public void InvalidAttachedValuesAndSpacingFailBeforeCommit()
    {
        var grid = new Grid();
        var child = new Panel();
        Assert.Throws<ArgumentException>(() => Grid.SetRow(child, -1));
        Assert.Throws<ArgumentException>(() => Grid.SetColumnSpan(child, 0));
        Assert.Throws<ArgumentException>(() => grid.RowSpacing = double.NaN);
        Assert.Throws<ArgumentException>(() => grid.ColumnSpacing = -1);
        Assert.Equal(0, Grid.GetRow(child));
        Assert.Equal(1, Grid.GetColumnSpan(child));
    }

    private static ProbeNode[] AddColumns(Grid grid, params double[] widths)
    {
        var nodes = widths.Select(width => new ProbeNode(_ => new Size(width, 10))).ToArray();
        for (var index = 0; index < nodes.Length; index++)
        {
            Grid.SetColumn(nodes[index], index);
            grid.Children.Add(nodes[index]);
        }
        return nodes;
    }

    private static void Layout(Grid grid, double width, double height)
    {
        grid.Measure(new Size(width, height));
        grid.Arrange(new Rect(0, 0, width, height));
    }

    private sealed class ProbeNode(Func<Size, Size> measure) : UiNode
    {
        public Size Constraint { get; private set; }

        protected override Size MeasureCore(Size size)
        {
            Constraint = size;
            return measure(size);
        }
    }
}
