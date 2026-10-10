using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Styling;

namespace PanguEngine.Tests.Client.UI;

public sealed class WrapPanelTests
{
    [Theory]
    [InlineData(Orientation.Horizontal)]
    [InlineData(Orientation.Vertical)]
    public void WrapsAlongTheSelectedAxisAndUsesEachLinesLargestCrossSize(Orientation orientation)
    {
        var panel = new WrapPanel { Orientation = orientation, ItemSpacing = 10, LineSpacing = 5 };
        var first = new TestNode(AxisSize(orientation, 40, 10));
        var second = new TestNode(AxisSize(orientation, 40, 20));
        var third = new TestNode(AxisSize(orientation, 40, 15));
        panel.Children.Add(first);
        panel.Children.Add(second);
        panel.Children.Add(third);

        panel.Measure(new Size(100, 100));

        Assert.Equal(AxisSize(orientation, 90, 40), panel.DesiredSize);
        Assert.Equal(new Size(100, 100), first.LastMeasureConstraint);
        Assert.Equal(first.LastMeasureConstraint, second.LastMeasureConstraint);

        panel.Arrange(new Rect(0, 0, 100, 100));

        Assert.Equal(AxisRect(orientation, 0, 0, 40, 20), first.LayoutBounds);
        Assert.Equal(AxisRect(orientation, 50, 0, 40, 20), second.LayoutBounds);
        Assert.Equal(AxisRect(orientation, 0, 25, 40, 15), third.LayoutBounds);
    }

    [Theory]
    [InlineData(Orientation.Horizontal)]
    [InlineData(Orientation.Vertical)]
    public void ArrangeReflowsAtTheFinalSizeWithoutRemeasuringChildren(Orientation orientation)
    {
        var panel = new WrapPanel { Orientation = orientation, ItemSpacing = 10, LineSpacing = 5 };
        var first = new TestNode(AxisSize(orientation, 40, 10));
        var second = new TestNode(AxisSize(orientation, 40, 10));
        var third = new TestNode(AxisSize(orientation, 40, 10));
        panel.Children.Add(first);
        panel.Children.Add(second);
        panel.Children.Add(third);
        panel.Measure(AxisSize(orientation, 100, 100));

        panel.Arrange(AxisRect(orientation, 0, 0, 140, 100));

        Assert.Equal(AxisRect(orientation, 100, 0, 40, 10), third.LayoutBounds);

        panel.Arrange(AxisRect(orientation, 0, 0, 80, 100));

        Assert.Equal(AxisRect(orientation, 0, 15, 40, 10), second.LayoutBounds);
        Assert.Equal(AxisRect(orientation, 0, 30, 40, 10), third.LayoutBounds);
        Assert.Equal(1, first.MeasureCount);
        Assert.Equal(1, second.MeasureCount);
        Assert.Equal(1, third.MeasureCount);
    }

    [Fact]
    public void BorderPaddingMarginsAndVisibilityParticipateInWrapping()
    {
        var panel = new WrapPanel
        {
            BorderThickness = new Thickness(2),
            Padding = new Thickness(3),
            ItemSpacing = 6,
            LineSpacing = 4
        };
        var first = new TestNode(new Size(30, 10)) { Margin = new Thickness(2) };
        var hidden = new TestNode(new Size(30, 20)) { Visibility = Visibility.Hidden };
        var collapsed = new TestNode(new Size(100, 100)) { Visibility = Visibility.Collapsed };
        var last = new TestNode(new Size(30, 15));
        panel.Children.Add(first);
        panel.Children.Add(collapsed);
        panel.Children.Add(hidden);
        panel.Children.Add(last);

        panel.Measure(new Size(80, 100));

        Assert.Equal(new Size(66, 86), first.LastMeasureConstraint);
        Assert.Equal(new Size(70, 90), hidden.LastMeasureConstraint);
        Assert.Equal(new Size(80, 49), panel.DesiredSize);
        Assert.Equal(0, collapsed.MeasureCount);
        Assert.True(collapsed.IsMeasureValid);

        panel.Arrange(new Rect(0, 0, 80, 100));

        Assert.Equal(new Rect(7, 7, 30, 16), first.LayoutBounds);
        Assert.Equal(new Rect(45, 5, 30, 20), hidden.LayoutBounds);
        Assert.Equal(new Rect(5, 29, 30, 15), last.LayoutBounds);
        Assert.Equal(Rect.Zero, collapsed.LayoutBounds);
        Assert.True(collapsed.IsArrangeValid);
        Assert.Equal(0, collapsed.ArrangeCount);
    }

    [Fact]
    public void ChildCrossAxisAlignmentUsesTheLineSlot()
    {
        var panel = new WrapPanel { ItemSpacing = 5 };
        var first = new TestNode(new Size(20, 10)) { VerticalAlignment = VerticalAlignment.Bottom };
        var second = new TestNode(new Size(20, 30));
        panel.Children.Add(first);
        panel.Children.Add(second);
        panel.Measure(new Size(100, 100));
        panel.Arrange(new Rect(0, 0, 100, 100));

        Assert.Equal(new Rect(0, 20, 20, 10), first.LayoutBounds);
        Assert.Equal(new Rect(25, 0, 20, 30), second.LayoutBounds);
    }

    [Fact]
    public void EmptyAndAllCollapsedPanelsHaveNoContentRequirement()
    {
        var empty = new WrapPanel { ItemSpacing = 10, LineSpacing = 20 };
        empty.Measure(new Size(100, 100));
        empty.Arrange(new Rect(0, 0, 100, 100));
        Assert.Equal(Size.Zero, empty.DesiredSize);

        var panel = new WrapPanel { ItemSpacing = 10, LineSpacing = 20 };
        var collapsed = new TestNode(new Size(30, 20)) { Visibility = Visibility.Collapsed };
        panel.Children.Add(collapsed);
        panel.Measure(new Size(100, 100));
        panel.Arrange(new Rect(0, 0, 100, 100));

        Assert.Equal(Size.Zero, panel.DesiredSize);
        Assert.True(collapsed.IsMeasureValid);
        Assert.True(collapsed.IsArrangeValid);
        Assert.Equal(Rect.Zero, collapsed.LayoutBounds);
    }

    [Fact]
    public void ZeroSizeChildrenStillParticipateInItemSpacing()
    {
        var panel = new WrapPanel { ItemSpacing = 10, LineSpacing = 5 };
        var first = new TestNode(Size.Zero);
        var second = new TestNode(Size.Zero) { Visibility = Visibility.Hidden };
        panel.Children.Add(first);
        panel.Children.Add(second);
        panel.Measure(new Size(100, 100));
        panel.Arrange(new Rect(0, 0, 100, 100));

        Assert.Equal(new Size(10, 0), panel.DesiredSize);
        Assert.Equal(new Rect(10, 0, 0, 0), second.LayoutBounds);

        panel.Measure(Size.Zero);
        panel.Arrange(Rect.Zero);

        Assert.Equal(new Size(0, 5), panel.DesiredSize);
        Assert.Equal(new Rect(0, 5, 0, 0), second.LayoutBounds);
    }

    [Theory]
    [InlineData(Orientation.Horizontal)]
    [InlineData(Orientation.Vertical)]
    public void OversizedChildOccupiesOneLineWithoutAnEmptyPrecedingLine(Orientation orientation)
    {
        var panel = new WrapPanel { Orientation = orientation, LineSpacing = 5 };
        var oversized = new TestNode(AxisSize(orientation, 120, 10));
        var zero = new TestNode(Size.Zero);
        var last = new TestNode(AxisSize(orientation, 20, 15));
        panel.Children.Add(oversized);
        panel.Children.Add(zero);
        panel.Children.Add(last);
        panel.Measure(new Size(100, 100));
        panel.Arrange(new Rect(0, 0, 100, 100));

        Assert.Equal(AxisSize(orientation, 120, 30), panel.DesiredSize);
        Assert.Equal(AxisRect(orientation, 0, 0, 120, 10), oversized.LayoutBounds);
        Assert.Equal(AxisRect(orientation, 0, 15, 0, 15), zero.LayoutBounds);
        Assert.Equal(AxisRect(orientation, 0, 15, 20, 15), last.LayoutBounds);
    }

    [Theory]
    [InlineData(Orientation.Horizontal)]
    [InlineData(Orientation.Vertical)]
    public void InfiniteMainAxisKeepsEveryChildOnOneLine(Orientation orientation)
    {
        var panel = new WrapPanel { Orientation = orientation, ItemSpacing = 10, LineSpacing = 50 };
        panel.Children.Add(new TestNode(AxisSize(orientation, 40, 10)));
        panel.Children.Add(new TestNode(AxisSize(orientation, 40, 20)));
        panel.Children.Add(new TestNode(AxisSize(orientation, 40, 15)));
        panel.Measure(AxisSize(orientation, double.PositiveInfinity, 100));

        Assert.Equal(AxisSize(orientation, 140, 20), panel.DesiredSize);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void LayoutPropertyChangesInvalidateMeasureForPanelAndAncestors(int property)
    {
        var panel = new WrapPanel();
        panel.Children.Add(new TestNode(new Size(20, 10)));
        var root = new Panel();
        root.Children.Add(panel);
        root.Measure(new Size(100, 100));
        root.Arrange(new Rect(0, 0, 100, 100));

        switch (property)
        {
            case 0:
                panel.ItemSpacing = 5;
                break;
            case 1:
                panel.LineSpacing = 5;
                break;
            case 2:
                panel.Orientation = Orientation.Vertical;
                break;
        }

        Assert.False(panel.IsMeasureValid);
        Assert.False(panel.IsArrangeValid);
        Assert.False(root.IsMeasureValid);
        Assert.False(root.IsArrangeValid);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(-1)]
    public void InvalidSpacingIsRejectedWithoutChangingValues(double value)
    {
        var panel = new WrapPanel { ItemSpacing = 3, LineSpacing = 4 };

        Assert.Throws<ArgumentException>(() => panel.ItemSpacing = value);
        Assert.Throws<ArgumentException>(() => panel.LineSpacing = value);
        Assert.Equal(3, panel.ItemSpacing);
        Assert.Equal(4, panel.LineSpacing);
    }

    [Fact]
    public void CssCanConfigureDirectionAndBothSpacingProperties()
    {
        var panel = new WrapPanel();
        var screen = new UiScreen(panel);
        screen.SetStyleSheets([UiStyleSheet.Parse(
            "WrapPanel { orientation: vertical; item-spacing: 10px; line-spacing: 5px; }")]);
        panel.Children.Add(new TestNode(new Size(10, 40)));
        panel.Children.Add(new TestNode(new Size(20, 40)));
        panel.Children.Add(new TestNode(new Size(15, 40)));
        panel.UpdateStyles();
        panel.Measure(new Size(100, 100));
        panel.Arrange(new Rect(0, 0, 100, 100));

        Assert.Equal(Orientation.Vertical, panel.Orientation);
        Assert.Equal(10, panel.ItemSpacing);
        Assert.Equal(5, panel.LineSpacing);
        Assert.Equal(new Size(40, 90), panel.DesiredSize);
        Assert.Equal(new Rect(25, 0, 15, 40), panel.Children[2].LayoutBounds);
    }

    [Theory]
    [InlineData(Orientation.Horizontal)]
    [InlineData(Orientation.Vertical)]
    public void RoundedFractionalItemsFitExactlyAndSpacingFollowsRoundingSetting(Orientation orientation)
    {
        var panel = new WrapPanel { Orientation = orientation, ItemSpacing = 0.6, LineSpacing = 0.6 };
        var screen = new UiScreen(panel) { Scale = 1.25 };
        var first = new TestNode(AxisSize(orientation, 6, 10));
        var second = new TestNode(AxisSize(orientation, 6, 12));
        var third = new TestNode(AxisSize(orientation, 6, 8));
        panel.Children.Add(first);
        panel.Children.Add(second);
        panel.Children.Add(third);
        panel.Measure(AxisSize(orientation, 13.6, 100));
        panel.Arrange(AxisRect(orientation, 0, 0, 13.6, 100));

        Assert.Equal(13.6, Main(panel.DesiredSize, orientation), 10);
        Assert.Equal(20.8, Cross(panel.DesiredSize, orientation), 10);
        Assert.Equal(7.2, MainOrigin(second.LayoutBounds, orientation), 10);
        Assert.Equal(0, CrossOrigin(second.LayoutBounds, orientation), 10);
        Assert.Equal(12.8, CrossOrigin(third.LayoutBounds, orientation), 10);

        screen.UseLayoutRounding = false;
        panel.Measure(AxisSize(orientation, 13.6, 100));
        panel.Arrange(AxisRect(orientation, 0, 0, 13.6, 100));

        Assert.Equal(12.6, Main(panel.DesiredSize, orientation), 10);
        Assert.Equal(20.6, Cross(panel.DesiredSize, orientation), 10);
        Assert.Equal(6.6, MainOrigin(second.LayoutBounds, orientation), 10);
        Assert.Equal(12.6, CrossOrigin(third.LayoutBounds, orientation), 10);
    }

    [Fact]
    public void RoundedItemsWrapWhenAvailableSpaceIsActuallyTooSmall()
    {
        var panel = new WrapPanel { ItemSpacing = 0.6, LineSpacing = 0.6 };
        _ = new UiScreen(panel) { Scale = 1.25 };
        panel.Children.Add(new TestNode(new Size(6, 10)));
        panel.Children.Add(new TestNode(new Size(6, 12)));
        panel.Measure(new Size(13.5, 100));

        Assert.Equal(6.4, panel.DesiredSize.Width, 10);
        Assert.Equal(23.2, panel.DesiredSize.Height, 10);
    }

    [Theory]
    [InlineData(Orientation.Horizontal)]
    [InlineData(Orientation.Vertical)]
    public void RoundedItemsFitExactlyAfterPaddingReducesAvailableSpace(Orientation orientation)
    {
        var panel = new WrapPanel
        {
            Orientation = orientation,
            Padding = orientation == Orientation.Horizontal
                ? new Thickness(0.8, 0, 0, 0)
                : new Thickness(0, 0.8, 0, 0)
        };
        _ = new UiScreen(panel) { Scale = 1.25 };
        var first = new TestNode(AxisSize(orientation, 6, 8));
        var second = new TestNode(AxisSize(orientation, 6, 8));
        panel.Children.Add(first);
        panel.Children.Add(second);
        panel.Measure(AxisSize(orientation, 13.6, 100));
        panel.Arrange(AxisRect(orientation, 0, 0, 13.6, 100));

        Assert.Equal(13.6, Main(panel.DesiredSize, orientation), 10);
        Assert.Equal(8, Cross(panel.DesiredSize, orientation), 10);
        Assert.Equal(7.2, MainOrigin(second.LayoutBounds, orientation), 10);
        Assert.Equal(0, CrossOrigin(second.LayoutBounds, orientation), 10);
    }

    [Theory]
    [InlineData(Orientation.Horizontal)]
    [InlineData(Orientation.Vertical)]
    public void UnroundedFractionalItemsFitExactlyWithoutAcceptingRealOverflow(Orientation orientation)
    {
        var panel = new WrapPanel { Orientation = orientation };
        _ = new UiScreen(panel) { UseLayoutRounding = false };
        var first = new TestNode(AxisSize(orientation, 0.1, 1));
        var second = new TestNode(AxisSize(orientation, 0.2, 1));
        panel.Children.Add(first);
        panel.Children.Add(second);
        panel.Measure(AxisSize(orientation, 0.3, 100));
        panel.Arrange(AxisRect(orientation, 0, 0, 0.3, 100));

        Assert.Equal(0.3, Main(panel.DesiredSize, orientation), 10);
        Assert.Equal(1, Cross(panel.DesiredSize, orientation));
        Assert.Equal(0.1, MainOrigin(second.LayoutBounds, orientation), 10);
        Assert.Equal(0, CrossOrigin(second.LayoutBounds, orientation));

        panel.Measure(AxisSize(orientation, 0.3 - 1e-8, 100));
        panel.Arrange(AxisRect(orientation, 0, 0, 0.3 - 1e-8, 100));

        Assert.Equal(0.2, Main(panel.DesiredSize, orientation), 10);
        Assert.Equal(2, Cross(panel.DesiredSize, orientation));
        Assert.Equal(0, MainOrigin(second.LayoutBounds, orientation));
        Assert.Equal(1, CrossOrigin(second.LayoutBounds, orientation));
    }

    [Theory]
    [InlineData(Orientation.Horizontal, 5e-11, false)]
    [InlineData(Orientation.Vertical, 5e-11, false)]
    [InlineData(Orientation.Horizontal, 1e-10, false)]
    [InlineData(Orientation.Vertical, 1e-10, false)]
    [InlineData(Orientation.Horizontal, 2e-10, true)]
    [InlineData(Orientation.Vertical, 2e-10, true)]
    public void UnroundedOverflowUsesTheFixedTolerance(Orientation orientation, double overflow, bool wraps)
    {
        var panel = new WrapPanel { Orientation = orientation };
        _ = new UiScreen(panel) { UseLayoutRounding = false };
        var first = new TestNode(AxisSize(orientation, 0, 1));
        var second = new TestNode(AxisSize(orientation, overflow, 1));
        panel.Children.Add(first);
        panel.Children.Add(second);

        panel.Measure(AxisSize(orientation, 0, 100));
        panel.Arrange(AxisRect(orientation, 0, 0, 0, 100));

        Assert.Equal(wraps ? 2 : 1, Cross(panel.DesiredSize, orientation));
        Assert.Equal(wraps ? 1 : 0, CrossOrigin(second.LayoutBounds, orientation));
    }

    [Theory]
    [InlineData(Orientation.Horizontal, true, 0)]
    [InlineData(Orientation.Vertical, true, 0)]
    [InlineData(Orientation.Horizontal, false, 0)]
    [InlineData(Orientation.Vertical, false, 0)]
    [InlineData(Orientation.Horizontal, true, 0.8)]
    [InlineData(Orientation.Vertical, true, 0.8)]
    [InlineData(Orientation.Horizontal, false, 0.8)]
    [InlineData(Orientation.Vertical, false, 0.8)]
    public void LongFractionalLinesFitExactlyAndReflowWhenSpaceIsReduced(
        Orientation orientation,
        bool useLayoutRounding,
        double itemSpacing)
    {
        var panel = new WrapPanel { Orientation = orientation, ItemSpacing = itemSpacing, LineSpacing = 0.8 };
        _ = new UiScreen(panel) { Scale = 1.25, UseLayoutRounding = useLayoutRounding };
        for (var index = 0; index < 200; index++)
            panel.Children.Add(new TestNode(AxisSize(orientation, 0.8, 0.8)));
        var availableMain = itemSpacing == 0 ? 160 : 319.2;

        panel.Measure(AxisSize(orientation, availableMain, 100));
        panel.Arrange(AxisRect(orientation, 0, 0, availableMain, 100));

        Assert.Equal(availableMain, Main(panel.DesiredSize, orientation), 10);
        Assert.Equal(0.8, Cross(panel.DesiredSize, orientation), 10);
        for (var index = 0; index < panel.Children.Count; index++)
        {
            Assert.Equal(index * (0.8 + itemSpacing), MainOrigin(panel.Children[index].LayoutBounds, orientation), 10);
            Assert.Equal(0, CrossOrigin(panel.Children[index].LayoutBounds, orientation));
        }

        panel.Measure(AxisSize(orientation, availableMain - 0.8, 100));
        panel.Arrange(AxisRect(orientation, 0, 0, availableMain - 0.8, 100));

        Assert.Equal(2.4, Cross(panel.DesiredSize, orientation), 10);
        Assert.Equal(0, MainOrigin(panel.Children[199].LayoutBounds, orientation));
        Assert.Equal(1.6, CrossOrigin(panel.Children[199].LayoutBounds, orientation), 10);
        Assert.Equal(0, CrossOrigin(panel.Children[198].LayoutBounds, orientation));
    }

    [Theory]
    [InlineData(Orientation.Horizontal)]
    [InlineData(Orientation.Vertical)]
    public void UnroundedChildSizesWrapWhenOverflowExceedsTolerance(Orientation orientation)
    {
        var panel = new WrapPanel { Orientation = orientation };
        _ = new UiScreen(panel) { UseLayoutRounding = false };
        var first = new TestNode(AxisSize(orientation, 0.1, 1));
        var second = new TestNode(AxisSize(orientation, 0.2 + 1e-8, 1));
        panel.Children.Add(first);
        panel.Children.Add(second);

        panel.Measure(AxisSize(orientation, 0.3, 100));
        panel.Arrange(AxisRect(orientation, 0, 0, 0.3, 100));

        Assert.Equal(2, Cross(panel.DesiredSize, orientation));
        Assert.Equal(0, MainOrigin(second.LayoutBounds, orientation));
        Assert.Equal(1, CrossOrigin(second.LayoutBounds, orientation));
    }

    [Theory]
    [InlineData(Orientation.Horizontal)]
    [InlineData(Orientation.Vertical)]
    public void FractionalArrangementOffsetsAccumulateIndependentlyOfALargeContentOrigin(Orientation orientation)
    {
        var panel = new WrapPanel
        {
            Orientation = orientation,
            Padding = orientation == Orientation.Horizontal
                ? new Thickness(1e16, 0, 0, 0)
                : new Thickness(0, 1e16, 0, 0)
        };
        _ = new UiScreen(panel) { UseLayoutRounding = false };
        for (var index = 0; index < 200; index++)
            panel.Children.Add(new TestNode(AxisSize(orientation, 0.1, 1)));

        panel.Measure(AxisSize(orientation, 1e16 + 32, 100));
        panel.Arrange(AxisRect(orientation, 0, 0, 1e16 + 32, 100));

        Assert.Equal(1e16, MainOrigin(panel.Children[0].LayoutBounds, orientation));
        Assert.Equal(1e16 + 20, MainOrigin(panel.Children[199].LayoutBounds, orientation));
        Assert.Equal(0, CrossOrigin(panel.Children[199].LayoutBounds, orientation));
    }

    [Theory]
    [InlineData(Orientation.Horizontal)]
    [InlineData(Orientation.Vertical)]
    public void SpacingOverflowReflowsForFiniteConstraintsAndFailsForInfiniteConstraints(Orientation orientation)
    {
        var panel = new WrapPanel { Orientation = orientation, ItemSpacing = double.MaxValue };
        _ = new UiScreen(panel) { UseLayoutRounding = false };
        var first = new TestNode(AxisSize(orientation, double.MaxValue, 1));
        var second = new TestNode(AxisSize(orientation, 1, 1));
        panel.Children.Add(first);
        panel.Children.Add(second);

        panel.Measure(AxisSize(orientation, double.MaxValue, 100));
        panel.Arrange(AxisRect(orientation, 0, 0, double.MaxValue, 100));

        Assert.Equal(AxisSize(orientation, double.MaxValue, 2), panel.DesiredSize);
        Assert.Equal(0, MainOrigin(second.LayoutBounds, orientation));
        Assert.Equal(1, CrossOrigin(second.LayoutBounds, orientation));
        Assert.Throws<InvalidOperationException>(() => panel.Measure(Size.Infinite));
        Assert.False(panel.IsMeasureValid);
    }

    [Theory]
    [InlineData(Orientation.Horizontal)]
    [InlineData(Orientation.Vertical)]
    public void NonFiniteLineOrTotalSizeDoesNotCommitMeasurement(Orientation orientation)
    {
        var mainOverflow = new WrapPanel { Orientation = orientation };
        _ = new UiScreen(mainOverflow) { UseLayoutRounding = false };
        mainOverflow.Children.Add(new TestNode(AxisSize(orientation, double.MaxValue, 0)));
        mainOverflow.Children.Add(new TestNode(AxisSize(orientation, double.MaxValue, 0)));

        Assert.Throws<InvalidOperationException>(() => mainOverflow.Measure(Size.Infinite));
        Assert.False(mainOverflow.IsMeasureValid);

        var crossOverflow = new WrapPanel { Orientation = orientation };
        _ = new UiScreen(crossOverflow) { UseLayoutRounding = false };
        crossOverflow.Children.Add(new TestNode(AxisSize(orientation, 1, double.MaxValue)));
        crossOverflow.Children.Add(new TestNode(AxisSize(orientation, 1, double.MaxValue)));

        Assert.Throws<InvalidOperationException>(() =>
            crossOverflow.Measure(AxisSize(orientation, 1, double.PositiveInfinity)));
        Assert.False(crossOverflow.IsMeasureValid);
    }

    [Fact]
    public void FiniteMainAxisWrapsBeforeLineSizeWouldOverflow()
    {
        var panel = new WrapPanel();
        _ = new UiScreen(panel) { UseLayoutRounding = false };
        panel.Children.Add(new TestNode(new Size(double.MaxValue, 1)));
        panel.Children.Add(new TestNode(new Size(double.MaxValue, 1)));
        panel.Measure(new Size(double.MaxValue, 100));

        Assert.Equal(new Size(double.MaxValue, 2), panel.DesiredSize);
    }

    [Theory]
    [InlineData(Orientation.Horizontal)]
    [InlineData(Orientation.Vertical)]
    public void ArrangementOriginOverflowStopsBeforeArrangingTheAffectedChild(Orientation orientation)
    {
        var panel = new WrapPanel { Orientation = orientation };
        _ = new UiScreen(panel) { UseLayoutRounding = false };
        var first = new TestNode(AxisSize(orientation, 1, double.MaxValue));
        var second = new TestNode(AxisSize(orientation, 1, double.MaxValue));
        var third = new TestNode(AxisSize(orientation, 1, double.MaxValue));
        panel.Children.Add(first);
        panel.Children.Add(second);
        panel.Children.Add(third);
        panel.Measure(AxisSize(orientation, 100, double.MaxValue));

        Assert.Throws<InvalidOperationException>(() =>
            panel.Arrange(AxisRect(orientation, 0, 0, 1, double.MaxValue)));

        Assert.Equal(1, first.ArrangeCount);
        Assert.Equal(1, second.ArrangeCount);
        Assert.Equal(0, third.ArrangeCount);
        Assert.False(panel.IsArrangeValid);
    }

    [Fact]
    public void ChildCollectionMutationDuringLayoutFailsFast()
    {
        var measurePanel = new WrapPanel();
        measurePanel.Children.Add(new TestNode(Size.Zero)
        {
            MeasureAction = () => measurePanel.Children.Add(new TestNode(Size.Zero))
        });

        Assert.Throws<InvalidOperationException>(() => measurePanel.Measure(new Size(100, 100)));
        Assert.False(measurePanel.IsMeasureValid);

        var arrangePanel = new WrapPanel();
        arrangePanel.Children.Add(new TestNode(Size.Zero)
        {
            ArrangeAction = () => arrangePanel.Children.Add(new TestNode(Size.Zero))
        });
        arrangePanel.Measure(new Size(100, 100));

        Assert.Throws<InvalidOperationException>(() => arrangePanel.Arrange(new Rect(0, 0, 100, 100)));
        Assert.False(arrangePanel.IsArrangeValid);
    }

    private static Size AxisSize(Orientation orientation, double main, double cross) =>
        orientation == Orientation.Horizontal ? new Size(main, cross) : new Size(cross, main);

    private static Rect AxisRect(Orientation orientation, double main, double cross, double mainSize, double crossSize) =>
        orientation == Orientation.Horizontal
            ? new Rect(main, cross, mainSize, crossSize)
            : new Rect(cross, main, crossSize, mainSize);

    private static double Main(Size size, Orientation orientation) =>
        orientation == Orientation.Horizontal ? size.Width : size.Height;

    private static double Cross(Size size, Orientation orientation) =>
        orientation == Orientation.Horizontal ? size.Height : size.Width;

    private static double MainOrigin(Rect rect, Orientation orientation) =>
        orientation == Orientation.Horizontal ? rect.X : rect.Y;

    private static double CrossOrigin(Rect rect, Orientation orientation) =>
        orientation == Orientation.Horizontal ? rect.Y : rect.X;

    private sealed class TestNode(Size desiredSize) : UiNode
    {
        internal Size LastMeasureConstraint { get; private set; }
        internal int MeasureCount { get; private set; }
        internal int ArrangeCount { get; private set; }
        internal Action? MeasureAction { get; init; }
        internal Action? ArrangeAction { get; init; }

        protected override Size MeasureCore(Size availableSize)
        {
            LastMeasureConstraint = availableSize;
            MeasureCount++;
            MeasureAction?.Invoke();
            return desiredSize;
        }

        protected override void ArrangeCore(Size finalSize)
        {
            ArrangeCount++;
            ArrangeAction?.Invoke();
        }
    }
}
