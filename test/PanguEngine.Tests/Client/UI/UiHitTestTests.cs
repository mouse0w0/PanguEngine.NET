using System.Runtime.ExceptionServices;
using PanguEngine.Client.UI;

namespace PanguEngine.Tests.Client.UI;

public sealed class UiHitTestTests
{
    [Fact]
    public void PointRequiresFiniteCoordinatesAndUsesValueEquality()
    {
        Assert.Equal(Point.Zero, new Point(0, 0));
        Assert.Equal(new Point(-2, 3), new Point(-2, 3));
        Assert.NotEqual(new Point(-2, 3), new Point(3, -2));

        Assert.Throws<ArgumentOutOfRangeException>(() => new Point(double.NaN, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Point(0, double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Point(double.NegativeInfinity, 0));
    }

    [Fact]
    public void InputPropertiesExposeExpectedDefaultsOwnersTargetsAndInvalidation()
    {
        Assert.Equal(typeof(UiNode), UiNode.FocusableProperty.OwnerType);
        Assert.Equal(typeof(UiNode), UiNode.FocusableProperty.TargetType);
        Assert.False(UiNode.FocusableProperty.DefaultValue);
        Assert.Equal(UiPropertyInvalidation.Input, UiNode.FocusableProperty.Invalidation);

        Assert.Equal(typeof(UiNode), UiNode.IsHitTestVisibleProperty.OwnerType);
        Assert.Equal(typeof(UiNode), UiNode.IsHitTestVisibleProperty.TargetType);
        Assert.True(UiNode.IsHitTestVisibleProperty.DefaultValue);
        Assert.Equal(UiPropertyInvalidation.Input, UiNode.IsHitTestVisibleProperty.Invalidation);

        Assert.Equal(typeof(Parent), Parent.ClipToBoundsProperty.OwnerType);
        Assert.Equal(typeof(Parent), Parent.ClipToBoundsProperty.TargetType);
        Assert.False(Parent.ClipToBoundsProperty.DefaultValue);
        Assert.Equal(
            UiPropertyInvalidation.Input | UiPropertyInvalidation.Render,
            Parent.ClipToBoundsProperty.Invalidation);
    }

    [Fact]
    public void ContainsRequiresValidArrangeAndUsesHalfOpenLocalBounds()
    {
        var node = new TestNode();

        Assert.False(node.Contains(new Point(0, 0)));

        Arrange(node, new Rect(10, 20, 30, 40));

        Assert.True(node.Contains(new Point(0, 0)));
        Assert.True(node.Contains(new Point(29.999, 39.999)));
        Assert.False(node.Contains(new Point(30, 10)));
        Assert.False(node.Contains(new Point(10, 40)));
        Assert.False(node.Contains(new Point(-0.001, 1)));
    }

    [Fact]
    public void ContainsCoreOverridesNodeGeometryForContainsAndHitTest()
    {
        var node = new TestNode
        {
            ContainsAction = point => point.X == point.Y
        };
        Arrange(node, new Rect(0, 0, 20, 20));

        Assert.True(node.Contains(new Point(5, 5)));
        Assert.False(node.Contains(new Point(5, 6)));
        Assert.Same(node, node.HitTest(new Point(5, 5)));
        Assert.Null(node.HitTest(new Point(5, 6)));
    }

    [Fact]
    public void HitTestReturnsDeepestNodeUsingChildLocalCoordinates()
    {
        var root = new TestParent();
        var child = new TestParent();
        var leaf = new TestNode();
        root.Width = 100;
        root.Height = 100;
        child.Width = 40;
        child.Height = 40;
        leaf.Width = 20;
        leaf.Height = 20;
        root.Add(child);
        child.Add(leaf);

        Arrange(root, new Rect(0, 0, 100, 100));
        Arrange(child, new Rect(20, 30, 40, 40));
        Arrange(leaf, new Rect(5, 6, 20, 20));

        Assert.Same(leaf, root.HitTest(new Point(26, 37)));
        Assert.Same(child, root.HitTest(new Point(55, 65)));
        Assert.Same(root, root.HitTest(new Point(1, 1)));
    }

    [Fact]
    public void HitTestUsesReverseDrawingOrderAndTracksMoveCommands()
    {
        var root = new TestParent { Width = 100, Height = 100 };
        var first = new TestNode { Width = 20, Height = 20 };
        var second = new TestNode { Width = 20, Height = 20 };
        root.Add(first);
        root.Add(second);
        Arrange(root, new Rect(0, 0, 100, 100));
        Arrange(first, new Rect(10, 10, 20, 20));
        Arrange(second, new Rect(10, 10, 20, 20));

        Assert.Same(second, root.HitTest(new Point(15, 15)));

        second.MoveToBack();

        Assert.Same(first, root.HitTest(new Point(15, 15)));
    }

    [Theory]
    [InlineData(Visibility.Hidden, true)]
    [InlineData(Visibility.Collapsed, true)]
    [InlineData(Visibility.Visible, false)]
    public void HiddenCollapsedAndHitTestInvisibleNodesPruneTheirSubtrees(
        Visibility visibility,
        bool isHitTestVisible)
    {
        var root = new TestParent { Width = 100, Height = 100 };
        var child = new TestParent
        {
            Width = 20,
            Height = 20,
            Visibility = visibility,
            IsHitTestVisible = isHitTestVisible
        };
        var leaf = new TestNode { Width = 10, Height = 10 };
        root.Add(child);
        child.Add(leaf);
        Arrange(root, new Rect(0, 0, 100, 100));
        Arrange(child, new Rect(10, 10, 20, 20));
        Arrange(leaf, new Rect(0, 0, 10, 10));

        Assert.Same(root, root.HitTest(new Point(15, 15)));
    }

    [Fact]
    public void ClipToBoundsOnlyClipsDescendantsAndDefaultsToAllowOverflow()
    {
        var root = new TestParent { Width = 100, Height = 100 };
        var child = new TestNode { Width = 20, Height = 20 };
        root.Add(child);
        Arrange(root, new Rect(0, 0, 100, 100));
        Arrange(child, new Rect(100, 0, 20, 20));

        Assert.Same(child, root.HitTest(new Point(105, 5)));
        Assert.Same(root, root.HitTest(new Point(5, 5)));

        root.ClipToBounds = true;

        Assert.Null(root.HitTest(new Point(105, 5)));
        Assert.Same(root, root.HitTest(new Point(5, 5)));
    }

    [Fact]
    public void ScreenHitTestConvertsFromScreenToRootLocalCoordinatesWhileInactive()
    {
        var root = new TestNode { Width = 20, Height = 20 };
        var screen = new UiScreen(root);
        Arrange(root, new Rect(10, 20, 20, 20));

        Assert.Same(root, screen.HitTest(new Point(10, 20)));
        Assert.Same(root, screen.HitTest(new Point(29.999, 39.999)));
        Assert.Null(screen.HitTest(new Point(30, 40)));
    }

    [Fact]
    public void CoordinateConversionUsesOwnedScreenAndAllCommittedLayoutOffsets()
    {
        var manager = new UiManager();
        var root = new TestParent();
        var branch = new TestParent();
        var leaf = new TestNode();
        root.Add(branch);
        branch.Add(leaf);
        var screen = new UiScreen(root);

        Assert.Same(screen, leaf.Screen);

        manager.Open(screen);
        Arrange(root, new Rect(10, 20, 100, 100));
        Arrange(branch, new Rect(3, 4, 50, 50));
        Arrange(leaf, new Rect(5, 6, 20, 20));

        Assert.Same(screen, leaf.Screen);
        Assert.Equal(new Point(1, 2), leaf.ScreenToLocal(new Point(19, 32)));
        Assert.Equal(new Point(19, 32), leaf.LocalToScreen(new Point(1, 2)));

        leaf.InvalidateArrange();
        Assert.Equal(new Point(1, 2), leaf.ScreenToLocal(new Point(19, 32)));

        manager.Close();

        Assert.Same(screen, leaf.Screen);
        Assert.Equal(new Point(1, 2), leaf.ScreenToLocal(new Point(19, 32)));
        Assert.Equal(new Point(19, 32), leaf.LocalToScreen(new Point(1, 2)));
    }

    [Fact]
    public void CoordinateConversionUsesOpenUiScreenOwnerThread()
    {
        var root = new TestNode();
        var screen = new UiScreen(root);
        screen.Open();

        var screenToLocalError = RunOnBackgroundThread(() =>
            Record.Exception(() => root.ScreenToLocal(Point.Zero)));
        var localToScreenError = RunOnBackgroundThread(() =>
            Record.Exception(() => root.LocalToScreen(Point.Zero)));

        Assert.IsType<InvalidOperationException>(screenToLocalError);
        Assert.IsType<InvalidOperationException>(localToScreenError);
        Assert.Equal(Point.Zero, root.ScreenToLocal(Point.Zero));
        Assert.Equal(Point.Zero, root.LocalToScreen(Point.Zero));

        screen.Close();

        Assert.Null(RunOnBackgroundThread(() => Record.Exception(() => root.ScreenToLocal(Point.Zero))));
        Assert.Null(RunOnBackgroundThread(() => Record.Exception(() => root.LocalToScreen(Point.Zero))));
    }

    [Fact]
    public void CoordinateConversionRejectsNonFiniteAccumulation()
    {
        var manager = new UiManager();
        var root = new TestParent();
        var leaf = new TestNode();
        root.Add(leaf);
        manager.Open(new UiScreen(root));
        Arrange(root, new Rect(double.MaxValue, 0, 1, 1));
        Arrange(leaf, new Rect(double.MaxValue, 0, 1, 1));

        Assert.Throws<ArgumentOutOfRangeException>(() => leaf.LocalToScreen(Point.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            leaf.ScreenToLocal(new Point(-double.MaxValue, 0)));
    }

    [Fact]
    public void ActiveContainsAndHitTestRequireOwnerThread()
    {
        var root = new TestNode { Width = 20, Height = 20 };
        Arrange(root, new Rect(0, 0, 20, 20));
        var screen = new UiScreen(root);
        screen.Open();

        var error = RunOnBackgroundThread(() => Record.Exception(() => root.Contains(new Point(1, 1))));
        var hitTestError = RunOnBackgroundThread(() => Record.Exception(() => root.HitTest(new Point(1, 1))));

        Assert.IsType<InvalidOperationException>(error);
        Assert.IsType<InvalidOperationException>(hitTestError);
        screen.Close();
    }

    [Fact]
    public void InactiveArrangedTreeAllowsBackgroundHitTesting()
    {
        var root = new TestNode { Width = 20, Height = 20 };
        Arrange(root, new Rect(0, 0, 20, 20));

        var result = RunOnBackgroundThread(() => root.HitTest(new Point(1, 1)));

        Assert.Same(root, result);
    }

    private static void Arrange(UiNode node, Rect bounds)
    {
        node.Measure(new Size(bounds.Width, bounds.Height));
        node.Arrange(bounds);
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

    private sealed class TestNode : UiNode
    {
        internal Func<Point, bool>? ContainsAction { get; init; }

        protected override bool ContainsCore(Point localPoint) =>
            ContainsAction?.Invoke(localPoint) ?? base.ContainsCore(localPoint);
    }

    private sealed class TestParent : Parent
    {
        internal void Add(UiNode child) => AddChild(child);
    }
}
