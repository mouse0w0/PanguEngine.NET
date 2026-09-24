using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Styling;

namespace PanguEngine.Tests.Client.UI.Styling;

public sealed class UiCssVariableUpdateTests
{
    [Fact]
    public void AncestorClassAndIdChangesRefreshInheritedVariables()
    {
        var root = new Panel();
        var child = new Panel();
        root.Children.Add(child);
        var screen = new UiScreen(root);
        screen.SetStyleSheets([UiStyleSheet.Parse("""
            .theme { --n: 4px; }
            #large { --n: 8px; }
            Panel { padding: var(--n, 0); }
            """)]);
        root.Classes.Add("theme");
        Assert.Equal(new Thickness(4), child.Padding);
        root.StyleId = "large";
        Assert.Equal(new Thickness(8), child.Padding);
        root.StyleId = null;
        Assert.Equal(new Thickness(4), child.Padding);
        root.Classes.Remove("theme");
        Assert.Equal(Thickness.Zero, child.Padding);
    }

    [Fact]
    public void AncestorPseudoChangesRefreshInheritedPaintValues()
    {
        var root = new Panel { StyleId = "root" };
        var child = new Panel();
        root.Children.Add(child);
        var screen = new UiScreen(root);
        screen.SetStyleSheets([UiStyleSheet.Parse("""
            #root { --alpha: 0.8; }
            #root:hover { --alpha: 0.4; }
            Panel { opacity: var(--alpha); }
            """)]);
        Assert.Equal(0.8, child.Opacity);
        root.SetHovered(true);
        Assert.Equal(0.4, child.Opacity);
        root.SetHovered(false);
        Assert.Equal(0.8, child.Opacity);
    }

    [Fact]
    public void SameScreenReparentRefreshesAnEntireInheritedSubtree()
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
        var screen = new UiScreen(root);
        screen.SetStyleSheets([UiStyleSheet.Parse("""
            #first { --n: 4px; }
            #second { --n: 8px; }
            Panel { padding: var(--n, 0); }
            """)]);
        Assert.Equal(new Thickness(4), leaf.Padding);
        second.Children.Add(subtree);
        Assert.Equal(new Thickness(8), subtree.Padding);
        Assert.Equal(new Thickness(8), leaf.Padding);
        second.Children.Remove(subtree);
        Assert.Equal(Thickness.Zero, subtree.Padding);
        Assert.Equal(Thickness.Zero, leaf.Padding);
    }

    [Fact]
    public void CrossScreenMoveAndStylesheetReplacementUseNewVariableEnvironment()
    {
        var first = new Panel { StyleId = "root" };
        var second = new Panel { StyleId = "root" };
        var child = new Panel();
        first.Children.Add(child);
        var firstScreen = new UiScreen(first);
        var secondScreen = new UiScreen(second);
        firstScreen.SetStyleSheets([UiStyleSheet.Parse("#root { --n: 4px; } Panel { padding: var(--n); }")]);
        secondScreen.SetStyleSheets([UiStyleSheet.Parse("#root { --n: 8px; } Panel { padding: var(--n); }")]);
        second.Children.Add(child);
        Assert.Equal(new Thickness(8), child.Padding);
        secondScreen.SetStyleSheets([UiStyleSheet.Parse("#root { --n: 12px; } Panel { padding: var(--n); }")]);
        Assert.Equal(new Thickness(12), child.Padding);
        secondScreen.SetStyleSheets([]);
        Assert.Equal(Thickness.Zero, child.Padding);
    }

    [Fact]
    public void NotificationsObserveTheWholeNewBatchAndSkipEqualValues()
    {
        var root = new Panel();
        var first = new Panel();
        var second = new Panel();
        root.Children.Add(first);
        root.Children.Add(second);
        var screen = new UiScreen(root);
        screen.SetStyleSheets([UiStyleSheet.Parse("""
            .theme { --alpha: 0.4; }
            .theme.same { --alpha: 0.4; }
            Panel { opacity: var(--alpha, 1); }
            """)]);
        var notifications = 0;
        double? observed = null;
        first.PropertyChanged += (_, args) =>
        {
            if (!ReferenceEquals(args.Property, UiNode.OpacityProperty))
                return;
            notifications++;
            observed = second.Opacity;
        };
        root.Classes.Add("theme");
        Assert.Equal(0.4, observed);
        Assert.Equal(1, notifications);
        root.Classes.Add("same");
        Assert.Equal(1, notifications);
    }

    [Fact]
    public void LocalValuesMaskStylesButDoNotBlockVariableInheritance()
    {
        var root = new Panel { Opacity = 0.9 };
        var child = new Panel();
        root.Children.Add(child);
        var screen = new UiScreen(root);
        screen.SetStyleSheets([UiStyleSheet.Parse(".theme { --alpha: 0.4; } Panel { opacity: var(--alpha, 1); }")]);
        root.Classes.Add("theme");
        Assert.Equal(0.9, root.Opacity);
        Assert.Equal(0.4, child.Opacity);
        Assert.True(Assert.Single(root.GetStyleValueSources(UiNode.OpacityProperty)).IsMaskedByLocalValue);
        root.ClearValue(UiNode.OpacityProperty);
        Assert.Equal(0.4, root.Opacity);
    }

    [Fact]
    public void LazyPreparationCalculatesAncestorsWithTheRequestedResolver()
    {
        var root = new Panel { StyleId = "root" };
        var child = new Panel();
        root.Children.Add(child);
        var resolver = new UiStyleResolver([], [UiStyleSheet.Parse("""
            #root { --alpha: 0.4; opacity: 0.2; }
            Panel { opacity: var(--alpha, 1); }
            """)]);
        var notifications = 0;
        root.PropertyChanged += (_, _) => notifications++;
        var snapshot = child.ComputeStyleSnapshot(resolver);
        Assert.Equal(0.4, snapshot.GetValue(UiNode.OpacityProperty));
        Assert.Equal(1d, root.Opacity);
        Assert.Equal(0, notifications);
    }

    [Fact]
    public void BatchEntryOrderDoesNotChangeParentEnvironment()
    {
        var root = new Panel { StyleId = "root" };
        var child = new Panel();
        root.Children.Add(child);
        var resolver = new UiStyleResolver([], [UiStyleSheet.Parse("""
            #root { --alpha: 0.4; }
            Panel { opacity: var(--alpha, 1); }
            """)]);
        var batch = UiNode.PrepareStyleSubtreeBatch([(child, resolver), (root, resolver)]);
        batch.Commit();
        Assert.Equal(0.4, root.Opacity);
        Assert.Equal(0.4, child.Opacity);
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
        var screen = new UiScreen(root);
        screen.SetStyleSheets([UiStyleSheet.Parse(".leader + Panel { --alpha: 0.4; } Panel { opacity: var(--alpha, 1); }")]);
        leader.Classes.Add("leader");
        Assert.Equal(0.4, leaf.Opacity);
        root.Children.Move(0, 1);
        Assert.Equal(1d, leaf.Opacity);
    }

    [Fact]
    public void FailedStylesheetPreparationPreservesCommittedValues()
    {
        var root = new Panel();
        var child = new Panel();
        root.Children.Add(child);
        var screen = new UiScreen(root);
        screen.SetStyleSheets([UiStyleSheet.Parse("Panel { --n: 4px; padding: var(--n); }")]);
        Assert.Throws<UiStyleParseException>(() => screen.SetStyleSheets([
            UiStyleSheet.Parse("Panel { --n: var(--missing); padding: var(--n); }")]));
        Assert.Equal(new Thickness(4), root.Padding);
        Assert.Equal(new Thickness(4), child.Padding);
    }

    [Fact]
    public void BaseAndAuthorReplacementRebuildsVariablesFromCurrentSheets()
    {
        var root = new Panel();
        var child = new Panel();
        root.Children.Add(child);
        var screen = new UiScreen(root);
        screen.SetStyleSheets([UiStyleSheet.Parse("Panel { padding: 2px; }")]);
        Assert.Equal(new Thickness(2), child.Padding);
        screen.SetBaseStyleSheets([UiStyleSheet.Parse("Panel { --n: 4px; }")]);
        screen.SetStyleSheets([UiStyleSheet.Parse("Panel { padding: var(--n); }")]);
        Assert.Equal(new Thickness(4), child.Padding);
        screen.SetBaseStyleSheets([UiStyleSheet.Parse("Panel { --n: 8px; }")]);
        Assert.Equal(new Thickness(8), child.Padding);
        screen.SetStyleSheets([UiStyleSheet.Parse("Panel { --n: 12px; padding: var(--n); }")]);
        screen.SetBaseStyleSheets([UiStyleSheet.Parse("Panel { --n: 16px; }")]);
        Assert.Equal(new Thickness(12), child.Padding);
        screen.SetStyleSheets([UiStyleSheet.Parse("Panel { padding: var(--n); }")]);
        Assert.Equal(new Thickness(16), child.Padding);
    }
}
