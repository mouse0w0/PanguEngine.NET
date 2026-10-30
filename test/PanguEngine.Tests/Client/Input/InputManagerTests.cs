using PanguEngine.Client.Game;
using PanguEngine.Client.Input;
using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Huds;
using PanguEngine.Client.UI.Scenes;
using PanguEngine.Client.World;
using PanguEngine.Input;
using PanguEngine.Registries;
using PanguEngine.Tests.Windowing;
using PanguEngine.UI;
using PanguEngine.UI.Controls;
using PanguEngine.Windowing;
using PanguEngine.Network;
using PanguEngine.Network.Packets;
using PanguEngine.Network.Packets.Clientbound;
using PanguEngine.Network.Packets.Serverbound;
using Silk.NET.Maths;
using KeyEventArgs = PanguEngine.Windowing.KeyEventArgs;
using UiKeyEventArgs = PanguEngine.UI.Input.KeyEventArgs;

namespace PanguEngine.Tests.Client.Input;

public sealed class InputManagerTests
{
    [Fact]
    public void UIShutdownClearsTheWindowBeforeInputShutdown()
    {
        var (input, window, ui) = CreateManager();
        OpenScene(ui, new GameUIScene(new TestNode { Cursor = CursorShape.Hand }));
        input.Start();
        Assert.Equal(CursorShape.Hand, window.CursorShape);

        ui.Destroy();

        Assert.Equal(CursorShape.Arrow, window.CursorShape);
        input.Destroy();
    }

    [Fact]
    public void DestroyedInputIgnoresAnInFlightUICursorNotification()
    {
        var (input, window, ui) = CreateManager();
        var root = new TestNode { Cursor = CursorShape.Hand };
        OpenScene(ui, new GameUIScene(root));
        ui.PointerCursorChanged += (_, _) =>
        {
            if (ui.PointerCursor == CursorShape.Wait)
            {
                input.Destroy();
                window.CursorShape = CursorShape.HResize;
            }
        };
        input.Start();

        root.Cursor = CursorShape.Wait;

        Assert.Equal(CursorShape.HResize, window.CursorShape);
        ui.Destroy();
    }

    [Fact]
    public void CursorFollowsSceneChangesWithoutRequiringMouseMovement()
    {
        var (input, window, ui) = CreateManager();
        var sceneContext = new InputContext(InputScope.Ui, pointerCapturePolicy: PointerCapturePolicy.Preserve);
        window.SetMousePosition(new Vector2D<float>(5, 5));
        var first = new TestNode { Cursor = CursorShape.Hand };
        var firstScene = new GameUIScene(first) { InputContext = sceneContext };
        OpenScene(ui, firstScene);
        var moves = 0;
        first.PointerMoved += (_, _) => moves++;
        input.Start();
        Assert.Equal(CursorShape.Hand, window.CursorShape);
        Assert.Equal(0, moves);

        var second = new TestNode { Cursor = CursorShape.Crosshair };
        ui.Open(new GameUIScene(second) { InputContext = sceneContext });
        Assert.Equal(CursorShape.Arrow, window.CursorShape);
        ui.UpdateFrame(new Size(100, 100), 0);
        Assert.Equal(CursorShape.Crosshair, window.CursorShape);
        first.Cursor = CursorShape.Wait;
        Assert.Equal(CursorShape.Crosshair, window.CursorShape);

        window.RaiseFocusChanged(false);
        Assert.Equal(CursorShape.Arrow, window.CursorShape);
        window.RaiseFocusChanged(true);
        Assert.Equal(CursorShape.Arrow, window.CursorShape);
        window.RaiseMouseMove(new MouseMoveEventArgs(5, 5));
        Assert.Equal(CursorShape.Crosshair, window.CursorShape);
        ui.Close();
        Assert.Equal(CursorShape.Arrow, window.CursorShape);

        OpenScene(ui, firstScene);
        Assert.Equal(CursorShape.Wait, window.CursorShape);
        input.Destroy();
        Assert.Equal(CursorShape.Arrow, window.CursorShape);
        window.CursorShape = CursorShape.HResize;
        first.Cursor = CursorShape.Hand;
        Assert.Equal(CursorShape.HResize, window.CursorShape);
        ui.Destroy();
    }

    [Fact]
    public void ReleasingCaptureInsideKeyRoutingSynchronizesTheAbsolutePointerPosition()
    {
        var (input, window, ui) = CreateManager();
        var root = new Canvas();
        var first = new Panel { Width = 20, Height = 20, Focusable = true, Cursor = CursorShape.Hand };
        var second = new Panel { Width = 20, Height = 20, Cursor = CursorShape.Crosshair };
        Canvas.SetLeft(second, 30);
        root.Children.Add(first);
        root.Children.Add(second);
        OpenScene(ui, new GameUIScene(root)
        {
            InputContext = new InputContext(InputScope.Ui, pointerCapturePolicy: PointerCapturePolicy.Preserve)
        });
        window.SetMousePosition(new Vector2D<float>(5, 5));
        input.Start();
        Assert.True(first.Focus());
        Assert.True(input.TryCapturePointer());
        first.KeyDown += (_, _) => input.ReleasePointer();
        window.SetMousePosition(new Vector2D<float>(35, 5));

        window.RaiseKeyDown(new KeyEventArgs(Key.E, KeyAction.Press, KeyModifiers.None));

        Assert.False(input.IsPointerCaptured);
        Assert.Equal(CursorState.Normal, window.CursorState);
        Assert.Equal(CursorShape.Crosshair, window.CursorShape);
        ui.UpdateFrame(new Size(100, 100), 0);
        Assert.Equal(CursorShape.Crosshair, window.CursorShape);
        input.Destroy();
        ui.Destroy();
    }

    [Fact]
    public void CursorShapeUpdatesDoNotReleaseRelativePointerCapture()
    {
        var (input, window, ui) = CreateManager();
        var leaf = new TestNode { Cursor = CursorShape.Hand };
        OpenScene(ui, new GameUIScene(leaf)
        {
            InputContext = new InputContext(InputScope.Ui, pointerCapturePolicy: PointerCapturePolicy.Preserve)
        });
        input.Start();
        Assert.True(input.TryCapturePointer());

        leaf.Cursor = CursorShape.Wait;

        Assert.Equal(CursorShape.Wait, window.CursorShape);
        Assert.Equal(CursorState.Disabled, window.CursorState);
        Assert.True(input.IsPointerCaptured);
        input.ReleasePointer();
        Assert.Equal(CursorShape.Wait, window.CursorShape);
        input.Destroy();
        ui.Destroy();
    }

    [Fact]
    public void ReentrantCursorNotificationCannotRestoreThePreviousScenesShape()
    {
        var (input, window, ui) = CreateManager();
        var first = new TestNode { Cursor = CursorShape.Hand };
        var firstScene = new GameUIScene(first);
        var secondScene = new GameUIScene(new TestNode { Cursor = CursorShape.Crosshair });
        firstScene.PointerCursorChanged += (_, _) =>
        {
            if (firstScene.PointerCursor == CursorShape.Wait)
                ui.Open(secondScene);
        };
        OpenScene(ui, firstScene);
        input.Start();

        first.Cursor = CursorShape.Wait;
        ui.UpdateFrame(new Size(100, 100), 0);

        Assert.Same(secondScene, ui.CurrentScene);
        Assert.Equal(CursorShape.Crosshair, window.CursorShape);
        input.Destroy();
        ui.Destroy();
    }

    [Fact]
    public void WindowTextInputAndKeyRepeatPreserveTheirContracts()
    {
        var window = new TestWindow();
        var texts = new List<string>();
        window.TextInput += (_, text) => texts.Add(text);
        var initial = new KeyEventArgs(Key.Left, KeyAction.Press, KeyModifiers.Control);
        var repeat = new KeyEventArgs(Key.Left, KeyAction.Press, KeyModifiers.Control)
        {
            IsRepeat = true
        };

        window.RaiseTextInput("A\U0001F600中");
        var (key, action, modifiers) = repeat;

        Assert.Equal(["A\U0001F600中"], texts);
        Assert.False(initial.IsRepeat);
        Assert.True(repeat.IsRepeat);
        Assert.Equal(Key.Left, key);
        Assert.Equal(KeyAction.Press, action);
        Assert.Equal(KeyModifiers.Control, modifiers);
        Assert.NotEqual(initial, repeat);
    }

    [Fact]
    public void StartControlsWindowSubscriptionsAndDestroyIsIdempotent()
    {
        var game = new InputContext(InputScope.Game);
        var action = new InputAction(InputValueType.Button,
            [InputBinding.Button("default", game, Key.E)]);
        var (input, window, ui) = CreateManager(("test:action", action));
        using var contextToken = input.ActivateContext(game);
        var calls = 0;
        input.RegisterHandler(action, _ =>
        {
            calls++;
            return InputHandling.Handled;
        });

        window.RaiseKeyDown(new KeyEventArgs(Key.E, KeyAction.Press, KeyModifiers.None));
        input.Start();
        window.RaiseKeyDown(new KeyEventArgs(Key.E, KeyAction.Press, KeyModifiers.None));
        window.RaiseKeyUp(new KeyEventArgs(Key.E, KeyAction.Release, KeyModifiers.None));
        Assert.Throws<InvalidOperationException>(input.Start);
        input.Destroy();
        input.Destroy();
        window.RaiseKeyDown(new KeyEventArgs(Key.E, KeyAction.Press, KeyModifiers.None));

        Assert.Equal(2, calls);
        ui.Destroy();
    }

    [Fact]
    public void StartFreezesHandlerRegistrationAfterContextsAreActivated()
    {
        var game = new InputContext(InputScope.Game);
        var action = new InputAction(InputValueType.Button,
            [InputBinding.Button("default", game, Key.E)]);
        var (input, window, ui) = CreateManager(("test:action", action));
        using var contextToken = input.ActivateContext(game);
        var calls = 0;
        input.RegisterHandler(action, _ =>
        {
            calls++;
            return InputHandling.Pass;
        });
        input.Start();
        window.RaiseKeyDown(new KeyEventArgs(Key.E, KeyAction.Press, KeyModifiers.None));

        Assert.Equal(1, calls);
        Assert.Throws<InvalidOperationException>(() =>
            input.RegisterHandler(action, _ => InputHandling.Pass));

        input.Destroy();
        Assert.Throws<ObjectDisposedException>(() =>
            input.RegisterHandler(action, _ => InputHandling.Pass));
        ui.Destroy();
    }

    [Fact]
    public void DestroyCompletesManagerCleanupWhenStoppedHandlerThrows()
    {
        var game = new InputContext(InputScope.Game);
        var action = new InputAction(InputValueType.Button,
            [InputBinding.Button("default", game, Key.E)]);
        var (input, window, ui) = CreateManager(("test:action", action));
        using var contextToken = input.ActivateContext(game);
        input.RegisterHandler(action, args =>
        {
            if (args.Phase == InputActionPhase.Stopped)
                throw new InvalidOperationException("stop failed");
            return InputHandling.Pass;
        });
        input.Start();
        window.RaiseKeyDown(new KeyEventArgs(Key.E, KeyAction.Press, KeyModifiers.None));

        Assert.Throws<InvalidOperationException>(input.Destroy);
        Assert.Throws<ObjectDisposedException>(() => input.GetValue(action));
        input.Destroy();
        ui.Destroy();
    }

    [Fact]
    public void HandledUiKeyDoesNotReachGameAction()
    {
        var game = new InputContext(InputScope.Game);
        var action = new InputAction(InputValueType.Button,
            [InputBinding.Button("default", game, Key.E)]);
        var (input, window, ui) = CreateManager(("test:action", action));
        var leaf = new TestNode { Focusable = true };
        OpenScene(ui, new GameUIScene(leaf) { InputContext = new InputContext(InputScope.Ui) });
        Assert.True(leaf.Focus());
        leaf.KeyDown += (_, args) => args.Handled = true;
        using var contextToken = input.ActivateContext(game);
        var calls = 0;
        input.RegisterHandler(action, _ =>
        {
            calls++;
            return InputHandling.Handled;
        });
        input.Start();

        window.RaiseKeyDown(new KeyEventArgs(Key.E, KeyAction.Press, KeyModifiers.None));

        Assert.Equal(0, calls);
        input.Destroy();
        ui.Destroy();
    }

    [Fact]
    public void HandledUiKeyUpStillReleasesActiveAction()
    {
        var game = new InputContext(InputScope.Game);
        var action = new InputAction(InputValueType.Button,
            [InputBinding.Button("default", game, Key.E)]);
        var (input, window, ui) = CreateManager(("test:action", action));
        var leaf = new TestNode { Focusable = true };
        OpenScene(ui, new GameUIScene(leaf) { InputContext = new InputContext(InputScope.Ui) });
        Assert.True(leaf.Focus());
        leaf.KeyUp += (_, args) => args.Handled = true;
        using var contextToken = input.ActivateContext(game);
        var phases = new List<InputActionPhase>();
        input.RegisterHandler(action, args =>
        {
            phases.Add(args.Phase);
            return InputHandling.Handled;
        });
        input.Start();

        window.RaiseKeyDown(new KeyEventArgs(Key.E, KeyAction.Press, KeyModifiers.None));
        window.RaiseKeyUp(new KeyEventArgs(Key.E, KeyAction.Release, KeyModifiers.None));

        Assert.Equal([InputActionPhase.Started, InputActionPhase.Stopped], phases);
        input.Destroy();
        ui.Destroy();
    }

    [Fact]
    public void PointerCoordinatesUseFramebufferScale()
    {
        var (input, window, ui) = CreateManager();
        window.Size = new Vector2D<int>(100, 50);
        window.SetFramebufferSize(new Vector2D<int>(200, 150));
        var leaf = new TestNode();
        OpenScene(ui, new GameUIScene(leaf), new Size(200, 150));
        Point? position = null;
        leaf.PointerMoved += (_, args) => position = args.ScenePosition;
        input.Start();

        window.RaiseMouseMove(new MouseMoveEventArgs(25, 10));

        Assert.Equal(new Point(50, 30), position);
        input.Destroy();
        ui.Destroy();
    }

    [Fact]
    public void TextInputAndKeyRepeatPreserveUiAndActionContracts()
    {
        var game = new InputContext(InputScope.Game);
        var action = new InputAction(InputValueType.Button,
            [InputBinding.Button("default", game, Key.Left)]);
        var (input, window, ui) = CreateManager(("test:action", action));
        using var contextToken = input.ActivateContext(game);
        var actionCalls = 0;
        input.RegisterHandler(action, _ =>
        {
            actionCalls++;
            return InputHandling.Handled;
        });
        var firstLeaf = new TestNode { Focusable = true };
        var first = new GameUIScene(firstLeaf)
        {
            InputContext = new InputContext(InputScope.Ui)
        };
        OpenScene(ui, first);
        Assert.True(firstLeaf.Focus());
        UiKeyEventArgs? keyDown = null;
        UiKeyEventArgs? keyUp = null;
        var texts = new List<string>();
        firstLeaf.KeyDown += (_, args) => keyDown = args;
        firstLeaf.KeyUp += (_, args) => keyUp = args;
        firstLeaf.TextInput += (_, args) => texts.Add(args.Text);
        input.Start();

        window.RaiseKeyDown(new KeyEventArgs(Key.Left, KeyAction.Press, KeyModifiers.Control)
        {
            IsRepeat = true
        });
        window.RaiseKeyUp(new KeyEventArgs(Key.Left, KeyAction.Release, KeyModifiers.Control)
        {
            IsRepeat = true
        });
        window.RaiseTextInput("A\U0001F600中");

        Assert.True(Assert.IsType<UiKeyEventArgs>(keyDown).IsRepeat);
        Assert.True(Assert.IsType<UiKeyEventArgs>(keyUp).IsRepeat);
        Assert.Equal(0, actionCalls);
        Assert.Equal(["A\U0001F600中"], texts);
        input.Destroy();
        window.RaiseTextInput("ignored");
        Assert.Equal(["A\U0001F600中"], texts);
        ui.Destroy();
    }

    [Fact]
    public void CapturedPointerUsesFirstPositionAsBaseline()
    {
        var game = new InputContext(InputScope.Game);
        var look = new InputAction(InputValueType.Axis2D,
        [
            InputBinding.Axis2D(
                "default",
                game,
                InputSource.MouseMove,
                new Vector2D<double>(1, 1))
        ]);
        var (input, window, ui) = CreateManager(("test:look", look));
        using var contextToken = input.ActivateContext(game);
        var deltas = new List<Vector2D<double>>();
        input.RegisterHandler(look, args =>
        {
            deltas.Add(args.Value.Axis2D);
            return InputHandling.Handled;
        });
        input.Start();
        Assert.True(input.TryCapturePointer());

        window.RaiseMouseMove(new MouseMoveEventArgs(10, 20));
        window.RaiseMouseMove(new MouseMoveEventArgs(14, 18));

        Assert.Equal([new Vector2D<double>(4, -2)], deltas);
        input.Destroy();
        ui.Destroy();
    }

    [Fact]
    public void HandledUiModifierChangeStillStopsMismatchedExactContribution()
    {
        var game = new InputContext(InputScope.Game);
        var move = new InputAction(InputValueType.Axis1D,
            [InputBinding.Axis1D("shift", game, Key.W, 1, KeyModifiers.Shift)]);
        var control = new InputAction(InputValueType.Button,
            [InputBinding.Button("default", game, Key.ControlLeft)]);
        var (input, window, ui) = CreateManager(
            ("test:move", move),
            ("test:control", control));
        var leaf = new TestNode { Focusable = true };
        OpenScene(ui, new GameUIScene(leaf) { InputContext = new InputContext(InputScope.Ui) });
        Assert.True(leaf.Focus());
        leaf.KeyDown += (_, args) =>
        {
            if (args.Key == Key.ControlLeft)
                args.Handled = true;
        };
        using var contextToken = input.ActivateContext(game);
        var movePhases = new List<InputActionPhase>();
        var controlCalls = 0;
        input.RegisterHandler(move, args =>
        {
            movePhases.Add(args.Phase);
            return InputHandling.Handled;
        });
        input.RegisterHandler(control, _ =>
        {
            controlCalls++;
            return InputHandling.Handled;
        });
        input.Start();

        window.RaiseKeyDown(new KeyEventArgs(
            Key.ShiftLeft,
            KeyAction.Press,
            KeyModifiers.Shift));
        window.RaiseKeyDown(new KeyEventArgs(
            Key.W,
            KeyAction.Press,
            KeyModifiers.Shift));
        window.RaiseKeyDown(new KeyEventArgs(
            Key.ControlLeft,
            KeyAction.Press,
            KeyModifiers.Shift | KeyModifiers.Control));

        Assert.Equal([InputActionPhase.Started, InputActionPhase.Stopped], movePhases);
        Assert.Equal(0, controlCalls);
        input.Destroy();
        ui.Destroy();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NestedSuspendRestoresOnlyAfterFinalTokenEnds(bool sameContext)
    {
        var first = new InputContext(InputScope.Ui, pointerCapturePolicy: PointerCapturePolicy.Suspend);
        var second = sameContext
            ? first
            : new InputContext(InputScope.Ui, pointerCapturePolicy: PointerCapturePolicy.Suspend);
        var (input, _, ui) = CreateManager();
        input.Start();
        Assert.True(input.TryCapturePointer());

        var firstToken = input.ActivateContext(first);
        var secondToken = input.ActivateContext(second);
        Assert.False(input.IsPointerCaptured);
        firstToken.Dispose();
        Assert.False(input.IsPointerCaptured);
        secondToken.Dispose();

        Assert.True(input.IsPointerCaptured);
        input.Destroy();
        ui.Destroy();
    }

    [Fact]
    public void FocusLossCancelsSuspendRestoreAndUiState()
    {
        var game = new InputContext(InputScope.Game);
        var suspend = new InputContext(InputScope.Ui, pointerCapturePolicy: PointerCapturePolicy.Suspend);
        var action = new InputAction(InputValueType.Button,
            [InputBinding.Button("default", game, Key.E)]);
        var (input, window, ui) = CreateManager(("test:action", action));
        var control = new TestControl { Focusable = true };
        OpenScene(ui, new GameUIScene(control) { InputContext = new InputContext(InputScope.Ui) });
        using var gameToken = input.ActivateContext(game);
        var phases = new List<InputActionPhase>();
        input.RegisterHandler(action, args =>
        {
            phases.Add(args.Phase);
            return InputHandling.Handled;
        });
        input.Start();
        window.RaiseMouseMove(new MouseMoveEventArgs(5, 5));
        window.RaiseMouseDown(new MouseClickEventArgs(MouseButton.Left, 5, 5));
        Assert.True(input.TryCapturePointer());
        var token = input.ActivateContext(suspend);
        window.RaiseKeyDown(new KeyEventArgs(Key.E, KeyAction.Press, KeyModifiers.None));

        window.RaiseFocusChanged(false);
        token.Dispose();
        window.RaiseFocusChanged(true);

        Assert.False(input.IsPointerCaptured);
        Assert.False(control.IsFocused);
        Assert.False(control.IsPressed);
        Assert.False(control.IsHovered);
        Assert.Equal([InputActionPhase.Started, InputActionPhase.Stopped], phases);
        input.Destroy();
        ui.Destroy();
    }

    [Fact]
    public void BuiltinBindingsMoveCaptureThenBreak()
    {
        var actions = new Registry<InputAction>(RegistryKeys.InputAction);
        var contexts = new Registry<InputContext>(RegistryKeys.InputContext);
        BuiltinInputActions.Register(actions);
        BuiltinInputContexts.Register(contexts);
        actions.Freeze();
        contexts.Freeze();
        var map = InputBindingMap.CreateFromRegistries(actions, contexts);
        var window = new TestWindow
        {
            Size = new Vector2D<int>(100, 100),
            CursorState = CursorState.Normal
        };
        var ui = new UIManager();
        var input = new InputManager(window, ui, map);
        using var game = input.ActivateContext(BuiltinInputContexts.Game);
        var breaks = 0;
        input.RegisterHandler(BuiltinInputActions.CapturePointer, args =>
        {
            if (args.Phase != InputActionPhase.Started || input.IsPointerCaptured)
                return InputHandling.Pass;
            return input.TryCapturePointer() ? InputHandling.Handled : InputHandling.Pass;
        });
        input.RegisterHandler(BuiltinInputActions.BreakBlock, args =>
        {
            if (args.Phase == InputActionPhase.Started)
                breaks++;
            return InputHandling.Handled;
        });
        input.Start();

        window.RaiseKeyDown(new KeyEventArgs(Key.W, KeyAction.Press, KeyModifiers.None));
        Assert.Equal(new Vector2D<double>(0, 1), input.GetValue(BuiltinInputActions.Move).Axis2D);
        window.RaiseMouseDown(new MouseClickEventArgs(MouseButton.Left, 10, 20));
        window.RaiseMouseUp(new MouseClickEventArgs(MouseButton.Left, 10, 20));
        Assert.True(input.IsPointerCaptured);
        Assert.Equal(0, breaks);
        window.RaiseMouseDown(new MouseClickEventArgs(MouseButton.Left, 10, 20));

        Assert.Equal(1, breaks);
        input.Destroy();
        ui.Destroy();
    }

    [Fact]
    public void OpeningSceneUsesDefaultUiContext()
    {
        var action = new InputAction(InputValueType.Button,
            [InputBinding.Button("default", BuiltinInputContexts.Ui, Key.E)]);
        var (input, window, ui) = CreateManager(("test:action", action));
        OpenScene(ui, new GameUIScene());
        var calls = 0;
        input.RegisterHandler(action, _ =>
        {
            calls++;
            return InputHandling.Handled;
        });
        input.Start();

        window.RaiseKeyDown(new KeyEventArgs(Key.E, KeyAction.Press, KeyModifiers.None));

        Assert.Equal(1, calls);
        input.Destroy();
        ui.Destroy();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SceneContextUsesItsOwnActionsAndPointerPolicy(bool externalOwner)
    {
        var context = new InputContext(
            InputScope.Ui,
            pointerCapturePolicy: PointerCapturePolicy.Suspend);
        var action = new InputAction(InputValueType.Button,
            [InputBinding.Button("default", context, Key.E)]);
        var (input, window, ui) = CreateManager(("test:action", action));
        var calls = 0;
        InputContext? receivedContext = null;
        input.RegisterHandler(action, args =>
        {
            calls++;
            receivedContext = args.Context;
            return InputHandling.Handled;
        });
        input.Start();
        Assert.True(input.TryCapturePointer());
        using var externalToken = externalOwner ? input.ActivateContext(context) : null;
        OpenScene(ui, new GameUIScene { InputContext = context });
        Assert.False(input.IsPointerCaptured);

        window.RaiseKeyDown(new KeyEventArgs(Key.E, KeyAction.Press, KeyModifiers.None));

        Assert.Equal(1, calls);
        Assert.Same(context, receivedContext);
        ui.Close();
        Assert.Equal(!externalOwner, input.IsPointerCaptured);
        externalToken?.Dispose();
        Assert.True(input.IsPointerCaptured);
        input.Destroy();
        ui.Destroy();
    }

    [Fact]
    public void PauseMenuCapturesGameKeyboardActions()
    {
        var (input, window, ui) = CreateManager(("test:move", BuiltinInputActions.Move));
        using var gameToken = input.ActivateContext(BuiltinInputContexts.Game);
        var calls = 0;
        input.RegisterHandler(BuiltinInputActions.Move, _ =>
        {
            calls++;
            return InputHandling.Handled;
        });
        input.Start();
        ui.Open(new PauseMenu());

        window.RaiseKeyDown(new KeyEventArgs(Key.W, KeyAction.Press, KeyModifiers.None));

        Assert.Equal(0, calls);
        input.Destroy();
        ui.Destroy();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SceneActivationFollowsQueuedSceneNotifications(bool closeCurrent)
    {
        var firstContext = new InputContext(InputScope.Ui);
        var secondContext = new InputContext(InputScope.Ui);
        var thirdContext = new InputContext(InputScope.Ui);
        var firstAction = new InputAction(InputValueType.Button,
            [InputBinding.Button("first", firstContext, Key.E)]);
        var secondAction = new InputAction(InputValueType.Button,
            [InputBinding.Button("second", secondContext, Key.F)]);
        var thirdAction = new InputAction(InputValueType.Button,
            [InputBinding.Button("third", thirdContext, Key.F)]);
        var (input, window, ui) = CreateManager(
            ("test:first", firstAction),
            ("test:second", secondAction),
            ("test:third", thirdAction));
        var routes = new List<string>();
        var replacement = new GameUIScene { InputContext = thirdContext };
        var replaced = false;
        input.RegisterHandler(firstAction, args =>
        {
            if (args.Phase == InputActionPhase.Stopped && !replaced)
            {
                replaced = true;
                ui.Open(replacement);
            }

            return InputHandling.Pass;
        });
        input.RegisterHandler(secondAction, _ =>
        {
            routes.Add("second");
            return InputHandling.Handled;
        });
        input.RegisterHandler(thirdAction, _ =>
        {
            routes.Add("third");
            return InputHandling.Handled;
        });
        input.Start();
        var observedContexts = new List<InputContext?>();
        ui.CurrentSceneChanged += (_, current) =>
        {
            Assert.Same(current, ui.CurrentScene);
            observedContexts.Add(current?.InputContext);
        };
        OpenScene(ui, new GameUIScene { InputContext = firstContext });
        window.RaiseKeyDown(new KeyEventArgs(Key.E, KeyAction.Press, KeyModifiers.None));

        if (closeCurrent)
            ui.Close();
        else
            ui.Open(new GameUIScene { InputContext = secondContext });
        window.RaiseKeyDown(new KeyEventArgs(Key.F, KeyAction.Press, KeyModifiers.None));

        InputContext?[] expectedContexts = closeCurrent
            ? [firstContext, null, thirdContext]
            : [firstContext, secondContext, thirdContext];
        Assert.Equal(expectedContexts, observedContexts);
        Assert.Equal(["third"], routes);
        input.Destroy();
        ui.Destroy();
    }

    [Fact]
    public void SceneTransitionFailureStillAllowsManagerCleanup()
    {
        var firstContext = new InputContext(InputScope.Ui);
        var secondContext = new InputContext(InputScope.Ui,
            pointerCapturePolicy: PointerCapturePolicy.Suspend);
        var firstAction = new InputAction(InputValueType.Button,
            [InputBinding.Button("first", firstContext, Key.E)]);
        var (input, window, ui) = CreateManager(("test:first", firstAction));
        var expected = new InvalidOperationException("stop failed");
        input.RegisterHandler(firstAction, args =>
        {
            if (args.Phase == InputActionPhase.Stopped)
                throw expected;
            return InputHandling.Pass;
        });
        input.Start();
        Assert.True(input.TryCapturePointer());
        OpenScene(ui, new GameUIScene { InputContext = firstContext });
        window.RaiseKeyDown(new KeyEventArgs(Key.E, KeyAction.Press, KeyModifiers.None));

        Assert.Same(expected, Assert.Throws<InvalidOperationException>(() =>
            ui.Open(new GameUIScene { InputContext = secondContext })));

        input.Destroy();
        Assert.Equal(CursorState.Normal, window.CursorState);
        Assert.Throws<ObjectDisposedException>(() => input.GetValue(firstAction));
        ui.Destroy();
    }

    [Fact]
    public void FocusLossStopsBeforePointerAndUiCleanupWhenStoppedHandlerThrows()
    {
        var game = new InputContext(InputScope.Game);
        var action = new InputAction(InputValueType.Button,
            [InputBinding.Button("default", game, Key.E)]);
        var (input, window, ui) = CreateManager(("test:action", action));
        using var gameToken = input.ActivateContext(game);
        var control = new TestControl { Focusable = true };
        OpenScene(ui, new GameUIScene(control) { InputContext = new InputContext(InputScope.Ui) });
        var expected = new InvalidOperationException("stop failed");
        var starts = 0;
        input.RegisterHandler(action, args =>
        {
            if (args.Phase == InputActionPhase.Started)
                starts++;
            if (args.Phase == InputActionPhase.Stopped)
                throw expected;
            return InputHandling.Pass;
        });
        input.Start();
        window.RaiseMouseMove(new MouseMoveEventArgs(5, 5));
        window.RaiseMouseDown(new MouseClickEventArgs(MouseButton.Left, 5, 5));
        Assert.True(control.IsFocused);
        Assert.True(control.IsPressed);
        Assert.True(control.IsHovered);
        Assert.True(input.TryCapturePointer());
        window.RaiseKeyDown(new KeyEventArgs(Key.E, KeyAction.Press, KeyModifiers.None));

        Assert.Same(expected, Assert.Throws<InvalidOperationException>(() => window.RaiseFocusChanged(false)));

        Assert.True(input.IsPointerCaptured);
        Assert.Equal(CursorState.Disabled, window.CursorState);
        Assert.True(control.IsFocused);
        Assert.True(control.IsPressed);
        Assert.True(control.IsHovered);
        Assert.Equal(InputActionValue.Zero, input.GetValue(action));
        Assert.Equal(1, starts);
        input.Destroy();
        ui.Destroy();
    }

    [Fact]
    public void ContextReleaseFailureStillRestoresPointer()
    {
        var modal = new InputContext(InputScope.Ui,
            captureMask: InputCaptureMask.All,
            pointerCapturePolicy: PointerCapturePolicy.Suspend);
        var modalAction = new InputAction(InputValueType.Button,
            [InputBinding.Button("default", modal, Key.E)]);
        var (input, window, ui) = CreateManager(("test:modal", modalAction));
        var expected = new InvalidOperationException("stop failed");
        input.RegisterHandler(modalAction, args =>
        {
            if (args.Phase == InputActionPhase.Stopped)
                throw expected;
            return InputHandling.Pass;
        });
        input.Start();
        Assert.True(input.TryCapturePointer());
        var token = input.ActivateContext(modal);
        Assert.False(input.IsPointerCaptured);
        window.RaiseKeyDown(new KeyEventArgs(Key.E, KeyAction.Press, KeyModifiers.None));

        Assert.Same(expected, Assert.Throws<InvalidOperationException>(token.Dispose));

        Assert.True(input.IsPointerCaptured);
        Assert.Equal(InputActionValue.Zero, input.GetValue(modalAction));
        input.Destroy();
        Assert.Equal(CursorState.Normal, window.CursorState);
        ui.Destroy();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NonCapturingSceneChangeKeepsMovementAndStopsCurrentPropagation(bool externalOwner)
    {
        var game = new InputContext(InputScope.Game);
        var sceneContext = new InputContext(InputScope.Ui);
        var move = new InputAction(InputValueType.Axis1D, [InputBinding.Axis1D("default", game, Key.W, 1)]);
        var open = new InputAction(InputValueType.Button, [InputBinding.Button("default", game, Key.E)]);
        var (input, window, ui) = CreateManager(("test:move", move), ("test:open", open));
        using var gameToken = input.ActivateContext(game);
        using var externalToken = externalOwner ? input.ActivateContext(sceneContext) : null;
        var secondHandlerCalls = 0;
        input.RegisterHandler(open, args =>
        {
            if (args.Phase == InputActionPhase.Started)
                ui.Open(new GameUIScene { InputContext = sceneContext });
            return InputHandling.Pass;
        }, priority: 1);
        input.RegisterHandler(open, args =>
        {
            if (args.Phase == InputActionPhase.Started)
                secondHandlerCalls++;
            return InputHandling.Pass;
        });
        input.Start();
        window.RaiseKeyDown(new KeyEventArgs(Key.W, KeyAction.Press, KeyModifiers.None));
        Assert.Equal(1, input.GetValue(move).Axis1D);

        window.RaiseKeyDown(new KeyEventArgs(Key.E, KeyAction.Press, KeyModifiers.None));

        Assert.Equal(0, secondHandlerCalls);
        Assert.Equal(1, input.GetValue(move).Axis1D);
        window.RaiseKeyDown(new KeyEventArgs(Key.W, KeyAction.Press, KeyModifiers.None));
        Assert.Equal(1, input.GetValue(move).Axis1D);
        ui.Close();
        Assert.Equal(1, input.GetValue(move).Axis1D);
        window.RaiseKeyUp(new KeyEventArgs(Key.W, KeyAction.Release, KeyModifiers.None));
        Assert.Equal(InputActionValue.Zero, input.GetValue(move));
        window.RaiseKeyDown(new KeyEventArgs(Key.W, KeyAction.Press, KeyModifiers.None));
        Assert.Equal(1, input.GetValue(move).Axis1D);
        input.Destroy();
        ui.Destroy();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SessionRefreshUsesMovementRemainingAfterInputInvalidation(bool loseFocus)
    {
        var action = BuiltinInputActions.Move;
        var (input, window, ui) = CreateManager(("test:move", action));
        using var gameToken = input.ActivateContext(BuiltinInputContexts.Game);
        var connection = new LocalConnection();
        var session = new ClientSession(connection, new ClientWorld(), new InitialWorldStatePacket(0, Guid.NewGuid(), default, 0.05, []), 0);
        input.RegisterHandler(action, _ =>
        {
            session.RefreshInput(input, 0, 0);
            return InputHandling.Handled;
        });
        input.StateInvalidated += () => session.RefreshInput(input, 0, 0);
        input.Start();
        window.RaiseKeyDown(new KeyEventArgs(Key.W, KeyAction.Press, KeyModifiers.None));
        List<ServerboundPacket> packets = [];
        connection.DrainServerboundPackets(packets);
        packets.Clear();

        if (loseFocus)
            window.RaiseFocusChanged(false);
        else
            ui.Open(new GameUIScene { PausesGame = false, InputContext = new InputContext(InputScope.Ui) });

        connection.DrainServerboundPackets(packets);
        Assert.NotEmpty(packets);
        var submitted = Assert.IsType<InputStatePacket>(packets[^1]);
        Assert.Equal(loseFocus ? 0d : 1d, submitted.Forward);
        input.Destroy();
        ui.Destroy();
    }

    [Fact]
    public void InstantMovementSampleDoesNotBecomePersistentIntent()
    {
        var action = BuiltinInputActions.Move;
        var (input, window, ui) = CreateManager(("test:move", action));
        using var gameToken = input.ActivateContext(BuiltinInputContexts.Game);
        input.Bindings.SetBinding(ResourceKey.Parse("test:move/forward"), InputSource.MouseWheel, KeyModifiers.None);
        var connection = new LocalConnection();
        var session = new ClientSession(connection, new ClientWorld(), new InitialWorldStatePacket(0, Guid.NewGuid(), default, 0.05, []), 0);
        input.RegisterHandler(action, _ =>
        {
            session.RefreshInput(input, 0, 0);
            return InputHandling.Handled;
        });
        input.Start();

        window.RaiseScroll(new ScrollEventArgs(0, 1));

        List<ServerboundPacket> packets = [];
        connection.DrainServerboundPackets(packets);
        var packet = Assert.Single(packets);
        var submitted = Assert.IsType<InputStatePacket>(packet);
        Assert.Equal((0d, 0d), (submitted.Forward, submitted.Right));
        input.Destroy();
        ui.Destroy();
    }

    [Fact]
    public void UnobservedPauseResumeRefreshesEffectiveHeldInput()
    {
        var action = BuiltinInputActions.Move;
        var (input, window, ui) = CreateManager(("test:move", action));
        using var gameToken = input.ActivateContext(BuiltinInputContexts.Game);
        var connection = new LocalConnection();
        var session = new ClientSession(connection, new ClientWorld(), new InitialWorldStatePacket(0, Guid.NewGuid(), default, 0.05, []), 0);
        input.Start();
        window.RaiseKeyDown(new KeyEventArgs(Key.W, KeyAction.Press, KeyModifiers.None));
        connection.SetAcceptingRequests(false, clearPending: true);
        connection.SetAcceptingRequests(true, clearPending: false);

        session.RefreshInputAfterResume(input, 0, 0);
        session.RefreshInputAfterResume(input, 0, 0);

        List<ServerboundPacket> packets = [];
        connection.DrainServerboundPackets(packets);
        var packet = Assert.Single(packets);
        Assert.Equal(1d, Assert.IsType<InputStatePacket>(packet).Forward);
        input.Destroy();
        ui.Destroy();
    }

    [Fact]
    public void ReplacingSceneWithSameContextCancelsOnlySceneState()
    {
        var game = new InputContext(InputScope.Game);
        var sceneContext = new InputContext(InputScope.Ui);
        var move = new InputAction(InputValueType.Axis1D,
            [InputBinding.Axis1D("default", game, Key.W, 1)]);
        var select = new InputAction(InputValueType.Button,
            [InputBinding.Button("default", sceneContext, Key.E)]);
        var (input, window, ui) = CreateManager(("test:move", move), ("test:select", select));
        using var gameToken = input.ActivateContext(game);
        using var externalToken = input.ActivateContext(sceneContext);
        var events = new List<InputActionEvent>();
        input.RegisterHandler(select, args =>
        {
            events.Add(args);
            return InputHandling.Pass;
        });
        input.Start();
        ui.Open(new GameUIScene { InputContext = sceneContext });
        window.RaiseKeyDown(new KeyEventArgs(Key.W, KeyAction.Press, KeyModifiers.None));
        window.RaiseKeyDown(new KeyEventArgs(Key.E, KeyAction.Press, KeyModifiers.None));

        ui.Open(new GameUIScene { InputContext = sceneContext });

        Assert.Equal(1, input.GetValue(move).Axis1D);
        Assert.Equal(InputActionValue.Zero, input.GetValue(select));
        Assert.Equal([InputActionPhase.Started, InputActionPhase.Stopped], events.Select(args => args.Phase));
        Assert.Null(events[^1].Source);
        window.RaiseKeyDown(new KeyEventArgs(Key.E, KeyAction.Press, KeyModifiers.None));
        Assert.Equal(InputActionValue.Zero, input.GetValue(select));
        window.RaiseKeyUp(new KeyEventArgs(Key.E, KeyAction.Release, KeyModifiers.None));
        window.RaiseKeyDown(new KeyEventArgs(Key.E, KeyAction.Press, KeyModifiers.None));
        Assert.True(input.GetValue(select).Button);
        input.Destroy();
        ui.Destroy();
    }

    [Fact]
    public void ModalSceneCancellationFailureStillAllowsCleanup()
    {
        var game = new InputContext(InputScope.Game);
        var action = new InputAction(InputValueType.Button,
        [
            InputBinding.Button("first", game, Key.E),
            InputBinding.Button("second", game, Key.F)
        ]);
        var (input, window, ui) = CreateManager(("test:action", action));
        using var gameToken = input.ActivateContext(game);
        var expected = new InvalidOperationException("stop failed");
        var starts = 0;
        input.RegisterHandler(action, args =>
        {
            if (args.Phase == InputActionPhase.Started)
                starts++;
            if (args.Phase == InputActionPhase.Stopped)
                throw expected;
            return InputHandling.Pass;
        });
        input.Start();
        Assert.True(input.TryCapturePointer());
        window.RaiseKeyDown(new KeyEventArgs(Key.E, KeyAction.Press, KeyModifiers.None));
        var scene = new GameUIScene();

        Assert.Same(expected, Assert.Throws<InvalidOperationException>(() => ui.Open(scene)));

        Assert.Same(scene, ui.CurrentScene);
        Assert.False(input.IsPointerCaptured);
        Assert.Equal(1, starts);
        Assert.Equal(InputActionValue.Zero, input.GetValue(action));
        input.Destroy();
        Assert.Equal(CursorState.Normal, window.CursorState);
        ui.Destroy();
    }

    [Fact]
    public void ThrowingUiKeyUpStopsBeforeGameActionRelease()
    {
        var game = new InputContext(InputScope.Game);
        var action = new InputAction(InputValueType.Button, [InputBinding.Button("default", game, Key.E)]);
        var (input, window, ui) = CreateManager(("test:action", action));
        using var gameToken = input.ActivateContext(game);
        var leaf = new TestNode { Focusable = true };
        OpenScene(ui, new GameUIScene(leaf) { InputContext = new InputContext(InputScope.Ui) });
        Assert.True(leaf.Focus());
        var expected = new InvalidOperationException("UI release failed");
        leaf.KeyUp += (_, _) => throw expected;
        var phases = new List<InputActionPhase>();
        input.RegisterHandler(action, args =>
        {
            phases.Add(args.Phase);
            return InputHandling.Pass;
        });
        input.Start();
        window.RaiseKeyDown(new KeyEventArgs(Key.E, KeyAction.Press, KeyModifiers.None));

        Assert.Same(expected, Assert.Throws<InvalidOperationException>(() =>
            window.RaiseKeyUp(new KeyEventArgs(Key.E, KeyAction.Release, KeyModifiers.None))));

        Assert.True(input.GetValue(action).Button);
        Assert.Equal([InputActionPhase.Started], phases);
        input.Destroy();
        ui.Destroy();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ThrowingUiMouseReleaseStopsBeforeGameActionRelease(bool clicked)
    {
        var game = new InputContext(InputScope.Game);
        var action = new InputAction(InputValueType.Button,
            [InputBinding.Button("default", game, MouseButton.Left)]);
        var (input, window, ui) = CreateManager(("test:action", action));
        using var gameToken = input.ActivateContext(game);
        var leaf = new TestNode();
        OpenScene(ui, new GameUIScene(leaf) { InputContext = new InputContext(InputScope.Ui) });
        var expected = new InvalidOperationException("UI release failed");
        if (clicked)
            leaf.PointerClicked += (_, _) => throw expected;
        else
            leaf.PointerReleased += (_, _) => throw expected;
        input.Start();
        window.RaiseMouseDown(new MouseClickEventArgs(MouseButton.Left, 5, 5));
        Assert.True(input.GetValue(action).Button);

        Assert.Same(expected, Assert.Throws<InvalidOperationException>(() =>
            window.RaiseMouseUp(new MouseClickEventArgs(MouseButton.Left, 5, 5))));

        Assert.True(input.GetValue(action).Button);
        input.Destroy();
        ui.Destroy();
    }

    [Fact]
    public void ThrowingUiModifierPressStopsBeforeWithdrawingExactContribution()
    {
        var game = new InputContext(InputScope.Game);
        var action = new InputAction(InputValueType.Button,
            [InputBinding.Button("default", game, Key.W, KeyModifiers.Shift)]);
        var (input, window, ui) = CreateManager(("test:action", action));
        using var gameToken = input.ActivateContext(game);
        var leaf = new TestNode { Focusable = true };
        OpenScene(ui, new GameUIScene(leaf) { InputContext = new InputContext(InputScope.Ui) });
        Assert.True(leaf.Focus());
        var expected = new InvalidOperationException("UI modifier failed");
        leaf.KeyDown += (_, args) =>
        {
            if (args.Key == Key.ControlLeft)
                throw expected;
        };
        input.Start();
        window.RaiseKeyDown(new KeyEventArgs(Key.W, KeyAction.Press, KeyModifiers.Shift));
        Assert.True(input.GetValue(action).Button);

        Assert.Same(expected, Assert.Throws<InvalidOperationException>(() =>
            window.RaiseKeyDown(new KeyEventArgs(Key.ControlLeft, KeyAction.Press,
                KeyModifiers.Control | KeyModifiers.Shift))));

        Assert.True(input.GetValue(action).Button);
        input.Destroy();
        ui.Destroy();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UiMovementUsesCurrentBaselineAfterHandledSample(bool captured)
    {
        var context = new InputContext(InputScope.Ui,
            pointerCapturePolicy: captured ? PointerCapturePolicy.Preserve : PointerCapturePolicy.Suspend);
        var action = new InputAction(InputValueType.Axis2D,
            [InputBinding.Axis2D("default", context, InputSource.MouseMove, new Vector2D<double>(1, 1))]);
        var (input, window, ui) = CreateManager(("test:action", action));
        var leaf = new TestNode();
        OpenScene(ui, new GameUIScene(leaf) { InputContext = context });
        leaf.PointerMoved += (_, args) => args.Handled = args.ScenePosition.X == 14;
        var deltas = new List<Vector2D<double>>();
        input.RegisterHandler(action, args =>
        {
            deltas.Add(args.Value.Axis2D);
            return InputHandling.Handled;
        });
        input.Start();
        if (captured)
            Assert.True(input.TryCapturePointer());
        else
            Assert.False(input.TryCapturePointer());

        window.RaiseMouseMove(new MouseMoveEventArgs(10, 20));
        window.RaiseMouseMove(new MouseMoveEventArgs(14, 18));
        window.RaiseMouseMove(new MouseMoveEventArgs(17, 19));

        Assert.Equal([new Vector2D<double>(3, 1)], deltas);
        input.Destroy();
        ui.Destroy();
    }

    [Fact]
    public void FirstUncapturedMouseSampleStillMaintainsModifiers()
    {
        var game = new InputContext(InputScope.Game);
        var action = new InputAction(InputValueType.Button,
            [InputBinding.Button("default", game, Key.W, KeyModifiers.Shift)]);
        var (input, window, ui) = CreateManager(("test:action", action));
        using var gameToken = input.ActivateContext(game);
        OpenScene(ui, new GameUIScene
        {
            InputContext = new InputContext(InputScope.Ui,
                pointerCapturePolicy: PointerCapturePolicy.Suspend)
        });
        input.Start();
        window.RaiseKeyDown(new KeyEventArgs(Key.W, KeyAction.Press, KeyModifiers.Shift));
        Assert.True(input.GetValue(action).Button);
        window.SetKeyModifiers(KeyModifiers.Control | KeyModifiers.Shift);

        window.RaiseMouseMove(new MouseMoveEventArgs(5, 5));

        Assert.False(input.IsPointerCaptured);
        Assert.Equal(InputActionValue.Zero, input.GetValue(action));
        input.Destroy();
        ui.Destroy();
    }

    [Fact]
    public void HandledUiMousePressDoesNotReachGameAction()
    {
        var game = new InputContext(InputScope.Game);
        var action = new InputAction(InputValueType.Button,
            [InputBinding.Button("default", game, MouseButton.Left)]);
        var (input, window, ui) = CreateManager(("test:action", action));
        using var gameToken = input.ActivateContext(game);
        var leaf = new TestNode();
        OpenScene(ui, new GameUIScene(leaf) { InputContext = new InputContext(InputScope.Ui) });
        leaf.PointerPressed += (_, args) => args.Handled = true;
        input.Start();

        window.RaiseMouseDown(new MouseClickEventArgs(MouseButton.Left, 5, 5));

        Assert.Equal(InputActionValue.Zero, input.GetValue(action));
        input.Destroy();
        ui.Destroy();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UiFailureStopsBeforeMaintenanceAndCompletion(bool failCompletion)
    {
        var game = new InputContext(InputScope.Game);
        var first = new InputAction(InputValueType.Button, [InputBinding.Button("default", game, Key.E)]);
        var second = new InputAction(InputValueType.Button, [InputBinding.Button("default", game, Key.F)]);
        var (input, window, ui) = CreateManager(("test:first", first), ("test:second", second));
        using var gameToken = input.ActivateContext(game);
        var leaf = new TestNode { Focusable = true };
        OpenScene(ui, new GameUIScene(leaf) { InputContext = new InputContext(InputScope.Ui) });
        Assert.True(leaf.Focus());
        var uiError = new InvalidOperationException("UI release failed");
        var actionError = new InvalidOperationException("action stop failed");
        var completionError = new InvalidOperationException("completion failed");
        leaf.KeyUp += (_, _) => throw uiError;
        IDisposable? modalToken = null;
        var dispatching = true;
        var stoppedActions = new List<InputAction>();
        input.RegisterHandler(first, args =>
        {
            if (args.Phase == InputActionPhase.Stopped && dispatching)
            {
                stoppedActions.Add(first);
                modalToken = input.ActivateContext(BuiltinInputContexts.Ui);
                throw actionError;
            }

            return InputHandling.Pass;
        });
        input.RegisterHandler(second, args =>
        {
            if (args.Phase == InputActionPhase.Stopped && dispatching)
            {
                stoppedActions.Add(second);
                if (failCompletion)
                    throw completionError;
            }

            return InputHandling.Pass;
        });
        input.Start();
        window.RaiseKeyDown(new KeyEventArgs(Key.E, KeyAction.Press, KeyModifiers.None));
        window.RaiseKeyDown(new KeyEventArgs(Key.F, KeyAction.Press, KeyModifiers.None));

        var error = Assert.Throws<InvalidOperationException>(() =>
            window.RaiseKeyUp(new KeyEventArgs(Key.E, KeyAction.Release, KeyModifiers.None)));

        Assert.Same(uiError, error);
        Assert.Empty(stoppedActions);
        Assert.True(input.GetValue(first).Button);
        Assert.True(input.GetValue(second).Button);
        Assert.Null(modalToken);
        dispatching = false;
        input.Destroy();
        ui.Destroy();
    }

    [Fact]
    public void DebugInfoToggleIgnoresRepeatAndIsBlockedByOpenScene()
    {
        var action = BuiltinInputActions.ToggleDebugInfo;
        var (input, window, ui) = CreateManager(("pangu:toggle_debug_info", action));
        var definitions = new Registry<HudDefinition>(RegistryKeys.Hud);
        var key = ResourceKey.Create("pangu", "debug_info");
        definitions.Register(key, BuiltinHuds.DebugInfo);
        definitions.Freeze();
        ui.InitializeHud(definitions);
        var debugInfo = Assert.IsType<DebugInfoHud>(ui.Hud.Get(key));
        using var activation = input.ActivateContext(BuiltinInputContexts.Game);
        input.RegisterHandler(action, args =>
        {
            if (args.Phase != InputActionPhase.Started)
                return InputHandling.Pass;

            debugInfo.Toggle();
            return InputHandling.Handled;
        });
        input.Start();

        Assert.Equal(Visibility.Collapsed, debugInfo.Root.Visibility);
        window.RaiseKeyDown(new KeyEventArgs(Key.F3, KeyAction.Press, KeyModifiers.None));
        Assert.Equal(Visibility.Visible, debugInfo.Root.Visibility);
        window.RaiseKeyDown(new KeyEventArgs(Key.F3, KeyAction.Press, KeyModifiers.None)
        {
            IsRepeat = true
        });
        window.RaiseKeyUp(new KeyEventArgs(Key.F3, KeyAction.Release, KeyModifiers.None));
        Assert.Equal(Visibility.Visible, debugInfo.Root.Visibility);

        ui.Open(new GameUIScene(new Panel()));
        window.RaiseKeyDown(new KeyEventArgs(Key.F3, KeyAction.Press, KeyModifiers.None));
        window.RaiseKeyUp(new KeyEventArgs(Key.F3, KeyAction.Release, KeyModifiers.None));
        Assert.Equal(Visibility.Visible, debugInfo.Root.Visibility);

        ui.Close();
        window.RaiseKeyDown(new KeyEventArgs(Key.F3, KeyAction.Press, KeyModifiers.None));
        window.RaiseKeyUp(new KeyEventArgs(Key.F3, KeyAction.Release, KeyModifiers.None));
        Assert.Equal(Visibility.Collapsed, debugInfo.Root.Visibility);
        input.Destroy();
        ui.Destroy();
    }

    private static (InputManager Input, TestWindow Window, UIManager Ui) CreateManager(
        params (string Key, InputAction Action)[] definitions)
    {
        var map = InputBindingMapTests.CreateMap(definitions);
        var window = new TestWindow
        {
            Size = new Vector2D<int>(100, 100),
            CursorState = CursorState.Normal
        };
        var ui = new UIManager();
        var input = new InputManager(window, ui, map);
        return (input, window, ui);
    }

    private static void OpenScene(UIManager manager, GameUIScene scene, Size? size = null)
    {
        manager.Open(scene);
        manager.UpdateFrame(size ?? new Size(100, 100), 0);
    }

    private sealed class TestNode : UIElement
    {
    }

    private sealed class TestControl : Control
    {
    }
}
