using PanguEngine.UI;
using PanguEngine.UI.Controls;
using PanguEngine.UI.Styling;

namespace PanguEngine.Tests.UI.Styling;

public sealed class CssVariableUpdateTests
{
    [Theory]
    [InlineData("#root:hover #child { padding: 8px; }")]
    [InlineData("#root:hover { --spacing: 8px; } #child { padding: var(--spacing, 0); }")]
    public void PseudoChangesInvalidateChildLayoutAndRestoreValues(string css)
    {
        var root = new Panel { StyleId = "root" };
        var child = new Panel { StyleId = "child" };
        root.Children.Add(child);
        var scene = new UIScene(root);
        scene.SetStyleSheets([StyleSheet.Parse(css)]);
        scene.Open();
        try
        {
            scene.PrepareFrame(new Size(100, 100));
            Assert.True(child.IsMeasureValid);
            Assert.True(child.IsArrangeValid);

            root.SetHovered(true);
            scene.Root!.UpdateStyles();

            Assert.Equal(new Thickness(8), child.Padding);
            Assert.False(child.IsMeasureValid);
            Assert.False(child.IsArrangeValid);
            scene.PrepareFrame(new Size(100, 100));
            Assert.True(child.IsMeasureValid);
            Assert.True(child.IsArrangeValid);

            root.SetHovered(false);
            scene.Root!.UpdateStyles();

            Assert.Equal(Thickness.Zero, child.Padding);
            Assert.False(child.IsMeasureValid);
            Assert.False(child.IsArrangeValid);
            scene.PrepareFrame(new Size(100, 100));
            Assert.True(child.IsMeasureValid);
            Assert.True(child.IsArrangeValid);
        }
        finally
        {
            scene.Close();
        }
    }

    [Fact]
    public void PseudoChangesInvalidateArrangeOnlyProperties()
    {
        var child = new Panel { StyleId = "child" };
        var root = new Canvas();
        root.Children.Add(child);
        var scene = new UIScene(root);
        scene.SetStyleSheets([StyleSheet.Parse("#child { left: 0; } #child:hover { left: 8px; }")]);
        scene.Open();
        try
        {
            scene.PrepareFrame(new Size(100, 100));
            child.SetHovered(true);
            scene.Root!.UpdateStyles();
            Assert.Equal(8d, child.GetValue(Canvas.LeftProperty));
            Assert.True(child.IsMeasureValid);
            Assert.False(child.IsArrangeValid);
            scene.PrepareFrame(new Size(100, 100));
            Assert.True(child.IsArrangeValid);

            child.SetHovered(false);
            scene.Root!.UpdateStyles();
            Assert.Equal(0d, child.GetValue(Canvas.LeftProperty));
            Assert.True(child.IsMeasureValid);
            Assert.False(child.IsArrangeValid);
        }
        finally
        {
            scene.Close();
        }
    }

    [Fact]
    public void AncestorClassAndIdChangesRefreshInheritedVariables()
    {
        var root = new Panel();
        var child = new Panel();
        root.Children.Add(child);
        var scene = new UIScene(root);
        scene.SetStyleSheets([
            StyleSheet.Parse("""
                             .theme { --n: 4px; }
                             #large { --n: 8px; }
                             Panel { padding: var(--n, 0); }
                             """)
        ]);
        root.Classes.Add("theme");
        scene.Root!.UpdateStyles();
        Assert.Equal(new Thickness(4), child.Padding);
        root.StyleId = "large";
        scene.Root!.UpdateStyles();
        Assert.Equal(new Thickness(8), child.Padding);
        root.StyleId = null;
        scene.Root!.UpdateStyles();
        Assert.Equal(new Thickness(4), child.Padding);
        root.Classes.Remove("theme");
        scene.Root!.UpdateStyles();
        Assert.Equal(Thickness.Zero, child.Padding);
    }

    [Fact]
    public void AncestorPseudoChangesRefreshInheritedPaintValues()
    {
        var root = new Panel { StyleId = "root" };
        var child = new Panel();
        root.Children.Add(child);
        var scene = new UIScene(root);
        scene.SetStyleSheets([
            StyleSheet.Parse("""
                             #root { --alpha: 0.8; }
                             #root:hover { --alpha: 0.4; }
                             Panel { opacity: var(--alpha); }
                             """)
        ]);
        scene.Root!.UpdateStyles();
        Assert.Equal(0.8, child.Opacity);
        root.SetHovered(true);
        scene.Root!.UpdateStyles();
        Assert.Equal(0.4, child.Opacity);
        root.SetHovered(false);
        scene.Root!.UpdateStyles();
        Assert.Equal(0.8, child.Opacity);
    }

    [Fact]
    public void SameSceneReparentRefreshesAnEntireInheritedSubtree()
    {
        var first = new Panel { StyleId = "first" };
        var second = new Panel { StyleId = "second" };
        var subtree = new Panel();
        var leaf = new Panel();
        subtree.Children.Add(leaf);
        first.Children.Add(subtree);
        var root = new Panel();
        root.Children.Add(first);
        root.Children.Add(second);
        var scene = new UIScene(root);
        scene.SetStyleSheets([
            StyleSheet.Parse("""
                             #first { --n: 4px; }
                             #second { --n: 8px; }
                             Panel { padding: var(--n, 0); }
                             """)
        ]);
        scene.Root!.UpdateStyles();
        Assert.Equal(new Thickness(4), leaf.Padding);
        second.Children.Add(subtree);
        scene.Root!.UpdateStyles();
        Assert.Equal(new Thickness(8), subtree.Padding);
        Assert.Equal(new Thickness(8), leaf.Padding);
        second.Children.Remove(subtree);
        subtree.UpdateStyles();
        Assert.Equal(Thickness.Zero, subtree.Padding);
        Assert.Equal(Thickness.Zero, leaf.Padding);
    }

    [Fact]
    public void CrossSceneMoveAndStylesheetReplacementUseNewVariableEnvironment()
    {
        var first = new Panel { StyleId = "root" };
        var second = new Panel { StyleId = "root" };
        var child = new Panel();
        first.Children.Add(child);
        var firstScene = new UIScene(first);
        var secondScene = new UIScene(second);
        firstScene.SetStyleSheets([StyleSheet.Parse("#root { --n: 4px; } Panel { padding: var(--n); }")]);
        secondScene.SetStyleSheets([StyleSheet.Parse("#root { --n: 8px; } Panel { padding: var(--n); }")]);
        second.Children.Add(child);
        secondScene.Root!.UpdateStyles();
        Assert.Equal(new Thickness(8), child.Padding);
        secondScene.SetStyleSheets([StyleSheet.Parse("#root { --n: 12px; } Panel { padding: var(--n); }")]);
        secondScene.Root!.UpdateStyles();
        Assert.Equal(new Thickness(12), child.Padding);
        secondScene.SetStyleSheets([]);
        secondScene.Root!.UpdateStyles();
        Assert.Equal(Thickness.Zero, child.Padding);
    }

    [Fact]
    public void NotificationsObserveLaterNodesBeforeTheirUpdateAndSkipEqualValues()
    {
        var root = new Panel();
        var first = new Panel();
        var second = new Panel();
        root.Children.Add(first);
        root.Children.Add(second);
        var scene = new UIScene(root);
        scene.SetStyleSheets([
            StyleSheet.Parse("""
                             .theme { --alpha: 0.4; }
                             .theme.same { --alpha: 0.4; }
                             Panel { opacity: var(--alpha, 1); }
                             """)
        ]);
        var notifications = 0;
        double? observed = null;
        first.PropertyChanged += (_, args) =>
        {
            if (!ReferenceEquals(args.Property, UIElement.OpacityProperty))
                return;
            notifications++;
            observed = second.Opacity;
        };
        root.Classes.Add("theme");
        scene.Root!.UpdateStyles();
        Assert.Equal(1d, observed);
        Assert.Equal(0.4, second.Opacity);
        Assert.Equal(1, notifications);
        root.Classes.Add("same");
        scene.Root!.UpdateStyles();
        Assert.Equal(1, notifications);
    }

    [Fact]
    public void LocalValuesMaskStylesButDoNotBlockVariableInheritance()
    {
        var root = new Panel { Opacity = 0.9 };
        var child = new Panel();
        root.Children.Add(child);
        var scene = new UIScene(root);
        scene.SetStyleSheets([StyleSheet.Parse(".theme { --alpha: 0.4; } Panel { opacity: var(--alpha, 1); }")]);
        root.Classes.Add("theme");
        scene.Root!.UpdateStyles();
        Assert.Equal(0.9, root.Opacity);
        Assert.Equal(0.4, child.Opacity);
        Assert.True(Assert.Single(root.GetStyleValueSources(UIElement.OpacityProperty)).IsMaskedByLocalValue);
        root.ClearValue(UIElement.OpacityProperty);
        Assert.Equal(0.4, root.Opacity);
    }

    [Fact]
    public void ResolutionCalculatesAncestorsWithTheRequestedResolver()
    {
        var root = new Panel { StyleId = "root" };
        var child = new Panel();
        root.Children.Add(child);
        var resolver = new StyleResolver([], [
            StyleSheet.Parse("""
                             #root { --alpha: 0.4; opacity: 0.2; }
                             Panel { opacity: var(--alpha, 1); }
                             """)
        ]);
        var notifications = 0;
        root.PropertyChanged += (_, _) => notifications++;
        var snapshot = resolver.Resolve(child);
        Assert.Equal(0.4, snapshot.GetValue(UIElement.OpacityProperty));
        Assert.Equal(1d, root.Opacity);
        Assert.Equal(0, notifications);
    }

    [Fact]
    public void LaterNodesUseVariablesChangedByEarlierNotifications()
    {
        var root = new Panel();
        var first = new Panel();
        var last = new Panel();
        root.Children.Add(first);
        root.Children.Add(last);
        var scene = new UIScene(root);
        scene.SetStyleSheets([
            StyleSheet.Parse("""
                             :root { --alpha: 0.4; }
                             :root.changed { --alpha: 0.7; }
                             Panel { opacity: var(--alpha, 1); }
                             """)
        ]);
        first.PropertyChanged += (_, args) =>
        {
            if (args.Property == UIElement.OpacityProperty)
                root.Classes.Add("changed");
        };

        scene.Root!.UpdateStyles();

        Assert.Equal(0.4, root.Opacity);
        Assert.Equal(0.4, first.Opacity);
        Assert.Equal(0.7, last.Opacity);
        Assert.False(root.IsStyleSubtreeValid);

        scene.Root!.UpdateStyles();

        Assert.Equal(0.7, root.Opacity);
        Assert.Equal(0.7, first.Opacity);
        Assert.True(root.IsStyleSubtreeValid);
    }

    [Fact]
    public void SiblingSelectorVariablesPropagateToDescendants()
    {
        var leader = new Panel();
        var follower = new Panel();
        var leaf = new Panel();
        follower.Children.Add(leaf);
        var root = new Panel();
        root.Children.Add(leader);
        root.Children.Add(follower);
        var scene = new UIScene(root);
        scene.SetStyleSheets([
            StyleSheet.Parse(".leader + Panel { --alpha: 0.4; } Panel { opacity: var(--alpha, 1); }")
        ]);
        leader.Classes.Add("leader");
        scene.Root!.UpdateStyles();
        Assert.Equal(0.4, leaf.Opacity);
        root.Children.Move(0, 1);
        scene.Root!.UpdateStyles();
        Assert.Equal(1d, leaf.Opacity);
    }

    [Fact]
    public void FailedRootCalculationPreservesPreviousValues()
    {
        var root = new Panel();
        var child = new Panel();
        root.Children.Add(child);
        var scene = new UIScene(root);
        scene.SetStyleSheets([StyleSheet.Parse("Panel { --n: 4px; padding: var(--n); }")]);
        scene.Root!.UpdateStyles();
        scene.SetStyleSheets([
            StyleSheet.Parse("Panel { --n: var(--missing); padding: var(--n); }")
        ]);
        Assert.Throws<StyleParseException>(scene.Root!.UpdateStyles);
        Assert.Equal(new Thickness(4), root.Padding);
        Assert.Equal(new Thickness(4), child.Padding);
    }

    [Fact]
    public void BaseAndAuthorReplacementRebuildsVariablesFromCurrentSheets()
    {
        var root = new Panel();
        var child = new Panel();
        root.Children.Add(child);
        var scene = new UIScene(root);
        scene.SetStyleSheets([StyleSheet.Parse("Panel { padding: 2px; }")]);
        scene.Root!.UpdateStyles();
        Assert.Equal(new Thickness(2), child.Padding);
        scene.SetBaseStyleSheets([StyleSheet.Parse("Panel { --n: 4px; }")]);
        scene.SetStyleSheets([StyleSheet.Parse("Panel { padding: var(--n); }")]);
        scene.Root!.UpdateStyles();
        Assert.Equal(new Thickness(4), child.Padding);
        scene.SetBaseStyleSheets([StyleSheet.Parse("Panel { --n: 8px; }")]);
        scene.Root!.UpdateStyles();
        Assert.Equal(new Thickness(8), child.Padding);
        scene.SetStyleSheets([StyleSheet.Parse("Panel { --n: 12px; padding: var(--n); }")]);
        scene.SetBaseStyleSheets([StyleSheet.Parse("Panel { --n: 16px; }")]);
        scene.Root!.UpdateStyles();
        Assert.Equal(new Thickness(12), child.Padding);
        scene.SetStyleSheets([StyleSheet.Parse("Panel { padding: var(--n); }")]);
        scene.Root!.UpdateStyles();
        Assert.Equal(new Thickness(16), child.Padding);
    }
}