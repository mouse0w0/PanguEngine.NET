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

        var parent = new TestParent();
        var first = new TestNode();
        var last = new TestNode();
        parent.Add(first);
        parent.Add(last);
        var screen = new UiScreen(parent);
        screen.Open();

        var backgroundResult = RunOnBackgroundThread(() =>
            (BackError: Record.Exception(first.MoveToBack),
                FrontError: Record.Exception(last.MoveToFront)));

        Assert.Null(backgroundResult.BackError);
        Assert.Null(backgroundResult.FrontError);
        Assert.Equal(new UiNode[] { first, last }, parent.Children);
        screen.Close();
    }

    [Fact]
    public void ActiveTreeMoveCommandsReorderOnOwnerThread()
    {
        var parent = new TestParent();
        var first = new TestNode();
        var middle = new TestNode();
        var last = new TestNode();
        parent.Add(first);
        parent.Add(middle);
        parent.Add(last);
        var screen = new UiScreen(parent);
        screen.Open();

        middle.MoveToFront();
        Assert.Equal(new UiNode[] { first, last, middle }, parent.Children);

        middle.MoveToBack();
        Assert.Equal(new UiNode[] { middle, first, last }, parent.Children);
        Assert.Same(screen, middle.Screen);
        screen.Close();
    }

    [Fact]
    public void AssigningAndClearingRootPropagateScreenThroughSubtree()
    {
        var root = new TestParent();
        var nested = new TestParent();
        var leaf = new TestNode();
        root.Add(nested);
        nested.Add(leaf);

        var screen = new UiScreen(root);

        Assert.Same(screen, root.Screen);
        Assert.Same(screen, nested.Screen);
        Assert.Same(screen, leaf.Screen);

        screen.Root = null;

        Assert.Null(root.Screen);
        Assert.Null(nested.Screen);
        Assert.Null(leaf.Screen);
    }

    [Fact]
    public void ScreenRootMustBeClearedBeforeBecomingAChild()
    {
        var activeRoot = new TestParent();
        var target = new TestParent();
        var screen = new UiScreen(activeRoot);
        screen.Open();

        Assert.Throws<InvalidOperationException>(() => target.Add(activeRoot));
        Assert.Null(activeRoot.Parent);
        Assert.Same(screen, activeRoot.Screen);
        Assert.Empty(target.Children);

        screen.Close();
        screen.Root = null;
        target.Add(activeRoot);

        Assert.Same(target, activeRoot.Parent);
        Assert.Null(activeRoot.Screen);
    }

    [Fact]
    public void ActiveParentActivatesAddedSubtreeAndDeactivatesRemovedSubtree()
    {
        var root = new TestParent();
        var screen = new UiScreen(root);
        screen.Open();
        var nested = new TestParent();
        var leaf = new TestNode();
        nested.Add(leaf);

        root.Add(nested);

        Assert.Same(screen, nested.Screen);
        Assert.Same(screen, leaf.Screen);

        Assert.True(root.Remove(nested));

        Assert.Null(nested.Parent);
        Assert.Null(nested.Screen);
        Assert.Null(leaf.Screen);
        screen.Close();
    }

    [Fact]
    public void ActiveParentClearDeactivatesEveryRemovedSubtree()
    {
        var root = new TestParent();
        var first = new TestParent();
        var firstLeaf = new TestNode();
        var second = new TestNode();
        first.Add(firstLeaf);
        root.Add(first);
        root.Add(second);
        var screen = new UiScreen(root);
        screen.Open();

        root.Clear();

        Assert.Empty(root.Children);
        Assert.Null(first.Parent);
        Assert.Null(second.Parent);
        Assert.Null(first.Screen);
        Assert.Null(firstLeaf.Screen);
        Assert.Null(second.Screen);
        Assert.Same(screen, root.Screen);
        screen.Close();
    }

    [Fact]
    public void ReparentWithinSameScreenKeepsSubtreeActive()
    {
        var root = new TestParent();
        var oldParent = new TestParent();
        var newParent = new TestParent();
        var child = new TestParent();
        var leaf = new TestNode();
        child.Add(leaf);
        oldParent.Add(child);
        root.Add(oldParent);
        root.Add(newParent);
        var screen = new UiScreen(root);
        screen.Open();

        newParent.Add(child);

        Assert.Empty(oldParent.Children);
        Assert.Equal(new UiNode[] { child }, newParent.Children);
        Assert.Same(newParent, child.Parent);
        Assert.Same(screen, child.Screen);
        Assert.Same(screen, leaf.Screen);
        screen.Close();
    }

    [Fact]
    public void ReparentAcrossSameThreadScreensChangesScreen()
    {
        var oldRoot = new TestParent();
        var newRoot = new TestParent();
        var child = new TestParent();
        var leaf = new TestNode();
        child.Add(leaf);
        oldRoot.Add(child);
        var oldScreen = new UiScreen(oldRoot);
        var newScreen = new UiScreen(newRoot);
        oldScreen.Open();
        newScreen.Open();

        newRoot.Add(child);

        Assert.Empty(oldRoot.Children);
        Assert.Equal(new UiNode[] { child }, newRoot.Children);
        Assert.Same(newRoot, child.Parent);
        Assert.Same(newScreen, child.Screen);
        Assert.Same(newScreen, leaf.Screen);
        newScreen.Close();
        oldScreen.Close();
    }

    [Fact]
    public void ReparentBetweenActiveAndInactiveTreesTransitionsSubtreeState()
    {
        var activeRoot = new TestParent();
        var activeParent = new TestParent();
        var inactiveParent = new TestParent();
        var child = new TestParent();
        var leaf = new TestNode();
        child.Add(leaf);
        activeParent.Add(child);
        activeRoot.Add(activeParent);
        var screen = new UiScreen(activeRoot);
        screen.Open();

        inactiveParent.Add(child);

        Assert.Empty(activeParent.Children);
        Assert.Same(inactiveParent, child.Parent);
        Assert.Null(child.Screen);
        Assert.Null(leaf.Screen);

        activeParent.Add(child);

        Assert.Empty(inactiveParent.Children);
        Assert.Same(activeParent, child.Parent);
        Assert.Same(screen, child.Screen);
        Assert.Same(screen, leaf.Screen);
        screen.Close();
    }

    [Fact]
    public void WrongThreadCannotReplaceOpenScreenRoot()
    {
        var root = new TestParent();
        var screen = new UiScreen(root);
        screen.Open();

        var result = RunOnBackgroundThread(() =>
            (RootError: Record.Exception(() => screen.Root = null),
                CloseError: Record.Exception(screen.Close)));

        Assert.IsType<InvalidOperationException>(result.RootError);
        Assert.IsType<InvalidOperationException>(result.CloseError);
        Assert.Same(screen, root.Screen);
        screen.Close();
    }

    [Fact]
    public void ActiveStructuralChangesRequireOwnerThread()
    {
        var root = new TestParent();
        var first = new TestNode();
        var second = new TestNode();
        root.Add(first);
        root.Add(second);
        var screen = new UiScreen(root);
        screen.Open();
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
        Assert.Null(added.Screen);
        screen.Close();
    }

    [Fact]
    public void ActiveNoOpOperationsDoNotRequireOwnerThread()
    {
        var root = new TestParent();
        var foreign = new TestNode();
        var screen = new UiScreen(root);
        screen.Open();

        var result = RunOnBackgroundThread(() =>
            (RemoveResult: root.Remove(foreign),
                ClearError: Record.Exception(root.Clear)));

        Assert.False(result.RemoveResult);
        Assert.Null(result.ClearError);
        Assert.Empty(root.Children);
        Assert.Same(screen, root.Screen);
        screen.Close();
    }

    [Fact]
    public void CrossThreadScreenMoveFailsWithoutChangingEitherTree()
    {
        var backgroundTree = RunOnBackgroundThread(() =>
        {
            var root = new TestParent();
            var child = new TestParent();
            var leaf = new TestNode();
            child.Add(leaf);
            root.Add(child);
            var screen = new UiScreen(root);
            screen.Open();
            return (Screen: screen, Root: root, Child: child, Leaf: leaf);
        });
        var target = new TestParent();
        var targetScreen = new UiScreen(target);
        targetScreen.Open();

        Assert.Throws<InvalidOperationException>(() => target.Add(backgroundTree.Child));

        Assert.Equal(new UiNode[] { backgroundTree.Child }, backgroundTree.Root.Children);
        Assert.Same(backgroundTree.Root, backgroundTree.Child.Parent);
        Assert.Same(backgroundTree.Screen, backgroundTree.Child.Screen);
        Assert.Same(backgroundTree.Screen, backgroundTree.Leaf.Screen);
        Assert.Empty(target.Children);
        Assert.Same(targetScreen, target.Screen);
        targetScreen.Close();
    }

    [Fact]
    public void NewScreenCheckFailsBeforeDetachingFromAccessibleOldTree()
    {
        var oldRoot = new TestParent();
        var child = new TestParent();
        var leaf = new TestNode();
        child.Add(leaf);
        oldRoot.Add(child);
        var oldScreen = new UiScreen(oldRoot);
        oldScreen.Open();
        var backgroundTarget = RunOnBackgroundThread(() =>
        {
            var root = new TestParent();
            var screen = new UiScreen(root);
            screen.Open();
            return (Screen: screen, Root: root);
        });

        Assert.Throws<InvalidOperationException>(() => backgroundTarget.Root.Add(child));

        Assert.Equal(new UiNode[] { child }, oldRoot.Children);
        Assert.Same(oldRoot, child.Parent);
        Assert.Same(oldScreen, child.Screen);
        Assert.Same(oldScreen, leaf.Screen);
        Assert.Empty(backgroundTarget.Root.Children);
        Assert.Same(backgroundTarget.Screen, backgroundTarget.Root.Screen);
        oldScreen.Close();
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
        Assert.Null(result.Parent.Screen);
        Assert.Null(result.First.Screen);
        Assert.Null(result.Second.Screen);
    }

    [Fact]
    public void ClosedScreenKeepsRootAssociated()
    {
        var root = new TestParent();
        var child = new TestNode();
        root.Add(child);
        var screen = new UiScreen(root);
        screen.Open();

        screen.Close();

        Assert.Same(screen, root.Screen);
        Assert.Same(screen, child.Screen);
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
