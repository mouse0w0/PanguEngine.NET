using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Drawing;
using PanguEngine.Client.UI.Styling;

namespace PanguEngine.Tests.Client.UI.Styling;

public sealed class UiCssBindingTests
{
    [Fact]
    public void AutomaticTypeNameMatchesRuntimeClassName()
    {
        var theme = new UiStyleResolver(UiStyleResolver.Default.BaseStyleSheets, [UiStyleSheet.Parse("Button { padding: 3px; }")]);

        Assert.Equal(new Thickness(3), theme.Resolve(new Button()).GetValue(Region.PaddingProperty));
    }

    [Fact]
    public void CustomCssTypeNameReplacesConcreteClassName()
    {
        var badgeTheme = new UiStyleResolver([], [UiStyleSheet.Parse("Badge { opacity: 0.25; }")]);
        Assert.Equal(0.25, badgeTheme.Resolve(new ThemedBadge()).GetValue(UiNode.OpacityProperty));

        var wrongNameTheme = new UiStyleResolver([], [UiStyleSheet.Parse("ThemedBadge { opacity: 0.25; }")]);
        Assert.Equal(1d, wrongNameTheme.Resolve(new ThemedBadge()).GetValue(UiNode.OpacityProperty));
    }

    [Fact]
    public void BaseClassNameStillMatchesWhenCustomNameOverrides()
    {
        var regionTheme = new UiStyleResolver([], [UiStyleSheet.Parse("Region { opacity: 0.4; }")]);
        Assert.Equal(0.4, regionTheme.Resolve(new ThemedPanel()).GetValue(UiNode.OpacityProperty));

        var panelTheme = new UiStyleResolver([], [UiStyleSheet.Parse("Panel { opacity: 0.6; }")]);
        Assert.Equal(0.6, panelTheme.Resolve(new ThemedPanel()).GetValue(UiNode.OpacityProperty));
    }

    [Fact]
    public void StronglyTypedRuleUsesClrPropertyRegardlessOfCssAlias()
    {
        var csharpSheet = new UiStyleSheet([
            new UiStyleRule(UiStyleSelector.For<AliasNode>(), [UiStyleSetter.Create(AliasNode.LevelProperty, 5d)])]);
        var csharpTheme = new UiStyleResolver([], [csharpSheet]);
        Assert.Equal(5d, csharpTheme.Resolve(new AliasNode()).GetValue(AliasNode.LevelProperty));

        var cssTheme = new UiStyleResolver([], [UiStyleSheet.Parse("AliasTag { alias-level: 7; }")]);
        Assert.Equal(7d, cssTheme.Resolve(new AliasNode()).GetValue(AliasNode.LevelProperty));
    }

    [Fact]
    public void CustomConverterIsPreferredForCssBinding()
    {
        var theme = new UiStyleResolver([], [UiStyleSheet.Parse("CustomConvNode { custom-conv-value: abcd; }")]);

        Assert.Equal(4d, theme.Resolve(new CustomConvNode()).GetValue(CustomConvNode.ValueProperty));
    }

    [Fact]
    public void BindingRunsConvertersOnceAcrossClassIdAndStateChanges()
    {
        CountingNode.Conversions = 0;
        var node = new CountingNode();
        var sheet = UiStyleSheet.Parse("""
            CountingNode { counted-value: a; }
            CountingNode.primary#save:hover { counted-value: abc; }
            """);
        var root = new Panel();
        root.Children.Add(node);
        var screen = new UiScreen(root);
        screen.SetStyleSheets([sheet]);

        var afterInitial = CountingNode.Conversions;
        Assert.True(afterInitial > 0);

        node.Classes.Add("primary");
        node.StyleId = "save";
        node.SetHovered(true);

        Assert.Equal(afterInitial, CountingNode.Conversions);
        Assert.Equal(3d, node.GetValue(CountingNode.ValueProperty));

        var second = new CountingNode();
        root.Children.Add(second);
        Assert.Equal(1d, second.GetValue(CountingNode.ValueProperty));
        Assert.Equal(afterInitial, CountingNode.Conversions);
        Assert.Same(screen, second.Screen);
    }

    [Fact]
    public void CssDeclarationsUseLastValueAndKeepSourceOrder()
    {
        var theme = new UiStyleResolver([], [UiStyleSheet.Parse(
            "Button { ghost: bad; opacity: 0.2; other: ignored; opacity: 0.6; tail: ignored; }")]);

        var result = theme.Resolve(new Button());

        Assert.Equal(0.6, result.GetValue(UiNode.OpacityProperty));
        Assert.Equal(3, Assert.Single(result.GetSources(UiNode.OpacityProperty)).DeclarationIndex);
    }

    [Fact]
    public void OverriddenCssDeclarationsStillValidateTheirValues()
    {
        var theme = new UiStyleResolver([], [UiStyleSheet.Parse("Button { opacity: bad; opacity: 0.6; }")]);

        var error = Assert.Throws<UiStyleParseException>(() => theme.Resolve(new Button()));

        Assert.Equal(UiStyleParseError.InvalidValue, error.Error);
    }

    [Fact]
    public void SameCssNameBindsSeparatelyForDifferentNodeTypes()
    {
        var theme = new UiStyleResolver([], [UiStyleSheet.Parse("Shared { level: 12; }")]);

        Assert.Equal(12d, theme.Resolve(new NumericNode()).GetValue(NumericNode.LevelProperty));
        Assert.Equal(2d, theme.Resolve(new LengthNode()).GetValue(LengthNode.LevelProperty));
    }

    [Fact]
    public void RuleForUnknownTypeNameHasNoEffect()
    {
        var theme = new UiStyleResolver([], [UiStyleSheet.Parse("Ghost { opacity: 0.2; color: nope; }")]);

        var result = theme.Resolve(new Button());

        Assert.Equal(1d, result.GetValue(UiNode.OpacityProperty));
    }

    [Fact]
    public void MatchedRuleWithInactiveClassStillValidatesValues()
    {
        var theme = new UiStyleResolver([], [UiStyleSheet.Parse("Button.extra { padding: nope; }")]);

        var error = Assert.Throws<UiStyleParseException>(() => theme.Resolve(new Button()));

        Assert.Equal(UiStyleParseError.InvalidValue, error.Error);
    }

    [Fact]
    public void StyleSheetApplicationFailureLeavesPreviousSourcesAndValue()
    {
        var button = new Button();
        var screen = new UiScreen(button);
        var original = button.Background;
        Assert.Equal(new SolidColorBrush(48, 54, 62), original);

        var previous = screen.StyleResolver;
        var error = Assert.Throws<UiStyleParseException>(() =>
            screen.SetStyleSheets([UiStyleSheet.Parse("Button { background: nope; }")]));

        Assert.Equal(UiStyleParseError.InvalidValue, error.Error);
        Assert.Same(previous, screen.StyleResolver);
        Assert.Empty(screen.StyleSheets);
        Assert.Equal(original, button.Background);

        var goodSheet = UiStyleSheet.Parse("Button { background: #010203; }");
        screen.SetStyleSheets([goodSheet]);

        Assert.Same(goodSheet, Assert.Single(screen.StyleSheets));
        Assert.Equal(new SolidColorBrush(1, 2, 3), button.Background);
    }

    [Fact]
    public void BindFailureIsNotCachedAndCanBeRetried()
    {
        FlakyNode.Attempts = 0;
        var node = new FlakyNode();
        var theme = new UiStyleResolver([], [UiStyleSheet.Parse("FlakyNode { flaky: x; }")]);

        var first = Assert.Throws<UiStyleParseException>(() => theme.Resolve(node));
        Assert.Equal(UiStyleParseError.InvalidValue, first.Error);

        var result = theme.Resolve(node);

        Assert.Equal(0.5, result.GetValue(FlakyNode.FlakyProperty));
    }

    [Fact]
    public void PropertyWithoutCssDefinitionIsIgnoredDuringStyleSheetApplication()
    {
        var button = new Button();
        var screen = new UiScreen(button);
        var sheet = UiStyleSheet.Parse("Button { text: hello; opacity: 0.6; }");

        screen.SetStyleSheets([sheet]);

        Assert.Same(sheet, Assert.Single(screen.StyleSheets));
        Assert.Equal(Button.TextProperty.DefaultValue, button.Text);
        Assert.Empty(button.GetStyleValueSources(Button.TextProperty));
        Assert.Equal(0.6, button.Opacity);
    }

    [Fact]
    public void ValueAndSourceQueriesDuringFirstBindingDoNotResolveRecursively()
    {
        var screen = new UiScreen();
        screen.SetStyleSheets([UiStyleSheet.Parse("SourceQueryNode { query-value: 2; }")]);
        var node = new SourceQueryNode();
        SourceQueryNode.Current = node;
        SourceQueryNode.Conversions = 0;
        try
        {
            screen.Root = node;

            Assert.Equal(1, SourceQueryNode.Conversions);
            Assert.Empty(node.SourceDuringBinding!);
            Assert.Equal(1d, node.ValueDuringBinding);
            Assert.Equal(2d, node.GetValue(SourceQueryNode.ValueProperty));
            Assert.NotEmpty(node.GetStyleValueSources(SourceQueryNode.ValueProperty));
        }
        finally
        {
            SourceQueryNode.Current = null;
        }
    }

    [Fact]
    public void SharedSheetDoesNotShareMutableScreenStyleState()
    {
        var sheet = UiStyleSheet.Parse("CountingNode { counted-value: abc; }");
        var firstNode = new CountingNode();
        var secondNode = new CountingNode();
        var first = new UiScreen(firstNode);
        var second = new UiScreen(secondNode);
        CountingNode.Conversions = 0;

        first.SetStyleSheets([sheet]);
        second.SetStyleSheets([sheet]);

        Assert.Equal(2, CountingNode.Conversions);
        Assert.NotSame(first.StyleResolver, second.StyleResolver);
        Assert.Equal(3d, firstNode.GetValue(CountingNode.ValueProperty));
        Assert.Equal(3d, secondNode.GetValue(CountingNode.ValueProperty));
        first.SetStyleSheets([]);
        Assert.Equal(0d, firstNode.GetValue(CountingNode.ValueProperty));
        Assert.Equal(3d, secondNode.GetValue(CountingNode.ValueProperty));
        Assert.Same(sheet, Assert.Single(second.StyleSheets));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ValueAndSourceQueriesDuringReplacementReadCommittedState(bool localOverride)
    {
        var node = new SourceQueryNode();
        var screen = new UiScreen(node);
        screen.SetStyleSheets([UiStyleSheet.Parse("SourceQueryNode { opacity: 0.3; }", "old.css")]);
        if (localOverride)
            node.Opacity = 0.8;
        var previousSource = node.GetStyleValueSources(UiNode.OpacityProperty);
        SourceQueryNode.Current = node;
        SourceQueryNode.Conversions = 0;
        try
        {
            screen.SetStyleSheets([UiStyleSheet.Parse(
                "SourceQueryNode { query-value: 2; opacity: 0.6; }", "new.css")]);

            Assert.NotEmpty(previousSource);
            Assert.NotNull(node.SourceDuringBinding);
            Assert.Equal("old.css", Assert.Single(node.SourceDuringBinding!).SheetSourceName);
            Assert.Equal(localOverride ? 0.8 : 0.3, node.ValueDuringBinding);
            Assert.Equal(1, SourceQueryNode.Conversions);
            Assert.Equal(localOverride ? 0.8 : 0.6, node.Opacity);
            Assert.Equal(
                "new.css",
                Assert.Single(node.GetStyleValueSources(UiNode.OpacityProperty)).SheetSourceName);
        }
        finally
        {
            SourceQueryNode.Current = null;
        }
    }

    [Fact]
    public void NestedPseudoStateBindingReusesAlreadyPublishedCache()
    {
        var screen = new UiScreen();
        screen.SetStyleSheets([UiStyleSheet.Parse("PseudoMutationNode { mutating-value: 2; }")]);
        var node = new PseudoMutationNode();
        PseudoMutationNode.Current = node;
        PseudoMutationNode.Conversions = 0;
        try
        {
            screen.Root = node;

            Assert.False(node.IsEnabled);
            Assert.Equal(2d, node.GetValue(PseudoMutationNode.ValueProperty));
            Assert.Equal(2, PseudoMutationNode.Conversions);
            Assert.Equal(2d, screen.StyleResolver.Resolve(node).GetValue(PseudoMutationNode.ValueProperty));
            Assert.Equal(2, PseudoMutationNode.Conversions);
        }
        finally
        {
            PseudoMutationNode.Current = null;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SiblingQueriesDuringRootAttachmentPreserveNotificationsAndRestoreGuards(bool failAfterQuery)
    {
        var screen = new UiScreen();
        var readerRule = failAfterQuery
            ? "SourceQueryNode { query-value: 2; width: bad; }"
            : "SourceQueryNode { query-value: 2; }";
        screen.SetStyleSheets([UiStyleSheet.Parse("Panel { opacity: 0.6; } " + readerRule)]);
        var reader = new SourceQueryNode();
        var sibling = new Panel();
        var root = new Panel();
        root.Children.Add(reader);
        root.Children.Add(sibling);
        var changes = new List<UiPropertyChangedEventArgs<double>>();
        sibling.PropertyChanged += (_, args) =>
        {
            if (ReferenceEquals(args.Property, UiNode.OpacityProperty))
                changes.Add(Assert.IsType<UiPropertyChangedEventArgs<double>>(args));
        };
        SourceQueryNode.Current = reader;
        SourceQueryNode.QueryTarget = sibling;
        SourceQueryNode.Conversions = 0;
        try
        {
            if (failAfterQuery)
                Assert.Throws<UiStyleParseException>(() => screen.Root = root);
            else
                screen.Root = root;

            Assert.Equal(1, SourceQueryNode.Conversions);
            Assert.NotNull(reader.SourceDuringBinding);
            Assert.Empty(reader.SourceDuringBinding!);
            Assert.Equal(1d, reader.ValueDuringBinding);
            if (failAfterQuery)
            {
                Assert.Empty(changes);
                screen.SetStyleSheets([]);
                Assert.Equal(1d, sibling.Opacity);
            }
            else
            {
                var change = Assert.Single(changes);
                Assert.Equal(1d, change.OldValue);
                Assert.Equal(0.6, change.NewValue);
                Assert.Equal(0.6, sibling.Opacity);
                Assert.NotEmpty(sibling.GetStyleValueSources(UiNode.OpacityProperty));
            }

            reader.StyleId = "ready";
            sibling.StyleId = "ready";
        }
        finally
        {
            SourceQueryNode.Current = null;
            SourceQueryNode.QueryTarget = null;
        }
    }

    private sealed class PseudoMutationNode : UiNode
    {
        static PseudoMutationNode()
        {
            UiCssRegistry.RegisterProperty<PseudoMutationNode, double>("mutating-value", ValueProperty, _ =>
            {
                if (++Conversions > 2)
                    throw new InvalidOperationException("Unexpected recursive binding.");
                Current!.IsEnabled = false;
                return 2d;
            });
        }

        internal static PseudoMutationNode? Current;
        internal static int Conversions;

        internal static readonly UiProperty<double> ValueProperty = UiProperty.Register<PseudoMutationNode, double>(
            "Value", 0);
    }

    private sealed class SourceQueryNode : UiNode
    {
        static SourceQueryNode()
        {
            UiCssRegistry.RegisterProperty<SourceQueryNode, double>("query-value", ValueProperty, _ =>
            {
                if (++Conversions > 1)
                    throw new InvalidOperationException("Unexpected recursive binding.");
                var node = Current!;
                var target = QueryTarget ?? node;
                node.SourceDuringBinding = target.GetStyleValueSources(UiNode.OpacityProperty);
                node.ValueDuringBinding = target.Opacity;
                return 2d;
            });
        }

        internal static SourceQueryNode? Current;
        internal static UiNode? QueryTarget;
        internal static int Conversions;
        internal IReadOnlyList<UiStyleValueSource>? SourceDuringBinding { get; private set; }
        internal double ValueDuringBinding { get; private set; }

        internal static readonly UiProperty<double> ValueProperty = UiProperty.Register<SourceQueryNode, double>(
            "Value", 0);
    }

    private sealed class ThemedBadge : UiNode
    {
        static ThemedBadge()
        {
            UiCssRegistry.RegisterElement<ThemedBadge>("Badge");
        }
    }

    private sealed class ThemedPanel : Panel
    {
        static ThemedPanel()
        {
            UiCssRegistry.RegisterElement<ThemedPanel>("Mega");
        }
    }

    private sealed class AliasNode : UiNode
    {
        static AliasNode()
        {
            UiCssRegistry.RegisterElement<AliasNode>("AliasTag");
            UiCssRegistry.RegisterProperty<AliasNode, double>(
                "alias-level",
                LevelProperty,
                UiCssValueConverters.ParseLength);
        }

        internal static readonly UiProperty<double> LevelProperty =
            UiProperty.Register<AliasNode, double>("Level", 0);
    }

    private sealed class CustomConvNode : UiNode
    {
        static CustomConvNode()
        {
            UiCssRegistry.RegisterProperty<CustomConvNode, double>(
                "custom-conv-value",
                ValueProperty,
                value => value.Length);
        }

        internal static readonly UiProperty<double> ValueProperty =
            UiProperty.Register<CustomConvNode, double>("Value", 0);
    }

    private sealed class CountingNode : UiNode
    {
        static CountingNode()
        {
            UiCssRegistry.RegisterProperty<CountingNode, double>("counted-value", ValueProperty, value =>
            {
                Conversions++;
                return value.Length;
            });
        }

        internal static int Conversions;

        internal static readonly UiProperty<double> ValueProperty =
            UiProperty.Register<CountingNode, double>("Value", 0);
    }

    private sealed class FlakyNode : UiNode
    {
        static FlakyNode()
        {
            UiCssRegistry.RegisterProperty<FlakyNode, double>("flaky", FlakyProperty, _ =>
            {
                Attempts++;
                if (Attempts == 1)
                    throw new FormatException("first attempt fails");
                return 0.5;
            });
        }

        internal static int Attempts;

        internal static readonly UiProperty<double> FlakyProperty =
            UiProperty.Register<FlakyNode, double>("Flaky", 0);
    }

    private sealed class NumericNode : UiNode
    {
        static NumericNode()
        {
            UiCssRegistry.RegisterElement<NumericNode>("Shared");
            UiCssRegistry.RegisterProperty<NumericNode, double>("level", LevelProperty, UiCssValueConverters.ParseLength);
        }

        internal static readonly UiProperty<double> LevelProperty =
            UiProperty.Register<NumericNode, double>("Level", 0);
    }

    private sealed class LengthNode : UiNode
    {
        static LengthNode()
        {
            UiCssRegistry.RegisterElement<LengthNode>("Shared");
            UiCssRegistry.RegisterProperty<LengthNode, double>("level", LevelProperty, static value => value.Length);
        }

        internal static readonly UiProperty<double> LevelProperty =
            UiProperty.Register<LengthNode, double>("Level", 0);
    }
}
