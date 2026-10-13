using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Styling;
using PanguEngine.ComponentModel;

namespace PanguEngine.Tests.Client.UI.Styling;

[Collection(UiToolkitCollection.Name)]
public sealed class UiCssRegistrationUpdateTests
{
    private static bool LayoutInitialized;

    [Fact]
    public void LateLayoutPropertyIsAppliedOnTheNextStyleRecalculation()
    {
        var child = new Panel();
        var root = new Panel();
        root.Children.Add(child);
        var screen = new UiScreen(root);
        screen.SetBaseStyleSheets([]);
        screen.SetStyleSheets([UiStyleSheet.Parse("* { late-layout-offset: 7; }")]);
        screen.Open();
        try
        {
            screen.PrepareFrame(new Size(100, 100));
            Assert.False(LayoutInitialized);
            Assert.Null(UiCssRegistry.FindProperty(typeof(Panel), "late-layout-offset"));

            _ = new LateLayout();

            screen.PrepareFrame(new Size(100, 100));
            Assert.True(LayoutInitialized);
            Assert.Equal(0d, root.GetValue(LateLayout.OffsetProperty));
            Assert.Equal(0d, child.GetValue(LateLayout.OffsetProperty));

            root.Classes.Add("refresh");
            child.Classes.Add("refresh");

            screen.PrepareFrame(new Size(100, 100));
            Assert.Equal(7d, root.GetValue(LateLayout.OffsetProperty));
            Assert.Equal(7d, child.GetValue(LateLayout.OffsetProperty));
        }
        finally
        {
            screen.Close();
        }
    }

    [Theory]
    [InlineData("8")]
    [InlineData("var(--offset)")]
    public void LateAttachedDefinitionWaitsForEachScreenStyleInvalidationAndPreservesLocalValues(string value)
    {
        var name = $"late-offset-{Guid.NewGuid():N}";
        var property = Property.RegisterAttached<AttachedOwner, UiNode, double>(name, 0,
            onChanged: static (node, _, _) => node.InvalidateMeasure());
        var sheet = UiStyleSheet.Parse($"* {{ --offset: 8; {name}: {value}; }}");
        var child = new Panel();
        var root = new Panel();
        root.Children.Add(child);
        root.SetValue(property, 3d);
        var secondRoot = new Panel();
        var first = new UiScreen(root);
        var second = new UiScreen(secondRoot);
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

            UiCssRegistry.RegisterProperty<UiNode, double>(name, property, UiCssValueConverters.ParseLength);

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
        var screen = new UiScreen(node);
        screen.SetBaseStyleSheets([]);
        screen.SetStyleSheets([
            UiStyleSheet.Parse("LateAliasNode { opacity: 0.3; } late-update-tag { opacity: 0.7; }")
        ]);
        node.UpdateStyles();
        Assert.Equal(0.3, node.Opacity);

        UiCssRegistry.RegisterElement<LateAliasNode>("late-update-tag");

        node.UpdateStyles();
        Assert.Equal(0.3, node.Opacity);

        node.Classes.Add("refresh");
        node.UpdateStyles();
        Assert.Equal(0.7, node.Opacity);
        Assert.Equal("late-update-tag", Assert.Single(node.GetStyleValueSources(UiNode.OpacityProperty)).SelectorText);
    }

    [Fact]
    public void LateAncestorAliasUpdatesRelationshipsAndInheritedVariablesAfterStyleInvalidation()
    {
        var root = new LateAncestorNode();
        var child = new Panel();
        root.Children.Add(child);
        var screen = new UiScreen(root);
        screen.SetBaseStyleSheets([]);
        screen.SetStyleSheets([
            UiStyleSheet.Parse("""
                               late-ancestor-tag { --alpha: 0.4; }
                               * { opacity: var(--alpha, 1); }
                               late-ancestor-tag > Panel { padding: 5; }
                               """)
        ]);
        root.UpdateStyles();
        Assert.Equal(1d, child.Opacity);
        Assert.Equal(Thickness.Zero, child.Padding);

        UiCssRegistry.RegisterElement<LateAncestorNode>("late-ancestor-tag");

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
        var property = Property.RegisterAttached<AttachedOwner, UiNode, double>(name, 0);
        var root = new Panel();
        var child = new Panel();
        root.Children.Add(child);
        var screen = new UiScreen(root);
        screen.SetBaseStyleSheets([]);
        screen.SetStyleSheets([UiStyleSheet.Parse($"* {{ {name}: 6; opacity: 0.5; }}")]);
        var notifications = 0;
        root.PropertyChanged += (_, args) =>
        {
            if (args.Property != UiNode.OpacityProperty)
                return;
            notifications++;
            UiCssRegistry.RegisterProperty<UiNode, double>(name, property, UiCssValueConverters.ParseLength);
        };

        screen.Open();
        try
        {
            screen.PrepareFrame(new Size(100, 100));
            Assert.Equal(0d, root.GetValue(property));

            screen.PrepareFrame(new Size(100, 100));
            Assert.Equal(0d, root.GetValue(property));

            root.Classes.Add("refresh");
            child.Classes.Add("refresh");
            screen.PrepareFrame(new Size(100, 100));

            Assert.Equal(6d, root.GetValue(property));
            Assert.Equal(6d, child.GetValue(property));
            Assert.Equal(1, notifications);
        }
        finally
        {
            screen.Close();
        }
    }

    [Fact]
    public void RegistrationDuringBindingDoesNotPublishSkippedDeclarationsAsCurrent()
    {
        var lateName = $"binding-late-{Guid.NewGuid():N}";
        var triggerName = $"binding-trigger-{Guid.NewGuid():N}";
        var registered = false;
        UiCssRegistry.RegisterProperty<BindingRegistrationNode, double>(
            triggerName, BindingRegistrationNode.TriggerProperty, value =>
            {
                if (!registered)
                {
                    registered = true;
                    UiCssRegistry.RegisterProperty<BindingRegistrationNode, double>(
                        lateName, BindingRegistrationNode.LateProperty, UiCssValueConverters.ParseLength);
                }

                return UiCssValueConverters.ParseLength(value);
            });
        var resolver = new UiStyleResolver([], [
            UiStyleSheet.Parse($"BindingRegistrationNode {{ {lateName}: 6; {triggerName}: 2; }}")
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
        UiCssRegistry.RegisterElement<CachePreservationNode>("cache-preservation-tag");
        UiCssRegistry.RegisterProperty<CachePreservationNode, double>(name, UiNode.OpacityProperty, value =>
        {
            conversions++;
            return UiCssValueConverters.ParseNumber(value);
        });
        var resolver = new UiStyleResolver([], [UiStyleSheet.Parse($"cache-preservation-tag {{ {name}: 0.4; }}")]);
        var node = new CachePreservationNode();
        Assert.Equal(0.4, resolver.Resolve(node).GetValue(UiNode.OpacityProperty));
        var version = UiCssRegistry.Version;

        Assert.Throws<InvalidOperationException>(() =>
            UiCssRegistry.RegisterElement<CachePreservationNode>("another-tag"));
        Assert.Throws<InvalidOperationException>(() => UiCssRegistry.RegisterProperty<CachePreservationNode, double>(
            name, UiNode.OpacityProperty, UiCssValueConverters.ParseNumber));
        Assert.Equal(version, UiCssRegistry.Version);
        Assert.Equal(0.4, resolver.Resolve(node).GetValue(UiNode.OpacityProperty));
        Assert.Equal(1, conversions);
    }

    [Fact]
    public void SuccessfulRegistrationRebuildsAllCachedTypesOnTheNextResolution()
    {
        var name = $"global-cache-{Guid.NewGuid():N}";
        var conversions = 0;
        UiCssRegistry.RegisterProperty<GlobalCacheFirstNode, double>(name, UiNode.OpacityProperty, Convert);
        UiCssRegistry.RegisterProperty<GlobalCacheSecondNode, double>(name, UiNode.OpacityProperty, Convert);
        var resolver = new UiStyleResolver([], [UiStyleSheet.Parse($"* {{ {name}: 0.4; }}")]);
        var first = new GlobalCacheFirstNode();
        var second = new GlobalCacheSecondNode();
        Assert.Equal(0.4, resolver.Resolve(first).GetValue(UiNode.OpacityProperty));
        Assert.Equal(0.4, resolver.Resolve(second).GetValue(UiNode.OpacityProperty));
        Assert.Equal(2, conversions);
        var version = UiCssRegistry.Version;

        UiCssRegistry.RegisterElement<UnrelatedRegistrationNode>("unrelated-update-tag");

        Assert.Equal(version + 1, UiCssRegistry.Version);
        Assert.Equal(2, conversions);
        Assert.Equal(0.4, resolver.Resolve(first).GetValue(UiNode.OpacityProperty));
        Assert.Equal(0.4, resolver.Resolve(second).GetValue(UiNode.OpacityProperty));
        Assert.Equal(4, conversions);
        _ = resolver.Resolve(first);
        _ = resolver.Resolve(second);
        Assert.Equal(4, conversions);

        double Convert(string value)
        {
            conversions++;
            return UiCssValueConverters.ParseNumber(value);
        }
    }

    private sealed class LateLayout : Panel
    {
        internal static readonly Property<double> OffsetProperty =
            Property.RegisterAttached<LateLayout, UiNode, double>("Offset", 0);

        static LateLayout()
        {
            LayoutInitialized = true;
            UiCssRegistry.RegisterElement<LateLayout>("LateLayout");
            UiCssRegistry.RegisterProperty<UiNode, double>(
                "late-layout-offset", OffsetProperty, UiCssValueConverters.ParseLength);
        }
    }

    private sealed class AttachedOwner : UiNode
    {
    }

    private sealed class LateAliasNode : UiNode
    {
    }

    private sealed class LateAncestorNode : Panel
    {
    }

    private sealed class BindingRegistrationNode : UiNode
    {
        internal static readonly Property<double> TriggerProperty =
            Property.Register<BindingRegistrationNode, double>("Trigger", 0);

        internal static readonly Property<double> LateProperty =
            Property.Register<BindingRegistrationNode, double>("Late", 0);
    }

    private sealed class CachePreservationNode : UiNode
    {
    }

    private sealed class UnrelatedRegistrationNode : UiNode
    {
    }

    private sealed class GlobalCacheFirstNode : UiNode
    {
    }

    private sealed class GlobalCacheSecondNode : UiNode
    {
    }
}
