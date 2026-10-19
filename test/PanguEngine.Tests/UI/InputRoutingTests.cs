using System.Runtime.ExceptionServices;
using PanguEngine.Client.UI;
using PanguEngine.Input;
using PanguEngine.UI;
using PanguEngine.UI.Controls;
using PanguEngine.UI.Input;

namespace PanguEngine.Tests.UI;

public sealed class InputRoutingTests
{
    [Fact]
    public void InputEventArgsExposeSourceHandledAndRelativePositions()
    {
        var (manager, _, root) = OpenScene();
        var leaf = Place(root, new TestNode(), 10, 20, 30, 40);
        PointerEventArgs? childArgs = null;
        PointerEventArgs? rootArgs = null;
        leaf.PointerMoved += (_, eventArgs) => childArgs = eventArgs;
        root.PointerMoved += (_, eventArgs) => rootArgs = eventArgs;

        manager.ProcessPointerMoved(new Point(15, 27));

        Assert.NotNull(childArgs);
        Assert.Same(childArgs, rootArgs);
        Assert.Same(leaf, childArgs.Source);
        Assert.False(childArgs.Handled);
        Assert.Equal(new Point(15, 27), childArgs.ScenePosition);
        Assert.Equal(new Point(5, 7), childArgs.GetPosition(leaf));
        Assert.Equal(new Point(15, 27), childArgs.GetPosition(root));
        Assert.Throws<ArgumentNullException>(() => childArgs.GetPosition(null!));
        Assert.Throws<ArgumentException>(() => childArgs.GetPosition(new TestNode()));
    }

    [Fact]
    public void GetPositionForOffPathNodeUsesEventScenePointAndCurrentTree()
    {
        var (manager, _, root) = OpenScene();
        var leaf = Place(root, new TestNode(), 0, 0, 20, 20);
        var sibling = Place(root, new TestNode(), 30, 0, 20, 20);
        Point? actual = null;
        PointerEventArgs? captured = null;
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
        var scene = new GameUIScene(root);
        scene.Open();
        root.Measure(new Size(1, 1));
        root.Arrange(new Rect(-double.MaxValue, 0, 1, 1));
        sibling.Measure(new Size(1, 1));
        sibling.Arrange(new Rect(0, 0, 1, 1));
        var eventArgs = new PointerEventArgs(
            root,
            new Point(double.MaxValue, 0),
            [new HitPathEntry(root, Point.Zero)]);

        Assert.Throws<ArgumentOutOfRangeException>(() => eventArgs.GetPosition(sibling));
        scene.Close();
    }

    [Fact]
    public void ManagerInputWithoutSceneIsNoOpOnOwnerThreadAndRejectsWrongThread()
    {
        var manager = new UIManager();

        manager.ProcessPointerMoved(Point.Zero);
        manager.ProcessPointerPressed(Point.Zero, MouseButton.Unknown, KeyModifiers.None);
        manager.ProcessPointerReleased(Point.Zero, (MouseButton)13, KeyModifiers.None);
        manager.ProcessPointerWheel(Point.Zero, double.NaN, double.PositiveInfinity);
        manager.ProcessKeyDown(Key.A, KeyModifiers.Shift);
        manager.ProcessKeyUp(Key.A, KeyModifiers.Shift);
        manager.ProcessTextInput("text");

        var threadError = RunOnBackgroundThread(() =>
            Record.Exception(() =>
            {
                manager.ProcessPointerMoved(Point.Zero);
                manager.ProcessPointerPressed(Point.Zero, MouseButton.Unknown, KeyModifiers.None);
                manager.ProcessPointerReleased(Point.Zero, (MouseButton)13, KeyModifiers.None);
                manager.ProcessPointerWheel(Point.Zero, double.NaN, double.PositiveInfinity);
                manager.ProcessKeyDown(Key.A, KeyModifiers.None);
                manager.ProcessKeyUp(Key.A, KeyModifiers.None);
                manager.ProcessTextInput("text");
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
    public void LeftPressedTargetReceivesMovedWhileHoverTracksActualLeaf()
    {
        var (manager, _, root) = OpenScene();
        var first = Place(root, new TestNode(), 0, 0, 20, 20);
        var second = Place(root, new TestNode(), 30, 0, 20, 20);
        var events = new List<string>();
        PointerEventArgs? moved = null;
        first.PointerMoved += (_, args) =>
        {
            moved = args;
            events.Add("first-move");
        };
        first.PointerExited += (_, _) => events.Add("first-exit");
        second.PointerEntered += (_, _) => events.Add("second-enter");
        second.PointerMoved += (_, _) => events.Add("second-move");

        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        events.Clear();
        manager.ProcessPointerMoved(new Point(35, 5));

        Assert.Equal(["first-exit", "second-enter", "first-move"], events);
        var movedArgs = Assert.IsType<PointerEventArgs>(moved);
        Assert.Same(first, movedArgs.Source);
        Assert.Equal(new Point(35, 5), movedArgs.ScenePosition);

        manager.ProcessPointerReleased(new Point(35, 5), MouseButton.Left, KeyModifiers.None);
        events.Clear();
        manager.ProcessPointerMoved(new Point(36, 5));

        Assert.Equal(["second-move"], events);
    }

    [Fact]
    public void RemovingPressedTargetReturnsMovedRoutingToHover()
    {
        var (manager, _, root) = OpenScene();
        var first = Place(root, new TestNode(), 0, 0, 20, 20);
        var second = Place(root, new TestNode(), 30, 0, 20, 20);
        var firstMoves = 0;
        var secondMoves = 0;
        first.PointerMoved += (_, _) => firstMoves++;
        second.PointerMoved += (_, _) => secondMoves++;
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);

        Assert.True(root.Children.Remove(first));
        manager.UpdateFrame(new Size(100, 100), 0);
        manager.ProcessPointerMoved(new Point(35, 5));

        Assert.Equal(0, firstMoves);
        Assert.Equal(1, secondMoves);
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
        var (manager, scene, root) = OpenScene();
        var leaf = Place(root, new TestNode { Focusable = true }, 0, 0, 20, 20);
        var wheels = new List<(UIElement Source, double X, double Y)>();
        leaf.PointerWheel += (_, eventArgs) =>
            wheels.Add((eventArgs.Source, eventArgs.DeltaX, eventArgs.DeltaY));
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);

        manager.ProcessPointerWheel(new Point(5, 5), 2.5, -3.5);
        manager.ProcessPointerReleased(new Point(5, 5), MouseButton.Left, KeyModifiers.None);

        Assert.Same(leaf, scene.FocusedNode);
        Assert.Equal([(leaf, 2.5, -3.5)], wheels);
    }

    [Fact]
    public void PressFocusesNearestEligibleAncestorButRoutesToDeepestLeaf()
    {
        var (manager, scene, root) = OpenScene();
        var container = Place(root, new Canvas { Focusable = true }, 10, 10, 40, 40);
        var leaf = Place(container, new TestNode(), 5, 5, 10, 10);
        var events = new List<string>();
        leaf.PointerPressed += (_, _) => events.Add("leaf");
        container.PointerPressed += (_, _) => events.Add("container");
        root.PointerPressed += (_, _) => events.Add("root");

        manager.ProcessPointerPressed(new Point(16, 16), MouseButton.Left, KeyModifiers.None);

        Assert.Same(container, scene.FocusedNode);
        Assert.Equal(["leaf", "container", "root"], events);
    }

    [Fact]
    public void PressWithoutEligibleCandidateClearsFocusBeforePressed()
    {
        var (manager, scene, root) = OpenScene();
        var focused = Place(root, new TestNode { Focusable = true }, 0, 0, 20, 20);
        var target = Place(root, new TestNode(), 30, 0, 20, 20);
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        manager.ProcessPointerReleased(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        UIElement? focusDuringPressed = focused;
        target.PointerPressed += (_, _) => focusDuringPressed = scene.FocusedNode;

        manager.ProcessPointerPressed(new Point(35, 5), MouseButton.Left, KeyModifiers.None);

        Assert.Null(focusDuringPressed);
        Assert.Null(scene.FocusedNode);
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
        EventHandler<PointerButtonEventArgs> throwingHandler = (_, _) => throw expected;
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
    public void SceneRejectsInvalidMouseButtonsBeforeRouting(MouseButton button)
    {
        var (_, scene, root) = OpenScene();
        var leaf = Place(root, new TestNode(), 0, 0, 20, 20);
        var events = 0;
        leaf.PointerEntered += (_, _) => events++;
        leaf.PointerPressed += (_, _) => events++;
        leaf.PointerReleased += (_, _) => events++;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            scene.ProcessPointerPressed(new Point(5, 5), button, KeyModifiers.None));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            scene.ProcessPointerReleased(new Point(5, 5), button, KeyModifiers.None));
        Assert.Equal(0, events);
    }

    [Fact]
    public void SceneRejectsNonFiniteWheelDeltasBeforeRouting()
    {
        var (_, scene, root) = OpenScene();
        var leaf = Place(root, new TestNode(), 0, 0, 20, 20);
        var events = 0;
        leaf.PointerEntered += (_, _) => events++;
        leaf.PointerWheel += (_, _) => events++;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            scene.ProcessPointerWheel(new Point(5, 5), double.NaN, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            scene.ProcessPointerWheel(new Point(5, 5), 0, double.PositiveInfinity));
        Assert.Equal(0, events);
    }

    [Fact]
    public void SceneInputRejectsWrongOwnerThread()
    {
        var (_, scene, _) = OpenScene();

        var errors = RunOnBackgroundThread(() =>
            (Pointer: Record.Exception(() => scene.ProcessPointerMoved(Point.Zero)),
                Key: Record.Exception(() => scene.ProcessKeyDown(Key.A, KeyModifiers.None))));

        Assert.IsType<InvalidOperationException>(errors.Pointer);
        Assert.IsType<InvalidOperationException>(errors.Key);
    }

    [Fact]
    public void FocusLossClearsFocusHoverPressedAndPointerState()
    {
        var (manager, scene, root) = OpenScene();
        var control = Place(root, new TestControl { Focusable = true }, 0, 0, 20, 20);
        var events = new List<string>();
        control.LostFocus += (_, _) => events.Add("lost");
        control.PointerExited += (_, _) => events.Add("exit");
        control.PointerCanceled += (_, args) =>
        {
            Assert.False(control.IsFocused);
            Assert.False(control.IsHovered);
            Assert.False(control.IsPressed);
            Assert.Null(scene.FocusedNode);
            Assert.Same(control, args.Source);
            Assert.Equal(MouseButton.Left, args.Button);
            Assert.Equal(new Point(5, 5), args.ScenePosition);
            events.Add("cancel");
        };
        control.PointerReleased += (_, _) => events.Add("release");
        control.PointerClicked += (_, _) => events.Add("click");
        control.KeyDown += (_, args) => events.Add($"down-{args.Key}");
        control.KeyUp += (_, _) => events.Add("up");
        root.KeyUp += (_, _) => events.Add("root-up");
        manager.ProcessPointerMoved(new Point(5, 5));
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        manager.ProcessKeyDown(Key.Space, KeyModifiers.None);
        Assert.True(control.IsFocused);
        Assert.True(control.IsHovered);
        Assert.True(control.IsPressed);

        manager.ProcessFocusChanged(false);
        manager.ProcessFocusChanged(true);
        manager.UpdateFrame(new Size(100, 100), 0);

        Assert.False(control.IsFocused);
        Assert.False(control.IsHovered);
        Assert.False(control.IsPressed);
        Assert.Null(scene.FocusedNode);
        manager.ProcessFocusChanged(false);
        manager.ProcessFocusChanged(true);
        manager.ProcessPointerReleased(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        Assert.Equal(["down-Space", "lost", "exit", "cancel"], events);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InputLossCancelsEachButtonAtItsOriginalTargetAndBubblesWithLastPosition(bool closeScene)
    {
        var (manager, _, root) = OpenScene();
        var first = Place(root, new TestNode(), 10, 10, 20, 20);
        var second = Place(root, new TestNode(), 40, 10, 20, 20);
        var events = new List<string>();
        first.PointerCanceled += (_, args) =>
        {
            Assert.Same(first, args.Source);
            Assert.Equal(new Point(70, 60), args.GetPosition(first));
            events.Add($"first-{args.Button}");
            args.Handled = args.Button == MouseButton.Left;
        };
        second.PointerCanceled += (_, args) =>
        {
            Assert.Same(second, args.Source);
            Assert.Equal(new Point(40, 60), args.GetPosition(second));
            events.Add($"second-{args.Button}");
        };
        root.PointerCanceled += (_, args) =>
        {
            Assert.Equal(new Point(80, 70), args.ScenePosition);
            Assert.Equal(KeyModifiers.None, args.Modifiers);
            events.Add($"root-{args.Button}");
        };
        manager.ProcessPointerPressed(new Point(15, 15), MouseButton.Left, KeyModifiers.Shift);
        manager.ProcessPointerPressed(new Point(45, 15), MouseButton.Right, KeyModifiers.Shift);
        manager.ProcessPointerPressed(new Point(15, 15), MouseButton.Button12, KeyModifiers.None);
        manager.ProcessPointerMoved(new Point(80, 70));

        if (closeScene)
            manager.Close();
        else
            manager.ProcessFocusChanged(false);

        Assert.Equal(
            ["first-Left", "second-Right", "root-Right", "first-Button12", "root-Button12"],
            events);
    }

    [Fact]
    public void PointerCanceledClosesImmediatelyWithoutRepeatingPendingButtonNotifications()
    {
        var (manager, scene, root) = OpenScene();
        var first = Place(root, new TestNode(), 0, 0, 20, 20);
        var second = Place(root, new TestNode(), 30, 0, 20, 20);
        var events = new List<string>();
        first.PointerCanceled += (_, _) =>
        {
            events.Add("first");
            manager.Close();
            Assert.False(scene.IsOpen());
        };
        second.PointerCanceled += (_, _) => events.Add("second");
        root.PointerCanceled += (_, _) => events.Add("root");
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        manager.ProcessPointerPressed(new Point(35, 5), MouseButton.Right, KeyModifiers.None);

        manager.ProcessFocusChanged(false);

        Assert.False(scene.IsOpen());
        Assert.Equal(["first", "root", "second", "root"], events);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void PointerCanceledSkipsLaterTargetMadeUnavailableByEarlierCallback(bool removeTarget, bool closeScene)
    {
        var (manager, _, root) = OpenScene();
        var first = Place(root, new TestNode(), 0, 0, 20, 20);
        var second = Place(root, new TestNode(), 30, 0, 20, 20);
        var events = new List<string>();
        first.PointerCanceled += (_, _) =>
        {
            events.Add("first");
            if (removeTarget)
                root.Children.Remove(second);
            else
                second.IsEnabled = false;
        };
        second.PointerCanceled += (_, _) => events.Add("second");
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        manager.ProcessPointerPressed(new Point(35, 5), MouseButton.Right, KeyModifiers.None);

        if (closeScene)
            manager.Close();
        else
            manager.ProcessFocusChanged(false);

        Assert.Equal(["first"], events);
        if (removeTarget)
            Assert.Null(second.Scene);
        else
            Assert.False(second.IsEnabled);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InputLossCancelsPendingButtonWithoutFocusHoverOrPressedControls(bool closeScene)
    {
        var (manager, scene, root) = OpenScene();
        var leaf = Place(root, new TestNode(), 0, 0, 20, 20);
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Right, KeyModifiers.None);
        manager.ProcessPointerMoved(new Point(150, 150));
        Assert.Null(scene.FocusedNode);
        Assert.False(root.IsHovered);
        Assert.False(leaf.IsHovered);
        var events = new List<string>();
        leaf.PointerCanceled += (_, args) =>
        {
            Assert.Equal(MouseButton.Right, args.Button);
            Assert.Equal(new Point(150, 150), args.ScenePosition);
            events.Add("cancel");
        };
        leaf.LostFocus += (_, _) => events.Add("lost");
        leaf.PointerExited += (_, _) => events.Add("exit");
        root.PointerExited += (_, _) => events.Add("root-exit");
        leaf.PointerReleased += (_, _) => events.Add("release");
        leaf.PointerClicked += (_, _) => events.Add("click");

        if (closeScene)
            manager.Close();
        else
            manager.ProcessFocusChanged(false);
        manager.ProcessFocusChanged(true);
        manager.ProcessPointerReleased(new Point(5, 5), MouseButton.Right, KeyModifiers.None);

        Assert.Equal(["cancel"], events);
    }

    [Fact]
    public void PointerCanceledExceptionPropagatesAndClearsAllPendingButtons()
    {
        var (manager, _, root) = OpenScene();
        var leaf = Place(root, new TestNode(), 0, 0, 20, 20);
        var expected = new InvalidOperationException("canceled");
        var events = new List<string>();
        EventHandler<PointerButtonEventArgs> throwingHandler = (_, args) =>
        {
            events.Add($"cancel-{args.Button}");
            throw expected;
        };
        leaf.PointerCanceled += throwingHandler;
        leaf.PointerReleased += (_, _) => events.Add("release");
        leaf.PointerClicked += (_, _) => events.Add("click");
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Right, KeyModifiers.None);

        var actual = Assert.Throws<InvalidOperationException>(() => manager.ProcessFocusChanged(false));
        leaf.PointerCanceled -= throwingHandler;
        manager.ProcessFocusChanged(true);
        manager.ProcessPointerReleased(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        manager.ProcessPointerReleased(new Point(5, 5), MouseButton.Right, KeyModifiers.None);

        Assert.Same(expected, actual);
        Assert.Equal(["cancel-Left"], events);

        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        manager.ProcessPointerReleased(new Point(5, 5), MouseButton.Left, KeyModifiers.None);

        Assert.Equal(["cancel-Left", "release", "click"], events);
    }

    [Fact]
    public void CloseCancelsAfterStateLossBeforeClosedAndRejectsReentrantInput()
    {
        var manager = new UIManager();
        var root = new Canvas();
        var leaf = Place(root, new TestControl { Focusable = true }, 0, 0, 20, 20);
        var events = new List<string>();
        var scene = new RecordingScene(root)
        {
            Closing = () =>
            {
                Assert.True(leaf.IsPressed);
                events.Add("closing");
            },
            Closed = () => events.Add("closed")
        };
        leaf.LostFocus += (_, _) => events.Add("lost");
        leaf.PointerExited += (_, _) => events.Add("exit");
        root.PointerExited += (_, _) => events.Add("root-exit");
        leaf.PointerCanceled += (_, args) =>
        {
            Assert.Null(scene.FocusedNode);
            Assert.False(leaf.IsFocused);
            Assert.False(leaf.IsHovered);
            Assert.False(leaf.IsPressed);
            Assert.Same(leaf, args.Source);
            Assert.Equal(new Point(5, 5), args.ScenePosition);
            Assert.Throws<InvalidOperationException>(() =>
                scene.ProcessPointerPressed(Point.Zero, MouseButton.Left, KeyModifiers.None));
            Assert.Throws<InvalidOperationException>(scene.Close);
            manager.Close();
            events.Add("cancel");
        };
        root.PointerCanceled += (_, _) => events.Add("root-cancel");
        leaf.PointerReleased += (_, _) => events.Add("release");
        leaf.PointerClicked += (_, _) => events.Add("click");
        manager.Open(scene);
        manager.UpdateFrame(new Size(100, 100), 0);
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);

        manager.Close();
        scene.Close();

        Assert.Equal(["closing", "lost", "exit", "root-exit", "cancel", "root-cancel", "closed"], events);
        Assert.False(scene.IsOpen());
        scene.Open();
        scene.PrepareFrame(new Size(100, 100));
        scene.ProcessPointerReleased(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        Assert.DoesNotContain("release", events);
        Assert.DoesNotContain("click", events);
        scene.Closing = null;
        scene.Close();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ManagerReplacementAndDestructionCancelPendingPress(bool destroy)
    {
        var (manager, scene, root) = OpenScene();
        var leaf = Place(root, new TestNode(), 0, 0, 20, 20);
        var events = new List<string>();
        leaf.PointerCanceled += (_, _) => events.Add("cancel");
        manager.CurrentSceneChanged += (_, _) => events.Add("changed");
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);

        if (destroy)
            manager.Destroy();
        else
            manager.Open(new GameUIScene());

        Assert.False(scene.IsOpen());
        Assert.Equal(["cancel", "changed"], events);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CloseStateLossFailureStopsCancellationAndClosed(bool failOnExit)
    {
        var root = new Canvas();
        var leaf = Place(root, new TestControl { Focusable = true }, 0, 0, 20, 20);
        var events = new List<string>();
        var scene = new RecordingScene(root) { Closed = () => events.Add("closed") };
        var expected = new InvalidOperationException("state loss failed");
        if (failOnExit)
            leaf.PointerExited += (_, _) => throw expected;
        else
            leaf.LostFocus += (_, _) => throw expected;
        leaf.PointerCanceled += (_, _) => events.Add("cancel");
        scene.Open();
        scene.PrepareFrame(new Size(100, 100));
        scene.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);

        Assert.Same(expected, Assert.Throws<InvalidOperationException>(scene.Close));

        Assert.Empty(events);
        Assert.Null(scene.FocusedNode);
        Assert.False(leaf.IsPressed);
        scene.Close();
        Assert.Equal(["closed"], events);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PressRouteContinuesAfterImmediateCloseAndCancellation(bool directClose)
    {
        var manager = new UIManager();
        var root = new Canvas();
        var leaf = Place(root, new TestNode(), 0, 0, 20, 20);
        var events = new List<string>();
        var scene = new RecordingScene(root)
        {
            Closing = () => events.Add("closing"),
            Closed = () => events.Add("closed")
        };
        leaf.PointerPressed += (_, _) =>
        {
            events.Add("leaf-press");
            if (directClose)
            {
                scene.Close();
                scene.Close();
            }
            else
                manager.Close();

            Assert.False(scene.IsOpen());
            Assert.Equal(["leaf-press", "closing", "leaf-cancel", "root-cancel", "closed"], events);
        };
        root.PointerPressed += (_, _) => events.Add("root-press");
        leaf.PointerCanceled += (_, _) => events.Add("leaf-cancel");
        root.PointerCanceled += (_, _) => events.Add("root-cancel");
        manager.Open(scene);
        manager.UpdateFrame(new Size(100, 100), 0);

        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);

        Assert.Equal(["leaf-press", "closing", "leaf-cancel", "root-cancel", "closed", "root-press"], events);
        Assert.False(scene.IsOpen());
    }

    [Theory]
    [InlineData("hover", false)]
    [InlineData("focus", true)]
    [InlineData("hover-state", false)]
    [InlineData("pressed-state", true)]
    public void InputPreparationCloseStopsBeforePressRouting(string closeDuring, bool expectCancellation)
    {
        var (manager, scene, root) = OpenScene();
        var leaf = Place(root, new TestControl { Focusable = true }, 0, 0, 20, 20);
        var events = new List<string>();

        void RequestClose()
        {
            events.Add("request");
            manager.Close();
        }

        switch (closeDuring)
        {
            case "hover":
                leaf.PointerEntered += (_, _) => RequestClose();
                break;
            case "focus":
                leaf.GotFocus += (_, _) => RequestClose();
                break;
            case "hover-state":
                _ = leaf.Subscribe(UIElement.IsHoveredProperty, (_, args) =>
                {
                    if (args.NewValue)
                        RequestClose();
                });
                break;
            case "pressed-state":
                _ = leaf.Subscribe(Control.IsPressedProperty, (_, args) =>
                {
                    if (args.NewValue)
                        RequestClose();
                });
                break;
        }

        leaf.PointerPressed += (_, _) => events.Add("leaf-press");
        root.PointerPressed += (_, _) => events.Add("root-press");
        leaf.PointerCanceled += (_, _) => events.Add("cancel");

        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);

        string[] expected = expectCancellation ? ["request", "cancel"] : ["request"];
        Assert.Equal(expected, events);
        Assert.False(scene.IsOpen());
        Assert.Null(scene.FocusedNode);
        Assert.False(leaf.IsPressed);
        Assert.False(leaf.IsHovered);
        Assert.False(leaf.IsFocused);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void KeyRouteContinuesAfterImmediateClose(bool keyUp)
    {
        var (manager, scene, root) = OpenScene();
        var leaf = Place(root, new TestNode { Focusable = true }, 0, 0, 20, 20);
        var events = new List<string>();

        void RequestClose(object? sender, KeyEventArgs args)
        {
            events.Add("leaf");
            manager.Close();
        }

        leaf.KeyDown += RequestClose;
        leaf.KeyUp += RequestClose;
        root.KeyDown += (_, _) => events.Add("root");
        root.KeyUp += (_, _) => events.Add("root");
        manager.CurrentSceneChanged += (_, _) => events.Add("closed");
        Assert.True(leaf.Focus());

        if (keyUp)
            manager.ProcessKeyUp(Key.A, KeyModifiers.None);
        else
            manager.ProcessKeyDown(Key.A, KeyModifiers.None);

        Assert.Equal(["leaf", "closed", "root"], events);
        Assert.False(scene.IsOpen());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WheelClosePreservesHandledAndRejectsFurtherSceneInput(bool handled)
    {
        var (manager, scene, root) = OpenScene();
        var leaf = Place(root, new TestNode(), 0, 0, 20, 20);
        var events = new List<string>();
        leaf.PointerWheel += (_, args) =>
        {
            events.Add("leaf");
            manager.Close();
            Assert.Throws<InvalidOperationException>(() => scene.ProcessPointerMoved(Point.Zero));
            args.Handled = handled;
        };
        root.PointerWheel += (_, _) => events.Add("root");
        manager.CurrentSceneChanged += (_, _) => events.Add("closed");

        var result = manager.ProcessPointerWheel(new Point(5, 5), 0, 1);

        Assert.Equal(handled, result);
        string[] expected = handled ? ["leaf", "closed"] : ["leaf", "closed", "root"];
        Assert.Equal(expected, events);
        Assert.False(scene.IsOpen());
    }

    [Fact]
    public void DestructionDuringInputCompletesBeforeRouteContinues()
    {
        var (manager, scene, root) = OpenScene();
        var leaf = Place(root, new TestNode(), 0, 0, 20, 20);
        var events = new List<string>();
        leaf.PointerPressed += (_, _) =>
        {
            events.Add("leaf");
            manager.Destroy();
            Assert.False(scene.IsOpen());
        };
        root.PointerPressed += (_, _) => events.Add("root");
        leaf.PointerCanceled += (_, _) => events.Add("cancel");
        manager.CurrentSceneChanged += (_, _) => events.Add("destroyed");

        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);

        Assert.Equal(["leaf", "cancel", "destroyed", "root"], events);
        Assert.Throws<ObjectDisposedException>(manager.Close);
    }

    [Fact]
    public void InputFailureDoesNotUndoCompletedDestruction()
    {
        var (manager, scene, root) = OpenScene();
        var leaf = Place(root, new TestNode(), 0, 0, 20, 20);
        var expected = new InvalidOperationException("input failed");
        EventHandler<PointerEventArgs> handler = (_, _) =>
        {
            manager.Destroy();
            throw expected;
        };
        leaf.PointerMoved += handler;

        Assert.Same(expected, Assert.Throws<InvalidOperationException>(() =>
            manager.ProcessPointerMoved(new Point(5, 5))));

        Assert.False(scene.IsOpen());
        Assert.Throws<ObjectDisposedException>(manager.Close);
    }

    [Fact]
    public void ClosingCallbackSceneChangeCompletesBeforeNextInputHandlerRequest()
    {
        var manager = new UIManager();
        var root = new Canvas();
        var leaf = Place(root, new TestNode(), 0, 0, 20, 20);
        var events = new List<string>();
        var firstReplacement = new RecordingScene { Opened = () => events.Add("first") };
        var secondReplacement = new RecordingScene { Opened = () => events.Add("second") };
        var scene = new RecordingScene(root) { Closing = () => manager.Open(secondReplacement) };
        leaf.PointerMoved += (_, _) =>
        {
            manager.Close();
            manager.Open(firstReplacement);
        };
        root.PointerMoved += (_, _) => events.Add("root");
        manager.Open(scene);
        manager.UpdateFrame(new Size(100, 100), 0);

        manager.ProcessPointerMoved(new Point(5, 5));

        Assert.Equal(["second", "first", "root"], events);
        Assert.Same(firstReplacement, manager.CurrentScene);
    }

    [Fact]
    public void ReleaseRouteContinuesAfterCloseWithoutClickOrCancellation()
    {
        var (manager, scene, root) = OpenScene();
        var leaf = Place(root, new TestNode(), 0, 0, 20, 20);
        var events = new List<string>();
        leaf.PointerReleased += (_, _) =>
        {
            events.Add("leaf-release");
            manager.Close();
        };
        root.PointerReleased += (_, _) => events.Add("root-release");
        leaf.PointerClicked += (_, _) => events.Add("leaf-click");
        root.PointerClicked += (_, _) => events.Add("root-click");
        leaf.PointerCanceled += (_, _) => events.Add("cancel");
        manager.CurrentSceneChanged += (_, _) => events.Add("closed");
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);

        manager.ProcessPointerReleased(new Point(5, 5), MouseButton.Left, KeyModifiers.None);

        Assert.Equal(["leaf-release", "closed", "root-release"], events);
        Assert.False(scene.IsOpen());
    }

    [Fact]
    public void InputFailureDoesNotUndoCompletedCloseAndReplacement()
    {
        var (manager, scene, root) = OpenScene();
        var leaf = Place(root, new TestNode(), 0, 0, 20, 20);
        var replacement = new GameUIScene();
        var expected = new InvalidOperationException("input failed");
        EventHandler<PointerEventArgs> handler = (_, _) =>
        {
            manager.Close();
            manager.Open(replacement);
            throw expected;
        };
        leaf.PointerMoved += handler;

        Assert.Same(expected, Assert.Throws<InvalidOperationException>(() =>
            manager.ProcessPointerMoved(new Point(5, 5))));

        Assert.Same(replacement, manager.CurrentScene);
        Assert.False(scene.IsOpen());
        Assert.True(replacement.IsOpen());
        manager.Close();
        Assert.False(replacement.IsOpen());
    }

    [Fact]
    public void CloseFailureInterruptsInputBeforeLaterRequestsAndBubbling()
    {
        var manager = new UIManager();
        var root = new Canvas();
        var leaf = Place(root, new TestNode(), 0, 0, 20, 20);
        var expected = new InvalidOperationException("close failed");
        var events = new List<string>();
        var scene = new RecordingScene(root) { Closing = () => throw expected };
        var replacement = new GameUIScene();
        leaf.PointerMoved += (_, _) =>
        {
            manager.Close();
            manager.Open(replacement);
        };
        root.PointerMoved += (_, _) => events.Add("root");
        manager.Open(scene);
        manager.UpdateFrame(new Size(100, 100), 0);

        Assert.Same(expected, Assert.Throws<InvalidOperationException>(() =>
            manager.ProcessPointerMoved(new Point(5, 5))));

        Assert.Empty(events);
        Assert.Null(manager.CurrentScene);
        Assert.False(replacement.IsOpen());
        manager.Open(new GameUIScene());
        Assert.False(replacement.IsOpen());
    }

    [Fact]
    public void FocusLossThenCloseDoesNotCancelAgain()
    {
        var (manager, scene, root) = OpenScene();
        var leaf = Place(root, new TestNode(), 0, 0, 20, 20);
        var buttons = new List<MouseButton>();
        leaf.PointerCanceled += (_, args) => buttons.Add(args.Button);
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Right, KeyModifiers.None);

        manager.ProcessFocusChanged(false);
        manager.Close();
        scene.Close();

        Assert.Equal([MouseButton.Left, MouseButton.Right], buttons);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CloseSkipsTargetRemovedBeforeCancellation(bool removeOnClosing)
    {
        var root = new Canvas();
        var leaf = Place(root, new TestNode { Focusable = true }, 0, 0, 20, 20);
        var scene = new RecordingScene(root);
        var canceled = 0;
        leaf.PointerCanceled += (_, _) => canceled++;
        if (removeOnClosing)
            scene.Closing = () => root.Children.Remove(leaf);
        else
            leaf.LostFocus += (_, _) => root.Children.Remove(leaf);
        scene.Open();
        scene.PrepareFrame(new Size(100, 100));
        scene.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);

        scene.Close();

        Assert.Equal(0, canceled);
        Assert.Null(leaf.Scene);
    }

    [Fact]
    public void CloseCancellationKeepsRouteSnapshotAndSkipsDetachedAncestors()
    {
        var (manager, _, root) = OpenScene();
        var branch = Place(root, new Canvas(), 0, 0, 30, 30);
        var leaf = Place(branch, new TestNode(), 0, 0, 20, 20);
        var events = new List<string>();
        leaf.PointerCanceled += (_, _) =>
        {
            events.Add("leaf");
            root.Children.Remove(branch);
        };
        branch.PointerCanceled += (_, _) => events.Add("branch");
        root.PointerCanceled += (_, args) =>
        {
            Assert.Same(leaf, args.Source);
            events.Add("root");
        };
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);

        manager.Close();

        Assert.Equal(["leaf", "root"], events);
    }

    [Fact]
    public void CloseCancellationFailurePropagatesAndStopsLaterCallbacksAfterClearingButtons()
    {
        var manager = new UIManager();
        var root = new Canvas();
        var leaf = Place(root, new TestControl(), 0, 0, 20, 20);
        var events = new List<string>();
        var scene = new RecordingScene(root) { Closed = () => events.Add("closed") };
        var expected = new InvalidOperationException("cancel failed");
        leaf.PointerCanceled += (_, args) =>
        {
            Assert.False(leaf.IsPressed);
            events.Add($"cancel-{args.Button}");
            throw expected;
        };
        root.PointerCanceled += (_, _) => events.Add("root");
        manager.CurrentSceneChanged += (_, _) => events.Add("changed");
        manager.Open(scene);
        manager.UpdateFrame(new Size(100, 100), 0);
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Right, KeyModifiers.None);
        events.Clear();

        var actual = Assert.Throws<InvalidOperationException>(manager.Close);

        Assert.Same(expected, actual);
        Assert.Equal(["cancel-Left"], events);
        Assert.True(scene.IsOpen());
        Assert.Null(manager.CurrentScene);
        scene.Close();
        Assert.Equal(["cancel-Left", "closed"], events);
    }

    [Fact]
    public void NodeUnavailabilityDoesNotRaisePointerCanceled()
    {
        var (manager, _, root) = OpenScene();
        var leaf = Place(root, new TestControl(), 0, 0, 20, 20);
        var canceled = 0;
        leaf.PointerCanceled += (_, _) => canceled++;
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        Assert.True(leaf.IsPressed);

        leaf.IsEnabled = false;
        manager.ProcessFocusChanged(false);
        manager.Close();

        Assert.False(leaf.IsPressed);
        Assert.Equal(0, canceled);
    }

    [Fact]
    public void NormalReleaseAndFocusChangesWithoutPendingPressDoNotRaisePointerCanceled()
    {
        var (manager, _, root) = OpenScene();
        var leaf = Place(root, new TestNode(), 0, 0, 20, 20);
        var events = new List<string>();
        leaf.PointerCanceled += (_, _) => events.Add("cancel");
        leaf.PointerReleased += (_, _) => events.Add("release");
        leaf.PointerClicked += (_, _) => events.Add("click");

        manager.ProcessPointerMoved(new Point(5, 5));
        manager.ProcessFocusChanged(false);
        manager.ProcessFocusChanged(true);
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        manager.ProcessFocusChanged(true);
        manager.ProcessPointerReleased(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        manager.ProcessFocusChanged(false);
        manager.Close();

        Assert.Equal(["release", "click"], events);
    }

    [Fact]
    public void FocusLossStopsAtFirstNotificationFailureAfterStateCommit()
    {
        var (manager, scene, root) = OpenScene();
        var control = Place(root, new TestControl { Focusable = true }, 0, 0, 20, 20);
        var focusedError = new InvalidOperationException("focused");
        var pressedError = new InvalidOperationException("pressed");
        var hoveredError = new InvalidOperationException("hovered");
        var lostError = new InvalidOperationException("lost");
        var exitError = new InvalidOperationException("exit");
        _ = control.Subscribe(UIElement.IsFocusedProperty, (_, args) =>
        {
            if (!args.NewValue)
                throw focusedError;
        });
        _ = control.Subscribe(Control.IsPressedProperty, (_, args) =>
        {
            if (!args.NewValue)
                throw pressedError;
        });
        _ = control.Subscribe(UIElement.IsHoveredProperty, (_, args) =>
        {
            if (!args.NewValue)
                throw hoveredError;
        });
        control.LostFocus += (_, _) => throw lostError;
        control.PointerExited += (_, _) => throw exitError;
        manager.ProcessPointerMoved(new Point(5, 5));
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);

        var actual = Assert.Throws<InvalidOperationException>(() => manager.ProcessFocusChanged(false));

        Assert.Same(focusedError, actual);
        Assert.False(control.IsFocused);
        Assert.True(control.IsHovered);
        Assert.True(control.IsPressed);
        Assert.Null(scene.FocusedNode);
    }

    [Fact]
    public void FocusRequiresActiveVisibleFocusableNodeWithoutRequiringValidLayout()
    {
        var inactive = new TestNode { Focusable = true };
        Assert.False(inactive.Focus());

        var (manager, scene, root) = OpenScene();
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
        Assert.Same(eligible, scene.FocusedNode);
        Assert.True(hitTestInvisible.Focus());
        Assert.Same(hitTestInvisible, scene.FocusedNode);
        Assert.False(hidden.Focus());
        Assert.Same(hitTestInvisible, scene.FocusedNode);

        eligible.InvalidateArrange();
        Assert.False(eligible.IsArrangeValid);
        Assert.True(eligible.Focus());
        Assert.Same(eligible, scene.FocusedNode);
        manager.UpdateFrame(new Size(100, 100), 0);
        Assert.Same(eligible, scene.FocusedNode);
        manager.Close();
    }

    [Fact]
    public void AttachedNodeCanReceiveFocusAndKeyboardInputBeforeItsFirstLayout()
    {
        var (manager, scene, root) = OpenScene();
        var node = new TestNode { Focusable = true, Width = 20, Height = 20 };
        root.Children.Add(node);
        var gotFocus = 0;
        var keyDown = 0;
        node.GotFocus += (_, _) => gotFocus++;
        node.KeyDown += (_, _) => keyDown++;

        Assert.False(node.IsArrangeValid);
        Assert.True(node.Focus());
        Assert.True(node.IsFocused);
        Assert.Same(node, scene.FocusedNode);
        Assert.Equal(1, gotFocus);

        manager.ProcessKeyDown(Key.A, KeyModifiers.None);
        Assert.Equal(1, keyDown);
        manager.UpdateFrame(new Size(100, 100), 0);
        Assert.Same(node, scene.FocusedNode);
        Assert.Equal(1, gotFocus);
        manager.Close();
    }

    [Fact]
    public void FocusAndClearFocusCommitBeforeDirectNotifications()
    {
        var (_, scene, root) = OpenScene();
        var first = Place(root, new TestNode { Focusable = true }, 0, 0, 20, 20);
        var second = Place(root, new TestNode { Focusable = true }, 30, 0, 20, 20);
        var events = new List<string>();
        first.GotFocus += (_, eventArgs) =>
            events.Add($"first-got:{ReferenceEquals(scene.FocusedNode, first)}:{eventArgs.OldFocus is null}");
        first.LostFocus += (_, eventArgs) =>
            events.Add(
                $"first-lost:{ReferenceEquals(scene.FocusedNode, second)}:{ReferenceEquals(eventArgs.NewFocus, second)}");
        second.GotFocus += (_, eventArgs) =>
            events.Add(
                $"second-got:{ReferenceEquals(scene.FocusedNode, second)}:{ReferenceEquals(eventArgs.OldFocus, first)}");
        second.LostFocus += (_, eventArgs) =>
            events.Add($"second-lost:{scene.FocusedNode is null}:{eventArgs.NewFocus is null}");

        Assert.True(first.Focus());
        Assert.True(second.Focus());
        scene.ClearFocus();

        Assert.Null(scene.FocusedNode);
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
        var (manager, scene, root) = OpenScene();
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
        scene.ClearFocus();
        manager.ProcessKeyDown(Key.A, KeyModifiers.None);

        Assert.Equal(1, focusCalls);
        Assert.Equal(
            ["leaf-down:A:Control", "root-down", "leaf-up", "root-up"],
            events);
    }

    [Fact]
    public void TextInputAndKeyRepeatBubbleFromFocusedNode()
    {
        var (manager, _, root) = OpenScene();
        var leaf = Place(root, new TestNode { Focusable = true }, 0, 0, 20, 20);
        var events = new List<string>();
        TextInputEventArgs? leafTextArgs = null;
        TextInputEventArgs? rootTextArgs = null;
        KeyEventArgs? keyArgs = null;
        leaf.TextInput += (_, args) =>
        {
            leafTextArgs = args;
            events.Add($"leaf:{args.Text}");
        };
        root.TextInput += (_, args) =>
        {
            rootTextArgs = args;
            events.Add("root");
        };
        leaf.KeyDown += (_, args) => keyArgs = args;
        Assert.True(leaf.Focus());

        manager.ProcessTextInput("A\U0001F600中");
        manager.ProcessKeyDown(Key.Left, KeyModifiers.Shift, isRepeat: true);

        Assert.Equal(["leaf:A\U0001F600中", "root"], events);
        var textArgs = Assert.IsType<TextInputEventArgs>(leafTextArgs);
        Assert.Same(textArgs, rootTextArgs);
        Assert.Same(leaf, textArgs.Source);
        Assert.False(textArgs.Handled);
        Assert.Equal("A\U0001F600中", textArgs.Text);
        Assert.True(Assert.IsType<KeyEventArgs>(keyArgs).IsRepeat);
    }

    [Fact]
    public void HandledTextInputStopsAncestorAndNoFocusIsNoOp()
    {
        var (manager, scene, root) = OpenScene();
        var leaf = Place(root, new TestNode { Focusable = true }, 0, 0, 20, 20);
        var events = new List<string>();
        leaf.TextInput += (_, args) =>
        {
            events.Add("leaf");
            args.Handled = true;
        };
        root.TextInput += (_, _) => events.Add("root");

        manager.ProcessTextInput("ignored");
        Assert.Empty(events);

        Assert.True(leaf.Focus());
        manager.ProcessTextInput("handled");
        scene.ClearFocus();
        manager.ProcessTextInput("ignored");

        Assert.Equal(["leaf"], events);
    }

    [Fact]
    public void TextInputRouteContinuesAfterReplacingScene()
    {
        var (manager, _, root) = OpenScene();
        var leaf = Place(root, new TestNode { Focusable = true }, 0, 0, 20, 20);
        var replacementRoot = new TestNode();
        var replacement = new GameUIScene(replacementRoot);
        var events = new List<string>();
        leaf.TextInput += (_, _) =>
        {
            events.Add("old-leaf");
            manager.Open(replacement);
            Assert.Same(replacement, manager.CurrentScene);
        };
        root.TextInput += (_, _) => events.Add("old-root");
        replacementRoot.TextInput += (_, _) => events.Add("new-root");
        Assert.True(leaf.Focus());

        manager.ProcessTextInput("replace");

        Assert.Equal(["old-leaf", "old-root"], events);
        Assert.Same(replacement, manager.CurrentScene);
    }

    [Fact]
    public void TextInputHandlerExceptionStopsRouteAndPreservesInstance()
    {
        var (manager, _, root) = OpenScene();
        var leaf = Place(root, new TestNode { Focusable = true }, 0, 0, 20, 20);
        var expected = new InvalidOperationException("text input");
        var rootCalls = 0;
        leaf.TextInput += (_, _) => throw expected;
        root.TextInput += (_, _) => rootCalls++;
        Assert.True(leaf.Focus());

        var actual = Assert.Throws<InvalidOperationException>(() => manager.ProcessTextInput("text"));

        Assert.Same(expected, actual);
        Assert.Equal(0, rootCalls);
    }

    [Fact]
    public void InvalidFocusIsClearedBeforeKeyboardRouting()
    {
        var (manager, scene, root) = OpenScene();
        var leaf = Place(root, new TestNode { Focusable = true }, 0, 0, 20, 20);
        var events = new List<string>();
        leaf.LostFocus += (_, _) => events.Add("lost");
        leaf.KeyDown += (_, _) => events.Add("key");
        Assert.True(leaf.Focus());
        leaf.Focusable = false;

        manager.ProcessKeyDown(Key.A, KeyModifiers.None);

        Assert.Null(scene.FocusedNode);
        Assert.Equal(["lost"], events);
    }

    [Fact]
    public void FocusedNodeRetainsKeyboardFocusWhileLayoutIsInvalid()
    {
        var (manager, scene, root) = OpenScene();
        var leaf = Place(root, new TestNode { Focusable = true }, 0, 0, 20, 20);
        var events = new List<string>();
        leaf.KeyDown += (_, _) => events.Add("key");
        leaf.TextInput += (_, _) => events.Add("text");
        Assert.True(leaf.Focus());
        leaf.InvalidateMeasure();

        manager.ProcessKeyDown(Key.A, KeyModifiers.None);
        manager.ProcessTextInput("a");

        Assert.Same(leaf, scene.FocusedNode);
        Assert.Equal(["key", "text"], events);
    }

    [Fact]
    public void FocusNotificationFailureStopsLaterNotifications()
    {
        var (_, scene, root) = OpenScene();
        var first = Place(root, new TestNode { Focusable = true }, 0, 0, 20, 20);
        var second = Place(root, new TestNode { Focusable = true }, 30, 0, 20, 20);
        var lostError = new InvalidOperationException("lost");
        var gotError = new InvalidOperationException("got");
        var gotCalls = 0;
        Assert.True(first.Focus());
        first.LostFocus += (_, _) => throw lostError;
        second.GotFocus += (_, _) =>
        {
            gotCalls++;
            throw gotError;
        };

        var actual = Assert.Throws<InvalidOperationException>(() => second.Focus());

        Assert.Same(second, scene.FocusedNode);
        Assert.Same(lostError, actual);
        Assert.Equal(0, gotCalls);
    }

    [Fact]
    public void SingleFocusNotificationErrorKeepsCommittedFocusAndOriginalException()
    {
        var (_, scene, root) = OpenScene();
        var first = Place(root, new TestNode { Focusable = true }, 0, 0, 20, 20);
        var second = Place(root, new TestNode { Focusable = true }, 30, 0, 20, 20);
        var expected = new InvalidOperationException("lost");
        Assert.True(first.Focus());
        first.LostFocus += (_, _) => throw expected;

        var actual = Assert.Throws<InvalidOperationException>(() => second.Focus());

        Assert.Same(expected, actual);
        Assert.Same(second, scene.FocusedNode);
    }

    [Fact]
    public void SynchronousFocusReentryFailsAndPostedFocusRunsOnNextUpdate()
    {
        var (manager, scene, root) = OpenScene();
        var first = Place(root, new TestNode { Focusable = true }, 0, 0, 20, 20);
        var second = Place(root, new TestNode { Focusable = true }, 30, 0, 20, 20);
        Exception? reentryError = null;
        first.GotFocus += (_, _) =>
        {
            reentryError = Record.Exception(() => { first.Focus(); });
            scene.Post(() => second.Focus());
        };

        Assert.True(first.Focus());
        Assert.Same(first, scene.FocusedNode);
        Assert.IsType<InvalidOperationException>(reentryError);

        manager.UpdateFrame(new Size(100, 100), 0);

        Assert.Same(second, scene.FocusedNode);
    }

    [Fact]
    public void BubbleSnapshotContinuesAfterReplacementAndNeverEntersReplacementScene()
    {
        var (manager, _, root) = OpenScene();
        var leaf = Place(root, new TestNode(), 0, 0, 20, 20);
        var replacementRoot = new Canvas();
        var replacement = new GameUIScene(replacementRoot);
        var events = new List<string>();
        leaf.PointerMoved += (_, _) =>
        {
            events.Add("old-leaf");
            manager.Open(replacement);
            Assert.Same(replacement, manager.CurrentScene);
        };
        root.PointerMoved += (_, _) => events.Add("old-root");
        replacementRoot.PointerMoved += (_, _) => events.Add("new-root");

        manager.ProcessPointerMoved(new Point(5, 5));

        Assert.Equal(["old-leaf", "old-root"], events);
        Assert.Same(replacement, manager.CurrentScene);
    }

    [Fact]
    public void SameSceneReparentClearsFocusPressAndHoverDuringRemoval()
    {
        var (manager, scene, root) = OpenScene();
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

        Assert.Null(scene.FocusedNode);
        Assert.Equal(["leaf-exit"], events);

        manager.UpdateFrame(new Size(100, 100), 0);
        manager.ProcessPointerReleased(new Point(55, 5), MouseButton.Left, KeyModifiers.None);

        Assert.Equal(["leaf-exit", "leaf-enter"], events);
        Assert.Null(scene.FocusedNode);
    }

    [Fact]
    public void CrossSceneMoveClearsOldSceneInteractionState()
    {
        var (oldManager, oldScene, oldRoot) = OpenScene();
        var leaf = Place(oldRoot, new TestNode { Focusable = true }, 0, 0, 20, 20);
        var (newManager, newScene, newRoot) = OpenScene();
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
        Assert.Null(oldScene.FocusedNode);
        Assert.Same(newScene, leaf.Scene);

        oldManager.ProcessPointerReleased(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        Assert.DoesNotContain("release", events);
        newManager.UpdateFrame(new Size(100, 100), 0);
    }

    [Fact]
    public void SubtreeRemovalClearsFocusPressAndExitsOldHoverPathAfterCommit()
    {
        var (manager, scene, root) = OpenScene();
        var branch = Place(root, new Canvas(), 10, 10, 30, 30);
        var leaf = Place(branch, new TestNode { Focusable = true }, 0, 0, 20, 20);
        var events = new List<string>();
        Point? exitPosition = null;
        Exception? conversionError = null;
        leaf.LostFocus += (_, _) =>
            events.Add($"lost:{leaf.Parent is not null}:{leaf.Scene is null}");
        leaf.PointerExited += (_, eventArgs) =>
        {
            events.Add($"leaf-exit:{leaf.Scene is null}");
            exitPosition = eventArgs.GetPosition(leaf);
            conversionError = Record.Exception(() =>
                leaf.SceneToLocal(eventArgs.ScenePosition));
        };
        branch.PointerExited += (_, _) =>
            events.Add($"branch-exit:{branch.Scene is null}");
        leaf.PointerReleased += (_, _) => events.Add("release");
        manager.ProcessPointerMoved(new Point(15, 15));
        Assert.True(leaf.Focus());
        manager.ProcessPointerPressed(new Point(15, 15), MouseButton.Left, KeyModifiers.None);
        events.Clear();

        Assert.True(root.Children.Remove(branch));

        Assert.Null(scene.FocusedNode);
        Assert.Equal(
            ["lost:True:True", "leaf-exit:True", "branch-exit:True"],
            events);
        Assert.Equal(new Point(5, 5), exitPosition);
        Assert.IsType<InvalidOperationException>(conversionError);
        manager.ProcessPointerReleased(new Point(15, 15), MouseButton.Left, KeyModifiers.None);
        Assert.DoesNotContain("release", events);
    }

    [Fact]
    public void CleanupFailureStopsLaterNotificationsAndLeavesCommittedState()
    {
        var (manager, scene, root) = OpenScene();
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

        var actual = Assert.Throws<InvalidOperationException>(() => { root.Children.Remove(branch); });

        Assert.Same(lostError, actual);
        Assert.Equal(["lost"], notifications);
        Assert.DoesNotContain(branch, root.Children);
        Assert.Null(branch.Scene);
        Assert.Null(leaf.Scene);
        Assert.Null(scene.FocusedNode);
    }

    [Fact]
    public void ClearAndReplaceMergeRemovedSubtreesIntoOneSceneCleanup()
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
        Assert.Null(first.Scene);
        Assert.Null(second.Scene);
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
        manager.UpdateFrame(new Size(100, 100), 0);
        leaf.IsHitTestVisible = true;
        manager.UpdateFrame(new Size(100, 100), 0);
        Canvas.SetLeft(leaf, 30);
        manager.UpdateFrame(new Size(100, 100), 0);

        Assert.Equal(["exit", "enter", "exit"], events);
    }

    [Fact]
    public void ScaleChangeInHandlerKeepsCurrentEventAndInvalidatesNewHitTests()
    {
        var manager = new UIManager();
        var root = new Canvas();
        var leaf = new TestNode { Width = 10, Height = 10 };
        Canvas.SetLeft(leaf, 10);
        Canvas.SetTop(leaf, 0);
        root.Children.Add(leaf);
        var scene = new GameUIScene(root) { Scale = 2 };
        PointerEventArgs? moved = null;
        PointerWheelEventArgs? wheel = null;
        leaf.PointerMoved += (_, eventArgs) =>
        {
            moved = eventArgs;
            scene.Scale = 4;
        };
        leaf.PointerWheel += (_, eventArgs) => wheel = eventArgs;
        manager.Open(scene);
        manager.UpdateFrame(new Size(100, 80), 0);

        manager.ProcessPointerMoved(new Point(24, 8));

        Assert.NotNull(moved);
        Assert.Equal(new Point(12, 4), moved.ScenePosition);
        Assert.Equal(new Point(2, 4), moved.GetPosition(leaf));
        Assert.Equal(4, scene.Scale);
        Assert.Null(scene.HitTest(new Point(12, 4)));
        Assert.False(root.IsArrangeValid);
        Assert.False(leaf.IsArrangeValid);

        manager.UpdateFrame(new Size(100, 80), 0);
        manager.ProcessPointerWheel(new Point(48, 16), 3, -5);

        Assert.NotNull(wheel);
        Assert.Equal(new Point(12, 4), wheel.ScenePosition);
        Assert.Equal(3, wheel.DeltaX);
        Assert.Equal(-5, wheel.DeltaY);
        manager.Close();
    }

    [Fact]
    public void MeasureCallbackReentrantPointerRoutingFailsAndRecoversAfterFinally()
    {
        var manager = new UIManager();
        var root = new LayoutActionNode();
        var scene = new GameUIScene(root);
        manager.Open(scene);
        manager.UpdateFrame(new Size(100, 100), 0);

        root.MeasureAction = () => manager.ProcessPointerMoved(new Point(1, 1));

        Assert.Throws<InvalidOperationException>(() => manager.UpdateFrame(new Size(200, 200), 0));
        root.MeasureAction = null;

        Assert.Null(Record.Exception(() =>
        {
            manager.UpdateFrame(new Size(100, 100), 0);
            manager.ProcessPointerMoved(new Point(1, 1));
        }));
        manager.Close();
    }

    [Fact]
    public void ArrangeCallbackReentrantPointerRoutingFailsAndRecoversAfterFinally()
    {
        var manager = new UIManager();
        var root = new LayoutActionNode();
        var scene = new GameUIScene(root);
        manager.Open(scene);
        manager.UpdateFrame(new Size(100, 100), 0);

        root.ArrangeAction = () => manager.ProcessPointerMoved(new Point(1, 1));

        Assert.Throws<InvalidOperationException>(() => manager.UpdateFrame(new Size(200, 200), 0));
        root.ArrangeAction = null;

        Assert.Null(Record.Exception(() =>
        {
            manager.UpdateFrame(new Size(100, 100), 0);
            manager.ProcessPointerMoved(new Point(1, 1));
        }));
        manager.Close();
    }

    [Fact]
    public void PendingActionInputUsesCurrentScaleAndSkipsInvalidLayout()
    {
        var manager = new UIManager();
        var root = new Canvas();
        var leaf = new TestNode { Width = 10, Height = 10 };
        Canvas.SetLeft(leaf, 15);
        Canvas.SetTop(leaf, 0);
        root.Children.Add(leaf);
        var scene = new GameUIScene(root);
        var positions = new List<Point>();
        var leafPositions = new List<Point>();
        leaf.PointerMoved += (_, eventArgs) =>
        {
            positions.Add(eventArgs.ScenePosition);
            leafPositions.Add(eventArgs.GetPosition(leaf));
        };
        manager.Open(scene);
        manager.UpdateFrame(new Size(100, 80), 0);

        scene.Post(() =>
        {
            scene.Scale = 2;
            manager.ProcessPointerMoved(new Point(24, 8));
        });
        manager.UpdateFrame(new Size(100, 80), 0);
        manager.ProcessPointerMoved(new Point(40, 16));

        Assert.Equal(2, scene.Scale);
        Assert.Equal([new Point(20, 8)], positions);
        Assert.Equal([new Point(5, 8)], leafPositions);
        manager.Close();
    }

    [Fact]
    public void HandlerLayoutChangesKeepCurrentRouteAndWheelDelta()
    {
        var manager = new UIManager();
        var root = new Canvas();
        var leaf = new TestNode { Width = 20, Height = 20 };
        Canvas.SetLeft(leaf, 20);
        Canvas.SetTop(leaf, 0);
        root.Children.Add(leaf);
        var scene = new GameUIScene(root) { Scale = 2 };
        var events = new List<string>();
        Point? movedPosition = null;
        Point? movedLeafPosition = null;
        leaf.PointerMoved += (_, eventArgs) =>
        {
            events.Add("leaf-move");
            movedPosition = eventArgs.ScenePosition;
            movedLeafPosition = eventArgs.GetPosition(leaf);
            scene.Scale = 4;
            scene.UseLayoutRounding = false;
        };
        root.PointerMoved += (_, _) => events.Add("root-move");
        double? wheelX = null;
        double? wheelY = null;
        leaf.PointerWheel += (_, eventArgs) =>
        {
            events.Add("leaf-wheel");
            wheelX = eventArgs.DeltaX;
            wheelY = eventArgs.DeltaY;
            scene.Scale = 5;
            scene.UseLayoutRounding = true;
        };
        root.PointerWheel += (_, _) => events.Add("root-wheel");
        manager.Open(scene);
        manager.UpdateFrame(new Size(100, 100), 0);

        manager.ProcessPointerMoved(new Point(44, 8));

        Assert.Equal(["leaf-move", "root-move"], events);
        Assert.Equal(new Point(22, 4), movedPosition);
        Assert.Equal(new Point(2, 4), movedLeafPosition);
        Assert.Equal(4, scene.Scale);
        Assert.False(scene.UseLayoutRounding);
        Assert.False(leaf.IsArrangeValid);

        events.Clear();
        manager.UpdateFrame(new Size(100, 100), 0);
        manager.ProcessPointerWheel(new Point(84, 8), 2.5, -1.5);

        Assert.Equal(["leaf-wheel", "root-wheel"], events);
        Assert.Equal(2.5, wheelX);
        Assert.Equal(-1.5, wheelY);
        Assert.Equal(5, scene.Scale);
        Assert.True(scene.UseLayoutRounding);
        Assert.False(leaf.IsArrangeValid);
        manager.Close();
    }

    [Fact]
    public void ScaleChangeReprojectsStoredOutputPointerWhenHoverRefreshes()
    {
        var manager = new UIManager();
        var root = new Canvas();
        var leaf = new TestNode { Width = 20, Height = 20 };
        Canvas.SetLeft(leaf, 30);
        Canvas.SetTop(leaf, 0);
        root.Children.Add(leaf);
        var scene = new GameUIScene(root);
        var events = new List<string>();
        Point? enteredPosition = null;
        Point? exitedPosition = null;
        Point? movedPosition = null;
        leaf.PointerEntered += (_, eventArgs) =>
        {
            events.Add("enter");
            enteredPosition = eventArgs.ScenePosition;
        };
        leaf.PointerExited += (_, eventArgs) =>
        {
            events.Add("exit");
            exitedPosition = eventArgs.ScenePosition;
        };
        leaf.PointerMoved += (_, eventArgs) => movedPosition = eventArgs.ScenePosition;
        manager.Open(scene);
        manager.UpdateFrame(new Size(100, 100), 0);
        manager.ProcessPointerMoved(new Point(15, 5));

        scene.Scale = 0.5;
        manager.ProcessPointerMoved(new Point(15, 5));
        Assert.Empty(events);

        manager.UpdateFrame(new Size(100, 100), 0);

        Assert.Equal(["enter"], events);
        Assert.Equal(new Point(30, 10), enteredPosition);

        manager.ProcessPointerMoved(new Point(16, 6));
        Assert.Equal(new Point(32, 12), movedPosition);

        scene.Scale = 2;
        manager.UpdateFrame(new Size(100, 100), 0);

        Assert.Equal(["enter", "exit"], events);
        Assert.Equal(new Point(8, 3), exitedPosition);
        manager.Close();
    }

    [Fact]
    public void SceneCloseAndReuseStartsWithEmptyInteractionState()
    {
        var manager = new UIManager();
        var root = new Canvas();
        var leaf = Place(root, new TestNode { Focusable = true }, 0, 0, 20, 20);
        var scene = new GameUIScene(root);
        var events = new List<string>();
        leaf.PointerEntered += (_, _) => events.Add("enter");
        leaf.PointerReleased += (_, _) => events.Add("release");
        manager.Open(scene);
        manager.UpdateFrame(new Size(100, 100), 0);
        manager.ProcessPointerMoved(new Point(5, 5));
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        events.Clear();

        manager.Close();
        manager.Open(scene);
        manager.UpdateFrame(new Size(100, 100), 0);
        manager.ProcessPointerReleased(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        manager.ProcessPointerMoved(new Point(5, 5));

        Assert.Null(scene.FocusedNode);
        Assert.Equal(["enter"], events);
    }

    [Fact]
    public void OpenedFailurePreservesInputStateAndSceneOwnership()
    {
        var manager = new UIManager();
        var root = new TestNode { Focusable = true };
        var expected = new InvalidOperationException("opened");
        var scene = new RecordingScene(root)
        {
            Opened = () =>
            {
                root.Measure(new Size(20, 20));
                root.Arrange(new Rect(0, 0, 20, 20));
                Assert.True(root.Focus());
                throw expected;
            }
        };

        var actual = Assert.Throws<InvalidOperationException>(() => manager.Open(scene));

        Assert.Same(expected, actual);
        Assert.Null(manager.CurrentScene);
        Assert.Same(scene, root.Scene);
        Assert.Same(root, scene.FocusedNode);
        Assert.True(root.IsFocused);
        Assert.True(scene.IsOpen());
        var otherManager = new UIManager();
        Assert.Throws<InvalidOperationException>(() => otherManager.Open(scene));
        Assert.Null(otherManager.CurrentScene);
    }

    [Fact]
    public void ReplacementCleanupFailureLeavesCandidateUnopened()
    {
        var manager = new UIManager();
        var oldRoot = new TestNode { Focusable = true };
        var oldScene = new GameUIScene(oldRoot);
        var expected = new InvalidOperationException("lost");
        oldRoot.LostFocus += (_, _) => throw expected;
        manager.Open(oldScene);
        oldRoot.Measure(new Size(20, 20));
        oldRoot.Arrange(new Rect(0, 0, 20, 20));
        Assert.True(oldRoot.Focus());
        var candidate = new GameUIScene(new TestNode());

        var actual = Assert.Throws<InvalidOperationException>(() => manager.Open(candidate));

        Assert.Same(expected, actual);
        Assert.Null(manager.CurrentScene);
        var otherManager = new UIManager();
        otherManager.Open(candidate);
        Assert.Same(candidate, otherManager.CurrentScene);
    }

    [Fact]
    public void DestroyStopsBeforeHudAfterInputCleanupFailure()
    {
        var manager = new UIManager();
        var root = new TestNode { Focusable = true };
        var scene = new GameUIScene(root);
        var expected = new InvalidOperationException("lost");
        root.LostFocus += (_, _) => throw expected;
        manager.Open(scene);
        root.Measure(new Size(20, 20));
        root.Arrange(new Rect(0, 0, 20, 20));
        Assert.True(root.Focus());

        var actual = Assert.Throws<InvalidOperationException>(manager.Destroy);

        Assert.Same(expected, actual);
        Assert.Null(manager.CurrentScene);
        Assert.Same(scene, root.Scene);
        Assert.True(manager.Hud.Scene.IsOpen());
    }

    [Fact]
    public void OpeningCannotFocusAndOpenedCanFocus()
    {
        var manager = new UIManager();
        var root = new TestNode { Focusable = true };
        var scene = new RecordingScene(root);
        bool? openingResult = null;
        bool? openedResult = null;
        scene.Opening = () => openingResult = root.Focus();
        scene.Opened = () =>
        {
            root.Measure(new Size(20, 20));
            root.Arrange(new Rect(0, 0, 20, 20));
            openedResult = root.Focus();
        };

        manager.Open(scene);

        Assert.Equal(false, openingResult);
        Assert.Equal(true, openedResult);
        Assert.Same(root, scene.FocusedNode);
    }

    [Fact]
    public void CloseStopsAfterCommittedCleanupFailureWithoutReleasingScene()
    {
        var manager = new UIManager();
        var root = new Canvas();
        var leaf = Place(root, new TestNode { Focusable = true }, 0, 0, 20, 20);
        var closedCalls = 0;
        var scene = new RecordingScene(root)
        {
            Closed = () => closedCalls++
        };
        var expected = new InvalidOperationException("lost");
        leaf.LostFocus += (_, _) => throw expected;
        manager.Open(scene);
        manager.UpdateFrame(new Size(100, 100), 0);
        Assert.True(leaf.Focus());

        var actual = Assert.Throws<InvalidOperationException>(manager.Close);

        Assert.Same(expected, actual);
        Assert.Equal(0, closedCalls);
        Assert.Null(manager.CurrentScene);
        Assert.Same(scene, root.Scene);
        Assert.Null(scene.FocusedNode);
        Assert.True(scene.IsOpen());
        var otherManager = new UIManager();
        Assert.Throws<InvalidOperationException>(() => otherManager.Open(scene));
        Assert.Null(otherManager.CurrentScene);
    }

    [Fact]
    public void InputDuringCommittedCloseCleanupUsesHostAndSceneTransitionPolicies()
    {
        var manager = new UIManager();
        var root = new Canvas();
        var leaf = Place(root, new TestNode { Focusable = true }, 0, 0, 20, 20);
        var scene = new GameUIScene(root);
        Exception? managerError = null;
        Exception? sceneError = null;
        var routedMoves = 0;
        root.PointerMoved += (_, _) => routedMoves++;
        leaf.LostFocus += (_, _) =>
        {
            managerError = Record.Exception(() => manager.ProcessPointerMoved(Point.Zero));
            sceneError = Record.Exception(() => scene.ProcessPointerMoved(Point.Zero));
        };
        manager.Open(scene);
        manager.UpdateFrame(new Size(100, 100), 0);
        Assert.True(leaf.Focus());

        manager.Close();

        Assert.Null(managerError);
        Assert.IsType<InvalidOperationException>(sceneError);
        Assert.Equal(0, routedMoves);
        Assert.Null(manager.CurrentScene);
        Assert.Same(scene, root.Scene);
    }

    [Fact]
    public void DetachCallbackCannotReopenSameSceneBeforeCleanupReturns()
    {
        var (manager, scene, root) = OpenScene();
        var branch = Place(root, new Canvas(), 10, 10, 30, 30);
        var leaf = Place(branch, new TestNode { Focusable = true }, 0, 0, 20, 20);
        var notifications = new List<string>();
        Exception? reopenError = null;
        leaf.LostFocus += (_, _) =>
        {
            notifications.Add("lost");
            manager.Close();
            reopenError = Record.Exception(() => manager.Open(scene));
        };
        leaf.PointerExited += (_, _) => notifications.Add("leaf-exit");
        branch.PointerExited += (_, _) => notifications.Add("branch-exit");
        manager.ProcessPointerMoved(new Point(15, 15));
        Assert.True(leaf.Focus());

        Assert.True(root.Children.Remove(branch));

        Assert.IsType<InvalidOperationException>(reopenError);
        Assert.Equal(["lost", "leaf-exit", "branch-exit"], notifications);
        Assert.Null(manager.CurrentScene);
        manager.Open(scene);
        manager.Close();
    }

    [Fact]
    public void UISceneInputRoutingWorksWithoutUIManager()
    {
        var root = new Canvas();
        var leaf = Place(root, new TestNode { Focusable = true }, 0, 0, 20, 20);
        var scene = new UIScene(root);
        var events = new List<string>();
        leaf.PointerMoved += (_, _) => events.Add("move");
        leaf.PointerPressed += (_, _) => events.Add("press");
        leaf.PointerReleased += (_, _) => events.Add("release");
        leaf.PointerClicked += (_, _) => events.Add("click");
        leaf.KeyDown += (_, _) => events.Add("key-down");
        leaf.LostFocus += (_, _) => events.Add("lost-focus");
        leaf.PointerExited += (_, _) => events.Add("exit");

        scene.Open();
        scene.PrepareFrame(new Size(100, 100));
        scene.ProcessPointerMoved(new Point(5, 5));
        scene.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        scene.ProcessPointerReleased(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        scene.ProcessKeyDown(Key.A, KeyModifiers.None);
        Assert.True(root.Children.Remove(leaf));
        scene.Close();

        Assert.Equal(
            ["move", "press", "release", "click", "key-down", "lost-focus", "exit"],
            events);
        Assert.Null(scene.FocusedNode);
        Assert.Null(leaf.Scene);
        Assert.Same(scene, scene.Root!.Scene);
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
    public void PointerCallbackCannotReopenSameSceneBeforeRoutingCompletes()
    {
        var manager = new UIManager();
        var root = new Canvas();
        var leaf = Place(root, new TestNode(), 0, 0, 20, 20);
        var scene = new GameUIScene(root);
        var events = new List<string>();
        Exception? reopenError = null;
        leaf.PointerMoved += (_, _) =>
        {
            events.Add("leaf");
            manager.Close();
            reopenError = Record.Exception(() => manager.Open(scene));
            Assert.Null(manager.CurrentScene);
            Assert.False(scene.IsOpen());
        };
        root.PointerMoved += (_, _) => events.Add("root");
        manager.Open(scene);
        manager.UpdateFrame(new Size(100, 100), 0);

        manager.ProcessPointerMoved(new Point(5, 5));

        Assert.Equal(["leaf", "root"], events);
        Assert.IsType<InvalidOperationException>(reopenError);
        Assert.Null(manager.CurrentScene);
        manager.Open(scene);
        manager.Close();
    }

    [Fact]
    public void FocusCallbackCannotReopenSameSceneBeforeTransitionReturns()
    {
        var manager = new UIManager();
        var root = new Canvas();
        var first = Place(root, new TestNode { Focusable = true }, 0, 0, 20, 20);
        var scene = new GameUIScene(root);
        Exception? reopenError = null;
        first.LostFocus += (_, _) =>
        {
            manager.Close();
            reopenError = Record.Exception(() => manager.Open(scene));
        };
        manager.Open(scene);
        manager.UpdateFrame(new Size(100, 100), 0);
        Assert.True(first.Focus());

        scene.ClearFocus();

        Assert.IsType<InvalidOperationException>(reopenError);
        Assert.Null(manager.CurrentScene);
        manager.Open(scene);
        manager.Close();
    }

    [Fact]
    public void CloseNotifiesInteractionLossWithoutClearingSceneOwnership()
    {
        var manager = new UIManager();
        var root = new Canvas();
        var branch = Place(root, new Canvas(), 0, 0, 30, 30);
        var leaf = Place(branch, new TestNode { Focusable = true }, 0, 0, 20, 20);
        var scene = new GameUIScene(root);
        var events = new List<string>();
        leaf.LostFocus += (_, _) =>
        {
            Assert.Same(scene, leaf.Scene);
            events.Add("lost");
        };
        leaf.PointerExited += (_, _) =>
        {
            Assert.Same(scene, leaf.Scene);
            events.Add("leaf-exit");
        };
        branch.PointerExited += (_, _) =>
        {
            Assert.Same(scene, branch.Scene);
            events.Add("branch-exit");
        };
        manager.Open(scene);
        manager.UpdateFrame(new Size(100, 100), 0);
        manager.ProcessPointerMoved(new Point(5, 5));
        Assert.True(leaf.Focus());

        manager.Close();

        Assert.Equal(["lost", "leaf-exit", "branch-exit"], events);
        Assert.Same(scene, root.Scene);
        Assert.Same(scene, branch.Scene);
        Assert.Same(scene, leaf.Scene);
    }

    [Fact]
    public void CloseNotificationCanReplaceRootAndStillCompletesSnapshot()
    {
        var manager = new UIManager();
        var oldRoot = new Canvas();
        var branch = Place(oldRoot, new Canvas(), 0, 0, 30, 30);
        var leaf = Place(branch, new TestNode { Focusable = true }, 0, 0, 20, 20);
        var newRoot = new Canvas();
        var scene = new GameUIScene(oldRoot);
        var events = new List<string>();
        leaf.LostFocus += (_, _) =>
        {
            events.Add("lost");
            scene.Root = newRoot;
        };
        leaf.PointerExited += (_, _) => events.Add("leaf-exit");
        branch.PointerExited += (_, _) => events.Add("branch-exit");
        manager.Open(scene);
        manager.UpdateFrame(new Size(100, 100), 0);
        manager.ProcessPointerMoved(new Point(5, 5));
        Assert.True(leaf.Focus());

        manager.Close();

        Assert.Equal(["lost", "leaf-exit", "branch-exit"], events);
        Assert.Null(oldRoot.Scene);
        Assert.Null(branch.Scene);
        Assert.Null(leaf.Scene);
        Assert.Same(newRoot, scene.Root);
        Assert.Same(scene, newRoot.Scene);
    }

    [Fact]
    public void ReplacingRootSynchronouslyCleansOutgoingInteraction()
    {
        var manager = new UIManager();
        var oldRoot = new Canvas();
        var branch = Place(oldRoot, new Canvas(), 0, 0, 30, 30);
        var leaf = Place(branch, new TestNode { Focusable = true }, 0, 0, 20, 20);
        var newRoot = new Canvas();
        var scene = new GameUIScene(oldRoot);
        var events = new List<string>();
        leaf.LostFocus += (_, _) => events.Add("lost");
        leaf.PointerExited += (_, _) => events.Add("leaf-exit");
        branch.PointerExited += (_, _) => events.Add("branch-exit");
        manager.Open(scene);
        manager.UpdateFrame(new Size(100, 100), 0);
        manager.ProcessPointerMoved(new Point(5, 5));
        Assert.True(leaf.Focus());

        scene.Root = newRoot;

        Assert.Equal(["lost", "leaf-exit", "branch-exit"], events);
        Assert.Null(oldRoot.Scene);
        Assert.Null(branch.Scene);
        Assert.Null(leaf.Scene);
        Assert.Same(scene, newRoot.Scene);
        Assert.Same(newRoot, scene.Root);
        manager.Close();
    }

    [Fact]
    public void PromotingDescendantRootClearsItsInteractionState()
    {
        var manager = new UIManager();
        var oldRoot = new Canvas();
        var incoming = Place(oldRoot, new Canvas(), 0, 0, 30, 30);
        // Explicit alignment keeps the fixed-size node at the origin after promotion;
        // the default Stretch alignment would center it at (35,35).
        incoming.HorizontalAlignment = HorizontalAlignment.Left;
        incoming.VerticalAlignment = VerticalAlignment.Top;
        var leaf = Place(incoming, new TestNode { Focusable = true }, 0, 0, 20, 20);
        var scene = new GameUIScene(oldRoot);
        var events = new List<string>();
        incoming.PointerExited += (_, _) => events.Add("incoming-exit");
        leaf.LostFocus += (_, _) => events.Add("lost");
        leaf.PointerExited += (_, _) => events.Add("leaf-exit");
        leaf.PointerClicked += (_, _) => events.Add("click");
        manager.Open(scene);
        manager.UpdateFrame(new Size(100, 100), 0);
        manager.ProcessPointerMoved(new Point(5, 5));
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        Assert.True(leaf.Focus());

        scene.Root = incoming;

        Assert.Equal(["lost", "leaf-exit", "incoming-exit"], events);
        Assert.Null(scene.FocusedNode);
        Assert.False(leaf.IsFocused);
        Assert.False(leaf.IsHovered);
        Assert.False(incoming.IsHovered);
        Assert.Null(oldRoot.Scene);
        Assert.Same(scene, incoming.Scene);
        Assert.Same(scene, leaf.Scene);

        manager.UpdateFrame(new Size(100, 100), 0);
        manager.ProcessPointerReleased(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        Assert.Equal(["lost", "leaf-exit", "incoming-exit"], events);
        manager.Close();
    }

    [Fact]
    public void RootTransferNotificationFailureKeepsCommittedStructure()
    {
        var incoming = new TestNode { Focusable = true };
        var source = new UIScene(incoming);
        var target = new UIScene(new Canvas());
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
        Assert.Same(target, incoming.Scene);
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
        var target = new UIScene(targetRoot);
        var source = new UIScene(sourceRoot);
        target.Open();
        source.Open();
        target.PrepareFrame(new Size(100, 100));
        source.PrepareFrame(new Size(100, 100));
        Assert.True(targetLeaf.Focus());
        Assert.True(sourceLeaf.Focus());
        UIElement? sourceFocusDuringTargetNotification = source.FocusedNode;
        targetLeaf.LostFocus += (_, _) => sourceFocusDuringTargetNotification = source.FocusedNode;

        target.Root = sourceRoot;

        Assert.Null(sourceFocusDuringTargetNotification);
        Assert.Null(source.FocusedNode);
        Assert.Same(target, sourceLeaf.Scene);
        target.Close();
        source.Close();
    }

    [Fact]
    public void RootTransferTargetNotificationFailureStopsSourceNotifications()
    {
        var targetRoot = new Canvas();
        var targetLeaf = Place(targetRoot, new TestNode { Focusable = true }, 0, 0, 20, 20);
        var sourceRoot = new Canvas();
        var sourceLeaf = Place(sourceRoot, new TestNode { Focusable = true }, 0, 0, 20, 20);
        var target = new UIScene(targetRoot);
        var source = new UIScene(sourceRoot);
        var targetError = new InvalidOperationException("target lost focus");
        var sourceError = new InvalidOperationException("source lost focus");
        var sourceNotified = false;
        targetLeaf.LostFocus += (_, _) => throw targetError;
        sourceLeaf.LostFocus += (_, _) =>
        {
            sourceNotified = true;
            throw sourceError;
        };
        target.Open();
        source.Open();
        target.PrepareFrame(new Size(100, 100));
        source.PrepareFrame(new Size(100, 100));
        Assert.True(targetLeaf.Focus());
        Assert.True(sourceLeaf.Focus());

        var actual = Assert.Throws<InvalidOperationException>(() => target.Root = sourceRoot);

        Assert.Same(targetError, actual);
        Assert.False(sourceNotified);
        Assert.True(sourceLeaf.IsFocused);
        Assert.Null(source.Root);
        Assert.Same(sourceRoot, target.Root);
        Assert.Null(targetLeaf.Scene);
        Assert.Same(target, sourceLeaf.Scene);
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
        var manager = new UIManager();
        var scene = new GameUIScene();
        manager.Open(scene);

        manager.ProcessPointerMoved(Point.Zero);
        manager.ProcessPointerPressed(Point.Zero, MouseButton.Left, KeyModifiers.None);
        manager.ProcessPointerReleased(Point.Zero, MouseButton.Left, KeyModifiers.None);
        manager.ProcessPointerWheel(Point.Zero, 1, -1);
        manager.ProcessKeyDown(Key.A, KeyModifiers.None);
        manager.ProcessKeyUp(Key.A, KeyModifiers.None);

        Assert.Null(scene.FocusedNode);
        manager.Close();
    }

    private static (UIManager Manager, GameUIScene Scene, Canvas Root) OpenScene()
    {
        var manager = new UIManager();
        var root = new Canvas();
        var scene = new GameUIScene(root);
        manager.Open(scene);
        manager.UpdateFrame(new Size(100, 100), 0);
        return (manager, scene, root);
    }

    private static T Place<T>(Canvas parent, T child, double x, double y, double width, double height)
        where T : UIElement
    {
        child.Width = width;
        child.Height = height;
        Canvas.SetLeft(child, x);
        Canvas.SetTop(child, y);
        parent.Children.Add(child);
        if (parent.Scene?.IsOpen() == true)
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

    private sealed class TestNode : UIElement
    {
    }

    private sealed class TestControl : Control
    {
    }

    private sealed class LayoutActionNode : UIElement
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

    private sealed class RecordingScene(UIElement? root = null) : GameUIScene(root)
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