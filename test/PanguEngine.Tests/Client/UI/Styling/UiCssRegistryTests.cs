using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Styling;

using PanguEngine.ComponentModel;

namespace PanguEngine.Tests.Client.UI.Styling;

[Collection(UiToolkitCollection.Name)]
public sealed class UiCssRegistryTests
{
    private static bool LazyAliasInitialized;

    [Fact]
    public void PropertiesWithoutCssDefinitionAreNotExposed()
    {
        _ = new Text();
        _ = new Button();
        _ = new ImageView();

        Assert.Null(UiCssRegistry.FindProperty(typeof(Text), "content"));
        Assert.Null(UiCssRegistry.FindProperty(typeof(Button), "text"));
        Assert.Null(UiCssRegistry.FindProperty(typeof(ImageView), "source"));
        Assert.Null(UiCssRegistry.FindProperty(typeof(UiNode), "not-a-css-property"));
    }

    [Fact]
    public void SimpleRegistrationExposesNameTargetAndConvertedSetters()
    {
        UiCssRegistry.RegisterProperty<SimpleCssNode, double>(
            "simple-css-length",
            SimpleCssNode.ValueProperty,
            UiCssValueConverters.ParseLength);

        var css = UiCssRegistry.FindProperty(typeof(SimpleCssNode), "SIMPLE-CSS-LENGTH")!;

        Assert.Equal("simple-css-length", css.Name);
        Assert.Equal(typeof(SimpleCssNode), css.TargetType);
        Assert.Same(css, UiCssRegistry.FindProperty(typeof(SimpleCssNode), "simple-css-length"));

        var setter = Assert.Single(css.Convert("4px"));

        Assert.Same(SimpleCssNode.ValueProperty, setter.Property);
        Assert.Null(setter.Component);
        Assert.Equal(4d, (double)setter.BoxedValue!);
    }

    [Fact]
    public void FindPropertyReturnsNearestInheritedDefinitionAndIgnoresDerivedWithoutDefinition()
    {
        UiCssRegistry.RegisterProperty<FindBaseNode, double>(
            "find-base-tone",
            FindBaseNode.ValueProperty,
            UiCssValueConverters.ParseLength);
        UiCssRegistry.RegisterProperty<FindDerivedNode, double>(
            "find-derived-tone",
            FindDerivedNode.ValueProperty,
            UiCssValueConverters.ParseLength);

        var baseCss = UiCssRegistry.FindProperty(typeof(FindDerivedNode), "find-base-tone");
        var derivedCss = UiCssRegistry.FindProperty(typeof(FindDerivedNode), "FIND-DERIVED-TONE");

        Assert.Same(UiCssRegistry.FindProperty(typeof(FindBaseNode), "find-base-tone"), baseCss);
        Assert.Same(derivedCss, UiCssRegistry.FindProperty(typeof(FindGrandchildNode), "find-derived-tone"));
        Assert.Same(baseCss, UiCssRegistry.FindProperty(typeof(FindGrandchildNode), "find-base-tone"));
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

        var resolver = new UiStyleResolver([], [UiStyleSheet.Parse("BaseAliasTag { alias-tone: 4; }")]);
        var snapshot = resolver.Resolve(node);

        Assert.Equal(4d, snapshot.GetValue(AliasBaseNode.BaseToneProperty));
        Assert.Equal(7d, snapshot.GetValue(UnregisteredAliasDerivedNode.DerivedToneProperty));
    }

    [Fact]
    public void SameElementAliasNearestInChainWins()
    {
        var resolver = new UiStyleResolver([], [UiStyleSheet.Parse("shadow-tag { shadow-tone: 3; }")]);
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
        Assert.NotNull(UiCssRegistry.FindProperty(typeof(CaseSensitiveNode), "CASE-TAG"));
    }

    [Fact]
    public void InvalidAndDuplicateElementRegistrationsAreRejectedWithoutPublishing()
    {
        Assert.Throws<ArgumentException>(() => UiCssRegistry.RegisterElement<InvalidElementNode>("9bad"));
        Assert.True(SingleSelector(nameof(InvalidElementNode)).Matches(new InvalidElementNode()));

        UiCssRegistry.RegisterElement<DuplicateElementNode>("dup-tag");
        Assert.Throws<InvalidOperationException>(() => UiCssRegistry.RegisterElement<DuplicateElementNode>("dup-tag"));
        Assert.Throws<InvalidOperationException>(() =>
            UiCssRegistry.RegisterElement<DuplicateElementNode>("other-tag"));

        Assert.True(SingleSelector("dup-tag").Matches(new DuplicateElementNode()));
        Assert.False(SingleSelector("other-tag").Matches(new DuplicateElementNode()));
    }

    [Fact]
    public void PublishedBindingCacheRefreshesAfterLateElementAndPropertyRegistration()
    {
        var resolver = new UiStyleResolver([], [UiStyleSheet.Parse("late-tag { opacity: 0.2; late-prop: 4; }")]);
        var node = new LateRegistrationNode();
        Assert.Equal(1d, resolver.Resolve(node).GetValue(UiNode.OpacityProperty));

        UiCssRegistry.RegisterElement<LateRegistrationNode>("late-tag");
        UiCssRegistry.RegisterProperty<LateRegistrationNode, double>(
            "late-prop",
            LateRegistrationNode.ValueProperty,
            UiCssValueConverters.ParseLength);

        var snapshot = resolver.Resolve(node);
        Assert.Equal(0.2, snapshot.GetValue(UiNode.OpacityProperty));
        Assert.Equal(4d, snapshot.GetValue(LateRegistrationNode.ValueProperty));
    }

    [Fact]
    public void ParsingTypeLookupAndBindingDoNotInitializeCssDefinitions()
    {
        LazyAliasInitialized = false;

        _ = UiStyleSheet.Parse("lazy-tag { }");
        Assert.False(LazyAliasInitialized);

        var selector = SingleSelector("lazy-tag");
        Assert.Null(selector.MatchTargetType(typeof(LazyAliasNode)));
        Assert.False(LazyAliasInitialized);
        Assert.Null(UiCssRegistry.FindProperty(typeof(LazyAliasNode), "lazy-alias-tone"));
        Assert.False(LazyAliasInitialized);
        var rule = Assert.Single(UiStyleSheet.Parse("lazy-tag { lazy-alias-tone: 0.4; }").Rules);
        Assert.Empty(rule.Bind(typeof(LazyAliasNode)));
        Assert.False(LazyAliasInitialized);

        var node = new LazyAliasNode();
        Assert.True(LazyAliasInitialized);
        Assert.True(selector.Matches(node));
        Assert.NotNull(UiCssRegistry.FindProperty(node.GetType(), "lazy-alias-tone"));
        Assert.Equal(0.4, Assert.Single(rule.Bind(node.GetType())).Setter.BoxedValue);
    }

    [Fact]
    public void DuplicateNameForTargetIsRejectedWithoutPublishingHalfRegistration()
    {
        var name = $"dup-{Guid.NewGuid():N}";
        UiCssRegistry.RegisterProperty<DupCssNode, double>(
            name,
            DupCssNode.FirstProperty,
            UiCssValueConverters.ParseLength);

        Assert.Throws<InvalidOperationException>(() =>
            UiCssRegistry.RegisterProperty<DupCssNode, double>(
                name,
                DupCssNode.SecondProperty,
                UiCssValueConverters.ParseLength));

        Assert.Same(
            DupCssNode.FirstProperty,
            Assert.Single(UiCssRegistry.FindProperty(typeof(DupCssNode), name)!.Convert("1")).Property);
    }

    [Fact]
    public void InvalidNameReadOnlyAndWrongTargetAreRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            UiCssRegistry.RegisterProperty<RejectCssNode, double>(
                "9invalid",
                Property.Register<RejectCssNode, double>("Nine", 0),
                UiCssValueConverters.ParseLength));

        var readOnly = Property.RegisterReadOnly<RejectCssNode, double>("Value", 0).Property;
        Assert.Throws<ArgumentException>(() =>
            UiCssRegistry.RegisterProperty<RejectCssNode, double>(
                "readonly-css",
                readOnly,
                UiCssValueConverters.ParseLength));

        var foreign = Property.Register<ForeignOwnerNode, double>("Value", 0);
        Assert.Throws<ArgumentException>(() =>
            UiCssRegistry.RegisterProperty<RejectCssNode, double>(
                "foreign-css",
                foreign,
                UiCssValueConverters.ParseLength));
    }

    [Fact]
    public void AttachedPropertyRegistrationResolvesOnItsTargetType()
    {
        UiCssRegistry.RegisterProperty<AttachedCssTarget, double>(
            "attached-css-style",
            AttachedCssOwner.ValueProperty,
            UiCssValueConverters.ParseLength);

        var css = UiCssRegistry.FindProperty(typeof(AttachedCssTarget), "attached-css-style")!;

        Assert.Equal(typeof(AttachedCssTarget), css.TargetType);
        Assert.Same(AttachedCssOwner.ValueProperty, Assert.Single(css.Convert("12")).Property);
    }

    [Fact]
    public void ExpandRegistrationReturnsTypedEdgesAndBindsOriginalDeclarationIndex()
    {
        var node = new ExpandCssNode();
        var css = UiCssRegistry.FindProperty(node.GetType(), "expand-sides")!;

        Assert.Equal(
            [UiStyleEdge.Left, UiStyleEdge.Right],
            css.Convert("5").Select(setter => setter.Component));

        var rule = Assert.Single(UiStyleSheet.Parse("ExpandCssNode { expand-sides: 2; expand-sides: 3; }").Rules);
        var bound = rule.Bind(node.GetType());

        Assert.Equal([0, 0, 1, 1], bound.Select(declaration => declaration.DeclarationIndex));
        Assert.All(bound, declaration => Assert.Equal("expand-sides", declaration.CssPropertyName));
        Assert.Equal(
            [UiStyleEdge.Left, UiStyleEdge.Right, UiStyleEdge.Left, UiStyleEdge.Right],
            bound.Select(declaration => declaration.Setter.Component));
    }

    [Fact]
    public void ExpandRegistrationRejectsDuplicateComponentAtBind()
    {
        var node = new ExpandCssNode();
        var rule = Assert.Single(UiStyleSheet.Parse("ExpandCssNode { expand-duplicate: 1; }").Rules);

        var error = Assert.Throws<UiStyleParseException>(() => rule.Bind(node.GetType()));

        Assert.Equal(UiStyleParseError.InvalidValue, error.Error);
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
        var rule = Assert.Single(UiStyleSheet.Parse("Panel:hover { padding-top: 4; }").Rules);

        var declaration = Assert.Single(rule.Bind(new Panel().GetType()));
        Assert.Same(Region.PaddingProperty, declaration.Setter.Property);
        Assert.Equal<UiStyleEdge?>(UiStyleEdge.Top, declaration.Setter.Component);
    }

    [Fact]
    public void DerivedLookupUsesLaterBaseDefinitions()
    {
        UiCssRegistry.RegisterProperty<RegistrationBoundaryDerivedA, double>(
            "boundary-a",
            RegistrationBoundaryDerivedA.ValueProperty,
            UiCssValueConverters.ParseLength);
        var resolver = new UiStyleResolver([], [UiStyleSheet.Parse("* { boundary-base: 3; boundary-a: 4; }")]);
        var node = new RegistrationBoundaryDerivedA();
        Assert.Equal(0d, resolver.Resolve(node).GetValue(RegistrationBoundaryBase.ValueProperty));

        UiCssRegistry.RegisterProperty<RegistrationBoundaryBase, double>(
            "boundary-base",
            RegistrationBoundaryBase.ValueProperty,
            UiCssValueConverters.ParseLength);

        var snapshot = resolver.Resolve(node);
        Assert.Equal(3d, snapshot.GetValue(RegistrationBoundaryBase.ValueProperty));
        Assert.Equal(4d, snapshot.GetValue(RegistrationBoundaryDerivedA.ValueProperty));
    }

    [Fact]
    public void BoundResultSurvivesConverterListMutation()
    {
        var node = new ExpandMutableNode();
        var resolver = new UiStyleResolver([], [UiStyleSheet.Parse("ExpandMutableNode { mutable-sides: 5; }")]);
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
        Assert.Equal(3d, UiCssValueConverters.ParseLength("3"));
        Assert.Equal(3d, UiCssValueConverters.ParseLength("3px"));
        Assert.Equal(0.5, UiCssValueConverters.ParseNumber("0.5"));
        Assert.Throws<FormatException>(() => UiCssValueConverters.ParseNumber("3px"));
    }

    [Fact]
    public void ParseThicknessSupportsOneToFourValuesAndRejectsOtherCounts()
    {
        Assert.Equal(new Thickness(5), UiCssValueConverters.ParseThickness("5"));
        Assert.Equal(new Thickness(2, 5, 2, 5), UiCssValueConverters.ParseThickness("5 2"));
        Assert.Equal(new Thickness(2, 5, 2, 8), UiCssValueConverters.ParseThickness("5 2 8"));
        Assert.Equal(new Thickness(4, 1, 2, 3), UiCssValueConverters.ParseThickness("1 2 3 4"));
        Assert.Throws<FormatException>(() => UiCssValueConverters.ParseThickness("1 2 3 4 5"));
    }

    private static UiStyleSelector SingleSelector(string name) =>
        Assert.Single(UiStyleSheet.Parse($"{name} {{ }}").Rules).Selectors[0];

    private static void AssertBindInvalid(string css)
    {
        var rule = Assert.Single(UiStyleSheet.Parse(css).Rules);

        var node = new ExpandRejectNode();
        var error = Assert.Throws<UiStyleParseException>(() => rule.Bind(node.GetType()));

        Assert.Equal(UiStyleParseError.InvalidValue, error.Error);
        Assert.IsType<ArgumentException>(error.InnerException);
    }

    private sealed class SimpleCssNode : UiNode
    {
        internal static readonly Property<double> ValueProperty =
            Property.Register<SimpleCssNode, double>("Value", 0);
    }

    private class FindBaseNode : UiNode
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

    private sealed class DupCssNode : UiNode
    {
        internal static readonly Property<double> FirstProperty =
            Property.Register<DupCssNode, double>("First", 0);

        internal static readonly Property<double> SecondProperty =
            Property.Register<DupCssNode, double>("Second", 0);
    }

    private sealed class RejectCssNode : UiNode
    {
    }

    private sealed class ForeignOwnerNode : UiNode
    {
    }

    private sealed class AttachedCssOwner : UiNode
    {
        internal static readonly Property<double> ValueProperty =
            Property.RegisterAttached<AttachedCssOwner, AttachedCssTarget, double>("Value", 0);
    }

    private sealed class AttachedCssTarget : UiNode
    {
    }

    private sealed class ExpandCssNode : UiNode
    {
        static ExpandCssNode()
        {
            UiCssRegistry.RegisterProperty<ExpandCssNode>("expand-sides", value =>
            {
                var edge = UiCssValueConverters.ParseLength(value);
                return
                [
                    UiStyleSetter.CreateEdge(PaddingProperty, UiStyleEdge.Left, edge),
                    UiStyleSetter.CreateEdge(PaddingProperty, UiStyleEdge.Right, edge)
                ];
            });

            UiCssRegistry.RegisterProperty<ExpandCssNode>("expand-duplicate", value =>
            [
                UiStyleSetter.Create(PaddingProperty, new Thickness(UiCssValueConverters.ParseLength(value))),
                UiStyleSetter.CreateEdge(PaddingProperty, UiStyleEdge.Left, UiCssValueConverters.ParseLength(value))
            ]);
        }

        internal static readonly Property<Thickness> PaddingProperty =
            Property.Register<ExpandCssNode, Thickness>("Padding", Thickness.Zero,
                onChanged: static (node, _, _) => node.InvalidateMeasure());
    }

    private class RegistrationBoundaryBase : UiNode
    {
        internal static readonly Property<double> ValueProperty =
            Property.Register<RegistrationBoundaryBase, double>("Value", 0);
    }

    private sealed class RegistrationBoundaryDerivedA : RegistrationBoundaryBase
    {
        internal new static readonly Property<double> ValueProperty =
            Property.Register<RegistrationBoundaryDerivedA, double>("Value", 0);
    }

    private sealed class ExpandRejectNode : UiNode
    {
        static ExpandRejectNode()
        {
            UiCssRegistry.RegisterProperty<ExpandRejectNode>("reject-readonly", _ =>
                new[] { UiStyleSetter.Create(ReadOnlyProperty, 1) });
            UiCssRegistry.RegisterProperty<ExpandRejectNode>("reject-foreign", _ =>
                new[] { UiStyleSetter.Create(ExpandForeignOwner.ValueProperty, 1d) });
        }

        internal static readonly Property<int> ReadOnlyProperty =
            Property.RegisterReadOnly<ExpandRejectNode, int>("ReadOnly", 0).Property;
    }

    private sealed class ExpandForeignOwner : UiNode
    {
        internal static readonly Property<double> ValueProperty =
            Property.Register<ExpandForeignOwner, double>("Value", 0);
    }

    private sealed class ExpandMutableNode : UiNode
    {
        static ExpandMutableNode()
        {
            UiCssRegistry.RegisterProperty<ExpandMutableNode>("mutable-sides", value =>
            {
                var edge = UiCssValueConverters.ParseLength(value);
                Output.Clear();
                Output.Add(UiStyleSetter.CreateEdge(PaddingProperty, UiStyleEdge.Left, edge));
                Output.Add(UiStyleSetter.CreateEdge(PaddingProperty, UiStyleEdge.Right, edge));
                return Output;
            });
        }

        internal static readonly List<UiStyleSetter> Output = [];

        internal static readonly Property<Thickness> PaddingProperty =
            Property.Register<ExpandMutableNode, Thickness>("Padding", Thickness.Zero,
                onChanged: static (node, _, _) => node.InvalidateMeasure());
    }

    private sealed class FallbackNameNode : UiNode
    {
    }

    private sealed class AliasedTagNode : UiNode
    {
        static AliasedTagNode()
        {
            UiCssRegistry.RegisterElement<AliasedTagNode>("AliasedTag");
        }
    }

    private class AliasBaseNode : UiNode
    {
        static AliasBaseNode()
        {
            UiCssRegistry.RegisterElement<AliasBaseNode>("BaseAliasTag");
            UiCssRegistry.RegisterProperty<AliasBaseNode, double>(
                "alias-tone",
                BaseToneProperty,
                UiCssValueConverters.ParseLength);
        }

        internal static readonly Property<double> BaseToneProperty =
            Property.Register<AliasBaseNode, double>("BaseTone", 0);
    }

    private sealed class UnregisteredAliasDerivedNode : AliasBaseNode
    {
        static UnregisteredAliasDerivedNode()
        {
            UiCssRegistry.RegisterProperty<UnregisteredAliasDerivedNode, double>(
                "alias-tone",
                DerivedToneProperty,
                UiCssValueConverters.ParseLength);
        }

        internal static readonly Property<double> DerivedToneProperty =
            Property.Register<UnregisteredAliasDerivedNode, double>("DerivedTone", 7);
    }

    private class ShadowTagBaseNode : UiNode
    {
        static ShadowTagBaseNode()
        {
            UiCssRegistry.RegisterElement<ShadowTagBaseNode>("shadow-tag");
            UiCssRegistry.RegisterProperty<ShadowTagBaseNode, double>(
                "shadow-tone",
                BaseToneProperty,
                UiCssValueConverters.ParseLength);
        }

        internal static readonly Property<double> BaseToneProperty =
            Property.Register<ShadowTagBaseNode, double>("BaseTone", 9);
    }

    private sealed class ShadowTagDerivedNode : ShadowTagBaseNode
    {
        static ShadowTagDerivedNode()
        {
            UiCssRegistry.RegisterElement<ShadowTagDerivedNode>("shadow-tag");
            UiCssRegistry.RegisterProperty<ShadowTagDerivedNode, double>(
                "shadow-tone",
                DerivedToneProperty,
                UiCssValueConverters.ParseLength);
        }

        internal static readonly Property<double> DerivedToneProperty =
            Property.Register<ShadowTagDerivedNode, double>("DerivedTone", 0);
    }

    private sealed class CaseSensitiveNode : UiNode
    {
        static CaseSensitiveNode()
        {
            UiCssRegistry.RegisterElement<CaseSensitiveNode>("case-tag");
            UiCssRegistry.RegisterProperty<CaseSensitiveNode, double>(
                "case-tag",
                LevelProperty,
                UiCssValueConverters.ParseLength);
        }

        internal static readonly Property<double> LevelProperty =
            Property.Register<CaseSensitiveNode, double>("Level", 0);
    }

    private sealed class InvalidElementNode : UiNode
    {
    }

    private sealed class DuplicateElementNode : UiNode
    {
    }

    private sealed class LateRegistrationNode : UiNode
    {
        internal static readonly Property<double> ValueProperty =
            Property.Register<LateRegistrationNode, double>("Value", 0);
    }

    private sealed class LazyAliasNode : UiNode
    {
        static LazyAliasNode()
        {
            LazyAliasInitialized = true;
            UiCssRegistry.RegisterElement<LazyAliasNode>("lazy-tag");
            UiCssRegistry.RegisterProperty<LazyAliasNode, double>(
                "lazy-alias-tone", UiNode.OpacityProperty, UiCssValueConverters.ParseNumber);
        }
    }
}
