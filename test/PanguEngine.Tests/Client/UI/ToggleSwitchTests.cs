using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Drawing;
using PanguEngine.Client.UI.Styling;
using PanguEngine.Input;

namespace PanguEngine.Tests.Client.UI;

public sealed class ToggleSwitchTests
{
    [Fact]
    public void SameValueDoesNotNotifyAndClearValueResetsState()
    {
        var control = new ToggleSwitch();
        var changes = new List<bool>();
        using var subscription = control.Subscribe(ToggleSwitch.IsOnProperty,
            (_, args) => changes.Add(args.NewValue));

        Assert.False(control.IsOn);
        control.IsOn = true;
        control.IsOn = true;
        control.ClearValue(ToggleSwitch.IsOnProperty);

        Assert.Equal([true, false], changes);
        Assert.False(control.IsOn);
        Assert.False(control.HasPseudoClass(UiPseudoClass.Get("checked")));
    }

    [Fact]
    public void StateIsCommittedBeforeSubscriptionsAndSurvivesTheirExceptions()
    {
        var control = new ToggleSwitch();
        var error = new InvalidOperationException("state subscriber");
        using var subscription = control.Subscribe(ToggleSwitch.IsOnProperty, (_, args) =>
        {
            Assert.Equal(args.NewValue, control.IsOn);
            Assert.True(control.HasPseudoClass(UiPseudoClass.Get("checked")));
            throw error;
        });

        var actual = Assert.Throws<InvalidOperationException>(() => control.IsOn = true);

        Assert.Same(error, actual);
        Assert.True(control.IsOn);
        Assert.True(control.HasPseudoClass(UiPseudoClass.Get("checked")));
    }

    [Fact]
    public void ReentrantStateChangeKeepsTheFinalPseudoClassAndBindingValue()
    {
        var source = new ToggleSwitch();
        var target = new ToggleSwitch();
        target.PropertyChanged += (_, args) =>
        {
            if (ReferenceEquals(args.Property, ToggleSwitch.IsOnProperty) && target.IsOn)
                target.IsOn = false;
        };
        target.BindTwoWay(ToggleSwitch.IsOnProperty, source, ToggleSwitch.IsOnProperty);

        target.IsOn = true;

        Assert.False(source.IsOn);
        Assert.False(target.IsOn);
        Assert.False(target.HasPseudoClass(UiPseudoClass.Get("checked")));
    }

    [Fact]
    public void ClickingTheThumbTogglesOnceAndTargetsTheControl()
    {
        var control = new ToggleSwitch();
        var (manager, _, _) = OpenScene(control);
        try
        {
            var thumb = FindThumb(control);
            var point = control.LocalToScreen(new Point(
                thumb.LayoutBounds.X + thumb.LayoutBounds.Width / 2,
                thumb.LayoutBounds.Y + thumb.LayoutBounds.Height / 2));
            UiNode? source = null;
            var changes = 0;
            control.PointerClicked += (_, args) => source = args.Source;
            using var subscription = control.Subscribe(ToggleSwitch.IsOnProperty, (_, _) => changes++);

            Click(manager, point);

            Assert.Same(control, source);
            Assert.True(control.IsOn);
            Assert.Equal(1, changes);
            Assert.False(control.IsPressed);
        }
        finally
        {
            manager.Destroy();
        }
    }

    [Theory]
    [InlineData(MouseButton.Right)]
    [InlineData(MouseButton.Middle)]
    public void OtherButtonsDoNotToggleAndContinueBubbling(MouseButton button)
    {
        var control = new ToggleSwitch();
        var (manager, _, root) = OpenScene(control);
        try
        {
            var bubbled = false;
            root.PointerClicked += (_, args) => bubbled = args.Button == button;

            Click(manager, new Point(20, 20), button);

            Assert.False(control.IsOn);
            Assert.True(bubbled);
        }
        finally
        {
            manager.Destroy();
        }
    }

    [Fact]
    public void ReleasingOutsideDoesNotToggle()
    {
        var control = new ToggleSwitch();
        var (manager, _, _) = OpenScene(control);
        try
        {
            manager.ProcessPointerPressed(new Point(20, 20), MouseButton.Left, KeyModifiers.None);
            manager.ProcessPointerReleased(new Point(100, 70), MouseButton.Left, KeyModifiers.None);

            Assert.False(control.IsOn);
            Assert.False(control.IsPressed);
        }
        finally
        {
            manager.Destroy();
        }
    }

    [Theory]
    [InlineData("disabled")]
    [InlineData("ancestor-disabled")]
    [InlineData("hidden")]
    [InlineData("ancestor-hidden")]
    [InlineData("detached")]
    [InlineData("closed")]
    [InlineData("window-focus")]
    public void UnavailabilityCancelsPendingPointerClick(string reason)
    {
        var control = new ToggleSwitch();
        var (manager, _, root) = OpenScene(control);
        try
        {
            manager.ProcessPointerPressed(new Point(20, 20), MouseButton.Left, KeyModifiers.None);
            MakeUnavailable(reason, manager, root, control);
            manager.ProcessPointerReleased(new Point(20, 20), MouseButton.Left, KeyModifiers.None);

            Assert.False(control.IsOn);
            Assert.False(control.IsPressed);
        }
        finally
        {
            manager.Destroy();
        }
    }

    [Fact]
    public void HandledPointerClickSuppressesDefaultToggle()
    {
        var control = new ToggleSwitch();
        var (manager, _, _) = OpenScene(control);
        try
        {
            control.PointerClicked += (_, args) => args.Handled = true;

            Click(manager, new Point(20, 20));

            Assert.False(control.IsOn);
        }
        finally
        {
            manager.Destroy();
        }
    }

    [Fact]
    public void DisablingAnAncestorInPointerClickSuppressesDefaultToggle()
    {
        var control = new ToggleSwitch();
        var (manager, _, root) = OpenScene(control);
        try
        {
            control.PointerClicked += (_, _) => root.IsEnabled = false;

            Click(manager, new Point(20, 20));

            Assert.False(control.IsOn);
        }
        finally
        {
            manager.Destroy();
        }
    }

    [Fact]
    public void PointerCallbackExceptionSuppressesToggleAndPreservesException()
    {
        var control = new ToggleSwitch();
        var (manager, _, _) = OpenScene(control);
        try
        {
            var error = new InvalidOperationException("pointer callback");
            control.PointerClicked += (_, _) => throw error;
            manager.ProcessPointerPressed(new Point(20, 20), MouseButton.Left, KeyModifiers.None);

            var actual = Assert.Throws<InvalidOperationException>(() =>
                manager.ProcessPointerReleased(new Point(20, 20), MouseButton.Left, KeyModifiers.None));

            Assert.Same(error, actual);
            Assert.False(control.IsOn);
            Assert.False(control.IsPressed);
        }
        finally
        {
            manager.Destroy();
        }
    }

    [Fact]
    public void EnterTogglesOnInitialDownOnly()
    {
        var control = new ToggleSwitch();
        var (manager, _, _) = OpenScene(control);
        try
        {
            Assert.True(control.Focus());
            Assert.True(manager.ProcessKeyDown(Key.Enter, KeyModifiers.None));
            Assert.True(control.IsOn);
            manager.ProcessKeyDown(Key.Enter, KeyModifiers.None, true);
            manager.ProcessKeyDown(Key.Enter, KeyModifiers.None);
            manager.ProcessKeyUp(Key.Enter, KeyModifiers.None);
            Assert.True(control.IsOn);

            manager.ProcessKeyDown(Key.Enter, KeyModifiers.None);

            Assert.False(control.IsOn);
        }
        finally
        {
            manager.Destroy();
        }
    }

    [Fact]
    public void SpaceTogglesOnlyOnPairedRelease()
    {
        var control = new ToggleSwitch();
        var (manager, _, _) = OpenScene(control);
        try
        {
            Assert.True(control.Focus());
            manager.ProcessKeyUp(Key.Space, KeyModifiers.None);
            Assert.False(control.IsOn);
            manager.ProcessKeyDown(Key.Space, KeyModifiers.None);
            manager.ProcessKeyDown(Key.Space, KeyModifiers.None, true);
            Assert.False(control.IsOn);
            Assert.True(control.HasPseudoClass(UiPseudoClass.Pressed));
            Assert.False(control.IsPressed);

            manager.ProcessKeyUp(Key.Space, KeyModifiers.None);

            Assert.True(control.IsOn);
            Assert.False(control.HasPseudoClass(UiPseudoClass.Pressed));
            manager.ProcessKeyUp(Key.Space, KeyModifiers.None);
            Assert.True(control.IsOn);
        }
        finally
        {
            manager.Destroy();
        }
    }

    [Theory]
    [InlineData(Key.Space)]
    [InlineData(Key.Enter)]
    public void RepeatAfterFocusLossCannotReactivateAKey(Key key)
    {
        var control = new ToggleSwitch();
        var (manager, screen, _) = OpenScene(control);
        try
        {
            Assert.True(control.Focus());
            manager.ProcessKeyDown(key, KeyModifiers.None);
            var state = control.IsOn;
            screen.ClearFocus();
            Assert.True(control.Focus());

            manager.ProcessKeyDown(key, KeyModifiers.None, true);
            manager.ProcessKeyUp(key, KeyModifiers.None);

            Assert.Equal(state, control.IsOn);
            Assert.False(control.HasPseudoClass(UiPseudoClass.Pressed));
        }
        finally
        {
            manager.Destroy();
        }
    }

    [Theory]
    [InlineData("disabled")]
    [InlineData("ancestor-disabled")]
    [InlineData("hidden")]
    [InlineData("ancestor-hidden")]
    [InlineData("detached")]
    [InlineData("closed")]
    [InlineData("window-focus")]
    public void UnavailabilityCancelsSpaceEvenAfterInputIsRestored(string reason)
    {
        var control = new ToggleSwitch();
        var (manager, screen, root) = OpenScene(control);
        try
        {
            Assert.True(control.Focus());
            manager.ProcessKeyDown(Key.Space, KeyModifiers.None);
            MakeUnavailable(reason, manager, root, control);
            Assert.False(control.HasPseudoClass(UiPseudoClass.Pressed));
            control.IsEnabled = true;
            control.Visibility = Visibility.Visible;
            root.IsEnabled = true;
            root.Visibility = Visibility.Visible;
            if (control.Parent is null)
                root.Children.Add(control);
            if (manager.CurrentScreen is null)
                manager.Open(screen);
            manager.ProcessFocusChanged(true);
            manager.UpdateFrame(new Size(120, 80), 0);
            Assert.True(control.Focus());

            manager.ProcessKeyDown(Key.Space, KeyModifiers.None, true);
            manager.ProcessKeyUp(Key.Space, KeyModifiers.None);

            Assert.False(control.IsOn);
        }
        finally
        {
            manager.Destroy();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CanceledOrThrowingSpaceReleaseConsumesThePair(bool throws)
    {
        var control = new ToggleSwitch();
        var (manager, _, _) = OpenScene(control);
        try
        {
            var error = new InvalidOperationException("key callback");
            control.KeyUp += (_, args) =>
            {
                if (throws)
                    throw error;
                args.Handled = true;
            };
            Assert.True(control.Focus());
            manager.ProcessKeyDown(Key.Space, KeyModifiers.None);

            if (throws)
                Assert.Same(error, Assert.Throws<InvalidOperationException>(() =>
                    manager.ProcessKeyUp(Key.Space, KeyModifiers.None)));
            else
                manager.ProcessKeyUp(Key.Space, KeyModifiers.None);

            Assert.False(control.IsOn);
            Assert.False(control.HasPseudoClass(UiPseudoClass.Pressed));
        }
        finally
        {
            manager.Destroy();
        }
    }

    [Fact]
    public void LosingFocusInSpaceReleaseCallbackSuppressesDefaultToggle()
    {
        var control = new ToggleSwitch();
        var (manager, screen, _) = OpenScene(control);
        try
        {
            Assert.True(control.Focus());
            control.KeyUp += (_, _) => screen.ClearFocus();
            manager.ProcessKeyDown(Key.Space, KeyModifiers.None);

            manager.ProcessKeyUp(Key.Space, KeyModifiers.None);

            Assert.False(control.IsOn);
            Assert.False(control.HasPseudoClass(UiPseudoClass.Pressed));
        }
        finally
        {
            manager.Destroy();
        }
    }

    [Theory]
    [InlineData(Key.Space)]
    [InlineData(Key.Enter)]
    public void RefocusingInKeyDownCallbackAllowsDefaultKeyboardAction(Key key)
    {
        var control = new ToggleSwitch();
        var (manager, screen, _) = OpenScene(control);
        try
        {
            Assert.True(control.Focus());
            control.KeyDown += (_, _) =>
            {
                screen.ClearFocus();
                Assert.True(control.Focus());
            };

            manager.ProcessKeyDown(key, KeyModifiers.None);
            manager.ProcessKeyUp(key, KeyModifiers.None);

            Assert.True(control.IsOn);
            Assert.False(control.HasPseudoClass(UiPseudoClass.Pressed));
        }
        finally
        {
            manager.Destroy();
        }
    }

    [Fact]
    public void RefocusingInSpaceReleaseCallbackAllowsToggleAndWriteBack()
    {
        var source = new ToggleSwitch();
        var control = new ToggleSwitch();
        control.BindTwoWay(ToggleSwitch.IsOnProperty, source, ToggleSwitch.IsOnProperty);
        var (manager, screen, _) = OpenScene(control);
        try
        {
            Assert.True(control.Focus());
            control.KeyUp += (_, _) =>
            {
                screen.ClearFocus();
                Assert.True(control.Focus());
            };
            manager.ProcessKeyDown(Key.Space, KeyModifiers.None);

            manager.ProcessKeyUp(Key.Space, KeyModifiers.None);

            Assert.True(control.IsOn);
            Assert.True(source.IsOn);
            Assert.False(control.HasPseudoClass(UiPseudoClass.Pressed));
            manager.ProcessKeyUp(Key.Space, KeyModifiers.None);
            Assert.True(control.IsOn);
        }
        finally
        {
            manager.Destroy();
        }
    }

    [Fact]
    public void UserClickWritesThroughTwoWayBindingAndUnbindPreservesState()
    {
        var source = new ToggleSwitch { IsOn = true };
        var target = new ToggleSwitch();
        target.BindTwoWay(ToggleSwitch.IsOnProperty, source, ToggleSwitch.IsOnProperty);
        Assert.True(target.IsOn);
        var (manager, _, _) = OpenScene(target);
        try
        {
            Click(manager, new Point(20, 20));

            Assert.False(source.IsOn);
            Assert.False(target.IsOn);
            Assert.True(target.IsBound(ToggleSwitch.IsOnProperty));
            source.IsOn = true;
            Assert.True(target.IsOn);
            target.Unbind(ToggleSwitch.IsOnProperty);
            source.IsOn = false;
            Assert.True(target.IsOn);
        }
        finally
        {
            manager.Destroy();
        }
    }

    [Fact]
    public void OneWayUserClickKeepsBindingWithoutWritingBack()
    {
        var source = new ToggleSwitch();
        var target = new ToggleSwitch();
        target.Bind(ToggleSwitch.IsOnProperty, source, ToggleSwitch.IsOnProperty);
        var (manager, _, _) = OpenScene(target);
        try
        {
            Click(manager, new Point(20, 20));

            Assert.True(target.IsOn);
            Assert.False(source.IsOn);
            Assert.True(target.IsBound(ToggleSwitch.IsOnProperty));
            source.IsOn = true;
            source.IsOn = false;
            Assert.False(target.IsOn);
            source.IsOn = true;
            target.ClearValue(ToggleSwitch.IsOnProperty);
            Assert.False(target.IsBound(ToggleSwitch.IsOnProperty));
            Assert.False(target.IsOn);
            Assert.True(source.IsOn);
            source.IsOn = false;
            source.IsOn = true;
            Assert.False(target.IsOn);
        }
        finally
        {
            manager.Destroy();
        }
    }

    [Fact]
    public void DisabledControlStillAcceptsProgrammaticAndBoundStateUpdates()
    {
        var source = new ToggleSwitch();
        var target = new ToggleSwitch { IsEnabled = false };
        target.BindTwoWay(ToggleSwitch.IsOnProperty, source, ToggleSwitch.IsOnProperty);
        var (manager, _, _) = OpenScene(target);
        try
        {
            Click(manager, new Point(20, 20));
            Assert.False(target.IsOn);
            Assert.False(target.Focus());

            target.IsOn = true;
            Assert.True(source.IsOn);
            source.IsOn = false;
            Assert.False(target.IsOn);
        }
        finally
        {
            manager.Destroy();
        }
    }

    [Theory]
    [InlineData(1d, false)]
    [InlineData(1.25d, false)]
    [InlineData(1.25d, true)]
    [InlineData(2d, true)]
    public void StateMovesThumbBetweenContentEndpoints(double scale, bool rounding)
    {
        var control = new ToggleSwitch();
        var (manager, screen, _) = OpenScene(control);
        try
        {
            screen.Scale = scale;
            screen.UseLayoutRounding = rounding;
            manager.UpdateFrame(new Size(120, 80), 0);
            var thumb = FindThumb(control);
            var off = thumb.LayoutBounds;
            var tolerance = rounding ? 1 / scale : 1e-9;
            Assert.InRange(Math.Abs(off.X - control.ContentBounds.X), 0, tolerance);
            AssertCentered(control.ContentBounds, off, tolerance);

            control.IsOn = true;
            Assert.False(control.IsArrangeValid);
            manager.UpdateFrame(new Size(120, 80), 0);
            var on = thumb.LayoutBounds;

            Assert.True(on.X > off.X);
            Assert.InRange(Math.Abs(on.X + on.Width -
                (control.ContentBounds.X + control.ContentBounds.Width)), 0, tolerance);
            AssertCentered(control.ContentBounds, on, tolerance);
        }
        finally
        {
            manager.Destroy();
        }
    }

    [Theory]
    [InlineData(12d, 10d, 1d, false)]
    [InlineData(0d, 0d, 1d, false)]
    [InlineData(12d, 10d, 1.25d, true)]
    [InlineData(0d, 0d, 1.25d, true)]
    [InlineData(12d, 10d, 1.25d, false)]
    public void AutomaticThumbShrinksToAvailableContent(
        double width, double height, double scale, bool rounding)
    {
        var control = new ToggleSwitch { Width = width, Height = height };
        var (manager, screen, _) = OpenScene(control);
        try
        {
            screen.Scale = scale;
            screen.UseLayoutRounding = rounding;
            var tolerance = rounding ? 1 / scale : 1e-9;
            foreach (var isOn in new[] { false, true })
            {
                control.IsOn = isOn;
                manager.UpdateFrame(new Size(120, 80), 0);
                var thumb = FindThumb(control).LayoutBounds;

                Assert.InRange(thumb.Width, 0, control.ContentBounds.Width + tolerance);
                Assert.InRange(thumb.Height, 0, control.ContentBounds.Height + tolerance);
                Assert.InRange(Math.Abs(thumb.X - control.ContentBounds.X), 0, tolerance);
                AssertCentered(control.ContentBounds, thumb, tolerance);
            }
        }
        finally
        {
            manager.Destroy();
        }
    }

    [Fact]
    public void AuthorStylesConfigureTrackAndThumbAndLocalValuesTakePrecedence()
    {
        var control = new ToggleSwitch();
        var (manager, screen, _) = OpenScene(control);
        try
        {
            screen.SetStyleSheets([UiStyleSheet.Parse("""
                ToggleSwitch { width: 60px; padding: 4px; background-color: #112233; }
                ToggleSwitch:checked { background-color: #445566; }
                ToggleSwitch .toggle-switch-thumb { padding: 5px; background-color: #778899; }
                """)]);
            manager.UpdateFrame(new Size(120, 80), 0);
            Assert.Equal(60, control.LayoutBounds.Width);
            Assert.Equal(new SolidColorBrush(17, 34, 51), control.Background);
            var thumb = FindThumb(control);
            Assert.Equal(new Thickness(5), thumb.Padding);
            Assert.Equal(new SolidColorBrush(119, 136, 153), thumb.Background);

            control.IsOn = true;
            manager.UpdateFrame(new Size(120, 80), 0);
            Assert.Equal(new SolidColorBrush(68, 85, 102), control.Background);
            var local = new SolidColorBrush(1, 2, 3);
            control.Background = local;
            control.IsOn = false;
            manager.UpdateFrame(new Size(120, 80), 0);
            Assert.Same(local, control.Background);
            control.ClearValue(Region.BackgroundProperty);
            Assert.Equal(new SolidColorBrush(17, 34, 51), control.Background);
            Assert.Null(UiCssRegistry.FindProperty(typeof(ToggleSwitch), "is-on"));
        }
        finally
        {
            manager.Destroy();
        }
    }

    [Fact]
    public void DisabledStyleWinsOverCheckedHoverAndPressedCombinations()
    {
        var control = new ToggleSwitch();
        var (manager, screen, _) = OpenScene(control);
        try
        {
            control.IsEnabled = false;
            manager.UpdateFrame(new Size(120, 80), 0);
            var disabledBackground = control.Background;
            var disabledBorder = control.BorderBrush;
            var thumbBackground = FindThumb(control).Background;
            control.IsOn = true;
            control.SetHovered(true);
            control.SetPressed(true);

            manager.UpdateFrame(new Size(120, 80), 0);

            Assert.Equal(disabledBackground, control.Background);
            Assert.Equal(disabledBorder, control.BorderBrush);
            Assert.Equal(thumbBackground, FindThumb(control).Background);
        }
        finally
        {
            manager.Destroy();
        }
    }

    [Fact]
    public void DefaultStylesRespondToHoverPressFocusAndCheckedHover()
    {
        var control = new ToggleSwitch();
        var (manager, _, root) = OpenScene(control);
        try
        {
            var normalBackground = control.Background;
            var normalBorder = control.BorderBrush;
            control.SetHovered(true);
            root.UpdateStyles();
            var hoveredBackground = control.Background;
            Assert.NotEqual(normalBackground, hoveredBackground);

            control.SetPressed(true);
            root.UpdateStyles();
            Assert.NotEqual(hoveredBackground, control.Background);
            control.SetPressed(false);
            control.SetHovered(false);
            Assert.True(control.Focus());
            root.UpdateStyles();
            Assert.NotEqual(normalBorder, control.BorderBrush);

            control.IsOn = true;
            root.UpdateStyles();
            var checkedBackground = control.Background;
            control.SetHovered(true);
            root.UpdateStyles();
            Assert.NotEqual(checkedBackground, control.Background);
        }
        finally
        {
            manager.Destroy();
        }
    }

    private static (UiManager Manager, GameScreen Screen, Canvas Root) OpenScene(ToggleSwitch control)
    {
        var root = new Canvas();
        Canvas.SetLeft(control, 10);
        Canvas.SetTop(control, 10);
        root.Children.Add(control);
        var screen = new GameScreen(root) { UseLayoutRounding = false };
        var manager = new UiManager();
        manager.Open(screen);
        manager.UpdateFrame(new Size(120, 80), 0);
        return (manager, screen, root);
    }

    private static void Click(UiManager manager, Point point, MouseButton button = MouseButton.Left)
    {
        manager.ProcessPointerPressed(point, button, KeyModifiers.None);
        manager.ProcessPointerReleased(point, button, KeyModifiers.None);
    }

    private static Panel FindThumb(ToggleSwitch control) =>
        Assert.IsType<Panel>(control.ReadOnlyChildren.Single(
            node => node.Classes.Contains("toggle-switch-thumb")));

    private static void AssertCentered(Rect content, Rect thumb, double tolerance) =>
        Assert.InRange(Math.Abs(content.Y + content.Height / 2 - thumb.Y - thumb.Height / 2), 0, tolerance);

    private static void MakeUnavailable(string reason, UiManager manager, Canvas root, ToggleSwitch control)
    {
        switch (reason)
        {
            case "disabled":
                control.IsEnabled = false;
                break;
            case "ancestor-disabled":
                root.IsEnabled = false;
                break;
            case "hidden":
                control.Visibility = Visibility.Hidden;
                break;
            case "ancestor-hidden":
                root.Visibility = Visibility.Hidden;
                break;
            case "detached":
                root.Children.Remove(control);
                break;
            case "closed":
                manager.Close();
                break;
            case "window-focus":
                manager.ProcessFocusChanged(false);
                break;
        }
    }
}
