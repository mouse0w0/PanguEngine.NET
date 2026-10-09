using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Styling;

namespace PanguEngine.Tests.Client.UI;

public sealed class UniformGridTests
{
    [Theory]
    [InlineData(0, 0, 1, 1, 1)]
    [InlineData(0, 0, 5, 3, 3)]
    [InlineData(0, 2, 5, 3, 2)]
    [InlineData(2, 0, 5, 2, 3)]
    [InlineData(2, 2, 5, 2, 2)]
    public void ResolvesAutomaticDimensionsAndArrangesInRowOrder(
        int rows, int columns, int count, int resolvedRows, int resolvedColumns)
    {
        var grid = new UniformGrid { Rows = rows, Columns = columns };
        _ = new UiScreen(grid) { UseLayoutRounding = false };
        var children = Enumerable.Range(0, count)
            .Select(_ => new TestNode { CoreDesiredSize = new Size(10, 5) }).ToArray();
        foreach (var child in children)
            grid.Children.Add(child);

        grid.Measure(new Size(120, 90));

        var cellWidth = 120d / resolvedColumns;
        var cellHeight = 90d / resolvedRows;
        Assert.All(children, child =>
            Assert.Equal(new Size(cellWidth, cellHeight), child.LastMeasureConstraint));
        Assert.Equal(new Size(10 * resolvedColumns, 5 * resolvedRows), grid.DesiredSize);

        grid.Arrange(new Rect(0, 0, 120, 90));

        for (var index = 0; index < count; index++)
        {
            var bounds = children[index].LayoutBounds;
            Assert.Equal(index % resolvedColumns * cellWidth, bounds.X, 10);
            Assert.Equal(index / resolvedColumns * cellHeight, bounds.Y, 10);
            Assert.Equal(cellWidth, bounds.Width, 10);
            Assert.Equal(cellHeight, bounds.Height, 10);
        }
    }

    [Fact]
    public void SpacingAndDecorationUseTheLargestParticipantIncludingMargin()
    {
        var grid = new UniformGrid
        {
            Rows = 2, Columns = 2, RowSpacing = 6, ColumnSpacing = 4,
            Padding = new Thickness(10), BorderThickness = new Thickness(2)
        };
        var first = new TestNode { CoreDesiredSize = new Size(10, 5), Margin = new Thickness(2) };
        var collapsed = new TestNode { Visibility = Visibility.Collapsed, CoreDesiredSize = new Size(100, 100) };
        var hidden = new TestNode { Visibility = Visibility.Hidden, CoreDesiredSize = new Size(20, 10) };
        var last = new TestNode();
        grid.Children.Add(first);
        grid.Children.Add(collapsed);
        grid.Children.Add(hidden);
        grid.Children.Add(last);

        grid.Measure(new Size(124, 104));

        Assert.Equal(new Size(44, 33), first.LastMeasureConstraint);
        Assert.Equal(new Size(48, 37), hidden.LastMeasureConstraint);
        Assert.Equal(new Size(68, 50), grid.DesiredSize);
        Assert.Equal(Size.Zero, collapsed.DesiredSize);
        Assert.Equal(0, collapsed.MeasureCount);

        grid.Arrange(new Rect(0, 0, 124, 104));

        Assert.Equal(new Rect(14, 14, 44, 33), first.LayoutBounds);
        Assert.Equal(new Rect(64, 12, 48, 37), hidden.LayoutBounds);
        Assert.Equal(new Rect(12, 55, 48, 37), last.LayoutBounds);
        Assert.Equal(Rect.Zero, collapsed.LayoutBounds);
        Assert.True(collapsed.IsMeasureValid);
        Assert.True(collapsed.IsArrangeValid);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(2, 3)]
    [InlineData(0, 3)]
    [InlineData(2, 0)]
    public void EmptyAndAllCollapsedContentDoNotReserveSpacing(int rows, int columns)
    {
        var grid = new UniformGrid { Rows = rows, Columns = columns, RowSpacing = 7, ColumnSpacing = 9 };
        grid.Measure(new Size(100, 100));
        Assert.Equal(Size.Zero, grid.DesiredSize);

        var collapsed = new TestNode { Visibility = Visibility.Collapsed };
        grid.Children.Add(collapsed);
        grid.Measure(new Size(100, 100));
        grid.Arrange(new Rect(0, 0, 100, 100));

        Assert.Equal(Size.Zero, grid.DesiredSize);
        Assert.True(collapsed.IsMeasureValid);
        Assert.True(collapsed.IsArrangeValid);
    }

    [Fact]
    public void CollapsedNodesDoNotAffectAutomaticDimensions()
    {
        var grid = new UniformGrid();
        var visible = new TestNode { CoreDesiredSize = new Size(10, 5) };
        grid.Children.Add(new TestNode { Visibility = Visibility.Collapsed });
        grid.Children.Add(visible);
        grid.Children.Add(new TestNode { Visibility = Visibility.Collapsed });

        grid.Measure(new Size(100, 80));
        grid.Arrange(new Rect(0, 0, 100, 80));

        Assert.Equal(new Size(10, 5), grid.DesiredSize);
        Assert.Equal(new Rect(0, 0, 100, 80), visible.LayoutBounds);
    }

    [Fact]
    public void InfiniteConstraintsRemainInfiniteWhileDesiredSizeIsFinite()
    {
        var grid = new UniformGrid { Columns = 2, RowSpacing = 2, ColumnSpacing = 3 };
        for (var index = 0; index < 3; index++)
            grid.Children.Add(new TestNode { CoreDesiredSize = new Size(10, 5) });

        grid.Measure(Size.Infinite);

        Assert.All(grid.Children, child => Assert.Equal(Size.Infinite, ((TestNode)child).LastMeasureConstraint));
        Assert.Equal(new Size(23, 12), grid.DesiredSize);
    }

    [Fact]
    public void ExcessiveSpacingProducesZeroSizedCellsAndPreservesGaps()
    {
        var grid = new UniformGrid { Rows = 2, Columns = 2, RowSpacing = 5, ColumnSpacing = 7 };
        for (var index = 0; index < 4; index++)
            grid.Children.Add(new TestNode());

        grid.Measure(new Size(3, 4));
        grid.Arrange(new Rect(0, 0, 3, 4));

        Assert.All(grid.Children, child => Assert.Equal(Size.Zero, ((TestNode)child).LastMeasureConstraint));
        Assert.Equal(new Size(7, 5), grid.DesiredSize);
        Assert.Equal(new Rect(7, 5, 0, 0), grid.Children[3].LayoutBounds);
    }

    [Fact]
    public void ExplicitSizeAndAlignmentPositionChildrenWithinTheirCells()
    {
        var grid = new UniformGrid { Columns = 2 };
        var first = new TestNode
        {
            Width = 20, Height = 10, Margin = new Thickness(2),
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom
        };
        grid.Children.Add(first);
        grid.Children.Add(new TestNode { MinWidth = 30, MaxWidth = 20 });

        grid.Measure(new Size(100, 80));
        grid.Arrange(new Rect(0, 0, 100, 80));

        Assert.Equal(new Size(60, 14), grid.DesiredSize);
        Assert.Equal(new Rect(28, 68, 20, 10), first.LayoutBounds);
        Assert.Equal(new Rect(60, 0, 30, 80), grid.Children[1].LayoutBounds);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BoundariesDistributeCellsWithoutGapsOrOverflow(bool rounding)
    {
        var grid = new UniformGrid { Columns = 3 };
        _ = new UiScreen(grid) { Scale = 1.25, UseLayoutRounding = rounding };
        for (var index = 0; index < 3; index++)
            grid.Children.Add(new TestNode());

        grid.Measure(new Size(80, 16));
        grid.Arrange(new Rect(0, 0, 80, 16));

        Assert.Equal(rounding ? 26.4 : 80d / 3, grid.Children[0].LayoutBounds.Width, 10);
        Assert.Equal(rounding ? 27.2 : 80d / 3, grid.Children[1].LayoutBounds.Width, 10);
        Assert.Equal(rounding ? 26.4 : 80d / 3, grid.Children[2].LayoutBounds.Width, 10);
        for (var index = 1; index < 3; index++)
        {
            var previous = grid.Children[index - 1].LayoutBounds;
            Assert.Equal(previous.X + previous.Width, grid.Children[index].LayoutBounds.X, 10);
        }
        var last = grid.Children[2].LayoutBounds;
        Assert.Equal(80, last.X + last.Width, 10);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(102)]
    public void HalfPixelBoundariesPreserveOnePixelSpacingOnBothAxes(double extent)
    {
        var grid = new UniformGrid { Rows = 2, Columns = 2, RowSpacing = 1, ColumnSpacing = 1 };
        for (var index = 0; index < 4; index++)
            grid.Children.Add(new TestNode());

        grid.Measure(new Size(extent, extent));
        grid.Arrange(new Rect(0, 0, extent, extent));

        var first = grid.Children[0].LayoutBounds;
        var next = grid.Children[1].LayoutBounds;
        var below = grid.Children[2].LayoutBounds;
        var last = grid.Children[3].LayoutBounds;
        Assert.Equal(1, next.X - first.X - first.Width, 10);
        Assert.Equal(1, below.Y - first.Y - first.Height, 10);
        Assert.Equal(extent, last.X + last.Width, 10);
        Assert.Equal(extent, last.Y + last.Height, 10);
    }

    [Theory]
    [InlineData(true, 0.8)]
    [InlineData(false, 0.6)]
    public void FractionalSpacingFollowsLayoutRoundingOnBothAxes(bool rounding, double expectedGap)
    {
        var grid = new UniformGrid { Rows = 2, Columns = 3, RowSpacing = 0.6, ColumnSpacing = 0.6 };
        _ = new UiScreen(grid) { Scale = 1.25, UseLayoutRounding = rounding };
        for (var index = 0; index < 6; index++)
            grid.Children.Add(new TestNode { CoreDesiredSize = new Size(8, 4) });

        grid.Measure(new Size(80, 40));
        Assert.Equal(24 + 2 * expectedGap, grid.DesiredSize.Width, 10);
        Assert.Equal(8 + expectedGap, grid.DesiredSize.Height, 10);
        grid.Arrange(new Rect(0, 0, 80, 40));

        var first = grid.Children[0].LayoutBounds;
        var next = grid.Children[1].LayoutBounds;
        var below = grid.Children[3].LayoutBounds;
        var last = grid.Children[5].LayoutBounds;
        Assert.Equal(expectedGap, next.X - first.X - first.Width, 10);
        Assert.Equal(expectedGap, below.Y - first.Y - first.Height, 10);
        Assert.Equal(80, last.X + last.Width, 10);
        Assert.Equal(40, last.Y + last.Height, 10);
        if (!rounding)
            Assert.Equal((80 - 1.2) / 3, first.Width, 10);
    }

    [Fact]
    public void PropertyAndChildChangesInvalidateLayoutAndRecomputeDimensions()
    {
        var grid = new UniformGrid();
        var root = new Panel();
        root.Children.Add(grid);
        var first = new TestNode();
        var second = new TestNode();
        grid.Children.Add(first);
        grid.Children.Add(second);
        Action[] mutations =
        [
            () => grid.Rows = 2,
            () => grid.Columns = 2,
            () => grid.RowSpacing = 3,
            () => grid.ColumnSpacing = 4,
            () => second.Visibility = Visibility.Collapsed,
            () => grid.Children.Add(new TestNode())
        ];
        foreach (var mutation in mutations)
        {
            Layout(root);
            mutation();
            Assert.False(grid.IsMeasureValid);
            Assert.False(grid.IsArrangeValid);
            Assert.False(root.IsMeasureValid);
        }

        grid.Rows = 0;
        grid.Columns = 0;
        grid.RowSpacing = 0;
        grid.ColumnSpacing = 0;
        grid.Children.RemoveAt(2);
        Layout(root);
        Assert.Equal(new Rect(0, 0, 100, 100), first.LayoutBounds);
    }

    [Fact]
    public void BoundColumnCountUpdatesLayout()
    {
        var source = new UniformGrid { Columns = 2 };
        var grid = new UniformGrid();
        for (var index = 0; index < 3; index++)
            grid.Children.Add(new TestNode());
        grid.Bind(UniformGrid.ColumnsProperty, source, UniformGrid.ColumnsProperty);
        Layout(grid);

        source.Columns = 3;

        Assert.Equal(3, grid.Columns);
        Assert.False(grid.IsMeasureValid);
        Layout(grid);
        Assert.Equal(0, grid.Children[2].LayoutBounds.Y);
    }

    [Fact]
    public void MeasureCallbackChangesDoNotCommitTheObsoleteDimensions()
    {
        var grid = new UniformGrid { Columns = 2 };
        var first = new TestNode();
        var second = new TestNode();
        grid.Children.Add(first);
        grid.Children.Add(second);
        first.MeasureAction = () =>
        {
            first.MeasureAction = null;
            grid.Columns = 1;
            second.Visibility = Visibility.Collapsed;
        };

        grid.Measure(new Size(100, 100));

        Assert.False(grid.IsMeasureValid);
        Layout(grid);
        Assert.True(grid.IsMeasureValid);
        Assert.True(grid.IsArrangeValid);
        Assert.Equal(new Rect(0, 0, 100, 100), first.LayoutBounds);
        Assert.Equal(Rect.Zero, second.LayoutBounds);
    }

    [Fact]
    public void ArrangeCallbackRemeasureRequiresArrangementWithTheNewDimensions()
    {
        var grid = new UniformGrid { Columns = 2 };
        var first = new TestNode();
        grid.Children.Add(first);
        grid.Children.Add(new TestNode());
        grid.Children.Add(new TestNode());
        first.ArrangeAction = () =>
        {
            first.ArrangeAction = null;
            grid.Columns = 3;
            grid.Measure(new Size(100, 100));
        };
        grid.Measure(new Size(100, 100));

        grid.Arrange(new Rect(0, 0, 100, 100));

        Assert.False(grid.IsArrangeValid);
        Layout(grid);
        Assert.True(grid.IsMeasureValid);
        Assert.True(grid.IsArrangeValid);
        Assert.Equal(new Rect(67, 0, 33, 100), grid.Children[2].LayoutBounds);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void NegativeCountsAreRejectedBeforeChangingProperties(int value)
    {
        var grid = new UniformGrid();
        Assert.Throws<ArgumentException>(() => grid.Rows = value);
        Assert.Throws<ArgumentException>(() => grid.Columns = value);
        Assert.Equal(0, grid.Rows);
        Assert.Equal(0, grid.Columns);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(-1)]
    public void InvalidSpacingIsRejectedBeforeChangingProperties(double value)
    {
        var grid = new UniformGrid();
        Assert.Throws<ArgumentException>(() => grid.RowSpacing = value);
        Assert.Throws<ArgumentException>(() => grid.ColumnSpacing = value);
        Assert.Equal(0, grid.RowSpacing);
        Assert.Equal(0, grid.ColumnSpacing);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MeasurementOverflowLeavesLayoutInvalid(bool spacingOverflow)
    {
        var grid = new UniformGrid { Columns = 3 };
        _ = new UiScreen(grid) { UseLayoutRounding = false };
        if (spacingOverflow)
            grid.ColumnSpacing = double.MaxValue;
        grid.Children.Add(new TestNode { CoreDesiredSize = new Size(double.MaxValue, 0) });

        Assert.Throws<InvalidOperationException>(() => grid.Measure(Size.Infinite));
        Assert.False(grid.IsMeasureValid);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CollectionMutationDuringLayoutFailsImmediately(bool measure)
    {
        var grid = new UniformGrid();
        var child = new TestNode();
        grid.Children.Add(child);
        if (measure)
        {
            child.MeasureAction = () => grid.Children.Add(new TestNode());
            Assert.Throws<InvalidOperationException>(() => grid.Measure(new Size(100, 100)));
            Assert.False(grid.IsMeasureValid);
        }
        else
        {
            grid.Measure(new Size(100, 100));
            child.ArrangeAction = () => grid.Children.Add(new TestNode());
            Assert.Throws<InvalidOperationException>(() => grid.Arrange(new Rect(0, 0, 100, 100)));
            Assert.False(grid.IsArrangeValid);
        }
    }

    [Fact]
    public void CssUpdatesAllGridPropertiesAndInvalidatesLayout()
    {
        var grid = new UniformGrid();
        var screen = new UiScreen(grid);
        Layout(grid);
        screen.SetStyleSheets([UiStyleSheet.Parse(
            "UniformGrid { rows: 2; columns: +3; row-spacing: 4px; column-spacing: 5; }")]);

        grid.UpdateStyles();

        Assert.Equal(2, grid.Rows);
        Assert.Equal(3, grid.Columns);
        Assert.Equal(4, grid.RowSpacing);
        Assert.Equal(5, grid.ColumnSpacing);
        Assert.False(grid.IsMeasureValid);
    }

    [Theory]
    [InlineData("2.0")]
    [InlineData("2px")]
    [InlineData("2e1")]
    [InlineData("2147483648")]
    public void CssCountsRejectNonIntegerSyntaxAndOverflow(string value)
    {
        var grid = new UniformGrid();
        var resolver = new UiStyleResolver([], [UiStyleSheet.Parse($"UniformGrid {{ columns: {value}; }}")]);

        var error = Assert.Throws<UiStyleParseException>(() => resolver.Resolve(grid));

        Assert.Equal(UiStyleParseError.InvalidValue, error.Error);
    }

    [Fact]
    public void CssNegativeCountsUseRegisteredPropertyValidation()
    {
        var grid = new UniformGrid();
        var screen = new UiScreen(grid);
        screen.SetStyleSheets([UiStyleSheet.Parse("UniformGrid { rows: -1; }")]);

        var error = Assert.Throws<UiStyleParseException>(grid.UpdateStyles);
        Assert.Equal(UiStyleParseError.InvalidValue, error.Error);
        Assert.IsType<ArgumentException>(error.InnerException);
        Assert.Equal(0, grid.Rows);
    }

    [Theory]
    [InlineData("row-spacing", "-1")]
    [InlineData("column-spacing", "-1")]
    [InlineData("row-spacing", "NaN")]
    [InlineData("column-spacing", "NaN")]
    [InlineData("row-spacing", "Infinity")]
    [InlineData("column-spacing", "Infinity")]
    public void CssInvalidSpacingReportsAValueError(string property, string value)
    {
        var grid = new UniformGrid();
        var screen = new UiScreen(grid);
        screen.SetStyleSheets([UiStyleSheet.Parse($"UniformGrid {{ {property}: {value}; }}")]);

        var error = Assert.Throws<UiStyleParseException>(grid.UpdateStyles);

        Assert.Equal(UiStyleParseError.InvalidValue, error.Error);
        if (value == "-1")
            Assert.IsType<ArgumentException>(error.InnerException);
        Assert.Equal(0, grid.RowSpacing);
        Assert.Equal(0, grid.ColumnSpacing);
    }

    private static void Layout(UiNode node)
    {
        node.Measure(new Size(100, 100));
        node.Arrange(new Rect(0, 0, 100, 100));
    }

    private sealed class TestNode : UiNode
    {
        internal Size CoreDesiredSize { get; init; }
        internal Size LastMeasureConstraint { get; private set; }
        internal int MeasureCount { get; private set; }
        internal Action? MeasureAction { get; set; }
        internal Action? ArrangeAction { get; set; }

        protected override Size MeasureCore(Size availableSize)
        {
            MeasureCount++;
            LastMeasureConstraint = availableSize;
            MeasureAction?.Invoke();
            return CoreDesiredSize;
        }

        protected override void ArrangeCore(Size finalSize) => ArrangeAction?.Invoke();
    }
}
