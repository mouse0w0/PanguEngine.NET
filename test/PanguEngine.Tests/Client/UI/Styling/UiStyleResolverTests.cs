using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Drawing;
using PanguEngine.Client.UI.Styling;

namespace PanguEngine.Tests.Client.UI.Styling;

public sealed class UiStyleResolverTests
{
    [Theory]
    [InlineData(UiStyleOrigin.Base)]
    [InlineData(UiStyleOrigin.Author)]
    public void EarlierSpecificRuleWinsOverLaterSheet(UiStyleOrigin origin)
    {
        var defaultSheet = Sheet(
            Rule(UiStyleSelector.For<Button>(classes: ["primary"]),
                UiStyleSetter.Create(Region.BackgroundProperty, Brush(1))));
        var overrideSheet = Sheet(
            Rule(UiStyleSelector.For<Button>(),
                UiStyleSetter.Create(Region.BackgroundProperty, Brush(2))));
        var resolver = origin == UiStyleOrigin.Base
            ? new UiStyleResolver([defaultSheet, overrideSheet], [])
            : new UiStyleResolver([], [defaultSheet, overrideSheet]);
        var button = new Button();
        button.Classes.Add("primary");

        Assert.Equal(Brush(1), resolver.Resolve(button).GetValue(Region.BackgroundProperty));
    }

    [Fact]
    public void MoreSpecificRuleWinsWithinOneSheetPerProperty()
    {
        var sheet = Sheet(
            Rule(UiStyleSelector.For<Control>(), UiStyleSetter.Create(Region.BackgroundProperty, Brush(1))),
            Rule(UiStyleSelector.For<Button>(classes: ["primary"]), UiStyleSetter.Create(Region.BackgroundProperty, Brush(2))));
        var button = new Button();
        button.Classes.Add("primary");

        Assert.Equal(Brush(2), new UiStyleResolver([], [sheet]).Resolve(button).GetValue(Region.BackgroundProperty));
    }

    [Fact]
    public void DeclarationOrderBreaksTieWithinOneSheet()
    {
        var sheet = Sheet(
            Rule(UiStyleSelector.For<Button>(), UiStyleSetter.Create(Region.BackgroundProperty, Brush(1))),
            Rule(UiStyleSelector.For<Button>(), UiStyleSetter.Create(Region.BackgroundProperty, Brush(2))));

        Assert.Equal(Brush(2), new UiStyleResolver([], [sheet]).Resolve(new Button()).GetValue(Region.BackgroundProperty));
    }

    [Fact]
    public void TypeDepthIncreasesSpecificityOverBaseSelector()
    {
        var sheet = Sheet(
            Rule(UiStyleSelector.For<Control>(), UiStyleSetter.Create(Region.BackgroundProperty, Brush(1))),
            Rule(UiStyleSelector.For<Button>(), UiStyleSetter.Create(Region.BackgroundProperty, Brush(2))));

        Assert.Equal(Brush(2), new UiStyleResolver([], [sheet]).Resolve(new Button()).GetValue(Region.BackgroundProperty));
    }

    [Fact]
    public void ResolveReturnsDescriptorDefaultWhenNoRuleMatches()
    {
        var sheet = Sheet(Rule(
            UiStyleSelector.For<Button>(classes: ["absent"]),
            UiStyleSetter.Create(UiNode.OpacityProperty, 0.5)));
        var result = new UiStyleResolver([], [sheet]).Resolve(new Button());

        Assert.Equal(1, result.GetValue(UiNode.OpacityProperty));
    }

    [Fact]
    public void DefaultBaseStyleSheetsDefineBuiltInButtonRules()
    {
        Assert.Single(UiStyleResolver.Default.BaseStyleSheets);
        Assert.Empty(UiStyleResolver.Default.StyleSheets);
        var result = UiStyleResolver.Default.Resolve(new Button());

        Assert.Equal(new Thickness(12, 7), result.GetValue(Region.PaddingProperty));
        Assert.Equal(new SolidColorBrush(48, 54, 62), result.GetValue(Region.BackgroundProperty));
        Assert.Equal(new SolidColorBrush(92, 103, 116), result.GetValue(Region.BorderBrushProperty));
        Assert.Equal(new Thickness(1), result.GetValue(Region.BorderThicknessProperty));
        Assert.Equal(new Color(242, 244, 247), result.GetValue(Button.ForegroundProperty));
        var source = Assert.Single(result.GetSources(Region.BackgroundProperty));
        Assert.Equal("pangu-default", source.SheetSourceName);
        Assert.Equal(UiStyleOrigin.Base, source.Origin);
        Assert.Equal(0, source.SheetIndex);
        Assert.Equal("background-color", source.CssPropertyName);
        Assert.NotNull(source.SourceLocation);
    }

    [Fact]
    public void AuthorRulesOverrideMoreSpecificBaseRules()
    {
        var baseSheet = Sheet(Rule(
            UiStyleSelector.For<Button>(classes: ["primary"]),
            UiStyleSetter.Create(Region.BackgroundProperty, Brush(1))));
        var authorSheet = Sheet(Rule(
            UiStyleSelector.For<Button>(),
            UiStyleSetter.Create(Region.BackgroundProperty, Brush(2))));
        var button = new Button();
        button.Classes.Add("primary");
        var resolver = new UiStyleResolver([baseSheet], [authorSheet]);

        Assert.Equal(Brush(2), resolver.Resolve(button).GetValue(Region.BackgroundProperty));
    }

    [Theory]
    [InlineData(UiStyleOrigin.Base)]
    [InlineData(UiStyleOrigin.Author)]
    public void LaterSheetWinsWhenSpecificityIsEqual(UiStyleOrigin origin)
    {
        var first = Sheet(Rule(UiStyleSelector.For<Button>(), UiStyleSetter.Create(Region.BackgroundProperty, Brush(1))));
        var second = Sheet(Rule(UiStyleSelector.For<Button>(), UiStyleSetter.Create(Region.BackgroundProperty, Brush(2))));
        var resolver = origin == UiStyleOrigin.Base
            ? new UiStyleResolver([first, second], [])
            : new UiStyleResolver([], [first, second]);

        var result = resolver.Resolve(new Button());

        Assert.Equal(Brush(2), result.GetValue(Region.BackgroundProperty));
        var source = Assert.Single(result.GetSources(Region.BackgroundProperty));
        Assert.Equal(origin, source.Origin);
        Assert.Equal(1, source.SheetIndex);
    }

    [Fact]
    public void ResolveRecordsSourceForWinningDeclaration()
    {
        var sheet = Sheet(Rule(
            UiStyleSelector.For<Button>(),
            UiStyleSetter.Create(Region.BackgroundProperty, Brush(7))));
        var source = Assert.Single(
            new UiStyleResolver(UiStyleResolver.Default.BaseStyleSheets, [sheet])
                .Resolve(new Button()).GetSources(Region.BackgroundProperty));

        Assert.Equal("Button", source.SelectorText);
        Assert.Equal(UiStyleOrigin.Author, source.Origin);
        Assert.Equal(0, source.SheetIndex);
        Assert.Equal(0, source.RuleIndex);
        Assert.Equal(0, source.DeclarationIndex);
        Assert.Null(source.CssPropertyName);
        Assert.False(source.IsMaskedByLocalValue);
    }

    [Fact]
    public void SelectorListAppliesOneDeclarationWhenAnyBranchMatches()
    {
        var resolver = new UiStyleResolver([], [UiStyleSheet.Parse(
            ".missing, .active { opacity: 0.4; }")]);
        var button = new Button();
        button.Classes.Add("active");

        Assert.Equal(0.4, resolver.Resolve(button).GetValue(UiNode.OpacityProperty));
    }

    [Fact]
    public void SelectorListUsesHighestMatchingBranchSpecificity()
    {
        var resolver = new UiStyleResolver([], [UiStyleSheet.Parse(
            ".active, #target { opacity: 0.4; } Button.active { opacity: 0.8; }")]);
        var button = new Button { StyleId = "target" };
        button.Classes.Add("active");

        var result = resolver.Resolve(button);
        var source = Assert.Single(result.GetSources(UiNode.OpacityProperty));

        Assert.Equal(0.4, result.GetValue(UiNode.OpacityProperty));
        Assert.Equal("#target", source.SelectorText);
    }

    [Fact]
    public void EqualMatchingBranchSpecificityKeepsFirstBranchSource()
    {
        var resolver = new UiStyleResolver([], [UiStyleSheet.Parse(
            ".first, .second { opacity: 0.4; }")]);
        var button = new Button();
        button.Classes.Add("first");
        button.Classes.Add("second");

        var source = Assert.Single(resolver.Resolve(button).GetSources(UiNode.OpacityProperty));

        Assert.Equal(".first", source.SelectorText);
    }

    [Fact]
    public void DuplicateMatchingBranchesApplyOneDeclaration()
    {
        var resolver = new UiStyleResolver([], [UiStyleSheet.Parse(
            ".active, .active { opacity: 0.4; }")]);
        var button = new Button();
        button.Classes.Add("active");

        var result = resolver.Resolve(button);

        Assert.Equal(0.4, result.GetValue(UiNode.OpacityProperty));
        Assert.Single(result.GetSources(UiNode.OpacityProperty));
    }

    [Fact]
    public void UnmatchedHighSpecificityBranchDoesNotAffectMatchingBranch()
    {
        var resolver = new UiStyleResolver([], [UiStyleSheet.Parse(
            "#absent, .active { opacity: 0.4; }\nButton.active { opacity: 0.8; }")]);
        var button = new Button();
        button.Classes.Add("active");

        var result = resolver.Resolve(button);

        Assert.Equal(0.8, result.GetValue(UiNode.OpacityProperty));
        Assert.Equal("Button.active", Assert.Single(result.GetSources(UiNode.OpacityProperty)).SelectorText);
    }

    [Fact]
    public void SelectorListCanBindDifferentTargetTypesWithoutDroppingAvailableProperties()
    {
        var resolver = new UiStyleResolver([], [UiStyleSheet.Parse(
            "Region#target, Button.active { font-size: 12px; }")]);
        var button = new Button { StyleId = "target" };
        button.Classes.Add("active");

        Assert.Equal(12, resolver.Resolve(button).GetValue(Button.FontSizeProperty));
    }

    [Fact]
    public void CssTargetDepthWinsBeforeRuleOrder()
    {
        var resolver = new UiStyleResolver([], [UiStyleSheet.Parse("""
            Button { background: #010203; }
            Control { background: #040506; }
            """)]);

        Assert.Equal(new SolidColorBrush(1, 2, 3), resolver.Resolve(new Button()).GetValue(Region.BackgroundProperty));
    }

    [Fact]
    public void SourceDeclarationIndexIncludesEarlierRules()
    {
        var resolver = new UiStyleResolver([], [UiStyleSheet.Parse("""
            Button { padding: 2px; background: #010203; }
            Button { background: #040506; }
            """, "order.css")]);

        var source = Assert.Single(resolver.Resolve(new Button()).GetSources(Region.BackgroundProperty));

        Assert.Equal(UiStyleOrigin.Author, source.Origin);
        Assert.Equal(0, source.SheetIndex);
        Assert.Equal(1, source.RuleIndex);
        Assert.Equal(2, source.DeclarationIndex);
        Assert.Equal("order.css", source.SheetSourceName);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UiNodeElementSelectorBeatsWildcardRegardlessOfOrder(bool wildcardFirst)
    {
        const string wildcard = "*.danger { opacity: 0.1; }";
        const string typed = "UiNode.danger { opacity: 0.4; }";
        var css = wildcardFirst ? wildcard + typed : typed + wildcard;
        var node = new Button();
        node.Classes.Add("danger");

        var value = new UiStyleResolver([], [UiStyleSheet.Parse(css)]).Resolve(node).GetValue(UiNode.OpacityProperty);

        Assert.Equal(0.4, value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NamedElementSelectorBeatsUnqualifiedPrefixRegardlessOfOrder(bool typedFirst)
    {
        const string unqualified = ".danger { background: #010101; }";
        const string named = "Button.danger { background: #040404; }";
        var css = typedFirst ? named + unqualified : unqualified + named;
        var node = new Button();
        node.Classes.Add("danger");

        var value = new UiStyleResolver([], [UiStyleSheet.Parse(css)]).Resolve(node).GetValue(Region.BackgroundProperty);

        Assert.Equal(Brush(4), value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClassConditionBeatsPlainElementSelectorRegardlessOfOrder(bool classFirst)
    {
        const string plain = "Button { background: #010101; }";
        const string classed = ".danger { background: #040404; }";
        var css = classFirst ? classed + plain : plain + classed;
        var node = new Button();
        node.Classes.Add("danger");

        var value = new UiStyleResolver([], [UiStyleSheet.Parse(css)]).Resolve(node).GetValue(Region.BackgroundProperty);

        Assert.Equal(Brush(4), value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IdConditionBeatsClassConditionRegardlessOfOrder(bool idFirst)
    {
        const string classed = ".danger { background: #010101; }";
        const string identified = "#save { background: #040404; }";
        var css = idFirst ? identified + classed : classed + identified;
        var node = new Button { StyleId = "save" };
        node.Classes.Add("danger");

        var value = new UiStyleResolver([], [UiStyleSheet.Parse(css)]).Resolve(node).GetValue(Region.BackgroundProperty);

        Assert.Equal(Brush(4), value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NormalizedWildcardSelectorsTieByDeclarationOrder(bool wildcardFirst)
    {
        const string wildcard = "*.danger { background: #010101; }";
        const string bare = ".danger { background: #040404; }";
        var css = wildcardFirst ? wildcard + bare : bare + wildcard;
        var node = new Button();
        node.Classes.Add("danger");

        var snapshot = new UiStyleResolver([], [UiStyleSheet.Parse(css)]).Resolve(node);

        Assert.Equal(wildcardFirst ? Brush(4) : Brush(1), snapshot.GetValue(Region.BackgroundProperty));
        Assert.Equal(".danger",
            Assert.Single(snapshot.GetSources(Region.BackgroundProperty)).SelectorText);
    }

    [Fact]
    public void BareWildcardAppliesAcrossNodeTypesWithoutConditions()
    {
        var resolver = new UiStyleResolver([], [UiStyleSheet.Parse("* { opacity: 0.4; }")]);

        Assert.Equal(0.4, resolver.Resolve(new Panel()).GetValue(UiNode.OpacityProperty));
        Assert.Equal(0.4, resolver.Resolve(new Button()).GetValue(UiNode.OpacityProperty));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StronglyTypedSelectorBeatsWildcardRegardlessOfOrder(bool csharpFirst)
    {
        var csharp = Sheet(Rule(
            UiStyleSelector.For<UiNode>(classes: ["danger"]),
            UiStyleSetter.Create(UiNode.OpacityProperty, 0.4)));
        var wildcard = UiStyleSheet.Parse("*.danger { opacity: 0.1; }");
        var resolver = csharpFirst
            ? new UiStyleResolver([], [csharp, wildcard])
            : new UiStyleResolver([], [wildcard, csharp]);
        var node = new Button();
        node.Classes.Add("danger");

        Assert.Equal(0.4, resolver.Resolve(node).GetValue(UiNode.OpacityProperty));
    }

    [Fact]
    public void UnqualifiedConditionsRequireAllClassesAndExactIdCase()
    {
        var resolver = new UiStyleResolver([], [UiStyleSheet.Parse(
            ".primary.large { opacity: 0.5; } #Save { background: #010203; }")]);

        var partial = new Button();
        partial.Classes.Add("primary");
        var partialSnapshot = resolver.Resolve(partial);
        Assert.Equal(1d, partialSnapshot.GetValue(UiNode.OpacityProperty));
        Assert.Empty(partialSnapshot.GetSources(Region.BackgroundProperty));

        var matched = new Button { StyleId = "Save" };
        matched.Classes.Add("primary");
        matched.Classes.Add("large");
        var matchedSnapshot = resolver.Resolve(matched);
        Assert.Equal(0.5, matchedSnapshot.GetValue(UiNode.OpacityProperty));
        Assert.Equal(new SolidColorBrush(1, 2, 3), matchedSnapshot.GetValue(Region.BackgroundProperty));

        var wrongCase = new Button { StyleId = "save" };
        wrongCase.Classes.Add("primary");
        wrongCase.Classes.Add("large");
        var wrongCaseSnapshot = resolver.Resolve(wrongCase);
        Assert.Equal(0.5, wrongCaseSnapshot.GetValue(UiNode.OpacityProperty));
        Assert.Empty(wrongCaseSnapshot.GetSources(Region.BackgroundProperty));
    }

    [Fact]
    public void RequiredPseudoClassesMustAllBeActive()
    {
        var resolver = new UiStyleResolver([], [UiStyleSheet.Parse(".danger:hover:focus { background: #040404; }")]);
        var node = new Button();
        node.Classes.Add("danger");

        Assert.Empty(resolver.Resolve(node).GetSources(Region.BackgroundProperty));

        node.SetHovered(true);
        Assert.Empty(resolver.Resolve(node).GetSources(Region.BackgroundProperty));

        node.SetFocused(true);
        Assert.Equal(Brush(4), resolver.Resolve(node).GetValue(Region.BackgroundProperty));
    }

    [Fact]
    public void CustomNamedPseudoClassMatchesOnlyWhileActive()
    {
        var resolver = new UiStyleResolver([], [UiStyleSheet.Parse("PseudoHost:loading { opacity: 0.4; }")]);
        var node = new PseudoHost();

        Assert.Empty(resolver.Resolve(node).GetSources(UiNode.OpacityProperty));

        node.Activate(UiPseudoClass.Get("loading"), true);
        Assert.Equal(0.4, resolver.Resolve(node).GetValue(UiNode.OpacityProperty));

        node.Activate(UiPseudoClass.Get("loading"), false);
        Assert.Empty(resolver.Resolve(node).GetSources(UiNode.OpacityProperty));
    }

    [Fact]
    public void MultiDeclarationRuleRechecksConditionsForEachNodeAndResolve()
    {
        var resolver = new UiStyleResolver([], [UiStyleSheet.Parse("""
            Button.primary#target:hover { opacity: 0.5; background: #070707; opacity: 0.4; }
            """)]);
        var node = new Button { StyleId = "target" };
        node.Classes.Add("primary");
        AssertNoMatch(node);

        node.SetHovered(true);
        AssertMatch(node);

        var other = new Button { StyleId = "target" };
        other.Classes.Add("primary");
        AssertNoMatch(other);
        other.SetHovered(true);
        AssertMatch(other);

        node.StyleId = "other";
        AssertNoMatch(node);
        node.StyleId = "target";
        node.Classes.Remove("primary");
        AssertNoMatch(node);
        node.Classes.Add("primary");
        AssertMatch(node);
        node.SetHovered(false);
        AssertNoMatch(node);
        AssertMatch(other);

        void AssertNoMatch(Button button)
        {
            var snapshot = resolver.Resolve(button);
            Assert.Equal(1d, snapshot.GetValue(UiNode.OpacityProperty));
            Assert.Empty(snapshot.GetSources(UiNode.OpacityProperty));
            Assert.Empty(snapshot.GetSources(Region.BackgroundProperty));
        }

        void AssertMatch(Button button)
        {
            var snapshot = resolver.Resolve(button);
            Assert.Equal(0.4, snapshot.GetValue(UiNode.OpacityProperty));
            Assert.Equal(Brush(7), snapshot.GetValue(Region.BackgroundProperty));
            Assert.Equal(2, Assert.Single(snapshot.GetSources(UiNode.OpacityProperty)).DeclarationIndex);
            Assert.Equal(1, Assert.Single(snapshot.GetSources(Region.BackgroundProperty)).DeclarationIndex);
        }
    }

    [Fact]
    public void MixedDeclarationsPreserveEdgeCascadeAndSourceOrderAcrossRules()
    {
        var resolver = new UiStyleResolver([], [UiStyleSheet.Parse("""
            Button.absent { opacity: 0.1; }
            Button.primary { padding: 1px 2px; opacity: 0.4; padding-left: 9px; }
            Button.primary { padding-top: 7px; opacity: 0.6; }
            """, "grouped.css")]);
        var node = new Button();
        node.Classes.Add("primary");

        var snapshot = resolver.Resolve(node);

        Assert.Equal(new Thickness(9, 7, 2, 1), snapshot.GetValue(Region.PaddingProperty));
        Assert.Equal(0.6, snapshot.GetValue(UiNode.OpacityProperty));
        var sources = snapshot.GetSources(Region.PaddingProperty);
        Assert.Equal(4, sources.Count);
        foreach (var source in sources)
        {
            Assert.Equal(UiStyleOrigin.Author, source.Origin);
            Assert.Equal("grouped.css", source.SheetSourceName);
            Assert.NotNull(source.SourceLocation);
            Assert.Equal(source.Component == UiStyleEdge.Top ? 2 : 1, source.RuleIndex);
            Assert.Equal(source.Component switch
            {
                UiStyleEdge.Top => 4,
                UiStyleEdge.Left => 3,
                _ => 1
            }, source.DeclarationIndex);
            Assert.Equal(source.Component switch
            {
                UiStyleEdge.Top => "padding-top",
                UiStyleEdge.Left => "padding-left",
                _ => "padding"
            }, source.CssPropertyName);
        }
        var opacitySource = Assert.Single(snapshot.GetSources(UiNode.OpacityProperty));
        Assert.Equal(2, opacitySource.RuleIndex);
        Assert.Equal(5, opacitySource.DeclarationIndex);
    }

    [Fact]
    public void ReusedRuleRetainsEachOccurrenceInCascadeAndSources()
    {
        var rule = Rule(UiStyleSelector.For<Button>(),
            UiStyleSetter.Create(UiNode.OpacityProperty, 0.4),
            UiStyleSetter.Create(Region.BackgroundProperty, Brush(7)));
        var sheet = Sheet(rule, rule);
        var resolver = new UiStyleResolver([sheet], [sheet, sheet]);

        var snapshot = resolver.Resolve(new Button());

        Assert.Equal(0.4, snapshot.GetValue(UiNode.OpacityProperty));
        Assert.Equal(Brush(7), snapshot.GetValue(Region.BackgroundProperty));
        var opacitySource = Assert.Single(snapshot.GetSources(UiNode.OpacityProperty));
        var backgroundSource = Assert.Single(snapshot.GetSources(Region.BackgroundProperty));
        Assert.Equal(2, opacitySource.DeclarationIndex);
        Assert.Equal(3, backgroundSource.DeclarationIndex);
        foreach (var source in new[] { opacitySource, backgroundSource })
        {
            Assert.Equal(UiStyleOrigin.Author, source.Origin);
            Assert.Equal(1, source.SheetIndex);
            Assert.Equal(1, source.RuleIndex);
        }
    }

    private sealed class PseudoHost : UiNode
    {
        internal void Activate(UiPseudoClass pseudoClass, bool active) => SetPseudoClass(pseudoClass, active);
    }

    private static UiStyleSheet Sheet(params UiStyleRule[] rules) => new(rules);

    private static UiStyleRule Rule(UiStyleSelector selector, params UiStyleSetter[] setters) => new(selector, setters);

    private static Brush Brush(byte value) => new SolidColorBrush(value, value, value);
}
