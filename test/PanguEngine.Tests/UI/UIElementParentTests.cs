using PanguEngine.ComponentModel;
using PanguEngine.UI;
using PanguEngine.UI.Controls;

namespace PanguEngine.Tests.UI;

public sealed class UIElementParentTests
{
    [Fact]
    public void ParentDefaultsToNullAndRejectsPublicWrites()
    {
        var node = new Panel();
        var parent = new Panel();

        Assert.True(UIElement.ParentProperty.IsReadOnly);
        Assert.Null(node.Parent);
        Assert.Null(node.GetValue(UIElement.ParentProperty));
        Assert.Throws<InvalidOperationException>(() => node.SetValue(UIElement.ParentProperty, parent));
        Assert.Throws<InvalidOperationException>(() => node.ClearValue(UIElement.ParentProperty));
        Assert.Null(node.Parent);
    }

    [Fact]
    public void SubscriptionIgnoresReorderingAndStopsAfterDisposal()
    {
        var parent = new Panel();
        var node = new Panel();
        parent.Children.Add(node);
        parent.Children.Add(new Panel());
        var changes = new List<(Container? OldValue, Container? NewValue)>();
        using var subscription = node.Subscribe(UIElement.ParentProperty, (_, args) =>
        {
            Assert.Same(args.NewValue, node.Parent);
            changes.Add((args.OldValue, args.NewValue));
        });

        Assert.Empty(changes);
        node.MoveToFront();
        node.MoveToBack();
        parent.Children[0] = node;
        Assert.Empty(changes);

        parent.Children.Remove(node);
        Assert.False(parent.Children.Remove(node));
        parent.Children.Add(node);
        Assert.Equal([(parent, null), (null, parent)], changes);

        subscription.Dispose();
        parent.Children.Remove(node);
        Assert.Equal(2, changes.Count);
    }

    [Fact]
    public void ReplacementAndClearNotifyAffectedChildren()
    {
        var parent = new Panel();
        var original = new Panel();
        var replacement = new Panel();
        parent.Children.Add(original);
        var originalChanges = new List<(Container? OldValue, Container? NewValue)>();
        var replacementChanges = new List<(Container? OldValue, Container? NewValue)>();
        using var originalSubscription = original.Subscribe(UIElement.ParentProperty,
            (_, args) => originalChanges.Add((args.OldValue, args.NewValue)));
        using var replacementSubscription = replacement.Subscribe(UIElement.ParentProperty,
            (_, args) => replacementChanges.Add((args.OldValue, args.NewValue)));

        parent.Children[0] = replacement;
        parent.Children.Clear();

        Assert.Equal([(parent, null)], originalChanges);
        Assert.Equal([(null, parent), (parent, null)], replacementChanges);
        Assert.Null(original.Parent);
        Assert.Null(replacement.Parent);
    }

    [Fact]
    public void BecomingSceneRootNotifiesParentLossWithoutChangingDescendantParents()
    {
        var parent = new Panel();
        var node = new Panel();
        var descendant = new Panel();
        node.Children.Add(descendant);
        parent.Children.Add(node);
        var scene = new UIScene(parent);
        var changes = new List<(Container? OldValue, Container? NewValue)>();
        var descendantNotifications = 0;
        using var subscription = node.Subscribe(UIElement.ParentProperty,
            (_, args) => changes.Add((args.OldValue, args.NewValue)));
        using var descendantSubscription = descendant.Subscribe(UIElement.ParentProperty,
            (_, _) => descendantNotifications++);

        scene.Root = node;

        Assert.Equal([(parent, null)], changes);
        Assert.Equal(0, descendantNotifications);
        Assert.Null(node.Parent);
        Assert.Same(node, descendant.Parent);
        Assert.Same(scene, node.Scene);
    }

    [Fact]
    public void ParentChangesNotifyAfterTheCurrentValueIsUpdated()
    {
        var node = new Panel();
        var first = new Panel();
        var second = new Panel();
        var changes = new List<(Container? OldValue, Container? NewValue)>();
        node.PropertyChanged += (_, args) =>
        {
            if (args.Property.Name != nameof(UIElement.Parent))
                return;

            var change = Assert.IsType<PropertyChangedEventArgs<Container?>>(args);
            Assert.Same(change.NewValue, node.Parent);
            changes.Add((change.OldValue, change.NewValue));
        };

        first.Children.Add(node);
        second.Children.Add(node);
        second.Children.Remove(node);

        Assert.Equal([(null, first), (first, null), (null, second), (second, null)], changes);
    }

    [Fact]
    public void ParentNotifiesBeforeSceneChangesDuringTransfer()
    {
        var node = new Panel();
        var firstParent = new Panel();
        var secondParent = new Panel();
        firstParent.Children.Add(node);
        var firstScene = new UIScene(firstParent);
        var secondScene = new UIScene(secondParent);
        var changes = new List<(string Property, Container? Parent, UIScene? Scene)>();
        node.PropertyChanged += (_, args) =>
        {
            if (args.Property.Name == nameof(UIElement.Parent))
            {
                var change = Assert.IsType<PropertyChangedEventArgs<Container?>>(args);
                Assert.Same(change.NewValue, node.Parent);
                changes.Add(("parent", node.Parent, node.Scene));
            }
            else if (ReferenceEquals(args.Property, UIElement.SceneProperty))
            {
                var change = Assert.IsType<PropertyChangedEventArgs<UIScene?>>(args);
                Assert.Same(change.NewValue, node.Scene);
                changes.Add(("scene", node.Parent, node.Scene));
            }
        };

        secondParent.Children.Add(node);

        Assert.Equal([
            ("parent", null, firstScene),
            ("scene", null, null),
            ("parent", secondParent, null),
            ("scene", secondParent, secondScene)
        ], changes);
    }
}