using PanguEngine.ComponentModel;
using PanguEngine.UI;
using PanguEngine.UI.Controls;

namespace PanguEngine.Tests.UI;

public sealed class UIElementScreenTests
{
    [Fact]
    public void ScreenDefaultsToNullAndRejectsPublicWrites()
    {
        var node = new Panel();
        var screen = new Screen();

        Assert.True(UIElement.ScreenProperty.IsReadOnly);
        Assert.Null(node.Screen);
        Assert.Null(node.GetValue(UIElement.ScreenProperty));
        Assert.Throws<InvalidOperationException>(() => node.SetValue(UIElement.ScreenProperty, screen));
        Assert.Throws<InvalidOperationException>(() => node.ClearValue(UIElement.ScreenProperty));
        Assert.Null(node.Screen);
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
        var first = new Screen(root);
        var second = new Screen();
        var changes = new List<(Screen? OldValue, Screen? NewValue)>();
        using var subscription = node.Subscribe(UIElement.ScreenProperty, (_, args) =>
        {
            Assert.Same(args.NewValue, node.Screen);
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
        Assert.Same(first, node.Screen);
    }

    [Fact]
    public void OwnershipChangesNotifyAfterTheCurrentNodeIsUpdated()
    {
        var node = new Panel();
        var first = new Screen();
        var second = new Screen();
        var changes = new List<(Screen? OldValue, Screen? NewValue)>();
        node.PropertyChanged += (_, args) =>
        {
            if (args.Property.Name != nameof(UIElement.Screen))
                return;

            var change = Assert.IsType<PropertyChangedEventArgs<Screen?>>(args);
            Assert.Same(change.NewValue, node.Screen);
            changes.Add((change.OldValue, change.NewValue));
        };

        first.Root = node;
        second.Root = node;
        second.Root = null;

        Assert.Equal([(null, first), (first, second), (second, null)], changes);
        Assert.Null(first.Root);
        Assert.Null(node.Screen);
    }

    [Fact]
    public void ParentNotifiesBeforeDescendantsChangeScreens()
    {
        var parent = new Panel();
        var child = new Panel();
        parent.Children.Add(child);
        var first = new Screen(parent);
        var second = new Screen();
        var order = new List<string>();
        parent.PropertyChanged += (_, args) =>
        {
            if (args.Property.Name != nameof(UIElement.Screen))
                return;

            Assert.Same(second, parent.Screen);
            Assert.Same(first, child.Screen);
            order.Add("parent");
        };
        child.PropertyChanged += (_, args) =>
        {
            if (args.Property.Name != nameof(UIElement.Screen))
                return;

            Assert.Same(second, parent.Screen);
            Assert.Same(second, child.Screen);
            order.Add("child");
        };

        second.Root = parent;

        Assert.Equal(["parent", "child"], order);
    }
}