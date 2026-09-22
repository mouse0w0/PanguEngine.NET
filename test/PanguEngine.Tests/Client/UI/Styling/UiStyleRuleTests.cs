using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Drawing;
using PanguEngine.Client.UI.Styling;

namespace PanguEngine.Tests.Client.UI.Styling;

public sealed class UiStyleRuleTests
{
    [Fact]
    public void SelectorCopiesClassesAndComputesSpecificity()
    {
        var classes = new[] { "primary", "wide", "primary" };
        var selector = UiStyleSelector.For<Button>(
            classes,
            "save",
            new[] { UiPseudoClass.Get("Hover"), UiPseudoClass.Focus, UiPseudoClass.Get("hover") });

        Assert.Equal(new[] { "primary", "wide" }, selector.Classes);
        Assert.Equal("save", selector.Id);
        Assert.Equal(new[] { "focus", "hover" }, selector.PseudoClasses.Select(pseudoClass => pseudoClass.Name));
        Assert.Same(UiPseudoClass.Focus, selector.PseudoClasses[0]);
        Assert.Same(UiPseudoClass.Hover, selector.PseudoClasses[1]);
        Assert.Equal((1, 4), (selector.IdCount, selector.ClassAndPseudoCount));
    }

    [Fact]
    public void SelectorCopiesPseudoClassesAndDoesNotExposeCallerCollection()
    {
        var pseudoClasses = new List<UiPseudoClass> { UiPseudoClass.Get("Hover") };
        var selector = UiStyleSelector.For<Button>(pseudoClasses: pseudoClasses);

        pseudoClasses.Add(UiPseudoClass.Focus);

        Assert.Same(UiPseudoClass.Hover, Assert.Single(selector.PseudoClasses));
        Assert.Throws<NotSupportedException>(() =>
            ((IList<UiPseudoClass>)selector.PseudoClasses).Add(UiPseudoClass.Get("loading")));
    }

    [Fact]
    public void StatefulRuleRejectsLayoutSetter()
    {
        var selector = UiStyleSelector.For<Button>(pseudoClasses: [UiPseudoClass.Hover]);
        var setter = UiStyleSetter.Create(Region.PaddingProperty, new Thickness(8));

        Assert.Throws<ArgumentException>(() => new UiStyleRule(selector, [setter]));
    }

    [Fact]
    public void RuleRejectsReadOnlyProperty()
    {
        var selector = UiStyleSelector.For<Control>();
        var setter = UiStyleSetter.Create(Control.IsPressedProperty, true);

        Assert.Throws<ArgumentException>(() => new UiStyleRule(selector, [setter]));
    }

    [Fact]
    public void RuleRejectsIncompatibleTargetType()
    {
        var selector = UiStyleSelector.For<UiNode>();
        var setter = UiStyleSetter.Create(Region.BackgroundProperty, new SolidColorBrush(1, 2, 3));

        Assert.Throws<ArgumentException>(() => new UiStyleRule(selector, [setter]));
    }

    [Fact]
    public void RuleKeepsLastDeclarationForDuplicateProperty()
    {
        var selector = UiStyleSelector.For<Button>();
        var first = UiStyleSetter.Create(Region.BackgroundProperty, new SolidColorBrush(1, 1, 1));
        var second = UiStyleSetter.Create(Region.BackgroundProperty, new SolidColorBrush(2, 2, 2));

        var rule = new UiStyleRule(selector, [first, second]);

        Assert.Single(rule.Setters);
        Assert.Equal(new SolidColorBrush(2, 2, 2), (SolidColorBrush)rule.Setters[0].BoxedValue!);
    }

    [Fact]
    public void RuleAppendsDuplicateSetterToEndKeepingLastValue()
    {
        var selector = UiStyleSelector.For<Button>();
        var first = UiStyleSetter.Create(Region.BackgroundProperty, new SolidColorBrush(1, 1, 1));
        var second = UiStyleSetter.Create(Region.PaddingProperty, new Thickness(3));
        var third = UiStyleSetter.Create(Region.BackgroundProperty, new SolidColorBrush(3, 3, 3));

        var rule = new UiStyleRule(selector, [first, second, third]);

        Assert.Equal(2, rule.Setters.Count);
        Assert.Same(Region.PaddingProperty, rule.Setters[0].Property);
        Assert.Same(Region.BackgroundProperty, rule.Setters[1].Property);
        Assert.Equal(new SolidColorBrush(3, 3, 3), (SolidColorBrush)rule.Setters[1].BoxedValue!);
    }

    [Fact]
    public void SelectorRejectsInvalidIdentifier()
    {
        Assert.Throws<ArgumentException>(() => UiStyleSelector.For<Button>(classes: ["1bad"]));
        Assert.Throws<ArgumentException>(() => UiStyleSelector.For<Button>(id: "bad id"));
    }

    [Fact]
    public void SheetCopiesRulesAndDoesNotExposeCallerCollection()
    {
        var rule = new UiStyleRule(
            UiStyleSelector.For<Button>(),
            [UiStyleSetter.Create(Region.BackgroundProperty, new SolidColorBrush(1, 1, 1))]);
        var list = new List<UiStyleRule> { rule };
        var sheet = new UiStyleSheet(list);

        list.Clear();

        Assert.Single(sheet.Rules);
        Assert.Same(rule, sheet.Rules[0]);
    }

    [Fact]
    public void ForSelectorKeepsClrTargetTypeAndExposesTypeName()
    {
        var selector = UiStyleSelector.For<Button>(classes: ["primary"]);

        Assert.Equal(typeof(Button), selector.TargetType);
        Assert.Equal("Button", selector.TypeName);
    }

    [Fact]
    public void ParsedSelectorExposesTypeNameWithoutTargetType()
    {
        var rule = Assert.Single(UiStyleSheet.Parse("Button.primary { }").Rules);

        Assert.Null(rule.Selector.TargetType);
        Assert.Equal("Button", rule.Selector.TypeName);
        Assert.Equal("Button.primary", rule.Selector.SelectorText);
        Assert.Empty(rule.Setters);
    }

    [Fact]
    public void ParsedRuleBindsStronglyTypedSetters()
    {
        var rule = Assert.Single(UiStyleSheet.Parse("Button { padding: 4px; }").Rules);

        var declaration = Assert.Single(rule.Bind(typeof(Button)));

        Assert.Same(Region.PaddingProperty, declaration.Setter.Property);
        Assert.Null(declaration.Setter.Component);
        Assert.Equal(new Thickness(4), (Thickness)declaration.Setter.BoxedValue!);
        Assert.Equal("padding", declaration.CssPropertyName);
        Assert.Equal(0, declaration.DeclarationIndex);
    }

    [Fact]
    public void ParsedDeclarationsKeepOriginalOrderAndLonghandProducesOneEdgeSetter()
    {
        var rule = Assert.Single(UiStyleSheet.Parse("Region { padding: 1px 2px; padding-left: 9px; }").Rules);

        var bound = rule.Bind(typeof(Region));

        Assert.Equal(2, bound.Count);
        Assert.Equal([0, 1], bound.Select(declaration => declaration.DeclarationIndex));
        Assert.Equal(["padding", "padding-left"], bound.Select(declaration => declaration.CssPropertyName));
        Assert.Null(bound[0].Setter.Component);
        Assert.Equal<UiStyleEdge?>(UiStyleEdge.Left, bound[1].Setter.Component);
        Assert.Equal(new Thickness(2, 1, 2, 1), (Thickness)bound[0].Setter.BoxedValue!);
        Assert.NotNull(bound[0].SourceLocation);
    }

    [Fact]
    public void TargetTypeDepthStartsAtOneForUiNodeAndGrowsWithInheritance()
    {
        Assert.Equal(1, UiStyleSelector.For<UiNode>().TargetTypeDepth);
        Assert.True(UiStyleSelector.For<Control>().TargetTypeDepth > UiStyleSelector.For<UiNode>().TargetTypeDepth);
        Assert.True(UiStyleSelector.For<Button>().TargetTypeDepth > UiStyleSelector.For<Control>().TargetTypeDepth);
    }

    [Theory]
    [InlineData("*")]
    [InlineData(".danger")]
    [InlineData("#save")]
    [InlineData(":disabled")]
    [InlineData("Button")]
    public void ParsedSelectorHasNoClrTargetAndZeroDepth(string text)
    {
        var selector = Assert.Single(UiStyleSheet.Parse(text + " { }").Rules).Selector;

        Assert.Null(selector.TargetType);
        Assert.Equal(0, selector.TargetTypeDepth);
    }
}
