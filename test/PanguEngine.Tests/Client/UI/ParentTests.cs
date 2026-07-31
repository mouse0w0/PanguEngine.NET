using System.Runtime.ExceptionServices;
using PanguEngine.Client.UI;

namespace PanguEngine.Tests.Client.UI;

public sealed class ParentTests
{
    [Fact]
    public void ChildrenViewIsStableReadOnlyAndTracksChanges()
    {
        var parent = new TestParent();
        var first = new TestNode();
        var second = new TestNode();
        var view = parent.Children;

        parent.Add(first);
        parent.Insert(0, second);

        Assert.Same(view, parent.Children);
        Assert.Equal(new UiNode[] { second, first }, view);
        Assert.Same(parent, first.Parent);
        Assert.Same(parent, second.Parent);
        Assert.IsNotType<List<UiNode>>(view);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<UiNode>)view).Add(new TestNode()));
    }

    [Fact]
    public void RemoveAndClearReleaseDirectChildren()
    {
        var parent = new TestParent();
        var first = new TestNode();
        var second = new TestNode();
        var foreign = new TestNode();
        parent.Add(first);
        parent.Add(second);

        Assert.True(parent.Remove(first));
        Assert.Null(first.Parent);
        Assert.False(parent.Remove(first));
        Assert.False(parent.Remove(foreign));

        parent.Clear();
        parent.Clear();

        Assert.Empty(parent.Children);
        Assert.Null(second.Parent);
        Assert.Throws<ArgumentNullException>(() => parent.Remove(null!));
    }

    [Fact]
    public void InsertAutomaticallyMovesChildFromAnotherParent()
    {
        var oldParent = new TestParent();
        var oldSibling = new TestNode();
        var newParent = new TestParent();
        var newSibling = new TestNode();
        var child = new TestNode();
        oldParent.Add(oldSibling);
        oldParent.Add(child);
        newParent.Add(newSibling);

        newParent.Insert(0, child);

        Assert.Equal(new UiNode[] { oldSibling }, oldParent.Children);
        Assert.Equal(new UiNode[] { child, newSibling }, newParent.Children);
        Assert.Same(newParent, child.Parent);
    }

    [Fact]
    public void DuplicateInsertionFailsWithoutChangingOrder()
    {
        var parent = new TestParent();
        var first = new TestNode();
        var child = new TestNode();
        parent.Add(first);
        parent.Add(child);
        var originalOrder = parent.Children.ToArray();

        Assert.Throws<InvalidOperationException>(() => parent.Add(child));
        Assert.Equal(originalOrder, parent.Children);
        Assert.Same(parent, child.Parent);

        Assert.Throws<InvalidOperationException>(() => parent.Insert(0, child));
        Assert.Equal(originalOrder, parent.Children);
        Assert.Same(parent, child.Parent);
    }

    [Fact]
    public void SelfAndAncestorInsertionFailWithoutChangingTree()
    {
        var root = new TestParent();
        var middle = new TestParent();
        var leaf = new TestParent();
        root.Add(middle);
        middle.Add(leaf);

        Assert.Throws<InvalidOperationException>(() => root.Add(root));
        Assert.Equal(new UiNode[] { middle }, root.Children);
        Assert.Null(root.Parent);

        Assert.Throws<InvalidOperationException>(() => middle.Add(root));
        Assert.Equal(new UiNode[] { leaf }, middle.Children);
        Assert.Same(root, middle.Parent);
        Assert.Null(root.Parent);

        Assert.Throws<InvalidOperationException>(() => leaf.Add(root));
        Assert.Empty(leaf.Children);
        Assert.Same(middle, leaf.Parent);
        Assert.Null(root.Parent);
    }

    [Fact]
    public void InvalidInsertArgumentsFailWithoutChangingTree()
    {
        var parent = new TestParent();
        var existing = new TestNode();
        var child = new TestNode();
        parent.Add(existing);
        var originalOrder = parent.Children.ToArray();

        Assert.Throws<ArgumentNullException>(() => parent.Add(null!));
        Assert.Throws<ArgumentNullException>(() => parent.Insert(0, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => parent.Insert(-1, child));
        Assert.Throws<ArgumentOutOfRangeException>(() => parent.Insert(2, child));

        Assert.Equal(originalOrder, parent.Children);
        Assert.Null(child.Parent);
    }

    [Fact]
    public void MoveCommandsUseVisualFrontAndBackOrder()
    {
        var parent = new TestParent();
        var first = new TestNode();
        var middle = new TestNode();
        var last = new TestNode();
        parent.Add(first);
        parent.Add(middle);
        parent.Add(last);

        middle.MoveToFront();
        Assert.Equal(new UiNode[] { first, last, middle }, parent.Children);
        Assert.Same(parent, middle.Parent);

        middle.MoveToBack();
        Assert.Equal(new UiNode[] { middle, first, last }, parent.Children);
        Assert.Same(parent, middle.Parent);
    }

    [Fact]
    public void MoveCommandsAreNoOpsWithoutParentOrAtTargetPosition()
    {
        var independent = new TestNode();
        independent.MoveToFront();
        independent.MoveToBack();
        Assert.Null(independent.Parent);

        var dispatcher = new UiDispatcher();
        var parent = new TestParent();
        var first = new TestNode();
        var last = new TestNode();
        parent.Add(first);
        parent.Add(last);
        parent.AttachToTree(dispatcher);

        var backgroundResult = RunOnBackgroundThread(() =>
            (BackError: Record.Exception(first.MoveToBack),
                FrontError: Record.Exception(last.MoveToFront)));

        Assert.Null(backgroundResult.BackError);
        Assert.Null(backgroundResult.FrontError);
        Assert.Equal(new UiNode[] { first, last }, parent.Children);
    }

    [Fact]
    public void ActiveTreeMoveCommandsReorderOnDispatcherThread()
    {
        var dispatcher = new UiDispatcher();
        var parent = new TestParent();
        var first = new TestNode();
        var middle = new TestNode();
        var last = new TestNode();
        parent.Add(first);
        parent.Add(middle);
        parent.Add(last);
        parent.AttachToTree(dispatcher);

        middle.MoveToFront();
        Assert.Equal(new UiNode[] { first, last, middle }, parent.Children);

        middle.MoveToBack();
        Assert.Equal(new UiNode[] { middle, first, last }, parent.Children);
        Assert.Same(dispatcher, middle.ActiveDispatcher);
    }

    [Fact]
    public void AttachAndDetachPropagateDispatcherThroughSubtree()
    {
        var dispatcher = new UiDispatcher();
        var root = new TestParent();
        var nested = new TestParent();
        var leaf = new TestNode();
        root.Add(nested);
        nested.Add(leaf);

        root.AttachToTree(dispatcher);

        Assert.Same(dispatcher, root.ActiveDispatcher);
        Assert.Same(dispatcher, nested.ActiveDispatcher);
        Assert.Same(dispatcher, leaf.ActiveDispatcher);

        root.DetachFromTree();
        root.DetachFromTree();

        Assert.Null(root.ActiveDispatcher);
        Assert.Null(nested.ActiveDispatcher);
        Assert.Null(leaf.ActiveDispatcher);
    }

    [Fact]
    public void RootAttachmentRejectsInvalidStateWithoutChangingSubtree()
    {
        var dispatcher = new UiDispatcher();
        var root = new TestParent();
        var child = new TestNode();
        root.Add(child);

        Assert.Throws<ArgumentNullException>(() => root.AttachToTree(null!));
        Assert.Null(root.ActiveDispatcher);
        Assert.Null(child.ActiveDispatcher);

        root.AttachToTree(dispatcher);
        Assert.Throws<InvalidOperationException>(() => root.AttachToTree(dispatcher));
        Assert.Same(dispatcher, root.ActiveDispatcher);
        Assert.Same(dispatcher, child.ActiveDispatcher);
    }

    [Fact]
    public void ChildCannotUseRootAttachmentEntrypoints()
    {
        var dispatcher = new UiDispatcher();
        var parent = new TestParent();
        var child = new TestNode();
        parent.Add(child);

        Assert.Throws<InvalidOperationException>(() => child.AttachToTree(dispatcher));
        Assert.Throws<InvalidOperationException>(child.DetachFromTree);

        Assert.Same(parent, child.Parent);
        Assert.Null(parent.ActiveDispatcher);
        Assert.Null(child.ActiveDispatcher);
    }

    [Fact]
    public void ActiveRootMustBeDetachedBeforeBecomingAChild()
    {
        var dispatcher = new UiDispatcher();
        var activeRoot = new TestParent();
        var target = new TestParent();
        activeRoot.AttachToTree(dispatcher);

        Assert.Throws<InvalidOperationException>(() => target.Add(activeRoot));
        Assert.Null(activeRoot.Parent);
        Assert.Same(dispatcher, activeRoot.ActiveDispatcher);
        Assert.Empty(target.Children);

        activeRoot.DetachFromTree();
        target.Add(activeRoot);

        Assert.Same(target, activeRoot.Parent);
        Assert.Null(activeRoot.ActiveDispatcher);
    }

    [Fact]
    public void ActiveParentActivatesAddedSubtreeAndDeactivatesRemovedSubtree()
    {
        var dispatcher = new UiDispatcher();
        var root = new TestParent();
        root.AttachToTree(dispatcher);
        var nested = new TestParent();
        var leaf = new TestNode();
        nested.Add(leaf);

        root.Add(nested);

        Assert.Same(dispatcher, nested.ActiveDispatcher);
        Assert.Same(dispatcher, leaf.ActiveDispatcher);

        Assert.True(root.Remove(nested));

        Assert.Null(nested.Parent);
        Assert.Null(nested.ActiveDispatcher);
        Assert.Null(leaf.ActiveDispatcher);
    }

    [Fact]
    public void ActiveParentClearDeactivatesEveryRemovedSubtree()
    {
        var dispatcher = new UiDispatcher();
        var root = new TestParent();
        var first = new TestParent();
        var firstLeaf = new TestNode();
        var second = new TestNode();
        first.Add(firstLeaf);
        root.Add(first);
        root.Add(second);
        root.AttachToTree(dispatcher);

        root.Clear();

        Assert.Empty(root.Children);
        Assert.Null(first.Parent);
        Assert.Null(second.Parent);
        Assert.Null(first.ActiveDispatcher);
        Assert.Null(firstLeaf.ActiveDispatcher);
        Assert.Null(second.ActiveDispatcher);
        Assert.Same(dispatcher, root.ActiveDispatcher);
    }

    [Fact]
    public void ReparentWithinSameDispatcherKeepsSubtreeActive()
    {
        var dispatcher = new UiDispatcher();
        var root = new TestParent();
        var oldParent = new TestParent();
        var newParent = new TestParent();
        var child = new TestParent();
        var leaf = new TestNode();
        child.Add(leaf);
        oldParent.Add(child);
        root.Add(oldParent);
        root.Add(newParent);
        root.AttachToTree(dispatcher);

        newParent.Add(child);

        Assert.Empty(oldParent.Children);
        Assert.Equal(new UiNode[] { child }, newParent.Children);
        Assert.Same(newParent, child.Parent);
        Assert.Same(dispatcher, child.ActiveDispatcher);
        Assert.Same(dispatcher, leaf.ActiveDispatcher);
    }

    [Fact]
    public void ReparentAcrossSameThreadDispatchersChangesSubtreeDispatcher()
    {
        var oldDispatcher = new UiDispatcher();
        var newDispatcher = new UiDispatcher();
        var oldRoot = new TestParent();
        var newRoot = new TestParent();
        var child = new TestParent();
        var leaf = new TestNode();
        child.Add(leaf);
        oldRoot.Add(child);
        oldRoot.AttachToTree(oldDispatcher);
        newRoot.AttachToTree(newDispatcher);

        newRoot.Add(child);

        Assert.Empty(oldRoot.Children);
        Assert.Equal(new UiNode[] { child }, newRoot.Children);
        Assert.Same(newRoot, child.Parent);
        Assert.Same(newDispatcher, child.ActiveDispatcher);
        Assert.Same(newDispatcher, leaf.ActiveDispatcher);
    }

    [Fact]
    public void ReparentBetweenActiveAndInactiveTreesTransitionsSubtreeState()
    {
        var dispatcher = new UiDispatcher();
        var activeRoot = new TestParent();
        var activeParent = new TestParent();
        var inactiveParent = new TestParent();
        var child = new TestParent();
        var leaf = new TestNode();
        child.Add(leaf);
        activeParent.Add(child);
        activeRoot.Add(activeParent);
        activeRoot.AttachToTree(dispatcher);

        inactiveParent.Add(child);

        Assert.Empty(activeParent.Children);
        Assert.Same(inactiveParent, child.Parent);
        Assert.Null(child.ActiveDispatcher);
        Assert.Null(leaf.ActiveDispatcher);

        activeParent.Add(child);

        Assert.Empty(inactiveParent.Children);
        Assert.Same(activeParent, child.Parent);
        Assert.Same(dispatcher, child.ActiveDispatcher);
        Assert.Same(dispatcher, leaf.ActiveDispatcher);
    }

    [Fact]
    public void WrongThreadCannotAttachOrDetachRoot()
    {
        var dispatcher = new UiDispatcher();
        var root = new TestParent();

        var attachError = RunOnBackgroundThread(() =>
            Record.Exception(() => root.AttachToTree(dispatcher)));

        Assert.IsType<InvalidOperationException>(attachError);
        Assert.Null(root.ActiveDispatcher);

        root.AttachToTree(dispatcher);
        var detachError = RunOnBackgroundThread(() =>
            Record.Exception(root.DetachFromTree));

        Assert.IsType<InvalidOperationException>(detachError);
        Assert.Same(dispatcher, root.ActiveDispatcher);
    }

    [Fact]
    public void ActiveStructuralChangesRequireDispatcherThread()
    {
        var dispatcher = new UiDispatcher();
        var root = new TestParent();
        var first = new TestNode();
        var second = new TestNode();
        root.Add(first);
        root.Add(second);
        root.AttachToTree(dispatcher);
        var added = new TestNode();

        var result = RunOnBackgroundThread(() =>
            (AddError: Record.Exception(() => root.Add(added)),
                RemoveError: Record.Exception(() => root.Remove(first)),
                ClearError: Record.Exception(root.Clear),
                MoveError: Record.Exception(second.MoveToBack)));

        Assert.IsType<InvalidOperationException>(result.AddError);
        Assert.IsType<InvalidOperationException>(result.RemoveError);
        Assert.IsType<InvalidOperationException>(result.ClearError);
        Assert.IsType<InvalidOperationException>(result.MoveError);
        Assert.Equal(new UiNode[] { first, second }, root.Children);
        Assert.Same(root, first.Parent);
        Assert.Same(root, second.Parent);
        Assert.Null(added.Parent);
        Assert.Null(added.ActiveDispatcher);
    }

    [Fact]
    public void ActiveNoOpOperationsDoNotRequireDispatcherThread()
    {
        var dispatcher = new UiDispatcher();
        var root = new TestParent();
        var foreign = new TestNode();
        root.AttachToTree(dispatcher);

        var result = RunOnBackgroundThread(() =>
            (RemoveResult: root.Remove(foreign),
                ClearError: Record.Exception(root.Clear)));

        Assert.False(result.RemoveResult);
        Assert.Null(result.ClearError);
        Assert.Empty(root.Children);
        Assert.Same(dispatcher, root.ActiveDispatcher);
    }

    [Fact]
    public void CrossThreadDispatcherMoveFailsWithoutChangingEitherTree()
    {
        var backgroundTree = RunOnBackgroundThread(() =>
        {
            var dispatcher = new UiDispatcher();
            var root = new TestParent();
            var child = new TestParent();
            var leaf = new TestNode();
            child.Add(leaf);
            root.Add(child);
            root.AttachToTree(dispatcher);
            return (Dispatcher: dispatcher, Root: root, Child: child, Leaf: leaf);
        });
        var targetDispatcher = new UiDispatcher();
        var target = new TestParent();
        target.AttachToTree(targetDispatcher);

        Assert.Throws<InvalidOperationException>(() => target.Add(backgroundTree.Child));

        Assert.Equal(new UiNode[] { backgroundTree.Child }, backgroundTree.Root.Children);
        Assert.Same(backgroundTree.Root, backgroundTree.Child.Parent);
        Assert.Same(backgroundTree.Dispatcher, backgroundTree.Child.ActiveDispatcher);
        Assert.Same(backgroundTree.Dispatcher, backgroundTree.Leaf.ActiveDispatcher);
        Assert.Empty(target.Children);
        Assert.Same(targetDispatcher, target.ActiveDispatcher);
    }

    [Fact]
    public void NewDispatcherCheckFailsBeforeDetachingFromAccessibleOldTree()
    {
        var oldDispatcher = new UiDispatcher();
        var oldRoot = new TestParent();
        var child = new TestParent();
        var leaf = new TestNode();
        child.Add(leaf);
        oldRoot.Add(child);
        oldRoot.AttachToTree(oldDispatcher);
        var backgroundTarget = RunOnBackgroundThread(() =>
        {
            var dispatcher = new UiDispatcher();
            var root = new TestParent();
            root.AttachToTree(dispatcher);
            return (Dispatcher: dispatcher, Root: root);
        });

        Assert.Throws<InvalidOperationException>(() => backgroundTarget.Root.Add(child));

        Assert.Equal(new UiNode[] { child }, oldRoot.Children);
        Assert.Same(oldRoot, child.Parent);
        Assert.Same(oldDispatcher, child.ActiveDispatcher);
        Assert.Same(oldDispatcher, leaf.ActiveDispatcher);
        Assert.Empty(backgroundTarget.Root.Children);
        Assert.Same(backgroundTarget.Dispatcher, backgroundTarget.Root.ActiveDispatcher);
    }

    [Fact]
    public void InactiveTreeCanBeConstructedAndChangedOnBackgroundThread()
    {
        var result = RunOnBackgroundThread(() =>
        {
            var parent = new TestParent();
            var first = new TestNode();
            var second = new TestNode();
            parent.Add(first);
            parent.Insert(0, second);
            second.MoveToFront();
            second.MoveToBack();
            var removed = parent.Remove(first);
            parent.Clear();
            return (Parent: parent, First: first, Second: second, Removed: removed);
        });

        Assert.True(result.Removed);
        Assert.Empty(result.Parent.Children);
        Assert.Null(result.First.Parent);
        Assert.Null(result.Second.Parent);
        Assert.Null(result.Parent.ActiveDispatcher);
        Assert.Null(result.First.ActiveDispatcher);
        Assert.Null(result.Second.ActiveDispatcher);
    }

    [Fact]
    public void ClosedDispatcherPreventsDetachingActiveRoot()
    {
        var dispatcher = new UiDispatcher();
        var root = new TestParent();
        var child = new TestNode();
        root.Add(child);
        root.AttachToTree(dispatcher);
        dispatcher.Shutdown();

        Assert.Throws<ObjectDisposedException>(root.DetachFromTree);

        Assert.Same(dispatcher, root.ActiveDispatcher);
        Assert.Same(dispatcher, child.ActiveDispatcher);
        Assert.Same(root, child.Parent);
    }

    private sealed class TestNode : UiNode
    {
    }

    private sealed class TestParent : Parent
    {
        internal void Add(UiNode child) =>
            AddChild(child);

        internal void Insert(int index, UiNode child) =>
            InsertChild(index, child);

        internal bool Remove(UiNode child) =>
            RemoveChild(child);

        internal void Clear() =>
            ClearChildren();
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
