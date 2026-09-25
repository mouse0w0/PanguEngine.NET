using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Drawing;
using PanguEngine.Client.UI.Styling;

namespace PanguEngine.Tests.Client.UI.Styling;

public sealed class UiStyleCombinatorTests
{
    private static UiStyleSelector Selector(string css) =>
        Assert.Single(UiStyleSheet.Parse(css + " { }").Rules).Selectors[0];

    private static UiStyleRule SingleRule(string css) =>
        Assert.Single(UiStyleSheet.Parse(css).Rules);

    private static Panel MakePanel(params string[] classes)
    {
        var panel = new Panel();
        foreach (var className in classes)
            panel.Classes.Add(className);
        return panel;
    }

    private static Brush Brush(byte value) => new SolidColorBrush(value, value, value);

    [Theory]
    [InlineData("*.a>*.b", ".a > .b")]
    [InlineData(".a>.b", ".a > .b")]
    [InlineData(".a + .b", ".a + .b")]
    [InlineData(".a~.b", ".a ~ .b")]
    [InlineData("Button   .primary", "Button .primary")]
    [InlineData("Button\n\t.primary", "Button .primary")]
    [InlineData(".a\r\n/* line\r\n */ >\r\n.b", ".a > .b")]
    [InlineData(".a/* line\r\n */.b", ".a.b")]
    [InlineData(".a/**/.b", ".a.b")]
    [InlineData(".a/**/ .b", ".a .b")]
    [InlineData(".a /*c*/.b", ".a .b")]
    [InlineData("#outer #inner", "#outer #inner")]
    [InlineData("Panel Button", "Panel Button")]
    public void NormalizesCombinatorSelectors(string text, string normalized)
    {
        Assert.Equal(normalized, Selector(text).SelectorText);
    }

    [Fact]
    public void SelectorListPreservesIndependentCombinatorBranches()
    {
        var rule = SingleRule(".a > .b, .c ~ .d { }");

        Assert.Equal([".a > .b", ".c ~ .d"], rule.Selectors.Select(selector => selector.SelectorText));
        Assert.True(rule.Selectors[0].HasRelationships);
        Assert.True(rule.Selectors[1].HasSiblingRelationships);
    }

    [Theory]
    [InlineData("Button .primary", 0, 1)]
    [InlineData("#outer #inner", 2, 0)]
    [InlineData("Button.a.b > .c:hover", 0, 4)]
    [InlineData(".a.a .a", 0, 2)]
    [InlineData("* #id .x:y", 1, 2)]
    [InlineData(".a .a", 0, 2)]
    public void IdAndClassContributionsSumAcrossSegments(string css, int idCount, int classAndPseudoCount)
    {
        var selector = Selector(css);

        Assert.Equal(idCount, selector.IdCount);
        Assert.Equal(classAndPseudoCount, selector.ClassAndPseudoCount);
    }

    [Fact]
    public void PublicPropertiesDescribeTheTargetSegmentOnly()
    {
        var selector = Selector("#outer.outer > .target:hover");

        Assert.Equal("*", selector.TypeName);
        Assert.Null(selector.Id);
        Assert.Equal(new[] { "target" }, selector.Classes);
        Assert.Equal(new[] { "hover" }, selector.PseudoClasses.Select(pseudoClass => pseudoClass.Name));
        Assert.Equal(1, selector.IdCount);
        Assert.Equal(3, selector.ClassAndPseudoCount);
        Assert.Equal(0, selector.TargetTypeDepth);
        Assert.Equal(".outer#outer > .target:hover", selector.SelectorText);
    }

    [Fact]
    public void PseudoClassesAreCountedAcrossSegmentsButExposedForTheTargetOnly()
    {
        var selector = Selector("Panel:hover .target:focus:disabled");

        Assert.Equal(new[] { "disabled", "focus" }, selector.PseudoClasses.Select(pseudoClass => pseudoClass.Name));
        Assert.Equal(4, selector.ClassAndPseudoCount);
        Assert.True(selector.HasPseudoClasses);
    }

    [Theory]
    [InlineData("Button", false, false)]
    [InlineData("Panel Button", true, false)]
    [InlineData("Panel > Button", true, false)]
    [InlineData("Button + Button", true, true)]
    [InlineData("Button ~ Button", true, true)]
    [InlineData("Panel Button + Button", true, true)]
    public void RelationshipFlagsDescribeTheWholeChain(string css, bool hasRelationships, bool hasSiblings)
    {
        var selector = Selector(css);

        Assert.Equal(hasRelationships, selector.HasRelationships);
        Assert.Equal(hasSiblings, selector.HasSiblingRelationships);
    }

    [Theory]
    [InlineData("Button", false)]
    [InlineData("Button:hover", true)]
    [InlineData("Panel:hover Button", true)]
    [InlineData("Panel > Button:disabled", true)]
    public void HasPseudoClassesCoversAnySegment(string css, bool expected) =>
        Assert.Equal(expected, Selector(css).HasPseudoClasses);

    [Fact]
    public void SelectorSourceLocationCoversTheWholeChainWithoutTrailingTrivia()
    {
        var rule = Assert.Single(UiStyleSheet.Parse("Button .primary   {\n}", "chain.css").Rules);
        var location = rule.SourceLocation!;

        Assert.Equal("chain.css", location.SourceName);
        Assert.Equal(1, location.Line);
        Assert.Equal(1, location.Column);
        Assert.Equal("Button .primary".Length, location.Length);
    }

    [Fact]
    public void SelectorSourceLocationIncludesInteriorComments()
    {
        var rule = Assert.Single(UiStyleSheet.Parse("Button/*x*/.primary { }", "comments.css").Rules);

        Assert.Equal("Button.primary", rule.Selectors[0].SelectorText);
        Assert.Equal("Button/*x*/.primary".Length, rule.SourceLocation!.Length);
    }

    [Fact]
    public void TrailingWhitespaceBeforeBlockDoesNotCreateDescendant()
    {
        Assert.Equal("Button", Selector("Button   ").SelectorText);
    }

    [Theory]
    [InlineData(".a > > .b", 6)]
    [InlineData(".a > { }", 6)]
    [InlineData("A >", 4)]
    [InlineData("A/**/B", 6)]
    [InlineData("But/**/ton", 8)]
    public void InvalidCombinatorSyntaxReportsPosition(string css, int column)
    {
        var error = Assert.Throws<UiStyleParseException>(() => UiStyleSheet.Parse(css));

        Assert.Equal(UiStyleParseError.InvalidSyntax, error.Error);
        Assert.Equal(1, error.Line);
        Assert.Equal(column, error.Column);
    }

    [Fact]
    public void LeadingCombinatorReportsTrailingTokenAtStart()
    {
        var error = Assert.Throws<UiStyleParseException>(() => UiStyleSheet.Parse("> .a { }"));

        Assert.Equal(UiStyleParseError.TrailingToken, error.Error);
        Assert.Equal(1, error.Line);
        Assert.Equal(1, error.Column);
    }

    [Fact]
    public void UnterminatedCommentInSelectorReportsError()
    {
        var error = Assert.Throws<UiStyleParseException>(() => UiStyleSheet.Parse(".a /* never closed"));

        Assert.Equal(UiStyleParseError.UnterminatedComment, error.Error);
    }

    [Fact]
    public void DuplicateIdIsRejectedWithinASegmentButAllowedAcrossSegments()
    {
        Assert.Equal(
            UiStyleParseError.DuplicateId,
            Assert.Throws<UiStyleParseException>(() => UiStyleSheet.Parse(".a#x#y { }")).Error);

        Assert.Equal(2, Selector("#outer #inner").IdCount);
    }

    [Fact]
    public void FunctionSyntaxRemainsUnsupported()
    {
        Assert.Equal(
            UiStyleParseError.InvalidSyntax,
            Assert.Throws<UiStyleParseException>(() => UiStyleSheet.Parse("Button:not(.primary) { }")).Error);
    }

    [Fact]
    public void DescendantRelationshipMatchesStrictAncestor()
    {
        var outer = MakePanel("x");
        var inner = MakePanel("y");
        outer.Children.Add(inner);

        Assert.True(Selector(".x .y").Matches(inner));
        Assert.False(Selector(".y .x").Matches(inner));
        Assert.False(Selector(".x .y").Matches(outer));
        Assert.True(Selector(".x").Matches(outer));
    }

    [Fact]
    public void ChildRelationshipRequiresImmediateParent()
    {
        var outer = MakePanel("x");
        var middle = MakePanel();
        var inner = MakePanel("y");
        outer.Children.Add(middle);
        middle.Children.Add(inner);

        Assert.False(Selector(".x > .y").Matches(inner));
        Assert.True(Selector(".x .y").Matches(inner));

        outer.Children.Remove(middle);
        outer.Children.Add(inner);

        Assert.True(Selector(".x > .y").Matches(inner));
        Assert.False(Selector(".x > .y").Matches(middle));
    }

    [Fact]
    public void AdjacentAndGeneralSiblingRelationships()
    {
        var first = MakePanel("x");
        var middle = MakePanel();
        var second = MakePanel("y");
        var root = MakePanel();
        root.Children.Add(first);
        root.Children.Add(middle);
        root.Children.Add(second);

        Assert.False(Selector(".x + .y").Matches(second));
        Assert.True(Selector(".x ~ .y").Matches(second));
        Assert.False(Selector(".y ~ .x").Matches(second));

        root.Children.Remove(middle);

        Assert.True(Selector(".x + .y").Matches(second));
    }

    [Fact]
    public void RootNodeHasNoSiblingRelationship()
    {
        var root = MakePanel("y");

        Assert.False(Selector(".x + .y").Matches(root));

        var child = MakePanel("x");
        root.Children.Add(child);

        Assert.False(Selector(".x + .y").Matches(root));
    }

    [Fact]
    public void DescendantChainBacktracksToAFartherMatchingAncestor()
    {
        var a = MakePanel("a");
        var bFar = MakePanel("b");
        var bNear = MakePanel("b");
        var c = MakePanel("c");
        a.Children.Add(bFar);
        bFar.Children.Add(bNear);
        bNear.Children.Add(c);

        Assert.True(Selector(".a > .b .c").Matches(c));
        Assert.False(Selector(".a > .b > .c").Matches(c));
    }

    [Fact]
    public void GeneralSiblingChainBacktracksAcrossPreviousSiblings()
    {
        var p = MakePanel("p");
        var good = MakePanel("a");
        var bad = MakePanel("a");
        var b = MakePanel("b");
        var root = MakePanel();
        root.Children.Add(p);
        root.Children.Add(good);
        root.Children.Add(bad);
        root.Children.Add(b);

        Assert.True(Selector(".p + .a").Matches(good));
        Assert.False(Selector(".p + .a").Matches(bad));
        Assert.True(Selector(".p + .a ~ .b").Matches(b));
    }

    [Fact]
    public void MixedChainMatchesThroughDescendantChildAndSiblingRelationships()
    {
        var menu = MakePanel("menu");
        var item1 = MakePanel("item");
        var item2 = MakePanel("item");
        var label = MakePanel("label");
        menu.Children.Add(item1);
        menu.Children.Add(item2);
        item2.Children.Add(label);
        var selector = Selector(".menu > .item:hover + .item .label");

        Assert.False(selector.Matches(label));

        item1.SetHovered(true);
        Assert.True(selector.Matches(label));

        item1.SetHovered(false);
        Assert.False(selector.Matches(label));
    }

    [Fact]
    public void DescendantAndSiblingRelationshipsDoNotCrossSeparateTrees()
    {
        var firstRoot = MakePanel();
        var first = MakePanel("a");
        firstRoot.Children.Add(first);
        var secondRoot = MakePanel();
        var second = MakePanel("b");
        secondRoot.Children.Add(second);

        Assert.False(Selector(".a .b").Matches(second));
        Assert.False(Selector(".a + .b").Matches(second));
    }

    [Fact]
    public void CollapsedNodesStillParticipateInSiblingRelationships()
    {
        var first = MakePanel("a");
        var second = MakePanel("b");
        var root = MakePanel();
        root.Children.Add(first);
        root.Children.Add(second);
        first.Visibility = Visibility.Collapsed;

        Assert.True(Selector(".a + .b").Matches(second));
    }

    [Fact]
    public void UnknownElementNameDoesNotMatchKnownNodes()
    {
        var root = MakePanel("a");
        var child = MakePanel("b");
        root.Children.Add(child);

        Assert.False(Selector("Ghost .b").Matches(child));
    }

    [Fact]
    public void UnknownPseudoClassInAnySegmentMustBeActive()
    {
        var host = new PseudoPanel("a");
        var child = MakePanel("b");
        host.Children.Add(child);
        var selector = Selector(".a:phantom .b");

        Assert.False(selector.Matches(child));

        host.Set("phantom", true);
        Assert.True(selector.Matches(child));

        host.Set("phantom", false);
        Assert.False(selector.Matches(child));
    }

    [Fact]
    public void WildcardSegmentMatchesAnyElement()
    {
        var root = MakePanel("a");
        var child = new Button();
        root.Children.Add(child);

        Assert.True(Selector(".a *").Matches(child));
        Assert.True(Selector("* > *").Matches(child));
        Assert.False(Selector(".a > Button > *").Matches(child));
    }

    [Fact]
    public void TryMatchReturnsTheSumOfNamedSegmentDepths()
    {
        var root = MakePanel("frame");
        var button = new Button();
        root.Children.Add(button);
        var panelDepth = UiStyleSelector.For<Panel>().TargetTypeDepth;
        var buttonDepth = UiStyleSelector.For<Button>().TargetTypeDepth;

        Assert.True(Selector("Panel > Button").TryMatch(button, out var childDepth));
        Assert.Equal(panelDepth + buttonDepth, childDepth);

        Assert.True(Selector("Panel Button").TryMatch(button, out var descendantDepth));
        Assert.Equal(panelDepth + buttonDepth, descendantDepth);

        Assert.True(Selector(".frame > Button").TryMatch(button, out var unnamedAncestorDepth));
        Assert.Equal(buttonDepth, unnamedAncestorDepth);

        Assert.True(Selector("* > Button").TryMatch(button, out var wildcardAncestorDepth));
        Assert.Equal(buttonDepth, wildcardAncestorDepth);

        Assert.True(Selector("Panel > *").TryMatch(button, out var wildcardTargetDepth));
        Assert.Equal(panelDepth, wildcardTargetDepth);
    }

    [Fact]
    public void TryMatchFailsAndReturnsZeroWhenNoRelationshipMatches()
    {
        var root = MakePanel();
        var button = new Button();
        root.Children.Add(button);

        Assert.False(Selector("StackPanel Button").TryMatch(button, out var depth));
        Assert.Equal(0, depth);
    }

    [Fact]
    public void DefaultResolverContainsNoRelationshipRules()
    {
        Assert.False(UiStyleResolver.Default.HasRelationships);
        Assert.False(UiStyleResolver.Default.HasSiblingRelationships);
    }

    [Fact]
    public void ResolverRelationshipFlagsAggregateAcrossSheets()
    {
        var descendant = new UiStyleResolver([], [UiStyleSheet.Parse(".a .b { opacity: 0.4; }")]);
        Assert.True(descendant.HasRelationships);
        Assert.False(descendant.HasSiblingRelationships);

        var sibling = new UiStyleResolver([], [UiStyleSheet.Parse(".a + .b { opacity: 0.4; }")]);
        Assert.True(sibling.HasRelationships);
        Assert.True(sibling.HasSiblingRelationships);
    }

    [Fact]
    public void NestedAncestorsWithDifferentElementDepthsUseTheMaximumContribution()
    {
        var far = new SharedDerived();
        var near = new SharedBase();
        var target = new Panel();
        var root = new FixedDepth();
        root.Children.Add(far);
        far.Children.Add(near);
        near.Children.Add(target);
        var sharedDepth = UiStyleSelector.For<SharedDerived>().TargetTypeDepth;
        var panelDepth = UiStyleSelector.For<Panel>().TargetTypeDepth;
        var shallowDepth = UiStyleSelector.For<SharedBase>().TargetTypeDepth;

        Assert.True(Selector("shared Panel").TryMatch(target, out var depth));
        Assert.Equal(sharedDepth + panelDepth, depth);
        Assert.True(sharedDepth > shallowDepth);

        var sharedFirst = new UiStyleResolver([], [
            UiStyleSheet.Parse("""
                               shared Panel { background: #010101; }
                               FixedDepth Panel { background: #040404; }
                               """)
        ]);
        var shallowFirst = new UiStyleResolver([], [
            UiStyleSheet.Parse("""
                               FixedDepth Panel { background: #040404; }
                               shared Panel { background: #010101; }
                               """)
        ]);

        Assert.Equal(Brush(1), sharedFirst.Resolve(target).GetValue(Region.BackgroundProperty));
        Assert.Equal(Brush(1), shallowFirst.Resolve(target).GetValue(Region.BackgroundProperty));
    }

    [Fact]
    public void GeneralSiblingEnumerationUsesTheMaximumContribution()
    {
        var high = new SharedDerived();
        var low = new SharedBase();
        var target = new Panel();
        var root = new FixedDepth();
        root.Children.Add(low);
        root.Children.Add(high);
        root.Children.Add(target);
        var expected = UiStyleSelector.For<SharedDerived>().TargetTypeDepth
                       + UiStyleSelector.For<Panel>().TargetTypeDepth;

        Assert.True(Selector("shared ~ Panel").TryMatch(target, out var depth));
        Assert.Equal(expected, depth);

        var sharedFirst = new UiStyleResolver([], [
            UiStyleSheet.Parse("""
                               shared ~ Panel { background: #010101; }
                               FixedDepth > Panel { background: #040404; }
                               """)
        ]);
        var shallowFirst = new UiStyleResolver([], [
            UiStyleSheet.Parse("""
                               FixedDepth > Panel { background: #040404; }
                               shared ~ Panel { background: #010101; }
                               """)
        ]);

        Assert.Equal(Brush(1), sharedFirst.Resolve(target).GetValue(Region.BackgroundProperty));
        Assert.Equal(Brush(1), shallowFirst.Resolve(target).GetValue(Region.BackgroundProperty));
    }

    [Fact]
    public void ResolverRecomputesTheChainContributionForTheSameTargetType()
    {
        var resolver = new UiStyleResolver([], [
            UiStyleSheet.Parse("""
                               shared Panel { background: #010101; }
                               FixedDepth Panel { background: #040404; }
                               """)
        ]);
        var nearOnly = new SharedBase();
        var target = new Panel();
        var root = new FixedDepth();
        root.Children.Add(nearOnly);
        nearOnly.Children.Add(target);

        Assert.Equal(Brush(4), resolver.Resolve(target).GetValue(Region.BackgroundProperty));

        var far = new SharedDerived();
        root.Children.Add(far);
        far.Children.Add(nearOnly);
        Assert.Equal(Brush(1), resolver.Resolve(target).GetValue(Region.BackgroundProperty));

        far.Children.Add(target);
        Assert.Equal(Brush(1), resolver.Resolve(target).GetValue(Region.BackgroundProperty));
    }

    [Fact]
    public void OnlyTheTargetSegmentSelectsWhichRulesBind()
    {
        var resolver = new UiStyleResolver([], [
            UiStyleSheet.Parse("""
                               .host Button { opacity: 0.4; }
                               .host Panel { opacity: 0.7; }
                               """)
        ]);
        var host = MakePanel("host");
        var button = new Button();
        var panel = new Panel();
        host.Children.Add(button);
        host.Children.Add(panel);

        Assert.Equal(0.4, resolver.Resolve(button).GetValue(UiNode.OpacityProperty));
        Assert.Equal(0.7, resolver.Resolve(panel).GetValue(UiNode.OpacityProperty));
    }

    [Fact]
    public void RulesArePreBoundByTargetTypeEvenWhenRelationshipsDoNotMatch()
    {
        var resolver = new UiStyleResolver([], [UiStyleSheet.Parse(".host Button { background: nope; }")]);

        var error = Assert.Throws<UiStyleParseException>(() => resolver.Resolve(new Button()));

        Assert.Equal(UiStyleParseError.InvalidValue, error.Error);
    }

    [Fact]
    public void BoundDeclarationsAreCachedPerTargetClrType()
    {
        CountingNode.ConvertCount = 0;
        var resolver = new UiStyleResolver([], [UiStyleSheet.Parse(".host CountingNode { count-prop: 1; }")]);
        var host = MakePanel("host");
        var first = new CountingNode();
        var second = new CountingNode();
        host.Children.Add(first);
        host.Children.Add(second);

        Assert.Equal(7d, resolver.Resolve(first).GetValue(CountingNode.CountProperty));
        Assert.Equal(7d, resolver.Resolve(second).GetValue(CountingNode.CountProperty));
        Assert.Equal(1, CountingNode.ConvertCount);
    }

    [Fact]
    public void PseudoClassInAncestorSegmentAllowsLayoutProperties()
    {
        var rule = SingleRule(".panel:hover .button { width: 10; }");

        var declaration = Assert.Single(rule.Bind(typeof(Button)));
        Assert.Same(UiNode.WidthProperty, declaration.Setter.Property);
        Assert.Equal(10d, declaration.Setter.BoxedValue);
    }

    [Fact]
    public void PseudoClassOnTheTargetSegmentMatchesOnlyWhileActive()
    {
        var resolver = new UiStyleResolver([], [UiStyleSheet.Parse(".host .target:loading { opacity: 0.4; }")]);
        var host = MakePanel("host");
        var target = new PseudoNode("target");
        host.Children.Add(target);

        Assert.Equal(1d, resolver.Resolve(target).GetValue(UiNode.OpacityProperty));

        target.Activate("loading", true);
        Assert.Equal(0.4, resolver.Resolve(target).GetValue(UiNode.OpacityProperty));

        target.Activate("loading", false);
        Assert.Equal(1d, resolver.Resolve(target).GetValue(UiNode.OpacityProperty));
    }

    private sealed class PseudoPanel : Panel
    {
        internal PseudoPanel(params string[] classes)
        {
            foreach (var className in classes)
                Classes.Add(className);
        }

        internal void Set(string name, bool active) => SetPseudoClass(UiPseudoClass.Get(name), active);
    }

    private sealed class PseudoNode : UiNode
    {
        internal PseudoNode(params string[] classes)
        {
            foreach (var className in classes)
                Classes.Add(className);
        }

        internal void Activate(string name, bool active) => SetPseudoClass(UiPseudoClass.Get(name), active);
    }

    private sealed class FixedDepth : Panel;

    private class SharedBase : Panel
    {
        static SharedBase()
        {
            UiCssRegistry.RegisterElement<SharedBase>("shared");
        }
    }

    private sealed class SharedDerived : SharedBase
    {
        static SharedDerived()
        {
            UiCssRegistry.RegisterElement<SharedDerived>("shared");
        }
    }

    private sealed class CountingNode : UiNode
    {
        static CountingNode()
        {
            UiCssRegistry.RegisterProperty<CountingNode, double>("count-prop", CountProperty, _ =>
            {
                ConvertCount++;
                return 7d;
            });
        }

        internal static int ConvertCount;

        internal static readonly UiProperty<double> CountProperty =
            UiProperty.Register<CountingNode, double>("Count", 0);
    }
}