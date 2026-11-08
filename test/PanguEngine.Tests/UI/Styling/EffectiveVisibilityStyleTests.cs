using PanguEngine.ComponentModel;
using PanguEngine.UI;
using PanguEngine.UI.Controls;
using PanguEngine.UI.Styling;

namespace PanguEngine.Tests.UI.Styling;

public sealed class EffectiveVisibilityStyleTests
{
    [Fact]
    public void StyleRootChangesDoNotLeaveEffectiveVisibilityStale()
    {
        var root = new Panel();
        var group = new Panel();
        var child = new Panel();
        var destination = new Panel();
        child.Classes.Add("target");
        root.Children.Add(group);
        group.Children.Add(child);
        var scene = new UIScene(root);
        _ = new UIScene(destination);
        scene.SetStyleSheets([StyleSheet.Parse(".target { width: 17; visibility: hidden; }")]);
        var effectiveValues = new List<bool>();
        using var visibility = child.Subscribe(UIElement.IsEffectivelyVisibleProperty,
            (_, args) => effectiveValues.Add(args.NewValue));
        using var width = child.Subscribe(UIElement.WidthProperty, (_, _) =>
        {
            destination.Children.Add(group);
        });

        root.UpdateStyles();

        Assert.False(child.IsEffectivelyVisible);
        Assert.Same(destination, group.Parent);
        Assert.Equal(new[] { false }, effectiveValues);
    }

    [Fact]
    public void ALocalWriteDuringStyleComparisonChangesEffectiveVisibilityOnlyOnce()
    {
        var node = new Panel();
        var comparer = new ReentrantComparer(() => node.Visibility = Visibility.Hidden);
        var property = Property.Register<Panel, int>(Guid.NewGuid().ToString(), comparer: comparer);
        var scene = new UIScene(node);
        scene.SetStyleSheets([
            new StyleSheet([
                new StyleRule(StyleSelector.For<Panel>(), [
                    StyleSetter.Create(UIElement.VisibilityProperty, Visibility.Hidden),
                    StyleSetter.Create(property, 1)
                ])
            ])
        ]);
        var effectiveValues = new List<bool>();
        using var subscription = node.Subscribe(UIElement.IsEffectivelyVisibleProperty,
            (_, args) => effectiveValues.Add(args.NewValue));

        node.UpdateStyles();

        Assert.False(node.IsEffectivelyVisible);
        Assert.Equal(new[] { false }, effectiveValues);
    }

    [Fact]
    public void ClearingALocalValueDuringComparisonStillMaintainsTheCommittedStyle()
    {
        var node = new Panel { Visibility = Visibility.Hidden };
        var comparer = new ReentrantComparer(() => node.ClearValue(UIElement.VisibilityProperty));
        var property = Property.Register<Panel, int>(Guid.NewGuid().ToString(), comparer: comparer);
        var scene = new UIScene(node);
        scene.SetStyleSheets([
            new StyleSheet([
                new StyleRule(StyleSelector.For<Panel>(), [
                    StyleSetter.Create(UIElement.VisibilityProperty, Visibility.Hidden),
                    StyleSetter.Create(property, 1)
                ])
            ])
        ]);
        var effectiveValues = new List<bool>();
        using var subscription = node.Subscribe(UIElement.IsEffectivelyVisibleProperty,
            (_, args) => effectiveValues.Add(args.NewValue));

        node.UpdateStyles();

        Assert.Equal(Visibility.Hidden, node.Visibility);
        Assert.False(node.IsEffectivelyVisible);
        Assert.Equal(new[] { true, false }, effectiveValues);
    }

    [Fact]
    public void RemovingTheHidingStyleRestoresEffectiveVisibilityOnce()
    {
        var node = new Panel();
        var scene = new UIScene(node);
        scene.SetStyleSheets([StyleSheet.Parse("Panel { visibility: hidden; }")]);
        node.UpdateStyles();
        Assert.False(node.IsEffectivelyVisible);
        var changes = new List<(bool Old, bool New)>();
        using var subscription = node.Subscribe(UIElement.IsEffectivelyVisibleProperty,
            (_, args) => changes.Add((args.OldValue, args.NewValue)));

        scene.SetStyleSheets([]);
        node.UpdateStyles();

        Assert.True(node.IsEffectivelyVisible);
        Assert.Equal(new[] { (false, true) }, changes);
    }

    private sealed class ReentrantComparer(Action callback) : IEqualityComparer<int>
    {
        private Action? _callback = callback;

        public bool Equals(int x, int y)
        {
            var callback = _callback;
            _callback = null;
            callback?.Invoke();
            return x == y;
        }

        public int GetHashCode(int value) => value;
    }
}
