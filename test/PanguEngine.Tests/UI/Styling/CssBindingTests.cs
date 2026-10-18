using PanguEngine.ComponentModel;
using PanguEngine.UI;
using PanguEngine.UI.Controls;
using PanguEngine.UI.Drawing;
using PanguEngine.UI.Styling;
using Path = PanguEngine.UI.Controls.Path;

namespace PanguEngine.Tests.UI.Styling;

[Collection(UiToolkitCollection.Name)]
public sealed class CssBindingTests
{
    [Fact]
    public void AutomaticTypeNameMatchesRuntimeClassName()
    {
        var theme = new StyleResolver(StyleResolver.Default.BaseStyleSheets,
            [StyleSheet.Parse("Button { padding: 3px; }")]);

        Assert.Equal(new Thickness(3), theme.Resolve(new Button()).GetValue(Region.PaddingProperty));
    }

    [Fact]
    public void CustomCssTypeNameReplacesConcreteClassName()
    {
        var badgeTheme = new StyleResolver([], [StyleSheet.Parse("Badge { opacity: 0.25; }")]);
        Assert.Equal(0.25, badgeTheme.Resolve(new ThemedBadge()).GetValue(UIElement.OpacityProperty));

        var wrongNameTheme = new StyleResolver([], [StyleSheet.Parse("ThemedBadge { opacity: 0.25; }")]);
        Assert.Equal(1d, wrongNameTheme.Resolve(new ThemedBadge()).GetValue(UIElement.OpacityProperty));
    }

    [Fact]
    public void BaseClassNameStillMatchesWhenCustomNameOverrides()
    {
        var regionTheme = new StyleResolver([], [StyleSheet.Parse("Region { opacity: 0.4; }")]);
        Assert.Equal(0.4, regionTheme.Resolve(new ThemedPanel()).GetValue(UIElement.OpacityProperty));

        var panelTheme = new StyleResolver([], [StyleSheet.Parse("Panel { opacity: 0.6; }")]);
        Assert.Equal(0.6, panelTheme.Resolve(new ThemedPanel()).GetValue(UIElement.OpacityProperty));
    }

    [Fact]
    public void StronglyTypedRuleUsesClrPropertyRegardlessOfCssAlias()
    {
        var csharpSheet = new StyleSheet([
            new StyleRule(StyleSelector.For<AliasNode>(), [StyleSetter.Create(AliasNode.LevelProperty, 5d)])
        ]);
        var csharpTheme = new StyleResolver([], [csharpSheet]);
        Assert.Equal(5d, csharpTheme.Resolve(new AliasNode()).GetValue(AliasNode.LevelProperty));

        var cssTheme = new StyleResolver([], [StyleSheet.Parse("AliasTag { alias-level: 7; }")]);
        Assert.Equal(7d, cssTheme.Resolve(new AliasNode()).GetValue(AliasNode.LevelProperty));
    }

    [Fact]
    public void CustomConverterIsPreferredForCssBinding()
    {
        var theme = new StyleResolver([], [StyleSheet.Parse("CustomConvNode { custom-conv-value: abcd; }")]);

        Assert.Equal(4d, theme.Resolve(new CustomConvNode()).GetValue(CustomConvNode.ValueProperty));
    }

    [Fact]
    public void PathDataCssValueUsesThePathDataProperty()
    {
        var path = new Path { Data = PathGeometry.Parse("M0 0 L2 2") };
        var theme = new StyleResolver([], [
            StyleSheet.Parse(
                "Path { data: \"M0 0 L10 0 L0 10 Z\"; }")
        ]);

        var snapshot = theme.Resolve(path);
        var data = snapshot.GetValue(Path.DataProperty);

        Assert.Equal(new Rect(0, 0, 10, 10), data!.Bounds);
        Assert.Same(Path.DataProperty, CssRegistry.FindProperty(typeof(Path), "data")!.Convert(
            "M0 0 L1 1").Single().Property);
        Assert.Null(CssRegistry.FindProperty(typeof(Path), "path-data"));
        Assert.Null(CssRegistry.FindProperty(typeof(Path), "CssData"));
    }

    [Fact]
    public void InvalidPathDataCssValueReportsThePathFormatError()
    {
        var theme = new StyleResolver([], [StyleSheet.Parse("Path { data: \"M0 0 L\"; }")]);

        var error = Assert.Throws<StyleParseException>(() => theme.Resolve(new Path()));

        Assert.Equal(StyleParseError.InvalidValue, error.Error);
        var formatError = Assert.IsType<FormatException>(error.InnerException);
        Assert.Contains("offset", formatError.Message);
    }

    [Fact]
    public void BindingRunsConvertersOnceAcrossClassIdAndStateChanges()
    {
        CountingNode.Conversions = 0;
        var node = new CountingNode();
        var sheet = StyleSheet.Parse("""
                                     CountingNode { counted-value: a; }
                                     CountingNode.primary#save:hover { counted-value: abc; }
                                     """);
        var root = new Panel();
        root.Children.Add(node);
        var screen = new Screen(root);
        screen.SetStyleSheets([sheet]);
        screen.Root!.UpdateStyles();

        var afterInitial = CountingNode.Conversions;
        Assert.True(afterInitial > 0);

        node.Classes.Add("primary");
        node.StyleId = "save";
        node.SetHovered(true);
        screen.Root!.UpdateStyles();

        Assert.Equal(afterInitial, CountingNode.Conversions);
        Assert.Equal(3d, node.GetValue(CountingNode.ValueProperty));

        var second = new CountingNode();
        root.Children.Add(second);
        screen.Root!.UpdateStyles();
        Assert.Equal(1d, second.GetValue(CountingNode.ValueProperty));
        Assert.Equal(afterInitial, CountingNode.Conversions);
        Assert.Same(screen, second.Screen);
    }

    [Fact]
    public void CssDeclarationsUseLastValueAndKeepSourceOrder()
    {
        var theme = new StyleResolver([], [
            StyleSheet.Parse(
                "Button { ghost: bad; opacity: 0.2; other: ignored; opacity: 0.6; tail: ignored; }")
        ]);

        var result = theme.Resolve(new Button());

        Assert.Equal(0.6, result.GetValue(UIElement.OpacityProperty));
        Assert.Equal(3, Assert.Single(result.GetSources(UIElement.OpacityProperty)).DeclarationIndex);
    }

    [Fact]
    public void OverriddenCssDeclarationsStillValidateTheirValues()
    {
        var theme = new StyleResolver([], [StyleSheet.Parse("Button { opacity: bad; opacity: 0.6; }")]);

        var error = Assert.Throws<StyleParseException>(() => theme.Resolve(new Button()));

        Assert.Equal(StyleParseError.InvalidValue, error.Error);
    }

    [Fact]
    public void SameCssNameBindsSeparatelyForDifferentNodeTypes()
    {
        var theme = new StyleResolver([], [StyleSheet.Parse("Shared { level: 12; }")]);

        Assert.Equal(12d, theme.Resolve(new NumericNode()).GetValue(NumericNode.LevelProperty));
        Assert.Equal(2d, theme.Resolve(new LengthNode()).GetValue(LengthNode.LevelProperty));
    }

    [Fact]
    public void RuleForUnknownTypeNameHasNoEffect()
    {
        var theme = new StyleResolver([], [StyleSheet.Parse("Ghost { opacity: 0.2; color: nope; }")]);

        var result = theme.Resolve(new Button());

        Assert.Equal(1d, result.GetValue(UIElement.OpacityProperty));
    }

    [Fact]
    public void MatchedRuleWithInactiveClassStillValidatesValues()
    {
        var theme = new StyleResolver([], [StyleSheet.Parse("Button.extra { padding: nope; }")]);

        var error = Assert.Throws<StyleParseException>(() => theme.Resolve(new Button()));

        Assert.Equal(StyleParseError.InvalidValue, error.Error);
    }

    [Fact]
    public void InactiveUnqualifiedConditionStillValidatesValues()
    {
        var theme = new StyleResolver([], [StyleSheet.Parse(".extra { padding: nope; }")]);

        var error = Assert.Throws<StyleParseException>(() => theme.Resolve(new Button()));

        Assert.Equal(StyleParseError.InvalidValue, error.Error);
    }

    [Fact]
    public void StatefulUnqualifiedRuleAppliesLayoutPropertyWhileActive()
    {
        var theme = new StyleResolver([], [StyleSheet.Parse(":hover { padding: 4; }")]);
        var node = new Panel();
        Assert.Equal(Thickness.Zero, theme.Resolve(node).GetValue(Region.PaddingProperty));
        node.SetHovered(true);
        Assert.Equal(new Thickness(4), theme.Resolve(node).GetValue(Region.PaddingProperty));
        node.SetHovered(false);
        Assert.Equal(Thickness.Zero, theme.Resolve(node).GetValue(Region.PaddingProperty));
    }

    [Fact]
    public void UnqualifiedClassIdAndStateRulesRefreshOnChanges()
    {
        var node = new Panel();
        var screen = new Screen(node);
        screen.SetStyleSheets([
            StyleSheet.Parse("""
                             .danger { opacity: 0.25; }
                             #save { opacity: 0.6; }
                             :hover { background: #040404; }
                             """)
        ]);

        Assert.Equal(1d, node.Opacity);
        Assert.Empty(node.GetStyleValueSources(Region.BackgroundProperty));

        node.Classes.Add("danger");
        screen.Root!.UpdateStyles();
        Assert.Equal(0.25, node.Opacity);

        node.StyleId = "save";
        screen.Root!.UpdateStyles();
        Assert.Equal(0.6, node.Opacity);

        node.SetHovered(true);
        screen.Root!.UpdateStyles();
        Assert.Equal(new SolidColorBrush(4, 4, 4), node.Background);
        Assert.Equal(0.6, node.Opacity);

        node.StyleId = null;
        screen.Root!.UpdateStyles();
        Assert.Equal(0.25, node.Opacity);
    }

    [Fact]
    public void UnqualifiedSelectorBindsFromRuntimeTypeAndSkipsUnknownProperties()
    {
        var theme = new StyleResolver([], [StyleSheet.Parse(".shared { level: 12; ghost: 1; }")]);
        var numeric = new NumericNode();
        numeric.Classes.Add("shared");
        var length = new LengthNode();
        length.Classes.Add("shared");

        Assert.Equal(12d, theme.Resolve(numeric).GetValue(NumericNode.LevelProperty));
        Assert.Equal(2d, theme.Resolve(length).GetValue(LengthNode.LevelProperty));
    }

    [Fact]
    public void UnqualifiedSelectorConvertsOncePerNodeType()
    {
        CountingNode.Conversions = 0;
        var resolver = new StyleResolver([], [StyleSheet.Parse(".counted { counted-value: abc; ghost: 1; }")]);
        var first = new CountingNode();
        first.Classes.Add("counted");
        var second = new CountingNode();
        second.Classes.Add("counted");

        Assert.Equal(3d, resolver.Resolve(first).GetValue(CountingNode.ValueProperty));
        Assert.Equal(3d, resolver.Resolve(second).GetValue(CountingNode.ValueProperty));

        Assert.Equal(1, CountingNode.Conversions);
    }

    [Fact]
    public void SelectorListWithSameBindingTargetConvertsOncePerNodeType()
    {
        CountingNode.Conversions = 0;
        var resolver = new StyleResolver([], [
            StyleSheet.Parse(
                ".first, .second { counted-value: abc; }")
        ]);
        var node = new CountingNode();
        node.Classes.Add("first");
        node.Classes.Add("second");

        Assert.Equal(3d, resolver.Resolve(node).GetValue(CountingNode.ValueProperty));
        Assert.Equal(1, CountingNode.Conversions);
        node.Classes.Remove("first");
        Assert.Equal(".second",
            Assert.Single(resolver.Resolve(node).GetSources(CountingNode.ValueProperty)).SelectorText);
        node.Classes.Remove("second");
        Assert.Empty(resolver.Resolve(node).GetSources(CountingNode.ValueProperty));
        node.Classes.Add("first");
        Assert.Equal(3d, resolver.Resolve(node).GetValue(CountingNode.ValueProperty));
        Assert.Equal(1, CountingNode.Conversions);
    }

    [Fact]
    public void SelectorListPseudoClassAllowsLayoutProperty()
    {
        var node = new Panel();
        node.Classes.Add("plain");
        var resolver = new StyleResolver([], [StyleSheet.Parse(".plain, :hover { padding: 4px; }")]);
        Assert.Equal(new Thickness(4), resolver.Resolve(node).GetValue(Region.PaddingProperty));
    }

    [Fact]
    public void StyleSheetApplicationFailureLeavesPreviousValueAndNewSources()
    {
        var button = new Button();
        var screen = new Screen(button);
        screen.Root!.UpdateStyles();
        var original = button.Background;
        Assert.Equal(new SolidColorBrush(48, 54, 62), original);

        var previous = screen.StyleResolver;
        screen.SetStyleSheets([StyleSheet.Parse("Button { background: nope; }")]);
        var error = Assert.Throws<StyleParseException>(screen.Root!.UpdateStyles);

        Assert.Equal(StyleParseError.InvalidValue, error.Error);
        Assert.NotSame(previous, screen.StyleResolver);
        Assert.Single(screen.StyleSheets);
        Assert.Equal(original, button.Background);

        var goodSheet = StyleSheet.Parse("Button { background: #010203; }");
        screen.SetStyleSheets([goodSheet]);
        screen.Root!.UpdateStyles();

        Assert.Same(goodSheet, Assert.Single(screen.StyleSheets));
        Assert.Equal(new SolidColorBrush(1, 2, 3), button.Background);
    }

    [Fact]
    public void BindFailureIsNotCachedAndCanBeRetried()
    {
        FlakyNode.Attempts = 0;
        var node = new FlakyNode();
        var theme = new StyleResolver([], [StyleSheet.Parse("FlakyNode { flaky: x; }")]);

        var first = Assert.Throws<StyleParseException>(() => theme.Resolve(node));
        Assert.Equal(StyleParseError.InvalidValue, first.Error);

        var result = theme.Resolve(node);

        Assert.Equal(0.5, result.GetValue(FlakyNode.FlakyProperty));
    }

    [Fact]
    public void PropertyWithoutCssDefinitionIsIgnoredDuringStyleSheetApplication()
    {
        var button = new Button();
        var screen = new Screen(button);
        var sheet = StyleSheet.Parse("Button { text: hello; opacity: 0.6; }");

        screen.SetStyleSheets([sheet]);
        screen.Root!.UpdateStyles();

        Assert.Same(sheet, Assert.Single(screen.StyleSheets));
        Assert.Equal(Button.TextProperty.DefaultValue, button.Text);
        Assert.Empty(button.GetStyleValueSources(Button.TextProperty));
        Assert.Equal(0.6, button.Opacity);
    }

    [Fact]
    public void ValueAndSourceQueriesDuringFirstBindingDoNotResolveRecursively()
    {
        var screen = new Screen();
        screen.SetStyleSheets([StyleSheet.Parse("SourceQueryNode { query-value: 2; }")]);
        var node = new SourceQueryNode();
        SourceQueryNode.Current = node;
        SourceQueryNode.Conversions = 0;
        try
        {
            screen.Root = node;
            screen.Root!.UpdateStyles();

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
        var sheet = StyleSheet.Parse("CountingNode { counted-value: abc; }");
        var firstNode = new CountingNode();
        var secondNode = new CountingNode();
        var first = new Screen(firstNode);
        var second = new Screen(secondNode);
        CountingNode.Conversions = 0;

        first.SetStyleSheets([sheet]);
        second.SetStyleSheets([sheet]);
        first.Root!.UpdateStyles();
        second.Root!.UpdateStyles();

        Assert.Equal(2, CountingNode.Conversions);
        Assert.NotSame(first.StyleResolver, second.StyleResolver);
        Assert.Equal(3d, firstNode.GetValue(CountingNode.ValueProperty));
        Assert.Equal(3d, secondNode.GetValue(CountingNode.ValueProperty));
        first.SetStyleSheets([]);
        first.Root!.UpdateStyles();
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
        var screen = new Screen(node);
        screen.SetStyleSheets([StyleSheet.Parse("SourceQueryNode { opacity: 0.3; }", "old.css")]);
        screen.Root!.UpdateStyles();
        if (localOverride)
            node.Opacity = 0.8;
        var previousSource = node.GetStyleValueSources(UIElement.OpacityProperty);
        SourceQueryNode.Current = node;
        SourceQueryNode.Conversions = 0;
        try
        {
            screen.SetStyleSheets([
                StyleSheet.Parse(
                    "SourceQueryNode { query-value: 2; opacity: 0.6; }", "new.css")
            ]);
            screen.Root!.UpdateStyles();

            Assert.NotEmpty(previousSource);
            Assert.NotNull(node.SourceDuringBinding);
            Assert.Equal("old.css", Assert.Single(node.SourceDuringBinding!).SheetSourceName);
            Assert.Equal(localOverride ? 0.8 : 0.3, node.ValueDuringBinding);
            Assert.Equal(1, SourceQueryNode.Conversions);
            Assert.Equal(localOverride ? 0.8 : 0.6, node.Opacity);
            Assert.Equal(
                "new.css",
                Assert.Single(node.GetStyleValueSources(UIElement.OpacityProperty)).SheetSourceName);
        }
        finally
        {
            SourceQueryNode.Current = null;
        }
    }

    [Fact]
    public void ConverterStateChangesKeepStylesPending()
    {
        var screen = new Screen();
        screen.SetStyleSheets([StyleSheet.Parse("PseudoMutationNode { mutating-value: 2; }")]);
        var node = new PseudoMutationNode();
        PseudoMutationNode.Current = node;
        PseudoMutationNode.Conversions = 0;
        try
        {
            screen.Root = node;
            screen.Root!.UpdateStyles();

            Assert.False(node.IsEnabled);
            Assert.True(node.HasPseudoClass(PseudoClass.Disabled));
            Assert.Equal(1, PseudoMutationNode.Conversions);
            Assert.False(node.IsStyleSubtreeValid);
            Assert.Equal(2d, node.GetValue(PseudoMutationNode.ValueProperty));

            screen.Root!.UpdateStyles();

            Assert.True(node.IsStyleSubtreeValid);
            Assert.Equal(1, PseudoMutationNode.Conversions);
        }
        finally
        {
            PseudoMutationNode.Current = null;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SiblingQueriesDuringApplicationPreserveNotificationsAndRestoreGuards(bool failAfterQuery)
    {
        var screen = new Screen();
        var readerRule = failAfterQuery
            ? "SourceQueryNode { query-value: 2; width: bad; }"
            : "SourceQueryNode { query-value: 2; }";
        screen.SetStyleSheets([StyleSheet.Parse("Panel { opacity: 0.6; } " + readerRule)]);
        var reader = new SourceQueryNode();
        var sibling = new Panel();
        var root = new Panel();
        root.Children.Add(reader);
        root.Children.Add(sibling);
        var changes = new List<PropertyChangedEventArgs<double>>();
        sibling.PropertyChanged += (_, args) =>
        {
            if (ReferenceEquals(args.Property, UIElement.OpacityProperty))
                changes.Add(Assert.IsType<PropertyChangedEventArgs<double>>(args));
        };
        SourceQueryNode.Current = reader;
        SourceQueryNode.QueryTarget = sibling;
        SourceQueryNode.Conversions = 0;
        try
        {
            screen.Root = root;
            if (failAfterQuery)
                Assert.Throws<StyleParseException>(screen.Root!.UpdateStyles);
            else
                screen.Root!.UpdateStyles();

            Assert.Equal(1, SourceQueryNode.Conversions);
            Assert.NotNull(reader.SourceDuringBinding);
            Assert.Empty(reader.SourceDuringBinding!);
            Assert.Equal(1d, reader.ValueDuringBinding);
            if (failAfterQuery)
            {
                Assert.Empty(changes);
                screen.SetStyleSheets([]);
                screen.Root!.UpdateStyles();
                Assert.Equal(1d, sibling.Opacity);
            }
            else
            {
                var change = Assert.Single(changes);
                Assert.Equal(1d, change.OldValue);
                Assert.Equal(0.6, change.NewValue);
                Assert.Equal(0.6, sibling.Opacity);
                Assert.NotEmpty(sibling.GetStyleValueSources(UIElement.OpacityProperty));
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

    private sealed class PseudoMutationNode : UIElement
    {
        static PseudoMutationNode()
        {
            CssRegistry.RegisterProperty<PseudoMutationNode, double>("mutating-value", ValueProperty, _ =>
            {
                if (++Conversions > 2)
                    throw new InvalidOperationException("Unexpected recursive binding.");
                Current!.IsEnabled = false;
                return 2d;
            });
        }

        internal static PseudoMutationNode? Current;
        internal static int Conversions;

        internal static readonly Property<double> ValueProperty = Property.Register<PseudoMutationNode, double>(
            "Value", 0);
    }

    private sealed class SourceQueryNode : UIElement
    {
        static SourceQueryNode()
        {
            CssRegistry.RegisterProperty<SourceQueryNode, double>("query-value", ValueProperty, _ =>
            {
                if (++Conversions > 1)
                    throw new InvalidOperationException("Unexpected recursive binding.");
                var node = Current!;
                var target = QueryTarget ?? node;
                node.SourceDuringBinding = target.GetStyleValueSources(OpacityProperty);
                node.ValueDuringBinding = target.Opacity;
                return 2d;
            });
        }

        internal static SourceQueryNode? Current;
        internal static UIElement? QueryTarget;
        internal static int Conversions;
        internal IReadOnlyList<StyleValueSource>? SourceDuringBinding { get; private set; }
        internal double ValueDuringBinding { get; private set; }

        internal static readonly Property<double> ValueProperty = Property.Register<SourceQueryNode, double>(
            "Value", 0);
    }

    private sealed class ThemedBadge : UIElement
    {
        static ThemedBadge()
        {
            CssRegistry.RegisterElement<ThemedBadge>("Badge");
        }
    }

    private sealed class ThemedPanel : Panel
    {
        static ThemedPanel()
        {
            CssRegistry.RegisterElement<ThemedPanel>("Mega");
        }
    }

    private sealed class AliasNode : UIElement
    {
        static AliasNode()
        {
            CssRegistry.RegisterElement<AliasNode>("AliasTag");
            CssRegistry.RegisterProperty<AliasNode, double>(
                "alias-level",
                LevelProperty,
                CssValueConverters.ParseLength);
        }

        internal static readonly Property<double> LevelProperty =
            Property.Register<AliasNode, double>("Level", 0);
    }

    private sealed class CustomConvNode : UIElement
    {
        static CustomConvNode()
        {
            CssRegistry.RegisterProperty<CustomConvNode, double>(
                "custom-conv-value",
                ValueProperty,
                value => value.Length);
        }

        internal static readonly Property<double> ValueProperty =
            Property.Register<CustomConvNode, double>("Value", 0);
    }

    private sealed class CountingNode : UIElement
    {
        static CountingNode()
        {
            CssRegistry.RegisterProperty<CountingNode, double>("counted-value", ValueProperty, value =>
            {
                Conversions++;
                return value.Length;
            });
        }

        internal static int Conversions;

        internal static readonly Property<double> ValueProperty =
            Property.Register<CountingNode, double>("Value", 0);
    }

    private sealed class FlakyNode : UIElement
    {
        static FlakyNode()
        {
            CssRegistry.RegisterProperty<FlakyNode, double>("flaky", FlakyProperty, _ =>
            {
                Attempts++;
                if (Attempts == 1)
                    throw new FormatException("first attempt fails");
                return 0.5;
            });
        }

        internal static int Attempts;

        internal static readonly Property<double> FlakyProperty =
            Property.Register<FlakyNode, double>("Flaky", 0);
    }

    private sealed class NumericNode : UIElement
    {
        static NumericNode()
        {
            CssRegistry.RegisterElement<NumericNode>("Shared");
            CssRegistry.RegisterProperty<NumericNode, double>("level", LevelProperty,
                CssValueConverters.ParseLength);
        }

        internal static readonly Property<double> LevelProperty =
            Property.Register<NumericNode, double>("Level", 0);
    }

    private sealed class LengthNode : UIElement
    {
        static LengthNode()
        {
            CssRegistry.RegisterElement<LengthNode>("Shared");
            CssRegistry.RegisterProperty<LengthNode, double>("level", LevelProperty, static value => value.Length);
        }

        internal static readonly Property<double> LevelProperty =
            Property.Register<LengthNode, double>("Level", 0);
    }
}