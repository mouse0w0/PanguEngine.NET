using PanguEngine.Client.Input;
using PanguEngine.Input;
using PanguEngine.Registries;
using Silk.NET.Maths;

namespace PanguEngine.Tests.Client.Input;

public sealed class InputRouterTests
{
    [Fact]
    public void ActionEventsAreValueTypes()
    {
        Assert.True(typeof(InputActionEvent).IsValueType);
    }

    [Fact]
    public void UiScopePassesToGameAndHandledStopsPropagation()
    {
        var ui = new InputContext(InputScope.Ui);
        var game = new InputContext(InputScope.Game);
        var uiAction = new InputAction(InputValueType.Button,
            [InputBinding.Button("default", ui, Key.E)]);
        var gameAction = new InputAction(InputValueType.Button,
            [InputBinding.Button("default", game, Key.E)]);
        var router = CreateRouter(
            ("test:ui", uiAction),
            ("test:game", gameAction));
        using var uiToken = router.ActivateContext(ui);
        using var gameToken = router.ActivateContext(game);
        var events = new List<string>();
        router.RegisterHandler(uiAction, _ =>
        {
            events.Add("ui");
            return InputHandling.Pass;
        });
        router.RegisterHandler(gameAction, _ =>
        {
            events.Add("game");
            return InputHandling.Handled;
        });

        Press(router, InputSource.FromKey(Key.E), KeyModifiers.None);

        Assert.Equal(["ui", "game"], events);
    }

    [Fact]
    public void NewerContextPassesToEarlierContext()
    {
        var earlier = new InputContext(InputScope.Game);
        var newer = new InputContext(InputScope.Game);
        var earlierAction = new InputAction(InputValueType.Button,
            [InputBinding.Button("default", earlier, Key.E)]);
        var newerAction = new InputAction(InputValueType.Button,
            [InputBinding.Button("default", newer, Key.E)]);
        var router = CreateRouter(
            ("test:earlier", earlierAction),
            ("test:newer", newerAction));
        using var earlierToken = router.ActivateContext(earlier);
        using var newerToken = router.ActivateContext(newer);
        var events = new List<(string Name, InputContext Context)>();
        router.RegisterHandler(newerAction, args =>
        {
            events.Add(("newer", args.Context));
            return InputHandling.Pass;
        });
        router.RegisterHandler(earlierAction, args =>
        {
            events.Add(("earlier", args.Context));
            return InputHandling.Handled;
        });

        Press(router, InputSource.FromKey(Key.E), KeyModifiers.None);

        Assert.Equal(
            [("newer", newer), ("earlier", earlier)],
            events);
    }

    [Fact]
    public void NewerContextCaptureStopsEarlierContext()
    {
        var earlier = new InputContext(InputScope.Game);
        var newer = new InputContext(
            InputScope.Game,
            captureMask: InputCaptureMask.Keyboard);
        var action = new InputAction(InputValueType.Button,
            [InputBinding.Button("default", earlier, Key.E)]);
        var router = CreateRouter(("test:action", action));
        using var earlierToken = router.ActivateContext(earlier);
        using var newerToken = router.ActivateContext(newer);
        var calls = 0;
        router.RegisterHandler(action, _ =>
        {
            calls++;
            return InputHandling.Handled;
        });

        Press(router, InputSource.FromKey(Key.E), KeyModifiers.None);

        Assert.Equal(0, calls);
    }

    [Fact]
    public void CaptureWithdrawalInvokesEveryHandler()
    {
        var game = new InputContext(InputScope.Game);
        var pause = new InputContext(InputScope.Ui, captureMask: InputCaptureMask.All);
        var action = new InputAction(InputValueType.Button,
            [InputBinding.Button("default", game, Key.E)]);
        var router = CreateRouter(("test:action", action));
        using var gameToken = router.ActivateContext(game);
        IDisposable? pauseToken = null;
        var firstHandlerEvents = new List<InputActionPhase>();
        var secondHandlerEvents = new List<InputActionPhase>();
        router.RegisterHandler(action, args =>
        {
            firstHandlerEvents.Add(args.Phase);
            if (args.Phase == InputActionPhase.Started)
                pauseToken = router.ActivateContext(pause);
            return InputHandling.Pass;
        }, priority: 100);
        router.RegisterHandler(action, args =>
        {
            secondHandlerEvents.Add(args.Phase);
            return InputHandling.Pass;
        });

        Press(router, InputSource.FromKey(Key.E), KeyModifiers.None);

        Assert.Equal([InputActionPhase.Started, InputActionPhase.Stopped], firstHandlerEvents);
        Assert.Equal([InputActionPhase.Stopped], secondHandlerEvents);
        pauseToken?.Dispose();
    }

    [Fact]
    public void IntermediateContextCaptureStopsEarlierContext()
    {
        var lower = new InputContext(InputScope.Game);
        var middle = new InputContext(
            InputScope.Game,
            captureMask: InputCaptureMask.Keyboard);
        var upper = new InputContext(InputScope.Game);
        var action = new InputAction(InputValueType.Button,
            [InputBinding.Button("default", lower, Key.E)]);
        var router = CreateRouter(("test:action", action));
        using var lowerToken = router.ActivateContext(lower);
        using var middleToken = router.ActivateContext(middle);
        using var upperToken = router.ActivateContext(upper);
        var calls = 0;
        router.RegisterHandler(action, _ =>
        {
            calls++;
            return InputHandling.Handled;
        });

        Press(router, InputSource.FromKey(Key.E), KeyModifiers.None);

        Assert.Equal(0, calls);
    }

    [Fact]
    public void ContextCaptureStopsLowerLayersWithoutBinding()
    {
        var game = new InputContext(InputScope.Game);
        var modal = new InputContext(InputScope.Ui, captureMask: InputCaptureMask.Keyboard);
        var action = new InputAction(InputValueType.Button,
            [InputBinding.Button("default", game, Key.E)]);
        var router = CreateRouter(("test:action", action));
        using var gameToken = router.ActivateContext(game);
        using var modalToken = router.ActivateContext(modal);
        var calls = 0;
        router.RegisterHandler(action, _ =>
        {
            calls++;
            return InputHandling.Handled;
        });

        Press(router, InputSource.FromKey(Key.E), KeyModifiers.None);

        Assert.Equal(0, calls);
    }

    [Fact]
    public void MultipleContextTokensKeepContextActiveUntilLastRelease()
    {
        var game = new InputContext(InputScope.Game);
        var action = new InputAction(InputValueType.Button,
            [InputBinding.Button("default", game, Key.E)]);
        var router = CreateRouter(("test:action", action));
        using var firstToken = router.ActivateContext(game);
        var phases = new List<InputActionPhase>();
        router.RegisterHandler(action, args =>
        {
            phases.Add(args.Phase);
            return InputHandling.Handled;
        });

        Press(router, InputSource.FromKey(Key.E), KeyModifiers.None);
        using var secondToken = router.ActivateContext(game);
        Assert.True(router.GetValue(action).Button);
        Assert.Equal([InputActionPhase.Started], phases);
        firstToken.Dispose();
        Assert.True(router.GetValue(action).Button);
        Assert.Equal([InputActionPhase.Started], phases);
        Release(router, InputSource.FromKey(Key.E), KeyModifiers.None);
        Press(router, InputSource.FromKey(Key.E), KeyModifiers.None);
        secondToken.Dispose();
        Release(router, InputSource.FromKey(Key.E), KeyModifiers.None);
        Press(router, InputSource.FromKey(Key.E), KeyModifiers.None);

        Assert.Equal(
            [
                InputActionPhase.Started,
                InputActionPhase.Stopped,
                InputActionPhase.Started,
                InputActionPhase.Stopped
            ],
            phases);
    }

    [Fact]
    public void ReleasingAndReactivatingContextBeforeTopologyApplyPreservesOrder()
    {
        var game = new InputContext(InputScope.Game);
        var other = new InputContext(InputScope.Game);
        var action = new InputAction(InputValueType.Button,
            [InputBinding.Button("default", game, Key.E)]);
        var otherAction = new InputAction(InputValueType.Button,
            [InputBinding.Button("default", other, Key.E)]);
        var router = CreateRouter(("test:action", action), ("test:other", otherAction));
        using var initialToken = router.ActivateContext(game);
        using var otherToken = router.ActivateContext(other);
        IDisposable? replacementToken = null;
        var replaced = false;
        var phases = new List<InputActionPhase>();
        var contexts = new List<InputContext>();
        router.RegisterHandler(otherAction, args =>
        {
            if (args.Phase == InputActionPhase.Started)
                contexts.Add(args.Context);
            return InputHandling.Pass;
        });
        router.RegisterHandler(action, args =>
        {
            phases.Add(args.Phase);
            if (args.Phase == InputActionPhase.Started)
                contexts.Add(args.Context);
            if (args.Phase == InputActionPhase.Started && !replaced)
            {
                replaced = true;
                initialToken.Dispose();
                replacementToken = router.ActivateContext(game);
            }
            return InputHandling.Pass;
        });

        Press(router, InputSource.FromKey(Key.E), KeyModifiers.None);
        Assert.True(router.GetValue(action).Button);
        Assert.Equal([InputActionPhase.Started], phases);
        Release(router, InputSource.FromKey(Key.E), KeyModifiers.None);
        Press(router, InputSource.FromKey(Key.E), KeyModifiers.None);

        Assert.Equal(
            [InputActionPhase.Started, InputActionPhase.Stopped, InputActionPhase.Started],
            phases);
        Assert.Equal([other, game, other, game], contexts);
        replacementToken?.Dispose();
    }

    [Fact]
    public void AxisContributionsAggregateAndCancelOnce()
    {
        var game = new InputContext(InputScope.Game);
        var move = new InputAction(InputValueType.Axis2D,
        [
            InputBinding.Axis2D("forward", game, Key.W, new Vector2D<double>(0, 1)),
            InputBinding.Axis2D("right", game, Key.D, new Vector2D<double>(1, 0))
        ]);
        var router = CreateRouter(("test:move", move));
        using var contextToken = router.ActivateContext(game);
        var events = new List<(InputActionPhase Phase, Vector2D<double> Value)>();
        router.RegisterHandler(move, args =>
        {
            events.Add((args.Phase, args.Value.Axis2D));
            return InputHandling.Handled;
        });

        Press(router, InputSource.FromKey(Key.W), KeyModifiers.None);
        Press(router, InputSource.FromKey(Key.D), KeyModifiers.None);
        Release(router, InputSource.FromKey(Key.W), KeyModifiers.None);
        Release(router, InputSource.FromKey(Key.D), KeyModifiers.None);

        Assert.Equal(
            [
                (InputActionPhase.Started, new Vector2D<double>(0, 1)),
                (InputActionPhase.Updated, new Vector2D<double>(1, 1)),
                (InputActionPhase.Updated, new Vector2D<double>(1, 0)),
                (InputActionPhase.Stopped, Vector2D<double>.Zero)
            ],
            events);
    }

    [Fact]
    public void PersistentAndTransientAxisSourcesRemainIndependent()
    {
        var game = new InputContext(InputScope.Game);
        var move = new InputAction(InputValueType.Axis2D,
        [
            InputBinding.Axis2D("forward", game, Key.W, new Vector2D<double>(0, 1)),
            InputBinding.Axis2D(
                "sample",
                game,
                InputSource.MouseMove,
                new Vector2D<double>(1, 1))
        ]);
        var router = CreateRouter(("test:move", move));
        using var contextToken = router.ActivateContext(game);
        var events = new List<(InputActionPhase Phase, Vector2D<double> Value)>();
        router.RegisterHandler(move, args =>
        {
            events.Add((args.Phase, args.Value.Axis2D));
            return InputHandling.Handled;
        });

        Press(router, InputSource.FromKey(Key.W), KeyModifiers.None);
        Sample(
            router,
            InputSource.MouseMove,
            new Vector2D<double>(2, 3),
            KeyModifiers.None,
            false);

        Assert.Equal(new Vector2D<double>(0, 1), router.GetValue(move).Axis2D);

        Release(router, InputSource.FromKey(Key.W), KeyModifiers.None);

        Assert.Equal(
            [
                (InputActionPhase.Started, new Vector2D<double>(0, 1)),
                (InputActionPhase.Updated, new Vector2D<double>(2, 3)),
                (InputActionPhase.Stopped, Vector2D<double>.Zero)
            ],
            events);
    }

    [Fact]
    public void Axis3DContributionsClampPerComponent()
    {
        var game = new InputContext(InputScope.Game);
        var move = new InputAction(InputValueType.Axis3D,
        [
            InputBinding.Axis3D("first", game, Key.W, new Vector3D<double>(1, 2, -2)),
            InputBinding.Axis3D("second", game, Key.D, new Vector3D<double>(1, -2, 0.5))
        ]);
        var router = CreateRouter(("test:move", move));
        using var contextToken = router.ActivateContext(game);
        var values = new List<Vector3D<double>>();
        router.RegisterHandler(move, args =>
        {
            values.Add(args.Value.Axis3D);
            return InputHandling.Handled;
        });

        Press(router, InputSource.FromKey(Key.W), KeyModifiers.None);
        Press(router, InputSource.FromKey(Key.D), KeyModifiers.None);

        Assert.Equal(
            [
                new Vector3D<double>(1, 1, -1),
                new Vector3D<double>(1, 0, -1)
            ],
            values);
    }

    [Fact]
    public void SameSourceBindingsAggregateBeforeSingleActionDispatch()
    {
        var game = new InputContext(InputScope.Game);
        var move = new InputAction(InputValueType.Axis1D,
        [
            InputBinding.Axis1D("first", game, Key.E, 0.25),
            InputBinding.Axis1D("second", game, Key.E, 0.5)
        ]);
        var router = CreateRouter(("test:move", move));
        using var contextToken = router.ActivateContext(game);
        var events = new List<InputActionEvent>();
        router.RegisterHandler(move, args =>
        {
            events.Add(args);
            return InputHandling.Handled;
        });

        Press(router, InputSource.FromKey(Key.E), KeyModifiers.None);

        var started = Assert.Single(events);
        Assert.Equal(InputActionPhase.Started, started.Phase);
        Assert.Equal(0.75, started.Value.Axis1D);
        Assert.Equal(0.75, router.GetValue(move).Axis1D);
    }

    [Fact]
    public void HandledHighPriorityCandidateStopsLowerCandidates()
    {
        var game = new InputContext(InputScope.Game);
        var high = new InputAction(InputValueType.Button,
            [InputBinding.Button("high", game, Key.E, priority: 10)]);
        var low = new InputAction(InputValueType.Button,
            [InputBinding.Button("low", game, Key.E)]);
        var router = CreateRouter(
            ("test:high", high),
            ("test:low", low));
        using var contextToken = router.ActivateContext(game);
        var events = new List<string>();
        router.RegisterHandler(high, _ =>
        {
            events.Add("high");
            return InputHandling.Handled;
        });
        router.RegisterHandler(low, _ =>
        {
            events.Add("low");
            return InputHandling.Handled;
        });

        Press(router, InputSource.FromKey(Key.E), KeyModifiers.None);

        Assert.Equal(["high"], events);
        Assert.True(router.GetValue(high).Button);
        Assert.False(router.GetValue(low).Button);
    }

    [Fact]
    public void MultipleHandlersUsePriorityAndPassContinuesToNextHandler()
    {
        var game = new InputContext(InputScope.Game);
        var action = new InputAction(InputValueType.Button,
            [InputBinding.Button("default", game, Key.E)]);
        var router = CreateRouter(("test:action", action));
        using var contextToken = router.ActivateContext(game);
        var calls = new List<string>();
        router.RegisterHandler(action, _ =>
        {
            calls.Add("high");
            return InputHandling.Pass;
        }, priority: 10);
        router.RegisterHandler(action, _ =>
        {
            calls.Add("low");
            return InputHandling.Handled;
        });

        Press(router, InputSource.FromKey(Key.E), KeyModifiers.None);

        Assert.Equal(["high", "low"], calls);
    }

    [Fact]
    public void HandlerRegistrationIsRejectedAfterInputStarts()
    {
        var game = new InputContext(InputScope.Game);
        var action = new InputAction(InputValueType.Button,
            [InputBinding.Button("default", game, Key.E)]);
        var router = CreateRouter(("test:action", action));
        using var contextToken = router.ActivateContext(game);
        router.RegisterHandler(action, _ => InputHandling.Pass);

        Press(router, InputSource.FromKey(Key.E), KeyModifiers.None);

        Assert.Throws<InvalidOperationException>(() =>
            router.RegisterHandler(action, _ => InputHandling.Pass));
    }

    [Fact]
    public void TopologyChangesUseFrozenHandlersForSyntheticEvents()
    {
        var game = new InputContext(InputScope.Game);
        var pause = new InputContext(InputScope.Ui, captureMask: InputCaptureMask.All);
        var action = new InputAction(InputValueType.Button,
            [InputBinding.Button("default", game, Key.E)]);
        var router = CreateRouter(("test:action", action));
        using var contextToken = router.ActivateContext(game);
        var events = new List<string>();
        IDisposable? pauseActivation = null;
        router.RegisterHandler(action, args =>
        {
            events.Add(args.Phase.ToString());
            if (args.Phase == InputActionPhase.Started)
                pauseActivation = router.ActivateContext(pause);
            return InputHandling.Pass;
        });

        Press(router, InputSource.FromKey(Key.E), KeyModifiers.None);

        Assert.Equal(["Started", "Stopped"], events);
        pauseActivation?.Dispose();
    }

    [Fact]
    public void NoneModifierBindingMatchesAnyCurrentModifiers()
    {
        var game = new InputContext(InputScope.Game);
        var action = new InputAction(InputValueType.Button,
            [InputBinding.Button("default", game, Key.E)]);
        var router = CreateRouter(("test:action", action));
        using var contextToken = router.ActivateContext(game);

        Press(router, InputSource.FromKey(Key.E), KeyModifiers.Control | KeyModifiers.Shift);

        Assert.True(router.GetValue(action).Button);
    }

    [Fact]
    public void ExactModifierBindingSuppressesWildcardForSameActionSource()
    {
        var game = new InputContext(InputScope.Game);
        var action = new InputAction(InputValueType.Axis1D,
        [
            InputBinding.Axis1D("wildcard", game, Key.E, 1),
            InputBinding.Axis1D("shift", game, Key.E, 0.25, KeyModifiers.Shift)
        ]);
        var router = CreateRouter(("test:action", action));
        using var contextToken = router.ActivateContext(game);

        Press(router, InputSource.FromKey(Key.E), KeyModifiers.Shift);

        Assert.Equal(0.25, router.GetValue(action).Axis1D);
    }

    [Fact]
    public void ExactContributionStopsWithoutFallbackUntilMainKeyIsRepressed()
    {
        var game = new InputContext(InputScope.Game);
        var action = new InputAction(InputValueType.Axis1D,
        [
            InputBinding.Axis1D("wildcard", game, Key.W, 1),
            InputBinding.Axis1D("shift", game, Key.W, 0.25, KeyModifiers.Shift)
        ]);
        var router = CreateRouter(("test:action", action));
        using var contextToken = router.ActivateContext(game);
        var events = new List<(InputActionPhase Phase, InputSource? Source)>();
        router.RegisterHandler(action, args =>
        {
            events.Add((args.Phase, args.Source));
            return InputHandling.Handled;
        });

        Press(router, InputSource.FromKey(Key.ShiftLeft), KeyModifiers.Shift);
        Press(router, InputSource.FromKey(Key.W), KeyModifiers.Shift);
        Press(
            router,
            InputSource.FromKey(Key.ControlLeft),
            KeyModifiers.Shift | KeyModifiers.Control,
            isRepeat: false);
        Release(router, InputSource.FromKey(Key.ControlLeft), KeyModifiers.Shift);

        Assert.True(router.GetValue(action).IsZero);

        Release(router, InputSource.FromKey(Key.W), KeyModifiers.Shift);
        Press(router, InputSource.FromKey(Key.W), KeyModifiers.Shift);

        Assert.Equal(
            [
                (InputActionPhase.Started, (InputSource?)InputSource.FromKey(Key.W)),
                (InputActionPhase.Stopped, (InputSource?)InputSource.FromKey(Key.ControlLeft)),
                (InputActionPhase.Started, (InputSource?)InputSource.FromKey(Key.W))
            ],
            events);
    }

    [Fact]
    public void ExactContributionStopsWhenRequiredModifierIsReleased()
    {
        var game = new InputContext(InputScope.Game);
        var action = new InputAction(InputValueType.Button,
            [InputBinding.Button("shift", game, Key.E, KeyModifiers.Shift)]);
        var router = CreateRouter(("test:action", action));
        using var contextToken = router.ActivateContext(game);
        var events = new List<(InputActionPhase Phase, InputSource? Source)>();
        router.RegisterHandler(action, args =>
        {
            events.Add((args.Phase, args.Source));
            return InputHandling.Handled;
        });

        Press(router, InputSource.FromKey(Key.ShiftLeft), KeyModifiers.Shift);
        Press(router, InputSource.FromKey(Key.E), KeyModifiers.Shift);
        Release(router, InputSource.FromKey(Key.ShiftLeft), KeyModifiers.None);

        Assert.Equal(
            [
                (InputActionPhase.Started, (InputSource?)InputSource.FromKey(Key.E)),
                (InputActionPhase.Stopped, (InputSource?)InputSource.FromKey(Key.ShiftLeft))
            ],
            events);
    }

    [Fact]
    public void ModifierPressedAfterMainKeyDoesNotActivateExactBinding()
    {
        var game = new InputContext(InputScope.Game);
        var action = new InputAction(InputValueType.Button,
            [InputBinding.Button("shift", game, Key.E, KeyModifiers.Shift)]);
        var router = CreateRouter(("test:action", action));
        using var contextToken = router.ActivateContext(game);

        Press(router, InputSource.FromKey(Key.E), KeyModifiers.None);
        Press(router, InputSource.FromKey(Key.ShiftLeft), KeyModifiers.Shift);

        Assert.False(router.GetValue(action).Button);
    }

    [Fact]
    public void WildcardContributionSurvivesLaterModifierChanges()
    {
        var game = new InputContext(InputScope.Game);
        var action = new InputAction(InputValueType.Button,
            [InputBinding.Button("wildcard", game, Key.E)]);
        var router = CreateRouter(("test:action", action));
        using var contextToken = router.ActivateContext(game);

        Press(router, InputSource.FromKey(Key.E), KeyModifiers.None);
        Press(router, InputSource.FromKey(Key.ShiftLeft), KeyModifiers.Shift);
        Release(router, InputSource.FromKey(Key.ShiftLeft), KeyModifiers.None);

        Assert.True(router.GetValue(action).Button);
    }

    [Fact]
    public void WildcardAndExactBindingsOnDifferentActionsRouteIndependently()
    {
        var game = new InputContext(InputScope.Game);
        var wildcard = new InputAction(InputValueType.Button,
            [InputBinding.Button("wildcard", game, Key.E, priority: 10)]);
        var exact = new InputAction(InputValueType.Button,
            [InputBinding.Button("shift", game, Key.E, KeyModifiers.Shift)]);
        var router = CreateRouter(
            ("test:wildcard", wildcard),
            ("test:exact", exact));
        using var contextToken = router.ActivateContext(game);
        var events = new List<string>();
        router.RegisterHandler(wildcard, _ =>
        {
            events.Add("wildcard");
            return InputHandling.Pass;
        });
        router.RegisterHandler(exact, _ =>
        {
            events.Add("exact");
            return InputHandling.Handled;
        });

        Press(router, InputSource.FromKey(Key.E), KeyModifiers.Shift);

        Assert.Equal(["wildcard", "exact"], events);
    }

    [Fact]
    public void TransientSamplesMatchCurrentModifiersIndependently()
    {
        var game = new InputContext(InputScope.Game);
        var look = new InputAction(InputValueType.Axis2D,
        [
            InputBinding.Axis2D(
                "wildcard",
                game,
                InputSource.MouseMove,
                new Vector2D<double>(1, 1)),
            InputBinding.Axis2D(
                "shift",
                game,
                InputSource.MouseMove,
                new Vector2D<double>(2, 3),
                KeyModifiers.Shift)
        ]);
        var router = CreateRouter(("test:look", look));
        using var contextToken = router.ActivateContext(game);
        var values = new List<Vector2D<double>>();
        router.RegisterHandler(look, args =>
        {
            values.Add(args.Value.Axis2D);
            return InputHandling.Handled;
        });

        Sample(
            router,
            InputSource.MouseMove,
            new Vector2D<double>(1, 1),
            KeyModifiers.None,
            false);
        Sample(
            router,
            InputSource.MouseMove,
            new Vector2D<double>(1, 1),
            KeyModifiers.Shift,
            false);

        Assert.Equal(
            [new Vector2D<double>(1, 1), new Vector2D<double>(2, 3)],
            values);
    }

    [Fact]
    public void SampleModifierSnapshotWithdrawsExactContribution()
    {
        var game = new InputContext(InputScope.Game);
        var action = new InputAction(InputValueType.Button,
            [InputBinding.Button("shift", game, Key.W, KeyModifiers.Shift)]);
        var router = CreateRouter(("test:action", action));
        using var contextToken = router.ActivateContext(game);
        InputActionEvent? stopped = null;
        router.RegisterHandler(action, args =>
        {
            if (args.Phase == InputActionPhase.Stopped)
                stopped = args;
            return InputHandling.Handled;
        });

        Press(router, InputSource.FromKey(Key.ShiftLeft), KeyModifiers.Shift);
        Press(router, InputSource.FromKey(Key.W), KeyModifiers.Shift);
        Sample(
            router,
            InputSource.MouseMove,
            new Vector2D<double>(1, 1),
            KeyModifiers.Shift | KeyModifiers.Control,
            false);

        Assert.True(stopped.HasValue);
        Assert.Equal(InputSource.MouseMove, stopped.Value.Source);
        Assert.False(router.GetValue(action).Button);
    }

    [Fact]
    public void HandledModifierMismatchStillMaintainsEveryAction()
    {
        var game = new InputContext(InputScope.Game);
        var first = new InputAction(InputValueType.Button,
            [InputBinding.Button("shift", game, Key.W, KeyModifiers.Shift, priority: 10)]);
        var second = new InputAction(InputValueType.Button,
            [InputBinding.Button("shift", game, Key.W, KeyModifiers.Shift)]);
        var router = CreateRouter(
            ("test:first", first),
            ("test:second", second));
        using var contextToken = router.ActivateContext(game);
        var firstStopped = 0;
        var secondStopped = 0;
        router.RegisterHandler(first, args =>
        {
            if (args.Phase == InputActionPhase.Stopped)
            {
                firstStopped++;
                return InputHandling.Handled;
            }
            return InputHandling.Pass;
        });
        router.RegisterHandler(second, args =>
        {
            if (args.Phase == InputActionPhase.Stopped)
            {
                secondStopped++;
                return InputHandling.Handled;
            }
            return InputHandling.Pass;
        });

        Press(router, InputSource.FromKey(Key.ShiftLeft), KeyModifiers.Shift);
        Press(router, InputSource.FromKey(Key.W), KeyModifiers.Shift);
        Press(router, InputSource.FromKey(Key.ControlLeft), KeyModifiers.Shift | KeyModifiers.Control);

        Assert.Equal(1, firstStopped);
        Assert.Equal(1, secondStopped);
        Assert.False(router.GetValue(first).Button);
        Assert.False(router.GetValue(second).Button);
    }

    [Fact]
    public void BindingChangeCancelsOnlyChangedSlotContribution()
    {
        var game = new InputContext(InputScope.Game);
        var move = new InputAction(InputValueType.Axis2D,
        [
            InputBinding.Axis2D("forward", game, Key.W, new Vector2D<double>(0, 1)),
            InputBinding.Axis2D("right", game, Key.D, new Vector2D<double>(1, 0))
        ]);
        var map = InputBindingMapTests.CreateMap(("test:move", move));
        var router = new InputRouter(map);
        using var contextToken = router.ActivateContext(game);
        var events = new List<(InputActionPhase Phase, Vector2D<double> Value, InputSource? Source)>();
        router.RegisterHandler(move, args =>
        {
            events.Add((args.Phase, args.Value.Axis2D, args.Source));
            return InputHandling.Handled;
        });
        Press(router, InputSource.FromKey(Key.W), KeyModifiers.None);
        Press(router, InputSource.FromKey(Key.D), KeyModifiers.None);

        map.SetBinding(
            ResourceKey.Parse("test:move/forward"),
            InputSource.FromKey(Key.Up),
            KeyModifiers.None);

        Assert.Equal(new Vector2D<double>(1, 0), router.GetValue(move).Axis2D);
        Assert.Equal(
            (InputActionPhase.Updated, new Vector2D<double>(1, 0), (InputSource?)null),
            events[^1]);
    }

    [Fact]
    public void BindingChangeSuppressionDoesNotBlockUnchangedSourceRelease()
    {
        var game = new InputContext(InputScope.Game);
        var changedAction = new InputAction(InputValueType.Button,
            [InputBinding.Button("changed", game, Key.W, priority: 10)]);
        var unchangedAction = new InputAction(InputValueType.Button,
            [InputBinding.Button("unchanged", game, Key.W)]);
        var map = InputBindingMapTests.CreateMap(
            ("test:changed", changedAction),
            ("test:unchanged", unchangedAction));
        var router = new InputRouter(map);
        using var contextToken = router.ActivateContext(game);
        var changedPhases = new List<InputActionPhase>();
        var unchangedPhases = new List<InputActionPhase>();
        router.RegisterHandler(changedAction, args =>
        {
            changedPhases.Add(args.Phase);
            return InputHandling.Pass;
        });
        router.RegisterHandler(unchangedAction, args =>
        {
            unchangedPhases.Add(args.Phase);
            return InputHandling.Handled;
        });

        Press(router, InputSource.FromKey(Key.W), KeyModifiers.None);
        map.SetBinding(
            ResourceKey.Parse("test:changed/changed"),
            InputSource.FromKey(Key.Up),
            KeyModifiers.None);
        Release(router, InputSource.FromKey(Key.W), KeyModifiers.None);

        Assert.Equal([InputActionPhase.Started, InputActionPhase.Stopped], changedPhases);
        Assert.Equal([InputActionPhase.Started, InputActionPhase.Stopped], unchangedPhases);
    }

    [Fact]
    public void ContextChangeMergesWithQueuedBindingChange()
    {
        var game = new InputContext(InputScope.Game);
        var pause = new InputContext(InputScope.Ui, captureMask: InputCaptureMask.All);
        var action = new InputAction(InputValueType.Button,
            [InputBinding.Button("default", game, Key.E)]);
        var map = InputBindingMapTests.CreateMap(("test:action", action));
        var router = new InputRouter(map);
        using var contextToken = router.ActivateContext(game);
        var phases = new List<InputActionPhase>();
        IDisposable? pauseActivation = null;
        router.RegisterHandler(action, args =>
        {
            phases.Add(args.Phase);
            if (args.Phase == InputActionPhase.Started)
            {
                map.SetBinding(
                    ResourceKey.Parse("test:action/default"),
                    InputSource.FromKey(Key.F),
                    KeyModifiers.None);
                pauseActivation = router.ActivateContext(pause);
            }
            return InputHandling.Pass;
        });

        Press(router, InputSource.FromKey(Key.E), KeyModifiers.None);

        Assert.Equal([InputActionPhase.Started, InputActionPhase.Stopped], phases);
        pauseActivation?.Dispose();
    }

    [Fact]
    public void CombinedQueuedChangesUseFrozenHandlersForSingleTargetedMaintenance()
    {
        var game = new InputContext(InputScope.Game);
        var pause = new InputContext(InputScope.Ui, captureMask: InputCaptureMask.All);
        var action = new InputAction(InputValueType.Button,
            [InputBinding.Button("default", game, Key.E)]);
        var map = InputBindingMapTests.CreateMap(("test:action", action));
        var router = new InputRouter(map);
        using var contextToken = router.ActivateContext(game);
        var phases = new List<InputActionPhase>();
        IDisposable? pauseActivation = null;
        var changed = false;
        router.RegisterHandler(action, args =>
        {
            phases.Add(args.Phase);
            if (args.Phase == InputActionPhase.Started && !changed)
            {
                changed = true;
                map.SetBinding(
                    ResourceKey.Parse("test:action/default"),
                    InputSource.FromKey(Key.F),
                    KeyModifiers.None);
                pauseActivation = router.ActivateContext(pause);
            }
            return InputHandling.Pass;
        });

        Press(router, InputSource.FromKey(Key.E), KeyModifiers.None);
        Release(router, InputSource.FromKey(Key.E), KeyModifiers.None);
        pauseActivation?.Dispose();
        Press(router, InputSource.FromKey(Key.F), KeyModifiers.None);

        Assert.Equal(
            [InputActionPhase.Started, InputActionPhase.Stopped, InputActionPhase.Started],
            phases);
    }

    [Fact]
    public void CapturingContextWithdrawsHeldSourceUntilRelease()
    {
        var game = new InputContext(InputScope.Game);
        var pause = new InputContext(InputScope.Ui, captureMask: InputCaptureMask.All);
        var move = new InputAction(InputValueType.Axis1D,
            [InputBinding.Axis1D("default", game, Key.W, 1)]);
        var router = CreateRouter(("test:move", move));
        using var gameToken = router.ActivateContext(game);
        var events = new List<(InputActionPhase Phase, InputSource? Source)>();
        router.RegisterHandler(move, args =>
        {
            events.Add((args.Phase, args.Source));
            return InputHandling.Handled;
        });
        Press(router, InputSource.FromKey(Key.W), KeyModifiers.None);

        using var pauseToken = router.ActivateContext(pause);
        Release(router, InputSource.FromKey(Key.W), KeyModifiers.None);

        Assert.Equal(
            [
                (InputActionPhase.Started, (InputSource?)InputSource.FromKey(Key.W)),
                (InputActionPhase.Stopped, null)
            ],
            events);
    }

    [Fact]
    public void CaptureWithdrawalDispatchesEveryActionAfterHandledEvent()
    {
        var game = new InputContext(InputScope.Game);
        var pause = new InputContext(InputScope.Ui, captureMask: InputCaptureMask.All);
        var first = new InputAction(InputValueType.Button,
            [InputBinding.Button("first", game, Key.E)]);
        var second = new InputAction(InputValueType.Button,
            [InputBinding.Button("second", game, Key.W)]);
        var router = CreateRouter(
            ("test:first", first),
            ("test:second", second));
        using var contextToken = router.ActivateContext(game);
        var firstEvents = new List<InputActionPhase>();
        var secondEvents = new List<InputActionPhase>();
        router.RegisterHandler(first, args =>
        {
            firstEvents.Add(args.Phase);
            return InputHandling.Handled;
        });
        router.RegisterHandler(second, args =>
        {
            secondEvents.Add(args.Phase);
            return InputHandling.Handled;
        });

        Press(router, InputSource.FromKey(Key.E), KeyModifiers.None);
        Press(router, InputSource.FromKey(Key.W), KeyModifiers.None);
        using var pauseToken = router.ActivateContext(pause);

        Assert.Equal([InputActionPhase.Started, InputActionPhase.Stopped], firstEvents);
        Assert.Equal([InputActionPhase.Started, InputActionPhase.Stopped], secondEvents);
    }

    [Fact]
    public void MostRecentlyActivatedContextHandlesInputFirst()
    {
        var first = new InputContext(InputScope.Game);
        var second = new InputContext(InputScope.Game);
        var firstAction = new InputAction(InputValueType.Button,
            [InputBinding.Button("default", first, Key.E)]);
        var secondAction = new InputAction(InputValueType.Button,
            [InputBinding.Button("default", second, Key.E)]);
        var router = CreateRouter(
            ("test:first", firstAction),
            ("test:second", secondAction));
        using var firstToken = router.ActivateContext(first);
        using var secondToken = router.ActivateContext(second);
        var events = new List<string>();
        router.RegisterHandler(firstAction, _ =>
        {
            events.Add("first");
            return InputHandling.Handled;
        });
        router.RegisterHandler(secondAction, _ =>
        {
            events.Add("second");
            return InputHandling.Handled;
        });

        Press(router, InputSource.FromKey(Key.E), KeyModifiers.None);

        Assert.Equal(["second"], events);
        Release(router, InputSource.FromKey(Key.E), KeyModifiers.None);
        secondToken.Dispose();
        events.Clear();
        Press(router, InputSource.FromKey(Key.E), KeyModifiers.None);
        Assert.Equal(["first"], events);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UiCapturesBeforeLaterActivatedGameContext(bool transient)
    {
        var otherGame = new InputContext(InputScope.Game);
        var action = new InputAction(InputValueType.Axis1D,
        [
            InputBinding.Axis1D("key", otherGame, Key.E, 1),
            InputBinding.Axis1D("wheel", otherGame, InputSource.MouseWheel, new Vector2D<double>(0, 1))
        ]);
        var router = CreateRouter(("test:action", action));
        using var uiToken = router.ActivateContext(BuiltinInputContexts.Ui);
        using var gameToken = router.ActivateContext(otherGame);
        var calls = 0;
        router.RegisterHandler(action, _ =>
        {
            calls++;
            return InputHandling.Handled;
        });

        if (transient)
            Sample(router, InputSource.MouseWheel, new Vector2D<double>(0, 1), KeyModifiers.None, false);
        else
            Press(router, InputSource.FromKey(Key.E), KeyModifiers.None);

        Assert.Equal(0, calls);
        Assert.Equal(InputActionValue.Zero, router.GetValue(action));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ContextTokensPreserveOrderUntilLastRelease(bool middleHandles)
    {
        var first = new InputContext(InputScope.Ui);
        var middle = new InputContext(InputScope.Ui);
        var last = new InputContext(InputScope.Ui);
        var action = new InputAction(InputValueType.Axis1D,
        [
            InputBinding.Axis1D("first", first, InputSource.MouseWheel, new Vector2D<double>(0, 1)),
            InputBinding.Axis1D("middle", middle, InputSource.MouseWheel, new Vector2D<double>(0, 1)),
            InputBinding.Axis1D("last", last, InputSource.MouseWheel, new Vector2D<double>(0, 1))
        ]);
        var router = CreateRouter(("test:action", action));
        using var firstToken = router.ActivateContext(first);
        using var middleToken = router.ActivateContext(middle);
        using var lastToken = router.ActivateContext(last);
        using var repeatedToken = router.ActivateContext(first);
        var contexts = new List<InputContext>();
        router.RegisterHandler(action, args =>
        {
            contexts.Add(args.Context);
            return middleHandles && ReferenceEquals(args.Context, middle)
                ? InputHandling.Handled
                : InputHandling.Pass;
        });

        Sample(router, InputSource.MouseWheel, new Vector2D<double>(0, 1), KeyModifiers.None, false);

        InputContext[] expected = middleHandles ? [last, middle] : [last, middle, first];
        Assert.Equal(expected, contexts);
        firstToken.Dispose();
        contexts.Clear();
        Sample(router, InputSource.MouseWheel, new Vector2D<double>(0, 1), KeyModifiers.None, false);
        Assert.Equal(expected, contexts);

        middleToken.Dispose();
        contexts.Clear();
        Sample(router, InputSource.MouseWheel, new Vector2D<double>(0, 1), KeyModifiers.None, false);
        Assert.Equal([last, first], contexts);

        repeatedToken.Dispose();
        contexts.Clear();
        Sample(router, InputSource.MouseWheel, new Vector2D<double>(0, 1), KeyModifiers.None, false);
        Assert.Equal([last], contexts);

        using var reactivatedToken = router.ActivateContext(first);
        contexts.Clear();
        Sample(router, InputSource.MouseWheel, new Vector2D<double>(0, 1), KeyModifiers.None, false);
        Assert.Equal([first, last], contexts);
    }

    [Fact]
    public void AllUiContextsRunBeforeLaterActivatedGameContexts()
    {
        var firstGame = new InputContext(InputScope.Game);
        var secondGame = new InputContext(InputScope.Game);
        var firstUi = new InputContext(InputScope.Ui);
        var secondUi = new InputContext(InputScope.Ui);
        var action = new InputAction(InputValueType.Button,
        [
            InputBinding.Button("first_game", firstGame, Key.E),
            InputBinding.Button("second_game", secondGame, Key.E),
            InputBinding.Button("first_ui", firstUi, Key.E),
            InputBinding.Button("second_ui", secondUi, Key.E)
        ]);
        var router = CreateRouter(("test:action", action));
        using var firstGameToken = router.ActivateContext(firstGame);
        using var firstToken = router.ActivateContext(firstUi);
        using var secondToken = router.ActivateContext(secondUi);
        using var secondGameToken = router.ActivateContext(secondGame);
        var contexts = new List<InputContext>();
        router.RegisterHandler(action, args =>
        {
            contexts.Add(args.Context);
            return InputHandling.Pass;
        });

        Press(router, InputSource.FromKey(Key.E), KeyModifiers.None);

        Assert.Equal([secondUi, firstUi, secondGame, firstGame], contexts);
    }

    [Fact]
    public void ActivationFailurePropagatesWithoutRetry()
    {
        var game = new InputContext(InputScope.Game);
        var modal = new InputContext(InputScope.Ui, captureMask: InputCaptureMask.All);
        var action = new InputAction(InputValueType.Button,
            [InputBinding.Button("default", game, Key.E)]);
        var router = CreateRouter(("test:action", action));
        using var gameToken = router.ActivateContext(game);
        var expected = new InvalidOperationException("invalidation failed");
        var notifications = 0;
        Action failInvalidation = () =>
        {
            notifications++;
            throw expected;
        };
        router.StateInvalidated += failInvalidation;
        try
        {
            Assert.Same(expected, Assert.Throws<InvalidOperationException>(() => router.ActivateContext(modal)));
            Assert.Equal(1, notifications);
        }
        finally
        {
            router.StateInvalidated -= failInvalidation;
            router.Destroy();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ContextReleaseCompletesAfterStopThrows(bool otherContextActive)
    {
        var other = new InputContext(InputScope.Game);
        var context = new InputContext(InputScope.Ui);
        var action = new InputAction(InputValueType.Button,
            [InputBinding.Button("default", context, Key.E)]);
        var router = CreateRouter(("test:action", action));
        using var otherToken = otherContextActive ? router.ActivateContext(other) : null;
        var contextToken = router.ActivateContext(context);
        var expected = new InvalidOperationException("stop failed");
        router.RegisterHandler(action, args =>
        {
            if (args.Phase == InputActionPhase.Stopped)
                throw expected;
            return InputHandling.Handled;
        });
        Press(router, InputSource.FromKey(Key.E), KeyModifiers.None);

        Assert.Same(expected, Assert.Throws<InvalidOperationException>(contextToken.Dispose));
        contextToken.Dispose();
        router.Destroy();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NonCapturingContextDoesNotInheritHeldKey(bool sameSource)
    {
        var game = new InputContext(InputScope.Game);
        var overlay = new InputContext(InputScope.Ui);
        var gameAction = new InputAction(InputValueType.Axis1D,
            [InputBinding.Axis1D("game", game, Key.W, 1)]);
        var overlayAction = new InputAction(InputValueType.Axis1D,
            [InputBinding.Axis1D("overlay", overlay, sameSource ? Key.W : Key.Q, 1)]);
        var router = CreateRouter(("test:game", gameAction), ("test:overlay", overlayAction));
        using var gameToken = router.ActivateContext(game);
        var overlayPhases = new List<InputActionPhase>();
        router.RegisterHandler(overlayAction, args =>
        {
            overlayPhases.Add(args.Phase);
            return InputHandling.Pass;
        });

        Press(router, InputSource.FromKey(Key.W), KeyModifiers.None);
        using var overlayToken = router.ActivateContext(overlay);

        Assert.Equal(1.0, router.GetValue(gameAction).Axis1D);
        Assert.Empty(overlayPhases);
        Assert.True(router.GetValue(overlayAction).IsZero);
        Press(router, InputSource.FromKey(Key.W), KeyModifiers.None);
        Assert.Empty(overlayPhases);
        Release(router, InputSource.FromKey(Key.W), KeyModifiers.None);
        Press(router, InputSource.FromKey(sameSource ? Key.W : Key.Q), KeyModifiers.None);
        Assert.Equal([InputActionPhase.Started], overlayPhases);
        Assert.Equal(1.0, router.GetValue(overlayAction).Axis1D);
    }

    [Fact]
    public void ReleasingOneContextWithdrawsOnlyItsOwnHeldSource()
    {
        var game = new InputContext(InputScope.Game);
        var other = new InputContext(InputScope.Game);
        var action = new InputAction(InputValueType.Axis1D,
        [
            InputBinding.Axis1D("game", game, Key.W, 1),
            InputBinding.Axis1D("other", other, Key.W, 1)
        ]);
        var router = CreateRouter(("test:action", action));
        using var gameToken = router.ActivateContext(game);
        using var otherToken = router.ActivateContext(other);
        var events = new List<(InputActionPhase Phase, InputContext Context)>();
        router.RegisterHandler(action, args =>
        {
            events.Add((args.Phase, args.Context));
            return InputHandling.Pass;
        });

        Press(router, InputSource.FromKey(Key.W), KeyModifiers.None);
        otherToken.Dispose();
        Assert.Equal(1.0, router.GetValue(action).Axis1D);

        Release(router, InputSource.FromKey(Key.W), KeyModifiers.None);

        Assert.Equal(
            [
                (InputActionPhase.Started, other),
                (InputActionPhase.Started, game),
                (InputActionPhase.Stopped, other),
                (InputActionPhase.Stopped, game)
            ],
            events);
    }

    [Fact]
    public void CaptureWithdrawsOnlyCapturedCategoryContribution()
    {
        var game = new InputContext(InputScope.Game);
        var modal = new InputContext(InputScope.Ui, captureMask: InputCaptureMask.Keyboard);
        var action = new InputAction(InputValueType.Axis1D,
        [
            InputBinding.Axis1D("key", game, Key.W, 0.4),
            InputBinding.Axis1D("mouse", game, MouseButton.Left, 0.3)
        ]);
        var router = CreateRouter(("test:action", action));
        using var gameToken = router.ActivateContext(game);
        var events = new List<(InputActionPhase Phase, double Value)>();
        router.RegisterHandler(action, args =>
        {
            events.Add((args.Phase, args.Value.Axis1D));
            return InputHandling.Pass;
        });

        Press(router, InputSource.FromKey(Key.W), KeyModifiers.None);
        Press(router, InputSource.FromMouseButton(MouseButton.Left), KeyModifiers.None);
        using var modalToken = router.ActivateContext(modal);

        Assert.Equal(0.3, router.GetValue(action).Axis1D, 3);
        Assert.Equal(InputActionPhase.Updated, events[^1].Phase);
        Assert.Equal(0.3, events[^1].Value, 3);
    }

    [Fact]
    public void KeyboardCaptureKeepsCapturerAndHigherContextState()
    {
        var lower = new InputContext(InputScope.Game);
        var capturer = new InputContext(InputScope.Game, captureMask: InputCaptureMask.Keyboard);
        var ui = new InputContext(InputScope.Ui);
        var lowerAction = new InputAction(InputValueType.Axis1D,
            [InputBinding.Axis1D("lower", lower, Key.W, 1)]);
        var capturerAction = new InputAction(InputValueType.Axis1D,
            [InputBinding.Axis1D("capturer", capturer, Key.W, 1)]);
        var uiAction = new InputAction(InputValueType.Axis1D,
            [InputBinding.Axis1D("ui", ui, Key.W, 1)]);
        var router = CreateRouter(
            ("test:lower", lowerAction),
            ("test:capturer", capturerAction),
            ("test:ui", uiAction));
        using var lowerToken = router.ActivateContext(lower);
        using var capturerToken = router.ActivateContext(capturer);
        using var uiToken = router.ActivateContext(ui);
        var lowerEvents = new List<InputActionPhase>();
        var capturerEvents = new List<InputActionPhase>();
        var uiEvents = new List<InputActionPhase>();
        router.RegisterHandler(lowerAction, args =>
        {
            lowerEvents.Add(args.Phase);
            return InputHandling.Pass;
        });
        router.RegisterHandler(capturerAction, args =>
        {
            capturerEvents.Add(args.Phase);
            return InputHandling.Pass;
        });
        router.RegisterHandler(uiAction, args =>
        {
            uiEvents.Add(args.Phase);
            return InputHandling.Pass;
        });

        Press(router, InputSource.FromKey(Key.W), KeyModifiers.None);
        using var unrelatedToken = router.ActivateContext(new InputContext(InputScope.Game));

        Assert.Equal([InputActionPhase.Started], uiEvents);
        Assert.Equal([InputActionPhase.Started], capturerEvents);
        Assert.Empty(lowerEvents);
        Assert.Equal(1.0, router.GetValue(uiAction).Axis1D);
        Assert.Equal(1.0, router.GetValue(capturerAction).Axis1D);
        Assert.True(router.GetValue(lowerAction).IsZero);
    }

    [Fact]
    public void RemovingCaptureDoesNotRestoreHeldSource()
    {
        var game = new InputContext(InputScope.Game);
        var modal = new InputContext(InputScope.Ui, captureMask: InputCaptureMask.Keyboard);
        var action = new InputAction(InputValueType.Axis1D,
            [InputBinding.Axis1D("default", game, Key.W, 1)]);
        var router = CreateRouter(("test:action", action));
        using var gameToken = router.ActivateContext(game);
        var events = new List<InputActionPhase>();
        router.RegisterHandler(action, args =>
        {
            events.Add(args.Phase);
            return InputHandling.Pass;
        });

        Press(router, InputSource.FromKey(Key.W), KeyModifiers.None);
        var modalToken = router.ActivateContext(modal);
        Assert.True(router.GetValue(action).IsZero);
        modalToken.Dispose();
        Assert.True(router.GetValue(action).IsZero);
        Release(router, InputSource.FromKey(Key.W), KeyModifiers.None);
        Press(router, InputSource.FromKey(Key.W), KeyModifiers.None);

        Assert.Equal(
            [InputActionPhase.Started, InputActionPhase.Stopped, InputActionPhase.Started],
            events);
    }

    [Fact]
    public void CombinedCaptureAndBindingChangeUsesSingleMaintenanceEvent()
    {
        var game = new InputContext(InputScope.Game);
        var modal = new InputContext(InputScope.Ui, captureMask: InputCaptureMask.Keyboard);
        var combined = new InputAction(InputValueType.Axis1D,
        [
            InputBinding.Axis1D("key", game, Key.W, 0.4),
            InputBinding.Axis1D("mouse", game, MouseButton.Left, 0.3)
        ]);
        var other = new InputAction(InputValueType.Axis1D,
            [InputBinding.Axis1D("other", game, MouseButton.Right, 1)]);
        var map = InputBindingMapTests.CreateMap(("test:combined", combined), ("test:other", other));
        var router = new InputRouter(map);
        using var gameToken = router.ActivateContext(game);
        var combinedEvents = new List<(InputActionPhase Phase, double Value)>();
        var otherEvents = new List<InputActionPhase>();
        IDisposable? modalToken = null;
        router.RegisterHandler(combined, args =>
        {
            combinedEvents.Add((args.Phase, args.Value.Axis1D));
            return InputHandling.Pass;
        });
        router.RegisterHandler(other, args =>
        {
            otherEvents.Add(args.Phase);
            if (args.Phase == InputActionPhase.Started && modalToken is null)
            {
                map.SetBinding(
                    ResourceKey.Parse("test:combined/mouse"),
                    InputSource.FromMouseButton(MouseButton.Middle),
                    KeyModifiers.None);
                modalToken = router.ActivateContext(modal);
            }
            return InputHandling.Pass;
        });

        Press(router, InputSource.FromKey(Key.W), KeyModifiers.None);
        Press(router, InputSource.FromMouseButton(MouseButton.Left), KeyModifiers.None);
        Press(router, InputSource.FromMouseButton(MouseButton.Right), KeyModifiers.None);

        Assert.Equal([InputActionPhase.Started], otherEvents);
        Assert.Equal(1.0, router.GetValue(other).Axis1D);
        Assert.Equal(3, combinedEvents.Count);
        Assert.Equal(InputActionPhase.Stopped, combinedEvents[^1].Phase);
        Assert.Equal(0.0, combinedEvents[^1].Value, 3);
        Release(router, InputSource.FromMouseButton(MouseButton.Left), KeyModifiers.None);
        Press(router, InputSource.FromMouseButton(MouseButton.Middle), KeyModifiers.None);
        Assert.Equal(0.3, router.GetValue(combined).Axis1D, 3);
        Assert.Equal(1.0, router.GetValue(other).Axis1D);
        modalToken?.Dispose();
    }

    [Fact]
    public void BindingCancellationStopsBeforeProcessingQueuedContextChange()
    {
        var game = new InputContext(InputScope.Game);
        var modal = new InputContext(InputScope.Ui, captureMask: InputCaptureMask.All);
        var first = new InputAction(InputValueType.Button,
            [InputBinding.Button("default", game, Key.E)]);
        var second = new InputAction(InputValueType.Button,
            [InputBinding.Button("default", game, Key.F)]);
        var map = InputBindingMapTests.CreateMap(("test:first", first), ("test:second", second));
        var router = new InputRouter(map);
        using var gameToken = router.ActivateContext(game);
        IDisposable? modalToken = null;
        var expected = new InvalidOperationException("stop failed");
        router.RegisterHandler(first, args =>
        {
            if (args.Phase == InputActionPhase.Stopped)
            {
                modalToken = router.ActivateContext(modal);
                throw expected;
            }
            return InputHandling.Pass;
        });
        var secondPhases = new List<InputActionPhase>();
        router.RegisterHandler(second, args =>
        {
            secondPhases.Add(args.Phase);
            return InputHandling.Pass;
        });
        Press(router, InputSource.FromKey(Key.E), KeyModifiers.None);
        Press(router, InputSource.FromKey(Key.F), KeyModifiers.None);

        Assert.Same(expected, Assert.Throws<InvalidOperationException>(() =>
            map.SetBinding(ResourceKey.Parse("test:first/default"), InputSource.FromKey(Key.G), KeyModifiers.None)));

        Assert.Equal([InputActionPhase.Started], secondPhases);
        Assert.True(router.GetValue(second).Button);
        modalToken?.Dispose();
        router.Destroy();
    }

    private static InputRouter CreateRouter(
        params (string Key, InputAction Action)[] actions) =>
        new(InputBindingMapTests.CreateMap(actions));

    private static void Press(
        InputRouter router,
        InputSource source,
        KeyModifiers modifiers,
        bool isRepeat = false)
    {
        router.FreezeHandlers();
        router.BeginInputEvent();
        try
        {
            router.RecordPress(source, modifiers);
            router.RoutePress(source, isRepeat);
        }
        finally
        {
            router.EndInputEvent();
        }
    }

    private static void Release(
        InputRouter router,
        InputSource source,
        KeyModifiers modifiers)
    {
        router.FreezeHandlers();
        router.BeginInputEvent();
        try
        {
            router.RecordRelease(source, modifiers);
            router.RouteRelease(source);
        }
        finally
        {
            router.EndInputEvent();
        }
    }

    private static void Sample(
        InputRouter router,
        InputSource source,
        Vector2D<double> sample,
        KeyModifiers modifiers,
        bool skipNewRouting)
    {
        router.FreezeHandlers();
        router.BeginInputEvent();
        try
        {
            router.RouteSample(source, sample, modifiers, skipNewRouting);
        }
        finally
        {
            router.EndInputEvent();
        }
    }
}
