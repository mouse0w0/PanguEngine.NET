using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;

namespace PanguEngine.Tests.Client.UI;

public sealed class UiNodeParentTests
{
    [Fact]
    public void ParentDefaultsToNullAndRejectsPublicWrites()
    {
        var node = new Panel();
        var parent = new Panel();

        Assert.True(UiNode.ParentProperty.IsReadOnly);
        Assert.Null(node.Parent);
        Assert.Null(node.GetValue(UiNode.ParentProperty));
        Assert.Throws<InvalidOperationException>(() => node.SetValue(UiNode.ParentProperty, parent));
        Assert.Throws<InvalidOperationException>(() => node.ClearValue(UiNode.ParentProperty));
        Assert.Null(node.Parent);
    }

    [Fact]
    public void SubscriptionIgnoresReorderingAndStopsAfterDisposal()
    {
        var parent = new Panel();
        var node = new Panel();
        parent.Children.Add(node);
        parent.Children.Add(new Panel());
        var changes = new List<(Parent? OldValue, Parent? NewValue)>();
        using var subscription = node.Subscribe(UiNode.ParentProperty, (_, args) =>
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
        var originalChanges = new List<(Parent? OldValue, Parent? NewValue)>();
        var replacementChanges = new List<(Parent? OldValue, Parent? NewValue)>();
        using var originalSubscription = original.Subscribe(UiNode.ParentProperty,
            (_, args) => originalChanges.Add((args.OldValue, args.NewValue)));
        using var replacementSubscription = replacement.Subscribe(UiNode.ParentProperty,
            (_, args) => replacementChanges.Add((args.OldValue, args.NewValue)));

        parent.Children[0] = replacement;
        parent.Children.Clear();

        Assert.Equal([(parent, null)], originalChanges);
        Assert.Equal([(null, parent), (parent, null)], replacementChanges);
        Assert.Null(original.Parent);
        Assert.Null(replacement.Parent);
    }

    [Fact]
    public void BecomingScreenRootNotifiesParentLossWithoutChangingDescendantParents()
    {
        var parent = new Panel();
        var node = new Panel();
        var descendant = new Panel();
        node.Children.Add(descendant);
        parent.Children.Add(node);
        var screen = new UiScreen(parent);
        var changes = new List<(Parent? OldValue, Parent? NewValue)>();
        var descendantNotifications = 0;
        using var subscription = node.Subscribe(UiNode.ParentProperty,
            (_, args) => changes.Add((args.OldValue, args.NewValue)));
        using var descendantSubscription = descendant.Subscribe(UiNode.ParentProperty,
            (_, _) => descendantNotifications++);

        screen.Root = node;

        Assert.Equal([(parent, null)], changes);
        Assert.Equal(0, descendantNotifications);
        Assert.Null(node.Parent);
        Assert.Same(node, descendant.Parent);
        Assert.Same(screen, node.Screen);
    }

    [Fact]
    public void ParentChangesNotifyAfterTheCurrentValueIsUpdated()
    {
        var node = new Panel();
        var first = new Panel();
        var second = new Panel();
        var changes = new List<(Parent? OldValue, Parent? NewValue)>();
        node.PropertyChanged += (_, args) =>
        {
            if (args.Property.Name != nameof(UiNode.Parent))
                return;

            var change = Assert.IsType<UiPropertyChangedEventArgs<Parent?>>(args);
            Assert.Same(change.NewValue, node.Parent);
            changes.Add((change.OldValue, change.NewValue));
        };

        first.Children.Add(node);
        second.Children.Add(node);
        second.Children.Remove(node);

        Assert.Equal([(null, first), (first, second), (second, null)], changes);
    }

    [Fact]
    public void ParentNotifiesBeforeScreenChangesDuringTransfer()
    {
        var node = new Panel();
        var firstParent = new Panel();
        var secondParent = new Panel();
        firstParent.Children.Add(node);
        var firstScreen = new UiScreen(firstParent);
        var secondScreen = new UiScreen(secondParent);
        var order = new List<string>();
        node.PropertyChanged += (_, args) =>
        {
            if (args.Property.Name == nameof(UiNode.Parent))
            {
                Assert.Same(secondParent, node.Parent);
                Assert.Same(firstScreen, node.Screen);
                order.Add("parent");
            }
            else if (ReferenceEquals(args.Property, UiNode.ScreenProperty))
            {
                Assert.Same(secondParent, node.Parent);
                Assert.Same(secondScreen, node.Screen);
                order.Add("screen");
            }
        };

        secondParent.Children.Add(node);

        Assert.Equal(["parent", "screen"], order);
    }
}