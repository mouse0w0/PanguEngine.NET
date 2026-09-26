using System.Runtime.ExceptionServices;
using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Input;

namespace PanguEngine.Tests.Client.UI;

public sealed class UiNodeInputStateTests
{
    [Fact]
    public void UiNodePropertiesExposeExpectedMetadataAndDefaults()
    {
        var node = new TestNode();

        AssertProperty(
            UiNode.IsEnabledProperty,
            typeof(UiNode),
            nameof(UiNode.IsEnabled),
            defaultValue: true,
            isReadOnly: false,
            UiPropertyInvalidation.Input | UiPropertyInvalidation.Render);
        AssertProperty(
            UiNode.IsHoveredProperty,
            typeof(UiNode),
            nameof(UiNode.IsHovered),
            defaultValue: false,
            isReadOnly: true,
            UiPropertyInvalidation.Render);
        AssertProperty(
            UiNode.IsFocusedProperty,
            typeof(UiNode),
            nameof(UiNode.IsFocused),
            defaultValue: false,
            isReadOnly: true,
            UiPropertyInvalidation.Render);

        Assert.True(node.IsEnabled);
        Assert.False(node.IsHovered);
        Assert.False(node.IsFocused);
    }

    [Fact]
    public void DisabledNodePrunesItsSubtreeFromHitTesting()
    {
        var root = new Canvas();
        var background = Place(root, new TestNode(), 0, 0, 100, 100);
        var container = Place(root, new Canvas(), 0, 0, 40, 40);
        var child = Place(container, new TestNode(), 0, 0, 40, 40);
        var manager = new UiManager();
        var screen = new UiScreen(root);
        manager.Open(screen);
        manager.PrepareFrame(new Size(100, 100), 0);

        Assert.Same(child, screen.HitTest(new Point(5, 5)));

        container.IsEnabled = false;

        Assert.Same(background, screen.HitTest(new Point(5, 5)));
        manager.Close();
    }

    [Fact]
    public void DisabledNodePreventsDescendantProgrammaticFocus()
    {
        var root = new Canvas();
        var container = Place(root, new Canvas(), 0, 0, 40, 40);
        var child = Place(container, new TestNode { Focusable = true }, 0, 0, 40, 40);
        var manager = new UiManager();
        var screen = new UiScreen(root);
        manager.Open(screen);
        manager.PrepareFrame(new Size(100, 100), 0);

        container.IsEnabled = false;
        Assert.False(child.Focus());
        Assert.Null(screen.FocusedNode);

        container.IsEnabled = true;
        manager.PrepareFrame(new Size(100, 100), 0);

        Assert.True(child.Focus());
        Assert.Same(child, screen.FocusedNode);
        manager.Close();
    }

    [Fact]
    public void HoverProjectsAcrossNestedNodesWithoutSiblingChurn()
    {
        var root = new Canvas();
        var outer = Place(root, new Canvas(), 0, 0, 100, 100);
        var inner = Place(outer, new Canvas(), 0, 0, 100, 100);
        var content = Place(inner, new Canvas(), 0, 0, 100, 100);
        var first = Place(content, new TestNode(), 0, 0, 20, 20);
        _ = Place(content, new TestNode(), 30, 0, 20, 20);
        var manager = new UiManager();
        var screen = new UiScreen(root);
        var outerChanges = new List<bool>();
        var innerChanges = new List<bool>();
        var childChanges = new List<bool>();
        using var outerSubscription = outer.Subscribe(
            UiNode.IsHoveredProperty,
            (_, args) => outerChanges.Add(args.NewValue));
        using var innerSubscription = inner.Subscribe(
            UiNode.IsHoveredProperty,
            (_, args) => innerChanges.Add(args.NewValue));
        using var childSubscription = first.Subscribe(
            UiNode.IsHoveredProperty,
            (_, args) => childChanges.Add(args.NewValue));
        var outerMoveCalls = 0;
        first.PointerEntered += (_, _) =>
        {
            Assert.True(outer.IsHovered);
            Assert.True(inner.IsHovered);
        };
        first.PointerMoved += (_, args) => args.Handled = true;
        outer.PointerMoved += (_, _) => outerMoveCalls++;
        manager.Open(screen);
        manager.PrepareFrame(new Size(100, 100), 0);

        manager.ProcessPointerMoved(new Point(5, 5));

        Assert.True(outer.IsHovered);
        Assert.True(inner.IsHovered);
        Assert.True(first.IsHovered);
        Assert.Equal(0, outerMoveCalls);
        Assert.Equal([true], outerChanges);
        Assert.Equal([true], innerChanges);
        Assert.Equal([true], childChanges);

        manager.ProcessPointerMoved(new Point(35, 5));

        Assert.True(outer.IsHovered);
        Assert.True(inner.IsHovered);
        Assert.False(first.IsHovered);
        Assert.Equal([true], outerChanges);
        Assert.Equal([true], innerChanges);
        Assert.Equal([true, false], childChanges);

        manager.ProcessPointerMoved(new Point(150, 150));

        Assert.False(outer.IsHovered);
        Assert.False(inner.IsHovered);
        Assert.Equal([true, false], outerChanges);
        Assert.Equal([true, false], innerChanges);
        manager.Close();
    }

    [Fact]
    public void FocusProjectsOnlyToTheFocusedNodeBeforeFocusEvents()
    {
        var root = new Canvas();
        var container = Place(root, new Canvas { Focusable = true }, 0, 0, 40, 40);
        var child = Place(container, new TestNode { Focusable = true }, 0, 0, 40, 40);
        var manager = new UiManager();
        var screen = new UiScreen(root);
        var states = new List<bool>();
        using var subscription = container.Subscribe(
            UiNode.IsFocusedProperty,
            (_, args) => states.Add(args.NewValue));
        var childStates = new List<bool>();
        using var childSubscription = child.Subscribe(
            UiNode.IsFocusedProperty,
            (_, args) => childStates.Add(args.NewValue));
        var events = new List<string>();
        container.GotFocus += (_, _) => events.Add($"got:{container.IsFocused}");
        container.LostFocus += (_, _) => events.Add($"lost:{container.IsFocused}");
        child.GotFocus += (_, _) => events.Add($"child-got:{child.IsFocused}:{container.IsFocused}");
        child.KeyDown += (_, args) => args.Handled = true;
        manager.Open(screen);
        manager.PrepareFrame(new Size(100, 100), 0);

        Assert.True(container.Focus());
        Assert.True(container.Focus());
        Assert.True(container.IsFocused);

        Assert.True(child.Focus());

        Assert.False(container.IsFocused);
        Assert.True(child.IsFocused);
        Assert.Same(child, screen.FocusedNode);
        manager.ProcessKeyDown(Key.A, KeyModifiers.None);
        Assert.False(container.IsFocused);
        screen.ClearFocus();
        Assert.False(child.IsFocused);
        Assert.Equal([true, false], states);
        Assert.Equal([true, false], childStates);
        Assert.Equal(["got:True", "lost:False", "child-got:True:False"], events);
        manager.Close();
    }

    [Fact]
    public void TreeRemovalClearsNodeStatesBeforeLossEvents()
    {
        var root = new Canvas();
        var container = Place(root, new Canvas { Focusable = true }, 0, 0, 40, 40);
        var child = Place(container, new TestNode { Focusable = true }, 0, 0, 40, 40);
        var manager = new UiManager();
        var screen = new UiScreen(root);
        var events = new List<string>();
        child.LostFocus += (_, _) =>
            events.Add($"lost:{child.IsFocused}:{container.IsHovered}");
        container.PointerExited += (_, _) =>
            events.Add($"exit:{child.IsFocused}:{container.IsHovered}");
        manager.Open(screen);
        manager.PrepareFrame(new Size(100, 100), 0);
        manager.ProcessPointerMoved(new Point(5, 5));
        Assert.True(child.Focus());
        Assert.True(container.IsHovered);
        Assert.True(child.IsFocused);

        Assert.True(root.Children.Remove(container));

        Assert.False(container.IsHovered);
        Assert.False(child.IsFocused);
        Assert.Null(screen.FocusedNode);
        Assert.Equal(["lost:False:False", "exit:False:False"], events);
        manager.Close();
    }

    [Fact]
    public void IndexerReplacementUsesDeepestExitedNodeAsExitSource()
    {
        var root = new Canvas();
        var branch = Place(root, new Canvas(), 0, 0, 40, 40);
        var leaf = Place(branch, new TestNode(), 0, 0, 20, 20);
        var manager = new UiManager();
        var screen = new UiScreen(root);
        UiNode? exitSource = null;
        branch.PointerExited += (_, args) => exitSource = args.Source;
        manager.Open(screen);
        manager.PrepareFrame(new Size(100, 100), 0);
        manager.ProcessPointerMoved(new Point(5, 5));
        Assert.True(branch.IsHovered);
        Assert.True(leaf.IsHovered);

        root.Children[0] = leaf;

        Assert.Same(leaf, root.Children[0]);
        Assert.Null(branch.Screen);
        Assert.Same(screen, leaf.Screen);
        Assert.False(branch.IsHovered);
        Assert.True(leaf.IsHovered);
        Assert.Same(branch, exitSource);
        manager.Close();
    }

    [Fact]
    public void DisablingOrdinaryNodeSynchronouslyClearsDescendantInputState()
    {
        var root = new Canvas();
        var background = Place(root, new TestNode(), 0, 0, 100, 100);
        var container = Place(root, new Canvas(), 0, 0, 40, 40);
        var child = Place(container, new TestControl { Focusable = true }, 0, 0, 40, 40);
        var manager = new UiManager();
        var screen = new UiScreen(root);
        var enabledNotifications = 0;
        using var subscription = container.Subscribe(
            UiNode.IsEnabledProperty,
            (_, args) =>
            {
                if (args.NewValue)
                    return;

                enabledNotifications++;
                Assert.False(container.IsHovered);
                Assert.False(child.IsHovered);
                Assert.False(child.IsPressed);
                Assert.False(child.IsFocused);
                Assert.Null(screen.FocusedNode);
            });
        manager.Open(screen);
        manager.PrepareFrame(new Size(100, 100), 0);
        manager.ProcessPointerMoved(new Point(5, 5));
        Assert.True(child.Focus());
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        Assert.True(container.IsHovered);
        Assert.True(child.IsHovered);
        Assert.True(child.IsPressed);
        Assert.True(child.IsFocused);

        container.IsEnabled = false;

        Assert.Equal(1, enabledNotifications);
        Assert.False(container.IsHovered);
        Assert.False(child.IsHovered);
        Assert.False(child.IsPressed);
        Assert.False(child.IsFocused);
        Assert.Null(screen.FocusedNode);
        Assert.Same(background, screen.HitTest(new Point(5, 5)));

        container.IsEnabled = true;
        Assert.False(container.IsHovered);
        Assert.False(child.IsHovered);
        Assert.False(child.IsPressed);
        Assert.False(child.IsFocused);

        manager.PrepareFrame(new Size(100, 100), 0);

        Assert.True(container.IsHovered);
        Assert.True(child.IsHovered);
        Assert.False(child.IsPressed);
        Assert.False(child.IsFocused);
        manager.Close();
    }

    [Fact]
    public void DisablingFocusedNodeInsideGotFocusDoesNotRestoreFocus()
    {
        var root = new Canvas();
        var node = Place(root, new TestNode { Focusable = true }, 0, 0, 40, 40);
        var manager = new UiManager();
        var screen = new UiScreen(root);
        var events = new List<string>();
        node.GotFocus += (_, _) =>
        {
            events.Add($"got:{node.IsFocused}");
            node.IsEnabled = false;
        };
        node.LostFocus += (_, _) => events.Add($"lost:{node.IsFocused}");
        manager.Open(screen);
        manager.PrepareFrame(new Size(100, 100), 0);

        Assert.True(node.Focus());

        Assert.False(node.IsEnabled);
        Assert.False(node.IsFocused);
        Assert.Null(screen.FocusedNode);
        Assert.Equal(["got:True", "lost:False"], events);
        manager.Close();
    }

    [Theory]
    [InlineData(Visibility.Hidden, false)]
    [InlineData(Visibility.Collapsed, false)]
    [InlineData(Visibility.Hidden, true)]
    [InlineData(Visibility.Collapsed, true)]
    public void HidingNodeClearsInputBeforeLossEventsAndVisibilityNotification(
        Visibility visibility,
        bool hideParent)
    {
        var root = new Canvas();
        var container = Place(root, new Canvas(), 0, 0, 40, 40);
        var child = Place(container, new TestControl { Focusable = true }, 0, 0, 40, 40);
        UiNode target = hideParent ? container : child;
        var manager = new UiManager();
        var screen = new UiScreen(root);
        var events = new List<string>();

        void AssertCleared()
        {
            Assert.Null(screen.FocusedNode);
            Assert.False(child.IsFocused);
            Assert.False(child.IsPressed);
            Assert.False(child.IsHovered);
            Assert.Equal(!hideParent, container.IsHovered);
            Assert.True(root.IsHovered);
        }

        child.LostFocus += (_, _) =>
        {
            AssertCleared();
            events.Add("lost");
        };
        child.PointerExited += (_, _) =>
        {
            AssertCleared();
            events.Add("child-exit");
        };
        container.PointerExited += (_, _) => events.Add("parent-exit");
        using var subscription = target.Subscribe(UiNode.VisibilityProperty, (_, _) =>
        {
            AssertCleared();
            events.Add("visibility");
        });
        manager.Open(screen);
        manager.PrepareFrame(new Size(100, 100), 0);
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        Assert.True(child.IsFocused);
        Assert.True(child.IsHovered);
        Assert.True(child.IsPressed);

        target.Visibility = visibility;

        AssertCleared();
        Assert.Equal(
            hideParent
                ? new[] { "lost", "child-exit", "parent-exit", "visibility" }
                : new[] { "lost", "child-exit", "visibility" },
            events);
        Assert.False(child.Focus());
        manager.Close();
    }

    [Theory]
    [InlineData(Visibility.Hidden)]
    [InlineData(Visibility.Collapsed)]
    public void HidingHoveredBranchPreservesFocusAndPressInAnotherBranch(Visibility visibility)
    {
        var root = new Canvas();
        var pressed = Place(root, new TestControl { Focusable = true }, 0, 0, 40, 40);
        var container = Place(root, new Canvas(), 50, 0, 40, 40);
        var hovered = Place(container, new TestNode(), 0, 0, 40, 40);
        var manager = new UiManager();
        var screen = new UiScreen(root);
        var lostFocusCalls = 0;
        var releaseCalls = 0;
        pressed.LostFocus += (_, _) => lostFocusCalls++;
        pressed.PointerReleased += (_, _) => releaseCalls++;
        manager.Open(screen);
        manager.PrepareFrame(new Size(100, 100), 0);
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        manager.ProcessPointerMoved(new Point(55, 5));
        Assert.True(hovered.IsHovered);
        Assert.True(pressed.IsPressed);

        container.Visibility = visibility;

        Assert.False(container.IsHovered);
        Assert.False(hovered.IsHovered);
        Assert.True(root.IsHovered);
        Assert.True(pressed.IsPressed);
        Assert.True(pressed.IsFocused);
        Assert.Same(pressed, screen.FocusedNode);
        Assert.Equal(0, lostFocusCalls);
        manager.ProcessPointerReleased(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        Assert.Equal(1, releaseCalls);
        Assert.False(pressed.IsPressed);
        manager.Close();
    }

    [Theory]
    [InlineData(Visibility.Hidden)]
    [InlineData(Visibility.Collapsed)]
    public void ShowingHiddenButtonDoesNotRestorePressOrClickOnRelease(Visibility visibility)
    {
        var root = new Canvas();
        var button = Place(root, new Button(), 0, 0, 40, 40);
        var manager = new UiManager();
        var screen = new UiScreen(root);
        var releaseCalls = 0;
        var pointerClickCalls = 0;
        var clickCalls = 0;
        button.PointerReleased += (_, _) => releaseCalls++;
        button.PointerClicked += (_, _) => pointerClickCalls++;
        button.Click += (_, _) => clickCalls++;
        manager.Open(screen);
        manager.PrepareFrame(new Size(100, 100), 0);
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        Assert.True(button.IsPressed);
        Assert.True(button.IsFocused);

        button.Visibility = visibility;
        button.Visibility = Visibility.Visible;

        Assert.False(button.IsPressed);
        Assert.False(button.IsFocused);
        Assert.False(button.IsHovered);
        manager.PrepareFrame(new Size(100, 100), 0);
        Assert.True(button.IsHovered);
        Assert.False(button.IsPressed);
        Assert.False(button.IsFocused);
        manager.ProcessPointerReleased(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        Assert.Equal(0, releaseCalls);
        Assert.Equal(0, pointerClickCalls);
        Assert.Equal(0, clickCalls);

        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        manager.ProcessPointerReleased(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        Assert.Equal(1, clickCalls);
        manager.Close();
    }

    [Theory]
    [InlineData(Visibility.Hidden)]
    [InlineData(Visibility.Collapsed)]
    public void HidingFocusedNodeInsideGotFocusDoesNotRestoreFocus(Visibility visibility)
    {
        var root = new Canvas();
        var node = Place(root, new TestNode { Focusable = true }, 0, 0, 40, 40);
        var manager = new UiManager();
        var screen = new UiScreen(root);
        var events = new List<string>();
        node.GotFocus += (_, _) =>
        {
            events.Add($"got:{node.IsFocused}");
            node.Visibility = visibility;
        };
        node.LostFocus += (_, _) => events.Add($"lost:{node.IsFocused}");
        manager.Open(screen);
        manager.PrepareFrame(new Size(100, 100), 0);

        Assert.True(node.Focus());

        Assert.Equal(visibility, node.Visibility);
        Assert.False(node.IsFocused);
        Assert.Null(screen.FocusedNode);
        Assert.Equal(["got:True", "lost:False"], events);
        manager.Close();
    }

    [Theory]
    [InlineData(Visibility.Hidden, false)]
    [InlineData(Visibility.Collapsed, false)]
    [InlineData(Visibility.Hidden, true)]
    [InlineData(Visibility.Collapsed, true)]
    public void VisibilityCleanupCallbacksDoNotRepeatLossOrRestoreInput(
        Visibility visibility,
        bool removeOnExit)
    {
        var root = new Canvas();
        var container = Place(root, new Canvas(), 0, 0, 40, 40);
        var child = Place(container, new TestControl { Focusable = true }, 0, 0, 40, 40);
        var manager = new UiManager();
        var screen = new UiScreen(root);
        var events = new List<string>();
        child.LostFocus += (_, _) =>
        {
            events.Add("lost");
            container.Visibility = Visibility.Visible;
        };
        child.PointerExited += (_, _) =>
        {
            events.Add("child-exit");
            if (removeOnExit)
                Assert.True(root.Children.Remove(container));
            else
                container.Visibility = visibility;
        };
        container.PointerExited += (_, _) => events.Add("parent-exit");
        manager.Open(screen);
        manager.PrepareFrame(new Size(100, 100), 0);
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        Assert.True(child.IsFocused);
        Assert.True(child.IsPressed);
        Assert.True(child.IsHovered);

        container.Visibility = visibility;

        Assert.Equal(["lost", "child-exit", "parent-exit"], events);
        Assert.Null(screen.FocusedNode);
        Assert.False(child.IsFocused);
        Assert.False(child.IsPressed);
        Assert.False(child.IsHovered);
        Assert.False(container.IsHovered);
        if (removeOnExit)
            Assert.Null(container.Screen);
        else
            Assert.Equal(visibility, container.Visibility);
        manager.Close();
    }

    [Fact]
    public void HoverStateCallbackClosingScreenPreventsLaterTrueProjection()
    {
        var manager = new UiManager();
        var root = new Canvas();
        var outer = Place(root, new Canvas(), 0, 0, 40, 40);
        var inner = Place(outer, new TestNode(), 0, 0, 40, 40);
        var screen = new UiScreen(root);
        var movedCalls = 0;
        using var subscription = outer.Subscribe(
            UiNode.IsHoveredProperty,
            (_, args) =>
            {
                if (args.NewValue)
                    manager.Close();
            });
        inner.PointerMoved += (_, _) => movedCalls++;
        manager.Open(screen);
        manager.PrepareFrame(new Size(100, 100), 0);

        manager.ProcessPointerMoved(new Point(5, 5));

        Assert.Null(manager.CurrentScreen);
        Assert.False(outer.IsHovered);
        Assert.False(inner.IsHovered);
        Assert.Equal(0, movedCalls);
    }

    [Fact]
    public void HoverStateFailureStopsRemainingProjectionAndEnteredEvents()
    {
        var root = new Canvas();
        var outer = Place(root, new Canvas(), 0, 0, 40, 40);
        var inner = Place(outer, new TestNode(), 0, 0, 40, 40);
        var manager = new UiManager();
        var screen = new UiScreen(root);
        var outerError = new InvalidOperationException("outer hover");
        var innerError = new InvalidOperationException("inner hover");
        using var outerSubscription = outer.Subscribe(
            UiNode.IsHoveredProperty,
            (_, args) =>
            {
                if (args.NewValue)
                    throw outerError;
            });
        using var innerSubscription = inner.Subscribe(
            UiNode.IsHoveredProperty,
            (_, args) =>
            {
                if (args.NewValue)
                    throw innerError;
            });
        var events = new List<string>();
        outer.PointerEntered += (_, _) => events.Add("outer-enter");
        inner.PointerEntered += (_, _) => events.Add("inner-enter");
        manager.Open(screen);
        manager.PrepareFrame(new Size(100, 100), 0);

        var actual = Assert.Throws<InvalidOperationException>(() =>
            manager.ProcessPointerMoved(new Point(5, 5)));

        Assert.Same(outerError, actual);
        Assert.True(outer.IsHovered);
        Assert.False(inner.IsHovered);
        Assert.Empty(events);
        manager.Close();
    }

    [Fact]
    public void OrdinaryNodeEnabledWritesUseOpenScreenOwnerThreadOnly()
    {
        var node = new TestNode();
        var screen = new UiScreen(node);
        screen.Open();

        var openError = RunOnBackgroundThread(() =>
            Record.Exception(() => node.IsEnabled = false));

        Assert.IsType<InvalidOperationException>(openError);
        Assert.True(node.IsEnabled);
        screen.Close();

        var closedError = RunOnBackgroundThread(() =>
            Record.Exception(() => node.IsEnabled = false));

        Assert.Null(closedError);
        Assert.False(node.IsEnabled);
    }

    private static void AssertProperty(
        UiProperty<bool> property,
        Type ownerType,
        string name,
        bool defaultValue,
        bool isReadOnly,
        UiPropertyInvalidation invalidation)
    {
        Assert.Equal(name, property.Name);
        Assert.Equal(ownerType, property.OwnerType);
        Assert.Equal(ownerType, property.TargetType);
        Assert.Equal(typeof(bool), property.ValueType);
        Assert.Equal(defaultValue, property.DefaultValue);
        Assert.Equal(isReadOnly, property.IsReadOnly);
        Assert.Equal(invalidation, property.Invalidation);
    }

    private static T Place<T>(Canvas parent, T child, double x, double y, double width, double height)
        where T : UiNode
    {
        child.Width = width;
        child.Height = height;
        Canvas.SetLeft(child, x);
        Canvas.SetTop(child, y);
        parent.Children.Add(child);
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

    private sealed class TestControl : Control
    {
    }

    private sealed class TestNode : UiNode
    {
    }
}