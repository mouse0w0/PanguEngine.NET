using System.Reflection;
using PanguEngine.Client.UI;
using PanguEngine.Input;

namespace PanguEngine.Tests.Client.UI;

public sealed class ControlTests
{
    [Fact]
    public void ControlHasRegionBaseAndOnlyReadOnlyPublicChildren()
    {
        var control = new TestControl();
        var first = new TestNode();
        var second = new TestNode();

        Assert.Equal(typeof(Region), typeof(Control).BaseType);
        Assert.False(typeof(Panel).IsAssignableFrom(typeof(Control)));
        Assert.Null(typeof(Control).GetProperty(
            nameof(Parent.Children),
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        Assert.IsAssignableFrom<IReadOnlyList<UiNode>>(control.Children);

        control.Add(first);
        control.Add(second);

        Assert.Equal([first, second], control.Children);
        Assert.Same(control, first.Parent);
        Assert.Same(control, second.Parent);

        Assert.True(control.Remove(first));
        Assert.Null(first.Parent);
        Assert.Equal([second], control.Children);

        control.Clear();
        Assert.Empty(control.Children);
        Assert.Null(second.Parent);
    }

    [Fact]
    public void ControlPressedPropertyExposesExpectedMetadataAndDefaults()
    {
        var control = new TestControl();

        AssertProperty(
            Control.IsPressedProperty,
            typeof(Control),
            nameof(Control.IsPressed),
            defaultValue: false,
            isReadOnly: true,
            UiPropertyInvalidation.Render);
        Assert.Null(typeof(Control).GetField(
            nameof(UiNode.IsEnabledProperty),
            BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly));
        Assert.Null(typeof(Control).GetField(
            nameof(UiNode.IsHoveredProperty),
            BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly));
        Assert.Null(typeof(Control).GetField(
            nameof(UiNode.IsFocusedProperty),
            BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly));
        Assert.Null(typeof(Control).GetProperty(
            nameof(UiNode.IsEnabled),
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        Assert.Null(typeof(Control).GetProperty(
            nameof(UiNode.IsHovered),
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        Assert.Null(typeof(Control).GetProperty(
            nameof(UiNode.IsFocused),
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));

        Assert.False(control.IsPressed);
    }

    [Fact]
    public void OnlyLeftButtonProjectsPressedAndReleaseClearsBeforeEvents()
    {
        var root = new Canvas();
        var control = Place(root, new TestControl(), 0, 0, 40, 40);
        var child = new TestNode();
        control.Add(child);
        var manager = new UiManager();
        var screen = new UiScreen(root);
        var states = new List<bool>();
        using var subscription = control.Subscribe(
            Control.IsPressedProperty,
            (_, args) => states.Add(args.NewValue));
        var leftEvents = new List<string>();
        var otherButtonStates = new List<bool>();
        child.PointerPressed += (_, args) =>
        {
            if (args.Button == MouseButton.Left)
                leftEvents.Add($"pressed:{control.IsPressed}");
            else
                otherButtonStates.Add(control.IsPressed);
        };
        child.PointerReleased += (_, args) =>
        {
            if (args.Button == MouseButton.Left)
                leftEvents.Add($"released:{control.IsPressed}");
            else
                otherButtonStates.Add(control.IsPressed);
        };
        child.PointerClicked += (_, args) =>
        {
            if (args.Button == MouseButton.Left)
                leftEvents.Add($"clicked:{control.IsPressed}");
        };
        manager.Open(screen);
        manager.Update(new Size(100, 100));

        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        Assert.True(control.IsPressed);
        manager.ProcessPointerMoved(new Point(80, 80));
        Assert.False(control.IsHovered);
        Assert.True(control.IsPressed);
        manager.ProcessPointerReleased(new Point(80, 80), MouseButton.Left, KeyModifiers.None);
        Assert.False(control.IsPressed);

        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        manager.ProcessPointerReleased(new Point(5, 5), MouseButton.Left, KeyModifiers.None);

        foreach (var button in new[] { MouseButton.Right, MouseButton.Middle, MouseButton.Button12 })
        {
            manager.ProcessPointerPressed(new Point(5, 5), button, KeyModifiers.None);
            manager.ProcessPointerReleased(new Point(5, 5), button, KeyModifiers.None);
        }

        Assert.Equal(
            [
                "pressed:True",
                "released:False",
                "pressed:True",
                "released:False",
                "clicked:False"
            ],
            leftEvents);
        Assert.Equal([true, false, true, false], states);
        Assert.Equal(6, otherButtonStates.Count);
        Assert.All(otherButtonStates, value => Assert.False(value));
        manager.Close();
    }

    [Fact]
    public void RepeatedLeftPressDiffsControlSnapshotsByReference()
    {
        var root = new Canvas();
        var outer = Place(root, new TestControl(), 0, 0, 100, 100);
        var content = new Canvas();
        outer.Add(content);
        var first = Place(content, new TestControl(), 0, 0, 20, 20);
        var second = Place(content, new TestControl(), 30, 0, 20, 20);
        var manager = new UiManager();
        var screen = new UiScreen(root);
        var outerStates = new List<bool>();
        using var subscription = outer.Subscribe(
            Control.IsPressedProperty,
            (_, args) => outerStates.Add(args.NewValue));
        manager.Open(screen);
        manager.Update(new Size(100, 100));

        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);

        Assert.True(outer.IsPressed);
        Assert.True(first.IsPressed);
        Assert.False(second.IsPressed);

        manager.ProcessPointerPressed(new Point(35, 5), MouseButton.Left, KeyModifiers.None);

        Assert.True(outer.IsPressed);
        Assert.False(first.IsPressed);
        Assert.True(second.IsPressed);
        Assert.Equal([true], outerStates);

        manager.ProcessPointerReleased(new Point(35, 5), MouseButton.Left, KeyModifiers.None);

        Assert.False(outer.IsPressed);
        Assert.False(second.IsPressed);
        Assert.Equal([true, false], outerStates);
        manager.Close();
    }

    [Fact]
    public void SameScreenReparentKeepsOriginalPressedControlUntilRelease()
    {
        var root = new Canvas();
        var oldControl = Place(root, new TestControl(), 0, 0, 40, 40);
        var newControl = Place(root, new TestControl(), 50, 0, 40, 40);
        var child = new TestNode();
        oldControl.Add(child);
        var manager = new UiManager();
        var screen = new UiScreen(root);
        manager.Open(screen);
        manager.Update(new Size(100, 100));
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);

        newControl.Add(child);

        Assert.True(oldControl.IsPressed);
        Assert.False(newControl.IsPressed);
        manager.Update(new Size(100, 100));
        Assert.True(oldControl.IsPressed);
        Assert.False(newControl.IsPressed);

        manager.ProcessPointerReleased(new Point(55, 5), MouseButton.Left, KeyModifiers.None);

        Assert.False(oldControl.IsPressed);
        Assert.False(newControl.IsPressed);
        manager.Close();
    }

    [Fact]
    public void ReparentIntoDisabledControlNormalizesStateOnNextUpdate()
    {
        var root = new Canvas();
        var oldControl = Place(root, new TestControl(), 0, 0, 40, 40);
        var disabledControl = Place(root, new TestControl { IsEnabled = false }, 50, 0, 40, 40);
        var child = new TestNode { Focusable = true };
        oldControl.Add(child);
        var manager = new UiManager();
        var screen = new UiScreen(root);
        var releaseCalls = 0;
        child.PointerReleased += (_, _) => releaseCalls++;
        manager.Open(screen);
        manager.Update(new Size(100, 100));
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        Assert.True(oldControl.IsPressed);
        Assert.Same(child, screen.FocusedNode);

        disabledControl.Add(child);

        Assert.True(oldControl.IsPressed);
        Assert.Same(child, screen.FocusedNode);

        manager.Update(new Size(100, 100));

        Assert.False(oldControl.IsPressed);
        Assert.Null(screen.FocusedNode);
        manager.ProcessPointerReleased(new Point(55, 5), MouseButton.Left, KeyModifiers.None);
        Assert.Equal(0, releaseCalls);
        manager.Close();
    }

    [Fact]
    public void RemovingPressedControlClearsItsSnapshotImmediately()
    {
        var root = new Canvas();
        var control = Place(root, new TestControl(), 0, 0, 40, 40);
        var child = new TestNode();
        control.Add(child);
        var manager = new UiManager();
        var screen = new UiScreen(root);
        var releaseCalls = 0;
        child.PointerReleased += (_, _) => releaseCalls++;
        manager.Open(screen);
        manager.Update(new Size(100, 100));
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);

        Assert.True(root.Children.Remove(control));

        Assert.False(control.IsPressed);
        manager.ProcessPointerReleased(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        Assert.Equal(0, releaseCalls);
        manager.Close();
    }

    [Fact]
    public void DisablingControlSynchronouslyClearsInputBeforeEnabledNotification()
    {
        var root = new Canvas();
        var background = Place(root, new TestNode(), 0, 0, 100, 100);
        var control = Place(root, new TestControl { Focusable = true }, 0, 0, 40, 40);
        var child = new TestNode();
        control.Add(child);
        var manager = new UiManager();
        var screen = new UiScreen(root);
        var backgroundEnters = 0;
        var childReleases = 0;
        var childClicks = 0;
        background.PointerEntered += (_, _) => backgroundEnters++;
        child.PointerReleased += (_, _) => childReleases++;
        child.PointerClicked += (_, _) => childClicks++;
        var enabledNotifications = 0;
        using var subscription = control.Subscribe(
            UiNode.IsEnabledProperty,
            (_, args) =>
            {
                if (args.NewValue)
                    return;

                enabledNotifications++;
                Assert.False(control.IsHovered);
                Assert.False(control.IsPressed);
                Assert.False(control.IsFocused);
                Assert.Null(screen.FocusedNode);
                Assert.Equal(0, backgroundEnters);
            });
        manager.Open(screen);
        manager.Update(new Size(100, 100));
        manager.ProcessPointerMoved(new Point(5, 5));
        Assert.True(control.Focus());
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        Assert.True(control.IsHovered);
        Assert.True(control.IsPressed);
        Assert.True(control.IsFocused);

        control.IsEnabled = false;

        Assert.Equal(1, enabledNotifications);
        Assert.False(control.IsHovered);
        Assert.False(control.IsPressed);
        Assert.False(control.IsFocused);
        Assert.Null(screen.FocusedNode);
        Assert.Equal(0, backgroundEnters);
        manager.ProcessPointerReleased(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        Assert.Equal(0, childReleases);
        Assert.Equal(0, childClicks);

        manager.Update(new Size(100, 100));
        Assert.Equal(1, backgroundEnters);

        control.IsEnabled = true;
        Assert.False(control.IsHovered);
        Assert.False(control.IsPressed);
        Assert.False(control.IsFocused);
        Assert.Null(screen.FocusedNode);

        manager.Update(new Size(100, 100));
        Assert.True(control.IsHovered);
        manager.Close();
    }

    [Fact]
    public void PressedClearErrorsCompleteSnapshotAndSuppressReleaseAndClick()
    {
        var root = new Canvas();
        var outer = Place(root, new TestControl(), 0, 0, 40, 40);
        var inner = new TestControl();
        outer.Add(inner);
        var manager = new UiManager();
        var screen = new UiScreen(root);
        var innerError = new InvalidOperationException("inner pressed");
        var outerError = new InvalidOperationException("outer pressed");
        using var innerSubscription = inner.Subscribe(
            Control.IsPressedProperty,
            (_, args) =>
            {
                if (!args.NewValue)
                    throw innerError;
            });
        using var outerSubscription = outer.Subscribe(
            Control.IsPressedProperty,
            (_, args) =>
            {
                if (!args.NewValue)
                    throw outerError;
            });
        var releaseCalls = 0;
        var clickCalls = 0;
        inner.PointerReleased += (_, _) => releaseCalls++;
        inner.PointerClicked += (_, _) => clickCalls++;
        manager.Open(screen);
        manager.Update(new Size(100, 100));
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);

        var aggregate = Assert.Throws<AggregateException>(() =>
            manager.ProcessPointerReleased(new Point(5, 5), MouseButton.Left, KeyModifiers.None));

        Assert.Equal([innerError, outerError], aggregate.InnerExceptions);
        Assert.False(inner.IsPressed);
        Assert.False(outer.IsPressed);
        Assert.Equal(0, releaseCalls);
        Assert.Equal(0, clickCalls);
        manager.ProcessPointerReleased(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        Assert.Equal(0, releaseCalls);
        manager.Close();
    }

    [Fact]
    public void CleanupCompletesAllStatesAndEventsBeforeAggregatingErrors()
    {
        var root = new Canvas();
        var outer = Place(root, new TestControl(), 0, 0, 40, 40);
        var inner = new TestControl { Focusable = true };
        outer.Add(inner);
        var manager = new UiManager();
        var screen = new UiScreen(root);
        var focusedError = new InvalidOperationException("focused");
        var innerPressedError = new InvalidOperationException("inner pressed");
        var outerPressedError = new InvalidOperationException("outer pressed");
        var innerHoveredError = new InvalidOperationException("inner hovered");
        var outerHoveredError = new InvalidOperationException("outer hovered");
        var lostError = new InvalidOperationException("lost");
        var exitError = new InvalidOperationException("exit");
        var events = new List<string>();
        _ = inner.Subscribe(UiNode.IsFocusedProperty, (_, args) =>
        {
            if (!args.NewValue)
                throw focusedError;
        });
        _ = inner.Subscribe(Control.IsPressedProperty, (_, args) =>
        {
            if (!args.NewValue)
                throw innerPressedError;
        });
        _ = outer.Subscribe(Control.IsPressedProperty, (_, args) =>
        {
            if (!args.NewValue)
                throw outerPressedError;
        });
        _ = inner.Subscribe(UiNode.IsHoveredProperty, (_, args) =>
        {
            if (!args.NewValue)
                throw innerHoveredError;
        });
        _ = outer.Subscribe(UiNode.IsHoveredProperty, (_, args) =>
        {
            if (!args.NewValue)
                throw outerHoveredError;
        });
        inner.LostFocus += (_, _) =>
        {
            events.Add("lost");
            throw lostError;
        };
        inner.PointerExited += (_, _) =>
        {
            events.Add("inner-exit");
            throw exitError;
        };
        outer.PointerExited += (_, _) => events.Add("outer-exit");
        manager.Open(screen);
        manager.Update(new Size(100, 100));
        manager.ProcessPointerMoved(new Point(5, 5));
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        Assert.True(inner.IsFocused);
        Assert.True(inner.IsPressed);
        Assert.True(outer.IsPressed);

        var aggregate = Assert.Throws<AggregateException>(() => root.Children.Remove(outer));

        Assert.Equal(
            [
                focusedError,
                innerPressedError,
                outerPressedError,
                innerHoveredError,
                outerHoveredError,
                lostError,
                exitError
            ],
            aggregate.InnerExceptions);
        Assert.False(inner.IsFocused);
        Assert.False(inner.IsPressed);
        Assert.False(outer.IsPressed);
        Assert.False(inner.IsHovered);
        Assert.False(outer.IsHovered);
        Assert.Null(screen.FocusedNode);
        Assert.Equal(["lost", "inner-exit", "outer-exit"], events);
        Assert.Null(outer.Screen);
        manager.Close();
    }

    [Fact]
    public void DisableCleanupAndEnabledNotificationErrorsAggregateInOrder()
    {
        var root = new Canvas();
        var control = Place(root, new TestControl { Focusable = true }, 0, 0, 40, 40);
        var manager = new UiManager();
        var screen = new UiScreen(root);
        var cleanupError = new InvalidOperationException("cleanup");
        var enabledError = new InvalidOperationException("enabled");
        _ = control.Subscribe(Control.IsPressedProperty, (_, args) =>
        {
            if (!args.NewValue)
                throw cleanupError;
        });
        control.PropertyChanged += (_, args) =>
        {
            if (ReferenceEquals(args.Property, UiNode.IsEnabledProperty))
                throw enabledError;
        };
        manager.Open(screen);
        manager.Update(new Size(100, 100));
        manager.ProcessPointerMoved(new Point(5, 5));
        Assert.True(control.Focus());
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);

        var aggregate = Assert.Throws<AggregateException>(() => control.IsEnabled = false);

        Assert.Equal([cleanupError, enabledError], aggregate.InnerExceptions);
        Assert.False(control.IsEnabled);
        Assert.False(control.IsHovered);
        Assert.False(control.IsPressed);
        Assert.False(control.IsFocused);
        Assert.Null(screen.FocusedNode);
        manager.Close();
    }

    [Fact]
    public void CloseAndReopenDoNotRestoreControlInteractionState()
    {
        var manager = new UiManager();
        var root = new Canvas();
        var control = Place(root, new TestControl { Focusable = true }, 0, 0, 40, 40);
        var screen = new UiScreen(root);
        manager.Open(screen);
        manager.Update(new Size(100, 100));
        manager.ProcessPointerMoved(new Point(5, 5));
        Assert.True(control.Focus());
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);

        manager.Close();

        Assert.Same(screen, control.Screen);
        Assert.False(control.IsHovered);
        Assert.False(control.IsPressed);
        Assert.False(control.IsFocused);
        Assert.Null(screen.FocusedNode);

        manager.Open(screen);
        manager.Update(new Size(100, 100));

        Assert.False(control.IsHovered);
        Assert.False(control.IsPressed);
        Assert.False(control.IsFocused);
        Assert.Null(screen.FocusedNode);
        manager.Close();
    }

    [Fact]
    public void RootReplacementAndCrossScreenMoveClearControlInteractionState()
    {
        var firstManager = new UiManager();
        var firstRoot = new Canvas();
        var control = Place(firstRoot, new TestControl { Focusable = true }, 0, 0, 40, 40);
        var firstScreen = new UiScreen(firstRoot);
        firstManager.Open(firstScreen);
        firstManager.Update(new Size(100, 100));
        firstManager.ProcessPointerMoved(new Point(5, 5));
        Assert.True(control.Focus());
        firstManager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);

        firstScreen.Root = new Canvas();

        Assert.False(control.IsHovered);
        Assert.False(control.IsPressed);
        Assert.False(control.IsFocused);
        Assert.Null(firstScreen.FocusedNode);

        var secondManager = new UiManager();
        var secondRoot = new Canvas();
        var secondScreen = new UiScreen(secondRoot);
        secondManager.Open(secondScreen);
        secondManager.Update(new Size(100, 100));
        firstScreen.Root = firstRoot;
        firstManager.Update(new Size(100, 100));
        firstManager.ProcessPointerMoved(new Point(5, 5));
        Assert.True(control.Focus());
        firstManager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);

        secondRoot.Children.Add(control);

        Assert.False(control.IsHovered);
        Assert.False(control.IsPressed);
        Assert.False(control.IsFocused);
        Assert.Null(firstScreen.FocusedNode);
        Assert.Same(secondScreen, control.Screen);
        secondManager.Close();
        firstManager.Close();
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

    private sealed class TestControl : Control
    {
        internal void Add(UiNode child) => AddChild(child);
        internal bool Remove(UiNode child) => RemoveChild(child);
        internal void Clear() => ClearChildren();
    }

    private sealed class TestNode : UiNode
    {
    }
}
