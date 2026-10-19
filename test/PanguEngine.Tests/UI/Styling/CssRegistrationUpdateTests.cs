using PanguEngine.ComponentModel;
using PanguEngine.UI;
using PanguEngine.UI.Controls;
using PanguEngine.UI.Styling;

namespace PanguEngine.Tests.UI.Styling;

[Collection(UIToolkitCollection.Name)]
public sealed class CssRegistrationUpdateTests
{
    private static bool LayoutInitialized;

    [Fact]
    public void LateLayoutPropertyIsAppliedOnTheNextStyleRecalculation()
    {
        var child = new Panel();
        var root = new Panel();
        root.Children.Add(child);
        var scene = new UIScene(root);
        scene.SetBaseStyleSheets([]);
        scene.SetStyleSheets([StyleSheet.Parse("* { late-layout-offset: 7; }")]);
        scene.Open();
        try
        {
            scene.PrepareFrame(new Size(100, 100));
            Assert.False(LayoutInitialized);
            Assert.Null(CssRegistry.FindProperty(typeof(Panel), "late-layout-offset"));

            _ = new LateLayout();

            scene.PrepareFrame(new Size(100, 100));
            Assert.True(LayoutInitialized);
            Assert.Equal(0d, root.GetValue(LateLayout.OffsetProperty));
            Assert.Equal(0d, child.GetValue(LateLayout.OffsetProperty));

            root.Classes.Add("refresh");
            child.Classes.Add("refresh");

            scene.PrepareFrame(new Size(100, 100));
            Assert.Equal(7d, root.GetValue(LateLayout.OffsetProperty));
            Assert.Equal(7d, child.GetValue(LateLayout.OffsetProperty));
        }
        finally
        {
            scene.Close();
        }
    }

    [Theory]
    [InlineData("8")]
    [InlineData("var(--offset)")]
    public void LateAttachedDefinitionWaitsForEachSceneStyleInvalidationAndPreservesLocalValues(string value)
    {
        var name = $"late-offset-{Guid.NewGuid():N}";
        var property = Property.RegisterAttached<AttachedOwner, UIElement, double>(name, 0,
            onChanged: static (node, _, _) => node.InvalidateMeasure());
        var sheet = StyleSheet.Parse($"* {{ --offset: 8; {name}: {value}; }}");
        var child = new Panel();
        var root = new Panel();
        root.Children.Add(child);
        root.SetValue(property, 3d);
        var secondRoot = new Panel();
        var first = new UIScene(root);
        var second = new UIScene(secondRoot);
        first.SetBaseStyleSheets([]);
        second.SetBaseStyleSheets([]);
        first.SetStyleSheets([sheet]);
        second.SetStyleSheets([sheet]);
        first.Open();
        second.Open();
        try
        {
            first.PrepareFrame(new Size(100, 100));
            second.PrepareFrame(new Size(100, 100));
            Assert.Equal(0d, child.GetValue(property));
            Assert.Equal(0d, secondRoot.GetValue(property));

            CssRegistry.RegisterProperty<UIElement, double>(name, property, CssValueConverters.ParseLength);

            first.PrepareFrame(new Size(100, 100));
            second.PrepareFrame(new Size(100, 100));
            Assert.Equal(0d, child.GetValue(property));
            Assert.Equal(0d, secondRoot.GetValue(property));
            Assert.Empty(root.GetStyleValueSources(property));

            root.Classes.Add("refresh");
            child.Classes.Add("refresh");

            first.PrepareFrame(new Size(100, 100));
            second.PrepareFrame(new Size(100, 100));
            Assert.Equal(3d, root.GetValue(property));
            Assert.Equal(8d, child.GetValue(property));
            Assert.Equal(0d, secondRoot.GetValue(property));
            Assert.True(Assert.Single(root.GetStyleValueSources(property)).IsMaskedByLocalValue);

            secondRoot.StyleId = "refresh";
            second.PrepareFrame(new Size(100, 100));
            Assert.Equal(8d, secondRoot.GetValue(property));

            root.ClearValue(property);
            Assert.Equal(8d, root.GetValue(property));
        }
        finally
        {
            second.Close();
            first.Close();
        }
    }

    [Fact]
    public void LateAliasReplacesClrNameRulesAfterStyleInvalidation()
    {
        var node = new LateAliasNode();
        var scene = new UIScene(node);
        scene.SetBaseStyleSheets([]);
        scene.SetStyleSheets([
            StyleSheet.Parse("LateAliasNode { opacity: 0.3; } late-update-tag { opacity: 0.7; }")
        ]);
        node.UpdateStyles();
        Assert.Equal(0.3, node.Opacity);

        CssRegistry.RegisterElement<LateAliasNode>("late-update-tag");

        node.UpdateStyles();
        Assert.Equal(0.3, node.Opacity);

        node.Classes.Add("refresh");
        node.UpdateStyles();
        Assert.Equal(0.7, node.Opacity);
        Assert.Equal("late-update-tag",
            Assert.Single(node.GetStyleValueSources(UIElement.OpacityProperty)).SelectorText);
    }

    [Fact]
    public void LateAncestorAliasUpdatesRelationshipsAndInheritedVariablesAfterStyleInvalidation()
    {
        var root = new LateAncestorNode();
        var child = new Panel();
        root.Children.Add(child);
        var scene = new UIScene(root);
        scene.SetBaseStyleSheets([]);
        scene.SetStyleSheets([
            StyleSheet.Parse("""
                             late-ancestor-tag { --alpha: 0.4; }
                             * { opacity: var(--alpha, 1); }
                             late-ancestor-tag > Panel { padding: 5; }
                             """)
        ]);
        root.UpdateStyles();
        Assert.Equal(1d, child.Opacity);
        Assert.Equal(Thickness.Zero, child.Padding);

        CssRegistry.RegisterElement<LateAncestorNode>("late-ancestor-tag");

        root.UpdateStyles();
        Assert.Equal(1d, child.Opacity);
        Assert.Equal(Thickness.Zero, child.Padding);

        root.Classes.Add("refresh");
        root.UpdateStyles();
        Assert.Equal(0.4, root.Opacity);
        Assert.Equal(0.4, child.Opacity);
        Assert.Equal(new Thickness(5), child.Padding);
    }

    [Fact]
    public void RegistrationDuringNotificationsWaitsForTheNextStyleInvalidation()
    {
        var name = $"notification-offset-{Guid.NewGuid():N}";
        var property = Property.RegisterAttached<AttachedOwner, UIElement, double>(name, 0);
        var root = new Panel();
        var child = new Panel();
        root.Children.Add(child);
        var scene = new UIScene(root);
        scene.SetBaseStyleSheets([]);
        scene.SetStyleSheets([StyleSheet.Parse($"* {{ {name}: 6; opacity: 0.5; }}")]);
        var notifications = 0;
        root.PropertyChanged += (_, args) =>
        {
            if (args.Property != UIElement.OpacityProperty)
                return;
            notifications++;
            CssRegistry.RegisterProperty<UIElement, double>(name, property, CssValueConverters.ParseLength);
        };

        scene.Open();
        try
        {
            scene.PrepareFrame(new Size(100, 100));
            Assert.Equal(0d, root.GetValue(property));

            scene.PrepareFrame(new Size(100, 100));
            Assert.Equal(0d, root.GetValue(property));

            root.Classes.Add("refresh");
            child.Classes.Add("refresh");
            scene.PrepareFrame(new Size(100, 100));

            Assert.Equal(6d, root.GetValue(property));
            Assert.Equal(6d, child.GetValue(property));
            Assert.Equal(1, notifications);
        }
        finally
        {
            scene.Close();
        }
    }

    [Fact]
    public void RegistrationDuringBindingDoesNotPublishSkippedDeclarationsAsCurrent()
    {
        var lateName = $"binding-late-{Guid.NewGuid():N}";
        var triggerName = $"binding-trigger-{Guid.NewGuid():N}";
        var registered = false;
        CssRegistry.RegisterProperty<BindingRegistrationNode, double>(
            triggerName, BindingRegistrationNode.TriggerProperty, value =>
            {
                if (!registered)
                {
                    registered = true;
                    CssRegistry.RegisterProperty<BindingRegistrationNode, double>(
                        lateName, BindingRegistrationNode.LateProperty, CssValueConverters.ParseLength);
                }

                return CssValueConverters.ParseLength(value);
            });
        var resolver = new StyleResolver([], [
            StyleSheet.Parse($"BindingRegistrationNode {{ {lateName}: 6; {triggerName}: 2; }}")
        ]);
        var node = new BindingRegistrationNode();

        Assert.Equal(0d, resolver.Resolve(node).GetValue(BindingRegistrationNode.LateProperty));

        var snapshot = resolver.Resolve(node);
        Assert.Equal(6d, snapshot.GetValue(BindingRegistrationNode.LateProperty));
        Assert.Equal(2d, snapshot.GetValue(BindingRegistrationNode.TriggerProperty));
    }

    [Fact]
    public void FailedRegistrationsPreserveTheVersionAndExistingBindings()
    {
        var name = $"cached-offset-{Guid.NewGuid():N}";
        var conversions = 0;
        CssRegistry.RegisterElement<CachePreservationNode>("cache-preservation-tag");
        CssRegistry.RegisterProperty<CachePreservationNode, double>(name, UIElement.OpacityProperty, value =>
        {
            conversions++;
            return CssValueConverters.ParseNumber(value);
        });
        var resolver = new StyleResolver([], [StyleSheet.Parse($"cache-preservation-tag {{ {name}: 0.4; }}")]);
        var node = new CachePreservationNode();
        Assert.Equal(0.4, resolver.Resolve(node).GetValue(UIElement.OpacityProperty));
        var version = CssRegistry.Version;

        Assert.Throws<InvalidOperationException>(() =>
            CssRegistry.RegisterElement<CachePreservationNode>("another-tag"));
        Assert.Throws<InvalidOperationException>(() => CssRegistry.RegisterProperty<CachePreservationNode, double>(
            name, UIElement.OpacityProperty, CssValueConverters.ParseNumber));
        Assert.Equal(version, CssRegistry.Version);
        Assert.Equal(0.4, resolver.Resolve(node).GetValue(UIElement.OpacityProperty));
        Assert.Equal(1, conversions);
    }

    [Fact]
    public void SuccessfulRegistrationRebuildsAllCachedTypesOnTheNextResolution()
    {
        var name = $"global-cache-{Guid.NewGuid():N}";
        var conversions = 0;
        CssRegistry.RegisterProperty<GlobalCacheFirstNode, double>(name, UIElement.OpacityProperty, Convert);
        CssRegistry.RegisterProperty<GlobalCacheSecondNode, double>(name, UIElement.OpacityProperty, Convert);
        var resolver = new StyleResolver([], [StyleSheet.Parse($"* {{ {name}: 0.4; }}")]);
        var first = new GlobalCacheFirstNode();
        var second = new GlobalCacheSecondNode();
        Assert.Equal(0.4, resolver.Resolve(first).GetValue(UIElement.OpacityProperty));
        Assert.Equal(0.4, resolver.Resolve(second).GetValue(UIElement.OpacityProperty));
        Assert.Equal(2, conversions);
        var version = CssRegistry.Version;

        CssRegistry.RegisterElement<UnrelatedRegistrationNode>("unrelated-update-tag");

        Assert.Equal(version + 1, CssRegistry.Version);
        Assert.Equal(2, conversions);
        Assert.Equal(0.4, resolver.Resolve(first).GetValue(UIElement.OpacityProperty));
        Assert.Equal(0.4, resolver.Resolve(second).GetValue(UIElement.OpacityProperty));
        Assert.Equal(4, conversions);
        _ = resolver.Resolve(first);
        _ = resolver.Resolve(second);
        Assert.Equal(4, conversions);

        double Convert(string value)
        {
            conversions++;
            return CssValueConverters.ParseNumber(value);
        }
    }

    private sealed class LateLayout : Panel
    {
        internal static readonly Property<double> OffsetProperty =
            Property.RegisterAttached<LateLayout, UIElement, double>("Offset", 0);

        static LateLayout()
        {
            LayoutInitialized = true;
            CssRegistry.RegisterElement<LateLayout>("LateLayout");
            CssRegistry.RegisterProperty<UIElement, double>(
                "late-layout-offset", OffsetProperty, CssValueConverters.ParseLength);
        }
    }

    private sealed class AttachedOwner : UIElement
    {
    }

    private sealed class LateAliasNode : UIElement
    {
    }

    private sealed class LateAncestorNode : Panel
    {
    }

    private sealed class BindingRegistrationNode : UIElement
    {
        internal static readonly Property<double> TriggerProperty =
            Property.Register<BindingRegistrationNode, double>("Trigger", 0);

        internal static readonly Property<double> LateProperty =
            Property.Register<BindingRegistrationNode, double>("Late", 0);
    }

    private sealed class CachePreservationNode : UIElement
    {
    }

    private sealed class UnrelatedRegistrationNode : UIElement
    {
    }

    private sealed class GlobalCacheFirstNode : UIElement
    {
    }

    private sealed class GlobalCacheSecondNode : UIElement
    {
    }
}