using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Styling;

namespace PanguEngine.Tests.Client.UI.Styling;

public sealed class UiStructuralPseudoClassTests
{
    private static UiStyleSelector Parse(string selector) =>
        Assert.Single(UiStyleSheet.Parse(selector + " { }").Rules).Selectors[0];

    [Theory]
    [InlineData(":first-child")]
    [InlineData(":last-child")]
    [InlineData(":only-child")]
    [InlineData(":nth-child(1)")]
    [InlineData(":nth-last-child(1)")]
    [InlineData(":nth-child(odd)")]
    [InlineData(":nth-last-child(-n+3)")]
    public void ParentlessNodeMatchesAsASingleSibling(string selector)
    {
        Assert.True(Parse(selector).Matches(new Panel()));
        Assert.True(Parse(selector).Matches(new Text()));
    }

    [Theory]
    [InlineData(":nth-child(2)")]
    [InlineData(":nth-last-child(even)")]
    public void SeparateRootsAreNotSiblings(string selector)
    {
        var first = new Panel();
        var second = new Panel();
        Assert.False(Parse(selector).Matches(first));
        Assert.False(Parse(selector).Matches(second));
    }

    [Fact]
    public void NamedPositionConditionsUseAllActualSiblings()
    {
        var first = new Text { Visibility = Visibility.Collapsed };
        var middle = new Panel { Visibility = Visibility.Hidden };
        var last = new Text();
        var host = new Panel();
        host.Children.Add(first);
        host.Children.Add(middle);
        host.Children.Add(last);

        Assert.True(Parse(":first-child").Matches(first));
        Assert.False(Parse(":first-child").Matches(middle));
        Assert.True(Parse(":last-child").Matches(last));
        Assert.False(Parse(":last-child").Matches(middle));
        Assert.False(Parse(":only-child").Matches(first));
        Assert.False(Parse(":only-child").Matches(middle));
        Assert.False(Parse(":only-child").Matches(last));
        Assert.True(Parse("Panel:nth-child(2)").Matches(middle));
        Assert.True(Parse("Text:nth-child(3)").Matches(last));
        Assert.False(Parse("Text:nth-child(2)").Matches(last));

        host.Children.Remove(first);
        host.Children.Remove(last);
        Assert.True(Parse(":first-child:last-child:only-child").Matches(middle));
    }

    [Fact]
    public void EmptyUsesChildCountInsteadOfTextContentOrVisibility()
    {
        var selector = Parse(":empty");
        var host = new Panel();
        Assert.True(selector.Matches(host));
        Assert.True(selector.Matches(new Text { Content = "content" }));
        host.Children.Add(new Panel { Visibility = Visibility.Collapsed });
        Assert.False(selector.Matches(host));
        host.Children.Clear();
        Assert.True(selector.Matches(host));
    }

    [Theory]
    [InlineData("first-child")]
    [InlineData("last-child")]
    [InlineData("only-child")]
    [InlineData("empty")]
    public void StructuralStateCannotBeOverriddenManually(string name)
    {
        var node = new StatePanel();
        var pseudo = UiPseudoClass.Get(name);
        var selector = UiStyleSelector.For<Panel>(pseudoClasses: [pseudo]);
        Assert.True(selector.Matches(node));
        node.Set(pseudo, false);
        Assert.True(selector.Matches(node));

        var host = new Panel();
        host.Children.Add(new Panel());
        host.Children.Add(node);
        host.Children.Add(new Panel());
        node.Children.Add(new Panel());
        Assert.False(selector.Matches(node));
        node.Set(pseudo, true);
        Assert.False(selector.Matches(node));
    }

    [Fact]
    public void BuiltInStructuralIdentifiersShareNamedInstances()
    {
        Assert.Same(UiPseudoClass.Get("FIRST-CHILD"), UiPseudoClass.FirstChild);
        Assert.Same(UiPseudoClass.Get("last-child"), UiPseudoClass.LastChild);
        Assert.Same(UiPseudoClass.Get("only-child"), UiPseudoClass.OnlyChild);
    }

    [Theory]
    [InlineData("3", "3")]
    [InlineData("+3", "3")]
    [InlineData("odd", "1,3,5")]
    [InlineData("even", "2,4,6")]
    [InlineData("2n+1", "1,3,5")]
    [InlineData("n", "1,2,3,4,5,6")]
    [InlineData("-n+3", "1,2,3")]
    [InlineData("n+3", "3,4,5,6")]
    [InlineData("0", "")]
    [InlineData("-2", "")]
    [InlineData("2n-1", "1,3,5")]
    [InlineData("-2n+5", "1,3,5")]
    [InlineData("0n+3", "3")]
    [InlineData("0n-3", "")]
    [InlineData("+n", "1,2,3,4,5,6")]
    [InlineData("-n", "")]
    public void PositionExpressionsMatchInBothDirections(string expression, string expected)
    {
        var host = new Panel();
        var nodes = Enumerable.Range(0, 6).Select(_ => new Panel()).ToArray();
        foreach (var node in nodes)
            host.Children.Add(node);

        foreach (var fromEnd in new[] { false, true })
        {
            var selector = Parse($":{(fromEnd ? "nth-last-child" : "nth-child")}({expression})");
            var ordered = fromEnd ? nodes.Reverse().ToArray() : nodes;
            var actual = ordered.Select((node, index) => (node, position: index + 1))
                .Where(item => selector.Matches(item.node)).Select(item => item.position);
            Assert.Equal(expected, string.Join(",", actual));
        }
    }

    [Theory]
    [InlineData(":nth-child(ODD)", ":nth-child(2n+1)")]
    [InlineData(":nth-last-child( 3n + 1 )", ":nth-last-child(3n+1)")]
    [InlineData(":nth-child(2n /*x*/ + 1)", ":nth-child(2n+1)")]
    [InlineData(":nth-child(n-/**/ 3)", ":nth-child(n-3)")]
    [InlineData(":nth-child(+/**/n)", ":nth-child(n)")]
    [InlineData(":nth-child(n/**/-1)", ":nth-child(n-1)")]
    [InlineData(":nth-child(2N\t+\n1)", ":nth-child(2n+1)")]
    [InlineData("*:nth-child(+3)", ":nth-child(3)")]
    [InlineData(":nth-child(1n+0)", ":nth-child(n)")]
    [InlineData(":nth-child(-2147483648n+2147483647)", ":nth-child(-2147483648n+2147483647)")]
    public void NormalizedPositionSelectorCanBeParsedAgain(string source, string expected)
    {
        var selector = Parse(source);
        Assert.Equal(expected, selector.SelectorText);
        Assert.Equal(expected, Parse(selector.SelectorText).SelectorText);
        Assert.Empty(selector.PseudoClasses);
        Assert.Equal(1, selector.ClassAndPseudoCount);
    }

    [Theory]
    [InlineData(":nth-child")]
    [InlineData(":nth-last-child")]
    [InlineData(":nth-child()")]
    [InlineData(":nth-last-child( )")]
    [InlineData(":nth-child (2)")]
    [InlineData(":nth-child(3 n)")]
    [InlineData(":nth-child(+ 2n)")]
    [InlineData(":nth-child(+ 2)")]
    [InlineData(":nth-child(3n + -6)")]
    [InlineData(":nth-child(3n--1)")]
    [InlineData(":nth-child(2/**/n)")]
    [InlineData(":nth-child(o/**/dd)")]
    [InlineData(":nth-child(-/**/n)")]
    [InlineData(":nth-child(+/**/2n)")]
    [InlineData(":nth-child(2147483648)")]
    [InlineData(":nth-child(-2147483649)")]
    [InlineData(":nth-child(2147483648n)")]
    [InlineData(":nth-child(n+2147483648)")]
    [InlineData(":nth-child(n-2147483649)")]
    [InlineData(":nth-child(2n of .row)")]
    [InlineData(":nth-last-child(2n of .row)")]
    [InlineData(":nth-child(2n, 3)")]
    [InlineData(":nth-child(1.5n)")]
    [InlineData(":nth-child(1e2)")]
    [InlineData(":nth-child((2n))")]
    [InlineData(":nth-child(odd extra)")]
    [InlineData(":nth-child(2n")]
    [InlineData(":first-child(1)")]
    [InlineData(":last-child()")]
    [InlineData(":only-child(1)")]
    [InlineData(":empty()")]
    [InlineData(":where(.row)")]
    [InlineData(":has(.row)")]
    public void InvalidPositionSyntaxReportsItsSource(string selector)
    {
        var error = Assert.Throws<UiStyleParseException>(() =>
            UiStyleSheet.Parse(selector + " { }", "position.css"));
        Assert.Equal(UiStyleParseError.InvalidSyntax, error.Error);
        Assert.Equal("position.css", error.SourceName);
        Assert.Equal(1, error.Line);
        Assert.True(error.Column > 0);
    }

    [Fact]
    public void InvalidArgumentRetainsItsLineAndColumn()
    {
        var error = Assert.Throws<UiStyleParseException>(() =>
            UiStyleSheet.Parse("Panel:nth-child(\n  2 n) { }", "position.css"));
        Assert.Equal(UiStyleParseError.InvalidSyntax, error.Error);
        Assert.Equal(2, error.Line);
        Assert.Equal(5, error.Column);
    }

    [Fact]
    public void UnterminatedArgumentCommentRetainsItsErrorCategory()
    {
        var error = Assert.Throws<UiStyleParseException>(() =>
            UiStyleSheet.Parse(":nth-child(2n /* comment", "position.css"));
        Assert.Equal(UiStyleParseError.UnterminatedComment, error.Error);
        Assert.Equal(15, error.Column);
    }

    [Theory]
    [InlineData("2147483647n-2147483646", true)]
    [InlineData("-2147483648n+1", true)]
    [InlineData("n-2147483648", true)]
    [InlineData("-n-2147483648", false)]
    [InlineData("2147483647n-2147483648", false)]
    [InlineData("2147483647", false)]
    [InlineData("-2147483648", false)]
    public void IntegerExtremesDoNotOverflowDuringMatching(string expression, bool matches)
    {
        Assert.Equal(matches, Parse($":nth-child({expression})").Matches(new Panel()));
    }

    [Fact]
    public void MultiplePositionConditionsMatchTheirIntersection()
    {
        var host = new Panel();
        var nodes = Enumerable.Range(0, 6).Select(_ => new Panel()).ToArray();
        foreach (var node in nodes)
            host.Children.Add(node);
        var selector = Parse(":nth-child(n+2):nth-child(-n+4)");
        Assert.Equal(new[] { false, true, true, true, false, false }, nodes.Select(selector.Matches));
        Assert.Equal(2, selector.ClassAndPseudoCount);
        var equivalent = Parse(":nth-child(odd):nth-child(2n+1):nth-last-child(odd)");
        Assert.Equal(2, equivalent.ClassAndPseudoCount);
        Assert.Equal(":nth-child(2n+1):nth-last-child(2n+1)", equivalent.SelectorText);
    }

    [Theory]
    [InlineData(".row:nth-child(2) > Text")]
    [InlineData(".row:nth-child(2) Text")]
    [InlineData(".row:first-child + .row:nth-child(even) > Text")]
    [InlineData(".row:first-child ~ .row:nth-last-child(1) > Text")]
    public void StructuralConditionsComposeWithRelationshipChains(string source)
    {
        var root = new Panel();
        var first = new StatePanel();
        var second = new StatePanel();
        first.Classes.Add("row");
        second.Classes.Add("row");
        var target = new Text();
        first.Children.Add(new Text());
        second.Children.Add(target);
        root.Children.Add(first);
        root.Children.Add(second);
        var selector = Parse(source);
        Assert.True(selector.Matches(target));
        Assert.False(selector.Matches(first.Children[0]));
        root.Children.Move(0, 1);
        Assert.False(selector.Matches(target));
    }

    [Fact]
    public void StructuralConditionsComposeWithNamedStateClassAndId()
    {
        var node = new StatePanel { StyleId = "target" };
        node.Classes.Add("row");
        var selector = Parse("Panel.row#target:loading:first-child:nth-last-child(1)");
        Assert.False(selector.Matches(node));
        node.Set(UiPseudoClass.Get("loading"), true);
        Assert.True(selector.Matches(node));
        Assert.True(Parse(":root:only-child").Matches(node));
        node.Set(UiPseudoClass.Hover, true);
        Assert.True(Parse(":hover:first-child").Matches(node));
    }

    [Fact]
    public void PositionSpecificityWinsAndRetainsItsFullSource()
    {
        var node = new Panel();
        node.Classes.Add("row");
        var screen = new UiScreen(node);
        screen.SetStyleSheets([UiStyleSheet.Parse("""
            .row:nth-child(odd) { opacity: 0.4; }
            .row { opacity: 0.8; }
            """)]);
        node.UpdateStyles();
        Assert.Equal(0.4, node.Opacity);
        var source = Assert.Single(node.GetStyleValueSources(UiNode.OpacityProperty));
        Assert.Equal(".row:nth-child(2n+1)", source.Selector.SelectorText);
    }

    private sealed class StatePanel : Panel
    {
        public void Set(UiPseudoClass pseudoClass, bool active) => SetPseudoClass(pseudoClass, active);
    }
}
