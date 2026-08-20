using PanguEngine.Client.Game;
using PanguEngine.Client.UI;
using PanguEngine.Input;
using PanguEngine.Tests.Windowing;
using PanguEngine.Windowing;
using Silk.NET.Maths;

namespace PanguEngine.Tests.Client.Game;

public sealed class ClientInputBridgeTests
{
    [Fact]
    public void WithoutScreenRoutesGameEventsOnly()
    {
        var window = CreateWindow();
        var input = new ClientInputState(state => window.CursorState = state);
        var manager = new UiManager();
        var bridge = new ClientInputBridge(window, manager, input, static () => false);
        var deltas = new List<Vector2D<float>>();
        input.MouseDelta += deltas.Add;

        window.RaiseKeyDown(new KeyEventArgs(Key.W, KeyAction.Press, KeyModifiers.Shift));
        window.RaiseMouseDown(new MouseClickEventArgs(MouseButton.Left, 10, 20));
        window.RaiseMouseMove(new MouseMoveEventArgs(13, 18));
        window.RaiseMouseDown(new MouseClickEventArgs(MouseButton.Left, 13, 18));
        window.RaiseMouseDown(new MouseClickEventArgs(MouseButton.Right, 13, 18));
        window.RaiseMouseUp(new MouseClickEventArgs(MouseButton.Left, 13, 18));
        window.RaiseScroll(new ScrollEventArgs(1, -2));

        Assert.True(input.IsKeyDown(Key.W));
        Assert.Equal([new Vector2D<float>(3, -2)], deltas);
        Assert.True(input.ConsumeLeftClickRequest());
        Assert.True(input.ConsumeRightClickRequest());

        window.RaiseKeyUp(new KeyEventArgs(Key.W, KeyAction.Release, KeyModifiers.None));
        Assert.False(input.IsKeyDown(Key.W));
        bridge.Destroy();
        input.Destroy();
        manager.Destroy();
    }

    [Fact]
    public void WithScreenRoutesUiEventsOnlyUsingFramebufferCoordinates()
    {
        var window = CreateWindow();
        window.Size = new Vector2D<int>(100, 50);
        window.SetFramebufferSize(new Vector2D<int>(200, 150));
        var input = new ClientInputState(state => window.CursorState = state);
        var manager = new UiManager();
        var leaf = new TestNode { Focusable = true };
        OpenScreen(manager, leaf, new Size(200, 150));
        var bridge = new ClientInputBridge(window, manager, input, static () => false);
        Point? movedPosition = null;
        UiPointerButtonEventArgs? pressed = null;
        UiPointerButtonEventArgs? released = null;
        UiPointerWheelEventArgs? wheel = null;
        UiKeyEventArgs? keyDown = null;
        leaf.PointerMoved += (_, args) => movedPosition = args.ScreenPosition;
        leaf.PointerPressed += (_, args) => pressed = args;
        leaf.PointerReleased += (_, args) => released = args;
        leaf.PointerWheel += (_, args) => wheel = args;
        leaf.KeyDown += (_, args) => keyDown = args;

        window.RaiseMouseMove(new MouseMoveEventArgs(25, 10));
        window.SetKeyModifiers(KeyModifiers.Control | KeyModifiers.Shift);
        window.RaiseMouseDown(new MouseClickEventArgs(MouseButton.Left, 25, 10));
        window.RaiseMouseUp(new MouseClickEventArgs(MouseButton.Left, 25, 10));
        window.RaiseKeyDown(new KeyEventArgs(Key.A, KeyAction.Press, KeyModifiers.Alt));
        window.SetMousePosition(new Vector2D<float>(25, 10));
        window.RaiseScroll(new ScrollEventArgs(1.5f, -2.5f));

        var pressedArgs = Assert.IsType<UiPointerButtonEventArgs>(pressed);
        var releasedArgs = Assert.IsType<UiPointerButtonEventArgs>(released);
        var keyDownArgs = Assert.IsType<UiKeyEventArgs>(keyDown);
        var wheelArgs = Assert.IsType<UiPointerWheelEventArgs>(wheel);
        Assert.Equal(new Point(50, 30), movedPosition!.Value);
        Assert.Equal(new Point(50, 30), pressedArgs.ScreenPosition);
        Assert.Equal(MouseButton.Left, pressedArgs.Button);
        Assert.Equal(KeyModifiers.Control | KeyModifiers.Shift, pressedArgs.Modifiers);
        Assert.Equal(new Point(50, 30), releasedArgs.ScreenPosition);
        Assert.Equal(KeyModifiers.Control | KeyModifiers.Shift, releasedArgs.Modifiers);
        Assert.Equal(Key.A, keyDownArgs.Key);
        Assert.Equal(KeyModifiers.Alt, keyDownArgs.Modifiers);
        Assert.Equal(new Point(50, 30), wheelArgs.ScreenPosition);
        Assert.Equal(1.5, wheelArgs.DeltaX);
        Assert.Equal(-2.5, wheelArgs.DeltaY);
        Assert.False(input.IsKeyDown(Key.A));
        Assert.False(input.IsMouseCaptured);
        Assert.False(input.ConsumeLeftClickRequest());
        bridge.Destroy();
        input.Destroy();
        manager.Destroy();
    }

    [Fact]
    public void ZeroWindowOrFramebufferAxisSkipsPositionedUiEvents()
    {
        var window = CreateWindow();
        var input = new ClientInputState(state => window.CursorState = state);
        var manager = new UiManager();
        var leaf = new TestNode();
        OpenScreen(manager, leaf, new Size(100, 100));
        var bridge = new ClientInputBridge(window, manager, input, static () => false);
        var routed = 0;
        leaf.PointerMoved += (_, _) => routed++;
        leaf.PointerPressed += (_, _) => routed++;
        leaf.PointerReleased += (_, _) => routed++;
        leaf.PointerWheel += (_, _) => routed++;

        window.Size = new Vector2D<int>(0, 100);
        window.RaiseMouseMove(new MouseMoveEventArgs(5, 5));
        window.RaiseMouseDown(new MouseClickEventArgs(MouseButton.Left, 5, 5));
        window.RaiseMouseUp(new MouseClickEventArgs(MouseButton.Left, 5, 5));
        window.RaiseScroll(new ScrollEventArgs(0, 1));
        window.Size = new Vector2D<int>(100, 100);
        window.SetFramebufferSize(new Vector2D<int>(100, 0));
        window.RaiseMouseMove(new MouseMoveEventArgs(5, 5));
        window.RaiseMouseDown(new MouseClickEventArgs(MouseButton.Left, 5, 5));
        window.RaiseMouseUp(new MouseClickEventArgs(MouseButton.Left, 5, 5));
        window.RaiseScroll(new ScrollEventArgs(0, 1));

        Assert.Equal(0, routed);
        bridge.Destroy();
        input.Destroy();
        manager.Destroy();
    }

    [Fact]
    public void ClosingScreenDuringPointerPressDoesNotRouteSameEventToGame()
    {
        var window = CreateWindow();
        var input = new ClientInputState(state => window.CursorState = state);
        var manager = new UiManager();
        var leaf = new TestNode();
        OpenScreen(manager, leaf, new Size(100, 100));
        var bridge = new ClientInputBridge(window, manager, input, static () => false);
        leaf.PointerPressed += (_, _) => manager.Close();

        window.RaiseMouseDown(new MouseClickEventArgs(MouseButton.Left, 5, 5));

        Assert.Null(manager.CurrentScreen);
        Assert.False(input.IsMouseCaptured);
        Assert.False(input.ConsumeLeftClickRequest());
        bridge.Destroy();
        input.Destroy();
        manager.Destroy();
    }

    [Fact]
    public void ButtonClickCloseDoesNotCreateGameClick()
    {
        var window = CreateWindow();
        var input = new ClientInputState(state => window.CursorState = state);
        input.CaptureMouse();
        var manager = new UiManager();
        var bridge = new ClientInputBridge(window, manager, input, static () => false);
        var button = new Button { Width = 40, Height = 30 };
        button.Click += (_, _) => manager.Close();
        OpenScreen(manager, button, new Size(100, 100));

        window.RaiseMouseDown(new MouseClickEventArgs(MouseButton.Left, 5, 5));
        window.RaiseMouseUp(new MouseClickEventArgs(MouseButton.Left, 5, 5));

        Assert.Null(manager.CurrentScreen);
        Assert.True(input.IsMouseCaptured);
        Assert.False(input.ConsumeLeftClickRequest());
        bridge.Destroy();
        input.Destroy();
        manager.Destroy();
    }

    [Fact]
    public void OpeningUiClearsGameStateAndFinalCloseRestoresPriorCapture()
    {
        var window = CreateWindow();
        var input = new ClientInputState(state => window.CursorState = state);
        var manager = new UiManager();
        var bridge = new ClientInputBridge(window, manager, input, static () => false);
        input.HandleKeyDown(new KeyEventArgs(Key.W, KeyAction.Press, KeyModifiers.None));
        input.HandleMouseDown(new MouseClickEventArgs(MouseButton.Left, 5, 5));
        input.HandleMouseDown(new MouseClickEventArgs(MouseButton.Left, 5, 5));
        input.HandleMouseDown(new MouseClickEventArgs(MouseButton.Right, 5, 5));
        var first = new UiScreen(new TestNode());
        var second = new UiScreen(new TestNode());

        manager.Open(first);
        manager.Open(second);

        Assert.False(input.IsKeyDown(Key.W));
        Assert.False(input.IsMouseCaptured);
        Assert.False(input.ConsumeLeftClickRequest());
        Assert.False(input.ConsumeRightClickRequest());

        manager.Close();

        Assert.True(input.IsMouseCaptured);
        bridge.Destroy();
        input.Destroy();
        manager.Destroy();
    }

    [Fact]
    public void ClosingUiDoesNotCaptureWhenPreviouslyUncapturedOrUnfocused()
    {
        var window = CreateWindow();
        var input = new ClientInputState(state => window.CursorState = state);
        var manager = new UiManager();
        var bridge = new ClientInputBridge(window, manager, input, static () => false);

        manager.Open(new UiScreen(new TestNode()));
        manager.Close();
        Assert.False(input.IsMouseCaptured);

        input.CaptureMouse();
        manager.Open(new UiScreen(new TestNode()));
        window.RaiseFocusChanged(false);
        manager.Close();
        window.RaiseFocusChanged(true);

        Assert.False(input.IsMouseCaptured);
        Assert.Equal(CursorState.Normal, window.CursorState);
        bridge.Destroy();
        input.Destroy();
        manager.Destroy();
    }

    [Fact]
    public void ExistingScreenSuspendsInputWhenBridgeIsConstructed()
    {
        var window = CreateWindow();
        var input = new ClientInputState(state => window.CursorState = state);
        input.CaptureMouse();
        input.HandleKeyDown(new KeyEventArgs(Key.W, KeyAction.Press, KeyModifiers.None));
        var manager = new UiManager();
        manager.Open(new UiScreen(new TestNode()));

        var bridge = new ClientInputBridge(window, manager, input, static () => false);

        Assert.False(input.IsMouseCaptured);
        Assert.False(input.IsKeyDown(Key.W));
        manager.Close();
        Assert.True(input.IsMouseCaptured);
        bridge.Destroy();
        input.Destroy();
        manager.Destroy();
    }

    [Fact]
    public void FocusLossClearsUiAndGameState()
    {
        var window = CreateWindow();
        var input = new ClientInputState(state => window.CursorState = state);
        var manager = new UiManager();
        var control = new TestControl { Focusable = true };
        OpenScreen(manager, control, new Size(100, 100));
        var bridge = new ClientInputBridge(window, manager, input, static () => false);
        window.RaiseMouseMove(new MouseMoveEventArgs(5, 5));
        window.RaiseMouseDown(new MouseClickEventArgs(MouseButton.Left, 5, 5));
        input.HandleKeyDown(new KeyEventArgs(Key.W, KeyAction.Press, KeyModifiers.None));

        window.RaiseFocusChanged(false);
        window.RaiseFocusChanged(true);

        Assert.False(input.IsKeyDown(Key.W));
        Assert.False(input.IsMouseCaptured);
        Assert.False(control.IsFocused);
        Assert.False(control.IsHovered);
        Assert.False(control.IsPressed);
        bridge.Destroy();
        input.Destroy();
        manager.Destroy();
    }

    [Fact]
    public void DestroyUnsubscribesEveryWindowAndManagerEvent()
    {
        var window = CreateWindow();
        var input = new ClientInputState(state => window.CursorState = state);
        var manager = new UiManager();
        var leaf = new TestNode { Focusable = true };
        OpenScreen(manager, leaf, new Size(100, 100));
        var bridge = new ClientInputBridge(window, manager, input, static () => false);
        var routed = 0;
        leaf.PointerMoved += (_, _) => routed++;
        leaf.PointerPressed += (_, _) => routed++;
        leaf.PointerReleased += (_, _) => routed++;
        leaf.PointerWheel += (_, _) => routed++;
        leaf.KeyDown += (_, _) => routed++;
        leaf.KeyUp += (_, _) => routed++;

        bridge.Destroy();
        bridge.Destroy();
        window.RaiseKeyDown(new KeyEventArgs(Key.W, KeyAction.Press, KeyModifiers.None));
        window.RaiseKeyUp(new KeyEventArgs(Key.W, KeyAction.Release, KeyModifiers.None));
        window.RaiseMouseMove(new MouseMoveEventArgs(5, 5));
        window.RaiseMouseDown(new MouseClickEventArgs(MouseButton.Left, 5, 5));
        window.RaiseMouseUp(new MouseClickEventArgs(MouseButton.Left, 5, 5));
        window.RaiseScroll(new ScrollEventArgs(0, 1));
        window.RaiseFocusChanged(false);
        manager.Close();

        Assert.Equal(0, routed);
        Assert.False(input.IsKeyDown(Key.W));
        Assert.False(input.IsMouseCaptured);
        input.Destroy();
        manager.Destroy();
    }

    [Fact]
    public void HandledEscapeDoesNotReachGame()
    {
        var window = CreateWindow();
        var input = new ClientInputState(state => window.CursorState = state);
        var manager = new UiManager();
        var toggles = 0;
        var bridge = new ClientInputBridge(window, manager, input, () =>
        {
            toggles++;
            return true;
        });

        window.RaiseKeyDown(new KeyEventArgs(Key.Escape, KeyAction.Press, KeyModifiers.None));

        Assert.Equal(1, toggles);
        Assert.False(input.IsKeyDown(Key.Escape));
        bridge.Destroy();
        input.Destroy();
        manager.Destroy();
    }

    [Fact]
    public void UnhandledEscapeContinuesToCurrentScreen()
    {
        var window = CreateWindow();
        var input = new ClientInputState(state => window.CursorState = state);
        var manager = new UiManager();
        var leaf = new TestNode { Focusable = true };
        OpenScreen(manager, leaf, new Size(100, 100));
        Assert.True(leaf.Focus());
        UiKeyEventArgs? routed = null;
        leaf.KeyDown += (_, args) => routed = args;
        var toggles = 0;
        var bridge = new ClientInputBridge(window, manager, input, () =>
        {
            toggles++;
            return false;
        });

        window.RaiseKeyDown(new KeyEventArgs(Key.Escape, KeyAction.Press, KeyModifiers.Alt));

        var eventArgs = Assert.IsType<UiKeyEventArgs>(routed);
        Assert.Equal(1, toggles);
        Assert.Equal(Key.Escape, eventArgs.Key);
        Assert.Equal(KeyModifiers.Alt, eventArgs.Modifiers);
        Assert.False(input.IsKeyDown(Key.Escape));
        bridge.Destroy();
        input.Destroy();
        manager.Destroy();
    }

    private static TestWindow CreateWindow()
    {
        return new TestWindow
        {
            Size = new Vector2D<int>(100, 100),
            CursorState = CursorState.Normal
        };
    }

    private static void OpenScreen(UiManager manager, UiNode node, Size viewport)
    {
        var root = new Canvas();
        node.Width = viewport.Width;
        node.Height = viewport.Height;
        root.Children.Add(node);
        manager.Open(new UiScreen(root));
        manager.Update(viewport);
    }

    private sealed class TestNode : UiNode
    {
    }

    private sealed class TestControl : Control
    {
    }
}
