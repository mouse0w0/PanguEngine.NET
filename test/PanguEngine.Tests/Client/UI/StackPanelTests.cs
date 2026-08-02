using System.Runtime.ExceptionServices;
using PanguEngine.Client.UI;

namespace PanguEngine.Tests.Client.UI;

public sealed class StackPanelTests
{
    [Fact]
    public void PropertiesExposeDefaultsOwnersAndMeasureInvalidation()
    {
        var panel = new StackPanel();

        Assert.Equal(Orientation.Vertical, panel.Orientation);
        Assert.Equal(0, panel.Spacing);
        Assert.Equal(typeof(StackPanel), StackPanel.OrientationProperty.OwnerType);
        Assert.Equal(typeof(StackPanel), StackPanel.OrientationProperty.TargetType);
        Assert.Equal(Orientation.Vertical, StackPanel.OrientationProperty.DefaultValue);
        Assert.Equal(UiPropertyInvalidation.Measure, StackPanel.OrientationProperty.Invalidation);
        Assert.Equal(typeof(StackPanel), StackPanel.SpacingProperty.OwnerType);
        Assert.Equal(typeof(StackPanel), StackPanel.SpacingProperty.TargetType);
        Assert.Equal(0, StackPanel.SpacingProperty.DefaultValue);
        Assert.Equal(UiPropertyInvalidation.Measure, StackPanel.SpacingProperty.Invalidation);
        Assert.True(typeof(StackPanel).IsSealed);
    }

    [Fact]
    public void VerticalMeasureAndArrangeUseInfiniteMainAxisAndIndependentSpacing()
    {
        var panel = new StackPanel
        {
            Padding = new Thickness(10, 20, 30, 40),
            Spacing = 7
        };
        var first = new TestNode
        {
            CoreDesiredSize = new Size(20, 10),
            Margin = new Thickness(1, 2, 3, 4)
        };
        var hidden = new TestNode
        {
            Visibility = Visibility.Hidden,
            CoreDesiredSize = new Size(30, 15),
            Margin = new Thickness(5, 6, 7, 8)
        };
        var collapsed = new TestNode
        {
            Visibility = Visibility.Collapsed,
            CoreDesiredSize = new Size(100, 100),
            Margin = new Thickness(10)
        };
        panel.Children.Add(first);
        panel.Children.Add(collapsed);
        panel.Children.Add(hidden);

        panel.Measure(new Size(200, 300));

        Assert.Equal(new Size(156, double.PositiveInfinity), first.LastMeasureConstraint);
        Assert.Equal(new Size(148, double.PositiveInfinity), hidden.LastMeasureConstraint);
        Assert.Equal(0, collapsed.MeasureCount);
        Assert.Equal(Size.Zero, collapsed.DesiredSize);
        Assert.Equal(new Size(82, 112), panel.DesiredSize);

        panel.Arrange(new Rect(0, 0, 200, 300));

        Assert.Equal(new Rect(11, 22, 156, 10), first.LayoutBounds);
        Assert.Equal(new Rect(15, 49, 148, 15), hidden.LayoutBounds);
        Assert.Equal(Rect.Zero, collapsed.LayoutBounds);
        Assert.True(collapsed.IsArrangeValid);
        Assert.Equal(0, collapsed.ArrangeCount);
    }

    [Fact]
    public void HorizontalMeasureAndArrangeUseTheSameAxisAlgorithm()
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 5
        };
        var first = new TestNode
        {
            CoreDesiredSize = new Size(10, 20),
            Margin = new Thickness(1, 2, 3, 4)
        };
        var second = new TestNode
        {
            CoreDesiredSize = new Size(15, 10),
            Margin = new Thickness(5, 6, 7, 8)
        };
        panel.Children.Add(first);
        panel.Children.Add(second);

        panel.Measure(new Size(100, 80));

        Assert.Equal(new Size(double.PositiveInfinity, 74), first.LastMeasureConstraint);
        Assert.Equal(new Size(double.PositiveInfinity, 66), second.LastMeasureConstraint);
        Assert.Equal(new Size(46, 26), panel.DesiredSize);

        panel.Arrange(new Rect(0, 0, 100, 80));

        Assert.Equal(new Rect(1, 2, 10, 74), first.LayoutBounds);
        Assert.Equal(new Rect(24, 6, 15, 66), second.LayoutBounds);
    }

    [Fact]
    public void EmptyCollapsedAndZeroSizeParticipantsAreDeterministic()
    {
        var empty = new StackPanel { Spacing = 12 };
        empty.Measure(new Size(100, 100));
        Assert.Equal(Size.Zero, empty.DesiredSize);

        var panel = new StackPanel { Spacing = 12 };
        var collapsed = new TestNode { Visibility = Visibility.Collapsed };
        var firstZero = new TestNode();
        var secondZero = new TestNode { Visibility = Visibility.Hidden };
        panel.Children.Add(collapsed);
        panel.Children.Add(firstZero);
        panel.Children.Add(secondZero);

        panel.Measure(new Size(100, 100));

        Assert.Equal(new Size(0, 12), panel.DesiredSize);
    }

    [Fact]
    public void AllCollapsedChildrenProduceZeroContentRequirement()
    {
        var panel = new StackPanel { Spacing = 12 };
        var first = new TestNode { Visibility = Visibility.Collapsed };
        var second = new TestNode { Visibility = Visibility.Collapsed };
        panel.Children.Add(first);
        panel.Children.Add(second);

        panel.Measure(new Size(100, 100));

        Assert.Equal(Size.Zero, panel.DesiredSize);
        Assert.True(first.IsMeasureValid);
        Assert.True(second.IsMeasureValid);
        Assert.Equal(0, first.MeasureCount);
        Assert.Equal(0, second.MeasureCount);
    }

    [Fact]
    public void ExplicitSizeMinMaxAndCrossAxisAlignmentFollowUiNodeRules()
    {
        var panel = new StackPanel();
        var explicitChild = new TestNode
        {
            CoreDesiredSize = new Size(5, 5),
            Width = 20,
            Height = 10,
            Margin = new Thickness(2),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var constrainedChild = new TestNode
        {
            CoreDesiredSize = new Size(5, 10),
            MinWidth = 30,
            MaxWidth = 20
        };
        panel.Children.Add(explicitChild);
        panel.Children.Add(constrainedChild);
        panel.Measure(new Size(100, 100));

        Assert.Equal(new Size(30, 24), panel.DesiredSize);

        panel.Arrange(new Rect(0, 0, 100, 100));

        Assert.Equal(new Rect(78, 2, 20, 10), explicitChild.LayoutBounds);
        Assert.Equal(new Rect(35, 14, 30, 10), constrainedChild.LayoutBounds);
    }

    [Fact]
    public void CollapsedStackPanelDoesNotMeasureItsChildren()
    {
        var panel = new StackPanel { Visibility = Visibility.Collapsed };
        var child = new TestNode { CoreDesiredSize = new Size(20, 10) };
        panel.Children.Add(child);

        panel.Measure(new Size(100, 100));

        Assert.Equal(Size.Zero, panel.DesiredSize);
        Assert.Equal(0, child.MeasureCount);
    }

    [Fact]
    public void PanelAndChildPropertyChangesInvalidateTheExpectedAncestors()
    {
        var root = new TestPanel();
        var panel = new StackPanel();
        var child = new TestNode { CoreDesiredSize = new Size(10, 10) };
        panel.Children.Add(child);
        root.Children.Add(panel);
        ValidateLayout(root);

        panel.Spacing = 4;
        Assert.False(panel.IsMeasureValid);
        Assert.False(root.IsMeasureValid);

        ValidateLayout(root);
        child.Visibility = Visibility.Collapsed;
        Assert.False(child.IsMeasureValid);
        Assert.False(panel.IsMeasureValid);
        Assert.False(root.IsMeasureValid);
    }

    [Fact]
    public void ChildrenStructureChangesInvalidateStackPanelAndAncestors()
    {
        var root = new TestPanel();
        var panel = new StackPanel();
        panel.Children.Add(new TestNode { CoreDesiredSize = new Size(10, 10) });
        root.Children.Add(panel);
        ValidateLayout(root);

        panel.Children.Add(new TestNode());

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
    public void InvalidSpacingFailsBeforeMeasuringChildren(double spacing)
    {
        var panel = new StackPanel { Spacing = spacing };
        var child = new TestNode();
        panel.Children.Add(child);

        Assert.Throws<InvalidOperationException>(() => panel.Measure(new Size(100, 100)));
        Assert.Equal(0, child.MeasureCount);
        Assert.False(panel.IsMeasureValid);
    }

    [Fact]
    public void InvalidOrientationFailsBeforeMeasuringChildren()
    {
        var panel = new StackPanel { Orientation = (Orientation)99 };
        var child = new TestNode();
        panel.Children.Add(child);

        Assert.Throws<InvalidOperationException>(() => panel.Measure(new Size(100, 100)));
        Assert.Equal(0, child.MeasureCount);
    }

    [Fact]
    public void MainAxisMeasurementOverflowDoesNotCommitPanelMeasure()
    {
        var panel = new StackPanel { Spacing = 1 };
        panel.Children.Add(new TestNode { CoreDesiredSize = new Size(0, double.MaxValue) });
        panel.Children.Add(new TestNode());

        Assert.Throws<InvalidOperationException>(() => panel.Measure(Size.Infinite));
        Assert.False(panel.IsMeasureValid);
    }

    [Fact]
    public void NonFiniteLaterSlotOriginFailsBeforeArrangingThatChild()
    {
        var panel = new StackPanel();
        var first = new TestNode { CoreDesiredSize = new Size(0, double.MaxValue) };
        var second = new TestNode();
        var third = new TestNode();
        first.ArrangeAction = () =>
        {
            second.CoreDesiredSize = new Size(0, double.MaxValue);
            second.InvalidateMeasure();
            second.Measure(Size.Infinite);
        };
        panel.Children.Add(first);
        panel.Children.Add(second);
        panel.Children.Add(third);
        panel.Measure(Size.Infinite);

        Assert.Throws<InvalidOperationException>(() =>
            panel.Arrange(new Rect(0, 0, 0, double.MaxValue)));

        Assert.Equal(1, first.ArrangeCount);
        Assert.Equal(1, second.ArrangeCount);
        Assert.Equal(0, third.ArrangeCount);
        Assert.False(panel.IsArrangeValid);
    }

    [Fact]
    public void ChildCollectionMutationDuringLayoutUsesFailFastEnumeration()
    {
        var measurePanel = new StackPanel();
        var measureChild = new TestNode();
        measureChild.MeasureAction = () => measurePanel.Children.Add(new TestNode());
        measurePanel.Children.Add(measureChild);

        Assert.Throws<InvalidOperationException>(() =>
            measurePanel.Measure(new Size(100, 100)));
        Assert.False(measurePanel.IsMeasureValid);

        var arrangePanel = new StackPanel();
        var arrangeChild = new TestNode();
        arrangeChild.ArrangeAction = () => arrangePanel.Children.Add(new TestNode());
        arrangePanel.Children.Add(arrangeChild);
        arrangePanel.Measure(new Size(100, 100));

        Assert.Throws<InvalidOperationException>(() =>
            arrangePanel.Arrange(new Rect(0, 0, 100, 100)));
        Assert.False(arrangePanel.IsArrangeValid);
    }

    [Fact]
    public void ActiveStackPanelPropertiesRejectWrongThreadWithoutPartialState()
    {
        var dispatcher = new UiDispatcher();
        var panel = new StackPanel();
        ValidateLayout(panel);
        panel.AttachToTree(dispatcher);

        var result = RunOnBackgroundThread(() =>
            (Spacing: Record.Exception(() => panel.Spacing = 4),
                Orientation: Record.Exception(() => panel.Orientation = Orientation.Horizontal)));

        Assert.IsType<InvalidOperationException>(result.Spacing);
        Assert.IsType<InvalidOperationException>(result.Orientation);
        Assert.Equal(0, panel.Spacing);
        Assert.Equal(Orientation.Vertical, panel.Orientation);
        Assert.True(panel.IsMeasureValid);
        Assert.True(panel.IsArrangeValid);
    }

    private static void ValidateLayout(UiNode node)
    {
        node.Measure(new Size(100, 100));
        node.Arrange(new Rect(0, 0, 100, 100));
    }

    private sealed class TestPanel : Panel
    {
    }

    private sealed class TestNode : UiNode
    {
        internal Size CoreDesiredSize { get; set; }
        internal Size LastMeasureConstraint { get; private set; }
        internal int MeasureCount { get; private set; }
        internal int ArrangeCount { get; private set; }
        internal Action? MeasureAction { get; set; }
        internal Action? ArrangeAction { get; set; }

        protected override Size MeasureCore(Size availableSize)
        {
            MeasureCount++;
            LastMeasureConstraint = availableSize;
            MeasureAction?.Invoke();
            return CoreDesiredSize;
        }

        protected override void ArrangeCore(Size finalSize)
        {
            ArrangeCount++;
            ArrangeAction?.Invoke();
        }
    }

    private static T RunOnBackgroundThread<T>(Func<T> action)
    {
        T result = default!;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = action();
            }
            catch (Exception exception)
            {
                error = exception;
            }
        });
        thread.Start();
        thread.Join();

        if (error is not null)
            ExceptionDispatchInfo.Capture(error).Throw();

        return result;
    }
}
