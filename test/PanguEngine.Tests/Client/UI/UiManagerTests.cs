using System.Runtime.ExceptionServices;
using PanguEngine.Client.UI;

namespace PanguEngine.Tests.Client.UI;

public sealed class UiManagerTests
{
    [Fact]
    public void ScreenRequiresRootAndKeepsTheSameReference()
    {
        var root = new TestNode();
        var screen = new Screen(root);

        Assert.Same(root, screen.Root);
        Assert.Throws<ArgumentNullException>(() => new Screen(null!));
    }

    [Fact]
    public void ManagerConstructionBindsDispatcherAndStartsWithoutScreen()
    {
        var manager = new UiManager();

        Assert.True(manager.Dispatcher.CheckAccess());
        Assert.Null(manager.CurrentScreen);
    }

    [Fact]
    public void PlainScreenCanBeOpenedWithoutSubclassing()
    {
        var manager = new UiManager();
        var root = new TestNode();
        var screen = new Screen(root);

        manager.Open(screen);

        Assert.Same(screen, manager.CurrentScreen);
        Assert.Same(manager.Dispatcher, root.ActiveDispatcher);
    }

    [Fact]
    public void OpenAttachesRootAroundTheOpeningCallbacks()
    {
        var manager = new UiManager();
        var root = new TestNode();
        var states = new List<(string Name, UiDispatcher? Dispatcher, Screen? Current)>();
        var screen = new RecordingScreen(root);
        screen.Opening = current =>
            states.Add(("opening", root.ActiveDispatcher, current.CurrentScreen));
        screen.Opened = current =>
            states.Add(("opened", root.ActiveDispatcher, current.CurrentScreen));

        manager.Open(screen);

        Assert.Same(manager.Dispatcher, root.ActiveDispatcher);
        Assert.Same(screen, manager.CurrentScreen);
        Assert.Collection(
            states,
            state =>
            {
                Assert.Equal("opening", state.Name);
                Assert.Null(state.Dispatcher);
                Assert.Null(state.Current);
            },
            state =>
            {
                Assert.Equal("opened", state.Name);
                Assert.Same(manager.Dispatcher, state.Dispatcher);
                Assert.Same(screen, state.Current);
            });
    }

    [Fact]
    public void CloseDetachesRootAroundTheClosingCallbacks()
    {
        var manager = new UiManager();
        var root = new TestNode();
        var states = new List<(string Name, UiDispatcher? Dispatcher, Screen? Current)>();
        var screen = new RecordingScreen(root);
        screen.Closing = current =>
            states.Add(("closing", root.ActiveDispatcher, current.CurrentScreen));
        screen.Closed = current =>
            states.Add(("closed", root.ActiveDispatcher, current.CurrentScreen));
        manager.Open(screen);

        manager.Close();
        manager.Close();

        Assert.Null(root.ActiveDispatcher);
        Assert.Null(manager.CurrentScreen);
        Assert.Collection(
            states,
            state =>
            {
                Assert.Equal("closing", state.Name);
                Assert.Same(manager.Dispatcher, state.Dispatcher);
                Assert.Same(screen, state.Current);
            },
            state =>
            {
                Assert.Equal("closed", state.Name);
                Assert.Null(state.Dispatcher);
                Assert.Null(state.Current);
            });
    }

    [Fact]
    public void ReplaceFullyClosesOldScreenBeforeOpeningNewScreen()
    {
        var manager = new UiManager();
        var oldRoot = new TestNode();
        var newRoot = new TestNode();
        var events = new List<string>();
        var activeRootCounts = new List<int>();
        var oldScreen = new RecordingScreen(oldRoot);
        var newScreen = new RecordingScreen(newRoot);
        oldScreen.Closing = _ => Record("old-closing");
        oldScreen.Closed = _ => Record("old-closed");
        newScreen.Opening = _ => Record("new-opening");
        newScreen.Opened = _ => Record("new-opened");
        manager.Open(oldScreen);

        manager.Open(newScreen);

        Assert.Equal(
            ["old-closing", "old-closed", "new-opening", "new-opened"],
            events);
        Assert.All(activeRootCounts, count => Assert.InRange(count, 0, 1));
        Assert.Null(oldRoot.ActiveDispatcher);
        Assert.Same(manager.Dispatcher, newRoot.ActiveDispatcher);
        Assert.Same(newScreen, manager.CurrentScreen);

        void Record(string name)
        {
            events.Add(name);
            activeRootCounts.Add(
                (oldRoot.ActiveDispatcher is null ? 0 : 1) +
                (newRoot.ActiveDispatcher is null ? 0 : 1));
        }
    }

    [Fact]
    public void OpeningCurrentScreenIsANoOp()
    {
        var manager = new UiManager();
        var openedCalls = 0;
        var closingCalls = 0;
        var screen = new RecordingScreen(new TestNode())
        {
            Opened = _ => openedCalls++,
            Closing = _ => closingCalls++
        };
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
        var screen = new RecordingScreen(new TestNode())
        {
            Opened = _ => openedCalls++
        };

        manager.Open(screen);
        manager.Close();
        manager.Open(screen);

        Assert.Equal(2, openedCalls);
        Assert.Same(screen, manager.CurrentScreen);
        Assert.Same(manager.Dispatcher, screen.Root.ActiveDispatcher);
    }

    [Fact]
    public void OpenRejectsNullAndUnavailableRootsWithoutReplacingCurrentScreen()
    {
        var manager = new UiManager();
        var current = new Screen(new TestNode());
        manager.Open(current);
        var parent = new TestParent();
        var parentedRoot = new TestNode();
        parent.Add(parentedRoot);
        var activeRoot = new TestNode();
        var otherDispatcher = new UiDispatcher();
        activeRoot.AttachToTree(otherDispatcher);

        Assert.Throws<ArgumentNullException>(() => manager.Open(null!));
        Assert.Throws<InvalidOperationException>(() =>
            manager.Open(new Screen(parentedRoot)));
        Assert.Throws<InvalidOperationException>(() =>
            manager.Open(new Screen(activeRoot)));

        Assert.Same(current, manager.CurrentScreen);
        Assert.Same(manager.Dispatcher, current.Root.ActiveDispatcher);
        Assert.Same(parent, parentedRoot.Parent);
        Assert.Same(otherDispatcher, activeRoot.ActiveDispatcher);
        activeRoot.DetachFromTree();
    }

    [Fact]
    public void ScreenOwnedByAnotherManagerCanBeOpenedAfterItCloses()
    {
        var first = new UiManager();
        var second = new UiManager();
        var screen = new Screen(new TestNode());
        first.Open(screen);

        Assert.Throws<InvalidOperationException>(() => second.Open(screen));
        Assert.Same(screen, first.CurrentScreen);
        Assert.Null(second.CurrentScreen);

        first.Close();
        second.Open(screen);

        Assert.Same(screen, second.CurrentScreen);
        Assert.Same(second.Dispatcher, screen.Root.ActiveDispatcher);
    }

    [Fact]
    public void ConcurrentManagersCannotClaimTheSameScreen()
    {
        using var start = new Barrier(2);
        using var attempted = new Barrier(2);
        var screen = new Screen(new TestNode());
        var successes = new bool[2];
        var errors = new Exception?[2];
        var threads = Enumerable.Range(0, 2)
            .Select(index => new Thread(() =>
            {
                var manager = new UiManager();
                start.SignalAndWait();
                try
                {
                    manager.Open(screen);
                    successes[index] = true;
                }
                catch (Exception exception)
                {
                    errors[index] = exception;
                }
                finally
                {
                    attempted.SignalAndWait();
                    if (successes[index])
                        manager.Close();
                    manager.Shutdown();
                }
            }))
            .ToArray();

        foreach (var thread in threads)
            thread.Start();
        foreach (var thread in threads)
            thread.Join();

        Assert.Equal(1, successes.Count(success => success));
        Assert.Equal(1, errors.Count(error => error is InvalidOperationException));
        Assert.Equal(1, errors.Count(error => error is null));
        Assert.Null(screen.Root.ActiveDispatcher);
    }

    [Fact]
    public void CandidateRootChangedByOldClosingIsRecheckedAndReleased()
    {
        var manager = new UiManager();
        var parent = new TestParent();
        var candidateRoot = new TestNode();
        var candidate = new Screen(candidateRoot);
        var oldScreen = new RecordingScreen(new TestNode())
        {
            Closing = _ => parent.Add(candidateRoot)
        };
        manager.Open(oldScreen);

        Assert.Throws<InvalidOperationException>(() => manager.Open(candidate));

        Assert.Null(manager.CurrentScreen);
        Assert.Null(oldScreen.Root.ActiveDispatcher);
        Assert.Same(parent, candidateRoot.Parent);
        Assert.True(parent.Remove(candidateRoot));
        var otherManager = new UiManager();
        otherManager.Open(candidate);
        Assert.Same(candidate, otherManager.CurrentScreen);
    }

    [Fact]
    public void RootChangedByOpeningIsRejectedAndScreenIsReleased()
    {
        var manager = new UiManager();
        var parent = new TestParent();
        var root = new TestNode();
        var screen = new RecordingScreen(root)
        {
            Opening = _ => parent.Add(root)
        };

        Assert.Throws<InvalidOperationException>(() => manager.Open(screen));

        Assert.Null(manager.CurrentScreen);
        Assert.Null(root.ActiveDispatcher);
        Assert.True(parent.Remove(root));
        screen.Opening = null;
        var otherManager = new UiManager();
        otherManager.Open(screen);
        Assert.Same(screen, otherManager.CurrentScreen);
    }

    [Fact]
    public void LifecycleCallbacksRejectNestedManagerOperations()
    {
        var manager = new UiManager();
        var errors = new List<Exception?>();
        var screen = new RecordingScreen(new TestNode());
        screen.Opening = Capture;
        screen.Opened = Capture;
        screen.Closing = Capture;
        screen.Closed = Capture;

        manager.Open(screen);
        manager.Close();

        Assert.Equal(16, errors.Count);
        Assert.All(errors, error => Assert.IsType<InvalidOperationException>(error));

        void Capture(UiManager current) =>
            CaptureNestedOperationErrors(current, errors);
    }

    [Fact]
    public void LayoutCallbacksRejectNestedManagerOperations()
    {
        var manager = new UiManager();
        var errors = new List<Exception?>();
        var root = new TestNode
        {
            CoreDesiredSize = new Size(5, 5)
        };
        root.MeasureAction = () => CaptureNestedOperationErrors(manager, errors);
        root.ArrangeAction = () => CaptureNestedOperationErrors(manager, errors);
        var screen = new Screen(root);
        manager.Open(screen);

        manager.Update(new Size(20, 20));

        Assert.Equal(8, errors.Count);
        Assert.All(errors, error => Assert.IsType<InvalidOperationException>(error));
        Assert.Same(screen, manager.CurrentScreen);
    }

    [Fact]
    public void LifecyclePostDefersCloseUntilTheNextDispatcherBatch()
    {
        var manager = new UiManager();
        var screen = new RecordingScreen(new TestNode())
        {
            Opened = current => current.Dispatcher.Post(current.Close)
        };
        manager.Dispatcher.Post(() => manager.Open(screen));

        manager.Update(new Size(20, 20));
        Assert.Same(screen, manager.CurrentScreen);

        manager.Update(new Size(20, 20));
        Assert.Null(manager.CurrentScreen);
    }

    [Fact]
    public void WrongThreadManagerOperationsFailWithoutChangingState()
    {
        var manager = new UiManager();
        var screen = new Screen(new TestNode());
        manager.Open(screen);

        var errors = RunOnBackgroundThread(() =>
            (Open: Record.Exception(() => manager.Open(new Screen(new TestNode()))),
                Close: Record.Exception(manager.Close),
                Update: Record.Exception(() => manager.Update(new Size(20, 20))),
                Shutdown: Record.Exception(manager.Shutdown)));

        Assert.IsType<InvalidOperationException>(errors.Open);
        Assert.IsType<InvalidOperationException>(errors.Close);
        Assert.IsType<InvalidOperationException>(errors.Update);
        Assert.IsType<InvalidOperationException>(errors.Shutdown);
        Assert.Same(screen, manager.CurrentScreen);
        Assert.Same(manager.Dispatcher, screen.Root.ActiveDispatcher);
    }

    [Fact]
    public void ClosingFailureKeepsScreenActiveAndRestoresTransitionGuard()
    {
        var manager = new UiManager();
        var expected = new InvalidOperationException("closing failed");
        var screen = new RecordingScreen(new TestNode())
        {
            Closing = _ => throw expected
        };
        manager.Open(screen);

        var actual = Assert.Throws<InvalidOperationException>(manager.Close);

        Assert.Same(expected, actual);
        Assert.Same(screen, manager.CurrentScreen);
        Assert.Same(manager.Dispatcher, screen.Root.ActiveDispatcher);
        screen.Closing = null;
        manager.Update(new Size(20, 20));
        manager.Close();
        Assert.Null(manager.CurrentScreen);

        var replacement = new Screen(new TestNode());
        manager.Open(replacement);
        Assert.Same(replacement, manager.CurrentScreen);
    }

    [Fact]
    public void OpeningFailureLeavesScreenInactiveAndReusable()
    {
        var manager = new UiManager();
        var expected = new InvalidOperationException("opening failed");
        var screen = new RecordingScreen(new TestNode())
        {
            Opening = _ => throw expected
        };

        var actual = Assert.Throws<InvalidOperationException>(() => manager.Open(screen));

        Assert.Same(expected, actual);
        Assert.Null(manager.CurrentScreen);
        Assert.Null(screen.Root.ActiveDispatcher);
        screen.Opening = null;
        manager.Open(screen);
        Assert.Same(screen, manager.CurrentScreen);
    }

    [Fact]
    public void OpenedFailureForcesClosingAndClosedRollback()
    {
        var manager = new UiManager();
        var expected = new InvalidOperationException("opened failed");
        var events = new List<string>();
        var screen = new RecordingScreen(new TestNode())
        {
            Opened = _ =>
            {
                events.Add("opened");
                throw expected;
            },
            Closing = _ => events.Add("closing"),
            Closed = _ => events.Add("closed")
        };

        var actual = Assert.Throws<InvalidOperationException>(() => manager.Open(screen));

        Assert.Same(expected, actual);
        Assert.Equal(["opened", "closing", "closed"], events);
        Assert.Null(manager.CurrentScreen);
        Assert.Null(screen.Root.ActiveDispatcher);
        screen.Opened = null;
        manager.Open(screen);
        Assert.Same(screen, manager.CurrentScreen);
    }

    [Fact]
    public void OpenedRollbackAggregatesCallbackErrorsInLifecycleOrder()
    {
        var manager = new UiManager();
        var openedError = new InvalidOperationException("opened failed");
        var closingError = new InvalidOperationException("closing failed");
        var closedError = new InvalidOperationException("closed failed");
        var screen = new RecordingScreen(new TestNode())
        {
            Opened = _ => throw openedError,
            Closing = _ => throw closingError,
            Closed = _ => throw closedError
        };

        var aggregate = Assert.Throws<AggregateException>(() => manager.Open(screen));

        Assert.Collection(
            aggregate.InnerExceptions,
            error => Assert.Same(openedError, error),
            error => Assert.Same(closingError, error),
            error => Assert.Same(closedError, error));
        Assert.Null(manager.CurrentScreen);
        Assert.Null(screen.Root.ActiveDispatcher);
    }

    [Fact]
    public void ClosedFailureLeavesScreenDetachedAndReusable()
    {
        var manager = new UiManager();
        var expected = new InvalidOperationException("closed failed");
        var screen = new RecordingScreen(new TestNode())
        {
            Closed = _ => throw expected
        };
        manager.Open(screen);

        var actual = Assert.Throws<InvalidOperationException>(manager.Close);

        Assert.Same(expected, actual);
        Assert.Null(manager.CurrentScreen);
        Assert.Null(screen.Root.ActiveDispatcher);
        screen.Closed = null;
        manager.Open(screen);
        Assert.Same(screen, manager.CurrentScreen);
    }

    [Fact]
    public void ReplacementOpeningFailureDoesNotRestoreClosedScreen()
    {
        var manager = new UiManager();
        var oldScreen = new Screen(new TestNode());
        var newScreen = new RecordingScreen(new TestNode())
        {
            Opening = _ => throw new InvalidOperationException("opening failed")
        };
        manager.Open(oldScreen);

        Assert.Throws<InvalidOperationException>(() => manager.Open(newScreen));

        Assert.Null(manager.CurrentScreen);
        Assert.Null(oldScreen.Root.ActiveDispatcher);
        Assert.Null(newScreen.Root.ActiveDispatcher);
        newScreen.Opening = null;
        manager.Open(newScreen);
        Assert.Same(newScreen, manager.CurrentScreen);
    }

    [Fact]
    public void OldClosingFailureReleasesCandidateForAnotherManager()
    {
        var manager = new UiManager();
        var expected = new InvalidOperationException("closing failed");
        var oldScreen = new RecordingScreen(new TestNode())
        {
            Closing = _ => throw expected
        };
        var candidate = new Screen(new TestNode());
        manager.Open(oldScreen);

        var actual = Assert.Throws<InvalidOperationException>(() => manager.Open(candidate));

        Assert.Same(expected, actual);
        Assert.Same(oldScreen, manager.CurrentScreen);
        var otherManager = new UiManager();
        otherManager.Open(candidate);
        Assert.Same(candidate, otherManager.CurrentScreen);
    }

    [Fact]
    public void OldClosedFailureReleasesCandidateAfterOldScreenDetaches()
    {
        var manager = new UiManager();
        var expected = new InvalidOperationException("closed failed");
        var oldScreen = new RecordingScreen(new TestNode())
        {
            Closed = _ => throw expected
        };
        var candidate = new Screen(new TestNode());
        manager.Open(oldScreen);

        var actual = Assert.Throws<InvalidOperationException>(() => manager.Open(candidate));

        Assert.Same(expected, actual);
        Assert.Null(manager.CurrentScreen);
        Assert.Null(oldScreen.Root.ActiveDispatcher);
        var otherManager = new UiManager();
        otherManager.Open(candidate);
        Assert.Same(candidate, otherManager.CurrentScreen);
    }

    [Fact]
    public void UpdateDrainsDispatcherBeforeMeasuringAndArranging()
    {
        var manager = new UiManager();
        var events = new List<string>();
        var root = new TestNode
        {
            CoreDesiredSize = new Size(5, 5),
            MeasureAction = () => events.Add("measure"),
            ArrangeAction = () => events.Add("arrange")
        };
        manager.Open(new Screen(root));
        manager.Dispatcher.Post(() => events.Add("dispatch"));

        manager.Update(new Size(20, 10));

        Assert.Equal(["dispatch", "measure", "arrange"], events);
    }

    [Fact]
    public void ScreenOpenedDuringDrainIsLaidOutInTheSameUpdate()
    {
        var manager = new UiManager();
        var events = new List<string>();
        var root = new TestNode
        {
            CoreDesiredSize = new Size(5, 5),
            MeasureAction = () => events.Add("measure"),
            ArrangeAction = () => events.Add("arrange")
        };
        var screen = new RecordingScreen(root)
        {
            Opened = _ => events.Add("opened")
        };
        manager.Dispatcher.Post(() => manager.Open(screen));

        manager.Update(new Size(20, 10));

        Assert.Equal(["opened", "measure", "arrange"], events);
        Assert.Same(screen, manager.CurrentScreen);
    }

    [Fact]
    public void ScreenOpenedAndClosedInTheSameBatchIsNotLaidOut()
    {
        var manager = new UiManager();
        var root = new TestNode { CoreDesiredSize = new Size(5, 5) };
        manager.Dispatcher.Post(() => manager.Open(new Screen(root)));
        manager.Dispatcher.Post(manager.Close);

        manager.Update(new Size(20, 10));

        Assert.Null(manager.CurrentScreen);
        Assert.Equal(0, root.MeasureCalls);
        Assert.Equal(0, root.ArrangeCalls);
    }

    [Fact]
    public void UpdateUsesViewportBoundsAndExistingLayoutCaches()
    {
        var manager = new UiManager();
        var root = new TestNode { CoreDesiredSize = new Size(5, 5) };
        manager.Open(new Screen(root));

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
    }

    [Fact]
    public void MeasureInvalidatedDuringCoreSkipsArrangeUntilNextUpdate()
    {
        var manager = new UiManager();
        var root = new TestNode { CoreDesiredSize = new Size(5, 5) };
        root.MeasureAction = root.InvalidateMeasure;
        manager.Open(new Screen(root));

        manager.Update(new Size(20, 10));

        Assert.False(root.IsMeasureValid);
        Assert.Equal(1, root.MeasureCalls);
        Assert.Equal(0, root.ArrangeCalls);

        root.MeasureAction = null;
        manager.Update(new Size(20, 10));
        Assert.True(root.IsMeasureValid);
        Assert.True(root.IsArrangeValid);
        Assert.Equal(2, root.MeasureCalls);
        Assert.Equal(1, root.ArrangeCalls);
    }

    [Fact]
    public void UpdateGuardsRecoverAfterDrainMeasureAndArrangeExceptions()
    {
        var drainManager = new UiManager();
        var drainError = new InvalidOperationException("drain failed");
        drainManager.Dispatcher.Post(() => throw drainError);
        Assert.Same(
            drainError,
            Assert.Throws<InvalidOperationException>(() =>
                drainManager.Update(new Size(20, 10))));
        var drainScreen = new Screen(new TestNode());
        drainManager.Dispatcher.Post(() => drainManager.Open(drainScreen));
        drainManager.Update(new Size(20, 10));
        Assert.Same(drainScreen, drainManager.CurrentScreen);

        var measureManager = new UiManager();
        var measureError = new InvalidOperationException("measure failed");
        var measureRoot = new TestNode { MeasureException = measureError };
        measureManager.Open(new Screen(measureRoot));
        Assert.Same(
            measureError,
            Assert.Throws<InvalidOperationException>(() =>
                measureManager.Update(new Size(20, 10))));
        measureRoot.MeasureException = null;
        measureManager.Update(new Size(20, 10));
        Assert.True(measureRoot.IsArrangeValid);

        var arrangeManager = new UiManager();
        var arrangeError = new InvalidOperationException("arrange failed");
        var arrangeRoot = new TestNode
        {
            CoreDesiredSize = new Size(5, 5),
            ArrangeException = arrangeError
        };
        arrangeManager.Open(new Screen(arrangeRoot));
        Assert.Same(
            arrangeError,
            Assert.Throws<InvalidOperationException>(() =>
                arrangeManager.Update(new Size(20, 10))));
        arrangeRoot.ArrangeException = null;
        arrangeManager.Update(new Size(20, 10));
        Assert.True(arrangeRoot.IsArrangeValid);
    }

    [Fact]
    public void InvalidViewportDoesNotDrainDispatcher()
    {
        var manager = new UiManager();
        var calls = 0;
        manager.Dispatcher.Post(() => calls++);

        Assert.Throws<ArgumentOutOfRangeException>(() => manager.Update(Size.Infinite));
        Assert.Equal(0, calls);

        manager.Update(new Size(20, 10));
        Assert.Equal(1, calls);
    }

    [Fact]
    public void UpdateReentryFromDispatcherActionIsRejected()
    {
        var manager = new UiManager();
        Exception? error = null;
        manager.Dispatcher.Post(() =>
            error = Record.Exception(() => manager.Update(new Size(20, 10))));

        manager.Update(new Size(20, 10));

        Assert.IsType<InvalidOperationException>(error);
    }

    [Fact]
    public void ShutdownWithoutScreenClosesDispatcherAndIsIdempotent()
    {
        var manager = new UiManager();

        manager.Shutdown();
        manager.Shutdown();
        var backgroundError = RunOnBackgroundThread(() =>
            Record.Exception(manager.Shutdown));

        Assert.Null(backgroundError);
        Assert.False(manager.Dispatcher.CheckAccess());
        Assert.Null(manager.CurrentScreen);
    }

    [Fact]
    public void ShutdownDetachesScreenAroundLifecycleCallbacks()
    {
        var manager = new UiManager();
        var root = new TestNode();
        var states = new List<(string Name, UiDispatcher? Dispatcher, Screen? Current)>();
        var screen = new RecordingScreen(root)
        {
            Closing = current =>
                states.Add(("closing", root.ActiveDispatcher, current.CurrentScreen)),
            Closed = current =>
                states.Add(("closed", root.ActiveDispatcher, current.CurrentScreen))
        };
        manager.Open(screen);

        manager.Shutdown();

        Assert.Collection(
            states,
            state =>
            {
                Assert.Equal("closing", state.Name);
                Assert.Same(manager.Dispatcher, state.Dispatcher);
                Assert.Same(screen, state.Current);
            },
            state =>
            {
                Assert.Equal("closed", state.Name);
                Assert.Null(state.Dispatcher);
                Assert.Null(state.Current);
            });
        Assert.Null(root.ActiveDispatcher);
        Assert.Null(manager.CurrentScreen);
        Assert.False(manager.Dispatcher.CheckAccess());
    }

    [Fact]
    public void ShutdownCompletesAfterSingleClosingError()
    {
        var manager = new UiManager();
        var expected = new InvalidOperationException("closing failed");
        var screen = new RecordingScreen(new TestNode())
        {
            Closing = _ => throw expected
        };
        manager.Open(screen);

        var actual = Assert.Throws<InvalidOperationException>(manager.Shutdown);

        Assert.Same(expected, actual);
        Assert.Null(manager.CurrentScreen);
        Assert.Null(screen.Root.ActiveDispatcher);
        Assert.False(manager.Dispatcher.CheckAccess());
    }

    [Fact]
    public void ShutdownCompletesAfterSingleClosedError()
    {
        var manager = new UiManager();
        var expected = new InvalidOperationException("closed failed");
        var screen = new RecordingScreen(new TestNode())
        {
            Closed = _ => throw expected
        };
        manager.Open(screen);

        var actual = Assert.Throws<InvalidOperationException>(manager.Shutdown);

        Assert.Same(expected, actual);
        Assert.Null(manager.CurrentScreen);
        Assert.Null(screen.Root.ActiveDispatcher);
        Assert.False(manager.Dispatcher.CheckAccess());
    }

    [Fact]
    public void ShutdownAggregatesClosingAndClosedErrorsInOrder()
    {
        var manager = new UiManager();
        var closingError = new InvalidOperationException("closing failed");
        var closedError = new InvalidOperationException("closed failed");
        var screen = new RecordingScreen(new TestNode())
        {
            Closing = _ => throw closingError,
            Closed = _ => throw closedError
        };
        manager.Open(screen);

        var aggregate = Assert.Throws<AggregateException>(manager.Shutdown);

        Assert.Collection(
            aggregate.InnerExceptions,
            error => Assert.Same(closingError, error),
            error => Assert.Same(closedError, error));
        Assert.Null(manager.CurrentScreen);
        Assert.Null(screen.Root.ActiveDispatcher);
        Assert.False(manager.Dispatcher.CheckAccess());
    }

    [Fact]
    public void ShutdownDuringDrainCompletesCurrentActionAndSkipsLayout()
    {
        var manager = new UiManager();
        var root = new TestNode { CoreDesiredSize = new Size(5, 5) };
        manager.Open(new Screen(root));
        var events = new List<string>();
        manager.Dispatcher.Post(() =>
        {
            events.Add("current-start");
            manager.Shutdown();
            events.Add("current-end");
        });
        manager.Dispatcher.Post(() => events.Add("discarded"));

        manager.Update(new Size(20, 10));

        Assert.Equal(["current-start", "current-end"], events);
        Assert.Equal(0, root.MeasureCalls);
        Assert.Equal(0, root.ArrangeCalls);
        Assert.Null(manager.CurrentScreen);
        Assert.False(manager.Dispatcher.CheckAccess());
    }

    [Fact]
    public void ClosedManagerRejectsOperationsAndKeepsNullCurrentScreen()
    {
        var manager = new UiManager();
        manager.Shutdown();

        Assert.Throws<ObjectDisposedException>(() =>
            manager.Open(new Screen(new TestNode())));
        Assert.Throws<ObjectDisposedException>(manager.Close);
        Assert.Throws<ObjectDisposedException>(() =>
            manager.Update(new Size(20, 10)));
        Assert.Throws<ObjectDisposedException>(() =>
            manager.Dispatcher.Post(() => { }));
        Assert.Null(manager.CurrentScreen);
    }

    private static void CaptureNestedOperationErrors(
        UiManager manager,
        ICollection<Exception?> errors)
    {
        errors.Add(Record.Exception(() => manager.Open(new Screen(new TestNode()))));
        errors.Add(Record.Exception(manager.Close));
        errors.Add(Record.Exception(manager.Shutdown));
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
        internal Exception? MeasureException { get; set; }
        internal Exception? ArrangeException { get; set; }
        internal int MeasureCalls { get; private set; }
        internal int ArrangeCalls { get; private set; }

        protected override Size MeasureCore(Size availableSize)
        {
            MeasureCalls++;
            LastMeasureConstraint = availableSize;
            if (MeasureException is not null)
                throw MeasureException;

            MeasureAction?.Invoke();
            return CoreDesiredSize;
        }

        protected override void ArrangeCore(Size finalSize)
        {
            ArrangeCalls++;
            LastArrangeSize = finalSize;
            if (ArrangeException is not null)
                throw ArrangeException;

            ArrangeAction?.Invoke();
        }
    }

    private sealed class TestParent : Parent
    {
        internal void Add(UiNode child) =>
            AddChild(child);

        internal bool Remove(UiNode child) =>
            RemoveChild(child);
    }

    private sealed class RecordingScreen(UiNode root) : Screen(root)
    {
        internal Action<UiManager>? Opening { get; set; }
        internal Action<UiManager>? Opened { get; set; }
        internal Action<UiManager>? Closing { get; set; }
        internal Action<UiManager>? Closed { get; set; }

        protected override void OnOpening(UiManager manager) =>
            Opening?.Invoke(manager);

        protected override void OnOpened(UiManager manager) =>
            Opened?.Invoke(manager);

        protected override void OnClosing(UiManager manager) =>
            Closing?.Invoke(manager);

        protected override void OnClosed(UiManager manager) =>
            Closed?.Invoke(manager);
    }
}
