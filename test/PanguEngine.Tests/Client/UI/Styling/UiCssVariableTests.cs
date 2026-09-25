using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Styling;

namespace PanguEngine.Tests.Client.UI.Styling;

public sealed class UiCssVariableTests
{
    [Theory]
    [InlineData("--n: 12px; padding: var(--n);", 12)]
    [InlineData("padding: var(--missing, var(--other, 8px));", 8)]
    [InlineData("--n: 4px; --n: 6px; padding: var(--n);", 6)]
    [InlineData("--n: var(--other); --other: 9px; padding: var(--n);", 9)]
    [InlineData("--N: 3px; padding: var(--n, 7px);", 7)]
    [InlineData("--n: var(--n); padding: var(--n, 5px);", 5)]
    [InlineData("--a: var(--b); --b: var(--a); padding: var(--a, 5px);", 5)]
    [InlineData("--a: var(--absent); padding: var(--a, 5px);", 5)]
    [InlineData("--a: var(--b, var(--a)); --b: 2px; padding: var(--a, 5px);", 5)]
    [InlineData("--a: var(--b, var(--c)); --b: var(--a); --c: var(--b, 2px); padding: var(--c, 5px);", 5)]
    [InlineData("--a: VAR(--b); --b: 3px; padding: VAR(--a);", 3)]
    [InlineData("---x: 3px; padding: var(---x);", 3)]
    [InlineData("--1: 3px; padding: var(--1);", 3)]
    public void ResolvesVariablesAndFallbacks(string declarations, double expected)
    {
        var panel = Apply(declarations);
        Assert.Equal(new Thickness(expected), panel.Padding);
    }

    [Theory]
    [InlineData("padding: var(--missing);")]
    [InlineData("--n: ; padding: var(--n, 8px);")]
    [InlineData("padding: var(--missing,);")]
    [InlineData("--n: nope; padding: var(--n, 8px);")]
    [InlineData("--n: 12; padding: var(--n)px;")]
    [InlineData("--n: var(--n); padding: var(--n);")]
    [InlineData("padding: var(--missing); padding: 8px;")]
    public void InvalidConsumedValuesFailStrictly(string declarations)
    {
        var error = Assert.Throws<UiStyleParseException>(() => Apply(declarations));
        Assert.Equal(UiStyleParseError.InvalidValue, error.Error);
        Assert.Equal("variables.css", error.SourceName);
    }

    [Theory]
    [InlineData("--: 1;")]
    [InlineData("padding: var();")]
    [InlineData("padding: var(color);")]
    [InlineData("padding: var(--a --b);")]
    [InlineData("padding: var(--);")]
    [InlineData("padding: var(--a;")]
    [InlineData("--a: 'unterminated;")]
    public void RejectsMalformedVariableSyntax(string declarations)
    {
        var error = Assert.Throws<UiStyleParseException>(() =>
            UiStyleSheet.Parse($"Panel {{ {declarations} }}", "variables.css"));
        Assert.Equal(UiStyleParseError.InvalidSyntax, error.Error);
    }

    [Fact]
    public void SimplifiedPropertiesKeepOriginalDeclarationOrder()
    {
        var panel = Apply("--spacing: 4px 8px; padding-left: 2px; padding: var(--spacing); padding-right: 9px;");
        Assert.Equal(new Thickness(8, 4, 9, 4), panel.Padding);
        var sources = panel.GetStyleValueSources(Region.PaddingProperty);
        Assert.Equal(4, sources.Count);
        Assert.Contains(sources, source => source.CssPropertyName == "padding-right");
    }

    [Fact]
    public void InheritedAliasesRetainTheirDefinitionScope()
    {
        var parent = new Panel();
        parent.Classes.Add("parent");
        var child = new Panel();
        child.Classes.Add("child");
        parent.Children.Add(child);
        var screen = new UiScreen(parent);
        screen.SetStyleSheets([
            UiStyleSheet.Parse("""
                               .parent { --a: 4px; --b: var(--a); }
                               .child { --a: 8px; padding: var(--b); margin: var(--a); }
                               """)
        ]);
        Assert.Equal(new Thickness(4), child.Padding);
        Assert.Equal(new Thickness(8), child.Margin);
    }

    [Fact]
    public void InvalidLocalDefinitionMasksInheritedValue()
    {
        var parent = new Panel();
        parent.Classes.Add("parent");
        var child = new Panel();
        child.Classes.Add("child");
        parent.Children.Add(child);
        var screen = new UiScreen(parent);
        screen.SetStyleSheets([
            UiStyleSheet.Parse("""
                               .parent { --a: 4px; }
                               .child { --a: var(--missing); padding: var(--a, 8px); }
                               """)
        ]);
        Assert.Equal(new Thickness(8), child.Padding);
    }

    [Fact]
    public void UnusedInvalidVariablesAndUnmatchedConsumersDoNotFail()
    {
        var panel = new Panel();
        var screen = new UiScreen(panel);
        screen.SetStyleSheets([
            UiStyleSheet.Parse("""
                               Panel { --a: var(--a); }
                               .absent { padding: var(--missing); }
                               """)
        ]);
        Assert.Equal(Thickness.Zero, panel.Padding);
    }

    [Fact]
    public void DifferentNodesOfTheSameTypeUseSeparateVariableEnvironments()
    {
        var first = new Panel { StyleId = "first" };
        var second = new Panel { StyleId = "second" };
        var root = new Panel();
        root.Children.Add(first);
        root.Children.Add(second);
        var screen = new UiScreen(root);
        screen.SetStyleSheets([
            UiStyleSheet.Parse("""
                               #first { --n: 4px; }
                               #second { --n: 8px; }
                               Panel { padding: var(--n, 0); }
                               """)
        ]);
        Assert.Equal(new Thickness(4), first.Padding);
        Assert.Equal(new Thickness(8), second.Padding);
    }

    [Theory]
    [InlineData("Panel:hover { --n: 4px; } Panel { padding: var(--n, 0); }", 0, 4)]
    [InlineData("Panel:hover { --n: 4px; } Panel { --alias: var(--n); padding: var(--alias, 0); }", 0, 4)]
    [InlineData("Panel:hover { --n: 4px; } Panel { --a: 2px; padding: var(--a, var(--n)); }", 2, 2)]
    public void PseudoVariableDependenciesCanAffectLayout(string css, double inactive, double active)
    {
        var panel = new Panel();
        var screen = new UiScreen(panel);
        screen.SetStyleSheets([UiStyleSheet.Parse(css)]);
        Assert.Equal(new Thickness(inactive), panel.Padding);
        panel.SetHovered(true);
        Assert.Equal(new Thickness(active), panel.Padding);
        panel.SetHovered(false);
        Assert.Equal(new Thickness(inactive), panel.Padding);
    }

    [Fact]
    public void PseudoConsumerCanSetLayoutThroughStaticVariable()
    {
        var panel = new Panel();
        panel.SetHovered(true);
        var screen = new UiScreen(panel);
        screen.SetStyleSheets([
            UiStyleSheet.Parse("Panel { --n: 4px; } Panel:hover { padding: var(--n); }")
        ]);
        Assert.Equal(new Thickness(4), panel.Padding);
        panel.SetHovered(false);
        Assert.Equal(Thickness.Zero, panel.Padding);
    }

    [Fact]
    public void ValueSyntaxPreservesQuotedTextAndFallbackCommas()
    {
        var node = new RawValueNode();
        var screen = new UiScreen(node);
        screen.SetStyleSheets([
            UiStyleSheet.Parse("""
                               RawValueNode { --x: 'var(--absent); /* literal */'; raw: var(--x); }
                               """)
        ]);
        Assert.Equal("'var(--absent); /* literal */'", node.GetValue(RawValueNode.ValueProperty));

        screen.SetStyleSheets([UiStyleSheet.Parse("RawValueNode { raw: var(--missing, Arial, sans-serif); }")]);
        Assert.Equal("Arial, sans-serif", node.GetValue(RawValueNode.ValueProperty));
    }

    [Fact]
    public void NestedVariablesInsideOtherFunctionsAreSubstituted()
    {
        var node = new RawValueNode();
        var screen = new UiScreen(node);
        screen.SetStyleSheets([UiStyleSheet.Parse("RawValueNode { --r: 12; raw: rgb(var(--r), 0, 0); }")]);
        Assert.Equal("rgb( 12 , 0, 0)", node.GetValue(RawValueNode.ValueProperty));
    }

    [Fact]
    public void AuthorVariablesOverrideBaseOriginAndLaterSheetsBreakTies()
    {
        var panel = new Panel { StyleId = "target" };
        var screen = new UiScreen(panel);
        screen.SetBaseStyleSheets([UiStyleSheet.Parse("#target { --n: 4px; }")]);
        screen.SetStyleSheets([
            UiStyleSheet.Parse("Panel { --n: 8px; padding: var(--n); }"),
            UiStyleSheet.Parse("Panel { --n: 12px; }")
        ]);
        Assert.Equal(new Thickness(12), panel.Padding);
    }

    [Fact]
    public void SourceIndexesIncludeCustomPropertyDeclarations()
    {
        var panel = new Panel();
        var screen = new UiScreen(panel);
        screen.SetStyleSheets([
            UiStyleSheet.Parse("""
                               Panel { --n: 0.4; opacity: var(--n); }
                               Panel { --n: 0.6; opacity: var(--n); }
                               """, "source.css")
        ]);
        var source = Assert.Single(panel.GetStyleValueSources(UiNode.OpacityProperty));
        Assert.Equal(3, source.DeclarationIndex);
        Assert.Equal(0.6, panel.Opacity);
    }

    [Fact]
    public void VariableFailuresReportTheConsumerValueAndDefinition()
    {
        var panel = new Panel();
        var screen = new UiScreen(panel);
        var error = Assert.Throws<UiStyleParseException>(() => screen.SetStyleSheets([
            UiStyleSheet.Parse("Panel {\r\n --n: var(--n);\r\n padding: var(--n);\r\n}", "diagnostic.css")
        ]));
        Assert.Equal(3, error.Line);
        Assert.Equal(11, error.Column);
        Assert.Contains("--n", error.InnerException!.Message);
        Assert.Contains("diagnostic.css:2:", error.InnerException.Message);
    }

    [Fact]
    public void MalformedReferencesKeepLocationsAfterMultilineComments()
    {
        var error = Assert.Throws<UiStyleParseException>(() => UiStyleSheet.Parse(
            "Panel { --n: rgb(1, /* first\r\nsecond */ var(color)); }", "syntax.css"));
        Assert.Equal(UiStyleParseError.InvalidSyntax, error.Error);
        Assert.Equal(2, error.Line);
        Assert.Equal(15, error.Column);
    }

    [Fact]
    public void EmptyVariablesAndFallbacksAreValidForAcceptingConverters()
    {
        var node = new RawValueNode();
        var screen = new UiScreen(node);
        screen.SetStyleSheets([UiStyleSheet.Parse("RawValueNode { --empty: ; raw: var(--empty, fallback); }")]);
        Assert.Equal("", node.GetValue(RawValueNode.ValueProperty));
        screen.SetStyleSheets([UiStyleSheet.Parse("RawValueNode { raw: var(--missing,); }")]);
        Assert.Equal("", node.GetValue(RawValueNode.ValueProperty));
    }

    [Theory]
    [InlineData("'var(--missing)'", "'var(--missing)'")]
    [InlineData("fn(  alpha  )", "fn(  alpha  )")]
    [InlineData("'escaped\\'quote;{}'", "'escaped\\'quote;{}'")]
    public void StaticValuesRetainFunctionWhitespaceAndQuotedContent(string value, string expected)
    {
        var node = new RawValueNode();
        var screen = new UiScreen(node);
        screen.SetStyleSheets([UiStyleSheet.Parse($"RawValueNode {{ raw: {value}; }}")]);
        Assert.Equal(expected, node.GetValue(RawValueNode.ValueProperty));
    }

    [Fact]
    public void UnknownConsumersAndUnmatchedVariableDefinitionsAreIgnored()
    {
        var panel = new Panel();
        var screen = new UiScreen(panel);
        screen.SetStyleSheets([
            UiStyleSheet.Parse("""
                               Panel { unknown: var(--missing); }
                               .absent { --a: var(--missing); --cycle: var(--cycle); }
                               """)
        ]);
        Assert.Equal(Thickness.Zero, panel.Padding);
    }

    [Fact]
    public void InheritedPseudoVariableCanAffectChildLayout()
    {
        var root = new Panel { StyleId = "root" };
        var child = new Panel { StyleId = "child" };
        root.Children.Add(child);
        var screen = new UiScreen(root);
        screen.SetStyleSheets([
            UiStyleSheet.Parse("""
                               #root { --n: 4px; }
                               #root:hover { --n: 8px; }
                               #child { padding: var(--n); }
                               """)
        ]);
        Assert.Equal(new Thickness(4), child.Padding);
        root.SetHovered(true);
        Assert.Equal(new Thickness(8), child.Padding);
        root.SetHovered(false);
        Assert.Equal(new Thickness(4), child.Padding);
    }

    [Fact]
    public void PseudoConsumerCanUseVariablesForPaintProperties()
    {
        var panel = new Panel();
        var screen = new UiScreen(panel);
        screen.SetStyleSheets([UiStyleSheet.Parse("Panel { --alpha: 0.4; } Panel:hover { opacity: var(--alpha); }")]);
        Assert.Equal(1d, panel.Opacity);
        panel.SetHovered(true);
        Assert.Equal(0.4, panel.Opacity);
    }

    [Fact]
    public void SuccessfulFallbackDoesNotPolluteAnotherFailureDiagnostic()
    {
        var error = Assert.Throws<UiStyleParseException>(() => Apply(
            "--bad: var(--bad); padding: var(--bad, 4px) var(--missing);"));
        Assert.Contains("--missing", error.InnerException!.Message);
        Assert.DoesNotContain("--bad", error.InnerException.Message);
    }

    [Fact]
    public void FailedAliasIncludesTheOriginalDependencySource()
    {
        var error = Assert.Throws<UiStyleParseException>(() => Apply(
            "--cycle: var(--cycle); --alias: var(--cycle); padding: var(--alias);"));
        Assert.Contains("--alias", error.InnerException!.Message);
        Assert.Contains("--cycle", error.InnerException.Message);
        Assert.Contains("Cyclic", error.InnerException.Message);
    }

    [Fact]
    public void StringsRequireEscapedLineBreaks()
    {
        Assert.Throws<UiStyleParseException>(() => UiStyleSheet.Parse("Panel { --a: 'first\nsecond'; }"));
        var node = new RawValueNode();
        var screen = new UiScreen(node);
        screen.SetStyleSheets([UiStyleSheet.Parse("RawValueNode { raw: 'first\\\r\nsecond'; }")]);
        Assert.Equal("'first\\\r\nsecond'", node.GetValue(RawValueNode.ValueProperty));
    }

    private static Panel Apply(string declarations)
    {
        var panel = new Panel();
        var screen = new UiScreen(panel);
        screen.SetStyleSheets([UiStyleSheet.Parse($"Panel {{ {declarations} }}", "variables.css")]);
        return panel;
    }

    private sealed class RawValueNode : UiNode
    {
        internal static readonly UiProperty<string> ValueProperty =
            UiProperty.Register<RawValueNode, string>("Value", "");

        static RawValueNode() =>
            UiCssRegistry.RegisterProperty<RawValueNode, string>("raw", ValueProperty, value => value);
    }
}