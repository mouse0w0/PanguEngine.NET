using PanguEngine.Client.UI;
using PanguEngine.ComponentModel;
using PanguEngine.Input;
using PanguEngine.UI;
using PanguEngine.UI.Controls;

namespace PanguEngine.Tests.UI;

public sealed class TabInfrastructureTests
{
    [Fact]
    public void ChildrenChangedNotifiesForEachStructuralRemoval()
    {
        var parent = new TrackingParent();
        var first = new TestNode();
        var second = new TestNode();
        parent.Add(first);
        parent.Add(second);
        Assert.Empty(parent.Removed);

        Assert.True(parent.Remove(first));

        Assert.Equal(new UIElement[] { first }, parent.Removed);
        Assert.Null(parent.RemovedParentAtCallback);

        parent.Clear();

        Assert.Equal(new UIElement[] { first, second }, parent.Removed);
        Assert.Null(parent.RemovedParentAtCallback);

        var replaced = new TestNode();
        var incoming = new TestNode();
        parent.Add(replaced);
        parent.Replace(0, incoming);

        Assert.Equal(new UIElement[] { first, second, replaced }, parent.Removed);
        Assert.Null(parent.RemovedParentAtCallback);
    }

    [Fact]
    public void ReparentNotifiesOldParentAfterDetachment()
    {
        var oldParent = new TrackingParent();
        var newParent = new TrackingParent();
        var child = new TestNode();
        oldParent.Add(child);
        oldParent.Removed.Clear();

        newParent.Add(child);

        Assert.Equal(new UIElement[] { child }, oldParent.Removed);
        Assert.Null(oldParent.RemovedParentAtCallback);
        Assert.Same(newParent, child.Parent);
        Assert.Empty(newParent.Removed);

        var siblingA = new TestNode();
        var siblingB = new TestNode();
        newParent.Add(siblingA);
        newParent.Add(siblingB);
        newParent.Removed.Clear();

        newParent.Move(0, 2);

        Assert.Empty(newParent.Removed);
    }

    [Fact]
    public void RootTransferNotifiesOldParent()
    {
        var sourceParent = new TrackingParent();
        var child = new TestNode();
        sourceParent.Add(child);
        sourceParent.Removed.Clear();

        var scene = new UIScene();
        scene.Root = child;

        Assert.Equal(new UIElement[] { child }, sourceParent.Removed);
        Assert.Null(sourceParent.RemovedParentAtCallback);
        Assert.Null(child.Parent);
        Assert.Same(scene, child.Scene);
    }

    [Fact]
    public void HidingClearsInputEvenWhenOnPropertyChangedOverrideSkipsBase()
    {
        var root = new Canvas();
        var node = Place(root, new NoBaseControl { Focusable = true }, 0, 0, 40, 40);
        var manager = new UIManager();
        var scene = new GameUIScene(root);
        manager.Open(scene);
        manager.UpdateFrame(new Size(100, 100), 0);
        manager.ProcessPointerMoved(new Point(5, 5));
        Assert.True(node.Focus());
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        Assert.True(node.IsFocused);
        Assert.True(node.IsPressed);

        node.Visibility = Visibility.Hidden;

        Assert.Null(scene.FocusedNode);
        Assert.False(node.IsFocused);
        Assert.False(node.IsHovered);
        Assert.False(node.IsPressed);
        manager.Close();
    }

    private static T Place<T>(Canvas parent, T child, double x, double y, double width, double height)
        where T : UIElement
    {
        child.Width = width;
        child.Height = height;
        Canvas.SetLeft(child, x);
        Canvas.SetTop(child, y);
        parent.Children.Add(child);
        return child;
    }

    private sealed class TestNode : UIElement
    {
    }

    private sealed class TrackingParent : Container
    {
        internal List<UIElement> Removed { get; } = [];

        internal UIElement? RemovedParentAtCallback { get; private set; }

        internal TrackingParent()
        {
            Children.Changed += (_, change) =>
            {
                foreach (var child in change.OldItems)
                {
                    if (ReferenceEquals(child.Parent, this))
                        continue;
                    Removed.Add(child);
                    RemovedParentAtCallback = child.Parent;
                }
            };
        }

        internal void Add(UIElement child) =>
            Children.Add(child);

        internal bool Remove(UIElement child) =>
            Children.Remove(child);

        internal void Clear() =>
            Children.Clear();

        internal void Replace(int index, UIElement child) =>
            Children[index] = child;

        internal void Move(int oldIndex, int newIndex) =>
            Children.Move(oldIndex, newIndex);
    }

    private sealed class NoBaseControl : Control
    {
        protected override void OnPropertyChanged(PropertyChangedEventArgs eventArgs)
        {
        }
    }
}