using PanguEngine.UI;
using PanguEngine.UI.Controls;

namespace PanguEngine.Tests.UI;

public sealed class EffectiveVisibilityTests
{
    [Fact]
    public void DescriptorAndBindingReadTheCurrentEffectiveState()
    {
        var parent = new Panel();
        var child = new Panel();
        var target = new Panel();
        parent.Children.Add(child);

        Assert.True(child.IsEffectivelyVisible);
        Assert.True(UIElement.IsEffectivelyVisibleProperty.IsReadOnly);
        Assert.True(UIElement.IsEffectivelyVisibleProperty.IsDirect);
        Assert.Equal(true, UIElement.IsEffectivelyVisibleProperty.DefaultValue);

        target.Bind(UIElement.IsEnabledProperty, child, UIElement.IsEffectivelyVisibleProperty);
        parent.Visibility = Visibility.Hidden;

        Assert.False(child.GetValue(UIElement.IsEffectivelyVisibleProperty));
        Assert.False(target.IsEnabled);

        parent.ClearValue(UIElement.VisibilityProperty);

        Assert.True(child.IsEffectivelyVisible);
        Assert.True(target.IsEnabled);
    }

    [Fact]
    public void AncestorChangesPreserveLocalSettingsAndSkipUnchangedStates()
    {
        var root = new Panel();
        var shown = new Panel();
        var shownLeaf = new Panel();
        var hidden = new Panel { Visibility = Visibility.Hidden };
        var leaf = new Panel();
        root.Children.AddRange([shown, hidden]);
        shown.Children.Add(shownLeaf);
        hidden.Children.Add(leaf);
        var shownChanges = new List<bool>();
        var hiddenChanges = 0;
        var descendantValuesAtRootNotifications = new List<bool>();
        using var first = shownLeaf.Subscribe(UIElement.IsEffectivelyVisibleProperty,
            (_, args) => shownChanges.Add(args.NewValue));
        using var second = leaf.Subscribe(UIElement.IsEffectivelyVisibleProperty,
            (_, _) => hiddenChanges++);
        using var rootSubscription = root.Subscribe(UIElement.IsEffectivelyVisibleProperty,
            (_, _) => descendantValuesAtRootNotifications.Add(shownLeaf.IsEffectivelyVisible));

        root.Visibility = Visibility.Hidden;
        root.Visibility = Visibility.Collapsed;
        hidden.Visibility = Visibility.Collapsed;
        root.Visibility = Visibility.Visible;

        Assert.Equal(new[] { false, true }, shownChanges);
        Assert.Equal(new[] { true, false }, descendantValuesAtRootNotifications);
        Assert.Equal(0, hiddenChanges);
        Assert.Equal(Visibility.Visible, shown.Visibility);
        Assert.Equal(Visibility.Collapsed, hidden.Visibility);
        Assert.True(shownLeaf.IsEffectivelyVisible);
        Assert.False(leaf.IsEffectivelyVisible);
    }

    [Fact]
    public void InheritanceCallbacksRunBeforeEffectiveVisibilityUpdates()
    {
        var parent = new Panel { Visibility = Visibility.Hidden, FontSize = 30 };
        var child = new Panel();
        var leaf = new Panel();
        child.Children.Add(leaf);
        var order = new List<string>();
        using var inherited = child.Subscribe(UIElement.FontSizeProperty, (_, _) =>
        {
            Assert.True(child.IsEffectivelyVisible);
            Assert.True(leaf.IsEffectivelyVisible);
            order.Add("inherited");
        });
        using var effective = child.Subscribe(UIElement.IsEffectivelyVisibleProperty,
            (_, _) =>
            {
                Assert.False(child.IsEffectivelyVisible);
                Assert.True(leaf.IsEffectivelyVisible);
                order.Add("effective");
            });
        using var relationship = child.Subscribe(UIElement.ParentProperty,
            (_, _) => order.Add("parent"));

        parent.Children.Add(child);

        Assert.Equal(new[] { "inherited", "effective", "parent" }, order);
        Assert.False(leaf.IsEffectivelyVisible);
    }

    [Fact]
    public void MigratingAnUnvisitedNodeDoesNotEmitDeferredTransitions()
    {
        var root = new Panel();
        var child = new Panel();
        var destination = new Panel();
        var bound = new Panel();
        root.Children.Add(child);
        bound.Bind(UIElement.IsEnabledProperty, child, UIElement.IsEffectivelyVisibleProperty);
        var childNotifications = 0;
        using var rootSubscription = root.Subscribe(UIElement.IsEffectivelyVisibleProperty, (_, args) =>
        {
            if (!args.NewValue)
                destination.Children.Add(child);
        });
        using var childSubscription = child.Subscribe(UIElement.IsEffectivelyVisibleProperty,
            (_, _) => childNotifications++);

        root.Visibility = Visibility.Hidden;

        Assert.Same(destination, child.Parent);
        Assert.True(child.IsEffectivelyVisible);
        Assert.True(bound.IsEnabled);
        Assert.Equal(0, childNotifications);
    }

    [Fact]
    public void NotificationFailureStopsDescendantUpdates()
    {
        var root = new Panel();
        var child = new Panel();
        root.Children.Add(child);
        var error = new InvalidOperationException("effective notification");
        var childNotifications = 0;
        using var first = root.Subscribe(UIElement.IsEffectivelyVisibleProperty, (_, _) => throw error);
        using var second = child.Subscribe(UIElement.IsEffectivelyVisibleProperty,
            (_, _) => childNotifications++);

        Assert.Same(error, Assert.Throws<InvalidOperationException>(() => root.Visibility = Visibility.Hidden));

        Assert.False(root.IsEffectivelyVisible);
        Assert.True(child.IsEffectivelyVisible);
        Assert.Equal(0, childNotifications);
    }

    [Fact]
    public void DescendantCallbacksCanMoveAnUnvisitedSibling()
    {
        var root = new Panel();
        var first = new Panel();
        var second = new Panel();
        var third = new Panel();
        var destination = new Panel();
        root.Children.AddRange([first, second, third]);
        UIElement? migrated = null;
        using var firstSubscription = first.Subscribe(UIElement.IsEffectivelyVisibleProperty,
            (_, _) => MovePendingSibling());
        using var secondSubscription = second.Subscribe(UIElement.IsEffectivelyVisibleProperty,
            (_, _) => MovePendingSibling());
        using var thirdSubscription = third.Subscribe(UIElement.IsEffectivelyVisibleProperty,
            (_, _) => MovePendingSibling());

        root.Visibility = Visibility.Hidden;

        Assert.NotNull(migrated);
        Assert.Same(destination, migrated.Parent);
        Assert.True(migrated.IsEffectivelyVisible);
        Assert.All(root.Children, child => Assert.False(child.IsEffectivelyVisible));

        void MovePendingSibling()
        {
            if (migrated is not null)
                return;
            migrated = root.Children.First(child => child.IsEffectivelyVisible);
            destination.Children.Add(migrated);
        }
    }

    [Fact]
    public void PendingRemovalsKeepTheirCacheUntilTheirParentIsUpdated()
    {
        var parent = new Panel();
        var first = new Panel();
        var second = new Panel();
        parent.Children.AddRange([first, second]);
        var notifications = 0;
        using var pending = second.Subscribe(UIElement.IsEffectivelyVisibleProperty,
            (_, _) => notifications++);
        using var removal = first.Subscribe(UIElement.ParentProperty, (_, _) =>
        {
            parent.Visibility = Visibility.Hidden;
            Assert.Same(parent, second.Parent);
            Assert.True(second.IsEffectivelyVisible);
        });

        parent.Children.Clear();

        Assert.Null(second.Parent);
        Assert.True(second.IsEffectivelyVisible);
        Assert.Equal(0, notifications);
    }

    [Fact]
    public void SceneOwnershipIsNotAVisibilityConstraint()
    {
        var root = new Panel();
        var child = new Panel();
        root.Children.Add(child);
        var first = new UIScene();
        var second = new UIScene();
        var notifications = 0;
        using var subscription = child.Subscribe(UIElement.IsEffectivelyVisibleProperty,
            (_, _) => notifications++);

        first.Root = root;
        second.Root = root;
        second.Root = null;

        Assert.True(root.IsEffectivelyVisible);
        Assert.True(child.IsEffectivelyVisible);
        Assert.Equal(0, notifications);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HiddenParentTransfersExposeTheDetachedIntermediateState(bool crossScene)
    {
        var source = new Panel { Visibility = Visibility.Hidden };
        var destination = new Panel { Visibility = Visibility.Hidden };
        var child = new Panel();
        UIScene scene;
        if (crossScene)
        {
            scene = new UIScene(source);
            _ = new UIScene(destination);
        }
        else
        {
            var root = new Panel();
            root.Children.AddRange([source, destination]);
            scene = new UIScene(root);
        }

        source.Children.Add(child);
        var changes = new List<(bool Old, bool New, UIScene? Scene)>();
        using var subscription = child.Subscribe(UIElement.IsEffectivelyVisibleProperty,
            (_, args) => changes.Add((args.OldValue, args.NewValue, child.Scene)));

        destination.Children.Add(child);

        Assert.Equal(new (bool, bool, UIScene?)[] { (false, true, scene), (true, false, null) }, changes);
        Assert.False(child.IsEffectivelyVisible);
    }
}
