using PanguEngine.Collections;
using PanguEngine.ComponentModel;
using PanguEngine.UI;
using PanguEngine.UI.Controls;
using PanguEngine.UI.Drawing;
using PanguEngine.UI.Styling;

namespace PanguEngine.Tests.UI.Styling;

public sealed class UIElementStylingTests
{
    private static readonly PseudoClass Loading = PseudoClass.Get("loading");
    private static readonly PseudoClass Phantom = PseudoClass.Get("phantom");

    [Fact]
    public void LocalValueMasksStyleAndClearRestoresStyle()
    {
        var button = new Button();
        ApplyTheme(button, Rule(StyleSelector.For<Button>(), Setter(Region.BackgroundProperty, Brush(1, 2, 3))));

        button.Background = new SolidColorBrush(4, 5, 6);
        Assert.Equal(new SolidColorBrush(4, 5, 6), button.Background);

        button.ClearValue(Region.BackgroundProperty);
        Assert.Equal(new SolidColorBrush(1, 2, 3), button.Background);
    }

    [Fact]
    public void BindingMasksStyleAndUnbindKeepsLocalUntilClear()
    {
        var source = new Button { Background = Brush(7) };
        var target = new Button();
        ApplyTheme(target, Rule(StyleSelector.For<Button>(), Setter(Region.BackgroundProperty, Brush(1))));
        target.Bind(Region.BackgroundProperty, source, Region.BackgroundProperty);

        source.Background = Brush(8);

        Assert.Equal(Brush(8), target.Background);
        Assert.True(Assert.Single(target.GetStyleValueSources(Region.BackgroundProperty)).IsMaskedByLocalValue);

        target.Unbind(Region.BackgroundProperty);
        source.Background = Brush(9);

        Assert.Equal(Brush(8), target.Background);
        target.ClearValue(Region.BackgroundProperty);
        Assert.Equal(Brush(1), target.Background);
        Assert.False(Assert.Single(target.GetStyleValueSources(Region.BackgroundProperty)).IsMaskedByLocalValue);
    }

    [Fact]
    public void LocalValueMasksStyleChangeWithoutNotification()
    {
        var button = new Button();
        ApplyTheme(button, Rule(StyleSelector.For<Button>([StyleSelectorCondition.Class("primary")]), Setter(Region.BackgroundProperty, Brush(1))));
        button.Background = new SolidColorBrush(7, 7, 7);

        var changes = 0;
        button.PropertyChanged += (_, e) =>
            changes += ReferenceEquals(e.Property, Region.BackgroundProperty) ? 1 : 0;

        button.Classes.Add("primary");

        button.GetStyleRoot().UpdateStyles();

        Assert.Equal(new SolidColorBrush(7, 7, 7), button.Background);
        Assert.Equal(0, changes);
    }

    [Fact]
    public void ClassMutationRecomputesOnlyOnRealChange()
    {
        var button = new Button();
        ApplyTheme(button, Rule(StyleSelector.For<Button>([StyleSelectorCondition.Class("primary")]), Setter(Region.BackgroundProperty, Brush(1))));

        var changes = 0;
        button.PropertyChanged += (_, e) =>
            changes += ReferenceEquals(e.Property, Region.BackgroundProperty) ? 1 : 0;

        Assert.True(button.Classes.Add("primary"));
        button.GetStyleRoot().UpdateStyles();
        Assert.False(button.Classes.Add("primary"));
        button.GetStyleRoot().UpdateStyles();
        Assert.Equal(1, changes);
    }

    [Fact]
    public void StyleIdChangeRecomputesOnlyOnRealChange()
    {
        var button = new Button();
        ApplyTheme(button, Rule(StyleSelector.For<Button>([StyleSelectorCondition.Id("save")]), Setter(Region.BackgroundProperty, Brush(1))));

        var changes = 0;
        button.PropertyChanged += (_, e) =>
            changes += ReferenceEquals(e.Property, Region.BackgroundProperty) ? 1 : 0;

        button.StyleId = "save";
        button.GetStyleRoot().UpdateStyles();
        Assert.Equal(1, changes);

        button.StyleId = "save";
        button.GetStyleRoot().UpdateStyles();
        Assert.Equal(1, changes);

        button.StyleId = "other";
        button.GetStyleRoot().UpdateStyles();
        Assert.Equal(2, changes);
    }

    [Fact]
    public void PseudoClassChangeTriggersStyleRefresh()
    {
        var button = new Button();
        ApplyTheme(button, Rule(
            StyleSelector.For<Button>([StyleSelectorCondition.Pseudo(PseudoClass.Hover)]),
            Setter(Region.BackgroundProperty, Brush(9))));

        Assert.Equal(new SolidColorBrush(48, 54, 62), button.Background);

        button.SetHovered(true);
        button.GetStyleRoot().UpdateStyles();
        Assert.Equal(Brush(9), button.Background);

        button.SetHovered(false);
        button.GetStyleRoot().UpdateStyles();
        Assert.Equal(new SolidColorBrush(48, 54, 62), button.Background);
    }

    [Fact]
    public void EarlierPropertyHandlerCanLocallyMaskLaterStyleNotification()
    {
        var button = new Button();
        var scene = new UIScene(button);
        var changes = new List<PropertyChangedEventArgs>();
        button.PropertyChanged += (_, e) =>
        {
            if (!ReferenceEquals(e.Property, Region.BackgroundProperty) &&
                !ReferenceEquals(e.Property, Region.BorderBrushProperty))
                return;
            changes.Add(e);
            if (changes.Count == 1)
                button.SetValue(ReferenceEquals(e.Property, Region.BackgroundProperty)
                    ? Region.BorderBrushProperty
                    : Region.BackgroundProperty, Brush(9));
        };
        var sheet = new StyleSheet([
            Rule(StyleSelector.For<Button>(), Setter(Region.BackgroundProperty, Brush(1))),
            Rule(StyleSelector.For<Button>(), Setter(Region.BorderBrushProperty, Brush(2)))
        ]);

        scene.SetStyleSheets([sheet]);
        scene.Root!.UpdateStyles();

        Assert.Equal(2, changes.Count);
        Assert.NotSame(changes[0].Property, changes[1].Property);
        Assert.Equal(Brush(9), changes[1].NewValue);
        var maskedProperty = ReferenceEquals(changes[0].Property, Region.BackgroundProperty)
            ? Region.BorderBrushProperty
            : Region.BackgroundProperty;
        Assert.Equal(Brush(9), button.GetValue(maskedProperty));
    }

    [Fact]
    public void ClassSwitchLocallyMasksLaterStyleNotification()
    {
        var button = new Button();
        ApplyTheme(
            button,
            Rule(
                StyleSelector.For<Button>(),
                Setter(Region.BackgroundProperty, Brush(1)),
                Setter(Region.BorderBrushProperty, Brush(2))),
            Rule(
                StyleSelector.For<Button>([StyleSelectorCondition.Class("primary")]),
                Setter(Region.BackgroundProperty, Brush(5)),
                Setter(Region.BorderBrushProperty, Brush(6))));

        var changes = new List<PropertyChangedEventArgs>();
        button.PropertyChanged += (_, e) =>
        {
            if (!ReferenceEquals(e.Property, Region.BackgroundProperty) &&
                !ReferenceEquals(e.Property, Region.BorderBrushProperty))
                return;
            changes.Add(e);
            if (changes.Count == 1)
                button.SetValue(ReferenceEquals(e.Property, Region.BackgroundProperty)
                    ? Region.BorderBrushProperty
                    : Region.BackgroundProperty, Brush(9));
        };

        button.Classes.Add("primary");
        button.GetStyleRoot().UpdateStyles();

        Assert.Equal(2, changes.Count);
        Assert.NotSame(changes[0].Property, changes[1].Property);
        Assert.Equal(Brush(9), changes[1].NewValue);
        var maskedProperty = ReferenceEquals(changes[0].Property, Region.BackgroundProperty)
            ? Region.BorderBrushProperty
            : Region.BackgroundProperty;
        Assert.Equal(Brush(9), button.GetValue(maskedProperty));
    }

    [Fact]
    public void StyleChangeExceptionStopsRemainingNotifications()
    {
        var button = new Button();
        var notified = new List<Property>();
        var failure = new InvalidOperationException("style-fail");
        button.PropertyChanged += (_, e) =>
        {
            if (ReferenceEquals(e.Property, Region.BackgroundProperty) ||
                ReferenceEquals(e.Property, Region.BorderBrushProperty))
            {
                notified.Add(e.Property);
                throw failure;
            }
        };

        var exception = Assert.Throws<InvalidOperationException>(() => ApplyTheme(
            button,
            Rule(StyleSelector.For<Button>(), Setter(Region.BackgroundProperty, Brush(1))),
            Rule(StyleSelector.For<Button>(), Setter(Region.BorderBrushProperty, Brush(2)))));

        Assert.Same(failure, exception);
        Assert.Single(notified);
        Assert.Equal(Brush(1), button.Background);
        Assert.Equal(Brush(2), button.BorderBrush);
        Assert.False(button.IsUpdatingStyles);
        Assert.False(button.Scene!.IsUpdatingLayout);
    }

    [Fact]
    public void FailedNodeUpdateKeepsEarlierUpdatesAndLeavesRemainingNodesPending()
    {
        var root = new Panel();
        var first = new Panel();
        var failing = new GuardedStyleNode();
        var last = new Panel();
        root.Children.Add(first);
        root.Children.Add(failing);
        root.Children.Add(last);
        var scene = new UIScene(root);
        scene.SetStyleSheets([
            new StyleSheet([
                Rule(StyleSelector.For<Panel>(), Setter(UIElement.OpacityProperty, 0.4)),
                Rule(StyleSelector.For<GuardedStyleNode>(), Setter(GuardedStyleNode.ValueProperty, new GuardedValue(0.5)))
            ])
        ]);
        var firstNotifications = 0;
        first.PropertyChanged += (_, args) =>
            firstNotifications += args.Property == UIElement.OpacityProperty ? 1 : 0;

        GuardedValue.ThrowOnCompare = true;
        try
        {
            Assert.Throws<InvalidOperationException>(scene.Root!.UpdateStyles);
        }
        finally
        {
            GuardedValue.ThrowOnCompare = false;
        }

        Assert.Equal(0.4, first.Opacity);
        Assert.Equal(0d, failing.GetValue(GuardedStyleNode.ValueProperty).Number);
        Assert.Equal(1d, last.Opacity);
        Assert.False(root.IsStyleSubtreeValid);
        Assert.False(failing.IsStyleSubtreeValid);
        Assert.False(root.IsUpdatingStyles);
        Assert.False(scene.IsUpdatingLayout);

        scene.Root!.UpdateStyles();

        Assert.Equal(0.5, failing.GetValue(GuardedStyleNode.ValueProperty).Number);
        Assert.Equal(0.4, last.Opacity);
        Assert.Equal(1, firstNotifications);
        Assert.True(root.IsStyleSubtreeValid);
    }

    [Fact]
    public void LaterChildUpdateDoesNotClearAncestorInvalidation()
    {
        var root = new Panel();
        var first = new Panel { StyleId = "first" };
        var last = new Panel { StyleId = "last" };
        root.Children.Add(first);
        root.Children.Add(last);
        var scene = new UIScene(root);
        scene.SetStyleSheets([StyleSheet.Parse("#first.active, #last.active { opacity: 0.4; }")]);
        scene.Root!.UpdateStyles();
        var notifications = 0;
        first.PropertyChanged += (_, args) =>
        {
            if (args.Property != UIElement.OpacityProperty)
                return;
            notifications++;
            last.Classes.Add("active");
        };

        first.Classes.Add("active");
        scene.Root!.UpdateStyles();

        Assert.Equal(0.4, last.Opacity);
        Assert.True(first.IsStyleSubtreeValid);
        Assert.True(last.IsStyleSubtreeValid);
        Assert.True(root.IsStyleValid);
        Assert.False(root.IsStyleSubtreeValid);

        scene.Root!.UpdateStyles();

        Assert.True(root.IsStyleSubtreeValid);
        Assert.Equal(1, notifications);
    }

    [Fact]
    public void NotificationFailureStopsBeforeUpdatingLaterNodes()
    {
        var root = new Panel();
        var first = new Panel();
        var last = new Panel();
        root.Children.Add(first);
        root.Children.Add(last);
        var scene = new UIScene(root);
        scene.SetStyleSheets([StyleSheet.Parse("Panel { opacity: 0.4; }")]);
        var expected = new InvalidOperationException("notification failed");
        first.PropertyChanged += (_, args) =>
        {
            if (args.Property == UIElement.OpacityProperty)
                throw expected;
        };

        Assert.Same(expected, Assert.Throws<InvalidOperationException>(scene.Root!.UpdateStyles));
        Assert.Equal(0.4, first.Opacity);
        Assert.Equal(1d, last.Opacity);
        Assert.False(root.IsStyleSubtreeValid);

        Assert.False(root.IsUpdatingStyles);
        Assert.False(first.IsUpdatingStyles);
        Assert.False(last.IsUpdatingStyles);

        scene.Root!.UpdateStyles();

        Assert.Equal(0.4, last.Opacity);
        Assert.True(root.IsStyleSubtreeValid);
    }

    [Fact]
    public void StandaloneStyleNotificationCannotReenterTheSameTree()
    {
        var button = new Button();
        Exception? error = null;
        button.PropertyChanged += (_, args) =>
        {
            if (args.Property != Region.BackgroundProperty)
                return;
            Assert.True(button.IsUpdatingStyles);
            error = Record.Exception(button.UpdateStyles);
        };

        button.UpdateStyles();

        Assert.IsType<InvalidOperationException>(error);
        Assert.False(button.IsUpdatingStyles);
        Assert.True(button.IsStyleSubtreeValid);
    }

    [Fact]
    public void RemovedLaterNodeIsNotUpdatedByTheOldTree()
    {
        var root = new Panel();
        var first = new Panel();
        var last = new Button();
        root.Children.Add(first);
        root.Children.Add(last);
        var scene = new UIScene(root);
        scene.SetStyleSheets([StyleSheet.Parse("Panel { opacity: 0.4; } Button { background: #010203; }")]);
        first.PropertyChanged += (_, args) =>
        {
            if (args.Property == UIElement.OpacityProperty)
                root.Children.Remove(last);
        };

        scene.Root!.UpdateStyles();

        Assert.Null(last.Parent);
        Assert.Null(last.Scene);
        Assert.Null(last.Background);
        Assert.False(last.IsStyleSubtreeValid);
        last.UpdateStyles();
        Assert.Equal(new SolidColorBrush(48, 54, 62), last.Background);
    }

    [Fact]
    public void ReentrantStyleChangeRemainsPendingUntilTheNextApplication()
    {
        var button = new Button();
        ApplyTheme(
            button,
            Rule(StyleSelector.For<Button>([StyleSelectorCondition.Class("x")]), Setter(Region.BackgroundProperty, Brush(5))),
            Rule(StyleSelector.For<Button>([StyleSelectorCondition.Class("extra")]), Setter(Region.BackgroundProperty, Brush(9))));

        var backgroundNotifications = 0;
        var collectionNotifications = 0;
        button.Classes.Changed += (_, _) => collectionNotifications++;
        button.PropertyChanged += (_, e) =>
        {
            if (!ReferenceEquals(e.Property, Region.BackgroundProperty))
                return;
            backgroundNotifications++;
            if (!button.Classes.Contains("extra"))
                button.Classes.Add("extra");
        };

        button.Classes.Add("x");
        button.GetStyleRoot().UpdateStyles();

        Assert.Equal(Brush(5), button.Background);
        Assert.Equal(1, backgroundNotifications);
        button.GetStyleRoot().UpdateStyles();

        Assert.Equal(Brush(9), button.Background);
        Assert.True(button.Classes.SetEquals(["x", "extra"]));
        Assert.Equal(2, backgroundNotifications);
        Assert.Equal(2, collectionNotifications);
    }

    [Fact]
    public void UnhandledClassListenerReentrancyStopsNotificationAndKeepsStylesPending()
    {
        var button = new Button();
        ApplyTheme(
            button,
            Rule(StyleSelector.For<Button>([StyleSelectorCondition.Class("x")]), Setter(Region.BackgroundProperty, Brush(5))),
            Rule(StyleSelector.For<Button>([StyleSelectorCondition.Class("extra")]), Setter(Region.BackgroundProperty, Brush(9))));
        var notifications = 0;
        var collectionNotifications = 0;
        var attemptReentrancy = true;
        button.Classes.Changed += (_, _) =>
        {
            if (attemptReentrancy)
                button.Classes.Add("extra");
        };
        button.Classes.Changed += (_, _) => collectionNotifications++;
        button.PropertyChanged += (_, e) =>
        {
            if (!ReferenceEquals(e.Property, Region.BackgroundProperty))
                return;
            notifications++;
        };

        Assert.Throws<InvalidOperationException>(() => button.Classes.Add("x"));

        Assert.Equal(new SolidColorBrush(48, 54, 62), button.Background);
        Assert.True(button.Classes.SetEquals(["x"]));
        Assert.Equal(0, notifications);
        Assert.Equal(0, collectionNotifications);
        button.GetStyleRoot().UpdateStyles();
        Assert.Equal(Brush(5), button.Background);
        Assert.Equal(1, notifications);

        attemptReentrancy = false;
        button.Classes.Add("extra");
        button.GetStyleRoot().UpdateStyles();
        Assert.Equal(Brush(9), button.Background);
        Assert.Equal(2, notifications);
        Assert.Equal(1, collectionNotifications);
    }

    [Fact]
    public void NotificationFailureStopsPendingRecompute()
    {
        var expected = new InvalidOperationException("first notification failed");
        var button = new Button();
        ApplyTheme(
            button,
            Rule(StyleSelector.For<Button>([StyleSelectorCondition.Class("x")]), Setter(Region.BackgroundProperty, Brush(5))),
            Rule(StyleSelector.For<Button>([StyleSelectorCondition.Class("extra")]), Setter(Region.BackgroundProperty, Brush(9))));
        var notifications = 0;
        button.PropertyChanged += (_, e) =>
        {
            if (!ReferenceEquals(e.Property, Region.BackgroundProperty))
                return;
            notifications++;
            if (notifications == 1)
            {
                button.Classes.Add("extra");
                throw expected;
            }
        };

        button.Classes.Add("x");
        var actual = Assert.Throws<InvalidOperationException>(button.GetStyleRoot().UpdateStyles);

        Assert.Same(expected, actual);
        Assert.Equal(Brush(5), button.Background);
        Assert.True(button.Classes.Contains("extra"));
        Assert.Equal(1, notifications);
    }

    [Fact]
    public void ClassSetBatchPublishesOneChangeBeforeStylesAreApplied()
    {
        var node = new Canvas();
        var scene = new UIScene(node);
        scene.SetStyleSheets([StyleSheet.Parse(".primary.wide { opacity: 0.4; }")]);
        scene.Root!.UpdateStyles();
        IObservableSet<string> classes = node.Classes;
        var order = new List<string>();
        var changes = new List<SetChangedEventArgs<string>>();
        node.PropertyChanged += (_, e) =>
        {
            if (ReferenceEquals(e.Property, UIElement.OpacityProperty))
                order.Add("style");
        };
        classes.Changed += (sender, change) =>
        {
            Assert.Same(node.Classes, sender);
            Assert.Equal(1d, node.Opacity);
            Assert.False(node.IsStyleValid);
            Assert.True(classes.SetEquals(["primary", "wide"]));
            order.Add("set");
            changes.Add(change);
        };

        classes.UnionWith(["primary", "wide", "primary"]);

        Assert.Equal(new[] { "set" }, order);
        scene.Root!.UpdateStyles();
        Assert.Equal(0.4, node.Opacity);
        Assert.Equal(new[] { "set", "style" }, order);
        var change = Assert.Single(changes);
        Assert.True(new HashSet<string>(change.AddedItems).SetEquals(["primary", "wide"]));
        Assert.Empty(change.RemovedItems);
        Assert.False(classes.Add("primary"));
        Assert.Single(changes);
        Assert.True(node.IsStyleValid);
        Assert.Same(StringComparer.Ordinal, classes.Comparer);
    }

    [Fact]
    public void ClassSetUsesOrdinalMembershipAndProvidesALiveReadOnlyView()
    {
        var node = new Canvas();
        var view = node.Classes.AsReadOnly();

        Assert.True(node.Classes.Add("primary"));
        Assert.True(node.Classes.Add("Primary"));
        Assert.False(node.Classes.Add("primary"));
        Assert.Equal(2, view.Count);
        Assert.True(view.SetEquals(["primary", "Primary"]));
        Assert.Same(StringComparer.Ordinal, view.Comparer);
    }

    [Theory]
    [InlineData("invalid class")]
    [InlineData("")]
    [InlineData(null)]
    public void ClassSetAcceptsNamesWithoutSelectorValidation(string? className)
    {
        var node = new Canvas();
        var scene = new UIScene(node);
        scene.SetStyleSheets([StyleSheet.Parse(".valid { opacity: 0.4; }")]);
        node.Classes.Add("kept");
        var changes = new List<SetChangedEventArgs<string>>();
        node.Classes.Changed += (_, change) => changes.Add(change);

        node.Classes.UnionWith(["valid", className!]);
        scene.Root!.UpdateStyles();

        Assert.True(node.Classes.SetEquals(["kept", "valid", className!]));
        Assert.Equal(0.4, node.Opacity);
        Assert.True(new HashSet<string>(Assert.Single(changes).AddedItems).SetEquals(["valid", className!]));
        node.Classes.SymmetricExceptWith(["kept", "valid", className!, className!]);
        scene.Root!.UpdateStyles();
        Assert.Empty(node.Classes);
        Assert.Equal(1d, node.Opacity);
        Assert.Equal(2, changes.Count);
    }

    [Fact]
    public void ClassSetCalculationFailureKeepsTheWholeBatchAndAlreadyPublishedChange()
    {
        var node = new GuardedStyleNode();
        var scene = new UIScene(node);
        scene.SetStyleSheets([
            new StyleSheet([
                Rule(StyleSelector.For<GuardedStyleNode>(),
                    Setter(GuardedStyleNode.ValueProperty, new GuardedValue(0.5)))
            ])
        ]);
        node.Classes.Add("kept");
        scene.Root!.UpdateStyles();
        var previousSource = Assert.Single(node.GetStyleValueSources(GuardedStyleNode.ValueProperty));
        var notifications = 0;
        node.Classes.Changed += (_, _) => notifications++;
        GuardedValue.ThrowOnCompare = true;
        try
        {
            node.Classes.UnionWith(["first", "second"]);
            Assert.Throws<InvalidOperationException>(scene.Root!.UpdateStyles);
        }
        finally
        {
            GuardedValue.ThrowOnCompare = false;
        }

        Assert.True(node.Classes.SetEquals(["kept", "first", "second"]));
        Assert.Equal(1, notifications);
        Assert.Same(scene, node.Scene);
        Assert.Same(previousSource, Assert.Single(node.GetStyleValueSources(GuardedStyleNode.ValueProperty)));
        node.Classes.UnionWith(["first", "second"]);
        Assert.Equal(1, notifications);
        Assert.False(node.IsStyleValid);
        Assert.True(node.Classes.Remove("first"));
        Assert.Equal(2, notifications);
        scene.Root!.UpdateStyles();
        Assert.True(node.IsStyleValid);
    }

    [Fact]
    public void ClassSetStyleNotificationFailureKeepsCommittedStateAndPublishedSetChange()
    {
        var expected = new InvalidOperationException("style notification failed");
        var node = new Canvas();
        var scene = new UIScene(node);
        scene.SetStyleSheets([StyleSheet.Parse(".primary { opacity: 0.4; }")]);
        var failNotification = true;
        var collectionNotifications = 0;
        node.PropertyChanged += (_, e) =>
        {
            if (ReferenceEquals(e.Property, UIElement.OpacityProperty) && failNotification)
                throw expected;
        };
        node.Classes.Changed += (_, _) => collectionNotifications++;

        node.Classes.Add("primary");
        Assert.Same(expected, Assert.Throws<InvalidOperationException>(scene.Root!.UpdateStyles));

        Assert.True(node.Classes.Contains("primary"));
        Assert.Equal(0.4, node.Opacity);
        Assert.Equal(1, collectionNotifications);
        failNotification = false;
        node.Classes.Remove("primary");
        scene.Root!.UpdateStyles();
        Assert.Equal(1d, node.Opacity);
        Assert.Equal(2, collectionNotifications);
    }

    [Fact]
    public void ClassSetListenerFailureLeavesStylesPendingAndReleasesMutationProtection()
    {
        var expected = new InvalidOperationException("class listener failed");
        var node = new Canvas();
        var scene = new UIScene(node);
        scene.SetStyleSheets([StyleSheet.Parse(".primary { opacity: 0.4; }")]);
        scene.Root!.UpdateStyles();
        var laterCalls = 0;
        EventHandler<SetChangedEventArgs<string>> failing = (_, _) => throw expected;
        node.Classes.Changed += failing;
        node.Classes.Changed += (_, _) => laterCalls++;

        Assert.Same(expected, Assert.Throws<InvalidOperationException>(() => node.Classes.Add("primary")));

        Assert.True(node.Classes.Contains("primary"));
        Assert.Equal(1d, node.Opacity);
        Assert.False(node.IsStyleValid);
        Assert.Equal(0, laterCalls);
        scene.Root!.UpdateStyles();
        Assert.Equal(0.4, node.Opacity);
        node.Classes.Changed -= failing;
        node.Classes.Remove("primary");
        scene.Root!.UpdateStyles();
        Assert.Equal(1d, node.Opacity);
        Assert.Equal(1, laterCalls);
    }

    [Fact]
    public void ClassSetListenerCannotReenterEvenForAnUnchangedRequest()
    {
        var node = new Canvas();
        var notifications = 0;
        Action[] mutations =
        [
            () => node.Classes.Add("primary"),
            () => node.Classes.Add("invalid class"),
            () => node.Classes.Add(null!),
            () => node.Classes.Remove("absent"),
            node.Classes.Clear,
            () => node.Classes.UnionWith(null!),
            () => node.Classes.IntersectWith(["primary"]),
            () => node.Classes.ExceptWith([]),
            () => node.Classes.SymmetricExceptWith([]),
            () => ((ICollection<string>)node.Classes).Add("primary")
        ];
        node.Classes.Changed += (_, _) =>
        {
            notifications++;
            foreach (var mutation in mutations)
                Assert.Throws<InvalidOperationException>(mutation);
        };

        node.Classes.Add("primary");

        Assert.True(node.Classes.SetEquals(["primary"]));
        Assert.Equal(1, notifications);
    }

    [Fact]
    public void StandardCollectionInterfacesUseNormalClassSetSemantics()
    {
        var node = new Canvas();
        ISet<string> set = node.Classes;
        ICollection<string> collection = node.Classes;

        Assert.True(set.Add("invalid class"));
        collection.Add(null!);
        set.UnionWith(["valid", "invalid class"]);

        Assert.True(set.SetEquals(["valid", "invalid class", null!]));
        set.SymmetricExceptWith(["valid", "invalid class"]);
        Assert.Null(Assert.Single(set));
    }

    [Fact]
    public void ClassSetRemovalOperationsAllowInvalidLookupValues()
    {
        var node = new Canvas();
        node.Classes.UnionWith(["kept", "removed"]);
        var notifications = 0;
        node.Classes.Changed += (_, _) => notifications++;

        node.Classes.IntersectWith(["kept", "invalid class", null!]);
        node.Classes.ExceptWith(["invalid class", null!]);
        Assert.False(node.Classes.Remove(null!));

        Assert.Equal("kept", Assert.Single(node.Classes));
        Assert.Equal(1, notifications);
    }

    [Fact]
    public void ClassSetInputPreparationCannotReenterTheSet()
    {
        var node = new Canvas();
        node.Classes.Add("kept");

        IEnumerable<string> Input()
        {
            yield return "next";
            node.Classes.Clear();
        }

        Assert.Throws<InvalidOperationException>(() => node.Classes.UnionWith(Input()));

        Assert.Equal("kept", Assert.Single(node.Classes));
        Assert.True(node.Classes.Add("next"));
    }

    [Fact]
    public void StyleNotificationSubscriptionChangesAffectSubsequentClassEvents()
    {
        var node = new Canvas();
        var scene = new UIScene(node);
        scene.SetStyleSheets([StyleSheet.Parse(".primary { opacity: 0.4; }")]);
        var removedCalls = 0;
        var addedCalls = 0;
        var changedSubscription = false;
        EventHandler<SetChangedEventArgs<string>> removed = (_, _) => removedCalls++;
        EventHandler<SetChangedEventArgs<string>> added = (_, _) => addedCalls++;
        node.Classes.Changed += removed;
        node.PropertyChanged += (_, e) =>
        {
            if (!ReferenceEquals(e.Property, UIElement.OpacityProperty) || changedSubscription)
                return;
            changedSubscription = true;
            node.Classes.Changed -= removed;
            node.Classes.Changed += added;
        };

        node.Classes.Add("primary");

        Assert.Equal(1, removedCalls);
        Assert.Equal(0, addedCalls);
        scene.Root!.UpdateStyles();
        node.Classes.Remove("primary");
        Assert.Equal(1, removedCalls);
        Assert.Equal(1, addedCalls);
    }

    [Fact]
    public void StyleIdNotificationClassChangeLeavesNestedStylesPending()
    {
        var node = new Canvas();
        var scene = new UIScene(node);
        scene.SetStyleSheets([StyleSheet.Parse("#trigger { opacity: 0.4; } #trigger.extra { opacity: 0.9; }")]);
        var order = new List<(string Kind, double Opacity)>();
        node.PropertyChanged += (_, e) =>
        {
            if (!ReferenceEquals(e.Property, UIElement.OpacityProperty))
                return;
            order.Add(("style", node.Opacity));
            if (!node.Classes.Contains("extra"))
                node.Classes.Add("extra");
        };
        node.Classes.Changed += (_, _) => order.Add(("set", node.Opacity));

        node.StyleId = "trigger";
        scene.Root!.UpdateStyles();

        Assert.True(node.Classes.Contains("extra"));
        Assert.Equal(0.4, node.Opacity);
        Assert.Equal(new[] { ("style", 0.4), ("set", 0.4) }, order);
        scene.Root!.UpdateStyles();
        Assert.Equal(0.9, node.Opacity);
        Assert.Equal(new[] { ("style", 0.4), ("set", 0.4), ("style", 0.9) }, order);
    }

    [Fact]
    public void PseudoClassNotificationClassChangePublishesBeforeTheNextStyleUpdate()
    {
        var node = new PseudoClassHost();
        var scene = new UIScene(node);
        scene.SetStyleSheets([StyleSheet.Parse(":loading { opacity: 0.4; } :loading.extra { opacity: 0.9; }")]);
        var styleNotifications = 0;
        var collectionNotifications = 0;
        node.PropertyChanged += (_, e) =>
        {
            if (!ReferenceEquals(e.Property, UIElement.OpacityProperty))
                return;
            styleNotifications++;
            if (!node.Classes.Contains("extra"))
                node.Classes.Add("extra");
            else
                Assert.False(node.Classes.Add("extra"));
        };
        node.Classes.Changed += (_, _) =>
        {
            Assert.Equal(0.4, node.Opacity);
            Assert.Equal(1, styleNotifications);
            collectionNotifications++;
        };

        node.Set(Loading, true);
        scene.Root!.UpdateStyles();

        Assert.True(node.Classes.SetEquals(["extra"]));
        Assert.Equal(0.4, node.Opacity);
        Assert.Equal(1, styleNotifications);
        scene.Root!.UpdateStyles();
        Assert.Equal(0.9, node.Opacity);
        Assert.Equal(2, styleNotifications);
        Assert.Equal(1, collectionNotifications);
    }

    [Fact]
    public void NestedClassStyleNotificationFailureKeepsCommittedStateAndPublishedSetChange()
    {
        var expected = new InvalidOperationException("nested style notification failed");
        var node = new Canvas();
        var scene = new UIScene(node);
        scene.SetStyleSheets([StyleSheet.Parse("#trigger { opacity: 0.4; } #trigger.extra { opacity: 0.9; }")]);
        var reactToNotification = true;
        var collectionNotifications = 0;
        node.PropertyChanged += (_, e) =>
        {
            if (!ReferenceEquals(e.Property, UIElement.OpacityProperty) || !reactToNotification)
                return;
            if (!node.Classes.Contains("extra"))
                node.Classes.Add("extra");
            else
                throw expected;
        };
        node.Classes.Changed += (_, _) => collectionNotifications++;

        node.StyleId = "trigger";
        scene.Root!.UpdateStyles();
        Assert.Equal(0.4, node.Opacity);
        Assert.Equal(1, collectionNotifications);
        Assert.Same(expected, Assert.Throws<InvalidOperationException>(scene.Root!.UpdateStyles));

        Assert.True(node.Classes.SetEquals(["extra"]));
        Assert.Equal(0.9, node.Opacity);
        Assert.Equal(1, collectionNotifications);
        reactToNotification = false;
        node.Classes.Remove("extra");
        scene.Root!.UpdateStyles();
        Assert.Equal(0.4, node.Opacity);
        Assert.Equal(2, collectionNotifications);
    }

    [Fact]
    public void FreshButtonReadUsesDefaultsUntilStylesAreApplied()
    {
        var button = new Button();

        Assert.Empty(button.GetStyleValueSources(Region.BackgroundProperty));
        Assert.Null(button.Background);
        button.GetStyleRoot().UpdateStyles();
        Assert.Equal("Button", Assert.Single(button.GetStyleValueSources(Region.BackgroundProperty)).SelectorText);
        Assert.Equal(new SolidColorBrush(48, 54, 62), button.Background);
    }

    [Fact]
    public void StandaloneLayoutDoesNotUpdateStyles()
    {
        var button = new Button();

        button.Measure(Size.Infinite);
        button.Arrange(new Rect(0, 0, button.DesiredSize));

        Assert.Equal(Size.Zero, button.DesiredSize);
        Assert.Null(button.Background);
        Assert.Empty(button.GetStyleValueSources(Region.BackgroundProperty));

        button.GetStyleRoot().UpdateStyles();
        Assert.False(button.IsMeasureValid);
        button.Measure(Size.Infinite);
        Assert.Equal(new Size(26, 16), button.DesiredSize);
    }

    [Fact]
    public void SourceReportsWinningDeclarationAndLocalMask()
    {
        var button = new Button();
        ApplyTheme(
            button,
            Rule(
                StyleSelector.For<Button>([StyleSelectorCondition.Class("primary")]),
                Setter(Region.BackgroundProperty, Brush(1, 2, 3))));
        button.Classes.Add("primary");
        button.Background = new SolidColorBrush(9, 9, 9);
        button.GetStyleRoot().UpdateStyles();

        var source = Assert.Single(button.GetStyleValueSources(Region.BackgroundProperty));

        Assert.True(source.IsMaskedByLocalValue);
        Assert.Equal("Button.primary", source.SelectorText);
        Assert.Equal(StyleOrigin.Author, source.Origin);
        Assert.Equal(0, source.SheetIndex);
    }

    [Fact]
    public void SourceReportsWinningBranchFromSelectorList()
    {
        var button = new Button();
        var scene = new UIScene(button);
        scene.SetStyleSheets([StyleSheet.Parse(".plain, #target { opacity: 0.4; }")]);
        button.Classes.Add("plain");
        button.GetStyleRoot().UpdateStyles();
        Assert.Equal(".plain", Assert.Single(button.GetStyleValueSources(UIElement.OpacityProperty)).SelectorText);
        button.StyleId = "target";
        button.Opacity = 0.9;
        button.GetStyleRoot().UpdateStyles();
        var source = Assert.Single(button.GetStyleValueSources(UIElement.OpacityProperty));
        Assert.Equal("#target", source.SelectorText);
        Assert.True(source.IsMaskedByLocalValue);
        button.StyleId = null;
        button.GetStyleRoot().UpdateStyles();
        Assert.Equal(".plain", Assert.Single(button.GetStyleValueSources(UIElement.OpacityProperty)).SelectorText);
        button.ClearValue(UIElement.OpacityProperty);
        Assert.Equal(0.4, button.Opacity);
        button.Classes.Remove("plain");
        button.GetStyleRoot().UpdateStyles();
        Assert.Equal(1d, button.Opacity);
    }

    [Fact]
    public void SourceQueryReturnsEmptyWithoutDeclarationAndRejectsWrongTarget()
    {
        var node = new Canvas();

        Assert.Empty(node.GetStyleValueSources(UIElement.WidthProperty));
        Assert.Throws<ArgumentException>(() =>
            node.GetStyleValueSources(Text.ContentProperty));
    }

    [Fact]
    public void StyleSelectorInputsRemainCommittedWhenApplicationFails()
    {
        var node = new GuardedStyleNode { StyleId = "old" };
        var sheet = new StyleSheet([
            Rule(
                StyleSelector.For<GuardedStyleNode>(),
                Setter(GuardedStyleNode.ValueProperty, new GuardedValue(0.5)))
        ]);
        var scene = new UIScene(node);
        scene.SetStyleSheets([sheet]);
        scene.Root!.UpdateStyles();
        GuardedValue.ThrowOnCompare = true;
        try
        {
            node.StyleId = "new";
            node.Classes.Add("primary");
            Assert.Throws<InvalidOperationException>(node.GetStyleRoot().UpdateStyles);
        }
        finally
        {
            GuardedValue.ThrowOnCompare = false;
        }

        Assert.Equal("new", node.StyleId);
        Assert.Equal("primary", Assert.Single(node.Classes));
        Assert.Same(scene, node.Scene);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SelectorChangesDuringNodeCalculationRemainPending(bool changeId)
    {
        var node = new GuardedStyleNode { StyleId = "original" };
        var scene = new UIScene(node);
        scene.SetStyleSheets([
            new StyleSheet([
                Rule(
                    StyleSelector.For<GuardedStyleNode>(),
                    Setter(GuardedStyleNode.ValueProperty, new GuardedValue(0.5)))
            ])
        ]);
        GuardedValue.OnCompare = () =>
        {
            if (changeId)
                node.StyleId = "nested";
            else
                node.Classes.Add("nested");
        };
        try
        {
            node.Classes.Add("outer");
            node.GetStyleRoot().UpdateStyles();
        }
        finally
        {
            GuardedValue.OnCompare = null;
        }

        Assert.Equal(changeId ? "nested" : "original", node.StyleId);
        Assert.Equal(!changeId, node.Classes.Contains("nested"));
        Assert.False(node.IsStyleSubtreeValid);
        node.GetStyleRoot().UpdateStyles();
        Assert.True(node.IsStyleSubtreeValid);
    }

    [Fact]
    public void StyleComparisonUsesThePropertyValueEqualityContract()
    {
        var node = new EquatableNode();
        var firstSheet = new StyleSheet([
            Rule(
                StyleSelector.For<EquatableNode>(),
                Setter(EquatableNode.ValueProperty, new EquatableValue(1)))
        ]);
        var secondSheet = new StyleSheet([
            Rule(
                StyleSelector.For<EquatableNode>(),
                Setter(EquatableNode.ValueProperty, new EquatableValue(1)))
        ]);
        var scene = new UIScene(node);
        scene.SetStyleSheets([firstSheet]);
        scene.Root!.UpdateStyles();
        var notifications = 0;
        node.PropertyChanged += (_, e) =>
            notifications += ReferenceEquals(e.Property, EquatableNode.ValueProperty) ? 1 : 0;

        scene.SetStyleSheets([secondSheet]);
        scene.Root!.UpdateStyles();

        Assert.Equal(0, notifications);
    }

    [Fact]
    public void PseudoClassHandlerExceptionStillInvalidatesStyle()
    {
        var button = new Button();
        ApplyTheme(button, Rule(
            StyleSelector.For<Button>([StyleSelectorCondition.Pseudo(PseudoClass.Hover)]),
            Setter(Region.BackgroundProperty, Brush(9))));
        button.PropertyChanged += (_, e) =>
        {
            if (ReferenceEquals(e.Property, UIElement.IsHoveredProperty))
                throw new InvalidOperationException("hover-handler-fail");
        };

        Assert.Throws<InvalidOperationException>(() => button.SetHovered(true));

        Assert.True(button.HasPseudoClass(PseudoClass.Hover));
        Assert.NotEqual(Brush(9), button.Background);
        button.GetStyleRoot().UpdateStyles();
        Assert.Equal(Brush(9), button.Background);
    }

    [Fact]
    public void PseudoClassCalculationFailureKeepsOldSnapshotUntilExplicitRetry()
    {
        var node = new GuardedStyleNode();
        ApplyTheme(
            node,
            Rule(
                StyleSelector.For<GuardedStyleNode>(),
                Setter(UIElement.OpacityProperty, 0.25)),
            Rule(
                StyleSelector.For<GuardedStyleNode>(),
                Setter(GuardedStyleNode.ValueProperty, new GuardedValue(0.5))),
            Rule(
                StyleSelector.For<GuardedStyleNode>([StyleSelectorCondition.Pseudo(PseudoClass.Hover)]),
                Setter(UIElement.OpacityProperty, 0.75)));

        Assert.Equal(0.25, node.Opacity);
        var hoverEvents = 0;
        var hoverSubscriptions = 0;
        node.PropertyChanged += (_, args) =>
        {
            if (ReferenceEquals(args.Property, UIElement.IsHoveredProperty))
                hoverEvents++;
        };
        using var subscription = node.Subscribe(UIElement.IsHoveredProperty, (_, _) => hoverSubscriptions++);
        GuardedValue.ThrowOnCompare = true;
        try
        {
            node.SetHovered(true);
            Assert.Throws<InvalidOperationException>(node.GetStyleRoot().UpdateStyles);
        }
        finally
        {
            GuardedValue.ThrowOnCompare = false;
        }

        Assert.True(node.HasPseudoClass(PseudoClass.Hover));
        Assert.Equal(0.25, node.Opacity);
        Assert.Equal(1, hoverEvents);
        Assert.Equal(1, hoverSubscriptions);

        node.SetHovered(true);
        Assert.Equal(0.25, node.Opacity);
        node.GetStyleRoot().UpdateStyles();
        Assert.Equal(0.75, node.Opacity);

        node.SetHovered(false);
        node.GetStyleRoot().UpdateStyles();
        Assert.Equal(0.25, node.Opacity);
        node.SetHovered(true);
        node.GetStyleRoot().UpdateStyles();
        Assert.Equal(0.75, node.Opacity);
    }

    [Fact]
    public void CustomControlCanActivateNamedPseudoClass()
    {
        var control = new CustomStateControl();
        ApplyTheme(
            control,
            Rule(
                StyleSelector.For<CustomStateControl>([StyleSelectorCondition.Pseudo(Loading)]),
                Setter(Region.BackgroundProperty, Brush(6))));

        control.SetLoading(true);
        control.GetStyleRoot().UpdateStyles();

        Assert.True(control.HasPseudoClass(Loading));
        Assert.Equal(Brush(6), control.Background);

        control.SetLoading(false);
        control.GetStyleRoot().UpdateStyles();

        Assert.False(control.HasPseudoClass(Loading));
        Assert.NotEqual(Brush(6), control.Background);
    }

    [Fact]
    public void NamedPseudoClassChangeTriggersStyleRefresh()
    {
        var host = new PseudoClassHost();
        ApplyTheme(
            host,
            Rule(
                StyleSelector.For<PseudoClassHost>([StyleSelectorCondition.Pseudo(Loading)]),
                Setter(UIElement.OpacityProperty, 0.4)));

        Assert.Equal(1d, host.Opacity);

        host.Set(Loading, true);
        host.GetStyleRoot().UpdateStyles();
        Assert.Equal(0.4, host.Opacity);

        host.Set(Loading, false);
        host.GetStyleRoot().UpdateStyles();
        Assert.Equal(1d, host.Opacity);
    }

    [Fact]
    public void NamedPseudoClassCombinationRequiresAllNamesAndSupportsWithdrawal()
    {
        var host = new PseudoClassHost();
        ApplyTheme(
            host,
            Rule(
                StyleSelector.For<PseudoClassHost>([StyleSelectorCondition.Pseudo(PseudoClass.Hover), StyleSelectorCondition.Pseudo(Loading)]),
                Setter(UIElement.OpacityProperty, 0.4)));

        host.Set(PseudoClass.Hover, true);
        host.GetStyleRoot().UpdateStyles();
        Assert.Equal(1d, host.Opacity);

        host.Set(Loading, true);
        host.GetStyleRoot().UpdateStyles();
        Assert.Equal(0.4, host.Opacity);

        host.Set(PseudoClass.Hover, false);
        host.GetStyleRoot().UpdateStyles();
        Assert.Equal(1d, host.Opacity);
    }

    [Fact]
    public void PseudoClassStyleNotificationFailureKeepsCommittedSnapshot()
    {
        var host = new PseudoClassHost();
        ApplyTheme(host, Rule(
            StyleSelector.For<PseudoClassHost>([StyleSelectorCondition.Pseudo(Loading)]),
            Setter(UIElement.OpacityProperty, 0.4)));
        var expected = new InvalidOperationException("style notification failed");
        var notifications = 0;
        host.PropertyChanged += (_, e) =>
        {
            if (!ReferenceEquals(e.Property, UIElement.OpacityProperty))
                return;
            notifications++;
            throw expected;
        };

        host.Set(Loading, true);
        Assert.Same(expected, Assert.Throws<InvalidOperationException>(host.GetStyleRoot().UpdateStyles));
        Assert.True(host.IsActive(Loading));
        Assert.Equal(0.4, host.Opacity);
        Assert.Single(host.GetStyleValueSources(UIElement.OpacityProperty));

        host.Set(Loading, true);
        host.GetStyleRoot().UpdateStyles();
        Assert.Equal(1, notifications);
    }

    [Fact]
    public void NamedPseudoClassIsCaseInsensitiveAndDeduplicated()
    {
        var host = new PseudoClassHost();
        ApplyTheme(
            host,
            Rule(
                StyleSelector.For<PseudoClassHost>([StyleSelectorCondition.Pseudo(Loading)]),
                Setter(UIElement.OpacityProperty, 0.4)));

        var refreshes = 0;
        host.PropertyChanged += (_, e) =>
            refreshes += ReferenceEquals(e.Property, UIElement.OpacityProperty) ? 1 : 0;

        host.Set(PseudoClass.Get("LOADING"), true);
        host.GetStyleRoot().UpdateStyles();
        Assert.Equal(0.4, host.Opacity);

        host.Set(Loading, true);
        host.GetStyleRoot().UpdateStyles();
        Assert.Equal(1, refreshes);
    }

    [Fact]
    public void SameNamedPseudoClassValueIsANoOp()
    {
        var host = new PseudoClassHost();
        ApplyTheme(
            host,
            Rule(
                StyleSelector.For<PseudoClassHost>([StyleSelectorCondition.Pseudo(Loading)]),
                Setter(UIElement.OpacityProperty, 0.4)));

        var refreshes = 0;
        host.PropertyChanged += (_, e) =>
            refreshes += ReferenceEquals(e.Property, UIElement.OpacityProperty) ? 1 : 0;

        host.Set(Loading, false);
        host.GetStyleRoot().UpdateStyles();
        Assert.Equal(0, refreshes);

        host.Set(Loading, true);
        host.GetStyleRoot().UpdateStyles();
        Assert.Equal(1, refreshes);

        host.Set(Loading, true);
        host.GetStyleRoot().UpdateStyles();
        Assert.Equal(1, refreshes);
    }

    [Fact]
    public void PseudoClassStyleNotificationDefersNestedStateChangesUntilNextApplication()
    {
        var host = new PseudoClassHost();
        ApplyTheme(
            host,
            Rule(
                StyleSelector.For<PseudoClassHost>([StyleSelectorCondition.Pseudo(Loading)]),
                Setter(UIElement.OpacityProperty, 0.4)),
            Rule(
                StyleSelector.For<PseudoClassHost>([StyleSelectorCondition.Pseudo(Phantom)]),
                Setter(UIElement.OpacityProperty, 0.7)));
        var notifications = 0;
        host.PropertyChanged += (_, e) =>
        {
            if (!ReferenceEquals(e.Property, UIElement.OpacityProperty))
                return;
            notifications++;
            if (host.Opacity != 0.4)
                return;

            host.Set(Phantom, true);
            host.Set(Loading, false);
            host.Set(Phantom, true);
        };

        host.Set(Loading, true);
        host.GetStyleRoot().UpdateStyles();

        Assert.False(host.IsActive(Loading));
        Assert.True(host.IsActive(Phantom));
        Assert.Equal(0.4, host.Opacity);
        Assert.Equal(1, notifications);
        Assert.False(host.IsStyleValid);
        host.GetStyleRoot().UpdateStyles();
        Assert.Equal(0.7, host.Opacity);
        Assert.Equal(2, notifications);
        host.Set(Phantom, false);
        Assert.False(host.IsActive(Phantom));
        host.GetStyleRoot().UpdateStyles();
        Assert.Equal(1d, host.Opacity);
        Assert.Equal(3, notifications);
    }

    [Fact]
    public void UnknownNamedPseudoClassIsLegalAndMatchesOnlyWhenActive()
    {
        var host = new PseudoClassHost();
        ApplyTheme(
            host,
            Rule(
                StyleSelector.For<PseudoClassHost>([StyleSelectorCondition.Pseudo(Phantom)]),
                Setter(UIElement.OpacityProperty, 0.4)));

        Assert.Equal(1d, host.Opacity);

        host.Set(Phantom, true);
        host.GetStyleRoot().UpdateStyles();
        Assert.Equal(0.4, host.Opacity);
    }

    [Fact]
    public void OpenSceneStyleIdRejectsWrongThreadButAllowsNoOps()
    {
        var button = new Button { StyleId = "save" };
        var scene = new UIScene(button);
        scene.Open();
        Exception? styleIdError = null;
        Exception? noOpError = null;
        var thread = new Thread(() =>
        {
            styleIdError = Record.Exception(() => button.StyleId = "other");
            noOpError = Record.Exception(() => button.StyleId = "save");
        });

        thread.Start();
        thread.Join();

        Assert.IsType<InvalidOperationException>(styleIdError);
        Assert.Null(noOpError);
        Assert.Equal("save", button.StyleId);
        scene.Close();
    }

    [Theory]
    [InlineData(0, new[] { "kept", "next" })]
    [InlineData(1, new string[] { })]
    [InlineData(2, new string[] { })]
    [InlineData(3, new[] { "kept", "next" })]
    [InlineData(4, new string[] { })]
    [InlineData(5, new string[] { })]
    [InlineData(6, new[] { "kept", "next" })]
    public void ClassSetMutationsInvalidateStylesBeforeListenersRun(
        int operation, string[] expected)
    {
        var node = new Canvas();
        node.Classes.Add("kept");
        var scene = new UIScene(node);
        scene.SetStyleSheets([StyleSheet.Parse(".next { opacity: 0.4; }")]);
        scene.Open();
        scene.Root!.UpdateStyles();
        Assert.True(node.IsStyleValid);
        IObservableSet<string> classes = node.Classes;
        Action[] mutations =
        [
            () => classes.Add("next"),
            () => classes.Remove("kept"),
            classes.Clear,
            () => classes.UnionWith(["next"]),
            () => classes.IntersectWith([]),
            () => classes.ExceptWith(["kept"]),
            () => classes.SymmetricExceptWith(["next"])
        ];
        var notifications = 0;
        classes.Changed += (_, _) =>
        {
            notifications++;
            Assert.False(node.IsStyleValid);
            Assert.False(node.IsStyleSubtreeValid);
        };

        mutations[operation]();

        Assert.True(classes.SetEquals(expected));
        Assert.Equal(1d, node.Opacity);
        Assert.Equal(1, notifications);
        scene.Root!.UpdateStyles();
        Assert.Equal(classes.Contains("next") ? 0.4 : 1d, node.Opacity);
        Assert.True(node.IsStyleValid);
        Assert.True(node.IsStyleSubtreeValid);
        scene.Close();
    }

    [Fact]
    public void OpenScenePseudoClassInputsRejectWrongThreadButAllowNoOps()
    {
        var host = new PseudoClassHost();
        var scene = new UIScene(host);
        scene.Open();
        Exception? wrongThreadError = null;
        Exception? noOpError = null;
        var thread = new Thread(() =>
        {
            wrongThreadError = Record.Exception(() => host.Set(Loading, true));
            noOpError = Record.Exception(() => host.Set(Loading, false));
        });

        thread.Start();
        thread.Join();

        Assert.IsType<InvalidOperationException>(wrongThreadError);
        Assert.Null(noOpError);
        Assert.False(host.IsActive(Loading));
        scene.Close();
    }

    [Fact]
    public void PseudoClassInputsCanChangeDuringLayoutButNotDrawing()
    {
        var node = new StyleMutationNode();
        var scene = new UIScene(node);
        Exception? layoutError = null;
        Exception? drawingError = null;
        node.MeasureAction = () => layoutError = Record.Exception(() => node.Set(Loading, true));
        node.DrawAction = () => drawingError = Record.Exception(() => node.Set(Loading, false));
        scene.Open();

        scene.PrepareFrame(new Size(20, 20));
        _ = scene.CreateDrawCommandList();

        Assert.Null(layoutError);
        Assert.IsType<InvalidOperationException>(drawingError);
        Assert.True(node.IsActive(Loading));
        scene.Close();
    }

    [Fact]
    public void PseudoClassInputsInvalidateStylesDuringApplicationNotification()
    {
        var host = new PseudoClassHost();
        var scene = new UIScene(host);
        scene.SetStyleSheets([
            new StyleSheet([
                Rule(StyleSelector.For<PseudoClassHost>(), Setter(UIElement.OpacityProperty, 0.3))
            ])
        ]);
        scene.Root!.UpdateStyles();
        Exception? error = null;
        host.PropertyChanged += (_, e) =>
        {
            if (ReferenceEquals(e.Property, UIElement.OpacityProperty))
                error = Record.Exception(() => host.Set(Loading, true));
        };

        scene.SetStyleSheets([
            new StyleSheet([
                Rule(StyleSelector.For<PseudoClassHost>(), Setter(UIElement.OpacityProperty, 0.8))
            ])
        ]);
        scene.Root!.UpdateStyles();

        Assert.Null(error);
        Assert.True(host.IsActive(Loading));
    }

    [Fact]
    public void ClassChangesDuringDrawingApplyStylesOnTheNextFrame()
    {
        var node = new StyleMutationNode();
        var scene = new UIScene(node);
        scene.SetStyleSheets([StyleSheet.Parse(".layout { opacity: 0.4; } .drawing { opacity: 0.7; }")]);
        var collectionNotifications = 0;
        node.Classes.Changed += (_, _) => collectionNotifications++;
        Exception? layoutStyleIdError = null;
        Exception? layoutClassError = null;
        Exception? drawingStyleIdError = null;
        Exception? drawingClassError = null;
        node.MeasureAction = () =>
        {
            layoutStyleIdError = Record.Exception(() => node.StyleId = "layout");
            layoutClassError = Record.Exception(() => node.Classes.Add("layout"));
        };
        node.DrawAction = () =>
        {
            drawingStyleIdError = Record.Exception(() => node.StyleId = "drawing");
            drawingClassError = Record.Exception(() => node.Classes.Add("drawing"));
        };
        scene.Open();

        scene.PrepareFrame(new Size(20, 20));
        _ = scene.CreateDrawCommandList();

        Assert.Null(layoutStyleIdError);
        Assert.Null(layoutClassError);
        Assert.IsType<InvalidOperationException>(drawingStyleIdError);
        Assert.Null(drawingClassError);
        Assert.Equal("layout", node.StyleId);
        Assert.True(node.Classes.SetEquals(["layout", "drawing"]));
        Assert.Equal(0.4, node.Opacity);
        Assert.Equal(2, collectionNotifications);
        Assert.False(node.IsStyleValid);
        Assert.False(node.IsStyleSubtreeValid);

        scene.PrepareFrame(new Size(20, 20));

        Assert.Equal(0.7, node.Opacity);
        Assert.True(node.IsStyleValid);
        Assert.True(node.IsStyleSubtreeValid);
        Assert.Equal(2, collectionNotifications);
        scene.Close();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StyleChangesApplyAfterAllChildrenCompleteTheCurrentLayoutPass(bool duringArrange)
    {
        var root = new StackPanel();
        var first = new StyleMutationNode { Height = 10 };
        var second = new StyleMutationNode { StyleId = "target", Height = 10 };
        root.Children.Add(first);
        root.Children.Add(second);
        var scene = new UIScene(root);
        scene.SetStyleSheets([StyleSheet.Parse("#target { width: 10; } .wide #target { width: 20; }")]);
        var measuredWidths = new List<double>();
        var arrangedWidths = new List<double>();
        second.MeasureAction = () => measuredWidths.Add(second.Width);
        second.ArrangeAction = () => arrangedWidths.Add(second.Width);
        if (duringArrange)
            first.ArrangeAction = () => root.Classes.Add("wide");
        else
            first.MeasureAction = () => root.Classes.Add("wide");
        scene.Open();
        try
        {
            scene.PrepareFrame(new Size(100, 100));

            Assert.Equal(new[] { 10d, 20d }, measuredWidths);
            Assert.Equal(duringArrange ? new[] { 10d, 20d } : new[] { 20d }, arrangedWidths);
            Assert.Equal(20, second.LayoutBounds.Width);
            Assert.True(root.IsMeasureValid);
            Assert.True(root.IsArrangeValid);
        }
        finally
        {
            scene.Close();
        }
    }

    [Fact]
    public void StyleClassesImplementReadOnlySetRelations()
    {
        var button = new Button();
        button.Classes.Add("primary");
        button.Classes.Add("wide");
        IReadOnlySet<string> classes = button.Classes;

        Assert.True(classes.IsSubsetOf(["primary", "wide", "extra"]));
        Assert.True(classes.IsProperSupersetOf(["primary"]));
        Assert.True(classes.Overlaps(["other", "wide"]));
        Assert.True(classes.SetEquals(["wide", "primary"]));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LayoutAndStyleApplicationDoNotBlockUnrelatedScenes(bool duringLayout)
    {
        var node = new StyleMutationNode();
        var scene = new UIScene(node);
        var other = new UIScene(new Panel());
        var visited = false;
        Action updateOther = () =>
        {
            visited = true;
            Assert.True(scene.IsUpdatingLayout);
            Assert.Equal(!duringLayout, node.IsUpdatingStyles);
            Assert.False(other.IsUpdatingLayout);
            Assert.False(other.Root!.IsUpdatingStyles);
            other.Root = new Panel();
            other.Scale = 2;
            other.SetStyleSheets([StyleSheet.Parse("Panel { opacity: 0.6; }")]);
            other.Root!.UpdateStyles();
            Assert.Equal(0.6, other.Root!.Opacity);
            Assert.True(scene.IsUpdatingLayout);
            Assert.Equal(!duringLayout, node.IsUpdatingStyles);
            if (!duringLayout)
            {
                Assert.Throws<InvalidOperationException>(node.UpdateStyles);
                Assert.Throws<InvalidOperationException>(() => node.Measure(new Size(100, 100)));
            }
        };
        if (duringLayout)
            node.MeasureAction = updateOther;
        else
            node.PropertyChanged += (_, args) =>
            {
                if (args.Property == UIElement.OpacityProperty)
                    updateOther();
            };
        scene.SetStyleSheets([StyleSheet.Parse("* { opacity: 0.4; }")]);
        scene.Open();
        try
        {
            scene.PrepareFrame(new Size(100, 100));

            Assert.True(visited);
            Assert.False(scene.IsUpdatingLayout);
            Assert.False(node.IsUpdatingStyles);
        }
        finally
        {
            scene.Close();
        }
    }

    private static void ApplyTheme(UIElement node, params StyleRule[] rules)
    {
        var scene = new UIScene(node);
        scene.SetStyleSheets([new StyleSheet(rules)]);
        scene.Root!.UpdateStyles();
    }

    private static StyleRule Rule(StyleSelector selector, params StyleSetter[] setters) =>
        new(selector, setters);

    private static StyleSetter Setter<T>(Property<T> property, T value) =>
        StyleSetter.Create(property, value);

    private static Brush Brush(byte value) => new SolidColorBrush(value, value, value);

    private static Brush Brush(byte r, byte g, byte b) => new SolidColorBrush(r, g, b);

    private sealed class EquatableNode : UIElement
    {
        internal static readonly Property<EquatableValue> ValueProperty =
            Property.Register<EquatableNode, EquatableValue>(
                "Value",
                new EquatableValue(0), comparer: EqualityComparer<EquatableValue>.Default);
    }

    private sealed class EquatableValue(int value) : IEquatable<EquatableValue>
    {
        public bool Equals(EquatableValue? other) => other is not null && value == other.Value;

        private int Value => value;
    }

    private class PseudoClassHost : UIElement
    {
        internal void Set(PseudoClass pseudoClass, bool active) => SetPseudoClass(pseudoClass, active);

        internal bool IsActive(PseudoClass pseudoClass) => HasPseudoClass(pseudoClass);
    }

    private sealed class GuardedValue(double number) : IEquatable<GuardedValue>
    {
        internal static bool ThrowOnCompare;
        internal static Action? OnCompare;

        internal double Number => number;

        public bool Equals(GuardedValue? other)
        {
            OnCompare?.Invoke();
            if (ThrowOnCompare)
                throw new InvalidOperationException("guarded value comparison failed");
            return other is not null && number.Equals(other.Number);
        }

        public override bool Equals(object? obj) => obj is GuardedValue other && Equals(other);

        public override int GetHashCode() => number.GetHashCode();
    }

    private sealed class GuardedStyleNode : UIElement
    {
        internal static readonly Property<GuardedValue> ValueProperty =
            Property.Register<GuardedStyleNode, GuardedValue>("Value", new GuardedValue(0),
                comparer: EqualityComparer<GuardedValue>.Default);
    }

    private sealed class CustomStateControl : Control
    {
        internal void SetLoading(bool value) => SetPseudoClass(Loading, value);
    }

    private sealed class StyleMutationNode : PseudoClassHost
    {
        internal Action? MeasureAction { get; set; }
        internal Action? ArrangeAction { get; set; }
        internal Action? DrawAction { get; set; }

        protected override Size MeasureCore(Size availableSize)
        {
            MeasureAction?.Invoke();
            return Size.Zero;
        }

        protected override void DrawCore(DrawingContext context) => DrawAction?.Invoke();

        protected override void ArrangeCore(Size finalSize) => ArrangeAction?.Invoke();
    }
}
