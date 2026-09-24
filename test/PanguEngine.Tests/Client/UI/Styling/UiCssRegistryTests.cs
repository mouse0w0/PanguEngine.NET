using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Styling;

namespace PanguEngine.Tests.Client.UI.Styling;

public sealed class UiCssRegistryTests
{
    private static bool LazyAliasInitialized;

    [Fact]
    public void BuiltInCssNamesResolveToRegisteredProperties()
    {
        Assert.Equal("width", UiCssRegistry.FindProperty(typeof(UiNode), "WIDTH")!.Name);
        Assert.Same(
            UiNode.WidthProperty,
            Assert.Single(UiCssRegistry.FindProperty(typeof(UiNode), "width")!.Convert("10px")).Property);
        Assert.Same(
            UiNode.MarginProperty,
            Assert.Single(UiCssRegistry.FindProperty(typeof(UiNode), "margin")!.Convert("1 2")).Property);
        Assert.Same(
            UiNode.MarginProperty,
            Assert.Single(UiCssRegistry.FindProperty(typeof(UiNode), "margin-left")!.Convert("4")).Property);
        Assert.Same(
            Region.PaddingProperty,
            Assert.Single(UiCssRegistry.FindProperty(typeof(Region), "padding")!.Convert("1 2 3 4")).Property);
        Assert.Same(
            Region.PaddingProperty,
            Assert.Single(UiCssRegistry.FindProperty(typeof(Panel), "padding-top")!.Convert("4")).Property);
        Assert.Same(
            Region.BackgroundProperty,
            Assert.Single(UiCssRegistry.FindProperty(typeof(Panel), "background-color")!.Convert("#010203")).Property);
        Assert.Same(
            StackPanel.OrientationProperty,
            Assert.Single(UiCssRegistry.FindProperty(typeof(StackPanel), "orientation")!.Convert("horizontal")).Property);
        Assert.Same(
            Text.FontSizeProperty,
            Assert.Single(UiCssRegistry.FindProperty(typeof(Text), "font-size")!.Convert("12")).Property);
        Assert.Same(
            ImageView.StretchProperty,
            Assert.Single(UiCssRegistry.FindProperty(typeof(ImageView), "stretch")!.Convert("uniform")).Property);
        Assert.Same(
            Button.ForegroundProperty,
            Assert.Single(UiCssRegistry.FindProperty(typeof(Button), "foreground")!.Convert("#f2f4f7")).Property);
        Assert.Same(
            Canvas.LeftProperty,
            Assert.Single(UiCssRegistry.FindProperty(typeof(UiNode), "left")!.Convert("4")).Property);
    }

    [Fact]
    public void PropertiesWithoutCssDefinitionAreNotExposed()
    {
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
        Assert.Throws<InvalidOperationException>(() => UiCssRegistry.RegisterElement<DuplicateElementNode>("other-tag"));

        Assert.True(SingleSelector("dup-tag").Matches(new DuplicateElementNode()));
        Assert.False(SingleSelector("other-tag").Matches(new DuplicateElementNode()));
    }

    [Fact]
    public void RegisteringOnFrozenTypeRejectsElementsAndProperties()
    {
        UiCssRegistry.RegisterProperty<FrozenRegistrationNode, double>(
            "frozen-reg-seed",
            FrozenRegistrationNode.FirstProperty,
            UiCssValueConverters.ParseLength);

        Assert.NotNull(UiCssRegistry.FindProperty(typeof(FrozenRegistrationNode), "frozen-reg-seed"));

        Assert.Throws<InvalidOperationException>(() =>
            UiCssRegistry.RegisterElement<FrozenRegistrationNode>("frozen-reg-tag"));
        Assert.Throws<InvalidOperationException>(() =>
            UiCssRegistry.RegisterProperty<FrozenRegistrationNode, double>(
                "frozen-reg-extra",
                FrozenRegistrationNode.SecondProperty,
                UiCssValueConverters.ParseLength));
    }

    [Fact]
    public void PublishedBindingCacheFreezesLateElementAndPropertyRegistration()
    {
        var resolver = new UiStyleResolver([], [UiStyleSheet.Parse("Ghost { opacity: 0.2; }")]);
        _ = resolver.Resolve(new LateRegistrationNode());

        Assert.Throws<InvalidOperationException>(() =>
            UiCssRegistry.RegisterElement<LateRegistrationNode>("late-tag"));
        Assert.Throws<InvalidOperationException>(() =>
            UiCssRegistry.RegisterProperty<LateRegistrationNode, double>(
                "late-prop",
                LateRegistrationNode.ValueProperty,
                UiCssValueConverters.ParseLength));
    }

    [Fact]
    public void StaticConstructorAliasRunsOnFirstSelectorMatchNotOnParse()
    {
        LazyAliasInitialized = false;

        _ = UiStyleSheet.Parse("lazy-tag { }");
        Assert.False(LazyAliasInitialized);

        var selector = SingleSelector("lazy-tag");
        Assert.Equal(typeof(LazyAliasNode), selector.MatchTargetType(typeof(LazyAliasNode)));
        Assert.True(LazyAliasInitialized);
    }

    [Fact]
    public void EmptyAndCSharpOnlySheetsAlsoFreezeRegistrationsBeforeCaching()
    {
        VerifyFrozen(new UiStyleResolver([], []), new EmptyCacheNode());
        var csharpRule = new UiStyleRule(
            UiStyleSelector.For<CSharpCacheNode>(),
            [UiStyleSetter.Create(UiNode.OpacityProperty, 0.5)]);
        VerifyFrozen(new UiStyleResolver([], [new UiStyleSheet([csharpRule])]), new CSharpCacheNode());

        static void VerifyFrozen<TNode>(UiStyleResolver resolver, TNode node) where TNode : UiNode
        {
            _ = resolver.Resolve(node);
            Assert.Throws<InvalidOperationException>(() => UiCssRegistry.RegisterElement<TNode>("late-tag"));
            Assert.Throws<InvalidOperationException>(() => UiCssRegistry.RegisterProperty<TNode, double>(
                "late-value", UiNode.OpacityProperty, UiCssValueConverters.ParseNumber));
        }
    }

    [Fact]
    public void StronglyTypedSelectorKeepsClrTargetAndTypeNameRegardlessOfAlias()
    {
        var selector = UiStyleSelector.For<CSharpAliasNode>();

        Assert.Equal(typeof(CSharpAliasNode), selector.TargetType);
        Assert.Equal(nameof(CSharpAliasNode), selector.TypeName);
        Assert.True(selector.Matches(new CSharpAliasNode()));
    }

    [Fact]
    public void AppendingRegistrationToFrozenTypeIsRejected()
    {
        UiCssRegistry.RegisterProperty<FrozenCssNode, double>(
            "frozen-seed",
            FrozenCssNode.FirstProperty,
            UiCssValueConverters.ParseLength);
        Assert.NotNull(UiCssRegistry.FindProperty(typeof(FrozenCssNode), "frozen-seed"));

        Assert.Throws<InvalidOperationException>(() =>
            UiCssRegistry.RegisterProperty<FrozenCssNode, double>(
                "frozen-extra",
                FrozenCssNode.SecondProperty,
                UiCssValueConverters.ParseLength));
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
    public void InvalidNameReadOnlyInputAndWrongTargetAreRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            UiCssRegistry.RegisterProperty<RejectCssNode, double>(
                "9invalid",
                UiProperty.Register<RejectCssNode, double>("Nine", 0),
                UiCssValueConverters.ParseLength));

        var readOnly = UiProperty.RegisterReadOnly<RejectCssNode, double>("Value", 0).Property;
        Assert.Throws<ArgumentException>(() =>
            UiCssRegistry.RegisterProperty<RejectCssNode, double>(
                "readonly-css",
                readOnly,
                UiCssValueConverters.ParseLength));

        var input = UiProperty.Register<RejectCssNode, double>(
            "Input",
            0,
            UiPropertyInvalidation.Input);
        Assert.Throws<ArgumentException>(() =>
            UiCssRegistry.RegisterProperty<RejectCssNode, double>(
                "input-css",
                input,
                UiCssValueConverters.ParseLength));

        var foreign = UiProperty.Register<ForeignOwnerNode, double>("Value", 0);
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
        var css = UiCssRegistry.FindProperty(typeof(ExpandCssNode), "expand-sides")!;

        Assert.Equal(
            [UiStyleEdge.Left, UiStyleEdge.Right],
            css.Convert("5").Select(setter => setter.Component));

        var rule = Assert.Single(UiStyleSheet.Parse("ExpandCssNode { expand-sides: 2; expand-sides: 3; }").Rules);
        var bound = rule.Bind(typeof(ExpandCssNode));

        Assert.Equal([0, 0, 1, 1], bound.Select(declaration => declaration.DeclarationIndex));
        Assert.All(bound, declaration => Assert.Equal("expand-sides", declaration.CssPropertyName));
        Assert.Equal(
            [UiStyleEdge.Left, UiStyleEdge.Right, UiStyleEdge.Left, UiStyleEdge.Right],
            bound.Select(declaration => declaration.Setter.Component));
    }

    [Fact]
    public void ExpandRegistrationRejectsDuplicateComponentAtBind()
    {
        var rule = Assert.Single(UiStyleSheet.Parse("ExpandCssNode { expand-duplicate: 1; }").Rules);

        var error = Assert.Throws<UiStyleParseException>(() => rule.Bind(typeof(ExpandCssNode)));

        Assert.Equal(UiStyleParseError.InvalidValue, error.Error);
    }

    [Fact]
    public void CreateEdgeCarriesComponentAndThicknessBoxedValue()
    {
        var setter = UiStyleSetter.CreateEdge(Region.PaddingProperty, UiStyleEdge.Left, 5);

        Assert.Equal<UiStyleEdge?>(UiStyleEdge.Left, setter.Component);
        Assert.Same(Region.PaddingProperty, setter.Property);
        Assert.IsType<Thickness>(setter.BoxedValue);
    }

    [Fact]
    public void ExpandRegistrationRejectsReadOnlyInputAndForeignOutputAtBind()
    {
        AssertBindInvalid("ExpandRejectNode { reject-readonly: 1; }");
        AssertBindInvalid("ExpandRejectNode { reject-input: 1; }");
        AssertBindInvalid("ExpandRejectNode { reject-foreign: 1; }");
    }

    [Fact]
    public void PseudoClassRuleRejectsExpandedLayoutOutputAtBind()
    {
        var rule = Assert.Single(UiStyleSheet.Parse("Panel:hover { padding-top: 4; }").Rules);

        var error = Assert.Throws<UiStyleParseException>(() => rule.Bind(typeof(Panel)));

        Assert.Equal(UiStyleParseError.InvalidValue, error.Error);
        Assert.IsType<ArgumentException>(error.InnerException);
    }

    [Fact]
    public void FrozenBaseStillAllowsRegisteringAnotherDerivedType()
    {
        UiCssRegistry.RegisterProperty<FrozenBoundaryDerivedA, double>(
            "boundary-a",
            FrozenBoundaryDerivedA.ValueProperty,
            UiCssValueConverters.ParseLength);
        Assert.NotNull(UiCssRegistry.FindProperty(typeof(FrozenBoundaryDerivedA), "boundary-a"));

        Assert.Throws<InvalidOperationException>(() =>
            UiCssRegistry.RegisterProperty<FrozenBoundaryBase, double>(
                "boundary-base",
                FrozenBoundaryBase.ValueProperty,
                UiCssValueConverters.ParseLength));

        UiCssRegistry.RegisterProperty<FrozenBoundaryDerivedB, double>(
            "boundary-b",
            FrozenBoundaryDerivedB.ValueProperty,
            UiCssValueConverters.ParseLength);
        UiCssRegistry.RegisterElement<FrozenBoundaryDerivedB>("boundary-b-tag");
        Assert.NotNull(UiCssRegistry.FindProperty(typeof(FrozenBoundaryDerivedB), "boundary-b"));
    }

    [Fact]
    public void ConvertReturnsACopyAndBoundResultSurvivesConverterListMutation()
    {
        var definition = UiCssRegistry.FindProperty(typeof(ExpandMutableNode), "mutable-sides")!;

        var first = definition.Convert("5");
        var second = definition.Convert("5");
        Assert.NotSame(first, second);
        Assert.Equal(2, first.Count);

        var resolver = new UiStyleResolver([], [UiStyleSheet.Parse("ExpandMutableNode { mutable-sides: 5; }")]);
        Assert.Equal(
            new Thickness(5, 0, 5, 0),
            resolver.Resolve(new ExpandMutableNode()).GetValue(ExpandMutableNode.PaddingProperty));

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

        var error = Assert.Throws<UiStyleParseException>(() => rule.Bind(typeof(ExpandRejectNode)));

        Assert.Equal(UiStyleParseError.InvalidValue, error.Error);
        Assert.IsType<ArgumentException>(error.InnerException);
    }

    private sealed class SimpleCssNode : UiNode
    {
        internal static readonly UiProperty<double> ValueProperty =
            UiProperty.Register<SimpleCssNode, double>("Value", 0);
    }

    private class FindBaseNode : UiNode
    {
        internal static readonly UiProperty<double> ValueProperty =
            UiProperty.Register<FindBaseNode, double>("Value", 0);
    }

    private class FindDerivedNode : FindBaseNode
    {
        internal new static readonly UiProperty<double> ValueProperty =
            UiProperty.Register<FindDerivedNode, double>("Value", 1);
    }

    private sealed class FindGrandchildNode : FindDerivedNode
    {
    }

    private sealed class FrozenCssNode : UiNode
    {
        internal static readonly UiProperty<double> FirstProperty =
            UiProperty.Register<FrozenCssNode, double>("First", 0);

        internal static readonly UiProperty<double> SecondProperty =
            UiProperty.Register<FrozenCssNode, double>("Second", 0);
    }

    private sealed class DupCssNode : UiNode
    {
        internal static readonly UiProperty<double> FirstProperty =
            UiProperty.Register<DupCssNode, double>("First", 0);

        internal static readonly UiProperty<double> SecondProperty =
            UiProperty.Register<DupCssNode, double>("Second", 0);
    }

    private sealed class RejectCssNode : UiNode
    {
    }

    private sealed class ForeignOwnerNode : UiNode
    {
    }

    private sealed class AttachedCssOwner : UiNode
    {
        internal static readonly UiProperty<double> ValueProperty =
            UiProperty.RegisterAttached<AttachedCssOwner, AttachedCssTarget, double>("Value", 0);
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

        internal static readonly UiProperty<Thickness> PaddingProperty =
            UiProperty.Register<ExpandCssNode, Thickness>("Padding", Thickness.Zero, UiPropertyInvalidation.Measure);
    }

    private class FrozenBoundaryBase : UiNode
    {
        internal static readonly UiProperty<double> ValueProperty =
            UiProperty.Register<FrozenBoundaryBase, double>("Value", 0);
    }

    private sealed class FrozenBoundaryDerivedA : FrozenBoundaryBase
    {
        internal new static readonly UiProperty<double> ValueProperty =
            UiProperty.Register<FrozenBoundaryDerivedA, double>("Value", 0);
    }

    private sealed class FrozenBoundaryDerivedB : FrozenBoundaryBase
    {
        internal new static readonly UiProperty<double> ValueProperty =
            UiProperty.Register<FrozenBoundaryDerivedB, double>("Value", 0);
    }

    private sealed class ExpandRejectNode : UiNode
    {
        static ExpandRejectNode()
        {
            UiCssRegistry.RegisterProperty<ExpandRejectNode>("reject-readonly", _ =>
                new[] { UiStyleSetter.Create(ReadOnlyProperty, 1) });
            UiCssRegistry.RegisterProperty<ExpandRejectNode>("reject-input", _ =>
                new[] { UiStyleSetter.Create(InputProperty, 1d) });
            UiCssRegistry.RegisterProperty<ExpandRejectNode>("reject-foreign", _ =>
                new[] { UiStyleSetter.Create(ExpandForeignOwner.ValueProperty, 1d) });
        }

        internal static readonly UiProperty<int> ReadOnlyProperty =
            UiProperty.RegisterReadOnly<ExpandRejectNode, int>("ReadOnly", 0).Property;

        internal static readonly UiProperty<double> InputProperty =
            UiProperty.Register<ExpandRejectNode, double>("Input", 0, UiPropertyInvalidation.Input);
    }

    private sealed class ExpandForeignOwner : UiNode
    {
        internal static readonly UiProperty<double> ValueProperty =
            UiProperty.Register<ExpandForeignOwner, double>("Value", 0);
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

        internal static readonly UiProperty<Thickness> PaddingProperty =
            UiProperty.Register<ExpandMutableNode, Thickness>("Padding", Thickness.Zero, UiPropertyInvalidation.Measure);
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

        internal static readonly UiProperty<double> BaseToneProperty =
            UiProperty.Register<AliasBaseNode, double>("BaseTone", 0);
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

        internal static readonly UiProperty<double> DerivedToneProperty =
            UiProperty.Register<UnregisteredAliasDerivedNode, double>("DerivedTone", 7);
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

        internal static readonly UiProperty<double> BaseToneProperty =
            UiProperty.Register<ShadowTagBaseNode, double>("BaseTone", 9);
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

        internal static readonly UiProperty<double> DerivedToneProperty =
            UiProperty.Register<ShadowTagDerivedNode, double>("DerivedTone", 0);
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

        internal static readonly UiProperty<double> LevelProperty =
            UiProperty.Register<CaseSensitiveNode, double>("Level", 0);
    }

    private sealed class InvalidElementNode : UiNode
    {
    }

    private sealed class DuplicateElementNode : UiNode
    {
    }

    private sealed class FrozenRegistrationNode : UiNode
    {
        internal static readonly UiProperty<double> FirstProperty =
            UiProperty.Register<FrozenRegistrationNode, double>("First", 0);

        internal static readonly UiProperty<double> SecondProperty =
            UiProperty.Register<FrozenRegistrationNode, double>("Second", 0);
    }

    private sealed class LateRegistrationNode : UiNode
    {
        internal static readonly UiProperty<double> ValueProperty =
            UiProperty.Register<LateRegistrationNode, double>("Value", 0);
    }

    private sealed class LazyAliasNode : UiNode
    {
        static LazyAliasNode()
        {
            LazyAliasInitialized = true;
            UiCssRegistry.RegisterElement<LazyAliasNode>("lazy-tag");
        }
    }

    private sealed class EmptyCacheNode : UiNode
    {
    }

    private sealed class CSharpCacheNode : UiNode
    {
    }

    private sealed class CSharpAliasNode : UiNode
    {
        static CSharpAliasNode()
        {
            UiCssRegistry.RegisterElement<CSharpAliasNode>("csharp-alias");
        }
    }
}
