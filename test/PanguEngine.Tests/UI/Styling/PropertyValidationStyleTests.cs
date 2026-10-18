using PanguEngine.ComponentModel;
using PanguEngine.UI;
using PanguEngine.UI.Controls;
using PanguEngine.UI.Styling;

namespace PanguEngine.Tests.UI.Styling;

public sealed class PropertyValidationStyleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InvalidWinnerPreservesSnapshotIncludingWhenMasked(bool masked)
    {
        var node = new StyleNode();
        if (masked)
            node.Value = 40;
        var screen = new Screen(node);
        var original = CreateSheet(20);
        screen.SetStyleSheets([original]);
        node.UpdateStyles();
        var notifications = 0;
        using var subscription = node.Subscribe(StyleNode.ValueProperty, (_, _) => notifications++);

        var invalid = CreateSheet(-1);
        screen.SetStyleSheets([invalid]);
        var error = Assert.Throws<ArgumentException>(node.UpdateStyles);

        Assert.StartsWith("Style value must be non-negative.", error.Message);
        Assert.Equal("value", error.ParamName);
        Assert.Same(invalid, Assert.Single(screen.StyleSheets));
        Assert.Equal(masked ? 40 : 20, node.Value);
        Assert.Equal(0, notifications);
        node.ClearValue(StyleNode.ValueProperty);
        Assert.Equal(20, node.Value);
        screen.SetStyleSheets([CreateSheet(30)]);
        node.UpdateStyles();
        Assert.Equal(30, node.Value);
    }

    [Fact]
    public void RejectedNodePreservesItsSnapshotAndEarlierNodesRemainCommitted()
    {
        var first = new StyleNode();
        var second = new StyleNode();
        second.Classes.Add("invalid");
        var panel = new Panel();
        panel.Children.Add(first);
        panel.Children.Add(second);
        var screen = new Screen(panel);
        screen.SetStyleSheets([CreateSheet(20)]);
        panel.UpdateStyles();
        var notifications = 0;
        using var subscription = first.Subscribe(StyleNode.ValueProperty, (_, _) => notifications++);
        var candidate = new StyleSheet([
            new StyleRule(StyleSelector.For<StyleNode>(), [StyleSetter.Create(StyleNode.ValueProperty, 30)]),
            new StyleRule(StyleSelector.For<StyleNode>(classes: ["invalid"]),
                [StyleSetter.Create(StyleNode.ValueProperty, -1)])
        ]);

        screen.SetStyleSheets([candidate]);
        Assert.Throws<ArgumentException>(panel.UpdateStyles);

        Assert.Equal(30, first.Value);
        Assert.Equal(20, second.Value);
        Assert.Equal(1, notifications);
        screen.SetStyleSheets([CreateSheet(40)]);
        panel.UpdateStyles();
        Assert.Equal(40, first.Value);
        Assert.Equal(40, second.Value);
    }

    [Fact]
    public void ReadingDoesNotResolvePendingInvalidStyles()
    {
        var node = new StyleNode();
        var screen = new Screen(node);
        screen.SetStyleSheets([CreateSheet(-1)]);
        Assert.Equal(0, node.Value);

        Assert.Throws<ArgumentException>(node.UpdateStyles);
        Assert.Equal(0, node.Value);
        Assert.Empty(node.GetStyleValueSources(StyleNode.ValueProperty));
        screen.SetStyleSheets([CreateSheet(20)]);
        Assert.Equal(0, node.Value);
        node.UpdateStyles();
        Assert.Equal(20, node.Value);
    }

    [Fact]
    public void NullableStyleValuesArePassedToTheValidator()
    {
        var node = new StyleNode();
        var screen = new Screen(node);
        screen.SetStyleSheets([
            new StyleSheet([
                new StyleRule(StyleSelector.For<StyleNode>(),
                    [StyleSetter.Create<string?>(StyleNode.OptionalProperty, "invalid")])
            ])
        ]);
        Assert.Throws<ArgumentException>(node.UpdateStyles);
        Assert.Equal("initial", node.GetValue(StyleNode.OptionalProperty));

        screen.SetStyleSheets([
            new StyleSheet([
                new StyleRule(StyleSelector.For<StyleNode>(),
                    [StyleSetter.Create<string?>(StyleNode.OptionalProperty, null)])
            ])
        ]);
        node.UpdateStyles();

        Assert.Null(node.GetValue(StyleNode.OptionalProperty));
    }

    [Fact]
    public void NullableValueTypeStylesValidateBothBoxingForms()
    {
        var node = new StyleNode();
        var screen = new Screen(node);
        screen.SetStyleSheets([CreateNullableSheet(null)]);
        node.UpdateStyles();
        Assert.Null(node.GetValue(StyleNode.NullableValueProperty));

        screen.SetStyleSheets([CreateNullableSheet(7)]);
        node.UpdateStyles();
        Assert.Equal(7, node.GetValue(StyleNode.NullableValueProperty));

        screen.SetStyleSheets([CreateNullableSheet(-1)]);
        Assert.Throws<ArgumentException>(node.UpdateStyles);
        Assert.Equal(7, node.GetValue(StyleNode.NullableValueProperty));
    }

    [Fact]
    public void ComponentWinnersAreCombinedBeforeValidation()
    {
        var node = new StyleNode();
        var screen = new Screen(node);
        screen.SetStyleSheets([CreateSpacingSheet(4, 4)]);
        node.UpdateStyles();
        Assert.Equal(new Thickness(4, 0, 4, 0), node.GetValue(StyleNode.SpacingProperty));
        var notifications = 0;
        using var subscription = node.Subscribe(StyleNode.SpacingProperty, (_, _) => notifications++);

        screen.SetStyleSheets([CreateSpacingSheet(4, 5)]);
        Assert.Throws<ArgumentException>(node.UpdateStyles);

        Assert.Equal(new Thickness(4, 0, 4, 0), node.GetValue(StyleNode.SpacingProperty));
        Assert.Equal(0, notifications);
    }

    [Theory]
    [InlineData("theme.css", false)]
    [InlineData("theme.css", true)]
    [InlineData(null, false)]
    public void CssRejectionReportsTheWinningDeclarationAndPreservesSnapshot(string? sourceName, bool masked)
    {
        var node = new StyleNode();
        if (masked)
            node.Value = 40;
        var screen = new Screen(node);
        screen.SetStyleSheets([CreateSheet(20)]);
        node.UpdateStyles();
        var notifications = 0;
        using var subscription = node.Subscribe(StyleNode.ValueProperty, (_, _) => notifications++);
        screen.SetStyleSheets([
            StyleSheet.Parse(
                "PropertyValidationStyleNode {\n    validated-value: -1;\n}", sourceName)
        ]);

        var error = Assert.Throws<StyleParseException>(node.UpdateStyles);

        Assert.Equal(StyleParseError.InvalidValue, error.Error);
        Assert.Equal(sourceName, error.SourceName);
        Assert.Equal(2, error.Line);
        Assert.Equal(5, error.Column);
        Assert.Equal("validated-value".Length, error.Length);
        var rejection = Assert.IsType<ArgumentException>(error.InnerException);
        Assert.Equal("value", rejection.ParamName);
        Assert.Equal(
            $"Failed to apply style property 'Value': InvalidValue at {sourceName}(2:5).{Environment.NewLine}" +
            $"Reason: {rejection.Message}{Environment.NewLine}" +
            $"Winning declarations:{Environment.NewLine}" +
            $"  validated-value at {sourceName}(2:5)", error.Message);
        Assert.Equal(masked ? 40 : 20, node.Value);
        Assert.Equal(0, notifications);
        node.ClearValue(StyleNode.ValueProperty);
        Assert.Equal(20, node.Value);
        screen.SetStyleSheets([CreateSheet(30)]);
        node.UpdateStyles();
        Assert.Equal(30, node.Value);
    }

    [Fact]
    public void InvalidCssLoserIsNotValidated()
    {
        var node = new StyleNode();
        var screen = new Screen(node);
        screen.SetStyleSheets([
            StyleSheet.Parse("PropertyValidationStyleNode { validated-value: -1; }", "loser.css"),
            StyleSheet.Parse("PropertyValidationStyleNode { validated-value: 20; }", "winner.css")
        ]);

        node.UpdateStyles();

        Assert.Equal(20, node.Value);
        Assert.Equal("winner.css", Assert.Single(node.GetStyleValueSources(StyleNode.ValueProperty)).SheetSourceName);
    }

    [Fact]
    public void RejectedVariableValueReportsTheConsumingDeclaration()
    {
        var node = new StyleNode();
        var screen = new Screen(node);
        screen.SetStyleSheets([
            StyleSheet.Parse(
                "PropertyValidationStyleNode {\n    --candidate: -1;\n    validated-value: var(--candidate);\n}",
                "variables.css")
        ]);

        var error = Assert.Throws<StyleParseException>(node.UpdateStyles);

        Assert.Equal("variables.css", error.SourceName);
        Assert.Equal(3, error.Line);
        Assert.Equal(5, error.Column);
        Assert.Contains("validated-value", error.Message);
        Assert.Equal(0, node.Value);
    }

    [Fact]
    public void RejectedCombinedValueReportsAllWinningDeclarations()
    {
        var node = new StyleNode();
        var screen = new Screen(node);
        screen.SetStyleSheets([CreateSpacingSheet(4, 4)]);
        node.UpdateStyles();
        screen.SetStyleSheets([
            StyleSheet.Parse("PropertyValidationStyleNode {\n    validated-spacing-left: 4;\n}", "left.css"),
            StyleSheet.Parse("PropertyValidationStyleNode {\n    validated-spacing-right: 5;\n}", "right.css")
        ]);

        var error = Assert.Throws<StyleParseException>(node.UpdateStyles);

        Assert.Equal("right.css", error.SourceName);
        Assert.Equal(2, error.Line);
        Assert.Equal(5, error.Column);
        Assert.Equal("validated-spacing-right".Length, error.Length);
        Assert.Contains($"{Environment.NewLine}  validated-spacing-left at left.css(2:5)", error.Message);
        Assert.Contains($"{Environment.NewLine}  validated-spacing-right at right.css(2:5)", error.Message);
        Assert.Equal(new Thickness(4, 0, 4, 0), node.GetValue(StyleNode.SpacingProperty));
    }

    [Theory]
    [InlineData(13)]
    [InlineData(14)]
    public void CssValidatorExceptionsPropagateWithoutWrappingAndUpdatesRecover(int value)
    {
        var node = new StyleNode();
        var screen = new Screen(node);
        screen.SetStyleSheets([CreateSheet(20)]);
        node.UpdateStyles();
        screen.SetStyleSheets([
            StyleSheet.Parse(
                $"PropertyValidationStyleNode {{ validated-value: {value}; }}", "validator.css")
        ]);

        if (value == 13)
            Assert.Equal("validator", Assert.Throws<InvalidOperationException>(node.UpdateStyles).Message);
        else
            Assert.Same(StyleNode.ValidatorError, Assert.Throws<ArgumentException>(node.UpdateStyles));
        Assert.Equal(20, node.Value);
        screen.SetStyleSheets([CreateSheet(30)]);
        node.UpdateStyles();
        Assert.Equal(30, node.Value);
    }

    [Fact]
    public void RejectedMixedValueReportsItsCssAndCSharpSources()
    {
        var node = new StyleNode();
        var screen = new Screen(node);
        screen.SetStyleSheets([
            new StyleSheet([
                new StyleRule(StyleSelector.For<StyleNode>(), [
                    StyleSetter.CreateEdge(StyleNode.SpacingProperty, StyleEdge.Left, 4)
                ])
            ]),
            StyleSheet.Parse("PropertyValidationStyleNode { validated-spacing-right: 5; }", "mixed.css")
        ]);

        var error = Assert.Throws<StyleParseException>(node.UpdateStyles);

        Assert.Equal("mixed.css", error.SourceName);
        Assert.Contains("Spacing (Left) from C# selector 'StyleNode'", error.Message);
        Assert.Contains("validated-spacing-right at mixed.css", error.Message);
        Assert.Equal(default(Thickness), node.GetValue(StyleNode.SpacingProperty));
    }

    [Fact]
    public void CSharpRuleLocationDoesNotChangeTheRejectionException()
    {
        var node = new StyleNode();
        var screen = new Screen(node);
        screen.SetStyleSheets([
            new StyleSheet([
                new StyleRule(StyleSelector.For<StyleNode>(), [StyleSetter.Create(StyleNode.ValueProperty, -1)],
                    new StyleSourceLocation("manual", 10, 1, 5))
            ], "manual")
        ]);

        var error = Assert.Throws<ArgumentException>(node.UpdateStyles);

        Assert.Equal("value", error.ParamName);
        Assert.StartsWith("Style value must be non-negative.", error.Message);
    }

    private static StyleSheet CreateNullableSheet(int? value) => new([
        new StyleRule(StyleSelector.For<StyleNode>(), [StyleSetter.Create(StyleNode.NullableValueProperty, value)])
    ]);

    private static StyleSheet CreateSpacingSheet(double left, double right) => new([
        new StyleRule(StyleSelector.For<StyleNode>(), [
            StyleSetter.CreateEdge(StyleNode.SpacingProperty, StyleEdge.Left, left),
            StyleSetter.CreateEdge(StyleNode.SpacingProperty, StyleEdge.Right, right)
        ])
    ]);

    private static StyleSheet CreateSheet(int value) => new([
        new StyleRule(StyleSelector.For<StyleNode>(), [StyleSetter.Create(StyleNode.ValueProperty, value)])
    ]);

    private sealed class StyleNode : UIElement
    {
        internal static readonly ArgumentException ValidatorError = new("validator", "candidate");

        internal static readonly Property<int> ValueProperty = Property.Register<StyleNode, int>(
            "Value", validate: static value => value switch
            {
                13 => throw new InvalidOperationException("validator"),
                14 => throw ValidatorError,
                _ => value >= 0
            },
            validationMessage: "Style value must be non-negative.");

        internal static readonly Property<string?> OptionalProperty = Property.Register<StyleNode, string?>(
            "Optional", "initial", validate: static value => value is null or "initial");

        internal static readonly Property<int?> NullableValueProperty = Property.Register<StyleNode, int?>(
            "NullableValue", 5, validate: static value => value is null or >= 0);

        internal static readonly Property<Thickness> SpacingProperty = Property.Register<StyleNode, Thickness>(
            "Spacing", validate: static value => value.Left == value.Right);

        static StyleNode()
        {
            CssRegistry.RegisterElement<StyleNode>("PropertyValidationStyleNode");
            CssRegistry.RegisterProperty<StyleNode, int>("validated-value", ValueProperty,
                static text => checked((int)CssValueConverters.ParseNumber(text)));
            CssRegistry.RegisterProperty<StyleNode>("validated-spacing-left", static text =>
            [
                StyleSetter.CreateEdge(SpacingProperty, StyleEdge.Left, CssValueConverters.ParseLength(text))
            ]);
            CssRegistry.RegisterProperty<StyleNode>("validated-spacing-right", static text =>
            [
                StyleSetter.CreateEdge(SpacingProperty, StyleEdge.Right, CssValueConverters.ParseLength(text))
            ]);
        }

        internal int Value
        {
            get => GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }
    }
}