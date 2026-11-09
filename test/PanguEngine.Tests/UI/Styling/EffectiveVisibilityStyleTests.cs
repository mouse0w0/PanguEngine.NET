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
    public void ALocalWriteDuringStyleNotificationChangesEffectiveVisibilityOnlyOnce()
    {
        var node = new Panel();
        var property = Property.Register<Panel, int>(Guid.NewGuid().ToString(),
            onChanged: static (owner, _, _) => owner.Visibility = Visibility.Hidden);
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
    public void ClearingALocalValueDuringNotificationMaintainsTheCurrentStyleAndVisibility()
    {
        var node = new Panel { Visibility = Visibility.Hidden };
        var visibilityWasStyled = false;
        var property = Property.Register<Panel, int>(Guid.NewGuid().ToString(),
            onChanged: (owner, _, _) =>
            {
                visibilityWasStyled = owner.GetStyleValueSources(UIElement.VisibilityProperty).Count != 0;
                owner.ClearValue(UIElement.VisibilityProperty);
            });
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
        Assert.Equal(visibilityWasStyled ? Array.Empty<bool>() : [true, false], effectiveValues);
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
}
