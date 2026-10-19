using PanguEngine.ComponentModel;
using PanguEngine.UI;
using PanguEngine.UI.Controls;
using PanguEngine.UI.Styling;

namespace PanguEngine.Tests.UI.Styling;

public sealed class StyleCombinatorUpdateTests
{
    private static Panel MakePanel(params string[] classes)
    {
        var panel = new Panel();
        foreach (var className in classes)
            panel.Classes.Add(className);
        return panel;
    }

    [Theory]
    [InlineData(".host .target")]
    [InlineData(".unused, .host .target")]
    public void AncestorClassChangeRefreshesDescendantStyles(string selector)
    {
        var host = MakePanel();
        var target = MakePanel("target");
        host.Children.Add(target);
        var scene = new UIScene(host);
        scene.SetStyleSheets([StyleSheet.Parse(selector + " { opacity: 0.4; }")]);

        Assert.Equal(1d, target.Opacity);

        host.Classes.Add("host");
        scene.Root!.UpdateStyles();
        Assert.Equal(0.4, target.Opacity);

        host.Classes.Remove("host");
        scene.Root!.UpdateStyles();
        Assert.Equal(1d, target.Opacity);
    }

    [Fact]
    public void SelectorListAggregatesSiblingRefreshRange()
    {
        var first = MakePanel();
        var second = MakePanel("target");
        var root = MakePanel();
        root.Children.Add(first);
        root.Children.Add(second);
        var scene = new UIScene(root);
        scene.SetStyleSheets([
            StyleSheet.Parse(
                ".unused, .leader + .target { opacity: 0.4; }")
        ]);

        first.Classes.Add("leader");
        scene.Root!.UpdateStyles();

        Assert.Equal(0.4, second.Opacity);
    }

    [Fact]
    public void SiblingClassChangeRefreshesFollowingSiblings()
    {
        var first = MakePanel();
        var second = MakePanel("target");
        var root = MakePanel();
        root.Children.Add(first);
        root.Children.Add(second);
        var scene = new UIScene(root);
        scene.SetStyleSheets([StyleSheet.Parse(".leader + .target { opacity: 0.4; }")]);

        Assert.Equal(1d, second.Opacity);

        first.Classes.Add("leader");
        scene.Root!.UpdateStyles();
        Assert.Equal(0.4, second.Opacity);

        first.Classes.Remove("leader");
        scene.Root!.UpdateStyles();
        Assert.Equal(1d, second.Opacity);
    }

    [Theory]
    [InlineData("#menu .target")]
    [InlineData(".unused, #menu .target")]
    public void AncestorIdChangeRefreshesDescendants(string selector)
    {
        var host = MakePanel();
        var target = MakePanel("target");
        host.Children.Add(target);
        var scene = new UIScene(host);
        scene.SetStyleSheets([StyleSheet.Parse(selector + " { opacity: 0.4; }")]);

        Assert.Equal(1d, target.Opacity);

        host.StyleId = "menu";
        scene.Root!.UpdateStyles();
        Assert.Equal(0.4, target.Opacity);
    }

    [Theory]
    [InlineData(".host:loading .target")]
    [InlineData(".unused, .host:loading .target")]
    public void AncestorPseudoClassChangeRefreshesDescendants(string selector)
    {
        var host = new PseudoPanel("host");
        var target = MakePanel("target");
        host.Children.Add(target);
        var scene = new UIScene(host);
        scene.SetStyleSheets([StyleSheet.Parse(selector + " { opacity: 0.4; }")]);

        Assert.Equal(1d, target.Opacity);

        host.Set("loading", true);
        scene.Root!.UpdateStyles();
        Assert.Equal(0.4, target.Opacity);

        host.Set("loading", false);
        scene.Root!.UpdateStyles();
        Assert.Equal(1d, target.Opacity);
    }

    [Theory]
    [InlineData("")]
    [InlineData(".unused, ")]
    public void SameSceneReparentRefreshesBothParents(string prefix)
    {
        var source = MakePanel("source");
        var destination = MakePanel("destination");
        var target = MakePanel("target");
        source.Children.Add(target);
        var root = MakePanel();
        root.Children.Add(source);
        root.Children.Add(destination);
        var scene = new UIScene(root);
        scene.SetStyleSheets([
            StyleSheet.Parse(
                prefix + ".source > .target { opacity: 0.4; }" +
                prefix + ".destination > .target { opacity: 0.7; }")
        ]);
        scene.Root!.UpdateStyles();

        Assert.Equal(0.4, target.Opacity);

        destination.Children.Add(target);
        scene.Root!.UpdateStyles();

        Assert.Same(destination, target.Parent);
        Assert.Equal(0.7, target.Opacity);
    }

    [Fact]
    public void CrossSceneMoveUsesTheFinalSceneResolver()
    {
        var host = MakePanel("host");
        var target = MakePanel("target");
        host.Children.Add(target);
        var first = new UIScene(host);
        first.SetStyleSheets([StyleSheet.Parse(".host .target { opacity: 0.4; }")]);
        first.Root!.UpdateStyles();
        Assert.Equal(0.4, target.Opacity);

        var secondHost = MakePanel();
        var second = new UIScene(secondHost);
        second.SetStyleSheets([StyleSheet.Parse(".target { opacity: 0.7; }")]);

        secondHost.Children.Add(target);
        second.Root!.UpdateStyles();

        Assert.Same(second, target.Scene);
        Assert.Equal(0.7, target.Opacity);
    }

    [Theory]
    [InlineData("")]
    [InlineData(".unused, ")]
    public void PromotingAChildToAnotherSceneRootRefreshesTheOldParent(string prefix)
    {
        var leader = MakePanel("leader");
        var target = MakePanel("target");
        var root = MakePanel();
        root.Children.Add(leader);
        root.Children.Add(target);
        var scene = new UIScene(root);
        scene.SetStyleSheets([StyleSheet.Parse(prefix + ".leader + .target { opacity: 0.4; }")]);
        scene.Root!.UpdateStyles();
        Assert.Equal(0.4, target.Opacity);

        var other = new UIScene();
        other.Root = leader;
        scene.Root!.UpdateStyles();
        other.Root!.UpdateStyles();

        Assert.Same(other, leader.Scene);
        Assert.DoesNotContain(leader, root.Children);
        Assert.Equal(1d, target.Opacity);
    }

    [Theory]
    [InlineData("")]
    [InlineData(".unused, ")]
    public void InsertingAPrecedingSiblingRefreshesFollowingSibling(string prefix)
    {
        var target = MakePanel("target");
        var root = MakePanel();
        root.Children.Add(target);
        var scene = new UIScene(root);
        scene.SetStyleSheets([StyleSheet.Parse(prefix + ".leader + .target { opacity: 0.4; }")]);
        Assert.Equal(1d, target.Opacity);

        var leader = MakePanel("leader");
        root.Children.Insert(0, leader);
        scene.Root!.UpdateStyles();

        Assert.Equal(0.4, target.Opacity);
    }

    [Theory]
    [InlineData("")]
    [InlineData(".unused, ")]
    public void RemovingAPrecedingSiblingRefreshesTheRemainingSibling(string prefix)
    {
        var leader = MakePanel("leader");
        var target = MakePanel("target");
        var root = MakePanel();
        root.Children.Add(leader);
        root.Children.Add(target);
        var scene = new UIScene(root);
        scene.SetStyleSheets([StyleSheet.Parse(prefix + ".leader + .target { opacity: 0.4; }")]);
        scene.Root!.UpdateStyles();
        Assert.Equal(0.4, target.Opacity);

        root.Children.Remove(leader);
        scene.Root!.UpdateStyles();

        Assert.Equal(1d, target.Opacity);
    }

    [Theory]
    [InlineData("")]
    [InlineData(".unused, ")]
    public void ReplacingAPrecedingSiblingRefreshesTheFollowingSibling(string prefix)
    {
        var old = MakePanel();
        var target = MakePanel("target");
        var root = MakePanel();
        root.Children.Add(old);
        root.Children.Add(target);
        var scene = new UIScene(root);
        scene.SetStyleSheets([StyleSheet.Parse(prefix + ".leader + .target { opacity: 0.4; }")]);
        Assert.Equal(1d, target.Opacity);

        root.Children[0] = MakePanel("leader");
        scene.Root!.UpdateStyles();

        Assert.Equal(0.4, target.Opacity);
    }

    [Theory]
    [InlineData("")]
    [InlineData(".unused, ")]
    public void ClearingChildrenResolvesMovedOutSubtreesWithTheDefaultResolver(string prefix)
    {
        var host = MakePanel("host");
        var child = MakePanel("target");
        host.Children.Add(child);
        var root = MakePanel();
        root.Children.Add(host);
        var scene = new UIScene(root);
        scene.SetStyleSheets([StyleSheet.Parse(prefix + ".host .target { opacity: 0.4; }")]);
        scene.Root!.UpdateStyles();
        Assert.Equal(0.4, child.Opacity);

        host.Children.Clear();
        child.UpdateStyles();

        Assert.Null(child.Parent);
        Assert.Null(child.Scene);
        Assert.Equal(1d, child.Opacity);
    }

    [Theory]
    [InlineData("")]
    [InlineData(".unused, ")]
    public void ReorderingSiblingsRefreshesSiblingRules(string prefix)
    {
        var leader = MakePanel("leader");
        var target = MakePanel("target");
        var root = MakePanel();
        root.Children.Add(leader);
        root.Children.Add(target);
        var scene = new UIScene(root);
        scene.SetStyleSheets([StyleSheet.Parse(prefix + ".leader + .target { opacity: 0.4; }")]);
        scene.Root!.UpdateStyles();
        Assert.Equal(0.4, target.Opacity);

        root.Children.Move(1, 0);
        scene.Root!.UpdateStyles();

        Assert.Equal(1d, target.Opacity);
    }

    [Fact]
    public void MoveToFrontAndMoveToBackRefreshSiblingRules()
    {
        var leader = MakePanel("leader");
        var target = MakePanel("target");
        var root = MakePanel();
        root.Children.Add(leader);
        root.Children.Add(target);
        var scene = new UIScene(root);
        scene.SetStyleSheets([StyleSheet.Parse(".leader + .target { opacity: 0.4; }")]);

        target.MoveToFront();
        scene.Root!.UpdateStyles();
        Assert.Equal(0.4, target.Opacity);

        leader.MoveToFront();
        scene.Root!.UpdateStyles();
        Assert.Equal(1d, target.Opacity);

        leader.MoveToBack();
        scene.Root!.UpdateStyles();
        Assert.Equal(0.4, target.Opacity);
    }

    [Fact]
    public void OverlappingRefreshRangesNotifyOnce()
    {
        var a = MakePanel("a");
        var target = MakePanel("target");
        a.Children.Add(target);
        var root = MakePanel();
        root.Children.Add(a);
        var scene = new UIScene(root);
        scene.SetStyleSheets([StyleSheet.Parse(".a + .target { opacity: 0.4; }")]);

        var notifications = 0;
        target.PropertyChanged += (_, e) =>
            notifications += ReferenceEquals(e.Property, UIElement.OpacityProperty) ? 1 : 0;

        root.Children.Add(target);

        scene.Root!.UpdateStyles();
        Assert.Equal(0.4, target.Opacity);
        Assert.Equal(1, notifications);

        Assert.True(target.Classes.Add("probe"));
    }

    [Fact]
    public void EachNodeNotifiesBeforeLaterNodesAreUpdated()
    {
        var host = MakePanel("host");
        var first = MakePanel("first");
        var second = MakePanel("second");
        host.Children.Add(first);
        host.Children.Add(second);
        var scene = new UIScene(host);
        scene.SetStyleSheets([
            StyleSheet.Parse("""
                             .host .first { opacity: 0.4; }
                             .host .second { opacity: 0.7; }
                             """)
        ]);
        scene.Root!.UpdateStyles();
        Assert.Equal(0.4, first.Opacity);
        Assert.Equal(0.7, second.Opacity);

        host.Classes.Remove("host");
        scene.Root!.UpdateStyles();
        Assert.Equal(1d, first.Opacity);
        Assert.Equal(1d, second.Opacity);

        double? observed = null;
        first.PropertyChanged += (_, e) =>
        {
            if (ReferenceEquals(e.Property, UIElement.OpacityProperty))
                observed ??= second.Opacity;
        };

        host.Classes.Add("host");
        scene.Root!.UpdateStyles();

        Assert.Equal(1d, observed);
        Assert.Equal(0.7, second.Opacity);
    }

    [Fact]
    public void ReentrantAncestorMutationRemainsPendingUntilTheNextApplication()
    {
        var host = MakePanel();
        var target = MakePanel("target");
        host.Children.Add(target);
        var scene = new UIScene(host);
        scene.SetStyleSheets([
            StyleSheet.Parse("""
                             .host .target { opacity: 0.4; }
                             .host.extra .target { opacity: 0.9; }
                             """)
        ]);

        var notifications = 0;
        target.PropertyChanged += (_, e) =>
        {
            if (!ReferenceEquals(e.Property, UIElement.OpacityProperty))
                return;
            notifications++;
            if (!host.Classes.Contains("extra"))
                host.Classes.Add("extra");
        };

        host.Classes.Add("host");
        scene.Root!.UpdateStyles();

        Assert.Equal(0.4, target.Opacity);
        Assert.Equal(1, notifications);
        scene.Root!.UpdateStyles();

        Assert.Equal(0.9, target.Opacity);
        Assert.True(host.Classes.SetEquals(["host", "extra"]));
        Assert.Equal(2, notifications);
    }

    [Fact]
    public void ClassChangeRemainsCommittedOnCalculationFailure()
    {
        var host = MakePanel();
        var guard = new GuardedNode();
        host.Children.Add(guard);
        var scene = new UIScene(host);
        scene.SetStyleSheets([StyleSheet.Parse(".host .guard { guarded: risky; }")]);

        Assert.Empty(guard.GetStyleValueSources(GuardedNode.ValueProperty));

        GuardedValue.ThrowOnCompare = true;
        try
        {
            host.Classes.Add("host");
            Assert.Throws<InvalidOperationException>(scene.Root!.UpdateStyles);
        }
        finally
        {
            GuardedValue.ThrowOnCompare = false;
        }

        Assert.Contains("host", (IReadOnlySet<string>)host.Classes);
        Assert.Empty(guard.GetStyleValueSources(GuardedNode.ValueProperty));
        Assert.True(host.Classes.Remove("host"));
        Assert.Empty(host.Classes);
    }

    [Fact]
    public void PseudoClassCalculationFailureKeepsTheActivePseudoAndOldSnapshot()
    {
        var host = new PseudoPanel();
        var guard = new GuardedNode();
        host.Children.Add(guard);
        var scene = new UIScene(host);
        scene.SetStyleSheets([StyleSheet.Parse(":loading .guard { guarded: risky; }")]);

        Assert.Empty(guard.GetStyleValueSources(GuardedNode.ValueProperty));

        GuardedValue.ThrowOnCompare = true;
        try
        {
            host.Set("loading", true);
            Assert.Throws<InvalidOperationException>(scene.Root!.UpdateStyles);
        }
        finally
        {
            GuardedValue.ThrowOnCompare = false;
        }

        Assert.True(host.IsActive("loading"));
        Assert.Empty(guard.GetStyleValueSources(GuardedNode.ValueProperty));
    }

    [Fact]
    public void FailedStylesheetApplicationPreservesThePreviousNodeSnapshot()
    {
        var host = MakePanel("host");
        var target = MakePanel("target");
        host.Children.Add(target);
        var scene = new UIScene(host);
        scene.SetStyleSheets([StyleSheet.Parse(".host .target { opacity: 0.4; }")]);
        scene.Root!.UpdateStyles();
        var resolver = scene.StyleResolver;
        Assert.Equal(0.4, target.Opacity);

        scene.SetStyleSheets([StyleSheet.Parse(".host .target { padding: nope; }")]);
        var error = Assert.Throws<StyleParseException>(scene.Root!.UpdateStyles);

        Assert.Equal(StyleParseError.InvalidValue, error.Error);
        Assert.NotSame(resolver, scene.StyleResolver);
        Assert.Equal(0.4, target.Opacity);
    }

    [Fact]
    public void LocalValuesMaskCombinatorStyleDeclarations()
    {
        var host = MakePanel("host");
        var target = MakePanel("target");
        host.Children.Add(target);
        var scene = new UIScene(host);
        scene.SetStyleSheets([StyleSheet.Parse(".host .target { opacity: 0.4; }")]);
        scene.Root!.UpdateStyles();
        Assert.Equal(0.4, target.Opacity);

        target.Opacity = 0.9;

        var masked = Assert.Single(target.GetStyleValueSources(UIElement.OpacityProperty));
        Assert.True(masked.IsMaskedByLocalValue);
        Assert.Equal(".host .target", masked.SelectorText);

        target.ClearValue(UIElement.OpacityProperty);

        var restored = Assert.Single(target.GetStyleValueSources(UIElement.OpacityProperty));
        Assert.False(restored.IsMaskedByLocalValue);
        Assert.Equal(0.4, target.Opacity);
    }

    [Fact]
    public void ReorderThatStartsAFittingSiblingRuleInvalidatesLayout()
    {
        var target = MakePanel("target");
        var leader = MakePanel("leader");
        var root = MakePanel();
        root.Children.Add(target);
        root.Children.Add(leader);
        var scene = new UIScene(root);
        scene.SetStyleSheets([StyleSheet.Parse(".leader + .target { padding: 4px; }")]);
        scene.Open();
        try
        {
            scene.PrepareFrame(new Size(100, 100));
            Assert.True(target.IsMeasureValid);
            Assert.True(target.IsArrangeValid);
            Assert.Equal(Thickness.Zero, target.Padding);

            root.Children.Move(1, 0);
            scene.Root!.UpdateStyles();

            Assert.Equal(new Thickness(4), target.Padding);
            Assert.False(target.IsMeasureValid);
            Assert.False(target.IsArrangeValid);
        }
        finally
        {
            scene.Close();
        }
    }

    [Fact]
    public void SameSceneMoveRefreshesRemainingSiblingsInOldParent()
    {
        var target = MakePanel("target");
        var remainder = MakePanel("remainder");
        var source = MakePanel();
        source.Children.Add(target);
        source.Children.Add(remainder);
        var destination = MakePanel();
        var root = MakePanel();
        root.Children.Add(source);
        root.Children.Add(destination);
        var scene = new UIScene(root);
        scene.SetStyleSheets([StyleSheet.Parse(".target + .remainder { opacity: 0.4; }")]);
        scene.Root!.UpdateStyles();
        Assert.Equal(0.4, remainder.Opacity);

        destination.Children.Add(target);
        scene.Root!.UpdateStyles();

        Assert.Equal(1d, remainder.Opacity);
    }

    [Fact]
    public void ReentrantCrossNodeMutationAppliesWhenTheLaterNodeIsVisited()
    {
        var host = MakePanel();
        var target = MakePanel("target");
        host.Children.Add(target);
        var otherHost = MakePanel();
        var other = MakePanel("other");
        otherHost.Children.Add(other);
        var root = MakePanel();
        root.Children.Add(host);
        root.Children.Add(otherHost);
        var scene = new UIScene(root);
        scene.SetStyleSheets([
            StyleSheet.Parse("""
                             .host .target { opacity: 0.4; }
                             .other-host .other { opacity: 0.9; }
                             """)
        ]);

        double? observed = null;
        target.PropertyChanged += (_, e) =>
        {
            if (!ReferenceEquals(e.Property, UIElement.OpacityProperty))
                return;
            otherHost.Classes.Add("other-host");
            observed = other.Opacity;
        };

        host.Classes.Add("host");
        scene.Root!.UpdateStyles();

        Assert.Equal(0.4, target.Opacity);
        Assert.Equal(1d, observed);
        Assert.Equal(0.9, other.Opacity);
    }

    [Fact]
    public void RelationshipNotificationFailureKeepsCommittedSnapshotAndInput()
    {
        var host = MakePanel();
        var target = MakePanel("target");
        host.Children.Add(target);
        var scene = new UIScene(host);
        scene.SetStyleSheets([StyleSheet.Parse(".host .target { opacity: 0.4; }")]);
        Assert.Equal(1d, target.Opacity);

        var expected = new InvalidOperationException("style notification failed");
        var notifications = 0;
        target.PropertyChanged += (_, e) =>
        {
            if (!ReferenceEquals(e.Property, UIElement.OpacityProperty))
                return;
            notifications++;
            throw expected;
        };

        host.Classes.Add("host");
        Assert.Same(expected, Assert.Throws<InvalidOperationException>(scene.Root!.UpdateStyles));

        Assert.Contains("host", (IReadOnlySet<string>)host.Classes);
        Assert.Equal(0.4, target.Opacity);
        Assert.Single(target.GetStyleValueSources(UIElement.OpacityProperty));

        host.Classes.Add("host");
        scene.Root!.UpdateStyles();
        Assert.Equal(1, notifications);
    }

    [Fact]
    public void UnchangedInputsDoNotNotifyAgain()
    {
        var host = new PseudoPanel("host");
        var target = MakePanel("target");
        host.Children.Add(target);
        var scene = new UIScene(host);
        scene.SetStyleSheets([StyleSheet.Parse(".host:loading .target { opacity: 0.4; }")]);
        host.Set("loading", true);
        scene.Root!.UpdateStyles();
        Assert.Equal(0.4, target.Opacity);

        var notifications = 0;
        target.PropertyChanged += (_, e) =>
            notifications += ReferenceEquals(e.Property, UIElement.OpacityProperty) ? 1 : 0;

        host.Set("loading", true);
        host.Classes.Add("host");
        host.StyleId = null;
        host.Classes.Remove("absent");
        scene.Root!.UpdateStyles();

        Assert.Equal(0, notifications);
    }

    [Fact]
    public void StyleIdChangeRemainsCommittedOnCalculationFailure()
    {
        var host = MakePanel();
        var guard = new GuardedNode();
        host.Children.Add(guard);
        var scene = new UIScene(host);
        scene.SetStyleSheets([StyleSheet.Parse("#menu .guard { guarded: risky; }")]);

        Assert.Empty(guard.GetStyleValueSources(GuardedNode.ValueProperty));

        GuardedValue.ThrowOnCompare = true;
        try
        {
            host.StyleId = "menu";
            Assert.Throws<InvalidOperationException>(scene.Root!.UpdateStyles);
        }
        finally
        {
            GuardedValue.ThrowOnCompare = false;
        }

        Assert.Equal("menu", host.StyleId);
        Assert.Empty(guard.GetStyleValueSources(GuardedNode.ValueProperty));
    }

    [Fact]
    public void ConverterCanInvalidateRelationshipsAndAddChildren()
    {
        var host = MakePanel("host");
        var scene = new UIScene(host);
        scene.SetStyleSheets([StyleSheet.Parse(".host MutationCallbackNode { mutation-value: 1; }")]);
        var node = new MutationCallbackNode();
        Exception? moveError = null;
        Exception? classError = null;
        MutationCallbackNode.OnConvert = () =>
        {
            moveError = Record.Exception(() => host.Children.Add(new Panel()));
            classError = Record.Exception(() => host.Classes.Add("blocked"));
        };
        try
        {
            host.Children.Add(node);
            scene.Root!.UpdateStyles();
        }
        finally
        {
            MutationCallbackNode.OnConvert = null;
        }

        Assert.Null(moveError);
        Assert.Null(classError);
        Assert.Equal(2, host.Children.Count);
        Assert.Same(node, host.Children[0]);
        Assert.Contains("blocked", (IReadOnlySet<string>)host.Classes);
        Assert.False(host.IsStyleSubtreeValid);
        scene.Root!.UpdateStyles();
        Assert.True(host.IsStyleSubtreeValid);
    }

    [Fact]
    public void ConverterCanApplyUnrelatedStylesButCannotReenterTheCurrentTree()
    {
        var host = MakePanel("host");
        var node = new MutationCallbackNode();
        host.Children.Add(node);
        var scene = new UIScene(host);
        scene.SetStyleSheets([StyleSheet.Parse(".host MutationCallbackNode { mutation-value: 1; }")]);
        var otherRoot = MakePanel("other");
        var otherScene = new UIScene(otherRoot);
        var otherChild = MakePanel("target");
        Exception? unrelatedError = null;
        Exception? reentryError = null;
        MutationCallbackNode.OnConvert = () =>
        {
            unrelatedError = Record.Exception(() =>
            {
                otherRoot.Children.Add(otherChild);
                otherScene.SetStyleSheets([StyleSheet.Parse(".other > .target { opacity: 0.7; }")]);
                otherScene.Root!.UpdateStyles();
            });
            reentryError = Record.Exception(node.UpdateStyles);
        };
        try
        {
            scene.Root!.UpdateStyles();
        }
        finally
        {
            MutationCallbackNode.OnConvert = null;
        }

        Assert.Null(unrelatedError);
        Assert.IsType<InvalidOperationException>(reentryError);
        Assert.Equal(0.7, otherChild.Opacity);
        Assert.Equal(1d, node.GetValue(MutationCallbackNode.ValueProperty));
        Assert.False(host.IsUpdatingStyles);
        Assert.False(scene.IsUpdatingLayout);
    }

    [Fact]
    public void FailedRelationshipCalculationReleasesTheTreeState()
    {
        var host = MakePanel("host");
        var node = new MutationCallbackNode();
        host.Children.Add(node);
        var scene = new UIScene(host);
        scene.SetStyleSheets([StyleSheet.Parse(".host MutationCallbackNode { mutation-value: 1; }")]);
        var expected = new InvalidOperationException("conversion failed");
        MutationCallbackNode.OnConvert = () => throw expected;
        try
        {
            var error = Assert.Throws<StyleParseException>(scene.Root!.UpdateStyles);
            Assert.Same(expected, error.InnerException);
        }
        finally
        {
            MutationCallbackNode.OnConvert = null;
        }

        Assert.False(node.IsStyleSubtreeValid);
        Assert.False(host.IsUpdatingStyles);
        Assert.False(scene.IsUpdatingLayout);
        Assert.True(host.Classes.Add("after-failure"));
        var otherRoot = new Panel();
        otherRoot.Children.Add(node);
        Assert.Same(otherRoot, node.Parent);
        Assert.Empty(host.Children);
    }

    [Fact]
    public void CrossSceneMoveRefreshesSiblingRulesInBothScenes()
    {
        var leader = MakePanel("leader");
        var sourceTarget = MakePanel("target");
        var sourceRoot = MakePanel();
        sourceRoot.Children.Add(leader);
        sourceRoot.Children.Add(sourceTarget);
        var source = new UIScene(sourceRoot);
        source.SetStyleSheets([StyleSheet.Parse(".leader + .target { opacity: 0.4; }")]);

        var destinationTarget = MakePanel("target");
        var destinationRoot = MakePanel();
        destinationRoot.Children.Add(destinationTarget);
        var destination = new UIScene(destinationRoot);
        destination.SetStyleSheets([StyleSheet.Parse(".leader ~ .target { opacity: 0.7; }")]);
        source.Root!.UpdateStyles();
        destination.Root!.UpdateStyles();
        Assert.Equal(0.4, sourceTarget.Opacity);
        Assert.Equal(1d, destinationTarget.Opacity);

        destinationRoot.Children.Insert(0, leader);
        source.Root!.UpdateStyles();
        destination.Root!.UpdateStyles();

        Assert.Same(destination, leader.Scene);
        Assert.Equal(1d, sourceTarget.Opacity);
        Assert.Equal(0.7, destinationTarget.Opacity);
    }

    [Fact]
    public void BuiltInHoverRefreshesFollowingSiblingDescendants()
    {
        var leader = MakePanel("leader");
        var following = MakePanel();
        var target = MakePanel("target");
        following.Children.Add(target);
        var root = MakePanel();
        root.Children.Add(leader);
        root.Children.Add(following);
        var scene = new UIScene(root);
        scene.SetStyleSheets([StyleSheet.Parse(".leader:hover + * .target { opacity: 0.4; }")]);
        Assert.Equal(1d, target.Opacity);

        leader.SetHovered(true);
        scene.Root!.UpdateStyles();
        Assert.Equal(0.4, target.Opacity);

        leader.SetHovered(false);
        scene.Root!.UpdateStyles();
        Assert.Equal(1d, target.Opacity);
    }

    [Fact]
    public void FailedApplicationAfterMovingPreservesTheCommittedTreeAndPreviousSnapshot()
    {
        var source = MakePanel("host");
        var destination = MakePanel();
        var guard = new GuardedNode();
        source.Children.Add(guard);
        var root = MakePanel();
        root.Children.Add(source);
        root.Children.Add(destination);
        var scene = new UIScene(root);
        scene.SetStyleSheets([StyleSheet.Parse(".host .guard { guarded: risky; opacity: 0.4; }")]);
        scene.Root!.UpdateStyles();
        Assert.Equal(0.4, guard.Opacity);
        Assert.Single(guard.GetStyleValueSources(GuardedNode.ValueProperty));

        GuardedValue.ThrowOnCompare = true;
        try
        {
            destination.Children.Add(guard);
            Assert.Throws<InvalidOperationException>(scene.Root!.UpdateStyles);
        }
        finally
        {
            GuardedValue.ThrowOnCompare = false;
        }

        Assert.Same(destination, guard.Parent);
        Assert.Same(scene, guard.Scene);
        Assert.Same(guard, Assert.Single(destination.Children));
        Assert.DoesNotContain(guard, source.Children);
        Assert.Equal(0.4, guard.Opacity);
        Assert.NotSame(GuardedNode.ValueProperty.DefaultValue, guard.GetValue(GuardedNode.ValueProperty));
        Assert.Single(guard.GetStyleValueSources(GuardedNode.ValueProperty));
    }

    private sealed class PseudoPanel : Panel
    {
        internal PseudoPanel(params string[] classes)
        {
            foreach (var className in classes)
                Classes.Add(className);
        }

        internal void Set(string name, bool active) => SetPseudoClass(PseudoClass.Get(name), active);

        internal bool IsActive(string name) => HasPseudoClass(PseudoClass.Get(name));
    }

    private sealed class GuardedValue(int number) : IEquatable<GuardedValue>
    {
        internal static bool ThrowOnCompare;

        private int Number => number;

        public bool Equals(GuardedValue? other)
        {
            if (ThrowOnCompare)
                throw new InvalidOperationException("guarded value comparison failed");
            return other is not null && number == other.Number;
        }

        public override bool Equals(object? obj) => obj is GuardedValue other && Equals(other);

        public override int GetHashCode() => number.GetHashCode();
    }

    private sealed class GuardedNode : UIElement
    {
        static GuardedNode()
        {
            CssRegistry.RegisterProperty<GuardedNode, GuardedValue>("guarded", ValueProperty, value =>
                new GuardedValue(value.Length));
        }

        internal GuardedNode() => Classes.Add("guard");

        internal static readonly Property<GuardedValue> ValueProperty =
            Property.Register<GuardedNode, GuardedValue>("Value", new GuardedValue(0),
                comparer: EqualityComparer<GuardedValue>.Default);
    }

    private sealed class MutationCallbackNode : UIElement
    {
        static MutationCallbackNode()
        {
            CssRegistry.RegisterProperty<MutationCallbackNode, double>("mutation-value", ValueProperty, _ =>
            {
                OnConvert?.Invoke();
                return 1d;
            });
        }

        internal static Action? OnConvert;

        internal static readonly Property<double> ValueProperty =
            Property.Register<MutationCallbackNode, double>("Value", 0);
    }
}