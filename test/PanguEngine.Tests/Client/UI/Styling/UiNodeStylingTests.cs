using System.Reflection;
using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Drawing;
using PanguEngine.Client.UI.Styling;

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
        Assert.False(button.Classes.Add("primary"));
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
        Assert.Equal(1, changes);

        button.StyleId = "save";
        Assert.Equal(1, changes);

        button.StyleId = "other";
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
        Assert.Equal(Brush(9), button.Background);

        button.SetHovered(false);
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
    public void EarlierBatchHandlerCanLocallyMaskLaterStyleNotification()
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
    }

    [Fact]
    public void ReentrantStyleChangeProcessesPendingRecompute()
    {
        var button = new Button();
        ApplyTheme(
            button,
            Rule(Selector<Button>(classes: ["x"]), Setter(Region.BackgroundProperty, Brush(5))),
            Rule(Selector<Button>(classes: ["extra"]), Setter(Region.BackgroundProperty, Brush(9))));

        var backgroundNotifications = 0;
        button.PropertyChanged += (_, e) =>
        {
            if (!ReferenceEquals(e.Property, Region.BackgroundProperty))
                return;
            backgroundNotifications++;
            if (!button.Classes.Contains("extra"))
                button.Classes.Add("extra");
        };

        button.Classes.Add("x");

        Assert.Equal(Brush(9), button.Background);
        Assert.Equal(2, backgroundNotifications);
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

        var actual = Assert.Throws<InvalidOperationException>(() => button.Classes.Add("x"));

        Assert.Same(expected, actual);
        Assert.Equal(Brush(5), button.Background);
        Assert.True(button.Classes.Contains("extra"));
        Assert.Equal(1, notifications);
    }

    [Fact]
    public void FreshButtonReadResolvesDefaultBaseStyles()
    {
        var button = new Button();

        Assert.Equal("Button", Assert.Single(button.GetStyleValueSources(Region.BackgroundProperty)).SelectorText);
        Assert.Equal(new SolidColorBrush(48, 54, 62), button.Background);
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

        var source = Assert.Single(button.GetStyleValueSources(Region.BackgroundProperty));

        Assert.True(source.IsMaskedByLocalValue);
        Assert.Equal("Button.primary", source.SelectorText);
        Assert.Equal(UiStyleOrigin.Author, source.Origin);
        Assert.Equal(0, source.SheetIndex);
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
    public void StyleSelectorInputsRollbackWhenResolutionFails()
    {
        var node = new GuardedStyleNode { StyleId = "old" };
        var sheet = new UiStyleSheet([
            Rule(
                UiStyleSelector.For<GuardedStyleNode>(),
                Setter(GuardedStyleNode.ValueProperty, new GuardedValue(0.5)))
        ]);
        var screen = new UiScreen(node);
        screen.SetStyleSheets([sheet]);
        GuardedValue.ThrowOnCompare = true;
        try
        {
            Assert.Throws<InvalidOperationException>(() => node.StyleId = "new");
            Assert.Throws<InvalidOperationException>(() => node.Classes.Add("primary"));
        }
        finally
        {
            GuardedValue.ThrowOnCompare = false;
        }

        Assert.Equal("old", node.StyleId);
        Assert.Empty(node.Classes);
        Assert.Same(screen, node.Screen);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SelectorInputsCannotReenterDuringNodeResolution(bool changeId)
    {
        var node = new ReentrantPreparationNode { StyleId = "original" };
        var screen = new UiScreen(node);
        screen.SetStyleSheets([
            new UiStyleSheet([
                Rule(
                    UiStyleSelector.For<ReentrantPreparationNode>(),
                    Setter(ReentrantPreparationNode.ValueProperty, new GuardedValue(0.5)))
            ])
        ]);
        node.OnRead = () =>
        {
            if (changeId)
                node.StyleId = "nested";
            else
                node.Classes.Add("nested");
        };
        GuardedValue.OnCompare = () => node.OnRead?.Invoke();
        try
        {
            Assert.Throws<InvalidOperationException>(() => node.Classes.Add("outer"));
        }
        finally
        {
            GuardedValue.OnCompare = null;
        }

        Assert.Empty(node.Classes);
        Assert.Equal("original", node.StyleId);
        Assert.Equal(1d, node.Opacity);
        node.OnRead = null;
        Assert.True(node.Classes.Add("accepted"));
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
        var notifications = 0;
        node.PropertyChanged += (_, e) =>
            notifications += ReferenceEquals(e.Property, EquatableNode.ValueProperty) ? 1 : 0;

        screen.SetStyleSheets([secondSheet]);

        Assert.Equal(0, notifications);
    }

    [Fact]
    public void PseudoClassHandlerExceptionStillUpdatesStyle()
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

        Assert.Equal(Brush(9), button.Background);
    }

    [Fact]
    public void PseudoClassPreparationFailureKeepsOldSnapshotAndRetriesOnlyOnRealChange()
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
            Assert.Throws<InvalidOperationException>(() => node.SetHovered(true));
        }
        finally
        {
            GuardedValue.ThrowOnCompare = false;
        }

        Assert.True(node.HasPseudoClass(UiPseudoClass.Hover));
        Assert.Equal(0.25, node.Opacity);
        Assert.Equal(0, hoverEvents);
        Assert.Equal(0, hoverSubscriptions);

        node.SetHovered(true);
        Assert.Equal(0.25, node.Opacity);

        node.SetHovered(false);
        Assert.Equal(0.25, node.Opacity);
        node.SetHovered(true);
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

        Assert.True(control.HasPseudoClass(Loading));
        Assert.Equal(Brush(6), control.Background);

        control.SetLoading(false);

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
        Assert.Equal(0.4, host.Opacity);

        host.Set(Loading, false);
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
        Assert.Equal(1d, host.Opacity);

        host.Set(Loading, true);
        Assert.Equal(0.4, host.Opacity);

        host.Set(UiPseudoClass.Hover, false);
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

        Assert.Same(expected, Assert.Throws<InvalidOperationException>(() => host.Set(Loading, true)));
        Assert.True(host.IsActive(Loading));
        Assert.Equal(0.4, host.Opacity);
        Assert.Single(host.GetStyleValueSources(UiNode.OpacityProperty));

        host.Set(Loading, true);
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
        Assert.Equal(0.4, host.Opacity);

        host.Set(Loading, true);
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
        Assert.Equal(0, refreshes);

        host.Set(Loading, true);
        Assert.Equal(1, refreshes);

        host.Set(Loading, true);
        Assert.Equal(1, refreshes);
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
        Assert.Equal(0.4, host.Opacity);
    }

    [Fact]
    public void OpenScreenStyleInputsRejectWrongThreadButAllowNoOps()
    {
        var button = new Button { StyleId = "save" };
        button.Classes.Add("primary");
        var screen = new UiScreen(button);
        screen.Open();
        Exception? styleIdError = null;
        Exception? classError = null;
        Exception? noOpError = null;
        var thread = new Thread(() =>
        {
            styleIdError = Record.Exception(() => button.StyleId = "other");
            classError = Record.Exception(() => button.Classes.Add("wide"));
            noOpError = Record.Exception(() =>
            {
                button.StyleId = "save";
                Assert.False(button.Classes.Add("primary"));
            });
        });

        thread.Start();
        thread.Join();

        Assert.IsType<InvalidOperationException>(styleIdError);
        Assert.IsType<InvalidOperationException>(classError);
        Assert.Null(noOpError);
        Assert.Equal("save", button.StyleId);
        Assert.DoesNotContain("wide", button.Classes);
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
    public void PseudoClassInputsCannotChangeDuringLayoutOrDrawing()
    {
        var node = new StyleMutationNode();
        var screen = new UiScreen(node);
        Exception? layoutError = null;
        Exception? drawingError = null;
        node.MeasureAction = () => layoutError = Record.Exception(() => node.Set(Loading, true));
        node.DrawAction = () => drawingError = Record.Exception(() => node.Set(Loading, true));
        screen.Open();

        screen.PrepareFrame(new Size(20, 20), 0);
        _ = screen.CreateDrawCommandList();

        Assert.IsType<InvalidOperationException>(layoutError);
        Assert.IsType<InvalidOperationException>(drawingError);
        Assert.False(node.IsActive(Loading));
        screen.Close();
    }

    [Fact]
    public void PseudoClassInputsAreRejectedDuringStyleSheetApplicationNotification()
    {
        var host = new PseudoClassHost();
        var screen = new UiScreen(host);
        screen.SetStyleSheets([
            new UiStyleSheet([
                Rule(UiStyleSelector.For<PseudoClassHost>(), Setter(UiNode.OpacityProperty, 0.3))
            ])
        ]);
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

        Assert.IsType<InvalidOperationException>(error);
        Assert.False(host.IsActive(Loading));
    }

    [Fact]
    public void StyleInputsCannotChangeDuringLayoutOrDrawing()
    {
        var node = new StyleMutationNode();
        var screen = new UiScreen(node);
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

        Assert.IsType<InvalidOperationException>(layoutStyleIdError);
        Assert.IsType<InvalidOperationException>(layoutClassError);
        Assert.IsType<InvalidOperationException>(drawingStyleIdError);
        Assert.IsType<InvalidOperationException>(drawingClassError);
        Assert.Null(node.StyleId);
        Assert.Empty(node.Classes);
        screen.Close();
    }

    [Fact]
    public void UiStyleClassCollectionHasNoPublicParameterlessConstructor()
    {
        var publicCtor = typeof(UiStyleClassCollection).GetConstructor(
            BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
        Assert.Null(publicCtor);

        var internalCtor = typeof(UiStyleClassCollection).GetConstructor(
            BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(UiNode) }, null);
        Assert.NotNull(internalCtor);
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

    private static void ApplyTheme(UiNode node, params UiStyleRule[] rules)
    {
        var screen = new UiScreen(node);
        screen.SetStyleSheets([new UiStyleSheet(rules)]);
    }

    private static UiStyleSelector Selector<TNode>(
        string? id = null,
        params string[] classes)
        where TNode : UiNode =>
        UiStyleSelector.For<TNode>(classes, id);

    private static UiStyleRule Rule(UiStyleSelector selector, params UiStyleSetter[] setters) =>
        new(selector, setters);

    private static UiStyleSetter Setter<T>(UiProperty<T> property, T value) =>
        UiStyleSetter.Create(property, value);

    private static Brush Brush(byte value) => new SolidColorBrush(value, value, value);

    private static Brush Brush(byte r, byte g, byte b) => new SolidColorBrush(r, g, b);

    private sealed class EquatableNode : UiNode
    {
        internal static readonly UiProperty<EquatableValue> ValueProperty =
            UiProperty.Register<EquatableNode, EquatableValue>(
                "Value",
                new EquatableValue(0),
                UiPropertyInvalidation.Render);
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
        internal static readonly UiProperty<GuardedValue> ValueProperty =
            UiProperty.Register<GuardedStyleNode, GuardedValue>("Value", new GuardedValue(0));
    }

    private sealed class ReentrantPreparationNode : UiNode
    {
        internal static readonly UiProperty<GuardedValue> ValueProperty =
            UiProperty.Register<ReentrantPreparationNode, GuardedValue>("Value", new GuardedValue(0));

        internal Action? OnRead { get; set; }
    }

    private sealed class CustomStateControl : Control
    {
        internal void SetLoading(bool value) => SetPseudoClass(Loading, value);
    }

    private sealed class StyleMutationNode : PseudoClassHost
    {
        internal Action? MeasureAction { get; set; }
        internal Action? DrawAction { get; set; }

        protected override Size MeasureCore(Size availableSize)
        {
            MeasureAction?.Invoke();
            return Size.Zero;
        }

        protected override void DrawCore(UiDrawingContext context) => DrawAction?.Invoke();
    }
}