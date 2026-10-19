using PanguEngine.ComponentModel;
using PanguEngine.UI;
using PanguEngine.UI.Controls;
using PanguEngine.UI.Styling;

namespace PanguEngine.Tests.UI.Styling;

[Collection(UIToolkitCollection.Name)]
public sealed class CssRegistryTests
{
    private static bool LazyAliasInitialized;

    [Fact]
    public void PropertiesWithoutCssDefinitionAreNotExposed()
    {
        _ = new Text();
        _ = new Button();
        _ = new ImageView();

        Assert.Null(CssRegistry.FindProperty(typeof(Text), "content"));
        Assert.Null(CssRegistry.FindProperty(typeof(Button), "text"));
        Assert.Null(CssRegistry.FindProperty(typeof(ImageView), "source"));
        Assert.Null(CssRegistry.FindProperty(typeof(UIElement), "not-a-css-property"));
    }

    [Fact]
    public void SimpleRegistrationExposesNameTargetAndConvertedSetters()
    {
        CssRegistry.RegisterProperty<SimpleCssNode, double>(
            "simple-css-length",
            SimpleCssNode.ValueProperty,
            CssValueConverters.ParseLength);

        var css = CssRegistry.FindProperty(typeof(SimpleCssNode), "SIMPLE-CSS-LENGTH")!;

        Assert.Equal("simple-css-length", css.Name);
        Assert.Equal(typeof(SimpleCssNode), css.TargetType);
        Assert.Same(css, CssRegistry.FindProperty(typeof(SimpleCssNode), "simple-css-length"));

        var setter = Assert.Single(css.Convert("4px"));

        Assert.Same(SimpleCssNode.ValueProperty, setter.Property);
        Assert.Null(setter.Component);
        Assert.Equal(4d, (double)setter.BoxedValue!);
    }

    [Fact]
    public void FindPropertyReturnsNearestInheritedDefinitionAndIgnoresDerivedWithoutDefinition()
    {
        CssRegistry.RegisterProperty<FindBaseNode, double>(
            "find-base-tone",
            FindBaseNode.ValueProperty,
            CssValueConverters.ParseLength);
        CssRegistry.RegisterProperty<FindDerivedNode, double>(
            "find-derived-tone",
            FindDerivedNode.ValueProperty,
            CssValueConverters.ParseLength);

        var baseCss = CssRegistry.FindProperty(typeof(FindDerivedNode), "find-base-tone");
        var derivedCss = CssRegistry.FindProperty(typeof(FindDerivedNode), "FIND-DERIVED-TONE");

        Assert.Same(CssRegistry.FindProperty(typeof(FindBaseNode), "find-base-tone"), baseCss);
        Assert.Same(derivedCss, CssRegistry.FindProperty(typeof(FindGrandchildNode), "find-derived-tone"));
        Assert.Same(baseCss, CssRegistry.FindProperty(typeof(FindGrandchildNode), "find-base-tone"));
    }

    [Fact]
    public void UnregisteredTypeUsesClrNameAsCssType()
    {
        Assert.True(SingleSelector(nameof(FallbackNameNode)).Matches(new FallbackNameNode()));
    }

    [Fact]
    public void RegisterElementAliasReplacesClrNameForMatching()
    {
        Assert.True(SingleSelector("AliasedTag").Matches(new AliasedTagNode()));
        Assert.False(SingleSelector(nameof(AliasedTagNode)).Matches(new AliasedTagNode()));
    }

    [Fact]
    public void UnregisteredDerivedMatchesBaseAliasAndUsesBaseProperty()
    {
        var node = new UnregisteredAliasDerivedNode();

        Assert.True(SingleSelector(nameof(UnregisteredAliasDerivedNode)).Matches(node));
        Assert.True(SingleSelector("BaseAliasTag").Matches(node));
        Assert.False(SingleSelector(nameof(AliasBaseNode)).Matches(node));

        var resolver = new StyleResolver([], [StyleSheet.Parse("BaseAliasTag { alias-tone: 4; }")]);
        var snapshot = resolver.Resolve(node);

        Assert.Equal(4d, snapshot.GetValue(AliasBaseNode.BaseToneProperty));
        Assert.Equal(7d, snapshot.GetValue(UnregisteredAliasDerivedNode.DerivedToneProperty));
    }

    [Fact]
    public void SameElementAliasNearestInChainWins()
    {
        var resolver = new StyleResolver([], [StyleSheet.Parse("shadow-tag { shadow-tone: 3; }")]);
        var snapshot = resolver.Resolve(new ShadowTagDerivedNode());

        Assert.Equal(3d, snapshot.GetValue(ShadowTagDerivedNode.DerivedToneProperty));
        Assert.Equal(9d, snapshot.GetValue(ShadowTagBaseNode.BaseToneProperty));
    }

    [Fact]
    public void ElementNamesAreCaseSensitiveWhilePropertyNamesAreNot()
    {
        var node = new CaseSensitiveNode();

        Assert.True(SingleSelector("case-tag").Matches(node));
        Assert.False(SingleSelector("CASE-TAG").Matches(node));
        Assert.NotNull(CssRegistry.FindProperty(typeof(CaseSensitiveNode), "CASE-TAG"));
    }

    [Fact]
    public void InvalidAndDuplicateElementRegistrationsAreRejectedWithoutPublishing()
    {
        Assert.Throws<ArgumentException>(() => CssRegistry.RegisterElement<InvalidElementNode>("9bad"));
        Assert.True(SingleSelector(nameof(InvalidElementNode)).Matches(new InvalidElementNode()));

        CssRegistry.RegisterElement<DuplicateElementNode>("dup-tag");
        Assert.Throws<InvalidOperationException>(() => CssRegistry.RegisterElement<DuplicateElementNode>("dup-tag"));
        Assert.Throws<InvalidOperationException>(() =>
            CssRegistry.RegisterElement<DuplicateElementNode>("other-tag"));

        Assert.True(SingleSelector("dup-tag").Matches(new DuplicateElementNode()));
        Assert.False(SingleSelector("other-tag").Matches(new DuplicateElementNode()));
    }

    [Fact]
    public void PublishedBindingCacheRefreshesAfterLateElementAndPropertyRegistration()
    {
        var resolver = new StyleResolver([], [StyleSheet.Parse("late-tag { opacity: 0.2; late-prop: 4; }")]);
        var node = new LateRegistrationNode();
        Assert.Equal(1d, resolver.Resolve(node).GetValue(UIElement.OpacityProperty));

        CssRegistry.RegisterElement<LateRegistrationNode>("late-tag");
        CssRegistry.RegisterProperty<LateRegistrationNode, double>(
            "late-prop",
            LateRegistrationNode.ValueProperty,
            CssValueConverters.ParseLength);

        var snapshot = resolver.Resolve(node);
        Assert.Equal(0.2, snapshot.GetValue(UIElement.OpacityProperty));
        Assert.Equal(4d, snapshot.GetValue(LateRegistrationNode.ValueProperty));
    }

    [Fact]
    public void ParsingTypeLookupAndBindingDoNotInitializeCssDefinitions()
    {
        LazyAliasInitialized = false;

        _ = StyleSheet.Parse("lazy-tag { }");
        Assert.False(LazyAliasInitialized);

        var selector = SingleSelector("lazy-tag");
        Assert.Null(selector.MatchTargetType(typeof(LazyAliasNode)));
        Assert.False(LazyAliasInitialized);
        Assert.Null(CssRegistry.FindProperty(typeof(LazyAliasNode), "lazy-alias-tone"));
        Assert.False(LazyAliasInitialized);
        var rule = Assert.Single(StyleSheet.Parse("lazy-tag { lazy-alias-tone: 0.4; }").Rules);
        Assert.Empty(rule.Bind(typeof(LazyAliasNode)));
        Assert.False(LazyAliasInitialized);

        var node = new LazyAliasNode();
        Assert.True(LazyAliasInitialized);
        Assert.True(selector.Matches(node));
        Assert.NotNull(CssRegistry.FindProperty(node.GetType(), "lazy-alias-tone"));
        Assert.Equal(0.4, Assert.Single(rule.Bind(node.GetType())).Setter.BoxedValue);
    }

    [Fact]
    public void DuplicateNameForTargetIsRejectedWithoutPublishingHalfRegistration()
    {
        var name = $"dup-{Guid.NewGuid():N}";
        CssRegistry.RegisterProperty<DupCssNode, double>(
            name,
            DupCssNode.FirstProperty,
            CssValueConverters.ParseLength);

        Assert.Throws<InvalidOperationException>(() =>
            CssRegistry.RegisterProperty<DupCssNode, double>(
                name,
                DupCssNode.SecondProperty,
                CssValueConverters.ParseLength));

        Assert.Same(
            DupCssNode.FirstProperty,
            Assert.Single(CssRegistry.FindProperty(typeof(DupCssNode), name)!.Convert("1")).Property);
    }

    [Fact]
    public void InvalidNameReadOnlyAndWrongTargetAreRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            CssRegistry.RegisterProperty<RejectCssNode, double>(
                "9invalid",
                Property.Register<RejectCssNode, double>("Nine", 0),
                CssValueConverters.ParseLength));

        var readOnly = Property.RegisterReadOnly<RejectCssNode, double>("Value", 0).Property;
        Assert.Throws<ArgumentException>(() =>
            CssRegistry.RegisterProperty<RejectCssNode, double>(
                "readonly-css",
                readOnly,
                CssValueConverters.ParseLength));

        var foreign = Property.Register<ForeignOwnerNode, double>("Value", 0);
        Assert.Throws<ArgumentException>(() =>
            CssRegistry.RegisterProperty<RejectCssNode, double>(
                "foreign-css",
                foreign,
                CssValueConverters.ParseLength));
    }

    [Fact]
    public void AttachedPropertyRegistrationResolvesOnItsTargetType()
    {
        CssRegistry.RegisterProperty<AttachedCssTarget, double>(
            "attached-css-style",
            AttachedCssOwner.ValueProperty,
            CssValueConverters.ParseLength);

        var css = CssRegistry.FindProperty(typeof(AttachedCssTarget), "attached-css-style")!;

        Assert.Equal(typeof(AttachedCssTarget), css.TargetType);
        Assert.Same(AttachedCssOwner.ValueProperty, Assert.Single(css.Convert("12")).Property);
    }

    [Fact]
    public void ExpandRegistrationReturnsTypedEdgesAndBindsOriginalDeclarationIndex()
    {
        var node = new ExpandCssNode();
        var css = CssRegistry.FindProperty(node.GetType(), "expand-sides")!;

        Assert.Equal(
            [StyleEdge.Left, StyleEdge.Right],
            css.Convert("5").Select(setter => setter.Component));

        var rule = Assert.Single(StyleSheet.Parse("ExpandCssNode { expand-sides: 2; expand-sides: 3; }").Rules);
        var bound = rule.Bind(node.GetType());

        Assert.Equal([0, 0, 1, 1], bound.Select(declaration => declaration.DeclarationIndex));
        Assert.All(bound, declaration => Assert.Equal("expand-sides", declaration.CssPropertyName));
        Assert.Equal(
            [StyleEdge.Left, StyleEdge.Right, StyleEdge.Left, StyleEdge.Right],
            bound.Select(declaration => declaration.Setter.Component));
    }

    [Fact]
    public void ExpandRegistrationRejectsDuplicateComponentAtBind()
    {
        var node = new ExpandCssNode();
        var rule = Assert.Single(StyleSheet.Parse("ExpandCssNode { expand-duplicate: 1; }").Rules);

        var error = Assert.Throws<StyleParseException>(() => rule.Bind(node.GetType()));

        Assert.Equal(StyleParseError.InvalidValue, error.Error);
    }

    [Fact]
    public void ExpandRegistrationRejectsReadOnlyAndForeignOutputAtBind()
    {
        AssertBindInvalid("ExpandRejectNode { reject-readonly: 1; }");
        AssertBindInvalid("ExpandRejectNode { reject-foreign: 1; }");
    }

    [Fact]
    public void PseudoClassRuleAcceptsExpandedLayoutOutputAtBind()
    {
        var rule = Assert.Single(StyleSheet.Parse("Panel:hover { padding-top: 4; }").Rules);

        var declaration = Assert.Single(rule.Bind(new Panel().GetType()));
        Assert.Same(Region.PaddingProperty, declaration.Setter.Property);
        Assert.Equal<StyleEdge?>(StyleEdge.Top, declaration.Setter.Component);
    }

    [Fact]
    public void DerivedLookupUsesLaterBaseDefinitions()
    {
        CssRegistry.RegisterProperty<RegistrationBoundaryDerivedA, double>(
            "boundary-a",
            RegistrationBoundaryDerivedA.ValueProperty,
            CssValueConverters.ParseLength);
        var resolver = new StyleResolver([], [StyleSheet.Parse("* { boundary-base: 3; boundary-a: 4; }")]);
        var node = new RegistrationBoundaryDerivedA();
        Assert.Equal(0d, resolver.Resolve(node).GetValue(RegistrationBoundaryBase.ValueProperty));

        CssRegistry.RegisterProperty<RegistrationBoundaryBase, double>(
            "boundary-base",
            RegistrationBoundaryBase.ValueProperty,
            CssValueConverters.ParseLength);

        var snapshot = resolver.Resolve(node);
        Assert.Equal(3d, snapshot.GetValue(RegistrationBoundaryBase.ValueProperty));
        Assert.Equal(4d, snapshot.GetValue(RegistrationBoundaryDerivedA.ValueProperty));
    }

    [Fact]
    public void BoundResultSurvivesConverterListMutation()
    {
        var node = new ExpandMutableNode();
        var resolver = new StyleResolver([], [StyleSheet.Parse("ExpandMutableNode { mutable-sides: 5; }")]);
        Assert.Equal(
            new Thickness(5, 0, 5, 0),
            resolver.Resolve(node).GetValue(ExpandMutableNode.PaddingProperty));

        ExpandMutableNode.Output.Clear();

        Assert.Equal(
            new Thickness(5, 0, 5, 0),
            resolver.Resolve(new ExpandMutableNode()).GetValue(ExpandMutableNode.PaddingProperty));
    }

    [Fact]
    public void ParseLengthAcceptsUnitAndPxWhileParseNumberRejectsUnits()
    {
        Assert.Equal(3d, CssValueConverters.ParseLength("3"));
        Assert.Equal(3d, CssValueConverters.ParseLength("3px"));
        Assert.Equal(0.5, CssValueConverters.ParseNumber("0.5"));
        Assert.Throws<FormatException>(() => CssValueConverters.ParseNumber("3px"));
    }

    [Fact]
    public void ParseThicknessSupportsOneToFourValuesAndRejectsOtherCounts()
    {
        Assert.Equal(new Thickness(5), CssValueConverters.ParseThickness("5"));
        Assert.Equal(new Thickness(2, 5, 2, 5), CssValueConverters.ParseThickness("5 2"));
        Assert.Equal(new Thickness(2, 5, 2, 8), CssValueConverters.ParseThickness("5 2 8"));
        Assert.Equal(new Thickness(4, 1, 2, 3), CssValueConverters.ParseThickness("1 2 3 4"));
        Assert.Throws<FormatException>(() => CssValueConverters.ParseThickness("1 2 3 4 5"));
    }

    private static StyleSelector SingleSelector(string name) =>
        Assert.Single(StyleSheet.Parse($"{name} {{ }}").Rules).Selectors[0];

    private static void AssertBindInvalid(string css)
    {
        var rule = Assert.Single(StyleSheet.Parse(css).Rules);

        var node = new ExpandRejectNode();
        var error = Assert.Throws<StyleParseException>(() => rule.Bind(node.GetType()));

        Assert.Equal(StyleParseError.InvalidValue, error.Error);
        Assert.IsType<ArgumentException>(error.InnerException);
    }

    private sealed class SimpleCssNode : UIElement
    {
        internal static readonly Property<double> ValueProperty =
            Property.Register<SimpleCssNode, double>("Value", 0);
    }

    private class FindBaseNode : UIElement
    {
        internal static readonly Property<double> ValueProperty =
            Property.Register<FindBaseNode, double>("Value", 0);
    }

    private class FindDerivedNode : FindBaseNode
    {
        internal new static readonly Property<double> ValueProperty =
            Property.Register<FindDerivedNode, double>("Value", 1);
    }

    private sealed class FindGrandchildNode : FindDerivedNode
    {
    }

    private sealed class DupCssNode : UIElement
    {
        internal static readonly Property<double> FirstProperty =
            Property.Register<DupCssNode, double>("First", 0);

        internal static readonly Property<double> SecondProperty =
            Property.Register<DupCssNode, double>("Second", 0);
    }

    private sealed class RejectCssNode : UIElement
    {
    }

    private sealed class ForeignOwnerNode : UIElement
    {
    }

    private sealed class AttachedCssOwner : UIElement
    {
        internal static readonly Property<double> ValueProperty =
            Property.RegisterAttached<AttachedCssOwner, AttachedCssTarget, double>("Value", 0);
    }

    private sealed class AttachedCssTarget : UIElement
    {
    }

    private sealed class ExpandCssNode : UIElement
    {
        static ExpandCssNode()
        {
            CssRegistry.RegisterProperty<ExpandCssNode>("expand-sides", value =>
            {
                var edge = CssValueConverters.ParseLength(value);
                return
                [
                    StyleSetter.CreateEdge(PaddingProperty, StyleEdge.Left, edge),
                    StyleSetter.CreateEdge(PaddingProperty, StyleEdge.Right, edge)
                ];
            });

            CssRegistry.RegisterProperty<ExpandCssNode>("expand-duplicate", value =>
            [
                StyleSetter.Create(PaddingProperty, new Thickness(CssValueConverters.ParseLength(value))),
                StyleSetter.CreateEdge(PaddingProperty, StyleEdge.Left, CssValueConverters.ParseLength(value))
            ]);
        }

        internal static readonly Property<Thickness> PaddingProperty =
            Property.Register<ExpandCssNode, Thickness>("Padding", Thickness.Zero,
                onChanged: static (node, _, _) => node.InvalidateMeasure());
    }

    private class RegistrationBoundaryBase : UIElement
    {
        internal static readonly Property<double> ValueProperty =
            Property.Register<RegistrationBoundaryBase, double>("Value", 0);
    }

    private sealed class RegistrationBoundaryDerivedA : RegistrationBoundaryBase
    {
        internal new static readonly Property<double> ValueProperty =
            Property.Register<RegistrationBoundaryDerivedA, double>("Value", 0);
    }

    private sealed class ExpandRejectNode : UIElement
    {
        static ExpandRejectNode()
        {
            CssRegistry.RegisterProperty<ExpandRejectNode>("reject-readonly", _ =>
                new[] { StyleSetter.Create(ReadOnlyProperty, 1) });
            CssRegistry.RegisterProperty<ExpandRejectNode>("reject-foreign", _ =>
                new[] { StyleSetter.Create(ExpandForeignOwner.ValueProperty, 1d) });
        }

        internal static readonly Property<int> ReadOnlyProperty =
            Property.RegisterReadOnly<ExpandRejectNode, int>("ReadOnly", 0).Property;
    }

    private sealed class ExpandForeignOwner : UIElement
    {
        internal static readonly Property<double> ValueProperty =
            Property.Register<ExpandForeignOwner, double>("Value", 0);
    }

    private sealed class ExpandMutableNode : UIElement
    {
        static ExpandMutableNode()
        {
            CssRegistry.RegisterProperty<ExpandMutableNode>("mutable-sides", value =>
            {
                var edge = CssValueConverters.ParseLength(value);
                Output.Clear();
                Output.Add(StyleSetter.CreateEdge(PaddingProperty, StyleEdge.Left, edge));
                Output.Add(StyleSetter.CreateEdge(PaddingProperty, StyleEdge.Right, edge));
                return Output;
            });
        }

        internal static readonly List<StyleSetter> Output = [];

        internal static readonly Property<Thickness> PaddingProperty =
            Property.Register<ExpandMutableNode, Thickness>("Padding", Thickness.Zero,
                onChanged: static (node, _, _) => node.InvalidateMeasure());
    }

    private sealed class FallbackNameNode : UIElement
    {
    }

    private sealed class AliasedTagNode : UIElement
    {
        static AliasedTagNode()
        {
            CssRegistry.RegisterElement<AliasedTagNode>("AliasedTag");
        }
    }

    private class AliasBaseNode : UIElement
    {
        static AliasBaseNode()
        {
            CssRegistry.RegisterElement<AliasBaseNode>("BaseAliasTag");
            CssRegistry.RegisterProperty<AliasBaseNode, double>(
                "alias-tone",
                BaseToneProperty,
                CssValueConverters.ParseLength);
        }

        internal static readonly Property<double> BaseToneProperty =
            Property.Register<AliasBaseNode, double>("BaseTone", 0);
    }

    private sealed class UnregisteredAliasDerivedNode : AliasBaseNode
    {
        static UnregisteredAliasDerivedNode()
        {
            CssRegistry.RegisterProperty<UnregisteredAliasDerivedNode, double>(
                "alias-tone",
                DerivedToneProperty,
                CssValueConverters.ParseLength);
        }

        internal static readonly Property<double> DerivedToneProperty =
            Property.Register<UnregisteredAliasDerivedNode, double>("DerivedTone", 7);
    }

    private class ShadowTagBaseNode : UIElement
    {
        static ShadowTagBaseNode()
        {
            CssRegistry.RegisterElement<ShadowTagBaseNode>("shadow-tag");
            CssRegistry.RegisterProperty<ShadowTagBaseNode, double>(
                "shadow-tone",
                BaseToneProperty,
                CssValueConverters.ParseLength);
        }

        internal static readonly Property<double> BaseToneProperty =
            Property.Register<ShadowTagBaseNode, double>("BaseTone", 9);
    }

    private sealed class ShadowTagDerivedNode : ShadowTagBaseNode
    {
        static ShadowTagDerivedNode()
        {
            CssRegistry.RegisterElement<ShadowTagDerivedNode>("shadow-tag");
            CssRegistry.RegisterProperty<ShadowTagDerivedNode, double>(
                "shadow-tone",
                DerivedToneProperty,
                CssValueConverters.ParseLength);
        }

        internal static readonly Property<double> DerivedToneProperty =
            Property.Register<ShadowTagDerivedNode, double>("DerivedTone", 0);
    }

    private sealed class CaseSensitiveNode : UIElement
    {
        static CaseSensitiveNode()
        {
            CssRegistry.RegisterElement<CaseSensitiveNode>("case-tag");
            CssRegistry.RegisterProperty<CaseSensitiveNode, double>(
                "case-tag",
                LevelProperty,
                CssValueConverters.ParseLength);
        }

        internal static readonly Property<double> LevelProperty =
            Property.Register<CaseSensitiveNode, double>("Level", 0);
    }

    private sealed class InvalidElementNode : UIElement
    {
    }

    private sealed class DuplicateElementNode : UIElement
    {
    }

    private sealed class LateRegistrationNode : UIElement
    {
        internal static readonly Property<double> ValueProperty =
            Property.Register<LateRegistrationNode, double>("Value", 0);
    }

    private sealed class LazyAliasNode : UIElement
    {
        static LazyAliasNode()
        {
            LazyAliasInitialized = true;
            CssRegistry.RegisterElement<LazyAliasNode>("lazy-tag");
            CssRegistry.RegisterProperty<LazyAliasNode, double>(
                "lazy-alias-tone", OpacityProperty, CssValueConverters.ParseNumber);
        }
    }
}