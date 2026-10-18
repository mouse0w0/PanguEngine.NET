using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Drawing;
using PanguEngine.Client.UI.Styling;

using PanguEngine.ComponentModel;

namespace PanguEngine.Tests.Client.UI.Styling;

public sealed class StyleComponentsTests
{
    [Fact]
    public void ThreeValuePaddingAndMarginExpandAsTopHorizontalBottom()
    {
        var resolver = new StyleResolver([], [StyleSheet.Parse("Panel { padding: 5 2 8; margin: 1 3 4; }")]);

        var snapshot = resolver.Resolve(new Panel());

        Assert.Equal(new Thickness(2, 5, 2, 8), snapshot.GetValue(Region.PaddingProperty));
        Assert.Equal(new Thickness(3, 1, 3, 4), snapshot.GetValue(Node.MarginProperty));
    }

    [Theory]
    [InlineData("padding")]
    [InlineData("margin")]
    public void ShorthandAndLonghandResolveByDeclarationOrderInBothDirections(string name)
    {
        var property = name == "padding" ? Region.PaddingProperty : Node.MarginProperty;
        var shorthandFirst = new StyleResolver([], [StyleSheet.Parse($"Panel {{ {name}: 4px; {name}-left: 9px; }}")]);
        Assert.Equal(new Thickness(9, 4, 4, 4), shorthandFirst.Resolve(new Panel()).GetValue(property));

        var longhandFirst = new StyleResolver([], [StyleSheet.Parse($"Panel {{ {name}-left: 9px; {name}: 4px; }}")]);
        Assert.Equal(new Thickness(4), longhandFirst.Resolve(new Panel()).GetValue(property));
    }

    [Theory]
    [InlineData("padding")]
    [InlineData("margin")]
    public void LonghandsTargetTheirNamedEdges(string name)
    {
        var property = name == "padding" ? Region.PaddingProperty : Node.MarginProperty;
        var rule = Assert.Single(StyleSheet.Parse(
            $"Panel {{ {name}-top: 1; {name}-right: 2; {name}-bottom: 3; {name}-left: 4; }}").Rules);

        var resolver = new StyleResolver([], [new StyleSheet([rule])]);
        Assert.Equal(new Thickness(4, 1, 2, 3), resolver.Resolve(new Panel()).GetValue(property));
    }

    [Fact]
    public void HigherSpecificityWinsAcrossRulesAndFilesOverDeclarationOrder()
    {
        var node = PanelWithClasses("primary");

        var specificEarlier = new StyleResolver([], [
            StyleSheet.Parse("Panel.primary { padding: 4px; } Panel { padding-left: 9px; }")]);
        Assert.Equal(new Thickness(4), specificEarlier.Resolve(node).GetValue(Region.PaddingProperty));

        var specificLater = new StyleResolver([], [
            StyleSheet.Parse("Panel { padding: 4px; } Panel.primary { padding-left: 9px; }")]);
        Assert.Equal(new Thickness(9, 4, 4, 4), specificLater.Resolve(node).GetValue(Region.PaddingProperty));

        var acrossFiles = new StyleResolver([], [
            StyleSheet.Parse("Panel.primary { padding: 4px; }", "specific.css"),
            StyleSheet.Parse("Panel { padding-left: 9px; }", "later.css")]);
        Assert.Equal(new Thickness(4), acrossFiles.Resolve(node).GetValue(Region.PaddingProperty));
    }

    [Fact]
    public void AuthorSingleEdgeOverridesAllBaseShorthandEdges()
    {
        var resolver = new StyleResolver(
            [StyleSheet.Parse("Panel { padding: 4px; }", "ua.css")],
            [StyleSheet.Parse("Panel { padding-left: 9px; }", "app.css")]);

        var snapshot = resolver.Resolve(new Panel());

        Assert.Equal(new Thickness(9, 4, 4, 4), snapshot.GetValue(Region.PaddingProperty));

        var sources = snapshot.GetSources(Region.PaddingProperty);
        Assert.Equal(
            new[] { StyleEdge.Top, StyleEdge.Right, StyleEdge.Bottom, StyleEdge.Left },
            sources.Select(source => source.Component!.Value));

        Assert.All(sources.Where(source => source.Component != StyleEdge.Left), source =>
        {
            Assert.Equal(StyleOrigin.Base, source.Origin);
            Assert.Equal("ua.css", source.SheetSourceName);
        });

        var left = Assert.Single(sources.Where(source => source.Component == StyleEdge.Left));
        Assert.Equal(StyleOrigin.Author, left.Origin);
        Assert.Equal("app.css", left.SheetSourceName);
        Assert.Equal("padding-left", left.CssPropertyName);
    }

    [Fact]
    public void CSharpFullThicknessAndCssSingleEdgeOverrideEachOther()
    {
        var csharpFull = new StyleRule(
            StyleSelector.For<Panel>(),
            [StyleSetter.Create(Region.PaddingProperty, new Thickness(1, 2, 3, 4))]);
        var csharpSheet = new StyleSheet([csharpFull]);
        var cssEdge = StyleSheet.Parse("Panel { padding-left: 9px; }");

        var cssLater = new StyleResolver([], [csharpSheet, cssEdge]);
        Assert.Equal(new Thickness(9, 2, 3, 4), cssLater.Resolve(new Panel()).GetValue(Region.PaddingProperty));

        var csharpLater = new StyleResolver([], [cssEdge, csharpSheet]);
        Assert.Equal(new Thickness(1, 2, 3, 4), csharpLater.Resolve(new Panel()).GetValue(Region.PaddingProperty));
    }

    [Fact]
    public void CSharpFullAndSingleEdgeCompeteByDeclarationOrder()
    {
        var full = new StyleRule(
            StyleSelector.For<Panel>(),
            [StyleSetter.Create(Region.PaddingProperty, new Thickness(1, 2, 3, 4))]);
        var edge = new StyleRule(
            StyleSelector.For<Panel>(),
            [StyleSetter.CreateEdge(Region.PaddingProperty, StyleEdge.Left, 9)]);

        var edgeLater = new StyleResolver([], [new StyleSheet([full, edge])]);
        Assert.Equal(new Thickness(9, 2, 3, 4), edgeLater.Resolve(new Panel()).GetValue(Region.PaddingProperty));

        var fullLater = new StyleResolver([], [new StyleSheet([edge, full])]);
        Assert.Equal(new Thickness(1, 2, 3, 4), fullLater.Resolve(new Panel()).GetValue(Region.PaddingProperty));
    }

    [Fact]
    public void UndeclaredEdgesFallBackToThePropertyDefault()
    {
        var resolver = new StyleResolver([], [StyleSheet.Parse("Panel { padding-left: 9px; }")]);

        var snapshot = resolver.Resolve(new Panel());

        Assert.Equal(new Thickness(9, 0, 0, 0), snapshot.GetValue(Region.PaddingProperty));

        var source = Assert.Single(snapshot.GetSources(Region.PaddingProperty));
        Assert.Equal(StyleEdge.Left, source.Component!.Value);
        Assert.Equal("padding-left", source.CssPropertyName);
    }

    [Fact]
    public void LocalAndBindingOverrideWholeThicknessAndClearRestoresStyle()
    {
        var node = new Panel();
        var screen = new Screen(node);
        screen.SetStyleSheets([StyleSheet.Parse("Panel { padding: 4px 8px; }")]);
        screen.Root!.UpdateStyles();

        Assert.Equal(new Thickness(8, 4, 8, 4), node.Padding);
        Assert.All(node.GetStyleValueSources(Region.PaddingProperty), source => Assert.False(source.IsMaskedByLocalValue));

        node.Padding = new Thickness(1, 2, 3, 4);
        Assert.Equal(new Thickness(1, 2, 3, 4), node.Padding);
        Assert.All(node.GetStyleValueSources(Region.PaddingProperty), source => Assert.True(source.IsMaskedByLocalValue));

        node.ClearValue(Region.PaddingProperty);
        Assert.Equal(new Thickness(8, 4, 8, 4), node.Padding);
        Assert.All(node.GetStyleValueSources(Region.PaddingProperty), source => Assert.False(source.IsMaskedByLocalValue));

        var source = new Panel { Padding = new Thickness(5) };
        node.Bind(Region.PaddingProperty, source, Region.PaddingProperty);
        source.Padding = new Thickness(6);

        Assert.Equal(new Thickness(6), node.Padding);
        Assert.All(node.GetStyleValueSources(Region.PaddingProperty), styleSource => Assert.True(styleSource.IsMaskedByLocalValue));

        node.Unbind(Region.PaddingProperty);
        source.Padding = new Thickness(7);
        Assert.Equal(new Thickness(6), node.Padding);

        node.ClearValue(Region.PaddingProperty);
        Assert.Equal(new Thickness(8, 4, 8, 4), node.Padding);
    }

    [Fact]
    public void PerEdgeSourcesKeepOriginalDeclarationIndicesAcrossRules()
    {
        var resolver = new StyleResolver([], [StyleSheet.Parse("""
            Panel { padding: 1px; opacity: 0.5; }
            Panel { padding-left: 9px; }
            """, "edges.css")]);

        var snapshot = resolver.Resolve(new Panel());

        var paddingSources = snapshot.GetSources(Region.PaddingProperty);
        Assert.Equal(
            new[] { StyleEdge.Top, StyleEdge.Right, StyleEdge.Bottom, StyleEdge.Left },
            paddingSources.Select(source => source.Component!.Value));

        Assert.All(paddingSources.Where(source => source.Component != StyleEdge.Left), source =>
        {
            Assert.Equal("padding", source.CssPropertyName);
            Assert.Equal(0, source.DeclarationIndex);
            Assert.Equal(0, source.RuleIndex);
        });

        var left = Assert.Single(paddingSources.Where(source => source.Component == StyleEdge.Left));
        Assert.Equal("padding-left", left.CssPropertyName);
        Assert.Equal(2, left.DeclarationIndex);
        Assert.Equal(1, left.RuleIndex);
        Assert.Equal("edges.css", left.SheetSourceName);

        var opacity = Assert.Single(snapshot.GetSources(Node.OpacityProperty));
        Assert.Equal(1, opacity.DeclarationIndex);
        Assert.Equal(0, opacity.RuleIndex);
    }

    [Fact]
    public void StyleApplicationNotifiesOnceAndFailedReplacementKeepsValue()
    {
        var node = new Panel();
        var screen = new Screen(node);
        var notifications = 0;
        node.PropertyChanged += (_, eventArgs) =>
            notifications += ReferenceEquals(eventArgs.Property, Region.PaddingProperty) ? 1 : 0;

        screen.SetStyleSheets([StyleSheet.Parse("Panel { padding: 4px; }")]);
        screen.Root!.UpdateStyles();

        Assert.Equal(1, notifications);
        Assert.Equal(new Thickness(4), node.Padding);

        screen.SetStyleSheets([StyleSheet.Parse("Panel { padding: nope; }")]);
        Assert.Throws<StyleParseException>(screen.Root!.UpdateStyles);

        Assert.Equal(1, notifications);
        Assert.Equal(new Thickness(4), node.Padding);
    }

    [Fact]
    public void OpacityRejectsPixelsWithInvalidValueDiagnostic()
    {
        var rule = Assert.Single(StyleSheet.Parse("Panel { opacity: 1px; }").Rules);
        var node = new Panel();
        var error = Assert.Throws<StyleParseException>(() => rule.Bind(node.GetType()));

        Assert.Equal(StyleParseError.InvalidValue, error.Error);
    }

    [Theory]
    [InlineData("background", "background-color")]
    [InlineData("background-color", "background")]
    public void BackgroundNamesCompeteByDeclarationOrderAndOrigin(string first, string second)
    {
        var sameRule = new StyleResolver([], [StyleSheet.Parse(
            $"Panel {{ {first}: #010203; {second}: #040506; }}")]);

        var snapshot = sameRule.Resolve(new Panel());

        Assert.Equal(new SolidColorBrush(4, 5, 6), snapshot.GetValue(Region.BackgroundProperty));
        Assert.Equal(second, Assert.Single(snapshot.GetSources(Region.BackgroundProperty)).CssPropertyName);

        var acrossOrigins = new StyleResolver(
            [StyleSheet.Parse($"Panel#target {{ {first}: #010203; }}")],
            [StyleSheet.Parse($"Panel {{ {second}: #040506; }}")]);

        snapshot = acrossOrigins.Resolve(new Panel { StyleId = "target" });

        Assert.Equal(new SolidColorBrush(4, 5, 6), snapshot.GetValue(Region.BackgroundProperty));
        var source = Assert.Single(snapshot.GetSources(Region.BackgroundProperty));
        Assert.Equal(StyleOrigin.Author, source.Origin);
        Assert.Equal(second, source.CssPropertyName);
    }

    [Fact]
    public void SameRuleCSharpWholeAndSingleEdgeFollowDeclarationOrder()
    {
        var whole = StyleSetter.Create(Region.PaddingProperty, new Thickness(1, 2, 3, 4));
        var edge = StyleSetter.CreateEdge(Region.PaddingProperty, StyleEdge.Left, 9);

        var edgeLater = new StyleResolver([], [new StyleSheet([
            new StyleRule(StyleSelector.For<Panel>(), [whole, edge])])]);
        Assert.Equal(new Thickness(9, 2, 3, 4), edgeLater.Resolve(new Panel()).GetValue(Region.PaddingProperty));

        var wholeLater = new StyleResolver([], [new StyleSheet([
            new StyleRule(StyleSelector.For<Panel>(), [edge, whole])])]);
        Assert.Equal(new Thickness(1, 2, 3, 4), wholeLater.Resolve(new Panel()).GetValue(Region.PaddingProperty));
    }

    [Fact]
    public void UndeclaredEdgesUseTheNonZeroPropertyDefault()
    {
        var resolver = new StyleResolver([], [StyleSheet.Parse("DefaultThicknessNode { default-pad-left: 9; }")]);

        var snapshot = resolver.Resolve(new DefaultThicknessNode());

        Assert.Equal(new Thickness(9, 7, 7, 7), snapshot.GetValue(DefaultThicknessNode.PaddingProperty));

        var source = Assert.Single(snapshot.GetSources(DefaultThicknessNode.PaddingProperty));
        Assert.Equal(StyleEdge.Left, source.Component!.Value);
        Assert.Equal("default-pad-left", source.CssPropertyName);
    }

    [Fact]
    public void CustomMultiOutputAcrossRulesKeepsFollowingDeclarationIndices()
    {
        var resolver = new StyleResolver([], [StyleSheet.Parse("""
            ExpandIndexNode { index-sides: 2; opacity: 0.5; }
            ExpandIndexNode { index-left: 3; }
            """)]);

        var snapshot = resolver.Resolve(new ExpandIndexNode());

        Assert.Equal(new Thickness(3, 0, 2, 0), snapshot.GetValue(ExpandIndexNode.PaddingProperty));

        var opacity = Assert.Single(snapshot.GetSources(Node.OpacityProperty));
        Assert.Equal(1, opacity.DeclarationIndex);

        var left = Assert.Single(
            snapshot.GetSources(ExpandIndexNode.PaddingProperty).Where(source => source.Component == StyleEdge.Left));
        Assert.Equal(2, left.DeclarationIndex);
        Assert.Equal(1, left.RuleIndex);
    }

    [Fact]
    public void SourceLocationPointsAtTheDeclarationPropertyName()
    {
        var resolver = new StyleResolver([], [StyleSheet.Parse("Panel {\n    padding-left: 9px;\n}", "pos.css")]);

        var source = Assert.Single(resolver.Resolve(new Panel()).GetSources(Region.PaddingProperty));

        Assert.NotNull(source.SourceLocation);
        Assert.Equal("pos.css", source.SourceLocation!.SourceName);
        Assert.Equal(2, source.SourceLocation.Line);
        Assert.Equal(5, source.SourceLocation.Column);
        Assert.Equal("padding-left".Length, source.SourceLocation.Length);
    }

    [Fact]
    public void SourceCollectionIsReadOnly()
    {
        var node = new Panel();
        var screen = new Screen(node);
        screen.SetStyleSheets([StyleSheet.Parse("Panel { padding: 4px; }")]);
        screen.Root!.UpdateStyles();

        var sources = node.GetStyleValueSources(Region.PaddingProperty);
        var list = Assert.IsAssignableFrom<IList<StyleValueSource>>(sources);

        Assert.True(list.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => list.Add(sources[0]));
    }

    private static Panel PanelWithClasses(params string[] classes)
    {
        var panel = new Panel();
        foreach (var name in classes)
            panel.Classes.Add(name);
        return panel;
    }

    private sealed class DefaultThicknessNode : Node
    {
        static DefaultThicknessNode()
        {
            CssRegistry.RegisterProperty<DefaultThicknessNode, Thickness>(
                "default-pad",
                PaddingProperty,
                CssValueConverters.ParseThickness);
            CssRegistry.RegisterProperty<DefaultThicknessNode>("default-pad-left", value =>
                new[] { StyleSetter.CreateEdge(PaddingProperty, StyleEdge.Left, CssValueConverters.ParseLength(value)) });
        }

        internal static readonly Property<Thickness> PaddingProperty =
            Property.Register<DefaultThicknessNode, Thickness>(
                "Padding",
                new Thickness(7),
                onChanged: static (node, _, _) => node.InvalidateMeasure());
    }

    private sealed class ExpandIndexNode : Node
    {
        static ExpandIndexNode()
        {
            CssRegistry.RegisterProperty<ExpandIndexNode>("index-sides", value =>
            {
                var edge = CssValueConverters.ParseLength(value);
                return
                [
                    StyleSetter.CreateEdge(PaddingProperty, StyleEdge.Left, edge),
                    StyleSetter.CreateEdge(PaddingProperty, StyleEdge.Right, edge)
                ];
            });
            CssRegistry.RegisterProperty<ExpandIndexNode>("index-left", value =>
                new[] { StyleSetter.CreateEdge(PaddingProperty, StyleEdge.Left, CssValueConverters.ParseLength(value)) });
        }

        internal static readonly Property<Thickness> PaddingProperty =
            Property.Register<ExpandIndexNode, Thickness>("Padding", Thickness.Zero,
                onChanged: static (node, _, _) => node.InvalidateMeasure());
    }
}
