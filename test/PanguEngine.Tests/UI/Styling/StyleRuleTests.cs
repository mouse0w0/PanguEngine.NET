using PanguEngine.UI;
using PanguEngine.UI.Controls;
using PanguEngine.UI.Drawing;
using PanguEngine.UI.Styling;

namespace PanguEngine.Tests.UI.Styling;

public sealed class StyleRuleTests
{
    [Fact]
    public void SelectorCopiesConditionsAndComputesSpecificity()
    {
        var conditions = new[]
        {
            StyleSelectorCondition.Class("wide"), StyleSelectorCondition.Class("primary"), StyleSelectorCondition.Class("wide"),
            StyleSelectorCondition.Id("save"), StyleSelectorCondition.Nth(false, 0, 1)
        };
        var selector = StyleSelector.For<Panel>(conditions);
        conditions[3] = StyleSelectorCondition.Id("other");

        var node = new Panel { StyleId = "save" };
        node.Classes.Add("primary");
        node.Classes.Add("wide");
        Assert.Equal("Panel.wide.primary#save:nth-child(1)", selector.SelectorText);
        Assert.Equal((1, 3), (selector.IdCount, selector.ClassAndPseudoCount));
        Assert.True(selector.Matches(node));
        node.Classes.Remove("wide");
        Assert.False(selector.Matches(node));
    }

    [Fact]
    public void EmptyConditionsDoNotChangeSelectorBehavior()
    {
        var empty = default(StyleSelectorCondition);
        var node = new Panel();
        Assert.True(empty.Matches(node));
        Assert.Equal((string.Empty, 0, 0, StyleSelectorDependencies.None),
            (empty.Text, empty.IdCount, empty.ClassAndPseudoCount, empty.Dependencies));

        var wildcard = StyleSelectorSegment.Create(null, "*", [empty, empty]);
        Assert.Equal("*", wildcard.Text);
        Assert.True(wildcard.TryMatch(node, out _));

        var selector = StyleSelector.For<Panel>([empty, StyleSelectorCondition.Class("target"), empty]);
        Assert.Equal("Panel.target", selector.SelectorText);
        Assert.Equal((0, 1), (selector.IdCount, selector.ClassAndPseudoCount));
        Assert.False(selector.Matches(node));
        node.Classes.Add("target");
        Assert.True(selector.Matches(node));
    }

    [Fact]
    public void IdConditionsDeduplicateButDifferentIdsMustAllMatch()
    {
        var repeated = StyleSelector.For<Panel>([
            StyleSelectorCondition.Id("a"), StyleSelectorCondition.Id("a"), StyleSelectorCondition.Class("a")
        ]);
        Assert.Equal((1, 1), (repeated.IdCount, repeated.ClassAndPseudoCount));
        var node = new Panel { StyleId = "a" };
        Assert.False(repeated.Matches(node));
        node.Classes.Add("a");
        Assert.True(repeated.Matches(node));

        var conflicting = StyleSelector.For<Panel>([StyleSelectorCondition.Id("a"), StyleSelectorCondition.Id("b")]);
        Assert.Equal(2, conflicting.IdCount);
        Assert.False(conflicting.Matches(new Panel { StyleId = "a" }));
        Assert.False(conflicting.Matches(new Panel { StyleId = "b" }));
    }

    [Fact]
    public void StatefulRuleAcceptsLayoutSetter()
    {
        var selector = StyleSelector.For<Button>([StyleSelectorCondition.Pseudo(PseudoClass.Hover)]);
        var setter = StyleSetter.Create(Region.PaddingProperty, new Thickness(8));

        Assert.Same(setter, Assert.Single(new StyleRule(selector, [setter]).Setters));
    }

    [Fact]
    public void RuleRejectsReadOnlyProperty()
    {
        var selector = StyleSelector.For<Control>();
        var setter = StyleSetter.Create(Control.IsPressedProperty, true);

        Assert.Throws<ArgumentException>(() => new StyleRule(selector, [setter]));
    }

    [Fact]
    public void RuleRejectsIncompatibleTargetType()
    {
        var selector = StyleSelector.For<UIElement>();
        var setter = StyleSetter.Create(Region.BackgroundProperty, new SolidColorBrush(1, 2, 3));

        Assert.Throws<ArgumentException>(() => new StyleRule(selector, [setter]));
    }

    [Fact]
    public void RuleKeepsLastDeclarationForDuplicateProperty()
    {
        var selector = StyleSelector.For<Button>();
        var first = StyleSetter.Create(Region.BackgroundProperty, new SolidColorBrush(1, 1, 1));
        var second = StyleSetter.Create(Region.BackgroundProperty, new SolidColorBrush(2, 2, 2));

        var rule = new StyleRule(selector, [first, second]);

        Assert.Single(rule.Setters);
        Assert.Equal(new SolidColorBrush(2, 2, 2), (SolidColorBrush)rule.Setters[0].BoxedValue!);
    }

    [Fact]
    public void RuleAppendsDuplicateSetterToEndKeepingLastValue()
    {
        var selector = StyleSelector.For<Button>();
        var first = StyleSetter.Create(Region.BackgroundProperty, new SolidColorBrush(1, 1, 1));
        var second = StyleSetter.Create(Region.PaddingProperty, new Thickness(3));
        var third = StyleSetter.Create(Region.BackgroundProperty, new SolidColorBrush(3, 3, 3));

        var rule = new StyleRule(selector, [first, second, third]);

        Assert.Equal(2, rule.Setters.Count);
        Assert.Same(Region.PaddingProperty, rule.Setters[0].Property);
        Assert.Same(Region.BackgroundProperty, rule.Setters[1].Property);
        Assert.Equal(new SolidColorBrush(3, 3, 3), (SolidColorBrush)rule.Setters[1].BoxedValue!);
    }

    [Fact]
    public void ConditionsRejectInvalidIdentifiers()
    {
        Assert.Throws<ArgumentException>(() => StyleSelectorCondition.Class("1bad"));
        Assert.Throws<ArgumentException>(() => StyleSelectorCondition.Id("bad id"));
    }

    [Fact]
    public void SheetCopiesRulesAndDoesNotExposeCallerCollection()
    {
        var rule = new StyleRule(
            StyleSelector.For<Button>(),
            [StyleSetter.Create(Region.BackgroundProperty, new SolidColorBrush(1, 1, 1))]);
        var list = new List<StyleRule> { rule };
        var sheet = new StyleSheet(list);

        list.Clear();

        Assert.Single(sheet.Rules);
        Assert.Same(rule, sheet.Rules[0]);
    }

    [Fact]
    public void ParsedRuleKeepsItsSelectorTextAndDefersSetters()
    {
        var rule = Assert.Single(StyleSheet.Parse("Button.primary { }").Rules);

        Assert.Single(rule.Selectors);
        Assert.Equal("Button.primary", rule.Selectors[0].SelectorText);
        Assert.Empty(rule.Setters);
    }

    [Fact]
    public void RuleCopiesSelectorsAndKeepsDuplicateBranches()
    {
        var selectors = new List<StyleSelector>
        {
            StyleSelector.For<Button>(),
            StyleSelector.For<Panel>(),
            StyleSelector.For<Button>(),
        };
        var rule = new StyleRule(selectors, []);

        selectors.Clear();

        Assert.Equal(3, rule.Selectors.Count);
        Assert.Equal(typeof(Button), rule.Selectors[0].TargetType);
        Assert.Equal(typeof(Panel), rule.Selectors[1].TargetType);
        Assert.Equal(typeof(Button), rule.Selectors[2].TargetType);
        Assert.Throws<NotSupportedException>(() => ((IList<StyleSelector>)rule.Selectors).Clear());
    }

    [Fact]
    public void RuleRejectsEmptyOrNullSelectorList()
    {
        Assert.Throws<ArgumentException>(() => new StyleRule([], []));
        Assert.Throws<ArgumentException>(() => new StyleRule(new StyleSelector[] { null! }, []));
    }

    [Fact]
    public void MultiSelectorRuleValidatesSetterAgainstEveryClrTarget()
    {
        var selectors = new[] { StyleSelector.For<Button>(), StyleSelector.For<UIElement>() };
        var setter = StyleSetter.Create(Region.BackgroundProperty, new SolidColorBrush(1, 2, 3));

        Assert.Throws<ArgumentException>(() => new StyleRule(selectors, [setter]));
    }

    [Fact]
    public void MultiSelectorRuleAcceptsLayoutSetterWhenAnyBranchHasPseudoClass()
    {
        var selectors = new[]
        {
            StyleSelector.For<Button>(),
            StyleSelector.For<Button>([StyleSelectorCondition.Pseudo(PseudoClass.Hover)]),
        };
        var setter = StyleSetter.Create(Region.PaddingProperty, new Thickness(8));

        Assert.Same(setter, Assert.Single(new StyleRule(selectors, [setter]).Setters));
    }

    [Fact]
    public void ParsedRuleBindsStronglyTypedSetters()
    {
        var rule = Assert.Single(StyleSheet.Parse("Button { padding: 4px; }").Rules);

        var declaration = Assert.Single(rule.Bind(new Button().GetType()));

        Assert.Same(Region.PaddingProperty, declaration.Setter.Property);
        Assert.Null(declaration.Setter.Component);
        Assert.Equal(new Thickness(4), (Thickness)declaration.Setter.BoxedValue!);
        Assert.Equal("padding", declaration.CssPropertyName);
        Assert.Equal(0, declaration.DeclarationIndex);
    }

    [Fact]
    public void ParsedDeclarationsKeepOriginalOrderAndLonghandProducesOneEdgeSetter()
    {
        var rule = Assert.Single(StyleSheet.Parse("Region { padding: 1px 2px; padding-left: 9px; }").Rules);

        var bound = rule.Bind(new Panel().GetType());

        Assert.Equal(2, bound.Count);
        Assert.Equal([0, 1], bound.Select(declaration => declaration.DeclarationIndex));
        Assert.Equal(["padding", "padding-left"], bound.Select(declaration => declaration.CssPropertyName));
        Assert.Null(bound[0].Setter.Component);
        Assert.Equal<StyleEdge?>(StyleEdge.Left, bound[1].Setter.Component);
        Assert.Equal(new Thickness(2, 1, 2, 1), (Thickness)bound[0].Setter.BoxedValue!);
        Assert.NotNull(bound[0].SourceLocation);
    }

    [Fact]
    public void MatchingTypeDepthStartsAtOneForUIElementAndGrowsWithInheritance()
    {
        Assert.True(StyleSelector.For<UIElement>().TryMatch(new Panel(), out var elementDepth));
        Assert.Equal(1, elementDepth);
        Assert.True(StyleSelector.For<Control>().TryMatch(new Button(), out var controlDepth));
        Assert.True(StyleSelector.For<Button>().TryMatch(new Button(), out var buttonDepth));
        Assert.True(buttonDepth > controlDepth);
        Assert.True(controlDepth > elementDepth);
    }

    [Theory]
    [InlineData("*")]
    [InlineData(".danger")]
    [InlineData("#save")]
    [InlineData(":disabled")]
    [InlineData("Button")]
    public void ParsedSelectorHasNoClrTarget(string text)
    {
        var selector = Assert.Single(StyleSheet.Parse(text + " { }").Rules).Selectors[0];

        Assert.Null(selector.TargetType);
    }
}
