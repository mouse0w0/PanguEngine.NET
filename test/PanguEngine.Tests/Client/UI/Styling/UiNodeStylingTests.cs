using PanguEngine.ComponentModel;
using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Drawing;
using PanguEngine.Client.UI.Styling;
using PanguEngine.Collections;

namespace PanguEngine.Tests.Client.UI.Styling;

public sealed class UiNodeStylingTests
{
    private static readonly UiPseudoClass Loading = UiPseudoClass.Get("loading");
    private static readonly UiPseudoClass Phantom = UiPseudoClass.Get("phantom");

    [Fact]
    public void LocalValueMasksStyleAndClearRestoresStyle()
    {
        var button = new Button();
        ApplyTheme(button, Rule(Selector<Button>(), Setter(Region.BackgroundProperty, Brush(1, 2, 3))));

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
        ApplyTheme(target, Rule(Selector<Button>(), Setter(Region.BackgroundProperty, Brush(1))));
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
        ApplyTheme(button, Rule(Selector<Button>(classes: ["primary"]), Setter(Region.BackgroundProperty, Brush(1))));
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
        ApplyTheme(button, Rule(Selector<Button>(classes: ["primary"]), Setter(Region.BackgroundProperty, Brush(1))));

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
        ApplyTheme(button, Rule(Selector<Button>(id: "save"), Setter(Region.BackgroundProperty, Brush(1))));

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
            UiStyleSelector.For<Button>(pseudoClasses: [UiPseudoClass.Hover]),
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
    public void StyleChangeNotificationsFollowRegistrationOrder()
    {
        var button = new Button();
        var order = new List<string>();
        button.PropertyChanged += (_, e) =>
        {
            if (ReferenceEquals(e.Property, Region.BackgroundProperty)) order.Add("Background");
            if (ReferenceEquals(e.Property, Region.BorderBrushProperty)) order.Add("BorderBrush");
        };

        ApplyTheme(
            button,
            Rule(Selector<Button>(), Setter(Region.BackgroundProperty, Brush(1))),
            Rule(Selector<Button>(), Setter(Region.BorderBrushProperty, Brush(2))));

        Assert.Equal(new[] { "Background", "BorderBrush" }, order);
    }

    [Fact]
    public void EarlierPropertyHandlerCanLocallyMaskLaterStyleNotification()
    {
        var button = new Button();
        var screen = new UiScreen(button);
        var borderNotifications = 0;
        button.PropertyChanged += (_, e) =>
        {
            if (ReferenceEquals(e.Property, Region.BackgroundProperty))
                button.BorderBrush = Brush(9);
            if (ReferenceEquals(e.Property, Region.BorderBrushProperty))
                borderNotifications++;
        };
        var sheet = new UiStyleSheet([
            Rule(Selector<Button>(), Setter(Region.BackgroundProperty, Brush(1))),
            Rule(Selector<Button>(), Setter(Region.BorderBrushProperty, Brush(2)))
        ]);

        screen.SetStyleSheets([sheet]);
        screen.Root!.UpdateStyles();

        Assert.Equal(Brush(9), button.BorderBrush);
        Assert.Equal(1, borderNotifications);
    }

    [Fact]
    public void ClassSwitchLocallyMasksLaterStyleNotification()
    {
        var button = new Button();
        ApplyTheme(
            button,
            Rule(
                Selector<Button>(),
                Setter(Region.BackgroundProperty, Brush(1)),
                Setter(Region.BorderBrushProperty, Brush(2))),
            Rule(
                Selector<Button>(classes: ["primary"]),
                Setter(Region.BackgroundProperty, Brush(5)),
                Setter(Region.BorderBrushProperty, Brush(6))));

        var borderNotifications = 0;
        button.PropertyChanged += (_, e) =>
        {
            if (ReferenceEquals(e.Property, Region.BackgroundProperty))
                button.BorderBrush = Brush(9);
            if (ReferenceEquals(e.Property, Region.BorderBrushProperty))
                borderNotifications++;
        };

        button.Classes.Add("primary");
        button.GetStyleRoot().UpdateStyles();

        Assert.Equal(Brush(9), button.BorderBrush);
        Assert.Equal(1, borderNotifications);
    }

    [Fact]
    public void StyleChangeExceptionStopsRemainingNotifications()
    {
        var button = new Button();
        var order = new List<string>();
        button.PropertyChanged += (_, e) =>
        {
            if (ReferenceEquals(e.Property, Region.BackgroundProperty))
            {
                order.Add("Background");
                throw new InvalidOperationException("bg-fail");
            }

            if (ReferenceEquals(e.Property, Region.BorderBrushProperty))
                order.Add("BorderBrush");
        };

        var exception = Assert.Throws<InvalidOperationException>(() => ApplyTheme(
            button,
            Rule(Selector<Button>(), Setter(Region.BackgroundProperty, Brush(1))),
            Rule(Selector<Button>(), Setter(Region.BorderBrushProperty, Brush(2)))));

        Assert.Equal("bg-fail", exception.Message);
        Assert.Equal(new[] { "Background" }, order);
        Assert.Equal(Brush(2), button.BorderBrush);
        Assert.False(button.IsUpdatingStyles);
        Assert.False(button.Screen!.IsUpdatingLayout);
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
        var screen = new UiScreen(root);
        screen.SetStyleSheets([new UiStyleSheet([
            Rule(Selector<Panel>(), Setter(UiNode.OpacityProperty, 0.4)),
            Rule(Selector<GuardedStyleNode>(), Setter(GuardedStyleNode.ValueProperty, new GuardedValue(0.5)))
        ])]);
        var firstNotifications = 0;
        first.PropertyChanged += (_, args) =>
            firstNotifications += args.Property == UiNode.OpacityProperty ? 1 : 0;

        GuardedValue.ThrowOnCompare = true;
        try
        {
            Assert.Throws<InvalidOperationException>(screen.Root!.UpdateStyles);
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
        Assert.False(screen.IsUpdatingLayout);

        screen.Root!.UpdateStyles();

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
        var screen = new UiScreen(root);
        screen.SetStyleSheets([UiStyleSheet.Parse("#first.active, #last.active { opacity: 0.4; }")]);
        screen.Root!.UpdateStyles();
        var notifications = 0;
        first.PropertyChanged += (_, args) =>
        {
            if (args.Property != UiNode.OpacityProperty)
                return;
            notifications++;
            last.Classes.Add("active");
        };

        first.Classes.Add("active");
        screen.Root!.UpdateStyles();

        Assert.Equal(0.4, last.Opacity);
        Assert.True(first.IsStyleSubtreeValid);
        Assert.True(last.IsStyleSubtreeValid);
        Assert.True(root.IsStyleValid);
        Assert.False(root.IsStyleSubtreeValid);

        screen.Root!.UpdateStyles();

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
        var screen = new UiScreen(root);
        screen.SetStyleSheets([UiStyleSheet.Parse("Panel { opacity: 0.4; }")]);
        var expected = new InvalidOperationException("notification failed");
        first.PropertyChanged += (_, args) =>
        {
            if (args.Property == UiNode.OpacityProperty)
                throw expected;
        };

        Assert.Same(expected, Assert.Throws<InvalidOperationException>(screen.Root!.UpdateStyles));
        Assert.Equal(0.4, first.Opacity);
        Assert.Equal(1d, last.Opacity);
        Assert.False(root.IsStyleSubtreeValid);

        Assert.False(root.IsUpdatingStyles);
        Assert.False(first.IsUpdatingStyles);
        Assert.False(last.IsUpdatingStyles);

        screen.Root!.UpdateStyles();

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
        var screen = new UiScreen(root);
        screen.SetStyleSheets([UiStyleSheet.Parse("Panel { opacity: 0.4; } Button { background: #010203; }")]);
        first.PropertyChanged += (_, args) =>
        {
            if (args.Property == UiNode.OpacityProperty)
                root.Children.Remove(last);
        };

        screen.Root!.UpdateStyles();

        Assert.Null(last.Parent);
        Assert.Null(last.Screen);
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
            Rule(Selector<Button>(classes: ["x"]), Setter(Region.BackgroundProperty, Brush(5))),
            Rule(Selector<Button>(classes: ["extra"]), Setter(Region.BackgroundProperty, Brush(9))));

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
            Rule(Selector<Button>(classes: ["x"]), Setter(Region.BackgroundProperty, Brush(5))),
            Rule(Selector<Button>(classes: ["extra"]), Setter(Region.BackgroundProperty, Brush(9))));
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
            Rule(Selector<Button>(classes: ["x"]), Setter(Region.BackgroundProperty, Brush(5))),
            Rule(Selector<Button>(classes: ["extra"]), Setter(Region.BackgroundProperty, Brush(9))));
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
        var screen = new UiScreen(node);
        screen.SetStyleSheets([UiStyleSheet.Parse(".primary.wide { opacity: 0.4; }")]);
        screen.Root!.UpdateStyles();
        IObservableSet<string> classes = node.Classes;
        var order = new List<string>();
        var changes = new List<SetChangedEventArgs<string>>();
        node.PropertyChanged += (_, e) =>
        {
            if (ReferenceEquals(e.Property, UiNode.OpacityProperty))
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
        screen.Root!.UpdateStyles();
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
        var screen = new UiScreen(node);
        screen.SetStyleSheets([UiStyleSheet.Parse(".valid { opacity: 0.4; }")]);
        node.Classes.Add("kept");
        var changes = new List<SetChangedEventArgs<string>>();
        node.Classes.Changed += (_, change) => changes.Add(change);

        node.Classes.UnionWith(["valid", className!]);
        screen.Root!.UpdateStyles();

        Assert.True(node.Classes.SetEquals(["kept", "valid", className!]));
        Assert.Equal(0.4, node.Opacity);
        Assert.True(new HashSet<string>(Assert.Single(changes).AddedItems).SetEquals(["valid", className!]));
        node.Classes.SymmetricExceptWith(["kept", "valid", className!, className!]);
        screen.Root!.UpdateStyles();
        Assert.Empty(node.Classes);
        Assert.Equal(1d, node.Opacity);
        Assert.Equal(2, changes.Count);
    }

    [Fact]
    public void ClassSetCalculationFailureKeepsTheWholeBatchAndAlreadyPublishedChange()
    {
        var node = new GuardedStyleNode();
        var screen = new UiScreen(node);
        screen.SetStyleSheets([new UiStyleSheet([
            Rule(UiStyleSelector.For<GuardedStyleNode>(),
                Setter(GuardedStyleNode.ValueProperty, new GuardedValue(0.5)))
        ])]);
        node.Classes.Add("kept");
        screen.Root!.UpdateStyles();
        var previousSource = Assert.Single(node.GetStyleValueSources(GuardedStyleNode.ValueProperty));
        var notifications = 0;
        node.Classes.Changed += (_, _) => notifications++;
        GuardedValue.ThrowOnCompare = true;
        try
        {
            node.Classes.UnionWith(["first", "second"]);
            Assert.Throws<InvalidOperationException>(screen.Root!.UpdateStyles);
        }
        finally
        {
            GuardedValue.ThrowOnCompare = false;
        }

        Assert.True(node.Classes.SetEquals(["kept", "first", "second"]));
        Assert.Equal(1, notifications);
        Assert.Same(screen, node.Screen);
        Assert.Same(previousSource, Assert.Single(node.GetStyleValueSources(GuardedStyleNode.ValueProperty)));
        node.Classes.UnionWith(["first", "second"]);
        Assert.Equal(1, notifications);
        Assert.False(node.IsStyleValid);
        Assert.True(node.Classes.Remove("first"));
        Assert.Equal(2, notifications);
        screen.Root!.UpdateStyles();
        Assert.True(node.IsStyleValid);
    }

    [Fact]
    public void ClassSetStyleNotificationFailureKeepsCommittedStateAndPublishedSetChange()
    {
        var expected = new InvalidOperationException("style notification failed");
        var node = new Canvas();
        var screen = new UiScreen(node);
        screen.SetStyleSheets([UiStyleSheet.Parse(".primary { opacity: 0.4; }")]);
        var failNotification = true;
        var collectionNotifications = 0;
        node.PropertyChanged += (_, e) =>
        {
            if (ReferenceEquals(e.Property, UiNode.OpacityProperty) && failNotification)
                throw expected;
        };
        node.Classes.Changed += (_, _) => collectionNotifications++;

        node.Classes.Add("primary");
        Assert.Same(expected, Assert.Throws<InvalidOperationException>(screen.Root!.UpdateStyles));

        Assert.True(node.Classes.Contains("primary"));
        Assert.Equal(0.4, node.Opacity);
        Assert.Equal(1, collectionNotifications);
        failNotification = false;
        node.Classes.Remove("primary");
        screen.Root!.UpdateStyles();
        Assert.Equal(1d, node.Opacity);
        Assert.Equal(2, collectionNotifications);
    }

    [Fact]
    public void ClassSetListenerFailureLeavesStylesPendingAndReleasesMutationProtection()
    {
        var expected = new InvalidOperationException("class listener failed");
        var node = new Canvas();
        var screen = new UiScreen(node);
        screen.SetStyleSheets([UiStyleSheet.Parse(".primary { opacity: 0.4; }")]);
        screen.Root!.UpdateStyles();
        var laterCalls = 0;
        EventHandler<SetChangedEventArgs<string>> failing = (_, _) => throw expected;
        node.Classes.Changed += failing;
        node.Classes.Changed += (_, _) => laterCalls++;

        Assert.Same(expected, Assert.Throws<InvalidOperationException>(() => node.Classes.Add("primary")));

        Assert.True(node.Classes.Contains("primary"));
        Assert.Equal(1d, node.Opacity);
        Assert.False(node.IsStyleValid);
        Assert.Equal(0, laterCalls);
        screen.Root!.UpdateStyles();
        Assert.Equal(0.4, node.Opacity);
        node.Classes.Changed -= failing;
        node.Classes.Remove("primary");
        screen.Root!.UpdateStyles();
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
        var screen = new UiScreen(node);
        screen.SetStyleSheets([UiStyleSheet.Parse(".primary { opacity: 0.4; }")]);
        var removedCalls = 0;
        var addedCalls = 0;
        var changedSubscription = false;
        EventHandler<SetChangedEventArgs<string>> removed = (_, _) => removedCalls++;
        EventHandler<SetChangedEventArgs<string>> added = (_, _) => addedCalls++;
        node.Classes.Changed += removed;
        node.PropertyChanged += (_, e) =>
        {
            if (!ReferenceEquals(e.Property, UiNode.OpacityProperty) || changedSubscription)
                return;
            changedSubscription = true;
            node.Classes.Changed -= removed;
            node.Classes.Changed += added;
        };

        node.Classes.Add("primary");

        Assert.Equal(1, removedCalls);
        Assert.Equal(0, addedCalls);
        screen.Root!.UpdateStyles();
        node.Classes.Remove("primary");
        Assert.Equal(1, removedCalls);
        Assert.Equal(1, addedCalls);
    }

    [Fact]
    public void StyleIdNotificationClassChangeLeavesNestedStylesPending()
    {
        var node = new Canvas();
        var screen = new UiScreen(node);
        screen.SetStyleSheets([UiStyleSheet.Parse("#trigger { opacity: 0.4; } #trigger.extra { opacity: 0.9; }")]);
        var order = new List<(string Kind, double Opacity)>();
        node.PropertyChanged += (_, e) =>
        {
            if (!ReferenceEquals(e.Property, UiNode.OpacityProperty))
                return;
            order.Add(("style", node.Opacity));
            if (!node.Classes.Contains("extra"))
                node.Classes.Add("extra");
        };
        node.Classes.Changed += (_, _) => order.Add(("set", node.Opacity));

        node.StyleId = "trigger";
        screen.Root!.UpdateStyles();

        Assert.True(node.Classes.Contains("extra"));
        Assert.Equal(0.4, node.Opacity);
        Assert.Equal(new[] { ("style", 0.4), ("set", 0.4) }, order);
        screen.Root!.UpdateStyles();
        Assert.Equal(0.9, node.Opacity);
        Assert.Equal(new[] { ("style", 0.4), ("set", 0.4), ("style", 0.9) }, order);
    }

    [Fact]
    public void PseudoClassNotificationClassChangePublishesBeforeTheNextStyleUpdate()
    {
        var node = new PseudoClassHost();
        var screen = new UiScreen(node);
        screen.SetStyleSheets([UiStyleSheet.Parse(":loading { opacity: 0.4; } :loading.extra { opacity: 0.9; }")]);
        var styleNotifications = 0;
        var collectionNotifications = 0;
        node.PropertyChanged += (_, e) =>
        {
            if (!ReferenceEquals(e.Property, UiNode.OpacityProperty))
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
        screen.Root!.UpdateStyles();

        Assert.True(node.Classes.SetEquals(["extra"]));
        Assert.Equal(0.4, node.Opacity);
        Assert.Equal(1, styleNotifications);
        screen.Root!.UpdateStyles();
        Assert.Equal(0.9, node.Opacity);
        Assert.Equal(2, styleNotifications);
        Assert.Equal(1, collectionNotifications);
    }

    [Fact]
    public void NestedClassStyleNotificationFailureKeepsCommittedStateAndPublishedSetChange()
    {
        var expected = new InvalidOperationException("nested style notification failed");
        var node = new Canvas();
        var screen = new UiScreen(node);
        screen.SetStyleSheets([UiStyleSheet.Parse("#trigger { opacity: 0.4; } #trigger.extra { opacity: 0.9; }")]);
        var reactToNotification = true;
        var collectionNotifications = 0;
        node.PropertyChanged += (_, e) =>
        {
            if (!ReferenceEquals(e.Property, UiNode.OpacityProperty) || !reactToNotification)
                return;
            if (!node.Classes.Contains("extra"))
                node.Classes.Add("extra");
            else
                throw expected;
        };
        node.Classes.Changed += (_, _) => collectionNotifications++;

        node.StyleId = "trigger";
        screen.Root!.UpdateStyles();
        Assert.Equal(0.4, node.Opacity);
        Assert.Equal(1, collectionNotifications);
        Assert.Same(expected, Assert.Throws<InvalidOperationException>(screen.Root!.UpdateStyles));

        Assert.True(node.Classes.SetEquals(["extra"]));
        Assert.Equal(0.9, node.Opacity);
        Assert.Equal(1, collectionNotifications);
        reactToNotification = false;
        node.Classes.Remove("extra");
        screen.Root!.UpdateStyles();
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
                Selector<Button>(classes: ["primary"]),
                Setter(Region.BackgroundProperty, Brush(1, 2, 3))));
        button.Classes.Add("primary");
        button.Background = new SolidColorBrush(9, 9, 9);
        button.GetStyleRoot().UpdateStyles();

        var source = Assert.Single(button.GetStyleValueSources(Region.BackgroundProperty));

        Assert.True(source.IsMaskedByLocalValue);
        Assert.Equal("Button.primary", source.SelectorText);
        Assert.Equal(UiStyleOrigin.Author, source.Origin);
        Assert.Equal(0, source.SheetIndex);
    }

    [Fact]
    public void SourceReportsWinningBranchFromSelectorList()
    {
        var button = new Button();
        var screen = new UiScreen(button);
        screen.SetStyleSheets([UiStyleSheet.Parse(".plain, #target { opacity: 0.4; }")]);
        button.Classes.Add("plain");
        button.GetStyleRoot().UpdateStyles();
        Assert.Equal(".plain", Assert.Single(button.GetStyleValueSources(UiNode.OpacityProperty)).SelectorText);
        button.StyleId = "target";
        button.Opacity = 0.9;
        button.GetStyleRoot().UpdateStyles();
        var source = Assert.Single(button.GetStyleValueSources(UiNode.OpacityProperty));
        Assert.Equal("#target", source.SelectorText);
        Assert.True(source.IsMaskedByLocalValue);
        button.StyleId = null;
        button.GetStyleRoot().UpdateStyles();
        Assert.Equal(".plain", Assert.Single(button.GetStyleValueSources(UiNode.OpacityProperty)).SelectorText);
        button.ClearValue(UiNode.OpacityProperty);
        Assert.Equal(0.4, button.Opacity);
        button.Classes.Remove("plain");
        button.GetStyleRoot().UpdateStyles();
        Assert.Equal(1d, button.Opacity);
    }

    [Fact]
    public void SourceQueryReturnsEmptyWithoutDeclarationAndRejectsWrongTarget()
    {
        var node = new Canvas();

        Assert.Empty(node.GetStyleValueSources(UiNode.WidthProperty));
        Assert.Throws<ArgumentException>(() =>
            node.GetStyleValueSources(Text.ContentProperty));
    }

    [Fact]
    public void StyleSelectorInputsRemainCommittedWhenApplicationFails()
    {
        var node = new GuardedStyleNode { StyleId = "old" };
        var sheet = new UiStyleSheet([
            Rule(
                UiStyleSelector.For<GuardedStyleNode>(),
                Setter(GuardedStyleNode.ValueProperty, new GuardedValue(0.5)))
        ]);
        var screen = new UiScreen(node);
        screen.SetStyleSheets([sheet]);
        screen.Root!.UpdateStyles();
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
        Assert.Same(screen, node.Screen);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SelectorChangesDuringNodeCalculationRemainPending(bool changeId)
    {
        var node = new GuardedStyleNode { StyleId = "original" };
        var screen = new UiScreen(node);
        screen.SetStyleSheets([
            new UiStyleSheet([
                Rule(
                    UiStyleSelector.For<GuardedStyleNode>(),
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
        var firstSheet = new UiStyleSheet([
            Rule(
                UiStyleSelector.For<EquatableNode>(),
                Setter(EquatableNode.ValueProperty, new EquatableValue(1)))
        ]);
        var secondSheet = new UiStyleSheet([
            Rule(
                UiStyleSelector.For<EquatableNode>(),
                Setter(EquatableNode.ValueProperty, new EquatableValue(1)))
        ]);
        var screen = new UiScreen(node);
        screen.SetStyleSheets([firstSheet]);
        screen.Root!.UpdateStyles();
        var notifications = 0;
        node.PropertyChanged += (_, e) =>
            notifications += ReferenceEquals(e.Property, EquatableNode.ValueProperty) ? 1 : 0;

        screen.SetStyleSheets([secondSheet]);
        screen.Root!.UpdateStyles();

        Assert.Equal(0, notifications);
    }

    [Fact]
    public void PseudoClassHandlerExceptionStillInvalidatesStyle()
    {
        var button = new Button();
        ApplyTheme(button, Rule(
            UiStyleSelector.For<Button>(pseudoClasses: [UiPseudoClass.Hover]),
            Setter(Region.BackgroundProperty, Brush(9))));
        button.PropertyChanged += (_, e) =>
        {
            if (ReferenceEquals(e.Property, UiNode.IsHoveredProperty))
                throw new InvalidOperationException("hover-handler-fail");
        };

        Assert.Throws<InvalidOperationException>(() => button.SetHovered(true));

        Assert.True(button.HasPseudoClass(UiPseudoClass.Hover));
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
                UiStyleSelector.For<GuardedStyleNode>(),
                Setter(UiNode.OpacityProperty, 0.25)),
            Rule(
                UiStyleSelector.For<GuardedStyleNode>(),
                Setter(GuardedStyleNode.ValueProperty, new GuardedValue(0.5))),
            Rule(
                UiStyleSelector.For<GuardedStyleNode>(pseudoClasses: [UiPseudoClass.Hover]),
                Setter(UiNode.OpacityProperty, 0.75)));

        Assert.Equal(0.25, node.Opacity);
        var hoverEvents = 0;
        var hoverSubscriptions = 0;
        node.PropertyChanged += (_, args) =>
        {
            if (ReferenceEquals(args.Property, UiNode.IsHoveredProperty))
                hoverEvents++;
        };
        using var subscription = node.Subscribe(UiNode.IsHoveredProperty, (_, _) => hoverSubscriptions++);
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

        Assert.True(node.HasPseudoClass(UiPseudoClass.Hover));
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
                UiStyleSelector.For<CustomStateControl>(pseudoClasses: [Loading]),
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
                UiStyleSelector.For<PseudoClassHost>(pseudoClasses: [Loading]),
                Setter(UiNode.OpacityProperty, 0.4)));

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
                UiStyleSelector.For<PseudoClassHost>(pseudoClasses: [UiPseudoClass.Hover, Loading]),
                Setter(UiNode.OpacityProperty, 0.4)));

        host.Set(UiPseudoClass.Hover, true);
        host.GetStyleRoot().UpdateStyles();
        Assert.Equal(1d, host.Opacity);

        host.Set(Loading, true);
        host.GetStyleRoot().UpdateStyles();
        Assert.Equal(0.4, host.Opacity);

        host.Set(UiPseudoClass.Hover, false);
        host.GetStyleRoot().UpdateStyles();
        Assert.Equal(1d, host.Opacity);
    }

    [Fact]
    public void PseudoClassStyleNotificationFailureKeepsCommittedSnapshot()
    {
        var host = new PseudoClassHost();
        ApplyTheme(host, Rule(
            UiStyleSelector.For<PseudoClassHost>(pseudoClasses: [Loading]),
            Setter(UiNode.OpacityProperty, 0.4)));
        var expected = new InvalidOperationException("style notification failed");
        var notifications = 0;
        host.PropertyChanged += (_, e) =>
        {
            if (!ReferenceEquals(e.Property, UiNode.OpacityProperty))
                return;
            notifications++;
            throw expected;
        };

        host.Set(Loading, true);
        Assert.Same(expected, Assert.Throws<InvalidOperationException>(host.GetStyleRoot().UpdateStyles));
        Assert.True(host.IsActive(Loading));
        Assert.Equal(0.4, host.Opacity);
        Assert.Single(host.GetStyleValueSources(UiNode.OpacityProperty));

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
                UiStyleSelector.For<PseudoClassHost>(pseudoClasses: [Loading]),
                Setter(UiNode.OpacityProperty, 0.4)));

        var refreshes = 0;
        host.PropertyChanged += (_, e) =>
            refreshes += ReferenceEquals(e.Property, UiNode.OpacityProperty) ? 1 : 0;

        host.Set(UiPseudoClass.Get("LOADING"), true);
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
                UiStyleSelector.For<PseudoClassHost>(pseudoClasses: [Loading]),
                Setter(UiNode.OpacityProperty, 0.4)));

        var refreshes = 0;
        host.PropertyChanged += (_, e) =>
            refreshes += ReferenceEquals(e.Property, UiNode.OpacityProperty) ? 1 : 0;

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
                UiStyleSelector.For<PseudoClassHost>(pseudoClasses: [Loading]),
                Setter(UiNode.OpacityProperty, 0.4)),
            Rule(
                UiStyleSelector.For<PseudoClassHost>(pseudoClasses: [Phantom]),
                Setter(UiNode.OpacityProperty, 0.7)));
        var notifications = 0;
        host.PropertyChanged += (_, e) =>
        {
            if (!ReferenceEquals(e.Property, UiNode.OpacityProperty))
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
                UiStyleSelector.For<PseudoClassHost>(pseudoClasses: [Phantom]),
                Setter(UiNode.OpacityProperty, 0.4)));

        Assert.Equal(1d, host.Opacity);

        host.Set(Phantom, true);
        host.GetStyleRoot().UpdateStyles();
        Assert.Equal(0.4, host.Opacity);
    }

    [Fact]
    public void OpenScreenStyleIdRejectsWrongThreadButAllowsNoOps()
    {
        var button = new Button { StyleId = "save" };
        var screen = new UiScreen(button);
        screen.Open();
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
        screen.Close();
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
        var screen = new UiScreen(node);
        screen.SetStyleSheets([UiStyleSheet.Parse(".next { opacity: 0.4; }")]);
        screen.Open();
        screen.Root!.UpdateStyles();
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
        screen.Root!.UpdateStyles();
        Assert.Equal(classes.Contains("next") ? 0.4 : 1d, node.Opacity);
        Assert.True(node.IsStyleValid);
        Assert.True(node.IsStyleSubtreeValid);
        screen.Close();
    }

    [Fact]
    public void OpenScreenPseudoClassInputsRejectWrongThreadButAllowNoOps()
    {
        var host = new PseudoClassHost();
        var screen = new UiScreen(host);
        screen.Open();
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
        screen.Close();
    }

    [Fact]
    public void PseudoClassInputsCanChangeDuringLayoutButNotDrawing()
    {
        var node = new StyleMutationNode();
        var screen = new UiScreen(node);
        Exception? layoutError = null;
        Exception? drawingError = null;
        node.MeasureAction = () => layoutError = Record.Exception(() => node.Set(Loading, true));
        node.DrawAction = () => drawingError = Record.Exception(() => node.Set(Loading, false));
        screen.Open();

        screen.PrepareFrame(new Size(20, 20), 0);
        _ = screen.CreateDrawCommandList();

        Assert.Null(layoutError);
        Assert.IsType<InvalidOperationException>(drawingError);
        Assert.True(node.IsActive(Loading));
        screen.Close();
    }

    [Fact]
    public void PseudoClassInputsInvalidateStylesDuringApplicationNotification()
    {
        var host = new PseudoClassHost();
        var screen = new UiScreen(host);
        screen.SetStyleSheets([
            new UiStyleSheet([
                Rule(UiStyleSelector.For<PseudoClassHost>(), Setter(UiNode.OpacityProperty, 0.3))
            ])
        ]);
        screen.Root!.UpdateStyles();
        Exception? error = null;
        host.PropertyChanged += (_, e) =>
        {
            if (ReferenceEquals(e.Property, UiNode.OpacityProperty))
                error = Record.Exception(() => host.Set(Loading, true));
        };

        screen.SetStyleSheets([
            new UiStyleSheet([
                Rule(UiStyleSelector.For<PseudoClassHost>(), Setter(UiNode.OpacityProperty, 0.8))
            ])
        ]);
        screen.Root!.UpdateStyles();

        Assert.Null(error);
        Assert.True(host.IsActive(Loading));
    }

    [Fact]
    public void ClassChangesDuringDrawingApplyStylesOnTheNextFrame()
    {
        var node = new StyleMutationNode();
        var screen = new UiScreen(node);
        screen.SetStyleSheets([UiStyleSheet.Parse(".layout { opacity: 0.4; } .drawing { opacity: 0.7; }")]);
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
        screen.Open();

        screen.PrepareFrame(new Size(20, 20), 0);
        _ = screen.CreateDrawCommandList();

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

        screen.PrepareFrame(new Size(20, 20), 0);

        Assert.Equal(0.7, node.Opacity);
        Assert.True(node.IsStyleValid);
        Assert.True(node.IsStyleSubtreeValid);
        Assert.Equal(2, collectionNotifications);
        screen.Close();
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
        var screen = new UiScreen(root);
        screen.SetStyleSheets([UiStyleSheet.Parse("#target { width: 10; } .wide #target { width: 20; }")]);
        var measuredWidths = new List<double>();
        var arrangedWidths = new List<double>();
        second.MeasureAction = () => measuredWidths.Add(second.Width);
        second.ArrangeAction = () => arrangedWidths.Add(second.Width);
        if (duringArrange)
            first.ArrangeAction = () => root.Classes.Add("wide");
        else
            first.MeasureAction = () => root.Classes.Add("wide");
        screen.Open();
        try
        {
            screen.PrepareFrame(new Size(100, 100), 0);

            Assert.Equal(new[] { 10d, 20d }, measuredWidths);
            Assert.Equal(duringArrange ? new[] { 10d, 20d } : new[] { 20d }, arrangedWidths);
            Assert.Equal(20, second.LayoutBounds.Width);
            Assert.True(root.IsMeasureValid);
            Assert.True(root.IsArrangeValid);
        }
        finally
        {
            screen.Close();
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
    public void LayoutAndStyleApplicationDoNotBlockUnrelatedScreens(bool duringLayout)
    {
        var node = new StyleMutationNode();
        var screen = new UiScreen(node);
        var other = new UiScreen(new Panel());
        var visited = false;
        Action updateOther = () =>
        {
            visited = true;
            Assert.True(screen.IsUpdatingLayout);
            Assert.Equal(!duringLayout, node.IsUpdatingStyles);
            Assert.False(other.IsUpdatingLayout);
            Assert.False(other.Root!.IsUpdatingStyles);
            other.Root = new Panel();
            other.Scale = 2;
            other.SetStyleSheets([UiStyleSheet.Parse("Panel { opacity: 0.6; }")]);
            other.Root!.UpdateStyles();
            Assert.Equal(0.6, other.Root!.Opacity);
            Assert.True(screen.IsUpdatingLayout);
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
                if (args.Property == UiNode.OpacityProperty)
                    updateOther();
            };
        screen.SetStyleSheets([UiStyleSheet.Parse("* { opacity: 0.4; }")]);
        screen.Open();
        try
        {
            screen.PrepareFrame(new Size(100, 100), 0);

            Assert.True(visited);
            Assert.False(screen.IsUpdatingLayout);
            Assert.False(node.IsUpdatingStyles);
        }
        finally
        {
            screen.Close();
        }
    }

    private static void ApplyTheme(UiNode node, params UiStyleRule[] rules)
    {
        var screen = new UiScreen(node);
        screen.SetStyleSheets([new UiStyleSheet(rules)]);
        screen.Root!.UpdateStyles();
    }

    private static UiStyleSelector Selector<TNode>(
        string? id = null,
        params string[] classes)
        where TNode : UiNode =>
        UiStyleSelector.For<TNode>(classes, id);

    private static UiStyleRule Rule(UiStyleSelector selector, params UiStyleSetter[] setters) =>
        new(selector, setters);

    private static UiStyleSetter Setter<T>(Property<T> property, T value) =>
        UiStyleSetter.Create(property, value);

    private static Brush Brush(byte value) => new SolidColorBrush(value, value, value);

    private static Brush Brush(byte r, byte g, byte b) => new SolidColorBrush(r, g, b);

    private sealed class EquatableNode : UiNode
    {
        internal static readonly Property<EquatableValue> ValueProperty =
            Property.Register<EquatableNode, EquatableValue>(
                "Value",
                new EquatableValue(0));
    }

    private sealed class EquatableValue(int value) : IEquatable<EquatableValue>
    {
        public bool Equals(EquatableValue? other) => other is not null && value == other.Value;

        private int Value => value;
    }

    private class PseudoClassHost : UiNode
    {
        internal void Set(UiPseudoClass pseudoClass, bool active) => SetPseudoClass(pseudoClass, active);

        internal bool IsActive(UiPseudoClass pseudoClass) => HasPseudoClass(pseudoClass);
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

    private sealed class GuardedStyleNode : UiNode
    {
        internal static readonly Property<GuardedValue> ValueProperty =
            Property.Register<GuardedStyleNode, GuardedValue>("Value", new GuardedValue(0));
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

        protected override void DrawCore(UiDrawingContext context) => DrawAction?.Invoke();

        protected override void ArrangeCore(Size finalSize) => ArrangeAction?.Invoke();
    }
}
