using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Styling;
using PanguEngine.ComponentModel;

namespace PanguEngine.Tests.Client.UI;

public sealed class PropertyInheritanceTests
{
    private static readonly Property<double> NumberProperty =
        Property.RegisterAttached<PropertyOwner, UiNode, double>("Number", 7, inherits: true);

    private static readonly Property<string?> LabelProperty =
        Property.RegisterAttached<PropertyOwner, UiNode, string?>("Label", "default", inherits: true);

    private static readonly Property<double> PlainProperty =
        Property.RegisterAttached<PropertyOwner, UiNode, double>("Plain", 7);

    private static readonly Property<double> OtherNumberProperty =
        Property.RegisterAttached<OtherOwner, UiNode, double>("Number", 3, inherits: true);

    [Fact]
    public void MultiplePropertyTypesInheritAndUnmarkedPropertiesDoNot()
    {
        var root = new Panel();
        var middle = new Panel();
        var child = new Text();
        middle.Children.Add(child);
        root.Children.Add(middle);

        root.SetValue(NumberProperty, 20);
        root.SetValue(LabelProperty, "root");
        root.SetValue(PlainProperty, 50);

        Assert.True(NumberProperty.Inherits);
        Assert.True(LabelProperty.Inherits);
        Assert.False(PlainProperty.Inherits);
        Assert.Equal(20d, child.GetValue(NumberProperty));
        Assert.Equal("root", child.GetValue(LabelProperty));
        Assert.Equal(7d, child.GetValue(PlainProperty));
        root.Children.Remove(middle);
        Assert.Equal(7d, child.GetValue(NumberProperty));
        Assert.Equal("default", child.GetValue(LabelProperty));
    }

    [Fact]
    public void NullIsAnInheritedValueEvenWithANonNullDefault()
    {
        var root = new Panel();
        var middle = new Panel();
        var child = new Text();
        middle.Children.Add(child);
        root.Children.Add(middle);
        var changes = new List<(string? Old, string? New)>();
        using var subscription = child.Subscribe(LabelProperty,
            (_, args) => changes.Add((args.OldValue, args.NewValue)));

        root.SetValue(LabelProperty, null);
        Assert.Null(middle.GetValue(LabelProperty));
        Assert.Null(child.GetValue(LabelProperty));
        middle.SetValue(LabelProperty, null);
        root.SetValue(LabelProperty, "updated");
        Assert.Null(child.GetValue(LabelProperty));
        middle.ClearValue(LabelProperty);

        Assert.Equal("updated", child.GetValue(LabelProperty));
        Assert.Equal(new (string?, string?)[] { ("default", null), (null, "updated") }, changes);
    }

    [Fact]
    public void ClearingLocalOverridesUsesTheCurrentAncestorValue()
    {
        var root = new Panel();
        root.SetValue(NumberProperty, 20);
        var middle = new Panel();
        var child = new Text();
        middle.Children.Add(child);
        root.Children.Add(middle);
        middle.SetValue(NumberProperty, 20);
        var notifications = 0;
        using var subscription = child.Subscribe(NumberProperty, (_, _) => notifications++);

        root.SetValue(NumberProperty, 30);
        Assert.Equal(20d, child.GetValue(NumberProperty));
        Assert.Equal(0, notifications);
        middle.ClearValue(NumberProperty);

        Assert.Equal(30d, child.GetValue(NumberProperty));
        Assert.Equal(1, notifications);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReparentingAnOverriddenSubtreeDoesNotNotifyUntilTheOverrideIsCleared(bool useStyle)
    {
        var root = new Panel();
        var first = new Panel();
        var second = new Panel();
        first.SetValue(NumberProperty, 20);
        second.SetValue(NumberProperty, 30);
        root.Children.AddRange([first, second]);
        var middle = new InheritedPanel { StyleId = "moving" };
        var child = new Text();
        middle.Children.Add(child);
        first.Children.Add(middle);
        var screen = new UiScreen(root);
        if (useStyle)
        {
            screen.SetStyleSheets([UiStyleSheet.Parse("#moving { inherited-number: 14; }")]);
            root.UpdateStyles();
        }
        else
        {
            middle.SetValue(NumberProperty, 14);
        }
        var middleChanges = new List<(double Old, double New)>();
        var childChanges = new List<(double Old, double New)>();
        using var middleSubscription = middle.Subscribe(NumberProperty,
            (_, args) => middleChanges.Add((args.OldValue, args.NewValue)));
        using var childSubscription = child.Subscribe(NumberProperty,
            (_, args) => childChanges.Add((args.OldValue, args.NewValue)));

        second.Children.Add(middle);
        if (useStyle)
            root.UpdateStyles();

        Assert.Same(second, middle.Parent);
        Assert.Equal(14d, middle.GetValue(NumberProperty));
        Assert.Equal(14d, child.GetValue(NumberProperty));
        Assert.Empty(middleChanges);
        Assert.Empty(childChanges);

        if (useStyle)
        {
            screen.SetStyleSheets([]);
            root.UpdateStyles();
        }
        else
        {
            middle.ClearValue(NumberProperty);
        }

        Assert.Equal(30d, middle.GetValue(NumberProperty));
        Assert.Equal(30d, child.GetValue(NumberProperty));
        Assert.Equal(new[] { (14d, 30d) }, middleChanges);
        Assert.Equal(new[] { (14d, 30d) }, childChanges);
    }

    [Fact]
    public void SameNameDescriptorsRemainIndependent()
    {
        var root = new Panel();
        var child = new Text();
        root.Children.Add(child);
        root.SetValue(NumberProperty, 20);
        root.SetValue(OtherNumberProperty, 30);

        Assert.Equal(NumberProperty.Name, OtherNumberProperty.Name);
        Assert.NotSame(NumberProperty, OtherNumberProperty);
        Assert.Equal(20d, child.GetValue(NumberProperty));
        Assert.Equal(30d, child.GetValue(OtherNumberProperty));
        root.ClearValue(NumberProperty);
        Assert.Equal(7d, child.GetValue(NumberProperty));
        Assert.Equal(30d, child.GetValue(OtherNumberProperty));
    }

    [Fact]
    public void LateRegistrationNotifiesTheFirstChangeWithTheOldDefault()
    {
        var root = new Panel();
        var middle = new Panel();
        var child = new Text();
        middle.Children.Add(child);
        root.Children.Add(middle);
        var property = Property.RegisterAttached<LateOwner, UiNode, int>("Late", 7, inherits: true);
        var changes = new List<(int Old, int New)>();
        using var subscription = child.Subscribe(property,
            (_, args) => changes.Add((args.OldValue, args.NewValue)));

        root.SetValue(property, 20);

        Assert.Equal(20, child.GetValue(property));
        Assert.Equal(new[] { (7, 20) }, changes);
        root.Children.Remove(middle);
        Assert.Equal(7, child.GetValue(property));
        Assert.Equal(new[] { (7, 20), (20, 7) }, changes);
    }

    [Fact]
    public void IncompatibleContainersBridgeValuesAndReparentingWithoutCallbacks()
    {
        var first = new ScopedPanel();
        var second = new ScopedPanel();
        first.SetValue(ScopedPanel.ValueProperty, 20);
        second.SetValue(ScopedPanel.ValueProperty, 30);
        var bridge = new Panel();
        var child = new ScopedPanel();
        bridge.Children.Add(child);
        first.Children.Add(bridge);
        var bridgeNotifications = 0;
        bridge.PropertyChanged += (_, args) =>
        {
            if (ReferenceEquals(args.Property, ScopedPanel.ValueProperty))
                bridgeNotifications++;
        };
        var changes = new List<int>();
        using var subscription = child.Subscribe(ScopedPanel.ValueProperty,
            (_, args) => changes.Add(args.NewValue));

        first.SetValue(ScopedPanel.ValueProperty, 25);
        Assert.Equal(25, child.GetValue(ScopedPanel.ValueProperty));
        second.Children.Add(bridge);
        Assert.Equal(30, child.GetValue(ScopedPanel.ValueProperty));
        second.Children.Remove(bridge);

        Assert.Equal(5, child.GetValue(ScopedPanel.ValueProperty));
        Assert.Equal(new[] { 25, 5, 30, 5 }, changes);
        Assert.Equal(0, bridgeNotifications);
        Assert.Throws<ArgumentException>(() => bridge.GetValue(ScopedPanel.ValueProperty));
    }

    [Fact]
    public void ReadOnlyKeysOverrideInheritedValuesAndClearBackToTheCurrentSource()
    {
        var root = new ReadOnlyPanel();
        var bridge = new Panel();
        var child = new ReadOnlyPanel();
        bridge.Children.Add(child);
        root.Children.Add(bridge);
        root.SetLabel("parent");
        Assert.True(ReadOnlyPanel.ValueProperty.Inherits);
        Assert.Equal("parent", child.Value);
        child.SetLabel("local");
        root.SetLabel(null);
        Assert.Equal("local", child.Value);
        child.ClearLabel();
        Assert.Null(child.Value);
        root.Children.Remove(bridge);
        Assert.Equal("fallback", child.Value);
    }

    [Fact]
    public void MultipleStyleChangesAreVisibleBeforeTheirNotifications()
    {
        var root = new InheritedPanel { StyleId = "root" };
        var child = new InheritedPanel();
        root.Children.Add(child);
        var screen = new UiScreen(root);
        screen.SetStyleSheets([
            UiStyleSheet.Parse("#root { inherited-number: 20; inherited-label: parent; }")
        ]);
        var changedProperties = new List<Property>();
        child.PropertyChanged += (_, args) =>
        {
            if (!ReferenceEquals(args.Property, NumberProperty) && !ReferenceEquals(args.Property, LabelProperty))
                return;
            Assert.Equal(20d, root.GetValue(NumberProperty));
            Assert.Equal("parent", root.GetValue(LabelProperty));
            Assert.Equal(20d, child.GetValue(NumberProperty));
            Assert.Equal("parent", child.GetValue(LabelProperty));
            changedProperties.Add(args.Property);
        };

        Assert.Equal(7d, child.GetValue(NumberProperty));
        Assert.Empty(changedProperties);
        root.UpdateStyles();

        Assert.Equal(20d, child.GetValue(NumberProperty));
        Assert.Equal("parent", child.GetValue(LabelProperty));
        Assert.Equal(2, changedProperties.Count);
        Assert.Contains(NumberProperty, changedProperties);
        Assert.Contains(LabelProperty, changedProperties);
    }

    [Fact]
    public void StyleUpdatesKeepAncestorSourcesSeparateFromChildStyleAndLocalValues()
    {
        var root = new InheritedPanel { StyleId = "root" };
        var child = new InheritedPanel { StyleId = "child" };
        var leaf = new Text();
        child.Children.Add(leaf);
        root.Children.Add(child);
        var screen = new UiScreen(root);
        screen.SetStyleSheets([UiStyleSheet.Parse(
            "#root { inherited-number: 20; } #child { inherited-number: 14; }")]);
        root.UpdateStyles();
        Assert.Equal(14d, leaf.GetValue(NumberProperty));
        child.SetValue(NumberProperty, 40);
        screen.SetStyleSheets([UiStyleSheet.Parse(
            "#root { inherited-number: 30; } #child { inherited-number: 15; }")]);
        root.UpdateStyles();
        Assert.Equal(40d, leaf.GetValue(NumberProperty));
        child.ClearValue(NumberProperty);
        Assert.Equal(15d, leaf.GetValue(NumberProperty));
        screen.SetStyleSheets([UiStyleSheet.Parse("#root { inherited-number: 30; }")]);
        root.UpdateStyles();
        Assert.Equal(30d, leaf.GetValue(NumberProperty));
    }

    [Fact]
    public void StyleUpdatesCrossIncompatibleContainers()
    {
        var root = new ScopedPanel { StyleId = "root" };
        var bridge = new Panel();
        var child = new ScopedPanel();
        bridge.Children.Add(child);
        root.Children.Add(bridge);
        var screen = new UiScreen(root);
        screen.SetStyleSheets([UiStyleSheet.Parse("#root { scoped-value: 28; }")]);
        var changes = new List<(int Old, int New)>();
        using var subscription = child.Subscribe(ScopedPanel.ValueProperty,
            (_, args) => changes.Add((args.OldValue, args.NewValue)));

        root.UpdateStyles();

        Assert.Equal(28, child.GetValue(ScopedPanel.ValueProperty));
        Assert.Equal(new[] { (5, 28) }, changes);
    }

    [Fact]
    public void ObservableObjectSupportsInheritedPropertiesThroughParentHook()
    {
        var firstParent = new PlainHost();
        var secondParent = new PlainHost();
        firstParent.SetValue(PlainHost.ValueProperty, 20);
        secondParent.SetValue(PlainHost.ValueProperty, 30);
        var child = new PlainHost(firstParent);
        Assert.True(PlainHost.ValueProperty.Inherits);
        Assert.Equal(20, child.GetValue(PlainHost.ValueProperty));
        var changes = new List<(int Old, int New)>();
        using var subscription = child.Subscribe(PlainHost.ValueProperty,
            (_, args) => changes.Add((args.OldValue, args.NewValue)));

        firstParent.SetValue(PlainHost.ValueProperty, 25);
        Assert.Equal(25, child.GetValue(PlainHost.ValueProperty));

        child.SetValue(PlainHost.ValueProperty, 40);
        firstParent.SetValue(PlainHost.ValueProperty, 50);
        Assert.Equal(40, child.GetValue(PlainHost.ValueProperty));
        child.ClearValue(PlainHost.ValueProperty);
        Assert.Equal(50, child.GetValue(PlainHost.ValueProperty));

        child.SetParent(secondParent);
        Assert.Equal(30, child.GetValue(PlainHost.ValueProperty));
        secondParent.SetValue(PlainHost.ValueProperty, 35);
        Assert.Equal(35, child.GetValue(PlainHost.ValueProperty));
        child.SetParent(null);
        Assert.Equal(7, child.GetValue(PlainHost.ValueProperty));
        Assert.Equal(new[] { (20, 25), (25, 40), (40, 50), (50, 30), (30, 35), (35, 7) }, changes);
    }

    [Fact]
    public void RemovingChildrenUsesCurrentOldParentSources()
    {
        var parent = new Panel { FontSize = 20 };
        var first = new Panel();
        var second = new Panel();
        parent.Children.AddRange([first, second]);
        using var firstSubscription = first.Subscribe(UiNode.FontSizeProperty, (_, _) =>
        {
            parent.FontSize = 30;
            Assert.Equal(30d, second.FontSize);
        });
        var changes = new List<(double Old, double New)>();
        using var secondSubscription = second.Subscribe(UiNode.FontSizeProperty,
            (_, args) => changes.Add((args.OldValue, args.NewValue)));

        parent.Children.RemoveRange(0, 2);

        Assert.Equal(16d, first.FontSize);
        Assert.Equal(16d, second.FontSize);
        Assert.Equal(new[] { (30d, 16d) }, changes);
    }

    [Fact]
    public void SourceNotificationsPrecedeDescendantNotifications()
    {
        var parent = new PlainHost();
        var first = new PlainHost(parent);
        var second = new PlainHost(parent);
        var order = new List<PlainHost>();
        using var parentSubscription = parent.Subscribe(PlainHost.ValueProperty,
            (_, _) => order.Add(parent));
        using var firstSubscription = first.Subscribe(PlainHost.ValueProperty,
            (_, _) =>
            {
                Assert.Equal(20, second.GetValue(PlainHost.ValueProperty));
                order.Add(first);
            });
        using var secondSubscription = second.Subscribe(PlainHost.ValueProperty,
            (_, args) =>
            {
                Assert.Equal((7, 20), (args.OldValue, args.NewValue));
                order.Add(second);
            });

        parent.SetValue(PlainHost.ValueProperty, 20);

        Assert.Equal(new[] { parent, first, second }, order);
    }

    [Fact]
    public void InheritedNotificationFailureStopsLaterSiblingsAndPreservesCommittedValue()
    {
        var parent = new PlainHost();
        parent.SetValue(PlainHost.ValueProperty, 20);
        var first = new PlainHost(parent);
        var second = new PlainHost(parent);
        var failure = new InvalidOperationException("inherited notification failed");
        using var firstSubscription = first.Subscribe(PlainHost.ValueProperty, (_, args) =>
        {
            Assert.Equal((20, 30), (args.OldValue, args.NewValue));
            throw failure;
        });
        var laterNotifications = 0;
        using var secondSubscription = second.Subscribe(PlainHost.ValueProperty,
            (_, _) => laterNotifications++);

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(
            () => parent.SetValue(PlainHost.ValueProperty, 30)));

        Assert.Equal(30, parent.GetValue(PlainHost.ValueProperty));
        Assert.Equal(30, first.GetValue(PlainHost.ValueProperty));
        Assert.Equal(30, second.GetValue(PlainHost.ValueProperty));
        Assert.Equal(0, laterNotifications);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RegisteredCallbacksReadTheCurrentInheritedValueBeforeNotification(bool useBridge)
    {
        var parent = new CallbackHost();
        var child = new CallbackHost();
        if (useBridge)
        {
            var bridge = new PlainHost();
            bridge.SetParent(parent);
            child.SetParent(bridge);
        }
        else
        {
            child.SetParent(parent);
        }
        parent.OnValueChanged = () => Assert.Equal(20, child.GetValue(CallbackHost.ValueProperty));
        var changes = new List<(int Old, int New)>();
        using var subscription = child.Subscribe(CallbackHost.ValueProperty,
            (_, args) => changes.Add((args.OldValue, args.NewValue)));

        parent.SetValue(CallbackHost.ValueProperty, 20);

        Assert.Equal(20, child.GetValue(CallbackHost.ValueProperty));
        Assert.Equal(new[] { (7, 20) }, changes);
    }

    [Fact]
    public void ParentChangeCapturesInputsForTheNodeAndDescendantsBeforeCallbacksRun()
    {
        var firstParent = new PlainHost();
        var secondParent = new PlainHost();
        var moving = new PlainHost();
        var descendant = new PlainHost();
        firstParent.SetValue(PlainHost.ValueProperty, 20);
        secondParent.SetValue(PlainHost.ValueProperty, 30);
        secondParent.SetValue(PlainHost.OtherValueProperty, 60);
        moving.SetParent(firstParent);
        descendant.SetParent(moving);
        var movingChanges = new List<(int Old, int New)>();
        var descendantChanges = new List<(int Old, int New)>();
        using var valueSubscription = moving.Subscribe(PlainHost.ValueProperty,
            (_, _) =>
            {
                _ = moving.GetValue(PlainHost.OtherValueProperty);
                _ = descendant.GetValue(PlainHost.OtherValueProperty);
            });
        using var movingOtherValueSubscription = moving.Subscribe(PlainHost.OtherValueProperty,
            (_, args) => movingChanges.Add((args.OldValue, args.NewValue)));
        using var descendantOtherValueSubscription = descendant.Subscribe(PlainHost.OtherValueProperty,
            (_, args) => descendantChanges.Add((args.OldValue, args.NewValue)));

        moving.SetParent(secondParent);

        Assert.Equal(30, moving.GetValue(PlainHost.ValueProperty));
        Assert.Equal(30, descendant.GetValue(PlainHost.ValueProperty));
        Assert.Equal(60, moving.GetValue(PlainHost.OtherValueProperty));
        Assert.Equal(60, descendant.GetValue(PlainHost.OtherValueProperty));
        Assert.Equal(new[] { (9, 60) }, movingChanges);
        Assert.Equal(new[] { (9, 60) }, descendantChanges);
    }

    [Fact]
    public void ParentChangeCapturesParentInputChangesForBridgedDescendantsBeforeCallbacksRun()
    {
        var first = new ScopedRoot();
        var second = new ScopedRoot();
        var moving = new BridgeRoot();
        var descendant = new ScopedLeaf();
        Assert.Same(ScopedRoot.ValueProperty, ScopedLeaf.ValueProperty);
        Assert.Throws<ArgumentException>(() => moving.GetValue(ScopedHost.ValueProperty));
        second.SetValue(ScopedHost.ValueProperty, 60);
        second.SetValue(BridgeRoot.TriggerProperty, 2);
        moving.SetChild(descendant);
        moving.SetParent(first);
        var changes = new List<(int Old, int New)>();
        using var triggerSubscription = moving.Subscribe(BridgeRoot.TriggerProperty,
            (_, _) => _ = descendant.GetValue(ScopedLeaf.ValueProperty));
        using var valueSubscription = descendant.Subscribe(ScopedLeaf.ValueProperty,
            (_, args) => changes.Add((args.OldValue, args.NewValue)));

        moving.SetParent(second);

        Assert.Equal(2, moving.GetValue(BridgeRoot.TriggerProperty));
        Assert.Equal(60, descendant.GetValue(ScopedLeaf.ValueProperty));
        Assert.Equal(new[] { (5, 60) }, changes);
    }

    [Fact]
    public void NullStyleValuesRemainPresentUnderLocalOverrides()
    {
        var root = new Panel();
        root.SetValue(LabelProperty, "parent");
        var child = new InheritedPanel();
        root.Children.Add(child);
        var screen = new UiScreen(root);
        screen.SetStyleSheets([new UiStyleSheet([
            new UiStyleRule(UiStyleSelector.For<InheritedPanel>(),
                [UiStyleSetter.Create(LabelProperty, (string?)null)])
        ])]);
        root.UpdateStyles();
        Assert.Null(child.GetValue(LabelProperty));

        child.SetValue(LabelProperty, "local");
        root.SetValue(LabelProperty, "latest");
        Assert.True(Assert.Single(child.GetStyleValueSources(LabelProperty)).IsMaskedByLocalValue);
        child.ClearValue(LabelProperty);

        Assert.Null(child.GetValue(LabelProperty));
        Assert.False(Assert.Single(child.GetStyleValueSources(LabelProperty)).IsMaskedByLocalValue);
        screen.SetStyleSheets([]);
        root.UpdateStyles();
        Assert.Equal("latest", child.GetValue(LabelProperty));
        Assert.Empty(child.GetStyleValueSources(LabelProperty));
    }

    [Fact]
    public void RemovingStyleRestoresInheritedNullAndDetachingUsesDefault()
    {
        var root = new Panel();
        root.SetValue(LabelProperty, null);
        var child = new InheritedPanel();
        root.Children.Add(child);
        Assert.Null(child.GetValue(LabelProperty));
        var screen = new UiScreen(root);
        screen.SetStyleSheets([new UiStyleSheet([
            new UiStyleRule(UiStyleSelector.For<InheritedPanel>(),
                [UiStyleSetter.Create(LabelProperty, (string?)"styled")])
        ])]);
        root.UpdateStyles();
        Assert.Equal("styled", child.GetValue(LabelProperty));
        var changes = new List<(string? Old, string? New)>();
        using var subscription = child.Subscribe(LabelProperty,
            (_, args) => changes.Add((args.OldValue, args.NewValue)));

        screen.SetStyleSheets([]);
        root.UpdateStyles();
        Assert.Null(child.GetValue(LabelProperty));
        root.Children.Remove(child);

        Assert.Equal("default", child.GetValue(LabelProperty));
        Assert.Equal(new (string?, string?)[] { ("styled", null), (null, "default") }, changes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnbindAfterRejectedInitializationDoesNotMaterializeFallback(bool useStyle)
    {
        var property = Property.RegisterAttached<LateOwner, UiNode, int>(
            Guid.NewGuid().ToString(), 7, validate: static value => value >= 0, inherits: true);
        var root = new Panel();
        root.SetValue(property, 20);
        var child = new InheritedPanel();
        root.Children.Add(child);
        var screen = new UiScreen(root);
        if (useStyle)
        {
            screen.SetStyleSheets([CreateStyle(25)]);
            root.UpdateStyles();
        }

        Assert.Throws<ArgumentException>(() => child.Bind(property, new PlainHost(),
            PlainHost.ValueProperty, static _ => -1));
        Assert.True(child.IsBound(property));
        Assert.Equal(useStyle ? 25 : 20, child.GetValue(property));
        child.Unbind(property);
        Assert.False(child.IsBound(property));
        root.SetValue(property, 30);
        if (useStyle)
        {
            screen.SetStyleSheets([CreateStyle(35)]);
            root.UpdateStyles();
            Assert.False(Assert.Single(child.GetStyleValueSources(property)).IsMaskedByLocalValue);
        }
        Assert.Equal(useStyle ? 35 : 30, child.GetValue(property));

        UiStyleSheet CreateStyle(int value) => new([
            new UiStyleRule(UiStyleSelector.For<InheritedPanel>(), [UiStyleSetter.Create(property, value)])
        ]);
    }

    [Fact]
    public void ParentCallbackReentryPropagatesTheCurrentSourceValue()
    {
        var parent = new CallbackHost();
        var child = new CallbackHost();
        parent.SetValue(CallbackHost.ValueProperty, 20);
        child.SetParent(parent);
        var changes = new List<(int Old, int New)>();
        using var subscription = child.Subscribe(CallbackHost.ValueProperty, (_, args) =>
        {
            Assert.Equal(40, child.GetValue(CallbackHost.ValueProperty));
            changes.Add((args.OldValue, args.NewValue));
        });
        parent.OnValueChanged = () =>
        {
            parent.OnValueChanged = null;
            parent.SetValue(CallbackHost.ValueProperty, 40);
        };

        parent.SetValue(CallbackHost.ValueProperty, 30);

        Assert.Equal(new[] { (30, 40), (20, 40) }, changes);
    }

    [Fact]
    public void ChildCallbackReentryResumesTheCapturedOuterNotification()
    {
        var parent = new PlainHost();
        var first = new PlainHost();
        var second = new PlainHost();
        parent.SetValue(PlainHost.ValueProperty, 20);
        first.SetParent(parent);
        second.SetParent(parent);
        using var firstSubscription = first.Subscribe(PlainHost.ValueProperty, (_, args) =>
        {
            if (args.NewValue == 30)
                parent.SetValue(PlainHost.ValueProperty, 40);
        });
        var changes = new List<(int Old, int New)>();
        using var secondSubscription = second.Subscribe(PlainHost.ValueProperty, (_, args) =>
        {
            Assert.Equal(40, second.GetValue(PlainHost.ValueProperty));
            changes.Add((args.OldValue, args.NewValue));
        });

        parent.SetValue(PlainHost.ValueProperty, 30);

        Assert.Equal(new[] { (30, 40), (20, 30) }, changes);
    }

    [Fact]
    public void EquivalentAncestorWritesAreVisibleWithoutNotifications()
    {
        var property = Property.Register<PlainHost, string>(Guid.NewGuid().ToString(), "default",
            comparer: StringComparer.OrdinalIgnoreCase, inherits: true);
        var parent = new PlainHost();
        parent.SetValue(property, "alpha");
        var child = new PlainHost(parent);
        Assert.Equal("alpha", child.GetValue(property));
        var notifications = 0;
        using var subscription = child.Subscribe(property, (_, _) => notifications++);

        parent.SetValue(property, "ALPHA");

        Assert.Equal("ALPHA", child.GetValue(property));
        Assert.Equal(0, notifications);
    }

    [Fact]
    public void SameValueLocalOverrideDuringInheritedNotificationPreservesDescendantEvent()
    {
        var parent = new PlainHost();
        parent.SetValue(PlainHost.ValueProperty, 20);
        var child = new PlainHost(parent);
        var leaf = new PlainHost(child);
        using var childSubscription = child.Subscribe(PlainHost.ValueProperty, (_, args) =>
        {
            if (args.NewValue == 30)
                child.SetValue(PlainHost.ValueProperty, 30);
        });
        var changes = new List<(int Old, int New)>();
        using var leafSubscription = leaf.Subscribe(PlainHost.ValueProperty,
            (_, args) => changes.Add((args.OldValue, args.NewValue)));

        parent.SetValue(PlainHost.ValueProperty, 30);
        Assert.Equal(new[] { (20, 30) }, changes);
        parent.SetValue(PlainHost.ValueProperty, 40);
        Assert.Equal(30, leaf.GetValue(PlainHost.ValueProperty));
        Assert.Equal(new[] { (20, 30) }, changes);
        child.ClearValue(PlainHost.ValueProperty);

        Assert.Equal(40, leaf.GetValue(PlainHost.ValueProperty));
        Assert.Equal(new[] { (20, 30), (30, 40) }, changes);
    }

    [Fact]
    public void ReparentedSiblingIsSkippedByOuterChildSnapshot()
    {
        var parent = new Panel { FontSize = 20 };
        var otherParent = new Panel { FontSize = 40 };
        var first = new Panel();
        var second = new Panel();
        parent.Children.AddRange([first, second]);
        using var firstSubscription = first.Subscribe(UiNode.FontSizeProperty,
            (_, _) => otherParent.Children.Add(second));
        var changes = new List<(double Old, double New)>();
        using var secondSubscription = second.Subscribe(UiNode.FontSizeProperty,
            (_, args) => changes.Add((args.OldValue, args.NewValue)));

        parent.FontSize = 30;

        Assert.Same(otherParent, second.Parent);
        Assert.Equal(40d, second.FontSize);
        Assert.Equal(new[] { (30d, 16d), (16d, 40d) }, changes);
    }

    private abstract class TestTreeHost : ObservableObject
    {
        private TestTreeHost? _parent;
        private readonly List<ObservableObject> _children = [];

        protected TestTreeHost(TestTreeHost? parent = null)
        {
            _parent = parent;
            parent?._children.Add(this);
        }

        protected override ObservableObject? InheritanceParent => _parent;
        protected override IReadOnlyList<ObservableObject> InheritanceChildren => _children;

        internal void SetParent(TestTreeHost? parent)
        {
            if (ReferenceEquals(_parent, parent))
                return;

            var oldParent = _parent;
            oldParent?._children.Remove(this);
            _parent = parent;
            parent?._children.Add(this);
            OnInheritanceParentChanged(oldParent);
        }
    }

    private sealed class PropertyOwner : ObservableObject
    {
    }

    private sealed class OtherOwner : ObservableObject
    {
    }

    private sealed class LateOwner : ObservableObject
    {
    }

    private sealed class PlainHost : TestTreeHost
    {
        internal static readonly Property<int> ValueProperty =
            Property.Register<PlainHost, int>("Value", 7, inherits: true);
        internal static readonly Property<int> OtherValueProperty =
            Property.Register<PlainHost, int>("OtherValue", 9, inherits: true);

        internal PlainHost(TestTreeHost? parent = null) : base(parent)
        {
        }
    }

    private sealed class CallbackHost : TestTreeHost
    {
        internal static readonly Property<int> ValueProperty =
            Property.Register<CallbackHost, int>("Value", 7,
                onChanged: static (node, _, _) => node.OnValueChanged?.Invoke(), inherits: true);

        internal Action? OnValueChanged { get; set; }
    }

    private sealed class InheritedPanel : Panel
    {
        static InheritedPanel()
        {
            UiCssRegistry.RegisterElement<InheritedPanel>("InheritedPanel");
            UiCssRegistry.RegisterProperty<InheritedPanel, double>(
                "inherited-number", NumberProperty, UiCssValueConverters.ParseNumber);
            UiCssRegistry.RegisterProperty<InheritedPanel, string?>(
                "inherited-label", LabelProperty, static value => value);
        }
    }

    private sealed class ScopedPanel : Panel
    {
        internal static readonly Property<int> ValueProperty =
            Property.Register<ScopedPanel, int>("ScopedValue", 5, inherits: true);

        static ScopedPanel()
        {
            UiCssRegistry.RegisterElement<ScopedPanel>("ScopedPanel");
            UiCssRegistry.RegisterProperty<ScopedPanel, int>(
                "scoped-value", ValueProperty, static value => (int)UiCssValueConverters.ParseNumber(value));
        }
    }

    private class BridgeRoot : TestTreeHost
    {
        private ScopedLeaf? _child;

        internal static readonly Property<int> TriggerProperty =
            Property.Register<BridgeRoot, int>(
                "Trigger",
                0,
                onChanged: static (node, _, _) => node.ReadChildValue(),
                inherits: true);

        internal void SetChild(ScopedLeaf child)
        {
            _child = child;
            child.SetParent(this);
        }

        private void ReadChildValue() => _ = _child?.GetValue(ScopedLeaf.ValueProperty);
    }

    private abstract class ScopedHost : BridgeRoot
    {
        internal static readonly Property<int> ValueProperty;

        static ScopedHost()
        {
            _ = TriggerProperty;
            ValueProperty = Property.Register<ScopedHost, int>("ScopedValue", 5, inherits: true);
        }
    }

    private sealed class ScopedRoot : ScopedHost
    {
    }

    private sealed class ScopedLeaf : ScopedHost
    {
    }

    private sealed class ReadOnlyPanel : Panel
    {
        private static readonly PropertyKey<string?> ValueKey =
            Property.RegisterReadOnly<ReadOnlyPanel, string?>("ReadOnlyValue", "fallback", inherits: true);

        internal static readonly Property<string?> ValueProperty = ValueKey.Property;
        internal string? Value => GetValue(ValueProperty);
        internal void SetLabel(string? value) => SetValue(ValueKey, value);
        internal void ClearLabel() => ClearValue(ValueKey);
    }
}
