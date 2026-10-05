using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Styling;

using PanguEngine.ComponentModel;

namespace PanguEngine.Tests.Client.UI.Styling;

public sealed class UiStyleCombinatorUpdateTests
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
        var screen = new UiScreen(host);
        screen.SetStyleSheets([UiStyleSheet.Parse(selector + " { opacity: 0.4; }")]);

        Assert.Equal(1d, target.Opacity);

        host.Classes.Add("host");
        screen.Root!.UpdateStyles();
        Assert.Equal(0.4, target.Opacity);

        host.Classes.Remove("host");
        screen.Root!.UpdateStyles();
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
        var screen = new UiScreen(root);
        screen.SetStyleSheets([UiStyleSheet.Parse(
            ".unused, .leader + .target { opacity: 0.4; }")]);

        first.Classes.Add("leader");
        screen.Root!.UpdateStyles();

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
        var screen = new UiScreen(root);
        screen.SetStyleSheets([UiStyleSheet.Parse(".leader + .target { opacity: 0.4; }")]);

        Assert.Equal(1d, second.Opacity);

        first.Classes.Add("leader");
        screen.Root!.UpdateStyles();
        Assert.Equal(0.4, second.Opacity);

        first.Classes.Remove("leader");
        screen.Root!.UpdateStyles();
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
        var screen = new UiScreen(host);
        screen.SetStyleSheets([UiStyleSheet.Parse(selector + " { opacity: 0.4; }")]);

        Assert.Equal(1d, target.Opacity);

        host.StyleId = "menu";
        screen.Root!.UpdateStyles();
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
        var screen = new UiScreen(host);
        screen.SetStyleSheets([UiStyleSheet.Parse(selector + " { opacity: 0.4; }")]);

        Assert.Equal(1d, target.Opacity);

        host.Set("loading", true);
        screen.Root!.UpdateStyles();
        Assert.Equal(0.4, target.Opacity);

        host.Set("loading", false);
        screen.Root!.UpdateStyles();
        Assert.Equal(1d, target.Opacity);
    }

    [Theory]
    [InlineData("")]
    [InlineData(".unused, ")]
    public void SameScreenReparentRefreshesBothParents(string prefix)
    {
        var source = MakePanel("source");
        var destination = MakePanel("destination");
        var target = MakePanel("target");
        source.Children.Add(target);
        var root = MakePanel();
        root.Children.Add(source);
        root.Children.Add(destination);
        var screen = new UiScreen(root);
        screen.SetStyleSheets([UiStyleSheet.Parse(
            prefix + ".source > .target { opacity: 0.4; }" +
            prefix + ".destination > .target { opacity: 0.7; }")]);
        screen.Root!.UpdateStyles();

        Assert.Equal(0.4, target.Opacity);

        destination.Children.Add(target);
        screen.Root!.UpdateStyles();

        Assert.Same(destination, target.Parent);
        Assert.Equal(0.7, target.Opacity);
    }

    [Fact]
    public void CrossScreenMoveUsesTheFinalScreenResolver()
    {
        var host = MakePanel("host");
        var target = MakePanel("target");
        host.Children.Add(target);
        var first = new UiScreen(host);
        first.SetStyleSheets([UiStyleSheet.Parse(".host .target { opacity: 0.4; }")]);
        first.Root!.UpdateStyles();
        Assert.Equal(0.4, target.Opacity);

        var secondHost = MakePanel();
        var second = new UiScreen(secondHost);
        second.SetStyleSheets([UiStyleSheet.Parse(".target { opacity: 0.7; }")]);

        secondHost.Children.Add(target);
        second.Root!.UpdateStyles();

        Assert.Same(second, target.Screen);
        Assert.Equal(0.7, target.Opacity);
    }

    [Theory]
    [InlineData("")]
    [InlineData(".unused, ")]
    public void PromotingAChildToAnotherScreenRootRefreshesTheOldParent(string prefix)
    {
        var leader = MakePanel("leader");
        var target = MakePanel("target");
        var root = MakePanel();
        root.Children.Add(leader);
        root.Children.Add(target);
        var screen = new UiScreen(root);
        screen.SetStyleSheets([UiStyleSheet.Parse(prefix + ".leader + .target { opacity: 0.4; }")]);
        screen.Root!.UpdateStyles();
        Assert.Equal(0.4, target.Opacity);

        var other = new UiScreen();
        other.Root = leader;
        screen.Root!.UpdateStyles();
        other.Root!.UpdateStyles();

        Assert.Same(other, leader.Screen);
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
        var screen = new UiScreen(root);
        screen.SetStyleSheets([UiStyleSheet.Parse(prefix + ".leader + .target { opacity: 0.4; }")]);
        Assert.Equal(1d, target.Opacity);

        var leader = MakePanel("leader");
        root.Children.Insert(0, leader);
        screen.Root!.UpdateStyles();

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
        var screen = new UiScreen(root);
        screen.SetStyleSheets([UiStyleSheet.Parse(prefix + ".leader + .target { opacity: 0.4; }")]);
        screen.Root!.UpdateStyles();
        Assert.Equal(0.4, target.Opacity);

        root.Children.Remove(leader);
        screen.Root!.UpdateStyles();

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
        var screen = new UiScreen(root);
        screen.SetStyleSheets([UiStyleSheet.Parse(prefix + ".leader + .target { opacity: 0.4; }")]);
        Assert.Equal(1d, target.Opacity);

        root.Children[0] = MakePanel("leader");
        screen.Root!.UpdateStyles();

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
        var screen = new UiScreen(root);
        screen.SetStyleSheets([UiStyleSheet.Parse(prefix + ".host .target { opacity: 0.4; }")]);
        screen.Root!.UpdateStyles();
        Assert.Equal(0.4, child.Opacity);

        host.Children.Clear();
        child.UpdateStyles();

        Assert.Null(child.Parent);
        Assert.Null(child.Screen);
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
        var screen = new UiScreen(root);
        screen.SetStyleSheets([UiStyleSheet.Parse(prefix + ".leader + .target { opacity: 0.4; }")]);
        screen.Root!.UpdateStyles();
        Assert.Equal(0.4, target.Opacity);

        root.Children.Move(1, 0);
        screen.Root!.UpdateStyles();

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
        var screen = new UiScreen(root);
        screen.SetStyleSheets([UiStyleSheet.Parse(".leader + .target { opacity: 0.4; }")]);

        target.MoveToFront();
        screen.Root!.UpdateStyles();
        Assert.Equal(0.4, target.Opacity);

        leader.MoveToFront();
        screen.Root!.UpdateStyles();
        Assert.Equal(1d, target.Opacity);

        leader.MoveToBack();
        screen.Root!.UpdateStyles();
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
        var screen = new UiScreen(root);
        screen.SetStyleSheets([UiStyleSheet.Parse(".a + .target { opacity: 0.4; }")]);

        var notifications = 0;
        target.PropertyChanged += (_, e) =>
            notifications += ReferenceEquals(e.Property, UiNode.OpacityProperty) ? 1 : 0;

        root.Children.Add(target);

        screen.Root!.UpdateStyles();
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
        var screen = new UiScreen(host);
        screen.SetStyleSheets([UiStyleSheet.Parse("""
            .host .first { opacity: 0.4; }
            .host .second { opacity: 0.7; }
            """)]);
        screen.Root!.UpdateStyles();
        Assert.Equal(0.4, first.Opacity);
        Assert.Equal(0.7, second.Opacity);

        host.Classes.Remove("host");
        screen.Root!.UpdateStyles();
        Assert.Equal(1d, first.Opacity);
        Assert.Equal(1d, second.Opacity);

        double? observed = null;
        first.PropertyChanged += (_, e) =>
        {
            if (ReferenceEquals(e.Property, UiNode.OpacityProperty))
                observed ??= second.Opacity;
        };

        host.Classes.Add("host");
        screen.Root!.UpdateStyles();

        Assert.Equal(1d, observed);
        Assert.Equal(0.7, second.Opacity);
    }

    [Fact]
    public void ReentrantAncestorMutationRemainsPendingUntilTheNextApplication()
    {
        var host = MakePanel();
        var target = MakePanel("target");
        host.Children.Add(target);
        var screen = new UiScreen(host);
        screen.SetStyleSheets([UiStyleSheet.Parse("""
            .host .target { opacity: 0.4; }
            .host.extra .target { opacity: 0.9; }
            """)]);

        var notifications = 0;
        target.PropertyChanged += (_, e) =>
        {
            if (!ReferenceEquals(e.Property, UiNode.OpacityProperty))
                return;
            notifications++;
            if (!host.Classes.Contains("extra"))
                host.Classes.Add("extra");
        };

        host.Classes.Add("host");
        screen.Root!.UpdateStyles();

        Assert.Equal(0.4, target.Opacity);
        Assert.Equal(1, notifications);
        screen.Root!.UpdateStyles();

        Assert.Equal(0.9, target.Opacity);
        Assert.Equal(2, notifications);
    }

    [Fact]
    public void ClassChangeRemainsCommittedOnCalculationFailure()
    {
        var host = MakePanel();
        var guard = new GuardedNode();
        host.Children.Add(guard);
        var screen = new UiScreen(host);
        screen.SetStyleSheets([UiStyleSheet.Parse(".host .guard { guarded: risky; }")]);

        Assert.Empty(guard.GetStyleValueSources(GuardedNode.ValueProperty));

        GuardedValue.ThrowOnCompare = true;
        try
        {
            host.Classes.Add("host");
            Assert.Throws<InvalidOperationException>(screen.Root!.UpdateStyles);
        }
        finally
        {
            GuardedValue.ThrowOnCompare = false;
        }

        Assert.Contains("host", host.Classes);
        Assert.Empty(guard.GetStyleValueSources(GuardedNode.ValueProperty));
    }

    [Fact]
    public void PseudoClassCalculationFailureKeepsTheActivePseudoAndOldSnapshot()
    {
        var host = new PseudoPanel();
        var guard = new GuardedNode();
        host.Children.Add(guard);
        var screen = new UiScreen(host);
        screen.SetStyleSheets([UiStyleSheet.Parse(":loading .guard { guarded: risky; }")]);

        Assert.Empty(guard.GetStyleValueSources(GuardedNode.ValueProperty));

        GuardedValue.ThrowOnCompare = true;
        try
        {
            host.Set("loading", true);
            Assert.Throws<InvalidOperationException>(screen.Root!.UpdateStyles);
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
        var screen = new UiScreen(host);
        screen.SetStyleSheets([UiStyleSheet.Parse(".host .target { opacity: 0.4; }")]);
        screen.Root!.UpdateStyles();
        var resolver = screen.StyleResolver;
        Assert.Equal(0.4, target.Opacity);

        screen.SetStyleSheets([UiStyleSheet.Parse(".host .target { padding: nope; }")]);
        var error = Assert.Throws<UiStyleParseException>(screen.Root!.UpdateStyles);

        Assert.Equal(UiStyleParseError.InvalidValue, error.Error);
        Assert.NotSame(resolver, screen.StyleResolver);
        Assert.Equal(0.4, target.Opacity);
    }

    [Fact]
    public void LocalValuesMaskCombinatorStyleDeclarations()
    {
        var host = MakePanel("host");
        var target = MakePanel("target");
        host.Children.Add(target);
        var screen = new UiScreen(host);
        screen.SetStyleSheets([UiStyleSheet.Parse(".host .target { opacity: 0.4; }")]);
        screen.Root!.UpdateStyles();
        Assert.Equal(0.4, target.Opacity);

        target.Opacity = 0.9;

        var masked = Assert.Single(target.GetStyleValueSources(UiNode.OpacityProperty));
        Assert.True(masked.IsMaskedByLocalValue);
        Assert.Equal(".host .target", masked.SelectorText);

        target.ClearValue(UiNode.OpacityProperty);

        var restored = Assert.Single(target.GetStyleValueSources(UiNode.OpacityProperty));
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
        var screen = new UiScreen(root);
        screen.SetStyleSheets([UiStyleSheet.Parse(".leader + .target { padding: 4px; }")]);
        screen.Open();
        try
        {
            screen.PrepareFrame(new Size(100, 100), 0);
            Assert.True(target.IsMeasureValid);
            Assert.True(target.IsArrangeValid);
            Assert.Equal(Thickness.Zero, target.Padding);

            root.Children.Move(1, 0);
            screen.Root!.UpdateStyles();

            Assert.Equal(new Thickness(4), target.Padding);
            Assert.False(target.IsMeasureValid);
            Assert.False(target.IsArrangeValid);
        }
        finally
        {
            screen.Close();
        }
    }

    [Fact]
    public void SameScreenMoveRefreshesRemainingSiblingsInOldParent()
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
        var screen = new UiScreen(root);
        screen.SetStyleSheets([UiStyleSheet.Parse(".target + .remainder { opacity: 0.4; }")]);
        screen.Root!.UpdateStyles();
        Assert.Equal(0.4, remainder.Opacity);

        destination.Children.Add(target);
        screen.Root!.UpdateStyles();

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
        var screen = new UiScreen(root);
        screen.SetStyleSheets([UiStyleSheet.Parse("""
            .host .target { opacity: 0.4; }
            .other-host .other { opacity: 0.9; }
            """)]);

        double? observed = null;
        target.PropertyChanged += (_, e) =>
        {
            if (!ReferenceEquals(e.Property, UiNode.OpacityProperty))
                return;
            otherHost.Classes.Add("other-host");
            observed = other.Opacity;
        };

        host.Classes.Add("host");
        screen.Root!.UpdateStyles();

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
        var screen = new UiScreen(host);
        screen.SetStyleSheets([UiStyleSheet.Parse(".host .target { opacity: 0.4; }")]);
        Assert.Equal(1d, target.Opacity);

        var expected = new InvalidOperationException("style notification failed");
        var notifications = 0;
        target.PropertyChanged += (_, e) =>
        {
            if (!ReferenceEquals(e.Property, UiNode.OpacityProperty))
                return;
            notifications++;
            throw expected;
        };

        host.Classes.Add("host");
        Assert.Same(expected, Assert.Throws<InvalidOperationException>(screen.Root!.UpdateStyles));

        Assert.Contains("host", host.Classes);
        Assert.Equal(0.4, target.Opacity);
        Assert.Single(target.GetStyleValueSources(UiNode.OpacityProperty));

        host.Classes.Add("host");
        screen.Root!.UpdateStyles();
        Assert.Equal(1, notifications);
    }

    [Fact]
    public void UnchangedInputsDoNotNotifyAgain()
    {
        var host = new PseudoPanel("host");
        var target = MakePanel("target");
        host.Children.Add(target);
        var screen = new UiScreen(host);
        screen.SetStyleSheets([UiStyleSheet.Parse(".host:loading .target { opacity: 0.4; }")]);
        host.Set("loading", true);
        screen.Root!.UpdateStyles();
        Assert.Equal(0.4, target.Opacity);

        var notifications = 0;
        target.PropertyChanged += (_, e) =>
            notifications += ReferenceEquals(e.Property, UiNode.OpacityProperty) ? 1 : 0;

        host.Set("loading", true);
        host.Classes.Add("host");
        host.StyleId = null;
        host.Classes.Remove("absent");
        screen.Root!.UpdateStyles();

        Assert.Equal(0, notifications);
    }

    [Fact]
    public void StyleIdChangeRemainsCommittedOnCalculationFailure()
    {
        var host = MakePanel();
        var guard = new GuardedNode();
        host.Children.Add(guard);
        var screen = new UiScreen(host);
        screen.SetStyleSheets([UiStyleSheet.Parse("#menu .guard { guarded: risky; }")]);

        Assert.Empty(guard.GetStyleValueSources(GuardedNode.ValueProperty));

        GuardedValue.ThrowOnCompare = true;
        try
        {
            host.StyleId = "menu";
            Assert.Throws<InvalidOperationException>(screen.Root!.UpdateStyles);
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
        var screen = new UiScreen(host);
        screen.SetStyleSheets([UiStyleSheet.Parse(".host MutationCallbackNode { mutation-value: 1; }")]);
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
            screen.Root!.UpdateStyles();
        }
        finally
        {
            MutationCallbackNode.OnConvert = null;
        }

        Assert.Null(moveError);
        Assert.Null(classError);
        Assert.Equal(2, host.Children.Count);
        Assert.Same(node, host.Children[0]);
        Assert.Contains("blocked", host.Classes);
        Assert.False(host.IsStyleSubtreeValid);
        screen.Root!.UpdateStyles();
        Assert.True(host.IsStyleSubtreeValid);
    }

    [Fact]
    public void ConverterCanApplyUnrelatedStylesButCannotReenterTheCurrentTree()
    {
        var host = MakePanel("host");
        var node = new MutationCallbackNode();
        host.Children.Add(node);
        var screen = new UiScreen(host);
        screen.SetStyleSheets([UiStyleSheet.Parse(".host MutationCallbackNode { mutation-value: 1; }")]);
        var otherRoot = MakePanel("other");
        var otherScreen = new UiScreen(otherRoot);
        var otherChild = MakePanel("target");
        Exception? unrelatedError = null;
        Exception? reentryError = null;
        MutationCallbackNode.OnConvert = () =>
        {
            unrelatedError = Record.Exception(() =>
            {
                otherRoot.Children.Add(otherChild);
                otherScreen.SetStyleSheets([UiStyleSheet.Parse(".other > .target { opacity: 0.7; }")]);
                otherScreen.Root!.UpdateStyles();
            });
            reentryError = Record.Exception(node.UpdateStyles);
        };
        try
        {
            screen.Root!.UpdateStyles();
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
        Assert.False(screen.IsUpdatingLayout);
    }

    [Fact]
    public void FailedRelationshipCalculationReleasesTheTreeState()
    {
        var host = MakePanel("host");
        var node = new MutationCallbackNode();
        host.Children.Add(node);
        var screen = new UiScreen(host);
        screen.SetStyleSheets([UiStyleSheet.Parse(".host MutationCallbackNode { mutation-value: 1; }")]);
        var expected = new InvalidOperationException("conversion failed");
        MutationCallbackNode.OnConvert = () => throw expected;
        try
        {
            var error = Assert.Throws<UiStyleParseException>(screen.Root!.UpdateStyles);
            Assert.Same(expected, error.InnerException);
        }
        finally
        {
            MutationCallbackNode.OnConvert = null;
        }

        Assert.False(node.IsStyleSubtreeValid);
        Assert.False(host.IsUpdatingStyles);
        Assert.False(screen.IsUpdatingLayout);
        Assert.True(host.Classes.Add("after-failure"));
        var otherRoot = new Panel();
        otherRoot.Children.Add(node);
        Assert.Same(otherRoot, node.Parent);
        Assert.Empty(host.Children);
    }

    [Fact]
    public void CrossScreenMoveRefreshesSiblingRulesInBothScreens()
    {
        var leader = MakePanel("leader");
        var sourceTarget = MakePanel("target");
        var sourceRoot = MakePanel();
        sourceRoot.Children.Add(leader);
        sourceRoot.Children.Add(sourceTarget);
        var source = new UiScreen(sourceRoot);
        source.SetStyleSheets([UiStyleSheet.Parse(".leader + .target { opacity: 0.4; }")]);

        var destinationTarget = MakePanel("target");
        var destinationRoot = MakePanel();
        destinationRoot.Children.Add(destinationTarget);
        var destination = new UiScreen(destinationRoot);
        destination.SetStyleSheets([UiStyleSheet.Parse(".leader ~ .target { opacity: 0.7; }")]);
        source.Root!.UpdateStyles();
        destination.Root!.UpdateStyles();
        Assert.Equal(0.4, sourceTarget.Opacity);
        Assert.Equal(1d, destinationTarget.Opacity);

        destinationRoot.Children.Insert(0, leader);
        source.Root!.UpdateStyles();
        destination.Root!.UpdateStyles();

        Assert.Same(destination, leader.Screen);
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
        var screen = new UiScreen(root);
        screen.SetStyleSheets([UiStyleSheet.Parse(".leader:hover + * .target { opacity: 0.4; }")]);
        Assert.Equal(1d, target.Opacity);

        leader.SetHovered(true);
        screen.Root!.UpdateStyles();
        Assert.Equal(0.4, target.Opacity);

        leader.SetHovered(false);
        screen.Root!.UpdateStyles();
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
        var screen = new UiScreen(root);
        screen.SetStyleSheets([UiStyleSheet.Parse(".host .guard { guarded: risky; opacity: 0.4; }")]);
        screen.Root!.UpdateStyles();
        Assert.Equal(0.4, guard.Opacity);
        Assert.Single(guard.GetStyleValueSources(GuardedNode.ValueProperty));

        GuardedValue.ThrowOnCompare = true;
        try
        {
            destination.Children.Add(guard);
            Assert.Throws<InvalidOperationException>(screen.Root!.UpdateStyles);
        }
        finally
        {
            GuardedValue.ThrowOnCompare = false;
        }

        Assert.Same(destination, guard.Parent);
        Assert.Same(screen, guard.Screen);
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

        internal void Set(string name, bool active) => SetPseudoClass(UiPseudoClass.Get(name), active);

        internal bool IsActive(string name) => HasPseudoClass(UiPseudoClass.Get(name));
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

    private sealed class GuardedNode : UiNode
    {
        static GuardedNode()
        {
            UiCssRegistry.RegisterProperty<GuardedNode, GuardedValue>("guarded", ValueProperty, value =>
                new GuardedValue(value.Length));
        }

        internal GuardedNode() => Classes.Add("guard");

        internal static readonly Property<GuardedValue> ValueProperty =
            Property.Register<GuardedNode, GuardedValue>("Value", new GuardedValue(0));
    }

    private sealed class MutationCallbackNode : UiNode
    {
        static MutationCallbackNode()
        {
            UiCssRegistry.RegisterProperty<MutationCallbackNode, double>("mutation-value", ValueProperty, _ =>
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
