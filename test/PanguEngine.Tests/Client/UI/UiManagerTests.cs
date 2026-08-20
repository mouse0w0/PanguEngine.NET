using System.Runtime.ExceptionServices;
using PanguEngine.Client.UI;

namespace PanguEngine.Tests.Client.UI;

public sealed class UiManagerTests
{
    [Fact]
    public void ManagerStartsWithoutScreen()
    {
        var manager = new UiManager();

        Assert.Null(manager.CurrentScreen);
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

        manager.Update(new Size(20, 20));

        Assert.Equal(8, errors.Count);
        Assert.All(errors, error => Assert.IsType<InvalidOperationException>(error));
        Assert.Same(screen, manager.CurrentScreen);
        manager.Close();
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

        manager.Update(new Size(20, 10));

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

        manager.Update(new Size(100, 100));

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
        manager.Update(new Size(100, 100));
        Assert.Equal(new Size(11, 11), root.DesiredSize);

        screen.UseLayoutRounding = false;

        Assert.False(root.IsMeasureValid);
        Assert.False(root.IsArrangeValid);
        manager.Update(new Size(100, 100));
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

        manager.Update(new Size(100, 80));

        Assert.Equal(new Size(50, 40), root.LastMeasureConstraint);
        Assert.Equal(new Rect(0, 0, 50, 40), root.LayoutBounds);

        screen.Scale = 4;
        Assert.Equal(new Size(50, 40), root.LastMeasureConstraint);
        Assert.False(root.IsMeasureValid);
        Assert.False(root.IsArrangeValid);

        manager.Update(new Size(100, 80));

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
        manager.Update(new Size(100, 80));
        manager.Open(second);
        manager.Update(new Size(100, 80));

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
        manager.Update(new Size(100, 100));
        manager.Close();
        var measureCalls = root.MeasureCalls;
        var arrangeCalls = root.ArrangeCalls;

        manager.Open(screen);
        manager.Update(new Size(100, 100));
        manager.Close();

        Assert.Equal(measureCalls, root.MeasureCalls);
        Assert.Equal(arrangeCalls, root.ArrangeCalls);

        screen.UseLayoutRounding = false;
        manager.Open(screen);
        manager.Update(new Size(100, 100));

        Assert.Equal(measureCalls + 1, root.MeasureCalls);
        Assert.Equal(arrangeCalls + 1, root.ArrangeCalls);
        manager.Close();
    }

    [Fact]
    public void ScreenPostCanCloseManagerBeforeLayout()
    {
        var manager = new UiManager();
        var root = new TestNode { CoreDesiredSize = new Size(5, 5) };
        var screen = new UiScreen(root);
        manager.Open(screen);
        screen.Post(manager.Close);

        manager.Update(new Size(20, 10));

        Assert.Null(manager.CurrentScreen);
        Assert.Equal(0, root.MeasureCalls);
        Assert.Equal(0, root.ArrangeCalls);
    }

    [Fact]
    public void ScreenPostCannotCloseAndReopenTheSameScreenThroughManager()
    {
        var manager = new UiManager();
        var screen = new UiScreen(new TestNode());
        Exception? reopenError = null;
        manager.Open(screen);
        screen.Post(() =>
        {
            manager.Close();
            reopenError = Record.Exception(() => manager.Open(screen));
        });

        manager.Update(new Size(20, 10));

        Assert.IsType<InvalidOperationException>(reopenError);
        Assert.Null(manager.CurrentScreen);
        manager.Open(screen);
        manager.Close();
    }

    [Fact]
    public void ScreenPostCanReplaceCurrentScreenWithDifferentScreen()
    {
        var manager = new UiManager();
        var oldScreen = new UiScreen(new TestNode());
        var replacement = new UiScreen(new TestNode());
        manager.Open(oldScreen);
        oldScreen.Post(() => manager.Open(replacement));

        manager.Update(new Size(20, 10));

        Assert.Same(oldScreen, oldScreen.Root!.Screen);
        Assert.Same(replacement, manager.CurrentScreen);
        Assert.Same(replacement, replacement.Root!.Screen);
        manager.Close();
    }

    [Fact]
    public void UpdateUsesViewportBoundsAndExistingLayoutCaches()
    {
        var manager = new UiManager();
        var root = new TestNode { CoreDesiredSize = new Size(5, 5) };
        manager.Open(new UiScreen(root));

        manager.Update(new Size(20, 10));

        Assert.Equal(new Size(20, 10), root.LastMeasureConstraint);
        Assert.Equal(new Size(20, 10), root.LastArrangeSize);
        Assert.Equal(new Rect(0, 0, 20, 10), root.LayoutBounds);
        Assert.Equal(1, root.MeasureCalls);
        Assert.Equal(1, root.ArrangeCalls);

        manager.Update(new Size(20, 10));
        Assert.Equal(1, root.MeasureCalls);
        Assert.Equal(1, root.ArrangeCalls);

        manager.Update(new Size(30, 15));
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

        Assert.Throws<ArgumentOutOfRangeException>(() => manager.Update(Size.Infinite));
        Assert.Equal(0, calls);

        manager.Update(new Size(20, 10));
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
                Update: Record.Exception(() => manager.Update(new Size(20, 20))),
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
        Assert.Throws<ObjectDisposedException>(() => manager.Update(new Size(20, 20)));
    }

    [Fact]
    public void DestroyDuringScreenPostCompletesActionAndSkipsLayout()
    {
        var manager = new UiManager();
        var root = new TestNode { CoreDesiredSize = new Size(5, 5) };
        var screen = new UiScreen(root);
        var events = new List<string>();
        manager.Open(screen);
        screen.Post(() =>
        {
            events.Add("current-start");
            manager.Destroy();
            events.Add("current-end");
        });
        screen.Post(() => events.Add("discarded"));

        manager.Update(new Size(20, 10));

        Assert.Equal(["current-start", "current-end"], events);
        Assert.Equal(0, root.MeasureCalls);
        Assert.Equal(0, root.ArrangeCalls);
        Assert.Null(manager.CurrentScreen);
    }

    [Fact]
    public void HoverRefreshCallbacksCanReplaceAfterLayout()
    {
        var manager = new UiManager();
        var root = new Canvas();
        var leaf = new TestNode { Width = 20, Height = 20 };
        root.Children.Add(leaf);
        var screen = new UiScreen(root);
        var replacement = new UiScreen(new TestNode());
        Exception? updateError = null;
        leaf.PointerExited += (_, _) =>
        {
            updateError = Record.Exception(() => manager.Update(new Size(100, 100)));
            manager.Open(replacement);
        };
        manager.Open(screen);
        manager.Update(new Size(100, 100));
        manager.ProcessPointerMoved(new Point(5, 5));
        leaf.IsHitTestVisible = false;

        manager.Update(new Size(100, 100));

        Assert.IsType<InvalidOperationException>(updateError);
        Assert.Same(replacement, manager.CurrentScreen);
        manager.Close();
    }

    [Fact]
    public void ClosedManagerRejectsOperationsWithObjectDisposedException()
    {
        var manager = new UiManager();
        manager.Destroy();

        Assert.Throws<ObjectDisposedException>(() => manager.Open(new UiScreen(new TestNode())));
        Assert.Throws<ObjectDisposedException>(manager.Close);
        Assert.Throws<ObjectDisposedException>(() => manager.Update(new Size(20, 10)));
        Assert.Throws<ObjectDisposedException>(() => manager.ProcessFocusChanged(false));
        Assert.Throws<ObjectDisposedException>(() => manager.ProcessFocusChanged(true));
    }

    private static void CaptureNestedOperationErrors(
        UiManager manager,
        ICollection<Exception?> errors)
    {
        errors.Add(Record.Exception(() => manager.Open(new UiScreen(new TestNode()))));
        errors.Add(Record.Exception(manager.Close));
        errors.Add(Record.Exception(manager.Destroy));
        errors.Add(Record.Exception(() => manager.Update(new Size(10, 10))));
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
