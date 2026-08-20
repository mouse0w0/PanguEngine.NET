using System.Runtime.ExceptionServices;
using PanguEngine.Client.UI;
using PanguEngine.Input;

namespace PanguEngine.Tests.Client.UI;

public sealed class UiInputRoutingTests
{
    [Fact]
    public void InputEventArgsExposeSourceHandledAndRelativePositions()
    {
        var (manager, _, root) = OpenScene();
        var leaf = Place(root, new TestNode(), 10, 20, 30, 40);
        UiPointerEventArgs? childArgs = null;
        UiPointerEventArgs? rootArgs = null;
        leaf.PointerMoved += (_, eventArgs) => childArgs = eventArgs;
        root.PointerMoved += (_, eventArgs) => rootArgs = eventArgs;

        manager.ProcessPointerMoved(new Point(15, 27));

        Assert.NotNull(childArgs);
        Assert.Same(childArgs, rootArgs);
        Assert.Same(leaf, childArgs.Source);
        Assert.False(childArgs.Handled);
        Assert.Equal(new Point(15, 27), childArgs.ScreenPosition);
        Assert.Equal(new Point(5, 7), childArgs.GetPosition(leaf));
        Assert.Equal(new Point(15, 27), childArgs.GetPosition(root));
        Assert.Throws<ArgumentNullException>(() => childArgs.GetPosition(null!));
        Assert.Throws<ArgumentException>(() => childArgs.GetPosition(new TestNode()));
    }

    [Fact]
    public void GetPositionForOffPathNodeUsesEventScreenPointAndCurrentTree()
    {
        var (manager, _, root) = OpenScene();
        var leaf = Place(root, new TestNode(), 0, 0, 20, 20);
        var sibling = Place(root, new TestNode(), 30, 0, 20, 20);
        Point? actual = null;
        UiPointerEventArgs? captured = null;
        leaf.PointerMoved += (_, eventArgs) =>
        {
            root.Arrange(new Rect(10, 0, 100, 100));
            actual = eventArgs.GetPosition(sibling);
            captured = eventArgs;
        };

        manager.ProcessPointerMoved(new Point(5, 5));

        Assert.Equal(new Point(-35, 5), actual);
        var threadError = RunOnBackgroundThread(() =>
            Record.Exception(() => captured!.GetPosition(sibling)));
        Assert.IsType<InvalidOperationException>(threadError);
    }

    [Fact]
    public void GetPositionForOffPathNodeRejectsNonFiniteAccumulation()
    {
        var root = new Canvas();
        var sibling = new TestNode();
        root.Children.Add(sibling);
        var screen = new UiScreen(root);
        screen.Open();
        root.Measure(new Size(1, 1));
        root.Arrange(new Rect(-double.MaxValue, 0, 1, 1));
        sibling.Measure(new Size(1, 1));
        sibling.Arrange(new Rect(0, 0, 1, 1));
        var eventArgs = new UiPointerEventArgs(
            root,
            new Point(double.MaxValue, 0),
            [new UiHitPathEntry(root, Point.Zero)]);

        Assert.Throws<ArgumentOutOfRangeException>(() => eventArgs.GetPosition(sibling));
        screen.Close();
    }

    [Fact]
    public void ManagerInputWithoutScreenIsNoOpOnOwnerThreadAndRejectsWrongThread()
    {
        var manager = new UiManager();

        manager.ProcessPointerMoved(Point.Zero);
        manager.ProcessPointerPressed(Point.Zero, MouseButton.Unknown, KeyModifiers.None);
        manager.ProcessPointerReleased(Point.Zero, (MouseButton)13, KeyModifiers.None);
        manager.ProcessPointerWheel(Point.Zero, double.NaN, double.PositiveInfinity);
        manager.ProcessKeyDown(Key.A, KeyModifiers.Shift);
        manager.ProcessKeyUp(Key.A, KeyModifiers.Shift);

        var threadError = RunOnBackgroundThread(() =>
            Record.Exception(() =>
            {
                manager.ProcessPointerMoved(Point.Zero);
                manager.ProcessPointerPressed(Point.Zero, MouseButton.Unknown, KeyModifiers.None);
                manager.ProcessPointerReleased(Point.Zero, (MouseButton)13, KeyModifiers.None);
                manager.ProcessPointerWheel(Point.Zero, double.NaN, double.PositiveInfinity);
                manager.ProcessKeyDown(Key.A, KeyModifiers.None);
                manager.ProcessKeyUp(Key.A, KeyModifiers.None);
            }));
        Assert.IsType<InvalidOperationException>(threadError);
    }

    [Fact]
    public void PointerMoveDiffsHoverThenBubblesMovedFromLeaf()
    {
        var (manager, _, root) = OpenScene();
        var first = Place(root, new TestNode(), 0, 0, 20, 20);
        var second = Place(root, new TestNode(), 30, 0, 20, 20);
        var events = new List<string>();
        root.PointerEntered += (_, _) => events.Add("root-enter");
        root.PointerMoved += (_, _) => events.Add("root-move");
        first.PointerEntered += (_, _) => events.Add("first-enter");
        first.PointerExited += (_, _) => events.Add("first-exit");
        first.PointerMoved += (_, _) => events.Add("first-move");
        second.PointerEntered += (_, _) => events.Add("second-enter");
        second.PointerMoved += (_, _) => events.Add("second-move");

        manager.ProcessPointerMoved(new Point(5, 5));

        Assert.Equal(
            ["root-enter", "first-enter", "first-move", "root-move"],
            events);
        events.Clear();

        manager.ProcessPointerMoved(new Point(35, 5));

        Assert.Equal(
            ["first-exit", "second-enter", "second-move", "root-move"],
            events);
    }

    [Fact]
    public void HandledStopsOnlyTheCurrentBubbleRoute()
    {
        var (manager, _, root) = OpenScene();
        var leaf = Place(root, new TestNode(), 0, 0, 20, 20);
        var events = new List<string>();
        leaf.PointerMoved += (_, eventArgs) =>
        {
            events.Add("leaf-move");
            eventArgs.Handled = true;
        };
        root.PointerMoved += (_, _) => events.Add("root-move");
        leaf.PointerWheel += (_, _) => events.Add("leaf-wheel");
        root.PointerWheel += (_, _) => events.Add("root-wheel");

        manager.ProcessPointerMoved(new Point(5, 5));
        manager.ProcessPointerWheel(new Point(5, 5), 1, -2);

        Assert.Equal(["leaf-move", "leaf-wheel", "root-wheel"], events);
    }

    [Fact]
    public void WheelBubblesFromCurrentHitWithoutChangingFocusOrPressPairing()
    {
        var (manager, screen, root) = OpenScene();
        var leaf = Place(root, new TestNode { Focusable = true }, 0, 0, 20, 20);
        var wheels = new List<(UiNode Source, double X, double Y)>();
        leaf.PointerWheel += (_, eventArgs) =>
            wheels.Add((eventArgs.Source, eventArgs.DeltaX, eventArgs.DeltaY));
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);

        manager.ProcessPointerWheel(new Point(5, 5), 2.5, -3.5);
        manager.ProcessPointerReleased(new Point(5, 5), MouseButton.Left, KeyModifiers.None);

        Assert.Same(leaf, screen.FocusedNode);
        Assert.Equal([(leaf, 2.5, -3.5)], wheels);
    }

    [Fact]
    public void PressFocusesNearestEligibleAncestorButRoutesToDeepestLeaf()
    {
        var (manager, screen, root) = OpenScene();
        var container = Place(root, new Canvas { Focusable = true }, 10, 10, 40, 40);
        var leaf = Place(container, new TestNode(), 5, 5, 10, 10);
        var events = new List<string>();
        leaf.PointerPressed += (_, _) => events.Add("leaf");
        container.PointerPressed += (_, _) => events.Add("container");
        root.PointerPressed += (_, _) => events.Add("root");

        manager.ProcessPointerPressed(new Point(16, 16), MouseButton.Left, KeyModifiers.None);

        Assert.Same(container, screen.FocusedNode);
        Assert.Equal(["leaf", "container", "root"], events);
    }

    [Fact]
    public void PressWithoutEligibleCandidateClearsFocusBeforePressed()
    {
        var (manager, screen, root) = OpenScene();
        var focused = Place(root, new TestNode { Focusable = true }, 0, 0, 20, 20);
        var target = Place(root, new TestNode(), 30, 0, 20, 20);
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        manager.ProcessPointerReleased(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        UiNode? focusDuringPressed = focused;
        target.PointerPressed += (_, _) => focusDuringPressed = screen.FocusedNode;

        manager.ProcessPointerPressed(new Point(35, 5), MouseButton.Left, KeyModifiers.None);

        Assert.Null(focusDuringPressed);
        Assert.Null(screen.FocusedNode);
    }

    [Fact]
    public void ButtonEventsUsePressAndReleaseModifierSnapshots()
    {
        var (manager, _, root) = OpenScene();
        var leaf = Place(root, new TestNode(), 0, 0, 20, 20);
        KeyModifiers? pressed = null;
        KeyModifiers? released = null;
        KeyModifiers? clicked = null;
        leaf.PointerPressed += (_, eventArgs) => pressed = eventArgs.Modifiers;
        leaf.PointerReleased += (_, eventArgs) => released = eventArgs.Modifiers;
        leaf.PointerClicked += (_, eventArgs) => clicked = eventArgs.Modifiers;

        manager.ProcessPointerPressed(
            new Point(5, 5),
            MouseButton.Left,
            KeyModifiers.Control);
        manager.ProcessPointerReleased(
            new Point(5, 5),
            MouseButton.Left,
            KeyModifiers.Shift | KeyModifiers.Alt);

        Assert.Equal(KeyModifiers.Control, pressed);
        Assert.Equal(KeyModifiers.Shift | KeyModifiers.Alt, released);
        Assert.Equal(KeyModifiers.Shift | KeyModifiers.Alt, clicked);
    }

    [Fact]
    public void MouseButtonsPairIndependentlyAndRepeatedPressReplacesTarget()
    {
        var (manager, _, root) = OpenScene();
        var first = Place(root, new TestNode(), 0, 0, 20, 20);
        var second = Place(root, new TestNode(), 30, 0, 20, 20);
        var events = new List<string>();
        first.PointerReleased += (_, eventArgs) => events.Add($"first-{eventArgs.Button}");
        second.PointerReleased += (_, eventArgs) => events.Add($"second-{eventArgs.Button}");

        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        manager.ProcessPointerPressed(new Point(35, 5), MouseButton.Right, KeyModifiers.None);
        manager.ProcessPointerPressed(new Point(35, 5), MouseButton.Left, KeyModifiers.None);
        manager.ProcessPointerReleased(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        manager.ProcessPointerReleased(new Point(5, 5), MouseButton.Right, KeyModifiers.None);
        manager.ProcessPointerPressed(new Point(35, 5), MouseButton.Button12, KeyModifiers.None);
        manager.ProcessPointerReleased(new Point(35, 5), MouseButton.Button12, KeyModifiers.None);

        Assert.Equal(["second-Left", "second-Right", "second-Button12"], events);
    }

    [Fact]
    public void ReleaseRoutesToPressTargetAndClicksOnlyWhenSameLeafIsHit()
    {
        var (manager, _, root) = OpenScene();
        var first = Place(root, new TestNode(), 0, 0, 20, 20);
        var second = Place(root, new TestNode(), 30, 0, 20, 20);
        var events = new List<string>();
        first.PointerReleased += (_, _) => events.Add("first-release");
        first.PointerClicked += (_, _) => events.Add("first-click");
        second.PointerReleased += (_, _) => events.Add("second-release");
        second.PointerClicked += (_, _) => events.Add("second-click");

        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        manager.ProcessPointerReleased(new Point(35, 5), MouseButton.Left, KeyModifiers.None);

        Assert.Equal(["first-release"], events);
        events.Clear();

        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        manager.ProcessPointerReleased(new Point(5, 5), MouseButton.Left, KeyModifiers.None);

        Assert.Equal(["first-release", "first-click"], events);
    }

    [Fact]
    public void PointerReleasedHandledDoesNotCancelPointerClicked()
    {
        var (manager, _, root) = OpenScene();
        var leaf = Place(root, new TestNode(), 0, 0, 20, 20);
        var events = new List<string>();
        leaf.PointerReleased += (_, eventArgs) =>
        {
            events.Add("release");
            eventArgs.Handled = true;
        };
        root.PointerReleased += (_, _) => events.Add("root-release");
        leaf.PointerClicked += (_, _) => events.Add("click");

        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        manager.ProcessPointerReleased(new Point(5, 5), MouseButton.Left, KeyModifiers.None);

        Assert.Equal(["release", "click"], events);
    }

    [Fact]
    public void PointerReleasedExceptionClearsPairAndSuppressesPointerClicked()
    {
        var (manager, _, root) = OpenScene();
        var leaf = Place(root, new TestNode(), 0, 0, 20, 20);
        var expected = new InvalidOperationException("release failed");
        var clicked = 0;
        EventHandler<UiPointerButtonEventArgs> throwingHandler = (_, _) => throw expected;
        leaf.PointerReleased += throwingHandler;
        leaf.PointerClicked += (_, _) => clicked++;
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);

        var actual = Assert.Throws<InvalidOperationException>(() =>
            manager.ProcessPointerReleased(new Point(5, 5), MouseButton.Left, KeyModifiers.None));
        leaf.PointerReleased -= throwingHandler;
        manager.ProcessPointerReleased(new Point(5, 5), MouseButton.Left, KeyModifiers.None);

        Assert.Same(expected, actual);
        Assert.Equal(0, clicked);
    }

    [Theory]
    [InlineData(MouseButton.Unknown)]
    [InlineData((MouseButton)13)]
    [InlineData((MouseButton)int.MaxValue)]
    public void ScreenRejectsInvalidMouseButtonsBeforeRouting(MouseButton button)
    {
        var (_, screen, root) = OpenScene();
        var leaf = Place(root, new TestNode(), 0, 0, 20, 20);
        var events = 0;
        leaf.PointerEntered += (_, _) => events++;
        leaf.PointerPressed += (_, _) => events++;
        leaf.PointerReleased += (_, _) => events++;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            screen.ProcessPointerPressed(new Point(5, 5), button, KeyModifiers.None));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            screen.ProcessPointerReleased(new Point(5, 5), button, KeyModifiers.None));
        Assert.Equal(0, events);
    }

    [Fact]
    public void ScreenRejectsNonFiniteWheelDeltasBeforeRouting()
    {
        var (_, screen, root) = OpenScene();
        var leaf = Place(root, new TestNode(), 0, 0, 20, 20);
        var events = 0;
        leaf.PointerEntered += (_, _) => events++;
        leaf.PointerWheel += (_, _) => events++;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            screen.ProcessPointerWheel(new Point(5, 5), double.NaN, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            screen.ProcessPointerWheel(new Point(5, 5), 0, double.PositiveInfinity));
        Assert.Equal(0, events);
    }

    [Fact]
    public void ScreenInputRejectsWrongOwnerThread()
    {
        var (_, screen, _) = OpenScene();

        var errors = RunOnBackgroundThread(() =>
            (Pointer: Record.Exception(() => screen.ProcessPointerMoved(Point.Zero)),
                Key: Record.Exception(() => screen.ProcessKeyDown(Key.A, KeyModifiers.None))));

        Assert.IsType<InvalidOperationException>(errors.Pointer);
        Assert.IsType<InvalidOperationException>(errors.Key);
    }

    [Fact]
    public void FocusLossClearsFocusHoverPressedAndPointerState()
    {
        var (manager, screen, root) = OpenScene();
        var control = Place(root, new TestControl { Focusable = true }, 0, 0, 20, 20);
        var events = new List<string>();
        control.LostFocus += (_, _) => events.Add("lost");
        control.PointerExited += (_, _) => events.Add("exit");
        manager.ProcessPointerMoved(new Point(5, 5));
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        Assert.True(control.IsFocused);
        Assert.True(control.IsHovered);
        Assert.True(control.IsPressed);

        manager.ProcessFocusChanged(false);
        manager.ProcessFocusChanged(true);
        manager.Update(new Size(100, 100));

        Assert.False(control.IsFocused);
        Assert.False(control.IsHovered);
        Assert.False(control.IsPressed);
        Assert.Null(screen.FocusedNode);
        Assert.Equal(["lost", "exit"], events);
    }

    [Fact]
    public void FocusLossAggregatesNotificationFailuresAfterStateCommit()
    {
        var (manager, screen, root) = OpenScene();
        var control = Place(root, new TestControl { Focusable = true }, 0, 0, 20, 20);
        var focusedError = new InvalidOperationException("focused");
        var pressedError = new InvalidOperationException("pressed");
        var hoveredError = new InvalidOperationException("hovered");
        var lostError = new InvalidOperationException("lost");
        var exitError = new InvalidOperationException("exit");
        _ = control.Subscribe(UiNode.IsFocusedProperty, (_, args) =>
        {
            if (!args.NewValue)
                throw focusedError;
        });
        _ = control.Subscribe(Control.IsPressedProperty, (_, args) =>
        {
            if (!args.NewValue)
                throw pressedError;
        });
        _ = control.Subscribe(UiNode.IsHoveredProperty, (_, args) =>
        {
            if (!args.NewValue)
                throw hoveredError;
        });
        control.LostFocus += (_, _) => throw lostError;
        control.PointerExited += (_, _) => throw exitError;
        manager.ProcessPointerMoved(new Point(5, 5));
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);

        var aggregate = Assert.Throws<AggregateException>(() => manager.ProcessFocusChanged(false));

        Assert.Equal(
            [focusedError, pressedError, hoveredError, lostError, exitError],
            aggregate.InnerExceptions);
        Assert.False(control.IsFocused);
        Assert.False(control.IsHovered);
        Assert.False(control.IsPressed);
        Assert.Null(screen.FocusedNode);
    }

    [Fact]
    public void FocusRequiresActiveVisibleArrangedFocusableNode()
    {
        var inactive = new TestNode { Focusable = true };
        Assert.False(inactive.Focus());

        var (manager, screen, root) = OpenScene();
        var eligible = Place(root, new TestNode { Focusable = true }, 0, 0, 20, 20);
        var hitTestInvisible = Place(
            root,
            new TestNode { Focusable = true, IsHitTestVisible = false },
            30,
            0,
            20,
            20);
        var hidden = Place(
            root,
            new TestNode { Focusable = true, Visibility = Visibility.Hidden },
            60,
            0,
            20,
            20);

        Assert.True(eligible.Focus());
        Assert.Same(eligible, screen.FocusedNode);
        Assert.True(hitTestInvisible.Focus());
        Assert.Same(hitTestInvisible, screen.FocusedNode);
        Assert.False(hidden.Focus());
        Assert.Same(hitTestInvisible, screen.FocusedNode);

        eligible.InvalidateArrange();
        Assert.False(eligible.Focus());
        manager.Update(new Size(100, 100));
    }

    [Fact]
    public void FocusAndClearFocusCommitBeforeDirectNotifications()
    {
        var (_, screen, root) = OpenScene();
        var first = Place(root, new TestNode { Focusable = true }, 0, 0, 20, 20);
        var second = Place(root, new TestNode { Focusable = true }, 30, 0, 20, 20);
        var events = new List<string>();
        first.GotFocus += (_, eventArgs) =>
            events.Add($"first-got:{ReferenceEquals(screen.FocusedNode, first)}:{eventArgs.OldFocus is null}");
        first.LostFocus += (_, eventArgs) =>
            events.Add($"first-lost:{ReferenceEquals(screen.FocusedNode, second)}:{ReferenceEquals(eventArgs.NewFocus, second)}");
        second.GotFocus += (_, eventArgs) =>
            events.Add($"second-got:{ReferenceEquals(screen.FocusedNode, second)}:{ReferenceEquals(eventArgs.OldFocus, first)}");
        second.LostFocus += (_, eventArgs) =>
            events.Add($"second-lost:{screen.FocusedNode is null}:{eventArgs.NewFocus is null}");

        Assert.True(first.Focus());
        Assert.True(second.Focus());
        screen.ClearFocus();

        Assert.Null(screen.FocusedNode);
        Assert.Equal(
            [
                "first-got:True:True",
                "first-lost:True:True",
                "second-got:True:True",
                "second-lost:True:True"
            ],
            events);
    }

    [Fact]
    public void RepeatedFocusIsANoOpAndKeyEventsBubbleFromFocusedNode()
    {
        var (manager, screen, root) = OpenScene();
        var leaf = Place(root, new TestNode { Focusable = true }, 0, 0, 20, 20);
        var focusCalls = 0;
        var events = new List<string>();
        leaf.GotFocus += (_, _) => focusCalls++;
        leaf.KeyDown += (_, eventArgs) => events.Add($"leaf-down:{eventArgs.Key}:{eventArgs.Modifiers}");
        root.KeyDown += (_, _) => events.Add("root-down");
        leaf.KeyUp += (_, _) => events.Add("leaf-up");
        root.KeyUp += (_, eventArgs) =>
        {
            events.Add("root-up");
            eventArgs.Handled = true;
        };

        Assert.True(leaf.Focus());
        Assert.True(leaf.Focus());
        manager.ProcessKeyDown(Key.A, KeyModifiers.Control);
        manager.ProcessKeyUp(Key.A, KeyModifiers.Control);
        screen.ClearFocus();
        manager.ProcessKeyDown(Key.A, KeyModifiers.None);

        Assert.Equal(1, focusCalls);
        Assert.Equal(
            ["leaf-down:A:Control", "root-down", "leaf-up", "root-up"],
            events);
    }

    [Fact]
    public void InvalidFocusIsClearedBeforeKeyboardRouting()
    {
        var (manager, screen, root) = OpenScene();
        var leaf = Place(root, new TestNode { Focusable = true }, 0, 0, 20, 20);
        var events = new List<string>();
        leaf.LostFocus += (_, _) => events.Add("lost");
        leaf.KeyDown += (_, _) => events.Add("key");
        Assert.True(leaf.Focus());
        leaf.Focusable = false;

        manager.ProcessKeyDown(Key.A, KeyModifiers.None);

        Assert.Null(screen.FocusedNode);
        Assert.Equal(["lost"], events);
    }

    [Fact]
    public void FocusNotificationErrorsCompleteAndAggregateInOrder()
    {
        var (_, screen, root) = OpenScene();
        var first = Place(root, new TestNode { Focusable = true }, 0, 0, 20, 20);
        var second = Place(root, new TestNode { Focusable = true }, 30, 0, 20, 20);
        var lostError = new InvalidOperationException("lost");
        var gotError = new InvalidOperationException("got");
        Assert.True(first.Focus());
        first.LostFocus += (_, _) => throw lostError;
        second.GotFocus += (_, _) => throw gotError;

        var aggregate = Assert.Throws<AggregateException>(() => second.Focus());

        Assert.Same(second, screen.FocusedNode);
        Assert.Equal([lostError, gotError], aggregate.InnerExceptions);
    }

    [Fact]
    public void SingleFocusNotificationErrorKeepsCommittedFocusAndOriginalException()
    {
        var (_, screen, root) = OpenScene();
        var first = Place(root, new TestNode { Focusable = true }, 0, 0, 20, 20);
        var second = Place(root, new TestNode { Focusable = true }, 30, 0, 20, 20);
        var expected = new InvalidOperationException("lost");
        Assert.True(first.Focus());
        first.LostFocus += (_, _) => throw expected;

        var actual = Assert.Throws<InvalidOperationException>(() => second.Focus());

        Assert.Same(expected, actual);
        Assert.Same(second, screen.FocusedNode);
    }

    [Fact]
    public void SynchronousFocusReentryFailsAndPostedFocusRunsOnNextUpdate()
    {
        var (manager, screen, root) = OpenScene();
        var first = Place(root, new TestNode { Focusable = true }, 0, 0, 20, 20);
        var second = Place(root, new TestNode { Focusable = true }, 30, 0, 20, 20);
        Exception? reentryError = null;
        first.GotFocus += (_, _) =>
        {
            reentryError = Record.Exception(() =>
            {
                first.Focus();
            });
            screen.Post(() => second.Focus());
        };

        Assert.True(first.Focus());
        Assert.Same(first, screen.FocusedNode);
        Assert.IsType<InvalidOperationException>(reentryError);

        manager.Update(new Size(100, 100));

        Assert.Same(second, screen.FocusedNode);
    }

    [Fact]
    public void BubbleSnapshotSkipsClosedScreenAndNeverEntersReplacementScreen()
    {
        var (manager, _, root) = OpenScene();
        var leaf = Place(root, new TestNode(), 0, 0, 20, 20);
        var replacementRoot = new Canvas();
        var replacement = new UiScreen(replacementRoot);
        var events = new List<string>();
        leaf.PointerMoved += (_, _) =>
        {
            events.Add("old-leaf");
            manager.Open(replacement);
        };
        root.PointerMoved += (_, _) => events.Add("old-root");
        replacementRoot.PointerMoved += (_, _) => events.Add("new-root");

        manager.ProcessPointerMoved(new Point(5, 5));

        Assert.Equal(["old-leaf"], events);
        Assert.Same(replacement, manager.CurrentScreen);
    }

    [Fact]
    public void SameScreenReparentPreservesFocusPressAndDefersHoverDiff()
    {
        var (manager, screen, root) = OpenScene();
        var oldParent = Place(root, new Canvas(), 0, 0, 40, 40);
        var newParent = Place(root, new Canvas(), 50, 0, 40, 40);
        var leaf = Place(oldParent, new TestNode { Focusable = true }, 0, 0, 20, 20);
        var events = new List<string>();
        leaf.PointerExited += (_, _) => events.Add("leaf-exit");
        leaf.PointerEntered += (_, _) => events.Add("leaf-enter");
        leaf.PointerClicked += (_, _) => events.Add("leaf-click");
        manager.ProcessPointerMoved(new Point(5, 5));
        Assert.True(leaf.Focus());
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        events.Clear();

        newParent.Children.Add(leaf);

        Assert.Same(leaf, screen.FocusedNode);
        Assert.Empty(events);

        manager.Update(new Size(100, 100));
        manager.ProcessPointerReleased(new Point(55, 5), MouseButton.Left, KeyModifiers.None);

        Assert.Equal(["leaf-exit", "leaf-enter", "leaf-click"], events);
        Assert.Same(leaf, screen.FocusedNode);
    }

    [Fact]
    public void CrossScreenMoveClearsOldScreenInteractionState()
    {
        var (oldManager, oldScreen, oldRoot) = OpenScene();
        var leaf = Place(oldRoot, new TestNode { Focusable = true }, 0, 0, 20, 20);
        var (newManager, newScreen, newRoot) = OpenScene();
        var events = new List<string>();
        leaf.LostFocus += (_, _) => events.Add("lost");
        leaf.PointerExited += (_, _) => events.Add("exit");
        leaf.PointerReleased += (_, _) => events.Add("release");
        oldManager.ProcessPointerMoved(new Point(5, 5));
        Assert.True(leaf.Focus());
        oldManager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        events.Clear();

        newRoot.Children.Add(leaf);

        Assert.Equal(["lost", "exit"], events);
        Assert.Null(oldScreen.FocusedNode);
        Assert.Same(newScreen, leaf.Screen);

        oldManager.ProcessPointerReleased(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        Assert.DoesNotContain("release", events);
        newManager.Update(new Size(100, 100));
    }

    [Fact]
    public void SubtreeRemovalClearsFocusPressAndExitsOldHoverPathAfterCommit()
    {
        var (manager, screen, root) = OpenScene();
        var branch = Place(root, new Canvas(), 10, 10, 30, 30);
        var leaf = Place(branch, new TestNode { Focusable = true }, 0, 0, 20, 20);
        var events = new List<string>();
        Point? exitPosition = null;
        Exception? conversionError = null;
        leaf.LostFocus += (_, _) =>
            events.Add($"lost:{leaf.Parent is not null}:{leaf.Screen is null}");
        leaf.PointerExited += (_, eventArgs) =>
        {
            events.Add($"leaf-exit:{leaf.Screen is null}");
            exitPosition = eventArgs.GetPosition(leaf);
            conversionError = Record.Exception(() =>
                leaf.ScreenToLocal(eventArgs.ScreenPosition));
        };
        branch.PointerExited += (_, _) =>
            events.Add($"branch-exit:{branch.Screen is null}");
        leaf.PointerReleased += (_, _) => events.Add("release");
        manager.ProcessPointerMoved(new Point(15, 15));
        Assert.True(leaf.Focus());
        manager.ProcessPointerPressed(new Point(15, 15), MouseButton.Left, KeyModifiers.None);
        events.Clear();

        Assert.True(root.Children.Remove(branch));

        Assert.Null(screen.FocusedNode);
        Assert.Equal(
            ["lost:True:True", "leaf-exit:True", "branch-exit:True"],
            events);
        Assert.Equal(new Point(5, 5), exitPosition);
        Assert.IsType<InvalidOperationException>(conversionError);
        manager.ProcessPointerReleased(new Point(15, 15), MouseButton.Left, KeyModifiers.None);
        Assert.DoesNotContain("release", events);
    }

    [Fact]
    public void CleanupErrorsCompleteAllNotificationsAndLeaveCommittedState()
    {
        var (manager, screen, root) = OpenScene();
        var branch = Place(root, new Canvas(), 10, 10, 30, 30);
        var leaf = Place(branch, new TestNode { Focusable = true }, 0, 0, 20, 20);
        var lostError = new InvalidOperationException("lost");
        var exitError = new InvalidOperationException("exit");
        var notifications = new List<string>();
        leaf.LostFocus += (_, _) =>
        {
            notifications.Add("lost");
            throw lostError;
        };
        leaf.PointerExited += (_, _) =>
        {
            notifications.Add("exit");
            throw exitError;
        };
        branch.PointerExited += (_, _) => notifications.Add("branch-exit");
        manager.ProcessPointerMoved(new Point(15, 15));
        Assert.True(leaf.Focus());
        notifications.Clear();

        var aggregate = Assert.Throws<AggregateException>(() =>
        {
            root.Children.Remove(branch);
        });

        Assert.Equal([lostError, exitError], aggregate.InnerExceptions);
        Assert.Equal(["lost", "exit", "branch-exit"], notifications);
        Assert.DoesNotContain(branch, root.Children);
        Assert.Null(branch.Screen);
        Assert.Null(leaf.Screen);
        Assert.Null(screen.FocusedNode);
    }

    [Fact]
    public void ClearAndReplaceMergeRemovedSubtreesIntoOneScreenCleanup()
    {
        var (manager, _, root) = OpenScene();
        var first = Place(root, new TestNode(), 0, 0, 20, 20);
        var second = Place(root, new TestNode(), 30, 0, 20, 20);
        var replacement = new TestNode { Width = 20, Height = 20 };
        Canvas.SetLeft(replacement, 60);
        Canvas.SetTop(replacement, 0);
        var events = new List<string>();
        first.PointerExited += (_, _) => events.Add("first-exit");
        second.PointerExited += (_, _) => events.Add("second-exit");
        manager.ProcessPointerMoved(new Point(5, 5));

        root.Children[0] = replacement;
        root.Children.Clear();

        Assert.Equal(["first-exit"], events);
        Assert.DoesNotContain(first, root.Children);
        Assert.DoesNotContain(second, root.Children);
        Assert.Null(first.Screen);
        Assert.Null(second.Screen);
    }

    [Fact]
    public void UpdateRefreshesHoverAfterLayoutOrInputPropertyChanges()
    {
        var (manager, _, root) = OpenScene();
        var leaf = Place(root, new TestNode(), 0, 0, 20, 20);
        var events = new List<string>();
        leaf.PointerEntered += (_, _) => events.Add("enter");
        leaf.PointerExited += (_, _) => events.Add("exit");
        manager.ProcessPointerMoved(new Point(5, 5));
        events.Clear();

        leaf.IsHitTestVisible = false;
        manager.Update(new Size(100, 100));
        leaf.IsHitTestVisible = true;
        manager.Update(new Size(100, 100));
        Canvas.SetLeft(leaf, 30);
        manager.Update(new Size(100, 100));

        Assert.Equal(["exit", "enter", "exit"], events);
    }

    [Fact]
    public void ScaleChangeInHandlerKeepsCurrentEventAndInvalidatesNewHitTests()
    {
        var manager = new UiManager();
        var root = new Canvas();
        var leaf = new TestNode { Width = 10, Height = 10 };
        Canvas.SetLeft(leaf, 10);
        Canvas.SetTop(leaf, 0);
        root.Children.Add(leaf);
        var screen = new UiScreen(root) { Scale = 2 };
        UiPointerEventArgs? moved = null;
        UiPointerWheelEventArgs? wheel = null;
        leaf.PointerMoved += (_, eventArgs) =>
        {
            moved = eventArgs;
            screen.Scale = 4;
        };
        leaf.PointerWheel += (_, eventArgs) => wheel = eventArgs;
        manager.Open(screen);
        manager.Update(new Size(100, 80));

        manager.ProcessPointerMoved(new Point(24, 8));

        Assert.NotNull(moved);
        Assert.Equal(new Point(12, 4), moved.ScreenPosition);
        Assert.Equal(new Point(2, 4), moved.GetPosition(leaf));
        Assert.Equal(4, screen.Scale);
        Assert.Null(screen.HitTest(new Point(12, 4)));
        Assert.False(root.IsArrangeValid);
        Assert.False(leaf.IsArrangeValid);

        manager.Update(new Size(100, 80));
        manager.ProcessPointerWheel(new Point(48, 16), 3, -5);

        Assert.NotNull(wheel);
        Assert.Equal(new Point(12, 4), wheel.ScreenPosition);
        Assert.Equal(3, wheel.DeltaX);
        Assert.Equal(-5, wheel.DeltaY);
        manager.Close();
    }

    [Fact]
    public void MeasureCallbackReentrantPointerRoutingFailsAndRecoversAfterFinally()
    {
        var manager = new UiManager();
        var root = new LayoutActionNode();
        var screen = new UiScreen(root);
        manager.Open(screen);
        manager.Update(new Size(100, 100));

        root.MeasureAction = () => manager.ProcessPointerMoved(new Point(1, 1));

        Assert.Throws<InvalidOperationException>(() => manager.Update(new Size(200, 200)));
        root.MeasureAction = null;

        Assert.Null(Record.Exception(() =>
        {
            manager.Update(new Size(100, 100));
            manager.ProcessPointerMoved(new Point(1, 1));
        }));
        manager.Close();
    }

    [Fact]
    public void ArrangeCallbackReentrantPointerRoutingFailsAndRecoversAfterFinally()
    {
        var manager = new UiManager();
        var root = new LayoutActionNode();
        var screen = new UiScreen(root);
        manager.Open(screen);
        manager.Update(new Size(100, 100));

        root.ArrangeAction = () => manager.ProcessPointerMoved(new Point(1, 1));

        Assert.Throws<InvalidOperationException>(() => manager.Update(new Size(200, 200)));
        root.ArrangeAction = null;

        Assert.Null(Record.Exception(() =>
        {
            manager.Update(new Size(100, 100));
            manager.ProcessPointerMoved(new Point(1, 1));
        }));
        manager.Close();
    }

    [Fact]
    public void PendingActionInputUsesCurrentScaleAndSkipsInvalidLayout()
    {
        var manager = new UiManager();
        var root = new Canvas();
        var leaf = new TestNode { Width = 10, Height = 10 };
        Canvas.SetLeft(leaf, 15);
        Canvas.SetTop(leaf, 0);
        root.Children.Add(leaf);
        var screen = new UiScreen(root);
        var positions = new List<Point>();
        var leafPositions = new List<Point>();
        leaf.PointerMoved += (_, eventArgs) =>
        {
            positions.Add(eventArgs.ScreenPosition);
            leafPositions.Add(eventArgs.GetPosition(leaf));
        };
        manager.Open(screen);
        manager.Update(new Size(100, 80));

        screen.Post(() =>
        {
            screen.Scale = 2;
            manager.ProcessPointerMoved(new Point(24, 8));
        });
        manager.Update(new Size(100, 80));
        manager.ProcessPointerMoved(new Point(40, 16));

        Assert.Equal(2, screen.Scale);
        Assert.Equal([new Point(20, 8)], positions);
        Assert.Equal([new Point(5, 8)], leafPositions);
        manager.Close();
    }

    [Fact]
    public void HandlerLayoutChangesKeepCurrentRouteAndWheelDelta()
    {
        var manager = new UiManager();
        var root = new Canvas();
        var leaf = new TestNode { Width = 20, Height = 20 };
        Canvas.SetLeft(leaf, 20);
        Canvas.SetTop(leaf, 0);
        root.Children.Add(leaf);
        var screen = new UiScreen(root) { Scale = 2 };
        var events = new List<string>();
        Point? movedPosition = null;
        Point? movedLeafPosition = null;
        leaf.PointerMoved += (_, eventArgs) =>
        {
            events.Add("leaf-move");
            movedPosition = eventArgs.ScreenPosition;
            movedLeafPosition = eventArgs.GetPosition(leaf);
            screen.Scale = 4;
            screen.UseLayoutRounding = false;
        };
        root.PointerMoved += (_, _) => events.Add("root-move");
        double? wheelX = null;
        double? wheelY = null;
        leaf.PointerWheel += (_, eventArgs) =>
        {
            events.Add("leaf-wheel");
            wheelX = eventArgs.DeltaX;
            wheelY = eventArgs.DeltaY;
            screen.Scale = 5;
            screen.UseLayoutRounding = true;
        };
        root.PointerWheel += (_, _) => events.Add("root-wheel");
        manager.Open(screen);
        manager.Update(new Size(100, 100));

        manager.ProcessPointerMoved(new Point(44, 8));

        Assert.Equal(["leaf-move", "root-move"], events);
        Assert.Equal(new Point(22, 4), movedPosition);
        Assert.Equal(new Point(2, 4), movedLeafPosition);
        Assert.Equal(4, screen.Scale);
        Assert.False(screen.UseLayoutRounding);
        Assert.False(leaf.IsArrangeValid);

        events.Clear();
        manager.Update(new Size(100, 100));
        manager.ProcessPointerWheel(new Point(84, 8), 2.5, -1.5);

        Assert.Equal(["leaf-wheel", "root-wheel"], events);
        Assert.Equal(2.5, wheelX);
        Assert.Equal(-1.5, wheelY);
        Assert.Equal(5, screen.Scale);
        Assert.True(screen.UseLayoutRounding);
        Assert.False(leaf.IsArrangeValid);
        manager.Close();
    }

    [Fact]
    public void ScaleChangeReprojectsStoredOutputPointerWhenHoverRefreshes()
    {
        var manager = new UiManager();
        var root = new Canvas();
        var leaf = new TestNode { Width = 20, Height = 20 };
        Canvas.SetLeft(leaf, 30);
        Canvas.SetTop(leaf, 0);
        root.Children.Add(leaf);
        var screen = new UiScreen(root);
        var events = new List<string>();
        Point? enteredPosition = null;
        Point? exitedPosition = null;
        Point? movedPosition = null;
        leaf.PointerEntered += (_, eventArgs) =>
        {
            events.Add("enter");
            enteredPosition = eventArgs.ScreenPosition;
        };
        leaf.PointerExited += (_, eventArgs) =>
        {
            events.Add("exit");
            exitedPosition = eventArgs.ScreenPosition;
        };
        leaf.PointerMoved += (_, eventArgs) => movedPosition = eventArgs.ScreenPosition;
        manager.Open(screen);
        manager.Update(new Size(100, 100));
        manager.ProcessPointerMoved(new Point(15, 5));

        screen.Scale = 0.5;
        manager.ProcessPointerMoved(new Point(15, 5));
        Assert.Empty(events);

        manager.Update(new Size(100, 100));

        Assert.Equal(["enter"], events);
        Assert.Equal(new Point(30, 10), enteredPosition);

        manager.ProcessPointerMoved(new Point(16, 6));
        Assert.Equal(new Point(32, 12), movedPosition);

        screen.Scale = 2;
        manager.Update(new Size(100, 100));

        Assert.Equal(["enter", "exit"], events);
        Assert.Equal(new Point(8, 3), exitedPosition);
        manager.Close();
    }

    [Fact]
    public void ScreenCloseAndReuseStartsWithEmptyInteractionState()
    {
        var manager = new UiManager();
        var root = new Canvas();
        var leaf = Place(root, new TestNode { Focusable = true }, 0, 0, 20, 20);
        var screen = new UiScreen(root);
        var events = new List<string>();
        leaf.PointerEntered += (_, _) => events.Add("enter");
        leaf.PointerReleased += (_, _) => events.Add("release");
        manager.Open(screen);
        manager.Update(new Size(100, 100));
        manager.ProcessPointerMoved(new Point(5, 5));
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        events.Clear();

        manager.Close();
        manager.Open(screen);
        manager.Update(new Size(100, 100));
        manager.ProcessPointerReleased(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        manager.ProcessPointerMoved(new Point(5, 5));

        Assert.Null(screen.FocusedNode);
        Assert.Equal(["enter"], events);
    }

    [Fact]
    public void OpenedFailureRollsBackInputStateAndReleasesScreen()
    {
        var manager = new UiManager();
        var root = new TestNode { Focusable = true };
        var expected = new InvalidOperationException("opened");
        var screen = new RecordingUiScreen(root)
        {
            Opened = () =>
            {
                root.Measure(new Size(20, 20));
                root.Arrange(new Rect(0, 0, 20, 20));
                Assert.True(root.Focus());
                throw expected;
            }
        };

        var actual = Assert.Throws<InvalidOperationException>(() => manager.Open(screen));

        Assert.Same(expected, actual);
        Assert.Null(manager.CurrentScreen);
        Assert.Same(screen, root.Screen);
        Assert.Null(screen.FocusedNode);
        screen.Opened = null;
        var otherManager = new UiManager();
        otherManager.Open(screen);
        Assert.Same(screen, otherManager.CurrentScreen);
    }

    [Fact]
    public void ReplacementCleanupFailureStopsCandidateOpeningAndReleasesCandidate()
    {
        var manager = new UiManager();
        var oldRoot = new TestNode { Focusable = true };
        var oldScreen = new UiScreen(oldRoot);
        var expected = new InvalidOperationException("lost");
        oldRoot.LostFocus += (_, _) => throw expected;
        manager.Open(oldScreen);
        oldRoot.Measure(new Size(20, 20));
        oldRoot.Arrange(new Rect(0, 0, 20, 20));
        Assert.True(oldRoot.Focus());
        var candidate = new UiScreen(new TestNode());

        var actual = Assert.Throws<InvalidOperationException>(() => manager.Open(candidate));

        Assert.Same(expected, actual);
        Assert.Null(manager.CurrentScreen);
        var otherManager = new UiManager();
        otherManager.Open(candidate);
        Assert.Same(candidate, otherManager.CurrentScreen);
    }

    [Fact]
    public void DestroyCompletesAfterCleanupFailure()
    {
        var manager = new UiManager();
        var root = new TestNode { Focusable = true };
        var screen = new UiScreen(root);
        var expected = new InvalidOperationException("lost");
        root.LostFocus += (_, _) => throw expected;
        manager.Open(screen);
        root.Measure(new Size(20, 20));
        root.Arrange(new Rect(0, 0, 20, 20));
        Assert.True(root.Focus());

        var actual = Assert.Throws<InvalidOperationException>(manager.Destroy);

        Assert.Same(expected, actual);
        Assert.Null(manager.CurrentScreen);
        Assert.Same(screen, root.Screen);
        Assert.Throws<ObjectDisposedException>(() => manager.Update(new Size(20, 20)));
    }

    [Fact]
    public void OpeningCannotFocusAndOpenedCanFocus()
    {
        var manager = new UiManager();
        var root = new TestNode { Focusable = true };
        var screen = new RecordingUiScreen(root);
        bool? openingResult = null;
        bool? openedResult = null;
        screen.Opening = () => openingResult = root.Focus();
        screen.Opened = () =>
        {
            root.Measure(new Size(20, 20));
            root.Arrange(new Rect(0, 0, 20, 20));
            openedResult = root.Focus();
        };

        manager.Open(screen);

        Assert.Equal(false, openingResult);
        Assert.Equal(true, openedResult);
        Assert.Same(root, screen.FocusedNode);
    }

    [Fact]
    public void CloseCompletesAfterCommittedCleanupFailureAndReleasesScreen()
    {
        var manager = new UiManager();
        var root = new Canvas();
        var leaf = Place(root, new TestNode { Focusable = true }, 0, 0, 20, 20);
        var closedCalls = 0;
        var screen = new RecordingUiScreen(root)
        {
            Closed = () => closedCalls++
        };
        var expected = new InvalidOperationException("lost");
        leaf.LostFocus += (_, _) => throw expected;
        manager.Open(screen);
        manager.Update(new Size(100, 100));
        Assert.True(leaf.Focus());

        var actual = Assert.Throws<InvalidOperationException>(manager.Close);

        Assert.Same(expected, actual);
        Assert.Equal(1, closedCalls);
        Assert.Null(manager.CurrentScreen);
        Assert.Same(screen, root.Screen);
        var otherManager = new UiManager();
        otherManager.Open(screen);
        Assert.Same(screen, otherManager.CurrentScreen);
    }

    [Fact]
    public void InputDuringCommittedCloseCleanupUsesHostAndScreenTransitionPolicies()
    {
        var manager = new UiManager();
        var root = new Canvas();
        var leaf = Place(root, new TestNode { Focusable = true }, 0, 0, 20, 20);
        var screen = new UiScreen(root);
        Exception? managerError = null;
        Exception? screenError = null;
        var routedMoves = 0;
        root.PointerMoved += (_, _) => routedMoves++;
        leaf.LostFocus += (_, _) =>
        {
            managerError = Record.Exception(() => manager.ProcessPointerMoved(Point.Zero));
            screenError = Record.Exception(() => screen.ProcessPointerMoved(Point.Zero));
        };
        manager.Open(screen);
        manager.Update(new Size(100, 100));
        Assert.True(leaf.Focus());

        manager.Close();

        Assert.Null(managerError);
        Assert.IsType<InvalidOperationException>(screenError);
        Assert.Equal(0, routedMoves);
        Assert.Null(manager.CurrentScreen);
        Assert.Same(screen, root.Screen);
    }

    [Fact]
    public void DetachCallbackCannotReopenSameScreenBeforeCleanupReturns()
    {
        var (manager, screen, root) = OpenScene();
        var branch = Place(root, new Canvas(), 10, 10, 30, 30);
        var leaf = Place(branch, new TestNode { Focusable = true }, 0, 0, 20, 20);
        var notifications = new List<string>();
        Exception? reopenError = null;
        leaf.LostFocus += (_, _) =>
        {
            notifications.Add("lost");
            manager.Close();
            reopenError = Record.Exception(() => manager.Open(screen));
        };
        leaf.PointerExited += (_, _) => notifications.Add("leaf-exit");
        branch.PointerExited += (_, _) => notifications.Add("branch-exit");
        manager.ProcessPointerMoved(new Point(15, 15));
        Assert.True(leaf.Focus());

        Assert.True(root.Children.Remove(branch));

        Assert.IsType<InvalidOperationException>(reopenError);
        Assert.Equal(["lost", "leaf-exit", "branch-exit"], notifications);
        Assert.Null(manager.CurrentScreen);
        manager.Open(screen);
        manager.Close();
    }

    [Fact]
    public void UiScreenInputRoutingWorksWithoutUiManager()
    {
        var root = new Canvas();
        var leaf = Place(root, new TestNode { Focusable = true }, 0, 0, 20, 20);
        var screen = new UiScreen(root);
        var events = new List<string>();
        leaf.PointerMoved += (_, _) => events.Add("move");
        leaf.PointerPressed += (_, _) => events.Add("press");
        leaf.PointerReleased += (_, _) => events.Add("release");
        leaf.PointerClicked += (_, _) => events.Add("click");
        leaf.KeyDown += (_, _) => events.Add("key-down");
        leaf.LostFocus += (_, _) => events.Add("lost-focus");
        leaf.PointerExited += (_, _) => events.Add("exit");

        screen.Open();
        screen.Update(new Size(100, 100));
        screen.ProcessPointerMoved(new Point(5, 5));
        screen.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        screen.ProcessPointerReleased(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        screen.ProcessKeyDown(Key.A, KeyModifiers.None);
        Assert.True(root.Children.Remove(leaf));
        screen.Close();

        Assert.Equal(
            ["move", "press", "release", "click", "key-down", "lost-focus", "exit"],
            events);
        Assert.Null(screen.FocusedNode);
        Assert.Null(leaf.Screen);
        Assert.Same(screen, screen.Root!.Screen);
    }

    [Fact]
    public void NestedPhysicalInputRoutingIsRejectedWithoutReplacingOuterRoute()
    {
        var (manager, _, root) = OpenScene();
        var leaf = Place(root, new TestNode(), 0, 0, 20, 20);
        Exception? nestedError = null;
        var rootMoves = 0;
        leaf.PointerMoved += (_, _) =>
            nestedError = Record.Exception(() => manager.ProcessPointerMoved(new Point(6, 6)));
        root.PointerMoved += (_, _) => rootMoves++;

        manager.ProcessPointerMoved(new Point(5, 5));

        Assert.IsType<InvalidOperationException>(nestedError);
        Assert.Equal(1, rootMoves);
    }

    [Fact]
    public void PointerCallbackCannotReopenSameScreenBeforeRoutingReturns()
    {
        var manager = new UiManager();
        var root = new Canvas();
        var leaf = Place(root, new TestNode(), 0, 0, 20, 20);
        var screen = new UiScreen(root);
        var events = new List<string>();
        Exception? reopenError = null;
        leaf.PointerMoved += (_, _) =>
        {
            events.Add("leaf");
            manager.Close();
            reopenError = Record.Exception(() => manager.Open(screen));
        };
        root.PointerMoved += (_, _) => events.Add("root");
        manager.Open(screen);
        manager.Update(new Size(100, 100));

        manager.ProcessPointerMoved(new Point(5, 5));

        Assert.Equal(["leaf"], events);
        Assert.IsType<InvalidOperationException>(reopenError);
        Assert.Null(manager.CurrentScreen);
        manager.Open(screen);
        manager.Close();
    }

    [Fact]
    public void FocusCallbackCannotReopenSameScreenBeforeTransitionReturns()
    {
        var manager = new UiManager();
        var root = new Canvas();
        var first = Place(root, new TestNode { Focusable = true }, 0, 0, 20, 20);
        var screen = new UiScreen(root);
        Exception? reopenError = null;
        first.LostFocus += (_, _) =>
        {
            manager.Close();
            reopenError = Record.Exception(() => manager.Open(screen));
        };
        manager.Open(screen);
        manager.Update(new Size(100, 100));
        Assert.True(first.Focus());

        screen.ClearFocus();

        Assert.IsType<InvalidOperationException>(reopenError);
        Assert.Null(manager.CurrentScreen);
        manager.Open(screen);
        manager.Close();
    }

    [Fact]
    public void CloseNotifiesInteractionLossWithoutClearingScreenOwnership()
    {
        var manager = new UiManager();
        var root = new Canvas();
        var branch = Place(root, new Canvas(), 0, 0, 30, 30);
        var leaf = Place(branch, new TestNode { Focusable = true }, 0, 0, 20, 20);
        var screen = new UiScreen(root);
        var events = new List<string>();
        leaf.LostFocus += (_, _) =>
        {
            Assert.Same(screen, leaf.Screen);
            events.Add("lost");
        };
        leaf.PointerExited += (_, _) =>
        {
            Assert.Same(screen, leaf.Screen);
            events.Add("leaf-exit");
        };
        branch.PointerExited += (_, _) =>
        {
            Assert.Same(screen, branch.Screen);
            events.Add("branch-exit");
        };
        manager.Open(screen);
        manager.Update(new Size(100, 100));
        manager.ProcessPointerMoved(new Point(5, 5));
        Assert.True(leaf.Focus());

        manager.Close();

        Assert.Equal(["lost", "leaf-exit", "branch-exit"], events);
        Assert.Same(screen, root.Screen);
        Assert.Same(screen, branch.Screen);
        Assert.Same(screen, leaf.Screen);
    }

    [Fact]
    public void CloseNotificationCanReplaceRootAndStillCompletesSnapshot()
    {
        var manager = new UiManager();
        var oldRoot = new Canvas();
        var branch = Place(oldRoot, new Canvas(), 0, 0, 30, 30);
        var leaf = Place(branch, new TestNode { Focusable = true }, 0, 0, 20, 20);
        var newRoot = new Canvas();
        var screen = new UiScreen(oldRoot);
        var events = new List<string>();
        leaf.LostFocus += (_, _) =>
        {
            events.Add("lost");
            screen.Root = newRoot;
        };
        leaf.PointerExited += (_, _) => events.Add("leaf-exit");
        branch.PointerExited += (_, _) => events.Add("branch-exit");
        manager.Open(screen);
        manager.Update(new Size(100, 100));
        manager.ProcessPointerMoved(new Point(5, 5));
        Assert.True(leaf.Focus());

        manager.Close();

        Assert.Equal(["lost", "leaf-exit", "branch-exit"], events);
        Assert.Null(oldRoot.Screen);
        Assert.Null(branch.Screen);
        Assert.Null(leaf.Screen);
        Assert.Same(newRoot, screen.Root);
        Assert.Same(screen, newRoot.Screen);
    }

    [Fact]
    public void ReplacingRootSynchronouslyCleansOutgoingInteraction()
    {
        var manager = new UiManager();
        var oldRoot = new Canvas();
        var branch = Place(oldRoot, new Canvas(), 0, 0, 30, 30);
        var leaf = Place(branch, new TestNode { Focusable = true }, 0, 0, 20, 20);
        var newRoot = new Canvas();
        var screen = new UiScreen(oldRoot);
        var events = new List<string>();
        leaf.LostFocus += (_, _) => events.Add("lost");
        leaf.PointerExited += (_, _) => events.Add("leaf-exit");
        branch.PointerExited += (_, _) => events.Add("branch-exit");
        manager.Open(screen);
        manager.Update(new Size(100, 100));
        manager.ProcessPointerMoved(new Point(5, 5));
        Assert.True(leaf.Focus());

        screen.Root = newRoot;

        Assert.Equal(["lost", "leaf-exit", "branch-exit"], events);
        Assert.Null(oldRoot.Screen);
        Assert.Null(branch.Screen);
        Assert.Null(leaf.Screen);
        Assert.Same(screen, newRoot.Screen);
        Assert.Same(newRoot, screen.Root);
        manager.Close();
    }

    [Fact]
    public void PromotingDescendantRootPreservesItsInteractionState()
    {
        var manager = new UiManager();
        var oldRoot = new Canvas();
        var incoming = Place(oldRoot, new Canvas(), 0, 0, 30, 30);
        // Explicit alignment keeps the fixed-size node at the origin after promotion;
        // the default Stretch alignment would center it at (35,35).
        incoming.HorizontalAlignment = HorizontalAlignment.Left;
        incoming.VerticalAlignment = VerticalAlignment.Top;
        var leaf = Place(incoming, new TestNode { Focusable = true }, 0, 0, 20, 20);
        var screen = new UiScreen(oldRoot);
        var events = new List<string>();
        incoming.PointerExited += (_, _) => events.Add("incoming-exit");
        leaf.PointerExited += (_, _) => events.Add("leaf-exit");
        leaf.PointerClicked += (_, _) => events.Add("click");
        manager.Open(screen);
        manager.Update(new Size(100, 100));
        manager.ProcessPointerMoved(new Point(5, 5));
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        Assert.True(leaf.Focus());

        screen.Root = incoming;

        Assert.Empty(events);
        Assert.Same(leaf, screen.FocusedNode);
        Assert.Null(oldRoot.Screen);
        Assert.Same(screen, incoming.Screen);
        Assert.Same(screen, leaf.Screen);

        manager.Update(new Size(100, 100));
        manager.ProcessPointerReleased(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        Assert.Equal(["click"], events);
        manager.Close();
    }

    [Fact]
    public void RootTransferNotificationFailureKeepsCommittedStructure()
    {
        var incoming = new TestNode { Focusable = true };
        var source = new UiScreen(incoming);
        var target = new UiScreen(new Canvas());
        var expected = new InvalidOperationException("lost");
        incoming.LostFocus += (_, _) => throw expected;
        source.Open();
        incoming.Measure(new Size(20, 20));
        incoming.Arrange(new Rect(0, 0, 20, 20));
        Assert.True(incoming.Focus());

        var actual = Assert.Throws<InvalidOperationException>(() => target.Root = incoming);

        Assert.Same(expected, actual);
        Assert.Null(source.Root);
        Assert.Same(incoming, target.Root);
        Assert.Same(target, incoming.Screen);
        Assert.Null(source.FocusedNode);
        source.Close();
    }

    [Fact]
    public void RootTransferCommitsSourceFocusBeforeTargetNotification()
    {
        var targetRoot = new Canvas();
        var targetLeaf = Place(targetRoot, new TestNode { Focusable = true }, 0, 0, 20, 20);
        var sourceRoot = new Canvas();
        var sourceLeaf = Place(sourceRoot, new TestNode { Focusable = true }, 0, 0, 20, 20);
        var target = new UiScreen(targetRoot);
        var source = new UiScreen(sourceRoot);
        target.Open();
        source.Open();
        target.Update(new Size(100, 100));
        source.Update(new Size(100, 100));
        Assert.True(targetLeaf.Focus());
        Assert.True(sourceLeaf.Focus());
        UiNode? sourceFocusDuringTargetNotification = source.FocusedNode;
        targetLeaf.LostFocus += (_, _) => sourceFocusDuringTargetNotification = source.FocusedNode;

        target.Root = sourceRoot;

        Assert.Null(sourceFocusDuringTargetNotification);
        Assert.Null(source.FocusedNode);
        Assert.Same(target, sourceLeaf.Screen);
        target.Close();
        source.Close();
    }

    [Fact]
    public void RootTransferAggregatesNotificationsFromTargetAndSourceScreens()
    {
        var targetRoot = new Canvas();
        var targetLeaf = Place(targetRoot, new TestNode { Focusable = true }, 0, 0, 20, 20);
        var sourceRoot = new Canvas();
        var sourceLeaf = Place(sourceRoot, new TestNode { Focusable = true }, 0, 0, 20, 20);
        var target = new UiScreen(targetRoot);
        var source = new UiScreen(sourceRoot);
        var targetError = new InvalidOperationException("target lost focus");
        var sourceError = new InvalidOperationException("source lost focus");
        targetLeaf.LostFocus += (_, _) => throw targetError;
        sourceLeaf.LostFocus += (_, _) => throw sourceError;
        target.Open();
        source.Open();
        target.Update(new Size(100, 100));
        source.Update(new Size(100, 100));
        Assert.True(targetLeaf.Focus());
        Assert.True(sourceLeaf.Focus());

        var actual = Assert.Throws<AggregateException>(() => target.Root = sourceRoot);

        Assert.Equal([targetError, sourceError], actual.InnerExceptions);
        Assert.Null(source.Root);
        Assert.Same(sourceRoot, target.Root);
        Assert.Null(targetLeaf.Screen);
        Assert.Same(target, sourceLeaf.Screen);
        Assert.Null(source.FocusedNode);
        target.Close();
        source.Close();

        var targetReopenError = Record.Exception(() =>
        {
            target.Open();
            target.Close();
        });
        var sourceReopenError = Record.Exception(() =>
        {
            source.Open();
            source.Close();
        });
        Assert.Null(targetReopenError);
        Assert.Null(sourceReopenError);
    }

    [Fact]
    public void NullRootInputProducesNoNodeEvents()
    {
        var manager = new UiManager();
        var screen = new UiScreen();
        manager.Open(screen);

        manager.ProcessPointerMoved(Point.Zero);
        manager.ProcessPointerPressed(Point.Zero, MouseButton.Left, KeyModifiers.None);
        manager.ProcessPointerReleased(Point.Zero, MouseButton.Left, KeyModifiers.None);
        manager.ProcessPointerWheel(Point.Zero, 1, -1);
        manager.ProcessKeyDown(Key.A, KeyModifiers.None);
        manager.ProcessKeyUp(Key.A, KeyModifiers.None);

        Assert.Null(screen.FocusedNode);
        manager.Close();
    }

    private static (UiManager Manager, UiScreen Screen, Canvas Root) OpenScene()
    {
        var manager = new UiManager();
        var root = new Canvas();
        var screen = new UiScreen(root);
        manager.Open(screen);
        manager.Update(new Size(100, 100));
        return (manager, screen, root);
    }

    private static T Place<T>(Canvas parent, T child, double x, double y, double width, double height)
        where T : UiNode
    {
        child.Width = width;
        child.Height = height;
        Canvas.SetLeft(child, x);
        Canvas.SetTop(child, y);
        parent.Children.Add(child);
        if (parent.Screen?.IsOpen() == true)
        {
            var root = parent;
            while (root.Parent is Canvas ancestor)
                root = ancestor;
            root.Measure(new Size(100, 100));
            root.Arrange(new Rect(0, 0, 100, 100));
        }

        return child;
    }

    private static T RunOnBackgroundThread<T>(Func<T> action)
    {
        T result = default!;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = action();
            }
            catch (Exception exception)
            {
                error = exception;
            }
        });
        thread.Start();
        thread.Join();

        if (error is not null)
            ExceptionDispatchInfo.Capture(error).Throw();

        return result;
    }

    private sealed class TestNode : UiNode
    {
    }

    private sealed class TestControl : Control
    {
    }

    private sealed class LayoutActionNode : UiNode
    {
        internal Action? MeasureAction { get; set; }
        internal Action? ArrangeAction { get; set; }

        protected override Size MeasureCore(Size availableSize)
        {
            MeasureAction?.Invoke();
            return new Size(10, 10);
        }

        protected override void ArrangeCore(Size finalSize) =>
            ArrangeAction?.Invoke();
    }

    private sealed class RecordingUiScreen(UiNode? root = null) : UiScreen(root)
    {
        internal Action? Opening { get; set; }
        internal Action? Opened { get; set; }
        internal Action? Closing { get; set; }
        internal Action? Closed { get; set; }

        protected override void OnOpening() => Opening?.Invoke();
        protected override void OnOpened() => Opened?.Invoke();
        protected override void OnClosing() => Closing?.Invoke();
        protected override void OnClosed() => Closed?.Invoke();
    }
}
