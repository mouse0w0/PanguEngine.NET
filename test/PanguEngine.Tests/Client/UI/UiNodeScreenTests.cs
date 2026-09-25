using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;

namespace PanguEngine.Tests.Client.UI;

public sealed class UiNodeScreenTests
{
    [Fact]
    public void ScreenDefaultsToNullAndRejectsPublicWrites()
    {
        var node = new Panel();
        var screen = new UiScreen();

        Assert.True(UiNode.ScreenProperty.IsReadOnly);
        Assert.Null(node.Screen);
        Assert.Null(node.GetValue(UiNode.ScreenProperty));
        Assert.Throws<InvalidOperationException>(() => node.SetValue(UiNode.ScreenProperty, screen));
        Assert.Throws<InvalidOperationException>(() => node.ClearValue(UiNode.ScreenProperty));
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
        var first = new UiScreen(root);
        var second = new UiScreen();
        var changes = new List<(UiScreen? OldValue, UiScreen? NewValue)>();
        using var subscription = node.Subscribe(UiNode.ScreenProperty, (_, args) =>
        {
            Assert.Same(args.NewValue, node.Screen);
            changes.Add((args.OldValue, args.NewValue));
        });

        Assert.Empty(changes);
        secondParent.Children.Add(node);
        Assert.Empty(changes);

        second.Root = node;
        second.Root = node;
        Assert.Equal([(first, second)], changes);

        second.Root = null;
        Assert.Equal([(first, second), (second, null)], changes);

        subscription.Dispose();
        firstParent.Children.Add(node);
        Assert.Equal(2, changes.Count);
        Assert.Same(first, node.Screen);
    }

    [Fact]
    public void OwnershipChangesNotifyAfterTheCurrentNodeIsUpdated()
    {
        var node = new Panel();
        var first = new UiScreen();
        var second = new UiScreen();
        var changes = new List<(UiScreen? OldValue, UiScreen? NewValue)>();
        node.PropertyChanged += (_, args) =>
        {
            if (args.Property.Name != nameof(UiNode.Screen))
                return;

            var change = Assert.IsType<UiPropertyChangedEventArgs<UiScreen?>>(args);
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
        var first = new UiScreen(parent);
        var second = new UiScreen();
        var order = new List<string>();
        parent.PropertyChanged += (_, args) =>
        {
            if (args.Property.Name != nameof(UiNode.Screen))
                return;

            Assert.Same(second, parent.Screen);
            Assert.Same(first, child.Screen);
            order.Add("parent");
        };
        child.PropertyChanged += (_, args) =>
        {
            if (args.Property.Name != nameof(UiNode.Screen))
                return;

            Assert.Same(second, parent.Screen);
            Assert.Same(second, child.Screen);
            order.Add("child");
        };

        second.Root = parent;

        Assert.Equal(["parent", "child"], order);
    }
}