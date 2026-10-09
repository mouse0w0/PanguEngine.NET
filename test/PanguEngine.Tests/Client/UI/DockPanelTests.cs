using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.ComponentModel;

namespace PanguEngine.Tests.Client.UI;

public sealed class DockPanelTests
{
    [Fact]
    public void AttachedPropertyUsesDefaultsNotificationsAndClearValue()
    {
        var panel = new DockPanel();
        var child = new TestNode();
        var changes = 0;
        using var subscription = child.Subscribe(DockPanel.DockProperty, (_, _) => changes++);

        Assert.True(panel.LastChildFill);
        Assert.Equal(Dock.Left, DockPanel.GetDock(child));
        Assert.Equal(typeof(DockPanel), DockPanel.DockProperty.OwnerType);
        Assert.Equal(typeof(UiNode), DockPanel.DockProperty.TargetType);
        Assert.Equal(typeof(DockPanel), DockPanel.LastChildFillProperty.OwnerType);
        Assert.Equal(typeof(DockPanel), DockPanel.LastChildFillProperty.TargetType);
        Assert.True(DockPanel.LastChildFillProperty.DefaultValue);

        DockPanel.SetDock(child, Dock.Bottom);
        DockPanel.SetDock(child, Dock.Bottom);
        Assert.Equal(Dock.Bottom, child.GetValue(DockPanel.DockProperty));
        Assert.Equal(1, changes);

        child.ClearValue(DockPanel.DockProperty);
        Assert.Equal(Dock.Left, DockPanel.GetDock(child));
        Assert.Equal(2, changes);
    }

    [Fact]
    public void MixedDockingMeasuresRemainingSpaceAndArrangesFromEachEdge()
    {
        var panel = new DockPanel();
        var top = new TestNode { CoreDesiredSize = new Size(80, 10) };
        var left = new TestNode { CoreDesiredSize = new Size(20, 30) };
        var right = new TestNode { CoreDesiredSize = new Size(15, 25) };
        var bottom = new TestNode { CoreDesiredSize = new Size(40, 5) };
        var fill = new TestNode { CoreDesiredSize = new Size(10, 10) };
        DockPanel.SetDock(top, Dock.Top);
        DockPanel.SetDock(left, Dock.Left);
        DockPanel.SetDock(right, Dock.Right);
        DockPanel.SetDock(bottom, Dock.Bottom);
        panel.Children.AddRange([top, left, right, bottom, fill]);

        panel.Measure(new Size(100, 60));

        Assert.Equal(new Size(100, 60), top.LastMeasureConstraint);
        Assert.Equal(new Size(100, 50), left.LastMeasureConstraint);
        Assert.Equal(new Size(80, 50), right.LastMeasureConstraint);
        Assert.Equal(new Size(65, 50), bottom.LastMeasureConstraint);
        Assert.Equal(new Size(65, 45), fill.LastMeasureConstraint);
        Assert.Equal(new Size(80, 40), panel.DesiredSize);

        panel.Arrange(new Rect(0, 0, 100, 60));

        Assert.Equal(new Rect(0, 0, 100, 10), top.LayoutBounds);
        Assert.Equal(new Rect(0, 10, 20, 50), left.LayoutBounds);
        Assert.Equal(new Rect(85, 10, 15, 50), right.LayoutBounds);
        Assert.Equal(new Rect(20, 55, 65, 5), bottom.LayoutBounds);
        Assert.Equal(new Rect(20, 10, 65, 45), fill.LayoutBounds);
    }

    [Theory]
    [InlineData(Dock.Left, 0, 0, 20, 60)]
    [InlineData(Dock.Top, 0, 0, 100, 10)]
    [InlineData(Dock.Right, 80, 0, 20, 60)]
    [InlineData(Dock.Bottom, 0, 50, 100, 10)]
    public void LastChildFillCanBeToggledForEveryDockDirection(
        Dock dock, double x, double y, double width, double height)
    {
        var panel = new DockPanel { LastChildFill = false };
        var child = new TestNode { CoreDesiredSize = new Size(20, 10) };
        DockPanel.SetDock(child, dock);
        panel.Children.Add(child);
        ValidateLayout(panel);

        Assert.Equal(new Rect(x, y, width, height), child.LayoutBounds);
        var desiredSize = panel.DesiredSize;

        panel.LastChildFill = true;
        Assert.True(panel.IsMeasureValid);
        panel.Arrange(new Rect(0, 0, 100, 60));

        Assert.Equal(new Rect(0, 0, 100, 60), child.LayoutBounds);
        Assert.Equal(desiredSize, panel.DesiredSize);
    }

    [Theory]
    [InlineData(Dock.Left)]
    [InlineData(Dock.Top)]
    [InlineData(Dock.Right)]
    [InlineData(Dock.Bottom)]
    public void FilledChildNaturalSizeContributesRegardlessOfItsDock(Dock dock)
    {
        var panel = new DockPanel();
        var edge = new TestNode { CoreDesiredSize = new Size(20, 30) };
        var fill = new TestNode { CoreDesiredSize = new Size(40, 10) };
        DockPanel.SetDock(fill, dock);
        panel.Children.AddRange([edge, fill]);

        ValidateLayout(panel);

        Assert.Equal(new Size(60, 30), panel.DesiredSize);
        Assert.Equal(new Rect(20, 0, 80, 60), fill.LayoutBounds);
    }

    [Fact]
    public void ReorderingChildrenChangesDockingOrderAndFillOwnership()
    {
        var panel = new DockPanel();
        var left = new TestNode { CoreDesiredSize = new Size(20, 30) };
        var top = new TestNode { CoreDesiredSize = new Size(80, 10) };
        var fill = new TestNode();
        DockPanel.SetDock(top, Dock.Top);
        panel.Children.AddRange([left, top, fill]);
        ValidateLayout(panel);

        Assert.Equal(new Rect(20, 0, 80, 10), top.LayoutBounds);
        Assert.Equal(new Size(100, 30), panel.DesiredSize);

        panel.Children.Move(1, 0);
        Assert.False(panel.IsMeasureValid);
        ValidateLayout(panel);

        Assert.Equal(new Rect(0, 0, 100, 10), top.LayoutBounds);
        Assert.Equal(new Rect(0, 10, 20, 50), left.LayoutBounds);
        Assert.Equal(new Size(80, 40), panel.DesiredSize);

        panel.Children.Move(2, 0);
        ValidateLayout(panel);

        Assert.Equal(new Rect(0, 0, 0, 60), fill.LayoutBounds);
        Assert.Equal(new Rect(0, 10, 100, 50), left.LayoutBounds);
    }

    [Fact]
    public void HiddenConsumesSpaceAndCollapsedLastChildDoesNotTransferFill()
    {
        var panel = new DockPanel();
        var hidden = new TestNode
        {
            CoreDesiredSize = new Size(20, 10),
            Visibility = Visibility.Hidden
        };
        var collapsed = new TestNode
        {
            CoreDesiredSize = new Size(200, 100),
            Visibility = Visibility.Collapsed,
            Margin = new Thickness(10)
        };
        panel.Children.AddRange([hidden, collapsed]);

        ValidateLayout(panel);

        Assert.Equal(new Size(20, 10), panel.DesiredSize);
        Assert.Equal(new Rect(0, 0, 20, 60), hidden.LayoutBounds);
        Assert.Equal(Rect.Zero, collapsed.LayoutBounds);
        Assert.True(collapsed.IsMeasureValid);
        Assert.True(collapsed.IsArrangeValid);
        Assert.Equal(0, collapsed.MeasureCount);
        Assert.Equal(0, collapsed.ArrangeCount);

        collapsed.Visibility = Visibility.Visible;
        ValidateLayout(panel);

        Assert.Equal(new Rect(0, 0, 20, 60), hidden.LayoutBounds);
        Assert.Equal(new Rect(30, 10, 60, 40), collapsed.LayoutBounds);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EmptyAndAllCollapsedPanelsHaveZeroContentRequirement(bool addChildren)
    {
        var panel = new DockPanel();
        if (addChildren)
        {
            panel.Children.AddRange([
                new TestNode { CoreDesiredSize = new Size(50, 20), Visibility = Visibility.Collapsed },
                new TestNode { CoreDesiredSize = new Size(30, 10), Visibility = Visibility.Collapsed }
            ]);
        }

        ValidateLayout(panel);

        Assert.Equal(Size.Zero, panel.DesiredSize);
        Assert.True(panel.IsArrangeValid);
        Assert.All(panel.Children, child => Assert.Equal(Rect.Zero, child.LayoutBounds));
    }

    [Fact]
    public void CollapsedMiddleChildDoesNotConsumeSpaceBeforeLaterDocking()
    {
        var panel = new DockPanel();
        var left = new TestNode { CoreDesiredSize = new Size(20, 10) };
        var collapsed = new TestNode
        {
            CoreDesiredSize = new Size(200, 100),
            Margin = new Thickness(10),
            Visibility = Visibility.Collapsed
        };
        var top = new TestNode { CoreDesiredSize = new Size(70, 15) };
        var fill = new TestNode { CoreDesiredSize = new Size(5, 5) };
        DockPanel.SetDock(collapsed, Dock.Right);
        DockPanel.SetDock(top, Dock.Top);
        panel.Children.AddRange([left, collapsed, top, fill]);

        ValidateLayout(panel);

        Assert.Equal(new Size(80, 60), top.LastMeasureConstraint);
        Assert.Equal(new Size(80, 45), fill.LastMeasureConstraint);
        Assert.Equal(new Size(90, 20), panel.DesiredSize);
        Assert.Equal(Rect.Zero, collapsed.LayoutBounds);
        Assert.Equal(new Rect(20, 0, 80, 15), top.LayoutBounds);
        Assert.Equal(new Rect(20, 15, 80, 45), fill.LayoutBounds);
    }

    [Fact]
    public void HiddenLastChildStillReceivesTheRemainingArea()
    {
        var panel = new DockPanel();
        var left = new TestNode { CoreDesiredSize = new Size(20, 10) };
        var hiddenFill = new TestNode
        {
            CoreDesiredSize = new Size(5, 5),
            Visibility = Visibility.Hidden
        };
        DockPanel.SetDock(hiddenFill, Dock.Bottom);
        panel.Children.AddRange([left, hiddenFill]);

        ValidateLayout(panel);

        Assert.Equal(new Size(25, 10), panel.DesiredSize);
        Assert.Equal(new Rect(20, 0, 80, 60), hiddenFill.LayoutBounds);
        Assert.Equal(1, hiddenFill.MeasureCount);
        Assert.Equal(1, hiddenFill.ArrangeCount);
    }

    [Fact]
    public void BorderPaddingMarginAndFillAlignmentUseTheSharedBoxModel()
    {
        var panel = new DockPanel
        {
            BorderThickness = new Thickness(1, 2, 3, 4),
            Padding = new Thickness(5, 6, 7, 8)
        };
        var left = new TestNode
        {
            CoreDesiredSize = new Size(20, 10),
            Margin = new Thickness(2, 3, 4, 5)
        };
        var fill = new TestNode
        {
            CoreDesiredSize = new Size(10, 10),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom
        };
        panel.Children.AddRange([left, fill]);

        ValidateLayout(panel);

        Assert.Equal(new Size(78, 32), left.LastMeasureConstraint);
        Assert.Equal(new Size(58, 40), fill.LastMeasureConstraint);
        Assert.Equal(new Size(52, 38), panel.DesiredSize);
        Assert.Equal(new Rect(6, 8, 84, 40), panel.ContentBounds);
        Assert.Equal(new Rect(8, 11, 20, 32), left.LayoutBounds);
        Assert.Equal(new Rect(80, 38, 10, 10), fill.LayoutBounds);
    }

    [Fact]
    public void ExhaustedSpaceGivesLaterChildrenNonNegativeConstraintsAndSlots()
    {
        var panel = new DockPanel();
        var left = new TestNode { CoreDesiredSize = new Size(200, 20) };
        var top = new TestNode { CoreDesiredSize = new Size(10, 80) };
        var fill = new TestNode { CoreDesiredSize = new Size(10, 10) };
        DockPanel.SetDock(top, Dock.Top);
        panel.Children.AddRange([left, top, fill]);

        ValidateLayout(panel);

        Assert.Equal(new Size(0, 60), top.LastMeasureConstraint);
        Assert.Equal(Size.Zero, fill.LastMeasureConstraint);
        Assert.Equal(new Size(210, 90), panel.DesiredSize);
        Assert.Equal(new Rect(0, 0, 100, 60), left.LayoutBounds);
        Assert.Equal(new Rect(100, 0, 0, 60), top.LayoutBounds);
        Assert.Equal(new Rect(100, 60, 0, 0), fill.LayoutBounds);
    }

    [Theory]
    [InlineData(double.PositiveInfinity, 60)]
    [InlineData(100, double.PositiveInfinity)]
    [InlineData(double.PositiveInfinity, double.PositiveInfinity)]
    public void InfiniteAxesKeepUnboundedConstraintsAndFiniteDesiredSize(double width, double height)
    {
        var panel = new DockPanel();
        var left = new TestNode { CoreDesiredSize = new Size(20, 30) };
        var top = new TestNode { CoreDesiredSize = new Size(40, 5) };
        var fill = new TestNode { CoreDesiredSize = new Size(10, 10) };
        DockPanel.SetDock(top, Dock.Top);
        panel.Children.AddRange([left, top, fill]);

        panel.Measure(new Size(width, height));

        Assert.Equal(new Size(width, height), left.LastMeasureConstraint);
        Assert.Equal(new Size(width - 20, height), top.LastMeasureConstraint);
        Assert.Equal(new Size(width - 20, height - 5), fill.LastMeasureConstraint);
        Assert.Equal(new Size(60, 30), panel.DesiredSize);
    }

    [Fact]
    public void DockAndFillChangesInvalidateTheRequiredLayoutPhasesAndAncestors()
    {
        var root = new Panel();
        var panel = new DockPanel();
        var child = new TestNode { CoreDesiredSize = new Size(20, 10) };
        panel.Children.Add(child);
        root.Children.Add(panel);
        ValidateLayout(root);

        DockPanel.SetDock(child, Dock.Top);

        Assert.False(child.IsMeasureValid);
        Assert.False(panel.IsMeasureValid);
        Assert.False(root.IsMeasureValid);
        Assert.False(root.IsArrangeValid);
        ValidateLayout(root);

        child.ClearValue(DockPanel.DockProperty);
        Assert.Equal(Dock.Left, DockPanel.GetDock(child));
        Assert.False(root.IsMeasureValid);
        ValidateLayout(root);

        panel.LastChildFill = false;

        Assert.True(child.IsMeasureValid);
        Assert.True(panel.IsMeasureValid);
        Assert.True(root.IsMeasureValid);
        Assert.False(panel.IsArrangeValid);
        Assert.False(root.IsArrangeValid);
        root.Arrange(new Rect(0, 0, 100, 60));
        Assert.Equal(new Rect(0, 0, 20, 60), child.LayoutBounds);
    }

    [Fact]
    public void BoundDockChangesInvalidateLayoutAndChangeTheAllocatedEdge()
    {
        var panel = new DockPanel { LastChildFill = false };
        var child = new TestNode { CoreDesiredSize = new Size(20, 10) };
        var source = new DockSource();
        child.Bind(DockPanel.DockProperty, source, DockSource.DirectionProperty);
        panel.Children.Add(child);
        ValidateLayout(panel);

        source.Direction = Dock.Bottom;

        Assert.Equal(Dock.Bottom, DockPanel.GetDock(child));
        Assert.False(panel.IsMeasureValid);
        ValidateLayout(panel);
        Assert.Equal(new Rect(0, 50, 100, 10), child.LayoutBounds);
    }

    [Theory]
    [InlineData(Dock.Left)]
    [InlineData(Dock.Top)]
    public void MeasurementOverflowDoesNotCommitAValidPanel(Dock dock)
    {
        var panel = new DockPanel();
        var size = dock == Dock.Left ? new Size(double.MaxValue, 1) : new Size(1, double.MaxValue);
        var first = new TestNode { CoreDesiredSize = size };
        var second = new TestNode { CoreDesiredSize = size };
        DockPanel.SetDock(first, dock);
        DockPanel.SetDock(second, dock);
        panel.Children.AddRange([first, second]);

        Assert.Throws<InvalidOperationException>(() => panel.Measure(Size.Infinite));
        Assert.False(panel.IsMeasureValid);
        Assert.False(panel.IsArrangeValid);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ChildCollectionMutationDuringLayoutFailsBeforeTheNextChild(bool duringArrange)
    {
        var panel = new DockPanel();
        var first = new TestNode();
        var second = new TestNode();
        panel.Children.AddRange([first, second]);
        if (duringArrange)
        {
            panel.Measure(new Size(100, 60));
            first.ArrangeAction = () => panel.Children.Remove(second);

            Assert.Throws<InvalidOperationException>(() => panel.Arrange(new Rect(0, 0, 100, 60)));
            Assert.Equal(0, second.ArrangeCount);
        }
        else
        {
            first.MeasureAction = () => panel.Children.Remove(second);

            Assert.Throws<InvalidOperationException>(() => panel.Measure(new Size(100, 60)));
            Assert.Equal(0, second.MeasureCount);
        }

        Assert.False(panel.IsMeasureValid);
        Assert.False(panel.IsArrangeValid);
    }

    [Theory]
    [InlineData(Dock.Left, true)]
    [InlineData(Dock.Right, true)]
    [InlineData(Dock.Left, false)]
    [InlineData(Dock.Right, false)]
    public void DockThicknessUsesChildDesiredSizeWithScreenLayoutRounding(Dock dock, bool rounding)
    {
        var panel = new DockPanel();
        var screen = new UiScreen(panel) { Scale = 1.25, UseLayoutRounding = rounding };
        var edge = new TestNode { CoreDesiredSize = new Size(10.1, 8.1) };
        var fill = new TestNode();
        DockPanel.SetDock(edge, dock);
        panel.Children.AddRange([edge, fill]);

        ValidateLayout(panel);

        var thickness = rounding ? 10.4 : 10.1;
        Assert.Equal(thickness, edge.LayoutBounds.Width, 10);
        Assert.Equal(100 - thickness, fill.LayoutBounds.Width, 10);
        Assert.Equal(dock == Dock.Left ? 0 : 100 - thickness, edge.LayoutBounds.X, 10);
        Assert.Equal(dock == Dock.Left ? thickness : 0, fill.LayoutBounds.X, 10);
        Assert.Same(screen, panel.Screen);
    }

    private static void ValidateLayout(UiNode node)
    {
        node.Measure(new Size(100, 60));
        node.Arrange(new Rect(0, 0, 100, 60));
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

    private sealed class DockSource : ObservableObject
    {
        internal static readonly Property<Dock> DirectionProperty =
            Property.Register<DockSource, Dock>(nameof(Direction), Dock.Left);

        internal Dock Direction
        {
            get => GetValue(DirectionProperty);
            set => SetValue(DirectionProperty, value);
        }
    }
}
