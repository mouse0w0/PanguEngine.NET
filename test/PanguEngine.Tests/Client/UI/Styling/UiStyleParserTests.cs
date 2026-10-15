using System.Text;
using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Drawing;
using PanguEngine.Client.UI.Styling;
using PanguEngine.Graphics.Text;

using PanguEngine.ComponentModel;

namespace PanguEngine.Tests.Client.UI.Styling;

public sealed class UiStyleParserTests
{
    private static Stream Utf8(string text) => new MemoryStream(Encoding.UTF8.GetBytes(text));

    private static UiStyleSheet ParseCss(string text, string? sourceName = null) =>
        UiStyleSheet.Parse(text, sourceName);

    private static UiStyleRule SingleRule(string css) => Assert.Single(ParseCss(css).Rules);

    private static IReadOnlyList<UiStyleRule.BoundDeclaration> BoundDeclarations(string css, UiNode node) =>
        SingleRule(css).Bind(node.GetType());

    private static IReadOnlyList<UiStyleSetter> BoundSetters(string css, UiNode node) =>
        BoundDeclarations(css, node).Select(declaration => declaration.Setter).ToArray();

    private static T ParsedValue<T>(UiNode node, string css)
    {
        var setter = Assert.Single(BoundSetters(css, node));
        return (T)setter.BoxedValue!;
    }

    [Fact]
    public void ParsesCompoundSelectorAndBuiltInValues()
    {
        var sheet = UiStyleSheet.Parse("""
                                       Button.primary#save:hover:focus {
                                           background: #112233cc;
                                           opacity: 0.8;
                                       }
                                       """, "menu.css");

        var rule = Assert.Single(sheet.Rules);
        var selector = rule.Selectors[0];
        Assert.Null(selector.TargetType);
        Assert.Equal("Button", selector.TypeName);
        Assert.Equal(new[] { "primary" }, selector.Classes);
        Assert.Equal("save", selector.Id);
        Assert.Equal(new[] { "focus", "hover" }, selector.PseudoClasses.Select(pseudoClass => pseudoClass.Name));
        Assert.Same(UiPseudoClass.Focus, selector.PseudoClasses[0]);
        Assert.Same(UiPseudoClass.Hover, selector.PseudoClasses[1]);
        Assert.Equal("Button.primary#save:focus:hover", selector.SelectorText);
        Assert.Equal("menu.css", sheet.SourceName);

        var setters = rule.Bind(new Button().GetType());
        Assert.Equal(new Color(0x11, 0x22, 0x33, 0xcc),
            Assert.IsType<SolidColorBrush>(setters[0].Setter.BoxedValue).Color);
        Assert.Equal(0.8, (double)setters[1].Setter.BoxedValue!);
    }

    [Theory]
    [InlineData("*", "*", "*")]
    [InlineData(".danger", "*", ".danger")]
    [InlineData("*.danger", "*", ".danger")]
    [InlineData("*#save", "*", "#save")]
    [InlineData("#save.danger", "*", ".danger#save")]
    [InlineData(":disabled", "*", ":disabled")]
    [InlineData("*:hover", "*", ":hover")]
    [InlineData(".primary.large", "*", ".primary.large")]
    [InlineData("Button.danger:hover#save", "Button", "Button.danger#save:hover")]
    public void ParsesUnqualifiedSelector(string text, string typeName, string normalized)
    {
        var selector = Assert.Single(ParseCss(text + " { }").Rules).Selectors[0];

        Assert.Null(selector.TargetType);
        Assert.Equal(typeName, selector.TypeName);
        Assert.Equal(normalized, selector.SelectorText);
    }

    [Theory]
    [InlineData(".danger")]
    [InlineData("#save")]
    [InlineData(":hover")]
    [InlineData(".danger#save:hover")]
    public void ExplicitAndOmittedWildcardHaveSameNormalizedRepresentation(string conditions)
    {
        var omitted = SingleRule(conditions + " { }").Selectors[0];
        var explicitWildcard = SingleRule("*" + conditions + " { }").Selectors[0];

        Assert.Equal("*", omitted.TypeName);
        Assert.Equal(omitted.TypeName, explicitWildcard.TypeName);
        Assert.Equal(conditions, omitted.SelectorText);
        Assert.Equal(omitted.SelectorText, explicitWildcard.SelectorText);
    }

    [Fact]
    public void UnqualifiedSelectorDeduplicatesClassesAndPseudoClasses()
    {
        var selector = SingleRule(".danger.danger:hover:hover { }").Selectors[0];

        Assert.Equal(".danger:hover", selector.SelectorText);
        Assert.Equal(new[] { "danger" }, selector.Classes);
        Assert.Equal(new[] { "hover" }, selector.PseudoClasses.Select(pseudoClass => pseudoClass.Name));
        Assert.Same(UiPseudoClass.Hover, Assert.Single(selector.PseudoClasses));
        Assert.Equal(2, selector.ClassAndPseudoCount);
    }

    [Fact]
    public void InvalidValueReportsSourceSpanWhenBoundWithoutClosingStream()
    {
        using var stream = Utf8("Button { padding: nope; }");

        var sheet = UiStyleSheet.Parse(stream, "bad.css");
        var rule = Assert.Single(sheet.Rules);
        var node = new Button();
        var error = Assert.Throws<UiStyleParseException>(() => rule.Bind(node.GetType()));

        Assert.Equal(UiStyleParseError.InvalidValue, error.Error);
        Assert.Equal("bad.css", error.SourceName);
        Assert.Equal(1, error.Line);
        Assert.True(stream.CanRead);
    }

    [Fact]
    public void StringAndStreamParseProduceSameModel()
    {
        const string css = "Button { background: #112233; }";
        var fromString = ParseCss(css);
        using var stream = Utf8(css);
        var fromStream = UiStyleSheet.Parse(stream);

        var ruleA = Assert.Single(fromString.Rules);
        var ruleB = Assert.Single(fromStream.Rules);
        var node = new Button();
        var a = (SolidColorBrush)Assert.Single(ruleA.Bind(node.GetType())).Setter.BoxedValue!;
        var b = (SolidColorBrush)Assert.Single(ruleB.Bind(node.GetType())).Setter.BoxedValue!;
        Assert.Equal(a.Color, b.Color);
    }

    [Fact]
    public void AcceptsUtf8BomAndLeavesStreamOpen()
    {
        var bytes = Encoding.UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes("Button { opacity: 0.5; }"))
            .ToArray();
        using var stream = new MemoryStream(bytes);

        var sheet = UiStyleSheet.Parse(stream, "bom.css");

        Assert.True(stream.CanRead);
        var rule = Assert.Single(sheet.Rules);
        Assert.Equal(0.5, (double)Assert.Single(rule.Bind(new Button().GetType())).Setter.BoxedValue!);
    }

    [Fact]
    public void InvalidUtf8ThrowsInvalidEncoding()
    {
        var bytes = new byte[] { 0x42, 0x75, 0x74, 0x74, 0x6f, 0x6e, 0xff, 0xfe };
        using var stream = new MemoryStream(bytes);

        var error = Assert.Throws<UiStyleParseException>(() => UiStyleSheet.Parse(stream));

        Assert.Equal(UiStyleParseError.InvalidEncoding, error.Error);
    }

    [Fact]
    public void IOExceptionPropagatesFromStream()
    {
        using var stream = new FailingStream();
        Assert.Throws<IOException>(() => UiStyleSheet.Parse(stream));
    }

    [Fact]
    public void CrLfCountsAsOneLineForErrorPosition()
    {
        var css = "Button {\r\n\twidth: 1;\r\n}\r\nCanvas {\r\n\theight: bad;\r\n}\r\n";

        var rule = Assert.Single(ParseCss(css, "crlf.css").Rules.Where(r => r.Selectors[0].TypeName == "Canvas"));
        var node = new Canvas();
        var error = Assert.Throws<UiStyleParseException>(() => rule.Bind(node.GetType()));

        Assert.Equal(UiStyleParseError.InvalidValue, error.Error);
        Assert.Equal(5, error.Line);
        Assert.Equal(10, error.Column);
    }

    [Fact]
    public void TabCountsAsOneColumnForInvalidDeclarationValue()
    {
        var rule = SingleRule("Button:hover {\n\twidth: nope;\n}");
        var node = new Button();
        var error = Assert.Throws<UiStyleParseException>(() => rule.Bind(node.GetType()));

        Assert.Equal(UiStyleParseError.InvalidValue, error.Error);
        Assert.Equal(2, error.Line);
        Assert.Equal(9, error.Column);
    }

    [Fact]
    public void ParsesMultipleClassesPseudoClassesAndSingleId()
    {
        var rule = SingleRule("Button.a.b.c:hover:focus:disabled { }");

        Assert.Equal(new[] { "a", "b", "c" }, rule.Selectors[0].Classes);
        Assert.Equal(new[] { "disabled", "focus", "hover" },
            rule.Selectors[0].PseudoClasses.Select(pseudoClass => pseudoClass.Name));
        Assert.Null(rule.Selectors[0].Id);
        Assert.Empty(rule.Setters);
    }

    [Fact]
    public void WhitespaceCreatesADescendantRelationship()
    {
        var selector = SingleRule("Button .primary { }").Selectors[0];

        Assert.Equal("*", selector.TypeName);
        Assert.Equal(new[] { "primary" }, selector.Classes);
        Assert.Equal(1, selector.ClassAndPseudoCount);
        Assert.Equal("Button .primary", selector.SelectorText);
    }

    [Fact]
    public void SkipsBlockCommentsAsTrivia()
    {
        var rule = SingleRule("/* header */ Button { /* inner */ width: 1; /* tail */ } /* footer */");

        Assert.Single(rule.Bind(new Button().GetType()));
    }

    [Fact]
    public void SkipsBlockCommentsInsideDeclarationValues()
    {
        var value = ParsedValue<Thickness>(new Panel(), "Region { padding: 1 /* ; } */ 2; }");

        Assert.Equal(new Thickness(2, 1, 2, 1), value);
    }

    [Fact]
    public void UnterminatedCommentInsideValueReportsError()
    {
        var error = Assert.Throws<UiStyleParseException>(() =>
            ParseCss("Region { padding: 1 /* never closed"));

        Assert.Equal(UiStyleParseError.UnterminatedComment, error.Error);
    }

    [Fact]
    public void EmptyDeclarationBlockIsAllowed()
    {
        var rule = SingleRule("Button { }");

        Assert.Empty(rule.Setters);
        Assert.Empty(rule.Bind(new Button().GetType()));
    }

    [Fact]
    public void MissingSemicolonReportsError()
    {
        var error = Assert.Throws<UiStyleParseException>(() => ParseCss("Button { width: 1 }"));

        Assert.Equal(UiStyleParseError.MissingSemicolon, error.Error);
    }

    [Fact]
    public void TrailingTokenReportsError()
    {
        Assert.Equal(
            UiStyleParseError.TrailingToken,
            Assert.Throws<UiStyleParseException>(() => ParseCss("Button { } }")).Error);
        Assert.Equal(
            UiStyleParseError.TrailingToken,
            Assert.Throws<UiStyleParseException>(() => ParseCss("Button { } ;")).Error);
    }

    [Fact]
    public void EmptySelectorReportsTrailingTokenAtStart()
    {
        var error = Assert.Throws<UiStyleParseException>(() => ParseCss("{}"));

        Assert.Equal(UiStyleParseError.TrailingToken, error.Error);
        Assert.Equal(1, error.Line);
        Assert.Equal(1, error.Column);
        Assert.Equal(1, error.Length);
    }

    [Theory]
    [InlineData(". { }", 2)]
    [InlineData("# { }", 2)]
    [InlineData(": { }", 2)]
    [InlineData(".danger. { }", 9)]
    public void IsolatedConditionPrefixReportsInvalidSyntax(string css, int column)
    {
        var error = Assert.Throws<UiStyleParseException>(() => ParseCss(css));

        Assert.Equal(UiStyleParseError.InvalidSyntax, error.Error);
        Assert.Equal(1, error.Line);
        Assert.Equal(column, error.Column);
        Assert.Equal(1, error.Length);
    }

    [Theory]
    [InlineData("** { }", 2)]
    [InlineData("Button* { }", 7)]
    [InlineData("*.danger* { }", 9)]
    [InlineData("Button ** { }", 9)]
    public void RepeatedOrMisplacedWildcardReportsInvalidSyntax(string css, int column)
    {
        var error = Assert.Throws<UiStyleParseException>(() => ParseCss(css));

        Assert.Equal(UiStyleParseError.InvalidSyntax, error.Error);
        Assert.Equal(1, error.Line);
        Assert.Equal(column, error.Column);
    }

    [Fact]
    public void CommaGroupingPreservesDuplicateBranchesInOneRule()
    {
        var rule = Assert.Single(ParseCss("Button, Button { }").Rules);
        Assert.Equal(["Button", "Button"], rule.Selectors.Select(selector => selector.SelectorText));
    }

    [Fact]
    public void CommaSelectorsShareOneRuleAndSourceSpan()
    {
        const string css = ".a,\n.b /*between*/, .c   { opacity: 0.5; }";

        var sheet = ParseCss(css, "list.css");
        var rule = Assert.Single(sheet.Rules);

        Assert.Equal([".a", ".b", ".c"], rule.Selectors.Select(selector => selector.SelectorText));
        Assert.Equal(css.IndexOf(".c") + ".c".Length, rule.SourceLocation!.Length);
        Assert.Equal(1, rule.SourceLocation.Line);
        Assert.Equal(1, rule.SourceLocation.Column);
        Assert.Equal("list.css", rule.SourceLocation.SourceName);
    }

    [Theory]
    [InlineData(".a,", 4)]
    [InlineData(".a,,.b", 4)]
    [InlineData(".a, { }", 5)]
    [InlineData(".a, ,.b", 5)]
    [InlineData(".a, }", 5)]
    [InlineData(".a, ;", 5)]
    [InlineData(".a > , .b { }", 6)]
    public void EmptyCommaBranchReportsInvalidSyntax(string css, int column)
    {
        var error = Assert.Throws<UiStyleParseException>(() => ParseCss(css));

        Assert.Equal(UiStyleParseError.InvalidSyntax, error.Error);
        Assert.Equal(1, error.Line);
        Assert.Equal(column, error.Column);
    }

    [Fact]
    public void LeadingCommaKeepsTopLevelTrailingTokenDiagnostic()
    {
        var error = Assert.Throws<UiStyleParseException>(() => ParseCss(", .a { }"));

        Assert.Equal(UiStyleParseError.TrailingToken, error.Error);
        Assert.Equal(1, error.Column);
    }

    [Theory]
    [InlineData(".a/**/,/**/.b", ".a|.b")]
    [InlineData(".a/*,*/.b,/*,*/.c", ".a.b|.c")]
    public void CommasInsideCommentsDoNotCreateUnexpectedBranches(string css, string normalized)
    {
        var rule = Assert.Single(ParseCss(css + " { }").Rules);

        Assert.Equal(normalized.Split('|'), rule.Selectors.Select(selector => selector.SelectorText));
    }

    [Fact]
    public void UnknownTypeNameIsAcceptedWithoutTargetType()
    {
        var rule = SingleRule("Ghost { }");

        Assert.Equal("Ghost", rule.Selectors[0].TypeName);
        Assert.Null(rule.Selectors[0].TargetType);
    }

    [Fact]
    public void UnknownPseudoClassIsAcceptedAsNamedCondition()
    {
        var selector = SingleRule("Button:phantom { }").Selectors[0];

        Assert.Equal(new[] { "phantom" }, selector.PseudoClasses.Select(pseudoClass => pseudoClass.Name));
        Assert.Same(UiPseudoClass.Get("phantom"), Assert.Single(selector.PseudoClasses));
        Assert.Equal("Button:phantom", selector.SelectorText);
    }

    [Fact]
    public void PseudoClassNamesAreNormalizedDeduplicatedAndSortedByOrdinal()
    {
        var selector = SingleRule("Button:LOADING:Hover:loading:hover { }").Selectors[0];

        Assert.Equal(new[] { "hover", "loading" }, selector.PseudoClasses.Select(pseudoClass => pseudoClass.Name));
        Assert.Same(UiPseudoClass.Hover, selector.PseudoClasses[0]);
        Assert.Same(UiPseudoClass.Get("loading"), selector.PseudoClasses[1]);
        Assert.Equal("Button:hover:loading", selector.SelectorText);
        Assert.Equal(2, selector.ClassAndPseudoCount);
    }

    [Theory]
    [InlineData("Button:not(.primary) { }")]
    [InlineData("Button:is(.primary) { }")]
    [InlineData("Button:nth-of-type(2) { }")]
    public void UnsupportedFunctionPseudoClassSyntaxIsRejected(string css)
    {
        var error = Assert.Throws<UiStyleParseException>(() => ParseCss(css));

        Assert.Equal(UiStyleParseError.InvalidSyntax, error.Error);
    }

    [Fact]
    public void UnknownPropertyIsIgnoredWhenBound()
    {
        var rule = SingleRule("Button { ghost: 1; }");

        Assert.Empty(rule.Bind(new Button().GetType()));
    }

    [Fact]
    public void DuplicateIdReportsError()
    {
        var error = Assert.Throws<UiStyleParseException>(() => ParseCss("Button#a#b { }"));

        Assert.Equal(UiStyleParseError.DuplicateId, error.Error);
    }

    [Fact]
    public void UnterminatedCommentReportsError()
    {
        var error = Assert.Throws<UiStyleParseException>(() => ParseCss("Button { /* never closed "));

        Assert.Equal(UiStyleParseError.UnterminatedComment, error.Error);
    }

    [Fact]
    public void DoubleRejectsExponentSyntax()
    {
        var rule = SingleRule("UiNode { width: 1e2; }");

        var node = new Panel();
        var error = Assert.Throws<UiStyleParseException>(() => rule.Bind(node.GetType()));

        Assert.Equal(UiStyleParseError.InvalidValue, error.Error);
    }

    [Fact]
    public void ParsesBuiltInColorAndBrushValues()
    {
        var text = new Text();
        Assert.Equal(new Color(0xaa, 0xbb, 0xcc), ParsedValue<Color>(text, "Text { color: #aabbcc; }"));
        Assert.Equal(new Color(0xaa, 0xbb, 0xcc, 0xdd), ParsedValue<Color>(text, "Text { color: #aabbccdd; }"));
        Assert.Equal(new Color(0, 0, 0, 0), ParsedValue<Color>(text, "Text { color: transparent; }"));

        var region = new Panel();
        var brush = ParsedValue<Brush>(region, "Region { background: #112233; }");
        Assert.Equal(new Color(0x11, 0x22, 0x33), Assert.IsType<SolidColorBrush>(brush).Color);

        var transparent = ParsedValue<Brush>(region, "Region { background: transparent; }");
        Assert.Equal(new Color(0, 0, 0, 0), Assert.IsType<SolidColorBrush>(transparent).Color);
    }

    [Fact]
    public void ParsesBuiltInEnumValuesCaseInsensitively()
    {
        Assert.Equal(Orientation.Horizontal,
            ParsedValue<Orientation>(new StackPanel(), "StackPanel { orientation: HORIZONTAL; }"));
        var node = new Panel();
        Assert.Equal(Visibility.Collapsed,
            ParsedValue<Visibility>(node, "UiNode { visibility: collapsed; }"));
        Assert.Equal(HorizontalAlignment.Center,
            ParsedValue<HorizontalAlignment>(node, "UiNode { horizontal-alignment: CENTER; }"));
        var text = new Text();
        Assert.Equal(TextWrapping.Wrap, ParsedValue<TextWrapping>(text, "Text { wrapping: wrap; }"));
        Assert.Equal(TextAlignment.Right, ParsedValue<TextAlignment>(text, "Text { text-alignment: right; }"));
        var image = new ImageView();
        Assert.Equal(ImageStretch.UniformToFill,
            ParsedValue<ImageStretch>(image, "ImageView { stretch: uniformToFill; }"));
        Assert.Equal(ImageSamplingMode.Nearest,
            ParsedValue<ImageSamplingMode>(image, "ImageView { sampling-mode: nearest; }"));
    }

    [Fact]
    public void PropertyNameResolutionIsCaseInsensitive()
    {
        var setter = Assert.Single(BoundSetters("Button { BACKGROUND: #112233; }", new Button()));

        Assert.Same(Region.BackgroundProperty, setter.Property);
    }

    [Fact]
    public void BindingUsesAttachedPropertiesRegisteredByCanvas()
    {
        Assert.Equal("Panel", SingleRule("Panel { }").Selectors[0].TypeName);

        var expected = Canvas.LeftProperty;
        var setter = Assert.Single(BoundSetters("Button { left: 4; }", new Button()));
        Assert.Same(expected, setter.Property);
        Assert.Equal(4d, (double)setter.BoxedValue!);
    }

    [Fact]
    public void StatefulLayoutPropertyBindsSuccessfully()
    {
        var rule = SingleRule("Button:hover { padding: 4; }");

        var declaration = Assert.Single(rule.Bind(new Button().GetType()));
        Assert.Same(Region.PaddingProperty, declaration.Setter.Property);
        Assert.Equal(new Thickness(4), declaration.Setter.BoxedValue);
    }

    [Fact]
    public void CustomPropertyConverterParsesValueAndReportsInvalidValue()
    {
        var node = new FlagConvNode();
        Assert.True(ParsedValue<bool>(node, "FlagConvNode { flag: yes; }"));

        var rule = SingleRule("FlagConvNode { flag: maybe; }");
        var error = Assert.Throws<UiStyleParseException>(() => rule.Bind(node.GetType()));
        Assert.Equal(UiStyleParseError.InvalidValue, error.Error);
    }

    [Fact]
    public void BuiltInBooleanConverterAcceptsTrueAndFalse()
    {
        var node = new BoolNode();
        Assert.True(ParsedValue<bool>(node, "BoolNode { flag: TRUE; }"));
        Assert.False(ParsedValue<bool>(node, "BoolNode { flag: false; }"));

        var rule = SingleRule("BoolNode { flag: yes; }");
        Assert.Equal(
            UiStyleParseError.InvalidValue,
            Assert.Throws<UiStyleParseException>(() => rule.Bind(node.GetType())).Error);
    }

    [Fact]
    public void CustomConverterReceivesOuterTrimWithoutInternalWhitespaceChanges()
    {
        TrimNode.Received = null;

        _ = ParsedValue<string>(new TrimNode(), "TrimNode { raw:  alpha\t beta  ; }");

        Assert.Equal("alpha\t beta", TrimNode.Received);
    }

    [Fact]
    public void ConverterExceptionIsPreservedAsInvalidValueInnerException()
    {
        var expected = new FormatException("converter failed");
        ThrowingNode.Error = expected;
        var rule = SingleRule("ThrowingNode { raw: value; }");

        var node = new ThrowingNode();
        var actual = Assert.Throws<UiStyleParseException>(() => rule.Bind(node.GetType()));

        Assert.Equal(UiStyleParseError.InvalidValue, actual.Error);
        Assert.Same(expected, actual.InnerException);
    }

    [Fact]
    public void BindingResolvesNearestDerivedPropertyForDerivedType()
    {
        var rule = SingleRule("DerivedStyleNode { tone: bright; }");

        var setter = Assert.Single(rule.Bind(new DerivedStyleNode().GetType()));

        Assert.Same(DerivedStyleNode.ToneProperty, setter.Setter.Property);
    }

    [Fact]
    public void SelectorListBindsSameCssNameToDistinctPropertiesOnlyForMatchingBranches()
    {
        var resolver = new UiStyleResolver([], [
            ParseCss(
                "BaseStyleNode.base, DerivedStyleNode.derived { tone: bright; }")
        ]);
        var node = new DerivedStyleNode();
        node.Classes.Add("derived");
        var initial = resolver.Resolve(node);
        Assert.Empty(initial.GetSources(BaseStyleNode.ToneProperty));
        Assert.Equal("bright", initial.GetValue(DerivedStyleNode.ToneProperty));
        node.Classes.Add("base");
        var both = resolver.Resolve(node);
        Assert.Equal("bright", both.GetValue(BaseStyleNode.ToneProperty));
        Assert.Equal("bright", both.GetValue(DerivedStyleNode.ToneProperty));
        Assert.Equal("BaseStyleNode.base", Assert.Single(both.GetSources(BaseStyleNode.ToneProperty)).SelectorText);
        Assert.Equal("DerivedStyleNode.derived",
            Assert.Single(both.GetSources(DerivedStyleNode.ToneProperty)).SelectorText);
    }

    [Fact]
    public void BoundDeclarationRecordsOriginalDeclarationAndCssName()
    {
        var rule = SingleRule("Button { opacity: 0.2; background: #010203; }");

        var bound = rule.Bind(new Button().GetType());

        Assert.Equal(2, bound.Count);
        Assert.Equal("opacity", bound[0].CssPropertyName);
        Assert.Equal("background", bound[1].CssPropertyName);
        Assert.Equal(0, bound[0].DeclarationIndex);
        Assert.Equal(1, bound[1].DeclarationIndex);
        Assert.NotNull(bound[1].SourceLocation);
    }

    [Fact]
    public void NullTextOrStreamThrows()
    {
        Assert.Throws<ArgumentNullException>(() => UiStyleSheet.Parse((string)null!));
        Assert.Throws<ArgumentNullException>(() => UiStyleSheet.Parse((Stream)null!));
    }

    [Theory]
    [InlineData("0.5 !important")]
    [InlineData("0.5!important")]
    [InlineData("0.5 !ImPoRtAnT \t")]
    [InlineData("0.5/* comment */!important")]
    [InlineData("0.5 /*!important*/ !important /* tail */")]
    public void ImportantSuffixIsRemovedBeforeConversion(string value)
    {
        var declaration = Assert.Single(BoundDeclarations($"UiNode {{ opacity: {value}; }}", new Panel()));

        Assert.Equal(0.5, (double)declaration.Setter.BoxedValue!);
        Assert.True(declaration.IsImportant);
    }

    [Theory]
    [InlineData("!important")]
    [InlineData("alpha !important !important")]
    [InlineData("alpha !important beta")]
    [InlineData("alpha !importantx")]
    public void MalformedImportantIsRejectedBeforeCustomConversion(string value)
    {
        TrimNode.Received = null;
        var rule = Assert.Single(ParseCss($"TrimNode {{\n  raw: {value};\n}}", "important.css").Rules);

        var node = new TrimNode();
        var error = Assert.Throws<UiStyleParseException>(() => rule.Bind(node.GetType()));

        Assert.Equal(UiStyleParseError.InvalidValue, error.Error);
        Assert.Equal("important.css", error.SourceName);
        Assert.Equal(2, error.Line);
        Assert.Equal(8, error.Column);
        Assert.Equal(value.Length, error.Length);
        Assert.IsType<FormatException>(error.InnerException);
        Assert.Null(TrimNode.Received);
    }

    [Fact]
    public void ImportantSuffixPreservesInternalWhitespaceAndTrimsSuffixWhitespace()
    {
        var declaration = Assert.Single(BoundDeclarations(
            "TrimNode { raw: alpha\t beta \t\r\n\f\v!important; }", new TrimNode()));

        Assert.Equal("alpha\t beta", declaration.Setter.BoxedValue);
        Assert.Equal("alpha\t beta", TrimNode.Received);
        Assert.True(declaration.IsImportant);
    }

    [Theory]
    [InlineData("alpha ! important", "alpha ! important")]
    [InlineData("alpha !/* comment */important", "alpha ! important")]
    [InlineData("alpha !", "alpha !")]
    [InlineData("alpha !foo", "alpha !foo")]
    [InlineData("important", "important")]
    [InlineData("alpha /*!important*/", "alpha")]
    public void ValuesWithoutContinuousImportantMarkerKeepConverterSemantics(string value, string expected)
    {
        var declaration = Assert.Single(BoundDeclarations($"TrimNode {{ raw: {value}; }}", new TrimNode()));

        Assert.Equal(expected, declaration.Setter.BoxedValue);
        Assert.False(declaration.IsImportant);
    }

    [Fact]
    public void UnknownPropertyWithMalformedImportantIsIgnored()
    {
        Assert.Empty(BoundDeclarations("UiNode { ghost: !important !important; }", new Panel()));
    }

    [Fact]
    public void EmptyValueStillFailsDuringParsing()
    {
        var error = Assert.Throws<UiStyleParseException>(() => ParseCss("UiNode { opacity: ; }"));

        Assert.Equal(UiStyleParseError.InvalidSyntax, error.Error);
    }

    [Fact]
    public void ImportantPseudoClassLayoutDeclarationBindsSuccessfully()
    {
        var rule = SingleRule("Button:hover { padding: 4 !important; }");

        var declaration = Assert.Single(rule.Bind(new Button().GetType()));
        Assert.Equal(new Thickness(4), declaration.Setter.BoxedValue);
        Assert.True(declaration.IsImportant);
    }

    private sealed class FlagConvNode : UiNode
    {
        static FlagConvNode()
        {
            UiCssRegistry.RegisterProperty<FlagConvNode, bool>(
                "flag",
                FlagProperty,
                value => value switch
                {
                    "yes" => true,
                    "no" => false,
                    _ => throw new FormatException($"Unknown flag value '{value}'.")
                });
        }

        internal static readonly Property<bool> FlagProperty =
            Property.Register<FlagConvNode, bool>("Flag", false);
    }

    private sealed class BoolNode : UiNode
    {
        static BoolNode()
        {
            UiCssRegistry.RegisterProperty<BoolNode, bool>("flag", FlagProperty, UiCssValueConverters.ParseBool);
        }

        internal static readonly Property<bool> FlagProperty =
            Property.Register<BoolNode, bool>("Flag", false);
    }

    private sealed class TrimNode : UiNode
    {
        static TrimNode()
        {
            UiCssRegistry.RegisterProperty<TrimNode, string>("raw", RawProperty, value => Received = value);
        }

        internal static string? Received;

        internal static readonly Property<string> RawProperty =
            Property.Register<TrimNode, string>("Raw", string.Empty);
    }

    private sealed class ThrowingNode : UiNode
    {
        static ThrowingNode()
        {
            UiCssRegistry.RegisterProperty<ThrowingNode, string>("raw", RawProperty, _ => throw Error);
        }

        internal static Exception Error = new FormatException("default");

        internal static readonly Property<string> RawProperty =
            Property.Register<ThrowingNode, string>("Raw", string.Empty);
    }

    private class BaseStyleNode : UiNode
    {
        static BaseStyleNode()
        {
            UiCssRegistry.RegisterProperty<BaseStyleNode, string>("tone", ToneProperty, static value => value);
        }

        internal static readonly Property<string> ToneProperty =
            Property.Register<BaseStyleNode, string>("Tone", string.Empty);
    }

    private sealed class DerivedStyleNode : BaseStyleNode
    {
        static DerivedStyleNode()
        {
            UiCssRegistry.RegisterProperty<DerivedStyleNode, string>("tone", ToneProperty, static value => value);
        }

        internal new static readonly Property<string> ToneProperty =
            Property.Register<DerivedStyleNode, string>("Tone", string.Empty);
    }

    private sealed class FailingStream : Stream
    {
        public override int Read(byte[] buffer, int offset, int count) => throw new IOException("boom");

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => 0;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
