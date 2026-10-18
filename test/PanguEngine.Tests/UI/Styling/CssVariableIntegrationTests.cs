using PanguEngine.ComponentModel;
using PanguEngine.UI;
using PanguEngine.UI.Controls;
using PanguEngine.UI.Styling;

namespace PanguEngine.Tests.UI.Styling;

public sealed class CssVariableIntegrationTests
{
    [Theory]
    [InlineData("--n: 0.4 !important; --n: 0.8; opacity: var(--n);", 0.4)]
    [InlineData("--n: 0.4; --n: 0.8 !IMPORTANT; opacity: var(--n);", 0.8)]
    [InlineData("--n: 0.4 !important; --n: 0.8 !important; opacity: var(--n);", 0.8)]
    [InlineData("--n: 0.4; opacity: var(--n)!important; opacity: 0.8;", 0.4)]
    [InlineData("--n: 0.4 !important; opacity: var(--n); opacity: 0.8;", 0.8)]
    [InlineData("opacity: var(--missing, 0.4) !important; opacity: 0.8;", 0.4)]
    public void VariableAndConsumerImportanceCascadeIndependently(string declarations, double expected)
    {
        var panel = new Panel();
        var screen = new Screen(panel);
        screen.SetStyleSheets([StyleSheet.Parse($"Panel {{ {declarations} }}")]);
        screen.Root!.UpdateStyles();
        Assert.Equal(expected, panel.Opacity);
    }

    [Fact]
    public void BaseImportantVariableBeatsNormalAuthorButNotImportantAuthor()
    {
        var panel = new Panel();
        var screen = new Screen(panel);
        screen.SetBaseStyleSheets([StyleSheet.Parse("Panel { --n: 0.4 !important; }")]);
        screen.SetStyleSheets([StyleSheet.Parse("Panel { --n: 0.8; opacity: var(--n); }")]);
        screen.Root!.UpdateStyles();
        Assert.Equal(0.4, panel.Opacity);
        screen.SetStyleSheets([StyleSheet.Parse("Panel { --n: 0.8 !important; opacity: var(--n); }")]);
        screen.Root!.UpdateStyles();
        Assert.Equal(0.8, panel.Opacity);
    }

    [Fact]
    public void ChildDefinitionOverridesInheritedImportantVariable()
    {
        var root = new Panel { StyleId = "root" };
        var child = new Panel { StyleId = "child" };
        root.Children.Add(child);
        var screen = new Screen(root);
        screen.SetStyleSheets([
            StyleSheet.Parse("""
                               #root { --n: 0.4 !important; }
                               #child { --n: 0.8; opacity: var(--n); }
                               """)
        ]);
        screen.Root!.UpdateStyles();
        Assert.Equal(0.8, child.Opacity);
    }

    [Fact]
    public void ImportantVariableShorthandPreservesEdgePriorityAndLocalMasking()
    {
        var panel = new Panel();
        var screen = new Screen(panel);
        screen.SetStyleSheets([
            StyleSheet.Parse("""
                               Panel { --n: 4px 8px; padding: var(--n) !important; padding-left: 12px; padding-right: 16px !important; }
                               """)
        ]);
        screen.Root!.UpdateStyles();
        Assert.Equal(new Thickness(8, 4, 16, 4), panel.Padding);
        panel.Padding = new Thickness(20);
        Assert.All(panel.GetStyleValueSources(Region.PaddingProperty),
            source => Assert.True(source.IsMaskedByLocalValue));
        Assert.Equal(new Thickness(20), panel.Padding);
    }

    [Fact]
    public void UnmatchedListBranchesDoNotContributeVariableSpecificity()
    {
        var panel = new Panel();
        panel.Classes.Add("target");
        var screen = new Screen(panel);
        screen.SetStyleSheets([
            StyleSheet.Parse("""
                               #missing, Panel { --n: 0.4 !important; }
                               .target { --n: 0.8 !important; }
                               Panel { opacity: var(--n); }
                               """)
        ]);
        screen.Root!.UpdateStyles();
        Assert.Equal(0.8, panel.Opacity);
    }

    [Fact]
    public void MatchingBranchControlsVariableConsumerPriorityAndSource()
    {
        var panel = new Panel { StyleId = "target" };
        panel.Classes.Add("target");
        var screen = new Screen(panel);
        screen.SetStyleSheets([
            StyleSheet.Parse("""
                               Panel { --n: 0.4; }
                               #target, Panel { opacity: var(--n) !important; }
                               .target { opacity: 0.8 !important; }
                               """)
        ]);
        screen.Root!.UpdateStyles();
        Assert.Equal(0.4, panel.Opacity);
        Assert.Equal("#target", Assert.Single(panel.GetStyleValueSources(Node.OpacityProperty)).SelectorText);
        panel.StyleId = null;
        screen.Root!.UpdateStyles();
        Assert.Equal(0.8, panel.Opacity);
        Assert.Equal(".target", Assert.Single(panel.GetStyleValueSources(Node.OpacityProperty)).SelectorText);
    }

    [Fact]
    public void ListPseudoBranchAllowsVariableLayoutConsumers()
    {
        var panel = new Panel();
        var screen = new Screen(panel);
        screen.SetStyleSheets([
            StyleSheet.Parse("""
                               Panel { --n: 4px; }
                               Panel, .missing:hover { padding: var(--n) !important; }
                               """)
        ]);
        screen.Root!.UpdateStyles();
        Assert.Equal(new Thickness(4), panel.Padding);
    }

    [Fact]
    public void ListPseudoVariableDefinitionAllowsLayout()
    {
        var panel = new Panel();
        var screen = new Screen(panel);
        screen.SetStyleSheets([
            StyleSheet.Parse("""
                               Panel, .missing:hover { --n: 4px !important; }
                               Panel { padding: var(--n); }
                               """)
        ]);
        screen.Root!.UpdateStyles();
        Assert.Equal(new Thickness(4), panel.Padding);
    }

    [Theory]
    [InlineData("--n: 0.4 !important !important; opacity: var(--n);")]
    [InlineData("--n: 0.4 !important trailing; opacity: var(--n);")]
    [InlineData("--n: 0.4; opacity: var(--n) !important !important;")]
    public void MalformedVariableImportanceRetainsInvalidValueDiagnostic(string declarations)
    {
        var panel = new Panel();
        var screen = new Screen(panel);
        var error = Assert.Throws<StyleParseException>(() =>
        {
            screen.SetStyleSheets([StyleSheet.Parse($"Panel {{ {declarations} }}", "important-variables.css")]);
            screen.Root!.UpdateStyles();
        });
        Assert.Equal(StyleParseError.InvalidValue, error.Error);
        Assert.Equal("important-variables.css", error.SourceName);
    }

    [Theory]
    [InlineData("--n: '!important'; raw: var(--n);", "'!important'")]
    [InlineData("--n: ; raw: var(--n, fallback) !important;", "")]
    [InlineData("--n: !important; raw: var(--n, fallback);", "")]
    [InlineData("raw: var(--missing, !important);", "!important")]
    [InlineData("raw: fn(!important);", "fn(!important)")]
    public void ImportanceIsParsedOutsideStringsAndBeforeSubstitution(string declarations, string expected)
    {
        var node = new CountingNode();
        var screen = new Screen(node);
        screen.SetStyleSheets([StyleSheet.Parse($"CountingNode {{ {declarations} }}")]);
        screen.Root!.UpdateStyles();
        Assert.Equal(expected, node.GetValue(CountingNode.ValueProperty));
    }

    [Fact]
    public void MatchingListBranchesConvertAVariableDeclarationOncePerNode()
    {
        var node = new CountingNode { StyleId = "target" };
        var resolver = new StyleResolver([], [
            StyleSheet.Parse("""
                               CountingNode { --n: value; }
                               #target, CountingNode, CountingNode { raw: var(--n) !important; }
                               """)
        ]);
        CountingNode.Conversions = 0;
        var snapshot = resolver.Resolve(node);
        Assert.Equal("value", snapshot.GetValue(CountingNode.ValueProperty));
        Assert.Equal(1, CountingNode.Conversions);
        Assert.Equal("#target", Assert.Single(snapshot.GetSources(CountingNode.ValueProperty)).SelectorText);
    }

    [Fact]
    public void ImportantVariableAliasStripsTrailingCommentsAndUsesFallback()
    {
        var panel = new Panel();
        var screen = new Screen(panel);
        screen.SetStyleSheets([
            StyleSheet.Parse("""
                               Panel { --n: var(--missing, 4px) !important /* tail */; }
                               Panel { --n: 8px; padding: var(--n); }
                               """)
        ]);
        screen.Root!.UpdateStyles();
        Assert.Equal(new Thickness(4), panel.Padding);
    }

    [Fact]
    public void ImportantVariableAndConsumerBeatMoreSpecificNormalRules()
    {
        var panel = new Panel { StyleId = "target" };
        var screen = new Screen(panel);
        screen.SetStyleSheets([
            StyleSheet.Parse("""
                               Panel { --alpha: 0.4 !important; opacity: var(--alpha) !important; }
                               #target { --alpha: 0.8; opacity: 0.9; }
                               """)
        ]);
        screen.Root!.UpdateStyles();
        Assert.Equal(0.4, panel.Opacity);
        Assert.Equal("Panel", Assert.Single(panel.GetStyleValueSources(Node.OpacityProperty)).SelectorText);
    }

    [Fact]
    public void EqualSpecificityVariableConsumersKeepFirstMatchingBranchSource()
    {
        var panel = new Panel();
        panel.Classes.Add("first");
        panel.Classes.Add("second");
        var screen = new Screen(panel);
        screen.SetStyleSheets([
            StyleSheet.Parse("""
                               Panel { --alpha: 0.4; }
                               .first, .second { opacity: var(--alpha) !important; }
                               """)
        ]);
        screen.Root!.UpdateStyles();
        Assert.Equal(0.4, panel.Opacity);
        Assert.Equal(".first", Assert.Single(panel.GetStyleValueSources(Node.OpacityProperty)).SelectorText);
        panel.Classes.Remove("first");
        screen.Root!.UpdateStyles();
        Assert.Equal(".second", Assert.Single(panel.GetStyleValueSources(Node.OpacityProperty)).SelectorText);
    }

    [Fact]
    public void MatchedMalformedImportantVariableKeepsClassAndOldStyleOnFailure()
    {
        var panel = new Panel();
        var screen = new Screen(panel);
        screen.SetStyleSheets([
            StyleSheet.Parse("""
                               .inactive { --n: 0.4 !important !important; }
                               Panel { opacity: var(--n, 1); }
                               """)
        ]);
        Assert.Equal(1d, panel.Opacity);
        panel.Classes.Add("inactive");
        var error = Assert.Throws<StyleParseException>(screen.Root!.UpdateStyles);
        Assert.Equal(StyleParseError.InvalidValue, error.Error);
        Assert.Contains("inactive", (IReadOnlySet<string>)panel.Classes);
        Assert.Equal(1d, panel.Opacity);
        Assert.True(panel.Classes.Remove("inactive"));
        screen.Root!.UpdateStyles();
        Assert.Equal(1d, panel.Opacity);
    }

    [Fact]
    public void PseudoConsumerCanUseImportantVariableForPaint()
    {
        var panel = new Panel { StyleId = "target" };
        var screen = new Screen(panel);
        screen.SetStyleSheets([
            StyleSheet.Parse("""
                               Panel { --alpha: 0.4 !important; }
                               Panel:hover { opacity: var(--alpha) !important; }
                               #target { opacity: 0.8; }
                               """)
        ]);
        screen.Root!.UpdateStyles();
        Assert.Equal(0.8, panel.Opacity);
        panel.SetHovered(true);
        screen.Root!.UpdateStyles();
        Assert.Equal(0.4, panel.Opacity);
        panel.SetHovered(false);
        screen.Root!.UpdateStyles();
        Assert.Equal(0.8, panel.Opacity);
    }

    [Fact]
    public void FunctionInternalMarkerDoesNotMakeTheDeclarationImportant()
    {
        var node = new CountingNode();
        var screen = new Screen(node);
        screen.SetStyleSheets([StyleSheet.Parse("CountingNode { raw: fn(!important); raw: later; }")]);
        screen.Root!.UpdateStyles();
        Assert.Equal("later", node.GetValue(CountingNode.ValueProperty));
    }

    private sealed class CountingNode : Node
    {
        internal static int Conversions;

        internal static readonly Property<string> ValueProperty =
            Property.Register<CountingNode, string>("Value", "");

        static CountingNode() => CssRegistry.RegisterProperty<CountingNode, string>("raw", ValueProperty, value =>
        {
            Conversions++;
            return value;
        });
    }
}
