using System.Reflection;
using PanguEngine.Client.UI;
using PanguEngine.ComponentModel;
using PanguEngine.Input;
using PanguEngine.UI;
using PanguEngine.UI.Controls;
using PanguEngine.UI.Styling;

namespace PanguEngine.Tests.UI;

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
            nameof(Container.Children),
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        Assert.IsAssignableFrom<IReadOnlyList<UIElement>>(control.ReadOnlyChildren);

        control.Add(first);
        control.Add(second);

        Assert.Equal([first, second], control.ReadOnlyChildren);
        Assert.Same(control, first.Parent);
        Assert.Same(control, second.Parent);

        Assert.True(control.Remove(first));
        Assert.Null(first.Parent);
        Assert.Equal([second], control.ReadOnlyChildren);

        control.Clear();
        Assert.Empty(control.ReadOnlyChildren);
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
            isReadOnly: true);
        Assert.Null(typeof(Control).GetField(
            nameof(UIElement.IsEnabledProperty),
            BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly));
        Assert.Null(typeof(Control).GetField(
            nameof(UIElement.IsHoveredProperty),
            BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly));
        Assert.Null(typeof(Control).GetField(
            nameof(UIElement.IsFocusedProperty),
            BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly));
        Assert.Null(typeof(Control).GetProperty(
            nameof(UIElement.IsEnabled),
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        Assert.Null(typeof(Control).GetProperty(
            nameof(UIElement.IsHovered),
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        Assert.Null(typeof(Control).GetProperty(
            nameof(UIElement.IsFocused),
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
        var manager = new UIManager();
        var scene = new GameUIScene(root);
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
        manager.Open(scene);
        manager.UpdateFrame(new Size(100, 100), 0);

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
        var manager = new UIManager();
        var scene = new GameUIScene(root);
        var outerStates = new List<bool>();
        using var subscription = outer.Subscribe(
            Control.IsPressedProperty,
            (_, args) => outerStates.Add(args.NewValue));
        manager.Open(scene);
        manager.UpdateFrame(new Size(100, 100), 0);

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
    public void SameSceneReparentClearsOriginalPressedControl()
    {
        var root = new Canvas();
        var oldControl = Place(root, new TestControl(), 0, 0, 40, 40);
        var newControl = Place(root, new TestControl(), 50, 0, 40, 40);
        var child = new TestNode();
        oldControl.Add(child);
        var manager = new UIManager();
        var scene = new GameUIScene(root);
        manager.Open(scene);
        manager.UpdateFrame(new Size(100, 100), 0);
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);

        newControl.Add(child);

        Assert.False(oldControl.IsPressed);
        Assert.False(newControl.IsPressed);
        manager.UpdateFrame(new Size(100, 100), 0);
        Assert.False(oldControl.IsPressed);
        Assert.False(newControl.IsPressed);

        manager.ProcessPointerReleased(new Point(55, 5), MouseButton.Left, KeyModifiers.None);

        Assert.False(oldControl.IsPressed);
        Assert.False(newControl.IsPressed);
        manager.Close();
    }

    [Fact]
    public void ReparentIntoDisabledControlClearsStateDuringRemoval()
    {
        var root = new Canvas();
        var oldControl = Place(root, new TestControl(), 0, 0, 40, 40);
        var disabledControl = Place(root, new TestControl { IsEnabled = false }, 50, 0, 40, 40);
        var child = new TestNode { Focusable = true };
        oldControl.Add(child);
        var manager = new UIManager();
        var scene = new GameUIScene(root);
        var releaseCalls = 0;
        child.PointerReleased += (_, _) => releaseCalls++;
        manager.Open(scene);
        manager.UpdateFrame(new Size(100, 100), 0);
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        Assert.True(oldControl.IsPressed);
        Assert.Same(child, scene.FocusedNode);

        disabledControl.Add(child);

        Assert.False(oldControl.IsPressed);
        Assert.Null(scene.FocusedNode);

        manager.UpdateFrame(new Size(100, 100), 0);

        Assert.False(oldControl.IsPressed);
        Assert.Null(scene.FocusedNode);
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
        var manager = new UIManager();
        var scene = new GameUIScene(root);
        var releaseCalls = 0;
        child.PointerReleased += (_, _) => releaseCalls++;
        manager.Open(scene);
        manager.UpdateFrame(new Size(100, 100), 0);
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
        var manager = new UIManager();
        var scene = new GameUIScene(root);
        var backgroundEnters = 0;
        var childReleases = 0;
        var childClicks = 0;
        background.PointerEntered += (_, _) => backgroundEnters++;
        child.PointerReleased += (_, _) => childReleases++;
        child.PointerClicked += (_, _) => childClicks++;
        var enabledNotifications = 0;
        using var subscription = control.Subscribe(
            UIElement.IsEnabledProperty,
            (_, args) =>
            {
                if (args.NewValue)
                    return;

                enabledNotifications++;
                Assert.False(control.IsHovered);
                Assert.False(control.IsPressed);
                Assert.False(control.IsFocused);
                Assert.Null(scene.FocusedNode);
                Assert.Equal(0, backgroundEnters);
            });
        manager.Open(scene);
        manager.UpdateFrame(new Size(100, 100), 0);
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
        Assert.Null(scene.FocusedNode);
        Assert.Equal(0, backgroundEnters);
        manager.ProcessPointerReleased(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        Assert.Equal(0, childReleases);
        Assert.Equal(0, childClicks);

        manager.UpdateFrame(new Size(100, 100), 0);
        Assert.Equal(1, backgroundEnters);

        control.IsEnabled = true;
        Assert.False(control.IsHovered);
        Assert.False(control.IsPressed);
        Assert.False(control.IsFocused);
        Assert.Null(scene.FocusedNode);

        manager.UpdateFrame(new Size(100, 100), 0);
        Assert.True(control.IsHovered);
        manager.Close();
    }

    [Fact]
    public void PressedClearFailureStopsRemainingStatesAndSuppressesReleaseAndClick()
    {
        var root = new Canvas();
        var outer = Place(root, new TestControl(), 0, 0, 40, 40);
        var inner = new TestControl();
        outer.Add(inner);
        var manager = new UIManager();
        var scene = new GameUIScene(root);
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
        manager.Open(scene);
        manager.UpdateFrame(new Size(100, 100), 0);
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);

        var actual = Assert.Throws<InvalidOperationException>(() =>
            manager.ProcessPointerReleased(new Point(5, 5), MouseButton.Left, KeyModifiers.None));

        Assert.Same(innerError, actual);
        Assert.False(inner.IsPressed);
        Assert.True(outer.IsPressed);
        Assert.Equal(0, releaseCalls);
        Assert.Equal(0, clickCalls);
        manager.ProcessPointerReleased(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        Assert.Equal(0, releaseCalls);
        manager.Close();
    }

    [Fact]
    public void CleanupFailureStopsRemainingStatesAndEvents()
    {
        var root = new Canvas();
        var outer = Place(root, new TestControl(), 0, 0, 40, 40);
        var inner = new TestControl { Focusable = true };
        outer.Add(inner);
        var manager = new UIManager();
        var scene = new GameUIScene(root);
        var focusedError = new InvalidOperationException("focused");
        var innerPressedError = new InvalidOperationException("inner pressed");
        var outerPressedError = new InvalidOperationException("outer pressed");
        var innerHoveredError = new InvalidOperationException("inner hovered");
        var outerHoveredError = new InvalidOperationException("outer hovered");
        var lostError = new InvalidOperationException("lost");
        var exitError = new InvalidOperationException("exit");
        var events = new List<string>();
        _ = inner.Subscribe(UIElement.IsFocusedProperty, (_, args) =>
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
        _ = inner.Subscribe(UIElement.IsHoveredProperty, (_, args) =>
        {
            if (!args.NewValue)
                throw innerHoveredError;
        });
        _ = outer.Subscribe(UIElement.IsHoveredProperty, (_, args) =>
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
        manager.Open(scene);
        manager.UpdateFrame(new Size(100, 100), 0);
        manager.ProcessPointerMoved(new Point(5, 5));
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        Assert.True(inner.IsFocused);
        Assert.True(inner.IsPressed);
        Assert.True(outer.IsPressed);

        var actual = Assert.Throws<InvalidOperationException>(() => root.Children.Remove(outer));

        Assert.Same(focusedError, actual);
        Assert.False(inner.IsFocused);
        Assert.True(inner.IsPressed);
        Assert.True(outer.IsPressed);
        Assert.True(inner.IsHovered);
        Assert.True(outer.IsHovered);
        Assert.Null(scene.FocusedNode);
        Assert.Empty(events);
        Assert.Null(outer.Scene);
        manager.Close();
    }

    [Fact]
    public void DisableCleanupFailureStopsEnabledNotifications()
    {
        var root = new Canvas();
        var control = Place(root, new TestControl { Focusable = true }, 0, 0, 40, 40);
        var manager = new UIManager();
        var scene = new GameUIScene(root);
        var cleanupError = new InvalidOperationException("cleanup");
        var enabledError = new InvalidOperationException("enabled");
        _ = control.Subscribe(Control.IsPressedProperty, (_, args) =>
        {
            if (!args.NewValue)
                throw cleanupError;
        });
        control.PropertyChanged += (_, args) =>
        {
            if (ReferenceEquals(args.Property, UIElement.IsEnabledProperty))
                throw enabledError;
        };
        manager.Open(scene);
        manager.UpdateFrame(new Size(100, 100), 0);
        manager.ProcessPointerMoved(new Point(5, 5));
        Assert.True(control.Focus());
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);

        var actual = Assert.Throws<InvalidOperationException>(() => control.IsEnabled = false);

        Assert.Same(cleanupError, actual);
        Assert.False(control.IsEnabled);
        Assert.True(control.IsHovered);
        Assert.False(control.IsPressed);
        Assert.False(control.IsFocused);
        Assert.Null(scene.FocusedNode);
        manager.Close();
    }

    [Fact]
    public void DisableCleanupFailureStopsInternalCallbackAndEnabledNotifications()
    {
        var root = new Canvas();
        var control = Place(root, new TestControl { Focusable = true }, 0, 0, 40, 40);
        var manager = new UIManager();
        var scene = new GameUIScene(root);
        scene.SetStyleSheets([
            new StyleSheet([
                new StyleRule(StyleSelector.For<TestControl>([StyleSelectorCondition.Pseudo(PseudoClass.Disabled)]), [
                    StyleSetter.Create(UIElement.OpacityProperty, 0.5)
                ])
            ])
        ]);
        var cleanupError = new InvalidOperationException("cleanup");
        var internalError = new InvalidOperationException("internal");
        var enabledNotifications = 0;
        var opacityNotifications = 0;
        using var pressedSubscription = control.Subscribe(Control.IsPressedProperty, (_, args) =>
        {
            if (!args.NewValue)
                throw cleanupError;
        });
        control.PropertyChanged += (_, args) =>
        {
            if (ReferenceEquals(args.Property, UIElement.IsEnabledProperty))
                enabledNotifications++;
            if (ReferenceEquals(args.Property, UIElement.OpacityProperty))
            {
                opacityNotifications++;
                throw internalError;
            }
        };
        manager.Open(scene);
        manager.UpdateFrame(new Size(100, 100), 0);
        manager.ProcessPointerMoved(new Point(5, 5));
        Assert.True(control.Focus());
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);

        var actual = Assert.Throws<InvalidOperationException>(() => control.IsEnabled = false);

        Assert.Same(cleanupError, actual);
        Assert.Equal(0, enabledNotifications);
        Assert.Equal(0, opacityNotifications);
        Assert.False(control.IsEnabled);
        Assert.True(control.HasPseudoClass(PseudoClass.Disabled));
        Assert.Equal(1, control.Opacity);
        manager.Close();
    }

    [Fact]
    public void CloseAndReopenDoNotRestoreControlInteractionState()
    {
        var manager = new UIManager();
        var root = new Canvas();
        var control = Place(root, new TestControl { Focusable = true }, 0, 0, 40, 40);
        var scene = new GameUIScene(root);
        manager.Open(scene);
        manager.UpdateFrame(new Size(100, 100), 0);
        manager.ProcessPointerMoved(new Point(5, 5));
        Assert.True(control.Focus());
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);

        manager.Close();

        Assert.Same(scene, control.Scene);
        Assert.False(control.IsHovered);
        Assert.False(control.IsPressed);
        Assert.False(control.IsFocused);
        Assert.Null(scene.FocusedNode);

        manager.Open(scene);
        manager.UpdateFrame(new Size(100, 100), 0);

        Assert.False(control.IsHovered);
        Assert.False(control.IsPressed);
        Assert.False(control.IsFocused);
        Assert.Null(scene.FocusedNode);
        manager.Close();
    }

    [Fact]
    public void RootReplacementAndCrossSceneMoveClearControlInteractionState()
    {
        var firstManager = new UIManager();
        var firstRoot = new Canvas();
        var control = Place(firstRoot, new TestControl { Focusable = true }, 0, 0, 40, 40);
        var firstScene = new GameUIScene(firstRoot);
        firstManager.Open(firstScene);
        firstManager.UpdateFrame(new Size(100, 100), 0);
        firstManager.ProcessPointerMoved(new Point(5, 5));
        Assert.True(control.Focus());
        firstManager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);

        firstScene.Root = new Canvas();

        Assert.False(control.IsHovered);
        Assert.False(control.IsPressed);
        Assert.False(control.IsFocused);
        Assert.Null(firstScene.FocusedNode);

        var secondManager = new UIManager();
        var secondRoot = new Canvas();
        var secondScene = new GameUIScene(secondRoot);
        secondManager.Open(secondScene);
        secondManager.UpdateFrame(new Size(100, 100), 0);
        firstScene.Root = firstRoot;
        firstManager.UpdateFrame(new Size(100, 100), 0);
        firstManager.ProcessPointerMoved(new Point(5, 5));
        Assert.True(control.Focus());
        firstManager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);

        secondRoot.Children.Add(control);

        Assert.False(control.IsHovered);
        Assert.False(control.IsPressed);
        Assert.False(control.IsFocused);
        Assert.Null(firstScene.FocusedNode);
        Assert.Same(secondScene, control.Scene);
        secondManager.Close();
        firstManager.Close();
    }

    private static void AssertProperty(
        Property<bool> property,
        Type ownerType,
        string name,
        bool defaultValue,
        bool isReadOnly)
    {
        Assert.Equal(name, property.Name);
        Assert.Equal(ownerType, property.OwnerType);
        Assert.Equal(ownerType, property.TargetType);
        Assert.Equal(typeof(bool), property.ValueType);
        Assert.Equal(defaultValue, property.DefaultValue);
        Assert.Equal(isReadOnly, property.IsReadOnly);
    }

    private static T Place<T>(Canvas parent, T child, double x, double y, double width, double height)
        where T : UIElement
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
        internal void Add(UIElement child) => Children.Add(child);
        internal bool Remove(UIElement child) => Children.Remove(child);
        internal void Clear() => Children.Clear();
    }

    private sealed class TestNode : UIElement
    {
    }
}
