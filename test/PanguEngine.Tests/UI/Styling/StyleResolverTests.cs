using PanguEngine.UI;
using PanguEngine.UI.Controls;
using PanguEngine.UI.Drawing;
using PanguEngine.UI.Styling;

namespace PanguEngine.Tests.UI.Styling;

public sealed class StyleResolverTests
{
    [Theory]
    [InlineData(StyleOrigin.Base)]
    [InlineData(StyleOrigin.Author)]
    public void EarlierSpecificRuleWinsOverLaterSheet(StyleOrigin origin)
    {
        var defaultSheet = Sheet(
            Rule(StyleSelector.For<Button>(classes: ["primary"]),
                StyleSetter.Create(Region.BackgroundProperty, Brush(1))));
        var overrideSheet = Sheet(
            Rule(StyleSelector.For<Button>(),
                StyleSetter.Create(Region.BackgroundProperty, Brush(2))));
        var resolver = origin == StyleOrigin.Base
            ? new StyleResolver([defaultSheet, overrideSheet], [])
            : new StyleResolver([], [defaultSheet, overrideSheet]);
        var button = new Button();
        button.Classes.Add("primary");

        Assert.Equal(Brush(1), resolver.Resolve(button).GetValue(Region.BackgroundProperty));
    }

    [Fact]
    public void MoreSpecificRuleWinsWithinOneSheetPerProperty()
    {
        var sheet = Sheet(
            Rule(StyleSelector.For<Control>(), StyleSetter.Create(Region.BackgroundProperty, Brush(1))),
            Rule(StyleSelector.For<Button>(classes: ["primary"]), StyleSetter.Create(Region.BackgroundProperty, Brush(2))));
        var button = new Button();
        button.Classes.Add("primary");

        Assert.Equal(Brush(2), new StyleResolver([], [sheet]).Resolve(button).GetValue(Region.BackgroundProperty));
    }

    [Fact]
    public void DeclarationOrderBreaksTieWithinOneSheet()
    {
        var sheet = Sheet(
            Rule(StyleSelector.For<Button>(), StyleSetter.Create(Region.BackgroundProperty, Brush(1))),
            Rule(StyleSelector.For<Button>(), StyleSetter.Create(Region.BackgroundProperty, Brush(2))));

        Assert.Equal(Brush(2), new StyleResolver([], [sheet]).Resolve(new Button()).GetValue(Region.BackgroundProperty));
    }

    [Fact]
    public void TypeDepthIncreasesSpecificityOverBaseSelector()
    {
        var sheet = Sheet(
            Rule(StyleSelector.For<Control>(), StyleSetter.Create(Region.BackgroundProperty, Brush(1))),
            Rule(StyleSelector.For<Button>(), StyleSetter.Create(Region.BackgroundProperty, Brush(2))));

        Assert.Equal(Brush(2), new StyleResolver([], [sheet]).Resolve(new Button()).GetValue(Region.BackgroundProperty));
    }

    [Fact]
    public void ResolveReturnsDescriptorDefaultWhenNoRuleMatches()
    {
        var sheet = Sheet(Rule(
            StyleSelector.For<Button>(classes: ["absent"]),
            StyleSetter.Create(Node.OpacityProperty, 0.5)));
        var result = new StyleResolver([], [sheet]).Resolve(new Button());

        Assert.Equal(1, result.GetValue(Node.OpacityProperty));
    }

    [Fact]
    public void DefaultBaseStyleSheetsDefineBuiltInButtonRules()
    {
        Assert.Single(StyleResolver.Default.BaseStyleSheets);
        Assert.Equal("pangu/ui/default.css", StyleResolver.Default.BaseStyleSheets[0].SourceName);
        Assert.Empty(StyleResolver.Default.StyleSheets);
        var result = StyleResolver.Default.Resolve(new Button());

        Assert.Equal(new Thickness(12, 7), result.GetValue(Region.PaddingProperty));
        Assert.Equal(new SolidColorBrush(48, 54, 62), result.GetValue(Region.BackgroundProperty));
        Assert.Equal(new SolidColorBrush(92, 103, 116), result.GetValue(Region.BorderBrushProperty));
        Assert.Equal(new Thickness(1), result.GetValue(Region.BorderThicknessProperty));
        Assert.Equal(new Color(242, 244, 247), result.GetValue(Node.ColorProperty));
        var source = Assert.Single(result.GetSources(Region.BackgroundProperty));
        Assert.Equal("pangu/ui/default.css", source.SheetSourceName);
        Assert.Equal(StyleOrigin.Base, source.Origin);
        Assert.Equal(0, source.SheetIndex);
        Assert.Equal("background-color", source.CssPropertyName);
        Assert.NotNull(source.SourceLocation);
        Assert.Equal("pangu/ui/default.css", source.SourceLocation!.SourceName);
    }

    [Fact]
    public void AuthorRulesOverrideMoreSpecificBaseRules()
    {
        var baseSheet = Sheet(Rule(
            StyleSelector.For<Button>(classes: ["primary"]),
            StyleSetter.Create(Region.BackgroundProperty, Brush(1))));
        var authorSheet = Sheet(Rule(
            StyleSelector.For<Button>(),
            StyleSetter.Create(Region.BackgroundProperty, Brush(2))));
        var button = new Button();
        button.Classes.Add("primary");
        var resolver = new StyleResolver([baseSheet], [authorSheet]);

        Assert.Equal(Brush(2), resolver.Resolve(button).GetValue(Region.BackgroundProperty));
    }

    [Theory]
    [InlineData(StyleOrigin.Base)]
    [InlineData(StyleOrigin.Author)]
    public void LaterSheetWinsWhenSpecificityIsEqual(StyleOrigin origin)
    {
        var first = Sheet(Rule(StyleSelector.For<Button>(), StyleSetter.Create(Region.BackgroundProperty, Brush(1))));
        var second = Sheet(Rule(StyleSelector.For<Button>(), StyleSetter.Create(Region.BackgroundProperty, Brush(2))));
        var resolver = origin == StyleOrigin.Base
            ? new StyleResolver([first, second], [])
            : new StyleResolver([], [first, second]);

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
            StyleSelector.For<Button>(),
            StyleSetter.Create(Region.BackgroundProperty, Brush(7))));
        var source = Assert.Single(
            new StyleResolver(StyleResolver.Default.BaseStyleSheets, [sheet])
                .Resolve(new Button()).GetSources(Region.BackgroundProperty));

        Assert.Equal("Button", source.SelectorText);
        Assert.Equal(StyleOrigin.Author, source.Origin);
        Assert.Equal(0, source.SheetIndex);
        Assert.Equal(0, source.RuleIndex);
        Assert.Equal(0, source.DeclarationIndex);
        Assert.Null(source.CssPropertyName);
        Assert.False(source.IsMaskedByLocalValue);
    }

    [Fact]
    public void SelectorListAppliesOneDeclarationWhenAnyBranchMatches()
    {
        var resolver = new StyleResolver([], [StyleSheet.Parse(
            ".missing, .active { opacity: 0.4; }")]);
        var button = new Button();
        button.Classes.Add("active");

        Assert.Equal(0.4, resolver.Resolve(button).GetValue(Node.OpacityProperty));
    }

    [Fact]
    public void SelectorListUsesHighestMatchingBranchSpecificity()
    {
        var resolver = new StyleResolver([], [StyleSheet.Parse(
            ".active, #target { opacity: 0.4; } Button.active { opacity: 0.8; }")]);
        var button = new Button { StyleId = "target" };
        button.Classes.Add("active");

        var result = resolver.Resolve(button);
        var source = Assert.Single(result.GetSources(Node.OpacityProperty));

        Assert.Equal(0.4, result.GetValue(Node.OpacityProperty));
        Assert.Equal("#target", source.SelectorText);
    }

    [Fact]
    public void EqualMatchingBranchSpecificityKeepsFirstBranchSource()
    {
        var resolver = new StyleResolver([], [StyleSheet.Parse(
            ".first, .second { opacity: 0.4; }")]);
        var button = new Button();
        button.Classes.Add("first");
        button.Classes.Add("second");

        var source = Assert.Single(resolver.Resolve(button).GetSources(Node.OpacityProperty));

        Assert.Equal(".first", source.SelectorText);
    }

    [Fact]
    public void DuplicateMatchingBranchesApplyOneDeclaration()
    {
        var resolver = new StyleResolver([], [StyleSheet.Parse(
            ".active, .active { opacity: 0.4; }")]);
        var button = new Button();
        button.Classes.Add("active");

        var result = resolver.Resolve(button);

        Assert.Equal(0.4, result.GetValue(Node.OpacityProperty));
        Assert.Single(result.GetSources(Node.OpacityProperty));
    }

    [Fact]
    public void UnmatchedHighSpecificityBranchDoesNotAffectMatchingBranch()
    {
        var resolver = new StyleResolver([], [StyleSheet.Parse(
            "#absent, .active { opacity: 0.4; }\nButton.active { opacity: 0.8; }")]);
        var button = new Button();
        button.Classes.Add("active");

        var result = resolver.Resolve(button);

        Assert.Equal(0.8, result.GetValue(Node.OpacityProperty));
        Assert.Equal("Button.active", Assert.Single(result.GetSources(Node.OpacityProperty)).SelectorText);
    }

    [Fact]
    public void SelectorListCanBindDifferentTargetTypesWithoutDroppingAvailableProperties()
    {
        var resolver = new StyleResolver([], [StyleSheet.Parse(
            "Region#target, Button.active { font-size: 12px; }")]);
        var button = new Button { StyleId = "target" };
        button.Classes.Add("active");

        Assert.Equal(12, resolver.Resolve(button).GetValue(Button.FontSizeProperty));
    }

    [Fact]
    public void CssTargetDepthWinsBeforeRuleOrder()
    {
        var resolver = new StyleResolver([], [StyleSheet.Parse("""
            Button { background: #010203; }
            Control { background: #040506; }
            """)]);

        Assert.Equal(new SolidColorBrush(1, 2, 3), resolver.Resolve(new Button()).GetValue(Region.BackgroundProperty));
    }

    [Fact]
    public void SourceDeclarationIndexIncludesEarlierRules()
    {
        var resolver = new StyleResolver([], [StyleSheet.Parse("""
            Button { padding: 2px; background: #010203; }
            Button { background: #040506; }
            """, "order.css")]);

        var source = Assert.Single(resolver.Resolve(new Button()).GetSources(Region.BackgroundProperty));

        Assert.Equal(StyleOrigin.Author, source.Origin);
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

        var value = new StyleResolver([], [StyleSheet.Parse(css)]).Resolve(node).GetValue(Node.OpacityProperty);

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

        var value = new StyleResolver([], [StyleSheet.Parse(css)]).Resolve(node).GetValue(Region.BackgroundProperty);

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

        var value = new StyleResolver([], [StyleSheet.Parse(css)]).Resolve(node).GetValue(Region.BackgroundProperty);

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

        var value = new StyleResolver([], [StyleSheet.Parse(css)]).Resolve(node).GetValue(Region.BackgroundProperty);

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

        var snapshot = new StyleResolver([], [StyleSheet.Parse(css)]).Resolve(node);

        Assert.Equal(wildcardFirst ? Brush(4) : Brush(1), snapshot.GetValue(Region.BackgroundProperty));
        Assert.Equal(".danger",
            Assert.Single(snapshot.GetSources(Region.BackgroundProperty)).SelectorText);
    }

    [Fact]
    public void BareWildcardAppliesAcrossNodeTypesWithoutConditions()
    {
        var resolver = new StyleResolver([], [StyleSheet.Parse("* { opacity: 0.4; }")]);

        Assert.Equal(0.4, resolver.Resolve(new Panel()).GetValue(Node.OpacityProperty));
        Assert.Equal(0.4, resolver.Resolve(new Button()).GetValue(Node.OpacityProperty));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StronglyTypedSelectorBeatsWildcardRegardlessOfOrder(bool csharpFirst)
    {
        var csharp = Sheet(Rule(
            StyleSelector.For<Node>(classes: ["danger"]),
            StyleSetter.Create(Node.OpacityProperty, 0.4)));
        var wildcard = StyleSheet.Parse("*.danger { opacity: 0.1; }");
        var resolver = csharpFirst
            ? new StyleResolver([], [csharp, wildcard])
            : new StyleResolver([], [wildcard, csharp]);
        var node = new Button();
        node.Classes.Add("danger");

        Assert.Equal(0.4, resolver.Resolve(node).GetValue(Node.OpacityProperty));
    }

    [Fact]
    public void UnqualifiedConditionsRequireAllClassesAndExactIdCase()
    {
        var resolver = new StyleResolver([], [StyleSheet.Parse(
            ".primary.large { opacity: 0.5; } #Save { background: #010203; }")]);

        var partial = new Button();
        partial.Classes.Add("primary");
        var partialSnapshot = resolver.Resolve(partial);
        Assert.Equal(1d, partialSnapshot.GetValue(Node.OpacityProperty));
        Assert.Empty(partialSnapshot.GetSources(Region.BackgroundProperty));

        var matched = new Button { StyleId = "Save" };
        matched.Classes.Add("primary");
        matched.Classes.Add("large");
        var matchedSnapshot = resolver.Resolve(matched);
        Assert.Equal(0.5, matchedSnapshot.GetValue(Node.OpacityProperty));
        Assert.Equal(new SolidColorBrush(1, 2, 3), matchedSnapshot.GetValue(Region.BackgroundProperty));

        var wrongCase = new Button { StyleId = "save" };
        wrongCase.Classes.Add("primary");
        wrongCase.Classes.Add("large");
        var wrongCaseSnapshot = resolver.Resolve(wrongCase);
        Assert.Equal(0.5, wrongCaseSnapshot.GetValue(Node.OpacityProperty));
        Assert.Empty(wrongCaseSnapshot.GetSources(Region.BackgroundProperty));
    }

    [Fact]
    public void RequiredPseudoClassesMustAllBeActive()
    {
        var resolver = new StyleResolver([], [StyleSheet.Parse(".danger:hover:focus { background: #040404; }")]);
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
        var resolver = new StyleResolver([], [StyleSheet.Parse("PseudoHost:loading { opacity: 0.4; }")]);
        var node = new PseudoHost();

        Assert.Empty(resolver.Resolve(node).GetSources(Node.OpacityProperty));

        node.Activate(PseudoClass.Get("loading"), true);
        Assert.Equal(0.4, resolver.Resolve(node).GetValue(Node.OpacityProperty));

        node.Activate(PseudoClass.Get("loading"), false);
        Assert.Empty(resolver.Resolve(node).GetSources(Node.OpacityProperty));
    }

    [Fact]
    public void MultiDeclarationRuleRechecksConditionsForEachNodeAndResolve()
    {
        var resolver = new StyleResolver([], [StyleSheet.Parse("""
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
            Assert.Equal(1d, snapshot.GetValue(Node.OpacityProperty));
            Assert.Empty(snapshot.GetSources(Node.OpacityProperty));
            Assert.Empty(snapshot.GetSources(Region.BackgroundProperty));
        }

        void AssertMatch(Button button)
        {
            var snapshot = resolver.Resolve(button);
            Assert.Equal(0.4, snapshot.GetValue(Node.OpacityProperty));
            Assert.Equal(Brush(7), snapshot.GetValue(Region.BackgroundProperty));
            Assert.Equal(2, Assert.Single(snapshot.GetSources(Node.OpacityProperty)).DeclarationIndex);
            Assert.Equal(1, Assert.Single(snapshot.GetSources(Region.BackgroundProperty)).DeclarationIndex);
        }
    }

    [Fact]
    public void MixedDeclarationsPreserveEdgeCascadeAndSourceOrderAcrossRules()
    {
        var resolver = new StyleResolver([], [StyleSheet.Parse("""
            Button.absent { opacity: 0.1; }
            Button.primary { padding: 1px 2px; opacity: 0.4; padding-left: 9px; }
            Button.primary { padding-top: 7px; opacity: 0.6; }
            """, "grouped.css")]);
        var node = new Button();
        node.Classes.Add("primary");

        var snapshot = resolver.Resolve(node);

        Assert.Equal(new Thickness(9, 7, 2, 1), snapshot.GetValue(Region.PaddingProperty));
        Assert.Equal(0.6, snapshot.GetValue(Node.OpacityProperty));
        var sources = snapshot.GetSources(Region.PaddingProperty);
        Assert.Equal(4, sources.Count);
        foreach (var source in sources)
        {
            Assert.Equal(StyleOrigin.Author, source.Origin);
            Assert.Equal("grouped.css", source.SheetSourceName);
            Assert.NotNull(source.SourceLocation);
            Assert.Equal(source.Component == StyleEdge.Top ? 2 : 1, source.RuleIndex);
            Assert.Equal(source.Component switch
            {
                StyleEdge.Top => 4,
                StyleEdge.Left => 3,
                _ => 1
            }, source.DeclarationIndex);
            Assert.Equal(source.Component switch
            {
                StyleEdge.Top => "padding-top",
                StyleEdge.Left => "padding-left",
                _ => "padding"
            }, source.CssPropertyName);
        }
        var opacitySource = Assert.Single(snapshot.GetSources(Node.OpacityProperty));
        Assert.Equal(2, opacitySource.RuleIndex);
        Assert.Equal(5, opacitySource.DeclarationIndex);
    }

    [Fact]
    public void ReusedRuleRetainsEachOccurrenceInCascadeAndSources()
    {
        var rule = Rule(StyleSelector.For<Button>(),
            StyleSetter.Create(Node.OpacityProperty, 0.4),
            StyleSetter.Create(Region.BackgroundProperty, Brush(7)));
        var sheet = Sheet(rule, rule);
        var resolver = new StyleResolver([sheet], [sheet, sheet]);

        var snapshot = resolver.Resolve(new Button());

        Assert.Equal(0.4, snapshot.GetValue(Node.OpacityProperty));
        Assert.Equal(Brush(7), snapshot.GetValue(Region.BackgroundProperty));
        var opacitySource = Assert.Single(snapshot.GetSources(Node.OpacityProperty));
        var backgroundSource = Assert.Single(snapshot.GetSources(Region.BackgroundProperty));
        Assert.Equal(2, opacitySource.DeclarationIndex);
        Assert.Equal(3, backgroundSource.DeclarationIndex);
        foreach (var source in new[] { opacitySource, backgroundSource })
        {
            Assert.Equal(StyleOrigin.Author, source.Origin);
            Assert.Equal(1, source.SheetIndex);
            Assert.Equal(1, source.RuleIndex);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ImportantBeatsMoreSpecificNormalRegardlessOfOrder(bool importantFirst)
    {
        const string important = "Button { opacity: 0.8 !important; }";
        const string normal = "#save.primary { opacity: 0.1; }";
        var resolver = new StyleResolver([], [StyleSheet.Parse(
            importantFirst ? important + normal : normal + important)]);
        var button = new Button { StyleId = "save" };
        button.Classes.Add("primary");

        Assert.Equal(0.8, resolver.Resolve(button).GetValue(Node.OpacityProperty));
    }

    [Theory]
    [InlineData(false, false, 0.6)]
    [InlineData(false, true, 0.6)]
    [InlineData(true, false, 0.2)]
    [InlineData(true, true, 0.6)]
    public void ImportancePrecedesOrigin(bool baseImportant, bool authorImportant, double expected)
    {
        var baseSheet = StyleSheet.Parse($"#save {{ opacity: 0.2{(baseImportant ? " !important" : "")}; }}");
        var authorSheet = StyleSheet.Parse($"Button {{ opacity: 0.6{(authorImportant ? " !important" : "")}; }}");
        var button = new Button { StyleId = "save" };

        Assert.Equal(expected, new StyleResolver([baseSheet], [authorSheet])
            .Resolve(button).GetValue(Node.OpacityProperty));
    }

    [Theory]
    [InlineData("opacity: 0.8 !important; opacity: 0.1;", 0)]
    [InlineData("opacity: 0.1; opacity: 0.8 !important;", 1)]
    [InlineData("opacity: 0.1 !important; opacity: 0.8 !important;", 1)]
    public void SameRuleImportanceAndOrderSelectOriginalSource(string declarations, int expectedIndex)
    {
        var resolver = new StyleResolver([], [StyleSheet.Parse($"Button {{ {declarations} }}", "important.css")]);

        var snapshot = resolver.Resolve(new Button());

        Assert.Equal(0.8, snapshot.GetValue(Node.OpacityProperty));
        var source = Assert.Single(snapshot.GetSources(Node.OpacityProperty));
        Assert.Equal(expectedIndex, source.DeclarationIndex);
        Assert.Equal("important.css", source.SheetSourceName);
        Assert.Equal("opacity", source.CssPropertyName);
        Assert.NotNull(source.SourceLocation);
    }

    [Theory]
    [InlineData("#save", "Button", 0.2)]
    [InlineData(".primary", "Button", 0.2)]
    [InlineData("Button", "Control", 0.2)]
    [InlineData("Button", "Button", 0.8)]
    public void ImportantDeclarationsRetainSpecificityAndRuleOrder(string first, string second, double expected)
    {
        var resolver = new StyleResolver([], [StyleSheet.Parse(
            $"{first} {{ opacity: 0.2 !important; }} {second} {{ opacity: 0.8 !important; }}")]);
        var button = new Button { StyleId = "save" };
        button.Classes.Add("primary");

        Assert.Equal(expected, resolver.Resolve(button).GetValue(Node.OpacityProperty));
    }

    [Fact]
    public void ImportantDeclarationsRetainSheetOrder()
    {
        var first = StyleSheet.Parse("Button { opacity: 0.2 !important; }");
        var second = StyleSheet.Parse("Button { opacity: 0.8 !important; }");
        var snapshot = new StyleResolver([], [first, second]).Resolve(new Button());

        Assert.Equal(0.8, snapshot.GetValue(Node.OpacityProperty));
        Assert.Equal(1, Assert.Single(snapshot.GetSources(Node.OpacityProperty)).SheetIndex);
    }

    [Theory]
    [InlineData("padding: 1px 2px !important;", "padding-left: 9px;", 2, 0)]
    [InlineData("padding-left: 9px !important;", "padding: 1px 2px;", 9, 0)]
    [InlineData("padding: 1px 2px !important;", "padding-left: 9px !important;", 9, 1)]
    public void ImportantThicknessDeclarationsCascadePerEdge(string first, string second, double left, int leftIndex)
    {
        var resolver = new StyleResolver([], [StyleSheet.Parse($"Button {{ {first} }} Button {{ {second} }}")]);
        var snapshot = resolver.Resolve(new Button());

        Assert.Equal(new Thickness(left, 1, 2, 1), snapshot.GetValue(Region.PaddingProperty));
        var sources = snapshot.GetSources(Region.PaddingProperty);
        Assert.Equal(4, sources.Count);
        Assert.Equal(leftIndex, Assert.Single(sources.Where(source => source.Component == StyleEdge.Left)).DeclarationIndex);
    }

    [Fact]
    public void ImportantCssBeatsProgrammaticSetter()
    {
        var css = StyleSheet.Parse("Button { opacity: 0.8 !important; }");
        var code = Sheet(Rule(StyleSelector.For<Button>(id: "save"), StyleSetter.Create(Node.OpacityProperty, 0.1)));
        var resolver = new StyleResolver([], [css, code]);

        Assert.Equal(0.8, resolver.Resolve(new Button { StyleId = "save" }).GetValue(Node.OpacityProperty));
        Assert.False(Assert.Single(code.Rules[0].Bind(typeof(Button))).IsImportant);
    }

    [Fact]
    public void ImportantMetadataSurvivesBindingCacheAcrossNodeTypesAndStates()
    {
        var resolver = new StyleResolver([], [StyleSheet.Parse(
            ".primary { opacity: 0.8 !important; } UiNode { opacity: 0.1; }")]);
        foreach (var node in new Node[] { new Button(), new Panel(), new Button() })
        {
            Assert.Equal(0.1, resolver.Resolve(node).GetValue(Node.OpacityProperty));
            node.Classes.Add("primary");
            Assert.Equal(0.8, resolver.Resolve(node).GetValue(Node.OpacityProperty));
            node.Classes.Remove("primary");
            Assert.Equal(0.1, resolver.Resolve(node).GetValue(Node.OpacityProperty));
        }
    }

    [Theory]
    [InlineData("Button, .primary")]
    [InlineData(".primary, Button")]
    public void ImportantSelectorListBeatsNormalRuleAcrossNodeTypes(string selectors)
    {
        var resolver = new StyleResolver([], [StyleSheet.Parse(
            $"{selectors} {{ opacity: 0.8 !important; }} #save.primary {{ opacity: 0.1; }}")]);

        foreach (var node in new Node[] { new Button(), new Panel(), new Button() })
        {
            node.StyleId = "save";
            node.Classes.Add("primary");

            var snapshot = resolver.Resolve(node);

            Assert.Equal(0.8, snapshot.GetValue(Node.OpacityProperty));
            var source = Assert.Single(snapshot.GetSources(Node.OpacityProperty));
            Assert.Equal(".primary", source.SelectorText);
            Assert.Equal(0, source.RuleIndex);
            Assert.Equal(0, source.DeclarationIndex);
        }
    }

    [Theory]
    [InlineData("Button, #save")]
    [InlineData("#save, Button")]
    public void ImportantSelectorListUsesOnlyMatchingBranchSpecificity(string selectors)
    {
        var resolver = new StyleResolver([], [StyleSheet.Parse(
            $"{selectors} {{ opacity: 0.8 !important; }} .primary {{ opacity: 0.4 !important; }}")]);
        var button = new Button { StyleId = "save" };
        button.Classes.Add("primary");

        foreach (var matchesId in new[] { true, false, true })
        {
            button.StyleId = matchesId ? "save" : "other";
            var snapshot = resolver.Resolve(button);

            Assert.Equal(matchesId ? 0.8 : 0.4, snapshot.GetValue(Node.OpacityProperty));
            var source = Assert.Single(snapshot.GetSources(Node.OpacityProperty));
            Assert.Equal(matchesId ? "#save" : ".primary", source.SelectorText);
            Assert.Equal(matchesId ? 0 : 1, source.RuleIndex);
            Assert.Equal(matchesId ? 0 : 1, source.DeclarationIndex);
        }
    }

    [Fact]
    public void ImportantSelectorListPreservesEdgePriorityWhenCachedBranchChanges()
    {
        var resolver = new StyleResolver([], [StyleSheet.Parse("""
            .primary, .secondary { padding: 1px 2px !important; }
            #save { padding-left: 9px; padding-top: 7px !important; }
            """)]);
        var button = new Button { StyleId = "save" };

        foreach (var className in new[] { "primary", "secondary", "primary" })
        {
            button.Classes.Add(className);
            var snapshot = resolver.Resolve(button);

            Assert.Equal(new Thickness(2, 7, 2, 1), snapshot.GetValue(Region.PaddingProperty));
            var sources = snapshot.GetSources(Region.PaddingProperty);
            Assert.Equal(4, sources.Count);
            foreach (var source in sources)
            {
                var isTop = source.Component == StyleEdge.Top;
                Assert.Equal(isTop ? "#save" : $".{className}", source.SelectorText);
                Assert.Equal(isTop ? 1 : 0, source.RuleIndex);
                Assert.Equal(isTop ? 2 : 0, source.DeclarationIndex);
                Assert.Equal(isTop ? "padding-top" : "padding", source.CssPropertyName);
            }

            button.Classes.Remove(className);
        }
    }

    private sealed class PseudoHost : Node
    {
        internal void Activate(PseudoClass pseudoClass, bool active) => SetPseudoClass(pseudoClass, active);
    }

    private static StyleSheet Sheet(params StyleRule[] rules) => new(rules);

    private static StyleRule Rule(StyleSelector selector, params StyleSetter[] setters) => new(selector, setters);

    private static Brush Brush(byte value) => new SolidColorBrush(value, value, value);
}
