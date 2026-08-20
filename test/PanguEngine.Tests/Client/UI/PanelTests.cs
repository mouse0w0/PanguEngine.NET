using System.Runtime.ExceptionServices;
using PanguEngine.Client.UI;

namespace PanguEngine.Tests.Client.UI;

public sealed class PanelTests
{
    [Fact]
    public void ConcretePanelDesiredSizeUsesPerAxisMaximum()
    {
        var panel = new Panel
        {
            Padding = new Thickness(10, 20, 30, 40)
        };
        panel.Children.Add(new TestNode { Width = 12, Height = 30 });
        panel.Children.Add(new TestNode { Width = 25, Height = 8 });

        panel.Measure(new Size(100, 120));

        Assert.Equal(new Size(65, 90), panel.DesiredSize);
    }

    [Fact]
    public void ConcretePanelChildrenShareContentSlotAndRespectAlignment()
    {
        var panel = new Panel
        {
            Padding = new Thickness(10, 20, 30, 40)
        };
        var stretched = new TestNode();
        var centered = new TestNode
        {
            Width = 20,
            Height = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        panel.Children.Add(stretched);
        panel.Children.Add(centered);

        panel.Measure(new Size(200, 120));
        panel.Arrange(new Rect(0, 0, 200, 120));

        Assert.Equal(new Rect(10, 20, 160, 60), stretched.LayoutBounds);
        Assert.Equal(new Rect(80, 45, 20, 10), centered.LayoutBounds);
    }

    [Fact]
    public void ChildrenExposesStableMutableAndReadOnlyViews()
    {
        var panel = new TestPanel();
        var mutable = panel.Children;
        var readOnly = ((Parent)panel).Children;
        var first = new TestNode();
        var second = new TestNode();

        mutable.Add(first);
        mutable.Insert(0, second);

        Assert.Same(mutable, panel.Children);
        Assert.Same(readOnly, ((Parent)panel).Children);
        Assert.Equal(new UiNode[] { second, first }, mutable);
        Assert.Equal(new UiNode[] { second, first }, readOnly);
        Assert.False(mutable.IsReadOnly);
        Assert.True(mutable.Contains(first));
        Assert.Equal(0, mutable.IndexOf(second));
        Assert.False(mutable.Contains(null));
        Assert.Equal(-1, mutable.IndexOf(null));

        var copy = new UiNode[3];
        mutable.CopyTo(copy, 1);
        Assert.Null(copy[0]);
        Assert.Same(second, copy[1]);
        Assert.Same(first, copy[2]);
    }

    [Fact]
    public void RemoveRemoveAtAndClearReleaseChildren()
    {
        var panel = new TestPanel();
        var first = new TestNode();
        var second = new TestNode();
        var third = new TestNode();
        panel.Children.Add(first);
        panel.Children.Add(second);
        panel.Children.Add(third);

        Assert.True(panel.Children.Remove(first));
        panel.Children.RemoveAt(1);

        Assert.Equal(new UiNode[] { second }, panel.Children);
        Assert.Null(first.Parent);
        Assert.Null(third.Parent);
        Assert.False(panel.Children.Remove(first));

        panel.Children.Clear();
        panel.Children.Clear();

        Assert.Empty(panel.Children);
        Assert.Null(second.Parent);
    }

    [Fact]
    public void CollectionRejectsInvalidArgumentsWithoutChangingChildren()
    {
        var panel = new TestPanel();
        var child = new TestNode();
        panel.Children.Add(child);

        Assert.Throws<ArgumentNullException>(() => panel.Children.Add(null!));
        Assert.Throws<ArgumentNullException>(() => panel.Children.Insert(0, null!));
        Assert.Throws<ArgumentNullException>(() => panel.Children.Remove(null!));
        Assert.Throws<ArgumentNullException>(() => panel.Children[0] = null!);
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = panel.Children[-1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => panel.Children.Insert(2, new TestNode()));
        Assert.Throws<ArgumentOutOfRangeException>(() => panel.Children.RemoveAt(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => panel.Children.Move(0, 1));
        Assert.Throws<ArgumentNullException>(() => panel.Children.CopyTo(null!, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => panel.Children.CopyTo([], -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => panel.Children.CopyTo(new UiNode[1], 2));
        Assert.Throws<ArgumentException>(() => panel.Children.CopyTo([], 0));

        Assert.Equal(new UiNode[] { child }, panel.Children);
        Assert.Same(panel, child.Parent);
    }

    [Fact]
    public void InsertAutomaticallyMovesChildFromAnotherParent()
    {
        var oldParent = new TestPanel();
        var target = new TestPanel();
        var incoming = new TestNode();
        var sibling = new TestNode();
        oldParent.Children.Add(incoming);
        target.Children.Add(sibling);

        target.Children.Insert(0, incoming);

        Assert.Empty(oldParent.Children);
        Assert.Equal(new UiNode[] { incoming, sibling }, target.Children);
        Assert.Same(target, incoming.Parent);
    }

    [Fact]
    public void InsertRejectsExistingChildInsteadOfMovingIt()
    {
        var panel = new TestPanel();
        var first = new TestNode();
        var second = new TestNode();
        panel.Children.Add(first);
        panel.Children.Add(second);

        Assert.Throws<InvalidOperationException>(() => panel.Children.Insert(0, second));

        Assert.Equal(new UiNode[] { first, second }, panel.Children);
    }

    [Fact]
    public void AddAndInsertRejectCyclesAndActiveRootsWithoutChangingTree()
    {
        var root = new TestPanel();
        var nested = new TestPanel();
        root.Children.Add(nested);
        var activeRoot = new TestNode();
        var activeScreen = new UiScreen(activeRoot);
        activeScreen.Open();

        Assert.Throws<InvalidOperationException>(() => nested.Children.Add(nested));
        Assert.Throws<InvalidOperationException>(() => nested.Children.Insert(0, root));
        Assert.Throws<InvalidOperationException>(() => nested.Children.Add(activeRoot));

        Assert.Empty(nested.Children);
        Assert.Equal(new UiNode[] { nested }, root.Children);
        Assert.Same(root, nested.Parent);
        Assert.Null(activeRoot.Parent);
        Assert.Same(activeScreen, activeRoot.Screen);
        activeScreen.Close();
    }

    [Fact]
    public void IndexReplacementAtomicallyMovesIncomingChildAndDetachesOutgoingChild()
    {
        var oldParent = new TestPanel();
        var target = new TestPanel();
        var incoming = new TestNode();
        var outgoing = new TestNode();
        oldParent.Children.Add(incoming);
        target.Children.Add(outgoing);

        target.Children[0] = incoming;

        Assert.Empty(oldParent.Children);
        Assert.Equal(new UiNode[] { incoming }, target.Children);
        Assert.Same(target, incoming.Parent);
        Assert.Null(outgoing.Parent);
    }

    [Fact]
    public void ActiveIndexReplacementKeepsIncomingMountedAndDeactivatesOutgoing()
    {
        var root = new TestPanel();
        var oldParent = new TestPanel();
        var target = new TestPanel();
        var incoming = new TestNode();
        var outgoing = new TestNode();
        oldParent.Children.Add(incoming);
        target.Children.Add(outgoing);
        root.Children.Add(oldParent);
        root.Children.Add(target);
        var screen = new UiScreen(root);
        screen.Open();

        target.Children[0] = incoming;

        Assert.Empty(oldParent.Children);
        Assert.Equal(new UiNode[] { incoming }, target.Children);
        Assert.Same(target, incoming.Parent);
        Assert.Same(screen, incoming.Screen);
        Assert.Null(outgoing.Parent);
        Assert.Null(outgoing.Screen);
        screen.Close();
    }

    [Fact]
    public void ReplacementCanPromoteIncomingChildFromOutgoingSubtree()
    {
        var target = new TestPanel();
        var outgoing = new TestPanel();
        var incoming = new TestNode();
        outgoing.Children.Add(incoming);
        target.Children.Add(outgoing);
        var screen = new UiScreen(target);
        screen.Open();

        target.Children[0] = incoming;

        Assert.Equal(new UiNode[] { incoming }, target.Children);
        Assert.Same(target, incoming.Parent);
        Assert.Same(screen, incoming.Screen);
        Assert.Empty(outgoing.Children);
        Assert.Null(outgoing.Parent);
        Assert.Null(outgoing.Screen);
        screen.Close();
    }

    [Fact]
    public void ReplacementAcrossScreensAdoptsTheTargetScreen()
    {
        var oldRoot = new TestPanel();
        var target = new TestPanel();
        var incoming = new TestNode();
        var outgoing = new TestNode();
        oldRoot.Children.Add(incoming);
        target.Children.Add(outgoing);
        var oldScreen = new UiScreen(oldRoot);
        var newScreen = new UiScreen(target);
        oldScreen.Open();
        newScreen.Open();

        target.Children[0] = incoming;

        Assert.Empty(oldRoot.Children);
        Assert.Equal(new UiNode[] { incoming }, target.Children);
        Assert.Same(target, incoming.Parent);
        Assert.Same(newScreen, incoming.Screen);
        Assert.Null(outgoing.Parent);
        Assert.Null(outgoing.Screen);
        newScreen.Close();
        oldScreen.Close();
    }

    [Fact]
    public void IndexReplacementRejectsExistingSiblingWithoutChangingTree()
    {
        var panel = new TestPanel();
        var first = new TestNode();
        var second = new TestNode();
        panel.Children.Add(first);
        panel.Children.Add(second);

        Assert.Throws<InvalidOperationException>(() => panel.Children[0] = second);

        Assert.Equal(new UiNode[] { first, second }, panel.Children);
        Assert.Same(panel, first.Parent);
        Assert.Same(panel, second.Parent);
    }

    [Fact]
    public void IndexReplacementRejectsCycleAndActiveRootWithoutChangingTree()
    {
        var root = new TestPanel();
        var nested = new TestPanel();
        var outgoing = new TestNode();
        root.Children.Add(nested);
        nested.Children.Add(outgoing);
        var activeRoot = new TestNode();
        var activeScreen = new UiScreen(activeRoot);
        activeScreen.Open();

        Assert.Throws<InvalidOperationException>(() => nested.Children[0] = root);
        Assert.Throws<InvalidOperationException>(() => nested.Children[0] = activeRoot);

        Assert.Equal(new UiNode[] { outgoing }, nested.Children);
        Assert.Same(nested, outgoing.Parent);
        Assert.Null(activeRoot.Parent);
        Assert.Same(activeScreen, activeRoot.Screen);
        activeScreen.Close();
    }

    [Fact]
    public void SameChildReplacementIsANoOp()
    {
        var panel = new TestPanel();
        var child = new TestNode();
        panel.Children.Add(child);
        var screen = new UiScreen(panel);
        ValidateLayout(panel);
        screen.Open();
        var enumerator = panel.Children.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        var error = RunOnBackgroundThread(() =>
            Record.Exception(() => panel.Children[0] = child));

        Assert.Null(error);
        Assert.False(enumerator.MoveNext());
        Assert.True(panel.IsMeasureValid);
        Assert.True(panel.IsArrangeValid);
        Assert.Same(screen, child.Screen);
        screen.Close();
    }

    [Fact]
    public void MoveUsesFinalIndexAndKeepsChildMounted()
    {
        var panel = new TestPanel();
        var first = new TestNode();
        var middle = new TestNode();
        var last = new TestNode();
        panel.Children.Add(first);
        panel.Children.Add(middle);
        panel.Children.Add(last);
        var screen = new UiScreen(panel);
        ValidateLayout(panel);
        screen.Open();

        panel.Children.Move(0, 2);

        Assert.Equal(new UiNode[] { middle, last, first }, panel.Children);
        Assert.Same(panel, first.Parent);
        Assert.Same(screen, first.Screen);
        Assert.False(panel.IsMeasureValid);
        Assert.False(panel.IsArrangeValid);

        panel.Children.Move(2, 0);
        Assert.Equal(new UiNode[] { first, middle, last }, panel.Children);
        screen.Close();
    }

    [Fact]
    public void SameIndexMoveIsANoOpOnWrongThread()
    {
        var panel = new TestPanel();
        panel.Children.Add(new TestNode());
        var screen = new UiScreen(panel);
        ValidateLayout(panel);
        screen.Open();
        var enumerator = panel.Children.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        var error = RunOnBackgroundThread(() =>
            Record.Exception(() => panel.Children.Move(0, 0)));

        Assert.Null(error);
        Assert.False(enumerator.MoveNext());
        Assert.True(panel.IsMeasureValid);
        Assert.True(panel.IsArrangeValid);
        screen.Close();
    }

    [Fact]
    public void MissingRemoveAndEmptyClearAreNoOpsOnWrongThread()
    {
        var panel = new TestPanel();
        var screen = new UiScreen(panel);
        ValidateLayout(panel);
        screen.Open();
        var enumerator = panel.Children.GetEnumerator();
        Assert.False(enumerator.MoveNext());

        var result = RunOnBackgroundThread(() =>
            (Remove: Record.Exception(() =>
                {
                    panel.Children.Remove(new TestNode());
                }),
                Clear: Record.Exception(panel.Children.Clear)));

        Assert.Null(result.Remove);
        Assert.Null(result.Clear);
        Assert.False(enumerator.MoveNext());
        Assert.True(panel.IsMeasureValid);
        Assert.True(panel.IsArrangeValid);
        screen.Close();
    }

    [Fact]
    public void ActiveCollectionRejectsRealChangesOnWrongThread()
    {
        var panel = new TestPanel();
        var first = new TestNode();
        var second = new TestNode();
        panel.Children.Add(first);
        panel.Children.Add(second);
        var screen = new UiScreen(panel);
        screen.Open();

        var result = RunOnBackgroundThread(() =>
            (Add: Record.Exception(() => panel.Children.Add(new TestNode())),
                Replace: Record.Exception(() => panel.Children[0] = new TestNode()),
                Remove: Record.Exception(() => panel.Children.RemoveAt(0)),
                Move: Record.Exception(() => panel.Children.Move(0, 1)),
                Clear: Record.Exception(panel.Children.Clear)));

        Assert.IsType<InvalidOperationException>(result.Add);
        Assert.IsType<InvalidOperationException>(result.Replace);
        Assert.IsType<InvalidOperationException>(result.Remove);
        Assert.IsType<InvalidOperationException>(result.Move);
        Assert.IsType<InvalidOperationException>(result.Clear);
        Assert.Equal(new UiNode[] { first, second }, panel.Children);
        screen.Close();
    }

    [Fact]
    public void InactiveCollectionCanBeChangedOnBackgroundThread()
    {
        var panel = RunOnBackgroundThread(() =>
        {
            var created = new TestPanel();
            var first = new TestNode();
            var second = new TestNode();
            created.Children.Add(first);
            created.Children.Add(second);
            created.Children.Move(1, 0);
            created.Children.RemoveAt(1);
            return created;
        });

        Assert.Single(panel.Children);
        Assert.Null(panel.Screen);
    }

    [Fact]
    public void EnumeratorFailsFastAfterRealStructuralChanges()
    {
        AssertEnumeratorInvalidated(children => children.Add(new TestNode()));
        AssertEnumeratorInvalidated(children => children[0] = new TestNode());
        AssertEnumeratorInvalidated(children => children.Remove(children[0]));
        AssertEnumeratorInvalidated(children => children.RemoveAt(0));
        AssertEnumeratorInvalidated(children => children.Clear());
        AssertEnumeratorInvalidated(children => children.Move(0, 1));
    }

    [Fact]
    public void EnumeratorSurvivesFailedAndNoOpOperations()
    {
        var panel = CreateTwoChildPanel();
        var first = panel.Children[0];
        var enumerator = panel.Children.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        Assert.Throws<InvalidOperationException>(() => panel.Children.Insert(0, first));
        Assert.False(panel.Children.Remove(new TestNode()));
        panel.Children[0] = first;
        panel.Children.Move(0, 0);

        Assert.True(enumerator.MoveNext());
        Assert.False(enumerator.MoveNext());
    }

    private static void AssertEnumeratorInvalidated(Action<UiNodeCollection> change)
    {
        var panel = CreateTwoChildPanel();
        var enumerator = panel.Children.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        change(panel.Children);

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    private static TestPanel CreateTwoChildPanel()
    {
        var panel = new TestPanel();
        panel.Children.Add(new TestNode());
        panel.Children.Add(new TestNode());
        return panel;
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
