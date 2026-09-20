using System.Runtime.ExceptionServices;
using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Drawing;
using PanguEngine.Threading;

namespace PanguEngine.Tests.Client.UI;

public sealed class UiManagerTests
{
    [Fact]
    public void ManagerStartsWithoutScreen()
    {
        var manager = new UiManager();

        Assert.Null(manager.CurrentScreen);
        Assert.NotNull(manager.Hud);
        manager.Destroy();
    }

    [Fact]
    public void CurrentScreenOperationsKeepHudMounted()
    {
        var manager = new UiManager();
        var screen = new UiScreen(new TestNode());

        manager.Open(screen);
        manager.PrepareFrame(new Size(200, 100), 0);

        Assert.Same(manager.Hud.Crosshair, Assert.Single(manager.Hud.Children));
        Assert.Same(screen, manager.CurrentScreen);

        manager.Close();
        manager.Destroy();
    }

    [Fact]
    public void UpdateLayoutsHudWhenCurrentScreenIsAbsent()
    {
        var manager = new UiManager();

        manager.PrepareFrame(new Size(200, 100), 0);

        Assert.True(manager.Hud.Crosshair.IsArrangeValid);
        manager.Destroy();
    }

    [Fact]
    public void PlainUiScreenCanBeOpenedWithoutSubclassing()
    {
        var manager = new UiManager();
        var root = new TestNode();
        var screen = new UiScreen(root);

        manager.Open(screen);

        Assert.Same(screen, manager.CurrentScreen);
        Assert.Same(screen, root.Screen);
    }

    [Fact]
    public void OpenPublishesCurrentScreenAfterOpened()
    {
        var manager = new UiManager();
        var root = new TestNode();
        UiScreen? openingCurrent = null;
        UiScreen? openedCurrent = null;
        var screen = new RecordingUiScreen(root);
        screen.Opening = () => openingCurrent = manager.CurrentScreen;
        screen.Opened = () => openedCurrent = manager.CurrentScreen;

        manager.Open(screen);

        Assert.Null(openingCurrent);
        Assert.Null(openedCurrent);
        Assert.Same(screen, manager.CurrentScreen);
        Assert.Same(screen, root.Screen);
    }

    [Fact]
    public void CloseClearsCurrentScreenBeforeClosingCallbacks()
    {
        var manager = new UiManager();
        var root = new TestNode();
        UiScreen? closingCurrent = null;
        UiScreen? closedCurrent = null;
        var screen = new RecordingUiScreen(root);
        screen.Closing = () => closingCurrent = manager.CurrentScreen;
        screen.Closed = () => closedCurrent = manager.CurrentScreen;
        manager.Open(screen);

        manager.Close();

        Assert.Null(closingCurrent);
        Assert.Null(closedCurrent);
        Assert.Null(manager.CurrentScreen);
        Assert.Same(screen, root.Screen);
    }

    [Fact]
    public void ReplaceFullyClosesOldScreenBeforeOpeningNewScreen()
    {
        var manager = new UiManager();
        var oldRoot = new TestNode();
        var newRoot = new TestNode();
        var events = new List<string>();
        var oldScreen = new RecordingUiScreen(oldRoot);
        var newScreen = new RecordingUiScreen(newRoot);
        oldScreen.Closing = () => Record("old-closing");
        oldScreen.Closed = () => Record("old-closed");
        newScreen.Opening = () => Record("new-opening");
        newScreen.Opened = () => Record("new-opened");
        manager.Open(oldScreen);

        manager.Open(newScreen);

        Assert.Equal(["old-closing", "old-closed", "new-opening", "new-opened"], events);
        Assert.Same(oldScreen, oldRoot.Screen);
        Assert.Same(newScreen, newRoot.Screen);
        Assert.Same(newScreen, manager.CurrentScreen);

        void Record(string name)
        {
            events.Add(name);
        }
    }

    [Fact]
    public void OpeningCurrentScreenIsANoOp()
    {
        var manager = new UiManager();
        var openedCalls = 0;
        var closingCalls = 0;
        var screen = new RecordingUiScreen(new TestNode());
        screen.Opened = () => openedCalls++;
        screen.Closing = () => closingCalls++;

        manager.Open(screen);
        manager.Open(screen);

        Assert.Equal(1, openedCalls);
        Assert.Equal(0, closingCalls);
        Assert.Same(screen, manager.CurrentScreen);
    }

    [Fact]
    public void ClosedScreenCanBeOpenedAgain()
    {
        var manager = new UiManager();
        var openedCalls = 0;
        var screen = new RecordingUiScreen(new TestNode());
        screen.Opened = () => openedCalls++;

        manager.Open(screen);
        manager.Close();
        manager.Open(screen);

        Assert.Equal(2, openedCalls);
        Assert.Same(screen, manager.CurrentScreen);
    }

    [Fact]
    public void OpenRejectsAlreadyOpenUiScreenWithoutReplacingCurrentScreen()
    {
        var manager = new UiManager();
        var current = new UiScreen(new TestNode());
        manager.Open(current);
        var unavailable = new UiScreen(new TestNode());
        unavailable.Open();

        Assert.Throws<ArgumentNullException>(() => manager.Open(null!));
        Assert.Throws<InvalidOperationException>(() => manager.Open(unavailable));

        Assert.Same(current, manager.CurrentScreen);
        unavailable.Close();
        manager.Close();
    }

    [Fact]
    public void BoundScreenCanBeOpenedByAnotherManagerAfterItCloses()
    {
        var first = new UiManager();
        var second = new UiManager();
        var screen = new UiScreen(new TestNode());
        first.Open(screen);

        Assert.Throws<InvalidOperationException>(() => second.Open(screen));
        Assert.Same(screen, first.CurrentScreen);
        Assert.Null(second.CurrentScreen);

        first.Close();
        second.Open(screen);

        Assert.Same(screen, second.CurrentScreen);
        second.Close();
    }

    [Fact]
    public void LifecycleCallbacksRejectNestedManagerOperations()
    {
        var manager = new UiManager();
        var errors = new List<Exception?>();
        var screen = new RecordingUiScreen(new TestNode());
        screen.Opening = Capture;
        screen.Opened = Capture;
        screen.Closing = Capture;
        screen.Closed = Capture;

        manager.Open(screen);
        manager.Close();

        Assert.Equal(16, errors.Count);
        Assert.All(errors, error => Assert.IsType<InvalidOperationException>(error));

        void Capture() => CaptureNestedOperationErrors(manager, errors);
    }

    [Fact]
    public void LayoutCallbacksRejectNestedManagerOperations()
    {
        var manager = new UiManager();
        var errors = new List<Exception?>();
        var root = new TestNode
        {
            CoreDesiredSize = new Size(5, 5),
            MeasureAction = () => CaptureNestedOperationErrors(manager, errors),
            ArrangeAction = () => CaptureNestedOperationErrors(manager, errors)
        };
        var screen = new UiScreen(root);
        manager.Open(screen);

        manager.PrepareFrame(new Size(20, 20), 0);

        Assert.Equal(8, errors.Count);
        Assert.All(errors, error => Assert.IsType<InvalidOperationException>(error));
        Assert.Same(screen, manager.CurrentScreen);
        manager.Close();
    }

    [Fact]
    public void HudLayoutRejectsNestedManagerOperations()
    {
        var manager = new UiManager();
        var errors = new List<Exception?>();
        var node = new TestNode
        {
            CoreDesiredSize = new Size(5, 5),
            MeasureAction = () => CaptureNestedOperationErrors(manager, errors),
            ArrangeAction = () => CaptureNestedOperationErrors(manager, errors)
        };
        manager.Hud.Children.Add(node);

        manager.PrepareFrame(new Size(20, 20), 0);

        Assert.Equal(8, errors.Count);
        Assert.All(errors, error => Assert.IsType<InvalidOperationException>(error));
        manager.Destroy();
    }

    [Fact]
    public void ScreenPostRunsBeforeManagerLayout()
    {
        var manager = new UiManager();
        var events = new List<string>();
        var root = new TestNode
        {
            CoreDesiredSize = new Size(5, 5),
            MeasureAction = () => events.Add("measure"),
            ArrangeAction = () => events.Add("arrange")
        };
        var screen = new UiScreen(root);
        manager.Open(screen);
        screen.Post(() =>
        {
            screen.Scale = 2;
            events.Add("post");
        });

        manager.PrepareFrame(new Size(20, 10), 0);

        Assert.Equal(["post", "measure", "arrange"], events);
        Assert.Equal(new Size(10, 5), root.LastMeasureConstraint);
        manager.Close();
    }

    [Fact]
    public void PostedLayoutRoundingChangeAppliesDuringTheSameUpdate()
    {
        var manager = new UiManager();
        var root = new TestNode { CoreDesiredSize = new Size(10.25, 10.25) };
        var screen = new UiScreen(root);
        manager.Open(screen);
        screen.Post(() => screen.UseLayoutRounding = false);

        manager.PrepareFrame(new Size(100, 100), 0);

        Assert.Equal(new Size(10.25, 10.25), root.DesiredSize);
        manager.Close();
    }

    [Fact]
    public void LayoutRoundingChangeInvalidatesAndReflowsLayout()
    {
        var manager = new UiManager();
        var root = new TestNode { CoreDesiredSize = new Size(10.25, 10.25) };
        var screen = new UiScreen(root);
        manager.Open(screen);
        manager.PrepareFrame(new Size(100, 100), 0);
        Assert.Equal(new Size(11, 11), root.DesiredSize);

        screen.UseLayoutRounding = false;

        Assert.False(root.IsMeasureValid);
        Assert.False(root.IsArrangeValid);
        manager.PrepareFrame(new Size(100, 100), 0);
        Assert.Equal(new Size(10.25, 10.25), root.DesiredSize);
        manager.Close();
    }

    [Fact]
    public void UpdateAppliesCurrentScreenScaleAndReflowsWhenScaleChanges()
    {
        var manager = new UiManager();
        var root = new TestNode { CoreDesiredSize = new Size(5, 5) };
        var screen = new UiScreen(root) { Scale = 2 };
        manager.Open(screen);

        manager.PrepareFrame(new Size(100, 80), 0);

        Assert.Equal(new Size(50, 40), root.LastMeasureConstraint);
        Assert.Equal(new Rect(0, 0, 50, 40), root.LayoutBounds);

        screen.Scale = 4;
        Assert.Equal(new Size(50, 40), root.LastMeasureConstraint);
        Assert.False(root.IsMeasureValid);
        Assert.False(root.IsArrangeValid);

        manager.PrepareFrame(new Size(100, 80), 0);

        Assert.Equal(new Size(25, 20), root.LastMeasureConstraint);
        Assert.Equal(new Rect(0, 0, 25, 20), root.LayoutBounds);
        manager.Close();
    }

    [Fact]
    public void ScreensKeepIndependentScaleConfigurations()
    {
        var manager = new UiManager();
        var firstRoot = new TestNode { CoreDesiredSize = new Size(5, 5) };
        var secondRoot = new TestNode { CoreDesiredSize = new Size(5, 5) };
        var first = new UiScreen(firstRoot) { Scale = 2 };
        var second = new UiScreen(secondRoot) { Scale = 4 };

        manager.Open(first);
        manager.PrepareFrame(new Size(100, 80), 0);
        manager.Open(second);
        manager.PrepareFrame(new Size(100, 80), 0);

        Assert.Equal(2, first.Scale);
        Assert.Equal(new Size(50, 40), firstRoot.LastMeasureConstraint);
        Assert.Equal(4, second.Scale);
        Assert.Equal(new Size(25, 20), secondRoot.LastMeasureConstraint);
        manager.Close();
    }

    [Fact]
    public void ReopeningReusesLayoutCacheUntilLayoutEnvironmentChanges()
    {
        var manager = new UiManager();
        var root = new TestNode { CoreDesiredSize = new Size(10.25, 10.25) };
        var screen = new UiScreen(root);
        manager.Open(screen);
        manager.PrepareFrame(new Size(100, 100), 0);
        manager.Close();
        var measureCalls = root.MeasureCalls;
        var arrangeCalls = root.ArrangeCalls;

        manager.Open(screen);
        manager.PrepareFrame(new Size(100, 100), 0);
        manager.Close();

        Assert.Equal(measureCalls, root.MeasureCalls);
        Assert.Equal(arrangeCalls, root.ArrangeCalls);

        screen.UseLayoutRounding = false;
        manager.Open(screen);
        manager.PrepareFrame(new Size(100, 100), 0);

        Assert.Equal(measureCalls + 1, root.MeasureCalls);
        Assert.Equal(arrangeCalls + 1, root.ArrangeCalls);
        manager.Close();
    }

    [Fact]
    public void ScreenPostRejectsSynchronousScreenChanges()
    {
        var manager = new UiManager();
        var root = new TestNode { CoreDesiredSize = new Size(5, 5) };
        var screen = new UiScreen(root);
        var replacement = new UiScreen();
        manager.Open(screen);
        screen.Post(() =>
        {
            Assert.Throws<InvalidOperationException>(manager.Close);
            Assert.Throws<InvalidOperationException>(() => manager.Open(replacement));
        });

        manager.PrepareFrame(new Size(20, 10), 0);

        Assert.Same(screen, manager.CurrentScreen);
        Assert.False(replacement.IsOpen());
        Assert.Equal(1, root.MeasureCalls);
        Assert.Equal(1, root.ArrangeCalls);
        manager.Destroy();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DispatchedReplacementKeepsOldScreenUntilQueueRuns(bool fromFrameCallback)
    {
        var queue = new EngineWorkQueue();
        var dispatcher = new EngineDispatcher(queue);
        var manager = new UiManager();
        manager.Hud.Children.Clear();
        var events = new List<string>();
        var oldScreen = new RecordingUiScreen(new TestNode
        {
            CoreDesiredSize = new Size(10, 10),
            DrawAction = DrawRectangle
        });
        var replacement = new RecordingUiScreen(new TestNode
        {
            CoreDesiredSize = new Size(10, 10),
            MeasureAction = () => events.Add("layout"),
            DrawAction = DrawRectangle
        }) { FrameAction = _ => events.Add("frame") };
        Task change = Task.CompletedTask;
        manager.Open(oldScreen);
        void Replace()
        {
            change = dispatcher.InvokeAsync(() =>
            {
                manager.Open(replacement);
                replacement.Post(() => events.Add("post"));
            });
        }
        if (fromFrameCallback)
            oldScreen.FrameAction = _ => Replace();
        else
            oldScreen.Post(Replace);

        manager.PrepareFrame(new Size(20, 10), 0);

        Assert.Same(oldScreen, oldScreen.Root!.Screen);
        Assert.Same(oldScreen, manager.CurrentScreen);
        Assert.False(change.IsCompleted);
        Assert.False(replacement.IsOpen());
        Assert.Empty(events);
        Assert.False(replacement.Root!.IsMeasureValid);
        var commands = new UiDrawCommandList();
        manager.AppendDrawCommands(commands);
        Assert.Single(commands.OfType<UiFillRectangleCommand>());

        queue.RunPending();
        Assert.True(change.IsCompletedSuccessfully);
        Assert.Same(replacement, manager.CurrentScreen);
        Assert.False(oldScreen.IsOpen());

        manager.PrepareFrame(new Size(20, 10), 0);

        Assert.Equal(["post", "frame", "layout"], events);
        Assert.True(replacement.Root!.IsArrangeValid);
        commands.Clear();
        manager.AppendDrawCommands(commands);
        Assert.Single(commands.OfType<UiFillRectangleCommand>());
        manager.Destroy();
        queue.Destroy();
        await change;
    }

    [Fact]
    public void UpdateUsesViewportBoundsAndExistingLayoutCaches()
    {
        var manager = new UiManager();
        var root = new TestNode { CoreDesiredSize = new Size(5, 5) };
        manager.Open(new UiScreen(root));

        manager.PrepareFrame(new Size(20, 10), 0);

        Assert.Equal(new Size(20, 10), root.LastMeasureConstraint);
        Assert.Equal(new Size(20, 10), root.LastArrangeSize);
        Assert.Equal(new Rect(0, 0, 20, 10), root.LayoutBounds);
        Assert.Equal(1, root.MeasureCalls);
        Assert.Equal(1, root.ArrangeCalls);

        manager.PrepareFrame(new Size(20, 10), 0);
        Assert.Equal(1, root.MeasureCalls);
        Assert.Equal(1, root.ArrangeCalls);

        manager.PrepareFrame(new Size(30, 15), 0);
        Assert.Equal(2, root.MeasureCalls);
        Assert.Equal(2, root.ArrangeCalls);
        Assert.Equal(new Rect(0, 0, 30, 15), root.LayoutBounds);
        manager.Close();
    }

    [Fact]
    public void InvalidViewportDoesNotUpdateScreen()
    {
        var manager = new UiManager();
        var calls = 0;
        var screen = new UiScreen(new TestNode());
        manager.Open(screen);
        screen.Post(() => calls++);

        Assert.Throws<ArgumentOutOfRangeException>(() => manager.PrepareFrame(Size.Infinite, 0));
        Assert.Equal(0, calls);

        manager.PrepareFrame(new Size(20, 10), 0);
        Assert.Equal(1, calls);
        manager.Close();
    }

    [Fact]
    public void WrongThreadManagerOperationsFailWithoutChangingState()
    {
        var manager = new UiManager();
        var screen = new UiScreen(new TestNode());
        manager.Open(screen);

        var errors = RunOnBackgroundThread(() =>
            (Open: Record.Exception(() => manager.Open(new UiScreen(new TestNode()))),
                Close: Record.Exception(manager.Close),
                Update: Record.Exception(() => manager.PrepareFrame(new Size(20, 20), 0)),
                Destroy: Record.Exception(manager.Destroy)));

        Assert.IsType<InvalidOperationException>(errors.Open);
        Assert.IsType<InvalidOperationException>(errors.Close);
        Assert.IsType<InvalidOperationException>(errors.Update);
        Assert.IsType<InvalidOperationException>(errors.Destroy);
        Assert.Same(screen, manager.CurrentScreen);
        manager.Close();
    }

    [Fact]
    public void CurrentScreenChangedPublishesOnlyFinalTransitions()
    {
        var manager = new UiManager();
        var first = new UiScreen(new TestNode());
        var second = new UiScreen(new TestNode());
        var changes = new List<(UiScreen? Old, UiScreen? New)>();
        manager.CurrentScreenChanged += (oldScreen, newScreen) => changes.Add((oldScreen, newScreen));

        manager.Open(first);
        manager.Open(second);
        manager.Open(second);
        manager.Close();

        Assert.Equal(3, changes.Count);
        Assert.Null(changes[0].Old);
        Assert.Same(first, changes[0].New);
        Assert.Same(first, changes[1].Old);
        Assert.Same(second, changes[1].New);
        Assert.Same(second, changes[2].Old);
        Assert.Null(changes[2].New);
    }

    [Fact]
    public void FailedReplacementPublishesOldToNull()
    {
        var manager = new UiManager();
        var expected = new InvalidOperationException("closing");
        var oldScreen = new RecordingUiScreen(new TestNode())
        {
            Closing = () => throw expected
        };
        var replacement = new UiScreen(new TestNode());
        var changes = new List<(UiScreen? Old, UiScreen? New)>();
        manager.CurrentScreenChanged += (old, current) => changes.Add((old, current));
        manager.Open(oldScreen);
        changes.Clear();

        var actual = Assert.Throws<InvalidOperationException>(() => manager.Open(replacement));

        Assert.Same(expected, actual);
        Assert.Null(manager.CurrentScreen);
        var change = Assert.Single(changes);
        Assert.Same(oldScreen, change.Old);
        Assert.Null(change.New);
    }

    [Fact]
    public void DestroyPublishesFinalChangeAfterCloseFailure()
    {
        var manager = new UiManager();
        var expected = new InvalidOperationException("closing");
        var screen = new RecordingUiScreen(new TestNode())
        {
            Closing = () => throw expected
        };
        var changes = new List<(UiScreen? Old, UiScreen? New)>();
        manager.CurrentScreenChanged += (old, current) => changes.Add((old, current));
        manager.Open(screen);
        changes.Clear();

        var actual = Assert.Throws<InvalidOperationException>(manager.Destroy);
        manager.Destroy();

        Assert.Same(expected, actual);
        Assert.Null(manager.CurrentScreen);
        var change = Assert.Single(changes);
        Assert.Same(screen, change.Old);
        Assert.Null(change.New);
    }

    [Fact]
    public void DestroyClosesScreenAndIsIdempotent()
    {
        var manager = new UiManager();
        var screen = new UiScreen(new TestNode());
        manager.Open(screen);

        manager.Destroy();
        manager.Destroy();

        Assert.Null(manager.CurrentScreen);
        Assert.Same(screen, screen.Root!.Screen);
        Assert.Throws<ObjectDisposedException>(() => manager.Open(new UiScreen(new TestNode())));
        Assert.Throws<ObjectDisposedException>(manager.Close);
        Assert.Throws<ObjectDisposedException>(() => manager.PrepareFrame(new Size(20, 20), 0));
    }

    [Fact]
    public void DestroyDuringScreenPostIsRejectedAndLayoutContinues()
    {
        var manager = new UiManager();
        var root = new TestNode { CoreDesiredSize = new Size(5, 5) };
        var screen = new UiScreen(root);
        var events = new List<string>();
        manager.Open(screen);
        screen.Post(() =>
        {
            events.Add("current-start");
            Assert.Throws<InvalidOperationException>(manager.Destroy);
            events.Add("current-end");
        });
        screen.Post(() => events.Add("remaining"));

        manager.PrepareFrame(new Size(20, 10), 0);

        Assert.Equal(["current-start", "current-end", "remaining"], events);
        Assert.Equal(1, root.MeasureCalls);
        Assert.Equal(1, root.ArrangeCalls);
        Assert.Same(screen, manager.CurrentScreen);
        manager.Destroy();
    }

    [Fact]
    public async Task HoverRefreshCanDispatchScreenReplacement()
    {
        var queue = new EngineWorkQueue();
        var dispatcher = new EngineDispatcher(queue);
        var manager = new UiManager();
        manager.Hud.Children.Clear();
        var root = new Canvas();
        var leaf = new TestNode { Width = 20, Height = 20, DrawAction = DrawRectangle };
        root.Children.Add(leaf);
        var screen = new UiScreen(root);
        var replacement = new UiScreen(new TestNode
        {
            CoreDesiredSize = new Size(10, 10),
            DrawAction = DrawRectangle
        });
        Exception? updateError = null;
        Task change = Task.CompletedTask;
        leaf.PointerExited += (_, _) =>
        {
            updateError = Record.Exception(() => manager.PrepareFrame(new Size(100, 100), 0));
            Assert.Throws<InvalidOperationException>(() => manager.Open(replacement));
            change = dispatcher.InvokeAsync(() => manager.Open(replacement));
        };
        manager.Open(screen);
        manager.PrepareFrame(new Size(100, 100), 0);
        manager.ProcessPointerMoved(new Point(5, 5));
        leaf.IsHitTestVisible = false;

        manager.PrepareFrame(new Size(100, 100), 0);

        Assert.IsType<InvalidOperationException>(updateError);
        Assert.Same(screen, manager.CurrentScreen);
        Assert.False(change.IsCompleted);
        var commands = new UiDrawCommandList();
        manager.AppendDrawCommands(commands);
        Assert.Single(commands.OfType<UiFillRectangleCommand>());

        queue.RunPending();
        Assert.True(change.IsCompletedSuccessfully);
        Assert.Same(replacement, manager.CurrentScreen);
        manager.PrepareFrame(new Size(100, 100), 0);
        commands.Clear();
        manager.AppendDrawCommands(commands);
        Assert.Single(commands.OfType<UiFillRectangleCommand>());
        manager.Destroy();
        queue.Destroy();
        await change;
    }

    [Fact]
    public void ClosedManagerRejectsOperationsWithObjectDisposedException()
    {
        var manager = new UiManager();
        manager.Destroy();

        Assert.Throws<ObjectDisposedException>(() => manager.Open(new UiScreen(new TestNode())));
        Assert.Throws<ObjectDisposedException>(manager.Close);
        Assert.Throws<ObjectDisposedException>(() => manager.PrepareFrame(new Size(20, 10), 0));
        Assert.Throws<ObjectDisposedException>(() => manager.ProcessFocusChanged(false));
        Assert.Throws<ObjectDisposedException>(() => manager.ProcessFocusChanged(true));
    }

    [Fact]
    public void HudPreparationCompletesBeforeScreenPreparation()
    {
        var events = new List<string>();
        var manager = new UiManager();
        manager.Hud.Children.Add(new TestNode { MeasureAction = () => events.Add("hud-layout") });
        manager.Hud.Post(() => events.Add("hud-post"));
        var screen = new RecordingUiScreen(new TestNode { MeasureAction = () => events.Add("screen-layout") })
        {
            FixedAction = () => events.Add("screen-fixed"),
            FrameAction = alpha =>
            {
                Assert.Equal(0.75, alpha);
                events.Add("screen-frame");
            }
        };
        manager.Open(screen);
        screen.Post(() => events.Add("screen-post"));

        manager.Update();
        Assert.Equal(["screen-fixed"], events);
        events.Clear();
        manager.PrepareFrame(new Size(100, 100), 0.75);

        Assert.Equal(["hud-post", "hud-layout", "screen-post", "screen-frame", "screen-layout"], events);
        events.Clear();
        manager.PrepareFrame(new Size(100, 100), 0.75);
        Assert.Equal(["screen-frame"], events);
        manager.Destroy();
    }

    [Fact]
    public async Task HudPostCanDispatchScreenOpening()
    {
        var queue = new EngineWorkQueue();
        var dispatcher = new EngineDispatcher(queue);
        var events = new List<string>();
        var manager = new UiManager();
        manager.Hud.Children.Clear();
        var screen = new RecordingUiScreen(new TestNode
        {
            CoreDesiredSize = new Size(10, 10),
            MeasureAction = () => events.Add("layout"),
            DrawAction = DrawRectangle
        }) { FrameAction = _ => events.Add("screen-frame") };
        Task change = Task.CompletedTask;
        manager.Hud.Post(() =>
        {
            events.Add("hud-post");
            change = dispatcher.InvokeAsync(() =>
            {
                manager.Open(screen);
                screen.Post(() => events.Add("screen-post"));
            });
        });

        manager.PrepareFrame(new Size(100, 100), 0);

        Assert.Equal(["hud-post"], events);
        Assert.Null(manager.CurrentScreen);
        Assert.False(change.IsCompleted);
        Assert.False(screen.Root!.IsMeasureValid);
        var commands = new UiDrawCommandList();
        manager.AppendDrawCommands(commands);
        Assert.Empty(commands.OfType<UiFillRectangleCommand>());

        queue.RunPending();
        Assert.True(change.IsCompletedSuccessfully);
        manager.PrepareFrame(new Size(100, 100), 0);

        Assert.Equal(["hud-post", "screen-post", "screen-frame", "layout"], events);
        Assert.True(screen.Root!.IsArrangeValid);
        commands.Clear();
        manager.AppendDrawCommands(commands);
        Assert.Single(commands.OfType<UiFillRectangleCommand>());
        manager.Destroy();
        queue.Destroy();
        await change;
    }

    [Fact]
    public async Task DispatchedCloseRunsOutsideFramePreparation()
    {
        var queue = new EngineWorkQueue();
        var dispatcher = new EngineDispatcher(queue);
        var manager = new UiManager();
        Task change = Task.CompletedTask;
        var screen = new RecordingUiScreen(new TestNode())
        {
            FrameAction = _ => change = dispatcher.InvokeAsync(manager.Close)
        };
        manager.Open(screen);
        manager.PrepareFrame(new Size(100, 100), 0);

        Assert.Same(screen, manager.CurrentScreen);
        Assert.True(screen.Root!.IsArrangeValid);
        Assert.False(change.IsCompleted);

        queue.RunPending();

        Assert.Null(manager.CurrentScreen);
        Assert.False(screen.IsOpen());
        manager.Destroy();
        queue.Destroy();
        await change;
    }

    [Fact]
    public async Task DispatchedOpeningFailureIsReportedThroughTask()
    {
        var queue = new EngineWorkQueue();
        var dispatcher = new EngineDispatcher(queue);
        var manager = new UiManager();
        var failure = new InvalidOperationException("opening failed");
        var replacement = new RecordingUiScreen
        {
            Opening = () => throw failure
        };
        Task change = Task.CompletedTask;
        var screen = new RecordingUiScreen
        {
            FrameAction = _ => change = dispatcher.InvokeAsync(() => manager.Open(replacement))
        };
        manager.Open(screen);
        manager.PrepareFrame(new Size(100, 100), 0);

        Assert.Same(screen, manager.CurrentScreen);
        queue.RunPending();
        Assert.Null(manager.CurrentScreen);
        manager.Destroy();
        queue.Destroy();

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => change);
        Assert.Same(failure, actual);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void UpdateFailureRestoresGuardsAndAllowsCleanup(bool fixedUpdate)
    {
        var manager = new UiManager();
        var failure = new InvalidOperationException("update failed");
        var screen = new RecordingUiScreen(new TestNode())
        {
            FixedAction = () => throw failure,
            FrameAction = _ => throw failure
        };
        manager.Open(screen);

        var actual = fixedUpdate
            ? Assert.Throws<InvalidOperationException>(manager.Update)
            : Assert.Throws<InvalidOperationException>(() => manager.PrepareFrame(new Size(100, 100), 0));

        Assert.Same(failure, actual);
        Assert.Same(screen, manager.CurrentScreen);
        manager.Destroy();
        Assert.Null(manager.CurrentScreen);
    }

    [Fact]
    public void ManagerRejectsNestedUpdateAndRecordingDuringPost()
    {
        var manager = new UiManager();
        manager.Hud.Post(() =>
        {
            Assert.Throws<InvalidOperationException>(() => manager.Open(new UiScreen()));
            Assert.Throws<InvalidOperationException>(manager.Close);
            Assert.Throws<InvalidOperationException>(manager.Update);
            Assert.Throws<InvalidOperationException>(manager.Destroy);
            Assert.Throws<InvalidOperationException>(() => manager.PrepareFrame(new Size(10, 10), 0));
            Assert.Throws<InvalidOperationException>(() => manager.AppendDrawCommands(new UiDrawCommandList()));
        });

        manager.PrepareFrame(new Size(10, 10), 0);
        manager.Destroy();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void UpdateCallbacksRejectSynchronousScreenChangesAndDestruction(bool fixedUpdate)
    {
        var manager = new UiManager();
        var root = new TestNode();
        var replacement = new UiScreen();
        var calls = 0;
        void Check()
        {
            calls++;
            Assert.Throws<InvalidOperationException>(() => manager.Open(replacement));
            Assert.Throws<InvalidOperationException>(manager.Close);
            Assert.Throws<InvalidOperationException>(manager.Destroy);
        }
        var screen = new RecordingUiScreen(root)
        {
            FixedAction = Check,
            FrameAction = _ => Check()
        };
        manager.Open(screen);

        if (fixedUpdate)
            manager.Update();
        else
            manager.PrepareFrame(new Size(10, 10), 0);

        Assert.Same(screen, manager.CurrentScreen);
        Assert.Equal(1, calls);
        Assert.False(replacement.IsOpen());
        Assert.True(screen.IsOpen());
        Assert.Equal(!fixedUpdate, root.IsMeasureValid);
        manager.Destroy();
        Assert.Throws<ObjectDisposedException>(manager.Update);
        Assert.Throws<ObjectDisposedException>(() => manager.AppendDrawCommands(new UiDrawCommandList()));
        manager.Destroy();
    }

    private static void DrawRectangle(UiDrawingContext context) =>
        context.FillRectangle(new Rect(0, 0, 10, 10), new Color(255, 255, 255));

    private static void CaptureNestedOperationErrors(
        UiManager manager,
        ICollection<Exception?> errors)
    {
        errors.Add(Record.Exception(() => manager.Open(new UiScreen(new TestNode()))));
        errors.Add(Record.Exception(manager.Close));
        errors.Add(Record.Exception(manager.Destroy));
        errors.Add(Record.Exception(() => manager.PrepareFrame(new Size(10, 10), 0)));
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
        internal Size CoreDesiredSize { get; set; }
        internal Size LastMeasureConstraint { get; private set; }
        internal Size LastArrangeSize { get; private set; }
        internal Action? MeasureAction { get; set; }
        internal Action? ArrangeAction { get; set; }
        internal Action<UiDrawingContext>? DrawAction { get; set; }
        internal int MeasureCalls { get; private set; }
        internal int ArrangeCalls { get; private set; }

        protected override Size MeasureCore(Size availableSize)
        {
            MeasureCalls++;
            LastMeasureConstraint = availableSize;
            MeasureAction?.Invoke();
            return CoreDesiredSize;
        }

        protected override void ArrangeCore(Size finalSize)
        {
            ArrangeCalls++;
            LastArrangeSize = finalSize;
            ArrangeAction?.Invoke();
        }

        protected override void DrawCore(UiDrawingContext context) => DrawAction?.Invoke(context);
    }

    private sealed class RecordingUiScreen(UiNode? root = null) : UiScreen(root)
    {
        internal Action? Opening { get; set; }
        internal Action? Opened { get; set; }
        internal Action? Closing { get; set; }
        internal Action? Closed { get; set; }
        internal Action? FixedAction { get; set; }
        internal Action<double>? FrameAction { get; set; }

        protected override void OnOpening() => Opening?.Invoke();
        protected override void OnOpened() => Opened?.Invoke();
        protected override void OnClosing() => Closing?.Invoke();
        protected override void OnClosed() => Closed?.Invoke();
        protected override void OnFixedUpdate() => FixedAction?.Invoke();
        protected override void OnFrameUpdate(double alpha) => FrameAction?.Invoke(alpha);
    }
}
