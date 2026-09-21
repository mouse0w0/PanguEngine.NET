using System.Reflection;
using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Drawing;
using PanguEngine.Client.UI.Styling;

namespace PanguEngine.Tests.Client.UI.Styling;

public sealed class UiNodeStylingTests
{
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
    public void PseudoStateChangeTriggersStyleRefresh()
    {
        var button = new Button();
        ApplyTheme(button, Rule(
            UiStyleSelector.For<Button>(states: UiPseudoStates.Hovered),
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
    public void StyleChangeExceptionContinuesRemainingNotifications()
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
        Assert.Equal(new[] { "Background", "BorderBrush" }, order);
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
    public void NotificationFailureStillProcessesPendingRecompute()
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
        Assert.Equal(Brush(9), button.Background);
        Assert.Equal(2, notifications);
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
        var node = new FailingStyleNode { StyleId = "old" };
        var sheet = new UiStyleSheet([
            Rule(
                UiStyleSelector.For<FailingStyleNode>(states: UiPseudoStates.Disabled),
                Setter(UiNode.OpacityProperty, 0.5))
        ]);
        var screen = new UiScreen(node);
        screen.SetStyleSheets([sheet]);
        node.ThrowOnStyleStateRead = true;

        Assert.Throws<InvalidOperationException>(() => node.StyleId = "new");
        Assert.Throws<InvalidOperationException>(() => node.Classes.Add("primary"));

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
        screen.SetStyleSheets([new UiStyleSheet([
            Rule(
                UiStyleSelector.For<ReentrantPreparationNode>(states: UiPseudoStates.Hovered),
                Setter(UiNode.OpacityProperty, 0.5))
        ])]);
        node.OnRead = () =>
        {
            if (changeId)
                node.StyleId = "nested";
            else
                node.Classes.Add("nested");
        };

        Assert.Throws<InvalidOperationException>(() => node.Classes.Add("outer"));

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
    public void PseudoStateHandlerExceptionStillUpdatesStyle()
    {
        var button = new Button();
        ApplyTheme(button, Rule(
            UiStyleSelector.For<Button>(states: UiPseudoStates.Hovered),
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
    public void PseudoStateResolutionFailureClearsSnapshotForRetry()
    {
        var node = new FailOncePseudoNode();
        ApplyTheme(
            node,
            Rule(
                UiStyleSelector.For<FailOncePseudoNode>(states: UiPseudoStates.Hovered),
                Setter(UiNode.OpacityProperty, 0.5)));
        node.FailNextStyleStateRead = true;

        Assert.Throws<InvalidOperationException>(() => node.SetHovered(true));

        Assert.Equal(0.5, node.Opacity);
    }

    [Fact]
    public void CustomControlCanRefreshExtendedPseudoState()
    {
        var control = new CustomStateControl();
        ApplyTheme(
            control,
            Rule(
                UiStyleSelector.For<CustomStateControl>(states: UiPseudoStates.Pressed),
                Setter(Region.BackgroundProperty, Brush(6))));

        control.SetCustomPressed(true);

        Assert.Equal(Brush(6), control.Background);
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

    private sealed class FailingStyleNode : UiNode
    {
        internal bool ThrowOnStyleStateRead { get; set; }

        protected override UiPseudoStates GetStylePseudoStates()
        {
            if (ThrowOnStyleStateRead)
                throw new InvalidOperationException("style state failed");
            return base.GetStylePseudoStates();
        }
    }

    private sealed class ReentrantPreparationNode : UiNode
    {
        internal Action? OnRead { get; set; }

        protected override UiPseudoStates GetStylePseudoStates()
        {
            OnRead?.Invoke();
            return base.GetStylePseudoStates();
        }
    }

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

    private sealed class FailOncePseudoNode : UiNode
    {
        internal bool FailNextStyleStateRead { get; set; }

        protected override UiPseudoStates GetStylePseudoStates()
        {
            if (FailNextStyleStateRead)
            {
                FailNextStyleStateRead = false;
                throw new InvalidOperationException("style state failed once");
            }

            return base.GetStylePseudoStates();
        }
    }

    private sealed class CustomStateControl : Control
    {
        private bool _customPressed;

        internal void SetCustomPressed(bool value)
        {
            _customPressed = value;
            RefreshStylePseudoStates();
        }

        protected override UiPseudoStates GetStylePseudoStates()
        {
            var states = base.GetStylePseudoStates();
            if (_customPressed)
                states |= UiPseudoStates.Pressed;
            return states;
        }
    }

    private sealed class StyleMutationNode : UiNode
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
