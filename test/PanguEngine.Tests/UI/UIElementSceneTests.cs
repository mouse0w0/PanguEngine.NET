using PanguEngine.ComponentModel;
using PanguEngine.UI;
using PanguEngine.UI.Controls;

namespace PanguEngine.Tests.UI;

public sealed class UIElementSceneTests
{
    [Fact]
    public void SceneDefaultsToNullAndRejectsPublicWrites()
    {
        var node = new Panel();
        var scene = new UIScene();

        Assert.True(UIElement.SceneProperty.IsReadOnly);
        Assert.Null(node.Scene);
        Assert.Null(node.GetValue(UIElement.SceneProperty));
        Assert.Throws<InvalidOperationException>(() => node.SetValue(UIElement.SceneProperty, scene));
        Assert.Throws<InvalidOperationException>(() => node.ClearValue(UIElement.SceneProperty));
        Assert.Null(node.Scene);
    }

    [Fact]
    public void SubscriptionOnlyReportsActualChangesUntilDisposed()
    {
        var root = new Panel();
        var firstParent = new Panel();
        var secondParent = new Panel();
        var node = new Panel();
        root.Children.Add(firstParent);
        root.Children.Add(secondParent);
        firstParent.Children.Add(node);
        var first = new UIScene(root);
        var second = new UIScene();
        var changes = new List<(UIScene? OldValue, UIScene? NewValue)>();
        using var subscription = node.Subscribe(UIElement.SceneProperty, (_, args) =>
        {
            Assert.Same(args.NewValue, node.Scene);
            changes.Add((args.OldValue, args.NewValue));
        });

        Assert.Empty(changes);
        secondParent.Children.Add(node);
        Assert.Equal([(first, null), (null, first)], changes);

        second.Root = node;
        second.Root = node;
        Assert.Equal([(first, null), (null, first), (first, null), (null, second)], changes);

        second.Root = null;
        Assert.Equal([(first, null), (null, first), (first, null), (null, second), (second, null)], changes);

        subscription.Dispose();
        firstParent.Children.Add(node);
        Assert.Equal(5, changes.Count);
        Assert.Same(first, node.Scene);
    }

    [Fact]
    public void OwnershipChangesNotifyAfterTheCurrentNodeIsUpdated()
    {
        var node = new Panel();
        var first = new UIScene();
        var second = new UIScene();
        var changes = new List<(UIScene? OldValue, UIScene? NewValue)>();
        node.PropertyChanged += (_, args) =>
        {
            if (args.Property.Name != nameof(UIElement.Scene))
                return;

            var change = Assert.IsType<PropertyChangedEventArgs<UIScene?>>(args);
            Assert.Same(change.NewValue, node.Scene);
            changes.Add((change.OldValue, change.NewValue));
        };

        first.Root = node;
        second.Root = node;
        second.Root = null;

        Assert.Equal([(null, first), (first, second), (second, null)], changes);
        Assert.Null(first.Root);
        Assert.Null(node.Scene);
    }

    [Fact]
    public void ParentNotifiesBeforeDescendantsChangeScenes()
    {
        var parent = new Panel();
        var child = new Panel();
        parent.Children.Add(child);
        var first = new UIScene(parent);
        var second = new UIScene();
        var order = new List<string>();
        parent.PropertyChanged += (_, args) =>
        {
            if (args.Property.Name != nameof(UIElement.Scene))
                return;

            Assert.Same(second, parent.Scene);
            Assert.Same(first, child.Scene);
            order.Add("parent");
        };
        child.PropertyChanged += (_, args) =>
        {
            if (args.Property.Name != nameof(UIElement.Scene))
                return;

            Assert.Same(second, parent.Scene);
            Assert.Same(second, child.Scene);
            order.Add("child");
        };

        second.Root = parent;

        Assert.Equal(["parent", "child"], order);
    }
}