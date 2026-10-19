using System.Runtime.ExceptionServices;
using PanguEngine.UI;
using PanguEngine.UI.Controls;

namespace PanguEngine.Tests.UI;

public sealed class ContainerTests
{
    [Fact]
    public void ChildrenViewIsStableReadOnlyAndTracksChanges()
    {
        var parent = new TestParent();
        var first = new TestNode();
        var second = new TestNode();
        var view = parent.ReadOnlyChildren;

        parent.Add(first);
        parent.Insert(0, second);

        Assert.Same(view, parent.ReadOnlyChildren);
        Assert.Equal(new UIElement[] { second, first }, view);
        Assert.Same(parent, first.Parent);
        Assert.Same(parent, second.Parent);
        Assert.IsNotType<List<UIElement>>(view);
        Assert.False(view is IList<UIElement>);
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
        Assert.False(parent.Remove(null!));
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

        Assert.Equal(new UIElement[] { oldSibling }, oldParent.Children);
        Assert.Equal(new UIElement[] { child, newSibling }, newParent.Children);
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
        Assert.Equal(new UIElement[] { middle }, root.Children);
        Assert.Null(root.Parent);

        Assert.Throws<InvalidOperationException>(() => middle.Add(root));
        Assert.Equal(new UIElement[] { leaf }, middle.Children);
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
        Assert.Equal(new UIElement[] { first, last, middle }, parent.Children);
        Assert.Same(parent, middle.Parent);

        middle.MoveToBack();
        Assert.Equal(new UIElement[] { middle, first, last }, parent.Children);
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
        var scene = new UIScene(parent);
        scene.Open();

        var backgroundResult = RunOnBackgroundThread(() =>
            (BackError: Record.Exception(first.MoveToBack),
                FrontError: Record.Exception(last.MoveToFront)));

        Assert.Null(backgroundResult.BackError);
        Assert.Null(backgroundResult.FrontError);
        Assert.Equal(new UIElement[] { first, last }, parent.Children);
        scene.Close();
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
        var scene = new UIScene(parent);
        scene.Open();

        middle.MoveToFront();
        Assert.Equal(new UIElement[] { first, last, middle }, parent.Children);

        middle.MoveToBack();
        Assert.Equal(new UIElement[] { middle, first, last }, parent.Children);
        Assert.Same(scene, middle.Scene);
        scene.Close();
    }

    [Fact]
    public void AssigningAndClearingRootPropagateSceneThroughSubtree()
    {
        var root = new TestParent();
        var nested = new TestParent();
        var leaf = new TestNode();
        root.Add(nested);
        nested.Add(leaf);

        var scene = new UIScene(root);

        Assert.Same(scene, root.Scene);
        Assert.Same(scene, nested.Scene);
        Assert.Same(scene, leaf.Scene);

        scene.Root = null;

        Assert.Null(root.Scene);
        Assert.Null(nested.Scene);
        Assert.Null(leaf.Scene);
    }

    [Fact]
    public void SceneRootMustBeClearedBeforeBecomingAChild()
    {
        var activeRoot = new TestParent();
        var target = new TestParent();
        var scene = new UIScene(activeRoot);
        scene.Open();

        Assert.Throws<InvalidOperationException>(() => target.Add(activeRoot));
        Assert.Null(activeRoot.Parent);
        Assert.Same(scene, activeRoot.Scene);
        Assert.Empty(target.Children);

        scene.Close();
        scene.Root = null;
        target.Add(activeRoot);

        Assert.Same(target, activeRoot.Parent);
        Assert.Null(activeRoot.Scene);
    }

    [Fact]
    public void ActiveParentActivatesAddedSubtreeAndDeactivatesRemovedSubtree()
    {
        var root = new TestParent();
        var scene = new UIScene(root);
        scene.Open();
        var nested = new TestParent();
        var leaf = new TestNode();
        nested.Add(leaf);

        root.Add(nested);

        Assert.Same(scene, nested.Scene);
        Assert.Same(scene, leaf.Scene);

        Assert.True(root.Remove(nested));

        Assert.Null(nested.Parent);
        Assert.Null(nested.Scene);
        Assert.Null(leaf.Scene);
        scene.Close();
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
        var scene = new UIScene(root);
        scene.Open();

        root.Clear();

        Assert.Empty(root.Children);
        Assert.Null(first.Parent);
        Assert.Null(second.Parent);
        Assert.Null(first.Scene);
        Assert.Null(firstLeaf.Scene);
        Assert.Null(second.Scene);
        Assert.Same(scene, root.Scene);
        scene.Close();
    }

    [Fact]
    public void ClearingSceneParentInvalidatesEveryRemovedSubtree()
    {
        var root = new Canvas();
        var first = new Canvas();
        var firstLeaf = new TestNode();
        var second = new TestNode();
        first.Children.Add(firstLeaf);
        root.Children.Add(first);
        root.Children.Add(second);
        var scene = new UIScene(root);
        scene.Open();
        scene.PrepareFrame(new Size(100, 100));
        Assert.True(first.IsMeasureValid);
        Assert.True(firstLeaf.IsMeasureValid);
        Assert.True(second.IsMeasureValid);

        root.Children.Clear();

        Assert.False(first.IsMeasureValid);
        Assert.False(first.IsArrangeValid);
        Assert.False(firstLeaf.IsMeasureValid);
        Assert.False(firstLeaf.IsArrangeValid);
        Assert.False(second.IsMeasureValid);
        Assert.False(second.IsArrangeValid);
        scene.Close();
    }

    [Fact]
    public void ReparentWithinSameSceneInvalidatesSubtreeLayout()
    {
        var root = new Canvas();
        var oldParent = new Canvas();
        var newParent = new Canvas();
        var child = new Canvas();
        var leaf = new TestNode();
        child.Children.Add(leaf);
        oldParent.Children.Add(child);
        root.Children.Add(oldParent);
        root.Children.Add(newParent);
        var scene = new UIScene(root);
        scene.Open();
        scene.PrepareFrame(new Size(100, 100));
        Assert.True(child.IsMeasureValid);
        Assert.True(child.IsArrangeValid);
        Assert.True(leaf.IsMeasureValid);
        Assert.True(leaf.IsArrangeValid);

        newParent.Children.Add(child);

        Assert.Empty(oldParent.Children);
        Assert.Equal(new UIElement[] { child }, newParent.Children);
        Assert.Same(newParent, child.Parent);
        Assert.Same(scene, child.Scene);
        Assert.Same(scene, leaf.Scene);
        Assert.False(child.IsMeasureValid);
        Assert.False(child.IsArrangeValid);
        Assert.False(leaf.IsMeasureValid);
        Assert.False(leaf.IsArrangeValid);
        scene.Close();
    }

    [Fact]
    public void ReparentAcrossSameThreadScenesChangesScene()
    {
        var oldRoot = new TestParent();
        var newRoot = new TestParent();
        var child = new TestParent();
        var leaf = new TestNode();
        child.Add(leaf);
        oldRoot.Add(child);
        var oldScene = new UIScene(oldRoot);
        var newScene = new UIScene(newRoot);
        oldScene.Open();
        newScene.Open();

        newRoot.Add(child);

        Assert.Empty(oldRoot.Children);
        Assert.Equal(new UIElement[] { child }, newRoot.Children);
        Assert.Same(newRoot, child.Parent);
        Assert.Same(newScene, child.Scene);
        Assert.Same(newScene, leaf.Scene);
        newScene.Close();
        oldScene.Close();
    }

    [Fact]
    public void ReparentAcrossDifferentSceneScalesInvalidatesEntireSubtree()
    {
        var oldRoot = new Canvas();
        var newRoot = new Canvas();
        var child = new Canvas();
        var grandchild = new TestNode();
        child.Children.Add(grandchild);
        oldRoot.Children.Add(child);
        var oldScene = new UIScene(oldRoot) { Scale = 2 };
        var newScene = new UIScene(newRoot) { Scale = 3 };
        oldScene.Open();
        newScene.Open();
        oldScene.PrepareFrame(new Size(100, 100));
        Assert.True(child.IsMeasureValid);
        Assert.True(grandchild.IsMeasureValid);

        newRoot.Children.Add(child);

        Assert.False(child.IsMeasureValid);
        Assert.False(child.IsArrangeValid);
        Assert.False(grandchild.IsMeasureValid);
        Assert.False(grandchild.IsArrangeValid);
        newScene.Close();
        oldScene.Close();
    }

    [Fact]
    public void ReparentAcrossDifferentRoundingModesInvalidatesEntireSubtree()
    {
        var oldRoot = new Canvas();
        var newRoot = new Canvas();
        var child = new Canvas();
        var grandchild = new TestNode();
        child.Children.Add(grandchild);
        oldRoot.Children.Add(child);
        var oldScene = new UIScene(oldRoot);
        var newScene = new UIScene(newRoot) { UseLayoutRounding = false };
        oldScene.Open();
        newScene.Open();
        oldScene.PrepareFrame(new Size(100, 100));

        newRoot.Children.Add(child);

        Assert.False(child.IsMeasureValid);
        Assert.False(child.IsArrangeValid);
        Assert.False(grandchild.IsMeasureValid);
        Assert.False(grandchild.IsArrangeValid);
        newScene.Close();
        oldScene.Close();
    }

    [Fact]
    public void ReparentAcrossScenesWithEqualLayoutSettingsInvalidatesEntireSubtree()
    {
        var oldRoot = new Canvas();
        var newRoot = new Canvas();
        var child = new Canvas();
        var grandchild = new TestNode();
        child.Children.Add(grandchild);
        oldRoot.Children.Add(child);
        var oldScene = new UIScene(oldRoot) { Scale = 2, UseLayoutRounding = false };
        var newScene = new UIScene(newRoot) { Scale = 2, UseLayoutRounding = false };
        oldScene.Open();
        newScene.Open();
        oldScene.PrepareFrame(new Size(100, 100));

        newRoot.Children.Add(child);

        Assert.False(child.IsMeasureValid);
        Assert.False(child.IsArrangeValid);
        Assert.False(grandchild.IsMeasureValid);
        Assert.False(grandchild.IsArrangeValid);
        newScene.Close();
        oldScene.Close();
    }

    [Fact]
    public void RemovingSubtreeFromSceneInvalidatesDescendants()
    {
        var root = new Canvas();
        var child = new Canvas();
        var grandchild = new TestNode();
        child.Children.Add(grandchild);
        root.Children.Add(child);
        var scene = new UIScene(root);
        scene.Open();
        scene.PrepareFrame(new Size(100, 100));

        root.Children.Remove(child);

        Assert.False(child.IsMeasureValid);
        Assert.False(child.IsArrangeValid);
        Assert.False(grandchild.IsMeasureValid);
        Assert.False(grandchild.IsArrangeValid);
        scene.Close();
    }

    [Fact]
    public void AddingDetachedSubtreeToSceneInvalidatesDescendants()
    {
        var root = new Canvas();
        var child = new Canvas();
        var grandchild = new TestNode();
        child.Children.Add(grandchild);
        child.Measure(new Size(100, 100));
        child.Arrange(new Rect(0, 0, 100, 100));
        Assert.True(child.IsMeasureValid);
        Assert.True(grandchild.IsMeasureValid);
        var scene = new UIScene(root);
        scene.Open();

        root.Children.Add(child);

        Assert.False(child.IsMeasureValid);
        Assert.False(child.IsArrangeValid);
        Assert.False(grandchild.IsMeasureValid);
        Assert.False(grandchild.IsArrangeValid);
        scene.Close();
    }

    [Fact]
    public void ReplacingAcrossScenesInvalidatesIncomingAndReplacedSubtrees()
    {
        var sourceRoot = new Canvas();
        var incoming = new Canvas();
        var incomingLeaf = new TestNode();
        incoming.Children.Add(incomingLeaf);
        sourceRoot.Children.Add(incoming);
        var targetRoot = new Canvas();
        var replaced = new Canvas();
        var replacedLeaf = new TestNode();
        replaced.Children.Add(replacedLeaf);
        targetRoot.Children.Add(replaced);
        var sourceScene = new UIScene(sourceRoot);
        var targetScene = new UIScene(targetRoot);
        sourceScene.Open();
        targetScene.Open();
        sourceScene.PrepareFrame(new Size(100, 100));
        targetScene.PrepareFrame(new Size(100, 100));
        Assert.True(incoming.IsMeasureValid);
        Assert.True(incomingLeaf.IsMeasureValid);
        Assert.True(replaced.IsMeasureValid);
        Assert.True(replacedLeaf.IsMeasureValid);

        targetRoot.Children[0] = incoming;

        Assert.False(incoming.IsMeasureValid);
        Assert.False(incoming.IsArrangeValid);
        Assert.False(incomingLeaf.IsMeasureValid);
        Assert.False(incomingLeaf.IsArrangeValid);
        Assert.False(replaced.IsMeasureValid);
        Assert.False(replaced.IsArrangeValid);
        Assert.False(replacedLeaf.IsMeasureValid);
        Assert.False(replacedLeaf.IsArrangeValid);
        targetScene.Close();
        sourceScene.Close();
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
        var scene = new UIScene(activeRoot);
        scene.Open();

        inactiveParent.Add(child);

        Assert.Empty(activeParent.Children);
        Assert.Same(inactiveParent, child.Parent);
        Assert.Null(child.Scene);
        Assert.Null(leaf.Scene);

        activeParent.Add(child);

        Assert.Empty(inactiveParent.Children);
        Assert.Same(activeParent, child.Parent);
        Assert.Same(scene, child.Scene);
        Assert.Same(scene, leaf.Scene);
        scene.Close();
    }

    [Fact]
    public void WrongThreadCannotReplaceOpenSceneRoot()
    {
        var root = new TestParent();
        var scene = new UIScene(root);
        scene.Open();

        var result = RunOnBackgroundThread(() =>
            (RootError: Record.Exception(() => scene.Root = null),
                CloseError: Record.Exception(scene.Close)));

        Assert.IsType<InvalidOperationException>(result.RootError);
        Assert.IsType<InvalidOperationException>(result.CloseError);
        Assert.Same(scene, root.Scene);
        scene.Close();
    }

    [Fact]
    public void ActiveStructuralChangesRequireOwnerThread()
    {
        var root = new TestParent();
        var first = new TestNode();
        var second = new TestNode();
        root.Add(first);
        root.Add(second);
        var scene = new UIScene(root);
        scene.Open();
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
        Assert.Equal(new UIElement[] { first, second }, root.Children);
        Assert.Same(root, first.Parent);
        Assert.Same(root, second.Parent);
        Assert.Null(added.Parent);
        Assert.Null(added.Scene);
        scene.Close();
    }

    [Fact]
    public void ActiveNoOpOperationsDoNotRequireOwnerThread()
    {
        var root = new TestParent();
        var foreign = new TestNode();
        var scene = new UIScene(root);
        scene.Open();

        var result = RunOnBackgroundThread(() =>
            (RemoveResult: root.Remove(foreign),
                ClearError: Record.Exception(root.Clear)));

        Assert.False(result.RemoveResult);
        Assert.Null(result.ClearError);
        Assert.Empty(root.Children);
        Assert.Same(scene, root.Scene);
        scene.Close();
    }

    [Fact]
    public void CrossThreadSceneMoveFailsWithoutChangingEitherTree()
    {
        var backgroundTree = RunOnBackgroundThread(() =>
        {
            var root = new TestParent();
            var child = new TestParent();
            var leaf = new TestNode();
            child.Add(leaf);
            root.Add(child);
            var scene = new UIScene(root);
            scene.Open();
            return (Scene: scene, Root: root, Child: child, Leaf: leaf);
        });
        var target = new TestParent();
        var targetScene = new UIScene(target);
        targetScene.Open();

        Assert.Throws<InvalidOperationException>(() => target.Add(backgroundTree.Child));

        Assert.Equal(new UIElement[] { backgroundTree.Child }, backgroundTree.Root.Children);
        Assert.Same(backgroundTree.Root, backgroundTree.Child.Parent);
        Assert.Same(backgroundTree.Scene, backgroundTree.Child.Scene);
        Assert.Same(backgroundTree.Scene, backgroundTree.Leaf.Scene);
        Assert.Empty(target.Children);
        Assert.Same(targetScene, target.Scene);
        targetScene.Close();
    }

    [Fact]
    public void NewSceneCheckFailsBeforeDetachingFromAccessibleOldTree()
    {
        var oldRoot = new TestParent();
        var child = new TestParent();
        var leaf = new TestNode();
        child.Add(leaf);
        oldRoot.Add(child);
        var oldScene = new UIScene(oldRoot);
        oldScene.Open();
        var backgroundTarget = RunOnBackgroundThread(() =>
        {
            var root = new TestParent();
            var scene = new UIScene(root);
            scene.Open();
            return (Scene: scene, Root: root);
        });

        Assert.Throws<InvalidOperationException>(() => backgroundTarget.Root.Add(child));

        Assert.Equal(new UIElement[] { child }, oldRoot.Children);
        Assert.Same(oldRoot, child.Parent);
        Assert.Same(oldScene, child.Scene);
        Assert.Same(oldScene, leaf.Scene);
        Assert.Empty(backgroundTarget.Root.Children);
        Assert.Same(backgroundTarget.Scene, backgroundTarget.Root.Scene);
        oldScene.Close();
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
        Assert.Null(result.Parent.Scene);
        Assert.Null(result.First.Scene);
        Assert.Null(result.Second.Scene);
    }

    [Fact]
    public void ClosedSceneKeepsRootAssociated()
    {
        var root = new TestParent();
        var child = new TestNode();
        root.Add(child);
        var scene = new UIScene(root);
        scene.Open();

        scene.Close();

        Assert.Same(scene, root.Scene);
        Assert.Same(scene, child.Scene);
        Assert.Same(root, child.Parent);
    }

    private sealed class TestNode : UIElement
    {
    }

    private sealed class TestParent : Container
    {
        internal void Add(UIElement child) =>
            Children.Add(child);

        internal void Insert(int index, UIElement child) =>
            Children.Insert(index, child);

        internal bool Remove(UIElement child) =>
            Children.Remove(child);

        internal void Clear() =>
            Children.Clear();
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