using System.Runtime.ExceptionServices;
using PanguEngine.Client.UI;

namespace PanguEngine.Tests.Client.UI;

public sealed class UiScreenTests
{
    [Fact]
    public void UiScreenSupportsOptionalRootAndAssociatesItWhileClosed()
    {
        var root = new LayoutNode();
        var screen = new UiScreen(root);
        var emptyScreen = new UiScreen();

        Assert.Same(root, screen.Root);
        Assert.Same(screen, root.Screen);
        Assert.Null(emptyScreen.Root);
    }

    [Fact]
    public void RootCanBeReplacedAndClearedWhileClosed()
    {
        var oldRoot = new Canvas();
        var oldChild = new LayoutNode();
        oldRoot.Children.Add(oldChild);
        var newRoot = new Canvas();
        var newChild = new LayoutNode();
        newRoot.Children.Add(newChild);
        var screen = new UiScreen(oldRoot);

        screen.Root = newRoot;

        Assert.Null(oldRoot.Screen);
        Assert.Null(oldChild.Screen);
        Assert.Same(screen, newRoot.Screen);
        Assert.Same(screen, newChild.Screen);

        screen.Root = null;

        Assert.Null(screen.Root);
        Assert.Null(newRoot.Screen);
        Assert.Null(newChild.Screen);
    }

    [Fact]
    public void RootTransferWhileClosedInvalidatesExistingLayoutCacheOnReopen()
    {
        var manager = new UiManager();
        var incoming = new LayoutNode { Width = 50, Height = 50 };
        var source = new UiScreen(incoming);
        var target = new UiScreen(new LayoutNode());
        manager.Open(source);
        manager.Update(new Size(100, 100));
        manager.Close();
        Assert.True(incoming.IsMeasureValid);
        Assert.True(incoming.IsArrangeValid);
        Assert.Equal(1, incoming.ArrangeCalls);

        target.Root = incoming;

        Assert.Null(source.Root);
        Assert.False(incoming.IsMeasureValid);
        Assert.False(incoming.IsArrangeValid);

        manager.Open(target);
        manager.Update(new Size(100, 100));

        Assert.True(incoming.IsMeasureValid);
        Assert.True(incoming.IsArrangeValid);
        Assert.Equal(new Rect(25, 25, 50, 50), incoming.LayoutBounds);
        Assert.Equal(2, incoming.ArrangeCalls);
        manager.Close();
    }

    [Fact]
    public void NullRootUiScreenCompletesLifecycleAndUpdate()
    {
        var calls = 0;
        var screen = new UiScreen();
        screen.Open();
        screen.Post(() => calls++);

        screen.Update(new Size(20, 20));
        screen.Close();

        Assert.Equal(1, calls);
        Assert.Null(screen.Root);
    }

    [Fact]
    public void ClosedUiScreenTreeCanBeModifiedOnAnotherThread()
    {
        var root = new Canvas();
        var screen = new UiScreen(root);
        screen.Open();
        screen.Close();
        Exception? error = null;
        var thread = new Thread(() =>
            error = Record.Exception(() => root.Children.Add(new LayoutNode())));

        thread.Start();
        thread.Join();

        Assert.Null(error);
        Assert.Same(screen, root.Children[0].Screen);
    }

    [Fact]
    public void RootMovesFromParentToUiScreen()
    {
        var sourceRoot = new Canvas();
        var incoming = new Canvas();
        var leaf = new LayoutNode();
        incoming.Children.Add(leaf);
        sourceRoot.Children.Add(incoming);
        var source = new UiScreen(sourceRoot);
        var target = new UiScreen();

        target.Root = incoming;

        Assert.Empty(sourceRoot.Children);
        Assert.Null(incoming.Parent);
        Assert.Same(source, sourceRoot.Screen);
        Assert.Same(target, incoming.Screen);
        Assert.Same(target, leaf.Screen);
    }

    [Fact]
    public void RootMovesBetweenUiScreensAndClearsSourceRoot()
    {
        var incoming = new Canvas();
        var leaf = new LayoutNode();
        incoming.Children.Add(leaf);
        var source = new UiScreen(incoming);
        var target = new UiScreen();

        target.Root = incoming;

        Assert.Null(source.Root);
        Assert.Same(incoming, target.Root);
        Assert.Same(target, incoming.Screen);
        Assert.Same(target, leaf.Screen);
    }

    [Fact]
    public void RootMovesBetweenOpenUiScreensOnTheirOwnerThread()
    {
        var incoming = new LayoutNode();
        var oldTargetRoot = new LayoutNode();
        var source = new UiScreen(incoming);
        var target = new UiScreen(oldTargetRoot);
        source.Open();
        target.Open();

        target.Root = incoming;

        Assert.Null(source.Root);
        Assert.Null(oldTargetRoot.Screen);
        Assert.Same(incoming, target.Root);
        Assert.Same(target, incoming.Screen);
        target.Close();
        source.Close();
    }

    [Fact]
    public void DescendantCanBecomeRootWithoutLeavingTheSameUiScreen()
    {
        var oldRoot = new Canvas();
        var incoming = new Canvas();
        var leaf = new LayoutNode();
        incoming.Children.Add(leaf);
        oldRoot.Children.Add(incoming);
        var screen = new UiScreen(oldRoot);

        screen.Root = incoming;

        Assert.Null(incoming.Parent);
        Assert.Null(oldRoot.Screen);
        Assert.Same(screen, incoming.Screen);
        Assert.Same(screen, leaf.Screen);
    }

    [Fact]
    public void RootTransferAcrossDifferentOwnerThreadsFailsBeforeMutation()
    {
        var ready = new ManualResetEventSlim();
        var release = new ManualResetEventSlim();
        UiScreen source = null!;
        Canvas incoming = null!;
        var thread = new Thread(() =>
        {
            incoming = new Canvas();
            source = new UiScreen(incoming);
            source.Open();
            ready.Set();
            release.Wait();
            source.Close();
        });
        thread.Start();
        ready.Wait();
        var oldRoot = new Canvas();
        var target = new UiScreen(oldRoot);
        target.Open();
        try
        {
            var error = Record.Exception(() => target.Root = incoming);

            Assert.IsType<InvalidOperationException>(error);
            Assert.Same(incoming, source.Root);
            Assert.Same(source, incoming.Screen);
            Assert.Same(oldRoot, target.Root);
            Assert.Same(target, oldRoot.Screen);
        }
        finally
        {
            target.Close();
            release.Set();
            thread.Join();
        }
    }

    [Fact]
    public void RootCannotChangeDuringLayout()
    {
        var oldRoot = new LayoutNode();
        var replacement = new LayoutNode();
        var screen = new UiScreen(oldRoot);
        Exception? error = null;
        oldRoot.MeasureAction = () =>
            error = Record.Exception(() => screen.Root = replacement);
        screen.Open();

        screen.Update(new Size(20, 20));

        Assert.IsType<InvalidOperationException>(error);
        Assert.Same(oldRoot, screen.Root);
        Assert.Same(screen, oldRoot.Screen);
        Assert.Null(replacement.Screen);
        screen.Close();
    }

    [Fact]
    public void AssigningCurrentRootIsANoOpBeforeThreadValidation()
    {
        var root = new LayoutNode();
        var screen = new UiScreen(root);
        screen.Open();
        Exception? error = null;
        var thread = new Thread(() =>
            error = Record.Exception(() => screen.Root = root));

        thread.Start();
        thread.Join();

        Assert.Null(error);
        Assert.Same(root, screen.Root);
        Assert.Same(screen, root.Screen);
        screen.Close();
    }

    [Fact]
    public void OpenAndCloseExposeExpectedRootStatesToCallbacks()
    {
        var root = new LayoutNode();
        var states = new List<string>();
        RecordingUiScreen screen = null!;
        screen = new RecordingUiScreen(root)
        {
            Opening = () => states.Add(ReferenceEquals(root.Screen, screen) ? "opening-associated" : "opening-unassociated"),
            Opened = () => states.Add(ReferenceEquals(root.Screen, screen) ? "opened-associated" : "opened-unassociated"),
            Closing = () => states.Add(ReferenceEquals(root.Screen, screen) ? "closing-associated" : "closing-unassociated"),
            Closed = () => states.Add(ReferenceEquals(root.Screen, screen) ? "closed-associated" : "closed-unassociated")
        };

        screen.Open();
        screen.Close();

        Assert.Equal(
            ["opening-associated", "opened-associated", "closing-associated", "closed-associated"],
            states);
        Assert.Same(screen, root.Screen);
    }

    [Fact]
    public void RootCanChangeInEveryLifecycleCallback()
    {
        var openingRoot = new LayoutNode();
        var openedRoot = new LayoutNode();
        var closingRoot = new LayoutNode();
        var closedRoot = new LayoutNode();
        RecordingUiScreen screen = null!;
        screen = new RecordingUiScreen
        {
            Opening = () => screen.Root = openingRoot,
            Opened = () => screen.Root = openedRoot,
            Closing = () => screen.Root = closingRoot,
            Closed = () => screen.Root = closedRoot
        };

        screen.Open();
        screen.Close();

        Assert.Null(openingRoot.Screen);
        Assert.Null(openedRoot.Screen);
        Assert.Null(closingRoot.Screen);
        Assert.Same(closedRoot, screen.Root);
        Assert.Same(screen, closedRoot.Screen);
    }

    [Fact]
    public void NeverOpenedAndClosedScreenCloseOperationsAreNoOps()
    {
        var screen = new UiScreen(new LayoutNode());

        screen.Close();
        screen.Open();
        screen.Close();
        screen.Close();

        Assert.Same(screen, screen.Root!.Screen);
    }

    [Fact]
    public void ClosedScreenCanOpenOnAnotherThread()
    {
        var screen = new UiScreen(new LayoutNode());
        screen.Open();
        screen.Close();
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                screen.Open();
                screen.Close();
            }
            catch (Exception exception)
            {
                error = exception;
            }
        });

        thread.Start();
        thread.Join();

        Assert.Null(error);
        Assert.Same(screen, screen.Root!.Screen);
    }

    [Fact]
    public void OpeningAnAlreadyOpenScreenFailsWithoutChangingState()
    {
        var screen = new UiScreen(new LayoutNode());
        screen.Open();

        Assert.Throws<InvalidOperationException>(screen.Open);
        Assert.Same(screen, screen.Root!.Screen);

        screen.Close();
    }

    [Fact]
    public void PostRequiresAnOpenScreen()
    {
        var screen = new UiScreen(new LayoutNode());

        Assert.Throws<InvalidOperationException>(() => screen.Post(() => { }));
        screen.Open();
        screen.Close();
        Assert.Throws<InvalidOperationException>(() => screen.Post(() => { }));
    }

    [Fact]
    public void OpeningAndOpenedCanPostUntilUpdate()
    {
        var calls = 0;
        RecordingUiScreen screen = null!;
        screen = new RecordingUiScreen(new LayoutNode())
        {
            Opening = () => screen.Post(() => calls++),
            Opened = () => screen.Post(() => calls++)
        };

        screen.Open();

        Assert.Equal(0, calls);
        screen.Update(new Size(20, 20));
        Assert.Equal(2, calls);
        screen.Close();
    }

    [Fact]
    public void PostPreservesFifoAndDefersActionsPostedDuringUpdate()
    {
        var calls = new List<string>();
        var screen = new RecordingUiScreen(new LayoutNode());
        screen.Opened = () => screen.Post(() =>
        {
            calls.Add("first");
            screen.Post(() => calls.Add("deferred"));
        });

        screen.Open();
        screen.Post(() => calls.Add("second"));
        screen.Update(new Size(20, 20));

        Assert.Equal(["first", "second"], calls);
        screen.Update(new Size(20, 20));
        Assert.Equal(["first", "second", "deferred"], calls);
        screen.Close();
    }

    [Fact]
    public void PostCanBeCalledFromBackgroundThread()
    {
        var screen = new UiScreen(new LayoutNode());
        var calls = 0;
        screen.Open();

        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                screen.Post(() => calls++);
            }
            catch (Exception exception)
            {
                error = exception;
            }
        });
        thread.Start();
        thread.Join();

        Assert.Null(error);
        Assert.Equal(0, calls);
        screen.Update(new Size(20, 20));
        Assert.Equal(1, calls);
        screen.Close();
    }

    [Fact]
    public void ActionFailureStopsTheBatchAndPreservesRemainingActions()
    {
        var expected = new InvalidOperationException("action failed");
        var calls = new List<string>();
        var screen = new UiScreen(new LayoutNode());
        screen.Open();
        screen.Post(() =>
        {
            calls.Add("failing");
            throw expected;
        });
        screen.Post(() => calls.Add("remaining"));

        var actual = Assert.Throws<InvalidOperationException>(() => screen.Update(new Size(20, 20)));

        Assert.Same(expected, actual);
        Assert.Equal(["failing"], calls);
        screen.Update(new Size(20, 20));
        Assert.Equal(["failing", "remaining"], calls);
        screen.Close();
    }

    [Fact]
    public void CloseRejectsNewPostsAndClearsPendingActions()
    {
        var calls = 0;
        Exception? postError = null;
        RecordingUiScreen screen = null!;
        screen = new RecordingUiScreen(new LayoutNode())
        {
            Closing = () => postError = Record.Exception(() => screen.Post(() => calls++))
        };
        screen.Open();
        screen.Post(() => calls++);

        screen.Close();
        screen.Open();
        screen.Update(new Size(20, 20));

        Assert.Equal(0, calls);
        Assert.IsType<InvalidOperationException>(postError);
        screen.Close();
    }

    [Fact]
    public void CloseInsidePostRejectsSameScreenReopenUntilUpdateReturns()
    {
        var root = new LayoutNode();
        var screen = new UiScreen(root);
        Exception? reopenError = null;
        screen.Open();
        screen.Update(new Size(20, 20));
        var arrangeCalls = root.ArrangeCalls;
        screen.Post(() =>
        {
            screen.Close();
            reopenError = Record.Exception(screen.Open);
        });

        screen.Update(new Size(20, 20));

        Assert.IsType<InvalidOperationException>(reopenError);
        Assert.Equal(arrangeCalls, root.ArrangeCalls);
        Assert.Same(screen, root.Screen);
        screen.Open();
        screen.Close();
    }

    [Fact]
    public void CloseInsidePostRejectsBackgroundReopenUntilUpdateReturns()
    {
        var screen = new UiScreen(new LayoutNode());
        var closed = new ManualResetEventSlim();
        var attempted = new ManualResetEventSlim();
        Exception? reopenError = null;
        screen.Open();
        screen.Post(() =>
        {
            screen.Close();
            closed.Set();
            attempted.Wait();
        });
        var thread = new Thread(() =>
        {
            closed.Wait();
            reopenError = Record.Exception(screen.Open);
            attempted.Set();
        });
        thread.Start();

        screen.Update(new Size(20, 20));
        thread.Join();

        Assert.IsType<InvalidOperationException>(reopenError);
        Exception? afterUpdateError = null;
        var afterUpdateThread = new Thread(() =>
        {
            afterUpdateError = Record.Exception(() =>
            {
                screen.Open();
                screen.Close();
            });
        });
        afterUpdateThread.Start();
        afterUpdateThread.Join();
        Assert.Null(afterUpdateError);
    }

    [Fact]
    public void ActionClosingBeforeThrowingPropagatesAndSkipsLayout()
    {
        var expected = new InvalidOperationException("action failed");
        var root = new LayoutNode();
        var screen = new UiScreen(root);
        screen.Open();
        screen.Update(new Size(20, 20));
        var arrangeCalls = root.ArrangeCalls;
        screen.Post(() =>
        {
            screen.Close();
            throw expected;
        });

        var actual = Assert.Throws<InvalidOperationException>(() => screen.Update(new Size(20, 20)));

        Assert.Same(expected, actual);
        Assert.Equal(arrangeCalls, root.ArrangeCalls);
        Assert.Same(screen, root.Screen);
    }

    [Fact]
    public void OpeningFailureClearsPostsAndCanBeRetried()
    {
        var postedCalls = 0;
        var expected = new InvalidOperationException("opening failed");
        RecordingUiScreen screen = null!;
        screen = new RecordingUiScreen(new LayoutNode())
        {
            Opening = () =>
            {
                screen.Post(() => postedCalls++);
                throw expected;
            }
        };

        var actual = Assert.Throws<InvalidOperationException>(screen.Open);

        Assert.Same(expected, actual);
        Assert.Same(screen, screen.Root!.Screen);
        screen.Opening = null;
        screen.Open();
        screen.Update(new Size(20, 20));
        Assert.Equal(0, postedCalls);
        screen.Close();
    }

    [Fact]
    public void OpenedFailureClearsPostsAndCompletesClose()
    {
        var postedCalls = 0;
        var expected = new InvalidOperationException("opened failed");
        RecordingUiScreen screen = null!;
        screen = new RecordingUiScreen(new LayoutNode())
        {
            Opened = () =>
            {
                screen.Post(() => postedCalls++);
                throw expected;
            }
        };

        var actual = Assert.Throws<InvalidOperationException>(screen.Open);

        Assert.Same(expected, actual);
        Assert.Same(screen, screen.Root!.Screen);
        screen.Opened = null;
        screen.Open();
        screen.Update(new Size(20, 20));
        Assert.Equal(0, postedCalls);
        screen.Close();
    }

    [Fact]
    public void UpdateMeasuresAndArrangesZeroOriginViewport()
    {
        var root = new LayoutNode();
        var screen = new UiScreen(root);
        screen.Open();

        screen.Update(new Size(100, 80));

        Assert.Equal(new Size(100, 80), root.LastMeasureConstraint);
        Assert.Equal(new Size(100, 80), root.LastArrangeSize);
        Assert.Equal(new Rect(0, 0, 100, 80), root.LayoutBounds);
        screen.Close();
    }

    [Fact]
    public void OpenUiScreenOperationsRejectWrongThread()
    {
        var screen = new UiScreen(new LayoutNode());
        screen.Open();
        Exception? updateError = null;
        Exception? closeError = null;
        var thread = new Thread(() =>
        {
            updateError = Record.Exception(() => screen.Update(new Size(20, 20)));
            closeError = Record.Exception(screen.Close);
        });

        thread.Start();
        thread.Join();

        Assert.IsType<InvalidOperationException>(updateError);
        Assert.IsType<InvalidOperationException>(closeError);
        screen.Close();
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

    private sealed class LayoutNode : UiNode
    {
        internal Action? MeasureAction { get; set; }
        internal Size LastMeasureConstraint { get; private set; }
        internal Size LastArrangeSize { get; private set; }
        internal int ArrangeCalls { get; private set; }

        protected override Size MeasureCore(Size availableSize)
        {
            MeasureAction?.Invoke();
            LastMeasureConstraint = availableSize;
            return availableSize;
        }

        protected override void ArrangeCore(Size finalSize)
        {
            LastArrangeSize = finalSize;
            ArrangeCalls++;
        }
    }
}
