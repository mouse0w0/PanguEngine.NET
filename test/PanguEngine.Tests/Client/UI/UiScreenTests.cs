using PanguEngine.Client.Screens;
using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Drawing;
using PanguEngine.Client.UI.Styling;

namespace PanguEngine.Tests.Client.UI;

public sealed class UiScreenTests
{
    [Fact]
    public void GameBehaviorDefaultsToDisabledAndCanBeConfigured()
    {
        var defaultScreen = new UiScreen();
        var configuredScreen = new UiScreen
        {
            PausesGame = true,
            CloseOnEscape = true
        };

        Assert.False(defaultScreen.PausesGame);
        Assert.False(defaultScreen.CloseOnEscape);
        Assert.True(configuredScreen.PausesGame);
        Assert.True(configuredScreen.CloseOnEscape);
    }

    [Fact]
    public void PauseScreenEnablesGamePauseAndEscapeClose()
    {
        var screen = new PauseScreen();

        Assert.True(screen.PausesGame);
        Assert.True(screen.CloseOnEscape);
    }

    [Fact]
    public void PauseScreenExitButtonUsesDangerClassWithoutLocalColors()
    {
        var screen = new PauseScreen();
        var root = Assert.IsType<Panel>(screen.Root);
        var panel = Assert.IsType<StackPanel>(Assert.Single(root.Children));
        var buttons = panel.Children.OfType<Button>().ToArray();

        Assert.Equal(2, buttons.Length);
        Assert.DoesNotContain("danger", buttons[0].Classes);
        var exit = buttons[1];
        Assert.Contains("danger", exit.Classes);
        Assert.Equal(new SolidColorBrush(104, 43, 45), exit.Background);
        Assert.Equal(new SolidColorBrush(157, 73, 77), exit.BorderBrush);
        Assert.Equal("Button.danger", Assert.Single(exit.GetStyleValueSources(Region.BackgroundProperty)).SelectorText);
        Assert.False(Assert.Single(exit.GetStyleValueSources(Region.BackgroundProperty)).IsMaskedByLocalValue);
        Assert.False(Assert.Single(exit.GetStyleValueSources(Region.BorderBrushProperty)).IsMaskedByLocalValue);
    }

    [Fact]
    public void ScaleDefaultsToOneAndRejectsInvalidValuesWithoutChangingState()
    {
        var screen = new UiScreen();
        Assert.Equal(1, screen.Scale);
        Assert.True(screen.UseLayoutRounding);

        screen.Scale = 1.5;

        Assert.Equal(1.5, screen.Scale);
        Assert.Throws<ArgumentOutOfRangeException>(() => screen.Scale = double.NaN);
        Assert.Throws<ArgumentOutOfRangeException>(() => screen.Scale = double.PositiveInfinity);
        Assert.Throws<ArgumentOutOfRangeException>(() => screen.Scale = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => screen.Scale = -1);
        Assert.Equal(1.5, screen.Scale);
    }

    [Fact]
    public void OpenUiScreenScaleRejectsWrongThread()
    {
        var screen = new UiScreen();
        screen.Open();
        Exception? error = null;
        var thread = new Thread(() =>
            error = Record.Exception(() => screen.Scale = 2));

        thread.Start();
        thread.Join();

        Assert.IsType<InvalidOperationException>(error);
        Assert.Equal(1, screen.Scale);
        screen.Close();
    }

    [Fact]
    public void OpenUiScreenLayoutRoundingRejectsWrongThreadButAllowsSameValue()
    {
        var screen = new UiScreen();
        screen.Open();
        Exception? changeError = null;
        Exception? sameValueError = null;
        var thread = new Thread(() =>
        {
            changeError = Record.Exception(() => screen.UseLayoutRounding = false);
            sameValueError = Record.Exception(() => screen.UseLayoutRounding = true);
        });

        thread.Start();
        thread.Join();

        Assert.IsType<InvalidOperationException>(changeError);
        Assert.Null(sameValueError);
        Assert.True(screen.UseLayoutRounding);
        screen.Close();
    }

    [Fact]
    public void UiScreenScaleCannotChangeDuringLayout()
    {
        UiScreen screen = null!;
        Exception? error = null;
        var root = new LayoutNode
        {
            MeasureAction = () => error = Record.Exception(() => screen.Scale = 2)
        };
        screen = new UiScreen(root);
        screen.Open();

        screen.PrepareFrame(new Size(100, 80), 0);

        Assert.IsType<InvalidOperationException>(error);
        Assert.Equal(1, screen.Scale);
        screen.Close();
    }

    [Fact]
    public void UiScreenLayoutRoundingCannotChangeDuringLayout()
    {
        UiScreen screen = null!;
        Exception? error = null;
        var root = new LayoutNode
        {
            MeasureAction = () =>
                error = Record.Exception(() => screen.UseLayoutRounding = false)
        };
        screen = new UiScreen(root);
        screen.Open();

        screen.PrepareFrame(new Size(100, 80), 0);

        Assert.IsType<InvalidOperationException>(error);
        Assert.True(screen.UseLayoutRounding);
        screen.Close();
    }

    [Fact]
    public void ManualMeasureCallbackCanChangeScaleButCannotCommitInvalidatedPass()
    {
        UiScreen screen = null!;
        Exception? error = null;
        var root = new LayoutNode
        {
            MeasureAction = () => error = Record.Exception(() => screen.Scale = 2)
        };
        screen = new UiScreen(root);

        root.Measure(new Size(100, 80));

        Assert.Null(error);
        Assert.Equal(2, screen.Scale);
        Assert.False(root.IsMeasureValid);
        Assert.False(root.IsArrangeValid);
    }

    [Fact]
    public void ScaleChangeImmediatelyInvalidatesEntireRootSubtree()
    {
        var root = new Canvas();
        var child = new Canvas();
        var grandchild = new LayoutNode { Width = 1, Height = 1 };
        child.Children.Add(grandchild);
        root.Children.Add(child);
        var screen = new UiScreen(root);
        screen.Open();
        screen.PrepareFrame(new Size(100, 80), 0);
        Assert.True(root.IsMeasureValid);
        Assert.True(child.IsMeasureValid);
        Assert.True(grandchild.IsMeasureValid);

        screen.Scale = 2;

        Assert.False(root.IsMeasureValid);
        Assert.False(root.IsArrangeValid);
        Assert.False(child.IsMeasureValid);
        Assert.False(child.IsArrangeValid);
        Assert.False(grandchild.IsMeasureValid);
        Assert.False(grandchild.IsArrangeValid);
        screen.Close();
    }

    [Fact]
    public void LayoutRoundingChangeImmediatelyInvalidatesEntireRootSubtree()
    {
        var root = new Canvas();
        var child = new Canvas();
        var grandchild = new LayoutNode { Width = 1, Height = 1 };
        child.Children.Add(grandchild);
        root.Children.Add(child);
        var screen = new UiScreen(root);
        screen.Open();
        screen.PrepareFrame(new Size(100, 80), 0);

        screen.UseLayoutRounding = false;

        Assert.False(root.IsMeasureValid);
        Assert.False(root.IsArrangeValid);
        Assert.False(child.IsMeasureValid);
        Assert.False(child.IsArrangeValid);
        Assert.False(grandchild.IsMeasureValid);
        Assert.False(grandchild.IsArrangeValid);
        screen.Close();
    }

    [Fact]
    public void ExtremelySmallScaleRejectsNonFiniteLogicalViewportBeforeLayout()
    {
        var root = new LayoutNode();
        var screen = new UiScreen(root) { Scale = double.Epsilon };
        screen.Open();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            screen.PrepareFrame(new Size(100, 80), 0));

        Assert.Equal(0, root.MeasureCalls);
        Assert.Equal(0, root.ArrangeCalls);
        screen.Close();
    }

    [Fact]
    public void AssigningSameScaleDoesNotInvalidateLayout()
    {
        var root = new LayoutNode();
        var screen = new UiScreen(root);
        screen.Open();
        screen.PrepareFrame(new Size(100, 80), 0);

        screen.Scale = 1;

        Assert.True(root.IsMeasureValid);
        Assert.True(root.IsArrangeValid);
        screen.Close();
    }

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
        manager.PrepareFrame(new Size(100, 100), 0);
        manager.Close();
        Assert.True(incoming.IsMeasureValid);
        Assert.True(incoming.IsArrangeValid);
        Assert.Equal(1, incoming.ArrangeCalls);

        target.Root = incoming;

        Assert.Null(source.Root);
        Assert.False(incoming.IsMeasureValid);
        Assert.False(incoming.IsArrangeValid);

        manager.Open(target);
        manager.PrepareFrame(new Size(100, 100), 0);

        Assert.True(incoming.IsMeasureValid);
        Assert.True(incoming.IsArrangeValid);
        Assert.Equal(new Rect(25, 25, 50, 50), incoming.LayoutBounds);
        Assert.Equal(2, incoming.ArrangeCalls);
        manager.Close();
    }

    [Fact]
    public void RootTransferAcrossScreensWithEqualLayoutSettingsInvalidatesEntireSubtree()
    {
        var incoming = new Canvas();
        var child = new Canvas();
        var grandchild = new LayoutNode { Width = 1, Height = 1 };
        child.Children.Add(grandchild);
        incoming.Children.Add(child);
        var source = new UiScreen(incoming);
        var target = new UiScreen();
        source.Open();
        target.Open();
        source.PrepareFrame(new Size(100, 100), 0);
        Assert.True(incoming.IsMeasureValid);
        Assert.True(child.IsMeasureValid);
        Assert.True(grandchild.IsMeasureValid);

        target.Root = incoming;

        Assert.False(incoming.IsMeasureValid);
        Assert.False(incoming.IsArrangeValid);
        Assert.False(child.IsMeasureValid);
        Assert.False(child.IsArrangeValid);
        Assert.False(grandchild.IsMeasureValid);
        Assert.False(grandchild.IsArrangeValid);
        target.Close();
        source.Close();
    }

    [Fact]
    public void NullRootUiScreenCompletesLifecycleAndUpdate()
    {
        var calls = 0;
        var fixedUpdates = 0;
        var frameUpdates = 0;
        var screen = new RecordingUiScreen
        {
            FixedAction = () => fixedUpdates++,
            FrameAction = _ => frameUpdates++
        };
        screen.Open();
        screen.Post(() => calls++);

        screen.Update();
        Assert.Equal(0, calls);
        screen.PrepareFrame(new Size(20, 20), 0);
        screen.Close();

        Assert.Equal(1, calls);
        Assert.Equal(1, fixedUpdates);
        Assert.Equal(1, frameUpdates);
        Assert.Null(screen.Root);
    }

    [Fact]
    public void FixedUpdateDoesNotDrainPostsOrLayout()
    {
        var events = new List<string>();
        var root = new LayoutNode { MeasureAction = () => events.Add("layout") };
        var screen = new RecordingUiScreen(root)
        {
            FixedAction = () => events.Add("fixed"),
            FrameAction = _ => events.Add("frame")
        };
        screen.Open();
        screen.Post(() => events.Add("post"));

        screen.Update();

        Assert.Equal(["fixed"], events);
        Assert.False(root.IsMeasureValid);

        screen.PrepareFrame(new Size(100, 100), 0.25);

        Assert.Equal(["fixed", "post", "frame", "layout"], events);
        screen.Close();
    }

    [Theory]
    [InlineData(Visibility.Hidden)]
    [InlineData(Visibility.Collapsed)]
    public void VisibilityDoesNotSuppressUpdates(Visibility visibility)
    {
        var fixedUpdates = 0;
        var frameUpdates = 0;
        var screen = new RecordingUiScreen(new LayoutNode { Visibility = visibility })
        {
            FixedAction = () => fixedUpdates++,
            FrameAction = _ => frameUpdates++
        };
        screen.Open();

        screen.Update();
        screen.PrepareFrame(new Size(100, 100), 0);

        Assert.Equal(1, fixedUpdates);
        Assert.Equal(1, frameUpdates);
        screen.Close();
    }

    [Fact]
    public void PostsFromFrameCallbackWaitUntilNextFrame()
    {
        var events = new List<string>();
        var screen = new RecordingUiScreen();
        screen.FrameAction = _ =>
        {
            events.Add("frame");
            screen.Post(() => events.Add("posted"));
        };
        screen.Open();

        screen.PrepareFrame(new Size(10, 10), 0);
        Assert.Equal(["frame"], events);
        screen.PrepareFrame(new Size(10, 10), 0);
        Assert.Equal(["frame", "posted", "frame"], events);
        screen.Close();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void UpdatingScreenRejectsReentryAndDirectDrawing(bool fixedUpdate)
    {
        var screen = new RecordingUiScreen();
        var calls = 0;
        void Check()
        {
            calls++;
            Assert.Throws<InvalidOperationException>(screen.Update);
            Assert.Throws<InvalidOperationException>(() => screen.PrepareFrame(new Size(10, 10), 0));
            Assert.Throws<InvalidOperationException>(() => screen.CreateDrawCommandList());
            Assert.Throws<InvalidOperationException>(() => new UiDrawCommandList().Append(screen));
        }
        screen.FixedAction = Check;
        screen.FrameAction = _ => Check();
        screen.Open();

        if (fixedUpdate)
            screen.Update();
        else
            screen.PrepareFrame(new Size(10, 10), 0);

        Assert.Equal(1, calls);
        Assert.Null(Record.Exception(() => screen.CreateDrawCommandList()));
        screen.Close();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ClosingDuringCallbackCannotReopenSameScreenUntilCallbackReturns(bool fixedUpdate)
    {
        var screen = new RecordingUiScreen();
        void CloseAndReopen()
        {
            screen.Close();
            Assert.Throws<InvalidOperationException>(screen.Open);
        }
        screen.FixedAction = CloseAndReopen;
        screen.FrameAction = _ => CloseAndReopen();
        screen.Open();

        if (fixedUpdate)
            screen.Update();
        else
            screen.PrepareFrame(new Size(10, 10), 0);

        Assert.False(screen.IsOpen());
        screen.Open();
        screen.Close();
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

        screen.PrepareFrame(new Size(20, 20), 0);

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
            Opening = () =>
                states.Add(ReferenceEquals(root.Screen, screen) ? "opening-associated" : "opening-unassociated"),
            Opened = () =>
                states.Add(ReferenceEquals(root.Screen, screen) ? "opened-associated" : "opened-unassociated"),
            Closing = () =>
                states.Add(ReferenceEquals(root.Screen, screen) ? "closing-associated" : "closing-unassociated"),
            Closed = () =>
                states.Add(ReferenceEquals(root.Screen, screen) ? "closed-associated" : "closed-unassociated")
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
        screen.PrepareFrame(new Size(20, 20), 0);
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
        screen.PrepareFrame(new Size(20, 20), 0);

        Assert.Equal(["first", "second"], calls);
        screen.PrepareFrame(new Size(20, 20), 0);
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
        screen.PrepareFrame(new Size(20, 20), 0);
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

        var actual = Assert.Throws<InvalidOperationException>(() => screen.PrepareFrame(new Size(20, 20), 0));

        Assert.Same(expected, actual);
        Assert.Equal(["failing"], calls);
        screen.PrepareFrame(new Size(20, 20), 0);
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
        screen.PrepareFrame(new Size(20, 20), 0);

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
        screen.PrepareFrame(new Size(20, 20), 0);
        var arrangeCalls = root.ArrangeCalls;
        screen.Post(() =>
        {
            screen.Close();
            reopenError = Record.Exception(screen.Open);
        });

        screen.PrepareFrame(new Size(20, 20), 0);

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

        screen.PrepareFrame(new Size(20, 20), 0);
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
        screen.PrepareFrame(new Size(20, 20), 0);
        var arrangeCalls = root.ArrangeCalls;
        screen.Post(() =>
        {
            screen.Close();
            throw expected;
        });

        var actual = Assert.Throws<InvalidOperationException>(() => screen.PrepareFrame(new Size(20, 20), 0));

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
        screen.PrepareFrame(new Size(20, 20), 0);
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
        screen.PrepareFrame(new Size(20, 20), 0);
        Assert.Equal(0, postedCalls);
        screen.Close();
    }

    [Fact]
    public void UpdateMeasuresAndArrangesZeroOriginViewport()
    {
        var root = new LayoutNode();
        var screen = new UiScreen(root);
        screen.Open();

        screen.PrepareFrame(new Size(100, 80), 0);

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
            updateError = Record.Exception(() => screen.PrepareFrame(new Size(20, 20), 0));
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
        internal Action? FixedAction { get; set; }
        internal Action<double>? FrameAction { get; set; }

        protected override void OnOpening() => Opening?.Invoke();
        protected override void OnOpened() => Opened?.Invoke();
        protected override void OnClosing() => Closing?.Invoke();
        protected override void OnClosed() => Closed?.Invoke();
        protected override void OnFixedUpdate() => FixedAction?.Invoke();
        protected override void OnFrameUpdate(double alpha) => FrameAction?.Invoke(alpha);
    }

    [Fact]
    public void StyleSheetsDefaultToEngineBaseAndRejectNull()
    {
        var screen = new UiScreen(new ProbeNode());
        Assert.Single(screen.BaseStyleSheets);
        Assert.Empty(screen.StyleSheets);
        Assert.Same(UiStyleResolver.Default, screen.StyleResolver);
        Assert.Throws<ArgumentNullException>(() => screen.SetStyleSheets(null!));
        Assert.Throws<ArgumentNullException>(() => screen.SetBaseStyleSheets(null!));
    }

    [Fact]
    public void AssigningSameStyleSheetSequenceIsNoOp()
    {
        var node = new ProbeNode();
        var screen = new UiScreen(node);
        var sheets = SourceStyleSheets;
        var notifications = 0;
        node.PropertyChanged += (_, e) =>
            notifications += ReferenceEquals(e.Property, ProbeNode.ValueProperty) ? 1 : 0;

        screen.SetStyleSheets(sheets);
        Assert.Equal(1, notifications);
        Assert.Equal(1, node.Value);

        var resolver = screen.StyleResolver;
        screen.SetStyleSheets(sheets);
        Assert.Same(resolver, screen.StyleResolver);
        Assert.Equal(1, notifications);
        Assert.Equal(1, node.Value);
    }

    [Fact]
    public void StyleSheetReplacementCommitsAllSnapshotsBeforeNotifications()
    {
        var first = new ProbeNode { ValueUnderNewStyles = 2 };
        var second = new ProbeNode { ValueUnderNewStyles = 2 };
        var root = new Panel();
        root.Children.Add(first);
        root.Children.Add(second);
        var screen = new UiScreen(root);

        first.PropertyChanged += (_, _) => Assert.Equal(second.ValueUnderNewStyles, second.Value);

        screen.SetStyleSheets(NewStyleSheets);

        Assert.Equal(first.ValueUnderNewStyles, first.Value);
    }

    [Fact]
    public void StyleSheetChangeContinuesNotificationsAndKeepsCommitted()
    {
        var first = new ProbeNode { ValueUnderNewStyles = 2 };
        var second = new ProbeNode { ValueUnderNewStyles = 2 };
        var root = new Panel();
        root.Children.Add(first);
        root.Children.Add(second);
        var screen = new UiScreen(root);

        first.PropertyChanged += (_, e) =>
        {
            if (ReferenceEquals(e.Property, ProbeNode.ValueProperty))
                throw new InvalidOperationException("first-fail");
        };

        var exception = Assert.Throws<InvalidOperationException>(() => screen.SetStyleSheets(NewStyleSheets));
        Assert.Equal("first-fail", exception.Message);
        Assert.Equal(2, second.Value);
        Assert.Equal(2, first.Value);
    }

    [Fact]
    public void StyleSheetChangeAggregatesErrorsAndNotifiesRemainingNodes()
    {
        var first = new ProbeNode();
        var second = new ProbeNode();
        var third = new ProbeNode();
        var root = new Panel();
        root.Children.Add(first);
        root.Children.Add(second);
        root.Children.Add(third);
        var screen = new UiScreen(root);
        var firstError = new InvalidOperationException("first");
        var secondError = new InvalidOperationException("second");
        var thirdNotified = false;
        first.PropertyChanged += (_, e) =>
        {
            if (ReferenceEquals(e.Property, ProbeNode.ValueProperty))
                throw firstError;
        };
        second.PropertyChanged += (_, e) =>
        {
            if (ReferenceEquals(e.Property, ProbeNode.ValueProperty))
                throw secondError;
        };
        third.PropertyChanged += (_, e) =>
            thirdNotified |= ReferenceEquals(e.Property, ProbeNode.ValueProperty);

        var error = Assert.Throws<AggregateException>(() => screen.SetStyleSheets(NewStyleSheets));

        Assert.Equal(2, error.InnerExceptions.Count);
        Assert.Same(firstError, error.InnerExceptions[0]);
        Assert.Same(secondError, error.InnerExceptions[1]);
        Assert.True(thirdNotified);
        Assert.Equal(2, first.Value);
        Assert.Equal(2, second.Value);
        Assert.Equal(2, third.Value);
    }

    [Fact]
    public void StyleSheetApplicationRejectsReentrantReplacementOrStyleInput()
    {
        var first = new ProbeNode { ValueUnderNewStyles = 2 };
        var second = new ProbeNode { ValueUnderNewStyles = 2 };
        var root = new Panel();
        root.Children.Add(first);
        root.Children.Add(second);
        var screen = new UiScreen(root);

        Exception? sheetError = null;
        Exception? styleError = null;
        first.PropertyChanged += (_, e) =>
        {
            if (ReferenceEquals(e.Property, ProbeNode.ValueProperty))
            {
                sheetError = Record.Exception(() => screen.SetStyleSheets(NewStyleSheets));
                styleError = Record.Exception(() => second.StyleId = "x");
            }
        };

        screen.SetStyleSheets(NewStyleSheets);

        Assert.IsType<InvalidOperationException>(sheetError);
        Assert.IsType<InvalidOperationException>(styleError);
    }

    [Fact]
    public void OpenScreenStyleSheetsRejectWrongThreadButAllowCurrentSnapshot()
    {
        var sheets = SourceStyleSheets;
        var replacement = TargetStyleSheets;
        var screen = new UiScreen(new ProbeNode());
        screen.SetStyleSheets(sheets);
        var snapshot = screen.StyleSheets;
        screen.Open();
        Exception? changeError = null;
        Exception? sameValueError = null;
        var thread = new Thread(() =>
        {
            changeError = Record.Exception(() => screen.SetStyleSheets(replacement));
            sameValueError = Record.Exception(() => screen.SetStyleSheets(snapshot));
        });

        thread.Start();
        thread.Join();

        Assert.IsType<InvalidOperationException>(changeError);
        Assert.Null(sameValueError);
        Assert.Same(snapshot, screen.StyleSheets);
        screen.Close();
    }

    [Fact]
    public void StyleSheetsCannotChangeDuringLayout()
    {
        UiScreen screen = null!;
        Exception? error = null;
        var root = new LayoutNode
        {
            MeasureAction = () => error = Record.Exception(() => screen.SetStyleSheets(SourceStyleSheets))
        };
        screen = new UiScreen(root);
        screen.Open();

        screen.PrepareFrame(new Size(20, 20), 0);

        Assert.IsType<InvalidOperationException>(error);
        Assert.Same(UiStyleResolver.Default, screen.StyleResolver);
        screen.Close();
    }

    [Fact]
    public void StyleSheetsCannotChangeDuringDrawing()
    {
        UiScreen screen = null!;
        Exception? error = null;
        var root = new DrawingNode
        {
            DrawAction = () => error = Record.Exception(() => screen.SetStyleSheets(SourceStyleSheets))
        };
        screen = new UiScreen(root);
        screen.Open();
        screen.PrepareFrame(new Size(20, 20), 0);

        _ = screen.CreateDrawCommandList();

        Assert.IsType<InvalidOperationException>(error);
        Assert.Same(UiStyleResolver.Default, screen.StyleResolver);
        screen.Close();
    }

    [Fact]
    public void ClosedScreenCanReplaceStyleSheetsAndRefreshRetainedRoot()
    {
        var node = new ProbeNode();
        var screen = new UiScreen(node);
        screen.Open();
        screen.Close();

        screen.SetStyleSheets(TargetStyleSheets);

        Assert.Same(screen, node.Screen);
        Assert.Equal(2, node.Value);
    }

    [Fact]
    public void StylePrecomputeFailureKeepsPreviousResolverAndSnapshot()
    {
        var node = new ThrowingStyleNode();
        var screen = new UiScreen(node);
        var sheets = new[] { new UiStyleSheet([
            new UiStyleRule(
                UiStyleSelector.For<UiNode>(states: UiPseudoStates.Disabled),
                [UiStyleSetter.Create(UiNode.OpacityProperty, 0.5)])
        ]) };
        node.ThrowOnStyleStateRead = true;

        Assert.Throws<InvalidOperationException>(() => screen.SetStyleSheets(sheets));

        Assert.Same(UiStyleResolver.Default, screen.StyleResolver);
        Assert.Equal(1, node.Opacity);
    }

    [Fact]
    public void StyleSheetChangeDoesNotNotifyLocallyMaskedProperty()
    {
        var node = new ProbeNode();
        var screen = new UiScreen(node);
        screen.SetStyleSheets(SourceStyleSheets);
        node.Value = 5;
        Assert.Equal(5, node.Value);

        var notifications = 0;
        node.PropertyChanged += (_, e) =>
            notifications += ReferenceEquals(e.Property, ProbeNode.ValueProperty) ? 1 : 0;

        screen.SetStyleSheets(TargetStyleSheets);

        Assert.Equal(5, node.Value);
        Assert.Equal(0, notifications);
    }

    [Fact]
    public void CrossScreenMigrationReadsTargetStyleSheets()
    {
        var node = new ProbeNode();
        var source = new UiScreen(node);
        source.SetStyleSheets(SourceStyleSheets);
        var target = new UiScreen();
        target.SetStyleSheets(TargetStyleSheets);

        Assert.Equal(1, node.Value);
        Assert.Same(source, node.Screen);

        target.Root = node;

        Assert.Same(target, node.Screen);
        Assert.Equal(2, node.Value);
    }

    [Fact]
    public void ParentCrossScreenMigrationReadsTargetStyleSheets()
    {
        var node = new ProbeNode();
        var sourceRoot = new Panel();
        sourceRoot.Children.Add(node);
        var source = new UiScreen(sourceRoot);
        source.SetStyleSheets(SourceStyleSheets);
        var targetRoot = new Panel();
        var target = new UiScreen(targetRoot);
        target.SetStyleSheets(TargetStyleSheets);

        targetRoot.Children.Add(node);

        Assert.Same(source, sourceRoot.Screen);
        Assert.Same(target, node.Screen);
        Assert.Equal(2, node.Value);
    }

    [Fact]
    public void FailedCrossScreenStyleRefreshDoesNotKeepSourceSnapshot()
    {
        var node = new FailOnceStyleNode { Focusable = true };
        var sourceSheets = new[] { new UiStyleSheet([
            new UiStyleRule(
                UiStyleSelector.For<FailOnceStyleNode>(),
                [UiStyleSetter.Create(FailOnceStyleNode.ValueProperty, 1)])
        ]) };
        var targetSheets = new[] { new UiStyleSheet([
            new UiStyleRule(
                UiStyleSelector.For<FailOnceStyleNode>(),
                [UiStyleSetter.Create(FailOnceStyleNode.ValueProperty, 2)]),
            new UiStyleRule(
                UiStyleSelector.For<FailOnceStyleNode>(states: UiPseudoStates.Disabled),
                [UiStyleSetter.Create(UiNode.OpacityProperty, 0.5)])
        ]) };
        var source = new UiScreen(node);
        source.SetStyleSheets(sourceSheets);
        var target = new UiScreen();
        target.SetStyleSheets(targetSheets);
        source.Open();
        target.Open();
        source.PrepareFrame(new Size(20, 20), 0);
        Assert.True(node.Focus());
        Assert.Equal(1, node.Value);
        node.FailNextStyleStateRead = true;

        Assert.Throws<InvalidOperationException>(() => target.Root = node);

        Assert.Null(source.Root);
        Assert.Same(target, node.Screen);
        Assert.False(node.IsFocused);
        Assert.Equal(2, node.Value);
        target.Close();
        source.Close();
    }

    private sealed class ProbeNode : UiNode
    {
        public static readonly UiProperty<int> ValueProperty =
            UiProperty.Register<ProbeNode, int>(nameof(Value));

        public int Value
        {
            get => GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public int ValueUnderNewStyles { get; set; }
    }

    private static UiStyleSheet[] NewStyleSheets => [new UiStyleSheet([
        new UiStyleRule(
            UiStyleSelector.For<ProbeNode>(),
            [UiStyleSetter.Create(ProbeNode.ValueProperty, 2)])
    ])];

    private static UiStyleSheet[] SourceStyleSheets => [new UiStyleSheet([
        new UiStyleRule(
            UiStyleSelector.For<ProbeNode>(),
            [UiStyleSetter.Create(ProbeNode.ValueProperty, 1)])
    ])];

    private static UiStyleSheet[] TargetStyleSheets => [new UiStyleSheet([
        new UiStyleRule(
            UiStyleSelector.For<ProbeNode>(),
            [UiStyleSetter.Create(ProbeNode.ValueProperty, 2)])
    ])];

    private sealed class LayoutNode : UiNode
    {
        internal Action? MeasureAction { get; set; }
        internal Action? ArrangeAction { get; set; }
        internal Size LastMeasureConstraint { get; private set; }
        internal Size LastArrangeSize { get; private set; }
        internal int MeasureCalls { get; private set; }
        internal int ArrangeCalls { get; private set; }

        protected override Size MeasureCore(Size availableSize)
        {
            MeasureCalls++;
            MeasureAction?.Invoke();
            LastMeasureConstraint = availableSize;
            return availableSize;
        }

        protected override void ArrangeCore(Size finalSize)
        {
            ArrangeAction?.Invoke();
            LastArrangeSize = finalSize;
            ArrangeCalls++;
        }
    }

    private sealed class DrawingNode : UiNode
    {
        internal Action? DrawAction { get; set; }

        protected override void DrawCore(UiDrawingContext context) => DrawAction?.Invoke();
    }

    private sealed class ThrowingStyleNode : UiNode
    {
        internal bool ThrowOnStyleStateRead { get; set; }

        protected override UiPseudoStates GetStylePseudoStates()
        {
            if (ThrowOnStyleStateRead)
                throw new InvalidOperationException("style state failed");
            return base.GetStylePseudoStates();
        }
    }

    private sealed class FailOnceStyleNode : UiNode
    {
        internal static readonly UiProperty<int> ValueProperty =
            UiProperty.Register<FailOnceStyleNode, int>("Value");

        internal bool FailNextStyleStateRead { get; set; }

        internal int Value => GetValue(ValueProperty);

        protected override UiPseudoStates GetStylePseudoStates()
        {
            if (FailNextStyleStateRead)
            {
                FailNextStyleStateRead = false;
                throw new InvalidOperationException("style state failed once");
            }

            return base.GetStylePseudoStates();
        }
    }
}
