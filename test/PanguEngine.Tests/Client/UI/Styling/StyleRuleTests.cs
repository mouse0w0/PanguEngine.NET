using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Drawing;
using PanguEngine.Client.UI.Styling;

namespace PanguEngine.Tests.Client.UI.Styling;

public sealed class StyleRuleTests
{
    [Fact]
    public void SelectorCopiesClassesAndComputesSpecificity()
    {
        var classes = new[] { "primary", "wide", "primary" };
        var selector = StyleSelector.For<Button>(
            classes,
            "save",
            new[] { PseudoClass.Get("Hover"), PseudoClass.Focus, PseudoClass.Get("hover") });

        Assert.Equal(new[] { "primary", "wide" }, selector.Classes);
        Assert.Equal("save", selector.Id);
        Assert.Equal(new[] { "focus", "hover" }, selector.PseudoClasses.Select(pseudoClass => pseudoClass.Name));
        Assert.Same(PseudoClass.Focus, selector.PseudoClasses[0]);
        Assert.Same(PseudoClass.Hover, selector.PseudoClasses[1]);
        Assert.Equal((1, 4), (selector.IdCount, selector.ClassAndPseudoCount));
    }

    [Fact]
    public void SelectorCopiesPseudoClassesAndDoesNotExposeCallerCollection()
    {
        var pseudoClasses = new List<PseudoClass> { PseudoClass.Get("Hover") };
        var selector = StyleSelector.For<Button>(pseudoClasses: pseudoClasses);

        pseudoClasses.Add(PseudoClass.Focus);

        Assert.Same(PseudoClass.Hover, Assert.Single(selector.PseudoClasses));
        Assert.Throws<NotSupportedException>(() =>
            ((IList<PseudoClass>)selector.PseudoClasses).Add(PseudoClass.Get("loading")));
    }

    [Fact]
    public void StatefulRuleAcceptsLayoutSetter()
    {
        var selector = StyleSelector.For<Button>(pseudoClasses: [PseudoClass.Hover]);
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
        var selector = StyleSelector.For<Node>();
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
    public void SelectorRejectsInvalidIdentifier()
    {
        Assert.Throws<ArgumentException>(() => StyleSelector.For<Button>(classes: ["1bad"]));
        Assert.Throws<ArgumentException>(() => StyleSelector.For<Button>(id: "bad id"));
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
    public void ForSelectorKeepsClrTargetTypeAndExposesTypeName()
    {
        var selector = StyleSelector.For<Button>(classes: ["primary"]);

        Assert.Equal(typeof(Button), selector.TargetType);
        Assert.Equal("Button", selector.TypeName);
    }

    [Fact]
    public void ParsedSelectorExposesTypeNameWithoutTargetType()
    {
        var rule = Assert.Single(StyleSheet.Parse("Button.primary { }").Rules);

        Assert.Single(rule.Selectors);
        Assert.Null(rule.Selectors[0].TargetType);
        Assert.Equal("Button", rule.Selectors[0].TypeName);
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
        var selectors = new[] { StyleSelector.For<Button>(), StyleSelector.For<Node>() };
        var setter = StyleSetter.Create(Region.BackgroundProperty, new SolidColorBrush(1, 2, 3));

        Assert.Throws<ArgumentException>(() => new StyleRule(selectors, [setter]));
    }

    [Fact]
    public void MultiSelectorRuleAcceptsLayoutSetterWhenAnyBranchHasPseudoClass()
    {
        var selectors = new[]
        {
            StyleSelector.For<Button>(),
            StyleSelector.For<Button>(pseudoClasses: [PseudoClass.Hover]),
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
    public void TargetTypeDepthStartsAtOneForUiNodeAndGrowsWithInheritance()
    {
        Assert.Equal(1, StyleSelector.For<Node>().TargetTypeDepth);
        Assert.True(StyleSelector.For<Control>().TargetTypeDepth > StyleSelector.For<Node>().TargetTypeDepth);
        Assert.True(StyleSelector.For<Button>().TargetTypeDepth > StyleSelector.For<Control>().TargetTypeDepth);
    }

    [Theory]
    [InlineData("*")]
    [InlineData(".danger")]
    [InlineData("#save")]
    [InlineData(":disabled")]
    [InlineData("Button")]
    public void ParsedSelectorHasNoClrTargetAndZeroDepth(string text)
    {
        var selector = Assert.Single(StyleSheet.Parse(text + " { }").Rules).Selectors[0];

        Assert.Null(selector.TargetType);
        Assert.Equal(0, selector.TargetTypeDepth);
    }
}
