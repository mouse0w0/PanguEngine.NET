using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Styling;

namespace PanguEngine.Tests.Client.UI.Styling;

public sealed class UiCssVariableIntegrationTests
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
        var screen = new UiScreen(panel);
        screen.SetStyleSheets([UiStyleSheet.Parse($"Panel {{ {declarations} }}")]);
        Assert.Equal(expected, panel.Opacity);
    }

    [Fact]
    public void BaseImportantVariableBeatsNormalAuthorButNotImportantAuthor()
    {
        var panel = new Panel();
        var screen = new UiScreen(panel);
        screen.SetBaseStyleSheets([UiStyleSheet.Parse("Panel { --n: 0.4 !important; }")]);
        screen.SetStyleSheets([UiStyleSheet.Parse("Panel { --n: 0.8; opacity: var(--n); }")]);
        Assert.Equal(0.4, panel.Opacity);
        screen.SetStyleSheets([UiStyleSheet.Parse("Panel { --n: 0.8 !important; opacity: var(--n); }")]);
        Assert.Equal(0.8, panel.Opacity);
    }

    [Fact]
    public void ChildDefinitionOverridesInheritedImportantVariable()
    {
        var root = new Panel { StyleId = "root" };
        var child = new Panel { StyleId = "child" };
        root.Children.Add(child);
        var screen = new UiScreen(root);
        screen.SetStyleSheets([
            UiStyleSheet.Parse("""
                               #root { --n: 0.4 !important; }
                               #child { --n: 0.8; opacity: var(--n); }
                               """)
        ]);
        Assert.Equal(0.8, child.Opacity);
    }

    [Fact]
    public void ImportantVariableShorthandPreservesEdgePriorityAndLocalMasking()
    {
        var panel = new Panel();
        var screen = new UiScreen(panel);
        screen.SetStyleSheets([
            UiStyleSheet.Parse("""
                               Panel { --n: 4px 8px; padding: var(--n) !important; padding-left: 12px; padding-right: 16px !important; }
                               """)
        ]);
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
        var screen = new UiScreen(panel);
        screen.SetStyleSheets([
            UiStyleSheet.Parse("""
                               #missing, Panel { --n: 0.4 !important; }
                               .target { --n: 0.8 !important; }
                               Panel { opacity: var(--n); }
                               """)
        ]);
        Assert.Equal(0.8, panel.Opacity);
    }

    [Fact]
    public void MatchingBranchControlsVariableConsumerPriorityAndSource()
    {
        var panel = new Panel { StyleId = "target" };
        panel.Classes.Add("target");
        var screen = new UiScreen(panel);
        screen.SetStyleSheets([
            UiStyleSheet.Parse("""
                               Panel { --n: 0.4; }
                               #target, Panel { opacity: var(--n) !important; }
                               .target { opacity: 0.8 !important; }
                               """)
        ]);
        Assert.Equal(0.4, panel.Opacity);
        Assert.Equal("#target", Assert.Single(panel.GetStyleValueSources(UiNode.OpacityProperty)).SelectorText);
        panel.StyleId = null;
        Assert.Equal(0.8, panel.Opacity);
        Assert.Equal(".target", Assert.Single(panel.GetStyleValueSources(UiNode.OpacityProperty)).SelectorText);
    }

    [Fact]
    public void ListPseudoBranchAllowsVariableLayoutConsumers()
    {
        var panel = new Panel();
        var screen = new UiScreen(panel);
        screen.SetStyleSheets([
            UiStyleSheet.Parse("""
                               Panel { --n: 4px; }
                               Panel, .missing:hover { padding: var(--n) !important; }
                               """)
        ]);
        Assert.Equal(new Thickness(4), panel.Padding);
    }

    [Fact]
    public void ListPseudoVariableDefinitionAllowsLayout()
    {
        var panel = new Panel();
        var screen = new UiScreen(panel);
        screen.SetStyleSheets([
            UiStyleSheet.Parse("""
                               Panel, .missing:hover { --n: 4px !important; }
                               Panel { padding: var(--n); }
                               """)
        ]);
        Assert.Equal(new Thickness(4), panel.Padding);
    }

    [Theory]
    [InlineData("--n: 0.4 !important !important; opacity: var(--n);")]
    [InlineData("--n: 0.4 !important trailing; opacity: var(--n);")]
    [InlineData("--n: 0.4; opacity: var(--n) !important !important;")]
    public void MalformedVariableImportanceRetainsInvalidValueDiagnostic(string declarations)
    {
        var panel = new Panel();
        var screen = new UiScreen(panel);
        var error = Assert.Throws<UiStyleParseException>(() => screen.SetStyleSheets([
            UiStyleSheet.Parse($"Panel {{ {declarations} }}", "important-variables.css")
        ]));
        Assert.Equal(UiStyleParseError.InvalidValue, error.Error);
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
        var screen = new UiScreen(node);
        screen.SetStyleSheets([UiStyleSheet.Parse($"CountingNode {{ {declarations} }}")]);
        Assert.Equal(expected, node.GetValue(CountingNode.ValueProperty));
    }

    [Fact]
    public void MatchingListBranchesConvertAVariableDeclarationOncePerNode()
    {
        var node = new CountingNode { StyleId = "target" };
        var resolver = new UiStyleResolver([], [
            UiStyleSheet.Parse("""
                               CountingNode { --n: value; }
                               #target, CountingNode, CountingNode { raw: var(--n) !important; }
                               """)
        ]);
        CountingNode.Conversions = 0;
        var snapshot = node.ComputeStyleSnapshot(resolver);
        Assert.Equal("value", snapshot.GetValue(CountingNode.ValueProperty));
        Assert.Equal(1, CountingNode.Conversions);
        Assert.Equal("#target", Assert.Single(snapshot.GetSources(CountingNode.ValueProperty)).SelectorText);
    }

    [Fact]
    public void ImportantVariableAliasStripsTrailingCommentsAndUsesFallback()
    {
        var panel = new Panel();
        var screen = new UiScreen(panel);
        screen.SetStyleSheets([
            UiStyleSheet.Parse("""
                               Panel { --n: var(--missing, 4px) !important /* tail */; }
                               Panel { --n: 8px; padding: var(--n); }
                               """)
        ]);
        Assert.Equal(new Thickness(4), panel.Padding);
    }

    [Fact]
    public void ImportantVariableAndConsumerBeatMoreSpecificNormalRules()
    {
        var panel = new Panel { StyleId = "target" };
        var screen = new UiScreen(panel);
        screen.SetStyleSheets([
            UiStyleSheet.Parse("""
                               Panel { --alpha: 0.4 !important; opacity: var(--alpha) !important; }
                               #target { --alpha: 0.8; opacity: 0.9; }
                               """)
        ]);
        Assert.Equal(0.4, panel.Opacity);
        Assert.Equal("Panel", Assert.Single(panel.GetStyleValueSources(UiNode.OpacityProperty)).SelectorText);
    }

    [Fact]
    public void EqualSpecificityVariableConsumersKeepFirstMatchingBranchSource()
    {
        var panel = new Panel();
        panel.Classes.Add("first");
        panel.Classes.Add("second");
        var screen = new UiScreen(panel);
        screen.SetStyleSheets([
            UiStyleSheet.Parse("""
                               Panel { --alpha: 0.4; }
                               .first, .second { opacity: var(--alpha) !important; }
                               """)
        ]);
        Assert.Equal(0.4, panel.Opacity);
        Assert.Equal(".first", Assert.Single(panel.GetStyleValueSources(UiNode.OpacityProperty)).SelectorText);
        panel.Classes.Remove("first");
        Assert.Equal(".second", Assert.Single(panel.GetStyleValueSources(UiNode.OpacityProperty)).SelectorText);
    }

    [Fact]
    public void UnmatchedMalformedImportantVariableIsDeferredUntilMatched()
    {
        var panel = new Panel();
        var screen = new UiScreen(panel);
        screen.SetStyleSheets([
            UiStyleSheet.Parse("""
                               .inactive { --n: 0.4 !important !important; }
                               Panel { opacity: var(--n, 1); }
                               """)
        ]);
        Assert.Equal(1d, panel.Opacity);
        var error = Assert.Throws<UiStyleParseException>(() => panel.Classes.Add("inactive"));
        Assert.Equal(UiStyleParseError.InvalidValue, error.Error);
        Assert.DoesNotContain("inactive", panel.Classes);
        Assert.Equal(1d, panel.Opacity);
    }

    [Fact]
    public void PseudoConsumerCanUseImportantVariableForPaint()
    {
        var panel = new Panel { StyleId = "target" };
        var screen = new UiScreen(panel);
        screen.SetStyleSheets([
            UiStyleSheet.Parse("""
                               Panel { --alpha: 0.4 !important; }
                               Panel:hover { opacity: var(--alpha) !important; }
                               #target { opacity: 0.8; }
                               """)
        ]);
        Assert.Equal(0.8, panel.Opacity);
        panel.SetHovered(true);
        Assert.Equal(0.4, panel.Opacity);
        panel.SetHovered(false);
        Assert.Equal(0.8, panel.Opacity);
    }

    [Fact]
    public void FunctionInternalMarkerDoesNotMakeTheDeclarationImportant()
    {
        var node = new CountingNode();
        var screen = new UiScreen(node);
        screen.SetStyleSheets([UiStyleSheet.Parse("CountingNode { raw: fn(!important); raw: later; }")]);
        Assert.Equal("later", node.GetValue(CountingNode.ValueProperty));
    }

    private sealed class CountingNode : UiNode
    {
        internal static int Conversions;

        internal static readonly UiProperty<string> ValueProperty =
            UiProperty.Register<CountingNode, string>("Value", "");

        static CountingNode() => UiCssRegistry.RegisterProperty<CountingNode, string>("raw", ValueProperty, value =>
        {
            Conversions++;
            return value;
        });
    }
}