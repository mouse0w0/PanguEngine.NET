using PanguEngine.UI;
using PanguEngine.UI.Controls;
using PanguEngine.UI.Styling;

namespace PanguEngine.Tests.UI.Styling;

public sealed class StructuralPseudoClassUpdateTests
{
    private static Panel Item(string? id = null)
    {
        var panel = new Panel { StyleId = id };
        panel.Classes.Add("item");
        return panel;
    }

    private static UIScene Style(Panel root, string css)
    {
        var scene = new UIScene(root);
        scene.SetBaseStyleSheets([]);
        scene.SetStyleSheets([StyleSheet.Parse(css)]);
        root.UpdateStyles();
        return scene;
    }

    [Theory]
    [InlineData(".item:nth-child(even)")]
    [InlineData(".unused, .item:nth-child(even)")]
    public void MoveRefreshesPositionStylesWithoutAnyCombinator(string selector)
    {
        var first = Item();
        var second = Item();
        var root = new Panel();
        root.Children.Add(first);
        root.Children.Add(second);
        Style(root, selector + " { opacity: 0.4; }");
        Assert.Equal(1d, first.Opacity);
        Assert.Equal(0.4, second.Opacity);

        root.Children.Move(0, 1);
        root.UpdateStyles();
        Assert.Equal(0.4, first.Opacity);
        Assert.Equal(1d, second.Opacity);
    }

    [Fact]
    public void InsertionAndRemovalRefreshExistingSiblingPositions()
    {
        var first = Item();
        var second = Item();
        var root = new Panel();
        root.Children.Add(first);
        root.Children.Add(second);
        Style(root, ".item:nth-child(even) { opacity: 0.4; }");
        var added = Item();

        root.Children.Insert(0, added);
        root.UpdateStyles();
        Assert.Equal(0.4, first.Opacity);
        Assert.Equal(1d, second.Opacity);

        root.Children.Remove(added);
        root.UpdateStyles();
        Assert.Equal(1d, first.Opacity);
        Assert.Equal(0.4, second.Opacity);
    }

    [Fact]
    public void AppendingAndRemovingTailRefreshReversePositions()
    {
        var first = Item();
        var second = Item();
        var root = new Panel();
        root.Children.Add(first);
        root.Children.Add(second);
        Style(root, ".item:nth-last-child(2) { opacity: 0.4; }");
        Assert.Equal(0.4, first.Opacity);
        Assert.Equal(1d, second.Opacity);

        var tail = Item();
        root.Children.Add(tail);
        root.UpdateStyles();
        Assert.Equal(1d, first.Opacity);
        Assert.Equal(0.4, second.Opacity);
        root.Children.Remove(tail);
        root.UpdateStyles();
        Assert.Equal(0.4, first.Opacity);
        Assert.Equal(1d, second.Opacity);
    }

    [Fact]
    public void ReplacingAndClearingChildrenRefreshesEmptyAndDetachedStyles()
    {
        var old = Item();
        var root = new Panel();
        root.Classes.Add("host");
        root.Children.Add(old);
        Style(root, ".item:only-child { opacity: 0.4; } .host:empty { opacity: 0.7; }");
        Assert.Equal(0.4, old.Opacity);
        Assert.Equal(1d, root.Opacity);

        var replacement = Item();
        root.Children[0] = replacement;
        root.UpdateStyles();
        old.UpdateStyles();
        Assert.Equal(0.4, replacement.Opacity);
        Assert.Equal(1d, old.Opacity);

        root.Children.Clear();
        root.UpdateStyles();
        replacement.UpdateStyles();
        Assert.Equal(0.7, root.Opacity);
        Assert.Equal(1d, replacement.Opacity);
    }

    [Fact]
    public void ActualSortingRefreshesFirstAndLastStyles()
    {
        var first = Item("b");
        var second = Item("a");
        var root = new Panel();
        root.Children.Add(first);
        root.Children.Add(second);
        Style(root, ".item:first-child { opacity: 0.4; } .item:last-child { opacity: 0.7; }");
        Assert.Equal(0.4, first.Opacity);
        Assert.Equal(0.7, second.Opacity);

        root.Children.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.StyleId, right.StyleId));
        root.UpdateStyles();
        Assert.Equal(0.7, first.Opacity);
        Assert.Equal(0.4, second.Opacity);
    }

    [Theory]
    [InlineData(".container:empty + Text")]
    [InlineData(".container:empty ~ Text")]
    public void EmptyChangeRefreshesFollowingSiblings(string selector)
    {
        var container = new Panel();
        container.Classes.Add("container");
        var target = new Text();
        var root = new Panel();
        root.Children.Add(container);
        root.Children.Add(target);
        Style(root, selector + " { opacity: 0.4; }");
        Assert.Equal(0.4, target.Opacity);

        container.Children.Add(new Panel());
        root.UpdateStyles();
        Assert.Equal(1d, target.Opacity);
        container.Children.Clear();
        root.UpdateStyles();
        Assert.Equal(0.4, target.Opacity);
    }

    [Theory]
    [InlineData(".item:nth-child(even) Text { opacity: 0.4; }")]
    [InlineData(".item:nth-child(even) { --alpha: 0.4; } Text { opacity: var(--alpha, 1); }")]
    public void PositionChangesRefreshDescendantValues(string css)
    {
        var first = Item();
        var second = Item();
        var firstText = new Text();
        var secondText = new Text();
        first.Children.Add(firstText);
        second.Children.Add(secondText);
        var root = new Panel();
        root.Children.Add(first);
        root.Children.Add(second);
        Style(root, css);
        Assert.Equal(1d, firstText.Opacity);
        Assert.Equal(0.4, secondText.Opacity);

        root.Children.Insert(0, new Panel());
        root.UpdateStyles();
        Assert.Equal(0.4, firstText.Opacity);
        Assert.Equal(1d, secondText.Opacity);

        root.Children.Move(1, 2);
        root.UpdateStyles();
        Assert.Equal(1d, firstText.Opacity);
        Assert.Equal(0.4, secondText.Opacity);
    }

    [Fact]
    public void ReparentingWithinSceneRefreshesBothSiblingSets()
    {
        var remaining = Item();
        var moving = Item();
        var existing = Item();
        var source = new Panel();
        var destination = new Panel();
        source.Children.Add(remaining);
        source.Children.Add(moving);
        destination.Children.Add(existing);
        var root = new Panel();
        root.Children.Add(source);
        root.Children.Add(destination);
        Style(root, ".item:only-child { opacity: 0.4; }");
        Assert.Equal(1d, remaining.Opacity);
        Assert.Equal(1d, moving.Opacity);
        Assert.Equal(0.4, existing.Opacity);

        destination.Children.Add(moving);
        root.UpdateStyles();
        Assert.Equal(0.4, remaining.Opacity);
        Assert.Equal(1d, moving.Opacity);
        Assert.Equal(1d, existing.Opacity);
    }

    [Fact]
    public void CrossSceneMoveUsesFinalRulesAndRefreshesSourcePositions()
    {
        var moving = Item();
        var remaining = Item();
        var source = new Panel();
        source.Children.Add(moving);
        source.Children.Add(remaining);
        Style(source, ".item:nth-child(even) { opacity: 0.4; }");
        var existing = Item();
        var destination = new Panel();
        destination.Children.Add(existing);
        var finalScene = Style(destination, ".item:first-child { opacity: 0.7; }");
        Assert.Equal(0.4, remaining.Opacity);
        Assert.Equal(0.7, existing.Opacity);

        destination.Children.Insert(0, moving);
        source.UpdateStyles();
        destination.UpdateStyles();
        Assert.Same(finalScene, moving.Scene);
        Assert.Equal(1d, remaining.Opacity);
        Assert.Equal(0.7, moving.Opacity);
        Assert.Equal(1d, existing.Opacity);
    }

    [Fact]
    public void DetachedNodeMatchesPositionOneInAnIndependentTree()
    {
        var root = new Panel();
        var first = Item();
        var detached = Item();
        root.Children.Add(first);
        root.Children.Add(detached);
        Style(root, ".item:nth-child(even) { opacity: 0.4; }");
        Assert.Equal(0.4, detached.Opacity);

        root.Children.Remove(detached);
        root.UpdateStyles();
        detached.UpdateStyles();
        Assert.Null(detached.Parent);
        Assert.Null(detached.Scene);
        Assert.Equal(1d, detached.Opacity);
        var selector = Assert.Single(StyleSheet.Parse(":nth-child(1):only-child { }").Rules).Selectors[0];
        Assert.True(selector.Matches(detached));
    }

    [Fact]
    public void PromotingAChildToSceneRootRefreshesItsFormerSiblings()
    {
        var moving = Item();
        var remaining = Item();
        var source = new Panel();
        source.Children.Add(moving);
        source.Children.Add(remaining);
        Style(source, ".item:nth-child(even) { opacity: 0.4; }");
        var destination = new UIScene();
        destination.SetBaseStyleSheets([]);
        destination.SetStyleSheets([StyleSheet.Parse(".item:nth-child(1) { opacity: 0.7; }")]);
        Assert.Equal(0.4, remaining.Opacity);

        destination.Root = moving;
        source.UpdateStyles();
        moving.UpdateStyles();
        Assert.Equal(1d, remaining.Opacity);
        Assert.Equal(0.7, moving.Opacity);
        Assert.Null(moving.Parent);
    }

    [Fact]
    public void ReplacingSheetsChangesTheStructuralRefreshRequirements()
    {
        var first = Item();
        var second = Item();
        var root = new Panel();
        root.Children.Add(first);
        root.Children.Add(second);
        var scene = Style(root, ".item { opacity: 0.8; }");
        scene.SetStyleSheets([StyleSheet.Parse(".item:first-child { opacity: 0.4; }")]);
        root.UpdateStyles();
        Assert.Equal(0.4, first.Opacity);
        Assert.Equal(1d, second.Opacity);

        root.Children.Move(0, 1);
        root.UpdateStyles();
        Assert.Equal(1d, first.Opacity);
        Assert.Equal(0.4, second.Opacity);
    }
}