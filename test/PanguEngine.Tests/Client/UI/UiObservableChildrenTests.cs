using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Collections;

namespace PanguEngine.Tests.Client.UI;

public sealed class UiObservableChildrenTests
{
    [Fact]
    public void InterfaceWritesAndRangeRemovalMaintainOwnership()
    {
        var panel = new Panel();
        var screen = new UiScreen(panel);
        IList<UiNode> items = panel.Children;
        var a = new Panel();
        var b = new Panel();
        var c = new Panel();
        var changes = new List<ListChangedEventArgs<UiNode>>();
        panel.Children.Changed += (_, change) => changes.Add(change);
        items.Add(a);
        items.Insert(0, b);
        items.Add(c);
        Assert.Same(panel, a.Parent);
        Assert.Same(screen, b.Screen);
        panel.Children.RemoveRange(0, 2);
        Assert.Null(a.Parent);
        Assert.Null(a.Screen);
        Assert.Null(b.Parent);
        Assert.Equal(new UiNode[] { b, a }, changes[^1].OldItems);
        items.RemoveAt(0);
        Assert.Null(c.Parent);
        items.Add(a);
        items.Clear();
        Assert.Null(a.Screen);
        Assert.Equal(7, changes.Count);
    }

    [Fact]
    public void ReparentPublishesDetachedStateBeforeAttachingToTarget()
    {
        var source = new Panel();
        var target = new Panel();
        var child = new Panel();
        source.Children.Add(child);
        var order = new List<string>();
        source.Children.Changed += (_, change) =>
        {
            Assert.Equal(ListChangeKind.Remove, change.Kind);
            Assert.Null(child.Parent);
            Assert.Null(child.Screen);
            Assert.Empty(target.Children);
            Assert.Throws<InvalidOperationException>(() => source.Children.Clear());
            Assert.Throws<InvalidOperationException>(() => target.Children.RemoveAt(-1));
            order.Add("source");
        };
        target.Children.Changed += (_, _) =>
        {
            Assert.Same(target, child.Parent);
            order.Add("target");
        };
        ObservableList<UiNode> list = target.Children;
        list.Add(child);
        Assert.Equal(["source", "target"], order);
    }

    [Fact]
    public void TargetNotificationRunsAfterSourceOperationHasReturned()
    {
        var source = new Panel();
        var target = new Panel();
        var child = new Panel();
        source.Children.Add(child);
        target.Children.Changed += (_, _) =>
        {
            source.Children.Add(new Panel());
            Assert.Single(source.Children);
            Assert.Throws<InvalidOperationException>(() => target.Children.Clear());
        };
        target.Children.Add(child);
        Assert.Same(target, child.Parent);
    }

    [Fact]
    public void RootTransferPublishesRemovalBeforeAssigningRoot()
    {
        var parent = new Panel();
        var child = new Panel();
        parent.Children.Add(child);
        var screen = new UiScreen();
        var notifications = 0;
        parent.Children.Changed += (_, _) =>
        {
            notifications++;
            Assert.Null(child.Parent);
            Assert.Null(child.Screen);
            Assert.Null(screen.Root);
            Assert.Throws<InvalidOperationException>(() => parent.Children.Add(child));
        };
        screen.Root = child;
        Assert.Equal(1, notifications);
        Assert.Same(screen, child.Screen);
        Assert.Same(child, screen.Root);
    }

    [Fact]
    public void ReplaceAllRetainsNodesAndReportsOldAndNewSnapshots()
    {
        var parent = new Panel();
        var a = new Panel();
        var b = new Panel();
        var c = new Panel();
        parent.Children.AddRange([a, b, c]);
        var changes = new List<ListChangedEventArgs<UiNode>>();
        parent.Children.Changed += (_, change) => changes.Add(change);
        parent.Children.ReplaceAll([c, a]);
        Assert.Same(parent, a.Parent);
        Assert.Same(parent, c.Parent);
        Assert.Null(b.Parent);
        Assert.Equal(ListChangeKind.Replace, Assert.Single(changes).Kind);
        Assert.Equal(new UiNode[] { a, b, c }, changes[0].OldItems);
        Assert.Equal(new UiNode[] { c, a }, changes[0].NewItems);
    }

    [Fact]
    public void RootTransferStopsWhenRemovalListenerThrows()
    {
        var parent = new Panel();
        var child = new Panel();
        parent.Children.Add(child);
        var oldRoot = new Panel();
        var screen = new UiScreen(oldRoot);
        var expected = new InvalidOperationException("listener");
        parent.Children.Changed += (_, _) => throw expected;

        Assert.Same(expected, Assert.Throws<InvalidOperationException>(() => screen.Root = child));

        Assert.Null(child.Parent);
        Assert.Null(child.Screen);
        Assert.Empty(parent.Children);
        Assert.Same(oldRoot, screen.Root);
    }

    [Fact]
    public void ReparentDoesNotOverwriteOwnershipAssignedByRemovalListener()
    {
        var source = new Panel();
        var target = new Panel();
        var other = new Panel();
        var child = new Panel();
        source.Children.Add(child);
        source.Children.Changed += (_, _) => other.Children.Add(child);

        Assert.Throws<InvalidOperationException>(() => target.Children.Add(child));

        Assert.Same(other, child.Parent);
        Assert.Single(other.Children);
        Assert.Empty(target.Children);
        Assert.Empty(source.Children);
    }

    [Fact]
    public void ReparentRejectsCycleCreatedByRemovalListener()
    {
        var source = new Panel();
        var target = new Panel();
        var child = new Panel();
        source.Children.Add(child);
        source.Children.Changed += (_, _) => child.Children.Add(target);

        Assert.Throws<InvalidOperationException>(() => target.Children.Add(child));

        Assert.Same(child, target.Parent);
        Assert.Null(child.Parent);
        Assert.Empty(target.Children);
    }

    [Fact]
    public void BatchValidationLeavesEverySourceUnchanged()
    {
        var source = new Panel();
        var target = new Panel();
        var child = new Panel();
        source.Children.Add(child);
        Assert.Throws<InvalidOperationException>(() => target.Children.AddRange([child, child]));
        Assert.Same(source, child.Parent);
        Assert.Equal(new UiNode[] { child }, source.Children);
        Assert.Empty(target.Children);
        target.Children.Add(child);
        Assert.Same(target, child.Parent);
    }

    [Fact]
    public void RelatedCollectionNotifiesSynchronouslyInsideChangeHandler()
    {
        var parent = new Panel();
        var related = new Panel();
        var child = new Panel();
        parent.Children.Add(child);
        var synchronized = false;
        var order = new List<string>();
        parent.Children.Changed += (_, _) =>
        {
            related.Children.Add(new Panel());
            synchronized = true;
        };
        related.Children.Changed += (_, _) =>
        {
            Assert.False(synchronized);
            order.Add("related");
        };
        parent.Children.Changed += (_, _) => order.Add("parent");
        parent.Children.Clear();
        Assert.Equal(["related", "parent"], order);
    }

    [Fact]
    public void ListenerFailureDoesNotRollbackAndReleasesAllScopes()
    {
        var source = new Panel();
        var target = new Panel();
        var child = new Panel();
        source.Children.Add(child);
        EventHandler<ListChangedEventArgs<UiNode>> handler = (_, _) => throw new InvalidOperationException("listener");
        source.Children.Changed += handler;
        Assert.Throws<InvalidOperationException>(() => target.Children.Add(child));
        Assert.Null(child.Parent);
        Assert.Null(child.Screen);
        Assert.Empty(source.Children);
        Assert.Empty(target.Children);
        source.Children.Changed -= handler;
        source.Children.Add(child);
        Assert.Same(source, child.Parent);
    }

    [Fact]
    public void MultiSourceTransferRemovesInInputOrderBeforeBatchInsertion()
    {
        var first = new Panel();
        var second = new Panel();
        var target = new Panel();
        var a = new Panel();
        var b = new Panel();
        var c = new Panel();
        var d = new Panel();
        var e = new Panel();
        first.Children.AddRange([a, b, c, d]);
        second.Children.Add(e);
        var events = new List<(string Source, int Index)>();
        first.Children.Changed += (_, change) =>
        {
            Assert.Empty(target.Children);
            Assert.Null(Assert.Single(change.OldItems).Parent);
            events.Add(("first", change.Index));
        };
        second.Children.Changed += (_, change) => events.Add(("second", change.Index));
        target.Children.Changed += (_, change) => events.Add(("target", change.Index));
        target.Children.AddRange([a, e, c]);
        Assert.Equal(new[] { ("first", 0), ("second", 0), ("first", 1), ("target", 0) }, events);
        Assert.Equal(new UiNode[] { b, d }, first.Children);
        Assert.Equal(new UiNode[] { a, e, c }, target.Children);
    }

    [Fact]
    public void BusySourceStopsTransferAfterEarlierRemovalsHaveCompleted()
    {
        var busy = new Panel();
        var available = new Panel();
        var target = new Panel();
        var a = new Panel();
        var b = new Panel();
        available.Children.Add(a);
        busy.Children.Changed += (_, _) =>
        {
            Assert.Throws<InvalidOperationException>(() => target.Children.AddRange([a, b]));
            available.Children.Add(new Panel());
            Assert.Single(available.Children);
        };
        busy.Children.Add(b);
        Assert.Empty(target.Children);
        Assert.Null(a.Parent);
        Assert.Same(busy, b.Parent);
        available.Children.Clear();
        target.Children.Add(a);
    }

    [Fact]
    public void SameNodeAssignmentAndReplaceAllBothNotifyReplacement()
    {
        var parent = new Panel();
        var child = new Panel();
        parent.Children.Add(child);
        var count = 0;
        parent.Children.Changed += (_, change) =>
        {
            count++;
            Assert.Equal(ListChangeKind.Replace, change.Kind);
            Assert.Same(child, Assert.Single(change.OldItems));
            Assert.Same(child, Assert.Single(change.NewItems));
            Assert.Same(parent, child.Parent);
        };
        parent.Children[0] = child;
        Assert.Equal(1, count);
        parent.Children.ReplaceAll(parent.Children);
        Assert.Equal(2, count);
    }

    [Fact]
    public void ReplaceAllCanReorderRetainedNodesAndTransferNewNodes()
    {
        var parent = new Panel();
        var source = new Panel();
        var a = new Panel();
        var b = new Panel();
        var c = new Panel();
        parent.Children.AddRange([a, b]);
        source.Children.Add(c);
        parent.Children.ReplaceAll([b, c, a]);
        Assert.Equal(new UiNode[] { b, c, a }, parent.Children);
        Assert.Empty(source.Children);
        Assert.Same(parent, c.Parent);
    }

    [Fact]
    public void MoveReverseAndSortPreserveParentsAndNotifyReorder()
    {
        var parent = new Panel();
        var a = new Panel();
        var b = new Panel();
        var c = new Panel();
        parent.Children.AddRange([a, b, c]);
        var changes = new List<ListChangedEventArgs<UiNode>>();
        parent.Children.Changed += (_, change) => changes.Add(change);
        parent.Children.Move(0, 2);
        Assert.Equal(new UiNode[] { b, c, a }, parent.Children);
        parent.Children.Reverse();
        Assert.Equal(new UiNode[] { a, c, b }, parent.Children);
        var positions = new Dictionary<UiNode, int> { [a] = 0, [b] = 1, [c] = 2 };
        parent.Children.Sort((left, right) => positions[left].CompareTo(positions[right]));
        Assert.Equal(new UiNode[] { a, b, c }, parent.Children);
        Assert.Equal(3, changes.Count);
        Assert.All(changes, change => Assert.Equal(ListChangeKind.Reorder, change.Kind));
        Assert.All(parent.Children, child => Assert.Same(parent, child.Parent));
    }

    [Fact]
    public void AncestorAndDescendantCanBeTransferredTogetherAsSiblings()
    {
        var source = new Panel();
        var ancestor = new Panel();
        var descendant = new Panel();
        source.Children.Add(ancestor);
        ancestor.Children.Add(descendant);
        var target = new Panel();
        target.Children.AddRange([ancestor, descendant]);
        Assert.Same(target, ancestor.Parent);
        Assert.Same(target, descendant.Parent);
        Assert.Empty(source.Children);
        Assert.Empty(ancestor.Children);
    }

    [Fact]
    public void ParentViewIsReadOnlyAndBothViewsNotifyWithTheirOwnSenders()
    {
        var panel = new Panel();
        Parent parent = panel;
        var view = parent.ReadOnlyChildren;
        Assert.False(view is ObservableList<UiNode>);
        Assert.False(view is IList<UiNode>);
        var changes = new List<ListChangedEventArgs<UiNode>>();
        var viewChanges = new List<ListChangedEventArgs<UiNode>>();
        panel.Children.Changed += (sender, change) =>
        {
            Assert.Same(panel.Children, sender);
            changes.Add(change);
        };
        view.Changed += (sender, change) =>
        {
            Assert.Same(view, sender);
            viewChanges.Add(change);
        };
        panel.Children.Add(new Panel());
        Assert.Same(Assert.Single(changes), Assert.Single(viewChanges));
        Assert.Same(view, parent.ReadOnlyChildren);
        Assert.Same(view, panel.Children.AsReadOnly());
        Assert.Single(view);
    }

    [Fact]
    public void SubscriptionAddedDuringNotificationReceivesNextChange()
    {
        var parent = new Panel();
        parent.Children.Add(new Panel());
        var count = 0;
        parent.Children.Changed += (_, _) => parent.Children.Changed += (_, _) => count++;
        parent.Children.Clear();
        Assert.Equal(0, count);
        parent.Children.Add(new Panel());
        Assert.Equal(1, count);
    }

    [Fact]
    public void ChangeHandlerCannotModifyItsCollection()
    {
        var parent = new Panel();
        parent.Children.Add(new Panel());
        parent.Children.Changed += (_, _) => Assert.Throws<InvalidOperationException>(() => parent.Children.Remove(null));
        parent.Children.Clear();
        Assert.Empty(parent.Children);
    }

    [Fact]
    public void SequentialRelatedChangesEachNotifyBeforeReturning()
    {
        var parent = new Panel();
        var related = new Panel();
        parent.Children.AddRange([new Panel(), new Panel()]);
        parent.Children.Changed += (_, change) =>
        {
            foreach (var child in change.OldItems)
                related.Children.Add(child);
        };
        var calls = 0;
        related.Children.Changed += (_, _) =>
        {
            calls++;
            Assert.Equal(calls, related.Children.Count);
            Assert.Throws<InvalidOperationException>(() => related.Children.Clear());
        };
        parent.Children.Clear();
        Assert.Equal(2, calls);
    }

    [Fact]
    public void EmptyBatchStillPublishesSuccessfulInputSideEffects()
    {
        var target = new Panel();
        var related = new Panel();
        var calls = 0;
        related.Children.Changed += (_, _) => calls++;
        IEnumerable<UiNode> Input()
        {
            related.Children.Add(new Panel());
            Assert.Equal(1, calls);
            yield break;
        }
        target.Children.AddRange(Input());
        Assert.Empty(target.Children);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void CaughtNestedListenerFailurePreservesChangesAndReleasesScope()
    {
        var parent = new Panel();
        var failing = new Panel();
        parent.Children.Add(new Panel());
        failing.Children.Add(new Panel());
        var calls = 0;
        failing.Children.Changed += (_, _) => calls++;
        EventHandler<ListChangedEventArgs<UiNode>> throwing = (_, _) => throw new InvalidOperationException("listener");
        failing.Children.Changed += throwing;
        parent.Children.Changed += (_, _) => Assert.Throws<InvalidOperationException>(() => failing.Children.Clear());
        parent.Children.Clear();
        Assert.Equal(1, calls);
        Assert.Empty(failing.Children);
        failing.Children.Changed -= throwing;
        failing.Children.Add(new Panel());
        Assert.Equal(2, calls);
    }

    [Fact]
    public void NullableQueriesRemainAvailableThroughInheritedPublicApi()
    {
        var parent = new Panel();
        Assert.False(parent.Children.Contains(null));
        Assert.Equal(-1, parent.Children.IndexOf(null));
        ObservableList<UiNode> list = parent.Children;
        Assert.False(list.Contains(null));
        Assert.Equal(-1, list.IndexOf(null));
    }

    [Fact]
    public void ReorderInvalidatesExistingEnumerator()
    {
        var parent = new Panel();
        parent.Children.AddRange([new Panel(), new Panel()]);
        using var enumerator = parent.Children.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        parent.Children.Reverse();
        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

}
