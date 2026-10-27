using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Scenes;
using PanguEngine.ComponentModel;
using PanguEngine.Input;
using PanguEngine.UI;
using PanguEngine.UI.Controls;
using PanguEngine.UI.Drawing;
using PanguEngine.UI.Styling;

namespace PanguEngine.Tests.UI;

public sealed class UISceneTests
{
    [Fact]
    public void StandaloneDrivingEntryPointsArePublic()
    {
        var scene = new UIScene();
        Delegate[] entryPoints =
        [
            (Action)scene.Open,
            (Action)scene.Close,
            (Func<bool>)scene.IsOpen,
            (Action<Size>)scene.PrepareFrame,
            (Action<Size, TimeSpan>)scene.PrepareFrame,
            (Func<Point, bool>)scene.ProcessPointerMoved,
            (Func<Point, MouseButton, KeyModifiers, bool>)scene.ProcessPointerPressed,
            (Func<Point, MouseButton, KeyModifiers, bool>)scene.ProcessPointerReleased,
            (Func<Point, double, double, KeyModifiers, bool>)scene.ProcessPointerWheel,
            (Func<Key, KeyModifiers, bool, bool>)scene.ProcessKeyDown,
            (Func<Key, KeyModifiers, bool, bool>)scene.ProcessKeyUp,
            (Action<string>)scene.ProcessTextInput,
            (Action<bool>)scene.ProcessFocusChanged
        ];

        Assert.All(entryPoints, entryPoint =>
            Assert.True(entryPoint.Method.IsPublic, entryPoint.Method.Name));
    }

    [Fact]
    public void PauseMenuEnablesGamePauseAndEscapeClose()
    {
        var scene = new PauseMenu();

        Assert.True(scene.PausesGame);
        Assert.True(scene.CloseOnEscape);
        Assert.Same(BuiltinInputContexts.Ui, scene.InputContext);
    }

    [Fact]
    public void PauseMenuExitButtonUsesDangerClassWithoutLocalColors()
    {
        var scene = new PauseMenu();
        scene.Root!.UpdateStyles();
        var root = Assert.IsType<Panel>(scene.Root);
        var panel = Assert.IsType<StackPanel>(Assert.Single(root.Children));
        var buttons = panel.Children.OfType<Button>().ToArray();

        Assert.Equal(3, buttons.Length);
        Assert.DoesNotContain("danger", (IReadOnlySet<string>)buttons.Single(button => button.Text == "回到游戏").Classes);
        var exit = buttons.Single(button => button.Text == "退出游戏");
        Assert.Contains("danger", (IReadOnlySet<string>)exit.Classes);
        Assert.Equal(new SolidColorBrush(104, 43, 45), exit.Background);
        Assert.Equal(new SolidColorBrush(157, 73, 77), exit.BorderBrush);
        Assert.Equal("Button.danger", Assert.Single(exit.GetStyleValueSources(Region.BackgroundProperty)).SelectorText);
        Assert.False(Assert.Single(exit.GetStyleValueSources(Region.BackgroundProperty)).IsMaskedByLocalValue);
        Assert.False(Assert.Single(exit.GetStyleValueSources(Region.BorderBrushProperty)).IsMaskedByLocalValue);
    }

    [Fact]
    public void PauseMenuShowsUiShowcaseEntryBetweenResumeAndExit()
    {
        var scene = new PauseMenu();
        var root = Assert.IsType<Panel>(scene.Root);
        var panel = Assert.IsType<StackPanel>(Assert.Single(root.Children));
        var buttons = panel.Children.OfType<Button>().ToArray();

        Assert.Equal(
            new[] { "回到游戏", "UI 展示", "退出游戏" },
            buttons.Select(button => button.Text).ToArray());
        var showcase = buttons.Single(button => button.Text == "UI 展示");
        Assert.DoesNotContain("danger", (IReadOnlySet<string>)showcase.Classes);
    }

    [Fact]
    public void ScaleDefaultsToOneAndRejectsInvalidValuesWithoutChangingState()
    {
        var scene = new UIScene();
        Assert.Equal(1, scene.Scale);
        Assert.True(scene.UseLayoutRounding);

        scene.Scale = 1.5;

        Assert.Equal(1.5, scene.Scale);
        Assert.Throws<ArgumentOutOfRangeException>(() => scene.Scale = double.NaN);
        Assert.Throws<ArgumentOutOfRangeException>(() => scene.Scale = double.PositiveInfinity);
        Assert.Throws<ArgumentOutOfRangeException>(() => scene.Scale = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => scene.Scale = -1);
        Assert.Equal(1.5, scene.Scale);
    }

    [Fact]
    public void OpenSceneScaleRejectsWrongThread()
    {
        var scene = new UIScene();
        scene.Open();
        Exception? error = null;
        var thread = new Thread(() =>
            error = Record.Exception(() => scene.Scale = 2));

        thread.Start();
        thread.Join();

        Assert.IsType<InvalidOperationException>(error);
        Assert.Equal(1, scene.Scale);
        scene.Close();
    }

    [Fact]
    public void OpenSceneLayoutRoundingRejectsWrongThreadButAllowsSameValue()
    {
        var scene = new UIScene();
        scene.Open();
        Exception? changeError = null;
        Exception? sameValueError = null;
        var thread = new Thread(() =>
        {
            changeError = Record.Exception(() => scene.UseLayoutRounding = false);
            sameValueError = Record.Exception(() => scene.UseLayoutRounding = true);
        });

        thread.Start();
        thread.Join();

        Assert.IsType<InvalidOperationException>(changeError);
        Assert.Null(sameValueError);
        Assert.True(scene.UseLayoutRounding);
        scene.Close();
    }

    [Fact]
    public void UISceneScaleCannotChangeDuringLayout()
    {
        UIScene scene = null!;
        Exception? error = null;
        var root = new LayoutNode
        {
            MeasureAction = () => error = Record.Exception(() => scene.Scale = 2)
        };
        scene = new UIScene(root);
        scene.Open();

        scene.PrepareFrame(new Size(100, 80));

        Assert.IsType<InvalidOperationException>(error);
        Assert.Equal(1, scene.Scale);
        scene.Close();
    }

    [Fact]
    public void UISceneLayoutRoundingCannotChangeDuringLayout()
    {
        UIScene scene = null!;
        Exception? error = null;
        var root = new LayoutNode
        {
            MeasureAction = () =>
                error = Record.Exception(() => scene.UseLayoutRounding = false)
        };
        scene = new UIScene(root);
        scene.Open();

        scene.PrepareFrame(new Size(100, 80));

        Assert.IsType<InvalidOperationException>(error);
        Assert.True(scene.UseLayoutRounding);
        scene.Close();
    }

    [Fact]
    public void ManualMeasureCallbackCanChangeScaleButCannotCommitInvalidatedPass()
    {
        UIScene scene = null!;
        Exception? error = null;
        var root = new LayoutNode
        {
            MeasureAction = () => error = Record.Exception(() => scene.Scale = 2)
        };
        scene = new UIScene(root);

        root.Measure(new Size(100, 80));

        Assert.Null(error);
        Assert.Equal(2, scene.Scale);
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
        var scene = new UIScene(root);
        scene.Open();
        scene.PrepareFrame(new Size(100, 80));
        Assert.True(root.IsMeasureValid);
        Assert.True(child.IsMeasureValid);
        Assert.True(grandchild.IsMeasureValid);

        scene.Scale = 2;

        Assert.False(root.IsMeasureValid);
        Assert.False(root.IsArrangeValid);
        Assert.False(child.IsMeasureValid);
        Assert.False(child.IsArrangeValid);
        Assert.False(grandchild.IsMeasureValid);
        Assert.False(grandchild.IsArrangeValid);
        scene.Close();
    }

    [Fact]
    public void LayoutRoundingChangeImmediatelyInvalidatesEntireRootSubtree()
    {
        var root = new Canvas();
        var child = new Canvas();
        var grandchild = new LayoutNode { Width = 1, Height = 1 };
        child.Children.Add(grandchild);
        root.Children.Add(child);
        var scene = new UIScene(root);
        scene.Open();
        scene.PrepareFrame(new Size(100, 80));

        scene.UseLayoutRounding = false;

        Assert.False(root.IsMeasureValid);
        Assert.False(root.IsArrangeValid);
        Assert.False(child.IsMeasureValid);
        Assert.False(child.IsArrangeValid);
        Assert.False(grandchild.IsMeasureValid);
        Assert.False(grandchild.IsArrangeValid);
        scene.Close();
    }

    [Fact]
    public void ExtremelySmallScaleRejectsNonFiniteLogicalViewportBeforeLayout()
    {
        var root = new LayoutNode();
        var scene = new UIScene(root) { Scale = double.Epsilon };
        scene.Open();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            scene.PrepareFrame(new Size(100, 80)));

        Assert.Equal(0, root.MeasureCalls);
        Assert.Equal(0, root.ArrangeCalls);
        scene.Close();
    }

    [Fact]
    public void AssigningSameScaleDoesNotInvalidateLayout()
    {
        var root = new LayoutNode();
        var scene = new UIScene(root);
        scene.Open();
        scene.PrepareFrame(new Size(100, 80));

        scene.Scale = 1;

        Assert.True(root.IsMeasureValid);
        Assert.True(root.IsArrangeValid);
        scene.Close();
    }

    [Fact]
    public void UISceneSupportsOptionalRootAndAssociatesItWhileClosed()
    {
        var root = new LayoutNode();
        var scene = new UIScene(root);
        var emptyScene = new UIScene();

        Assert.Same(root, scene.Root);
        Assert.Same(scene, root.Scene);
        Assert.Null(emptyScene.Root);
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
        var scene = new UIScene(oldRoot);

        scene.Root = newRoot;

        Assert.Null(oldRoot.Scene);
        Assert.Null(oldChild.Scene);
        Assert.Same(scene, newRoot.Scene);
        Assert.Same(scene, newChild.Scene);

        scene.Root = null;

        Assert.Null(scene.Root);
        Assert.Null(newRoot.Scene);
        Assert.Null(newChild.Scene);
    }

    [Fact]
    public void RootTransferWhileClosedInvalidatesExistingLayoutCacheOnReopen()
    {
        var manager = new UIManager();
        var incoming = new LayoutNode { Width = 50, Height = 50 };
        var source = new GameUIScene(incoming);
        var target = new GameUIScene(new LayoutNode());
        manager.Open(source);
        manager.UpdateFrame(new Size(100, 100), 0);
        manager.Close();
        Assert.True(incoming.IsMeasureValid);
        Assert.True(incoming.IsArrangeValid);
        Assert.Equal(1, incoming.ArrangeCalls);

        target.Root = incoming;

        Assert.Null(source.Root);
        Assert.False(incoming.IsMeasureValid);
        Assert.False(incoming.IsArrangeValid);

        manager.Open(target);
        manager.UpdateFrame(new Size(100, 100), 0);

        Assert.True(incoming.IsMeasureValid);
        Assert.True(incoming.IsArrangeValid);
        Assert.Equal(new Rect(25, 25, 50, 50), incoming.LayoutBounds);
        Assert.Equal(2, incoming.ArrangeCalls);
        manager.Close();
    }

    [Fact]
    public void RootTransferAcrossScenesWithEqualLayoutSettingsInvalidatesEntireSubtree()
    {
        var incoming = new Canvas();
        var child = new Canvas();
        var grandchild = new LayoutNode { Width = 1, Height = 1 };
        child.Children.Add(grandchild);
        incoming.Children.Add(child);
        var source = new UIScene(incoming);
        var target = new UIScene();
        source.Open();
        target.Open();
        source.PrepareFrame(new Size(100, 100));
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
    public void ClosedSceneTreeCanBeModifiedOnAnotherThread()
    {
        var root = new Canvas();
        var scene = new UIScene(root);
        scene.Open();
        scene.Close();
        Exception? error = null;
        var thread = new Thread(() =>
            error = Record.Exception(() => root.Children.Add(new LayoutNode())));

        thread.Start();
        thread.Join();

        Assert.Null(error);
        Assert.Same(scene, root.Children[0].Scene);
    }

    [Fact]
    public void RootMovesFromParentToScene()
    {
        var sourceRoot = new Canvas();
        var incoming = new Canvas();
        var leaf = new LayoutNode();
        incoming.Children.Add(leaf);
        sourceRoot.Children.Add(incoming);
        var source = new UIScene(sourceRoot);
        var target = new UIScene();

        target.Root = incoming;

        Assert.Empty(sourceRoot.Children);
        Assert.Null(incoming.Parent);
        Assert.Same(source, sourceRoot.Scene);
        Assert.Same(target, incoming.Scene);
        Assert.Same(target, leaf.Scene);
    }

    [Fact]
    public void RootMovesBetweenScenesAndClearsSourceRoot()
    {
        var incoming = new Canvas();
        var leaf = new LayoutNode();
        incoming.Children.Add(leaf);
        var source = new UIScene(incoming);
        var target = new UIScene();

        target.Root = incoming;

        Assert.Null(source.Root);
        Assert.Same(incoming, target.Root);
        Assert.Same(target, incoming.Scene);
        Assert.Same(target, leaf.Scene);
    }

    [Fact]
    public void RootMovesBetweenOpenScenesOnTheirOwnerThread()
    {
        var incoming = new LayoutNode();
        var oldTargetRoot = new LayoutNode();
        var source = new UIScene(incoming);
        var target = new UIScene(oldTargetRoot);
        source.Open();
        target.Open();

        target.Root = incoming;

        Assert.Null(source.Root);
        Assert.Null(oldTargetRoot.Scene);
        Assert.Same(incoming, target.Root);
        Assert.Same(target, incoming.Scene);
        target.Close();
        source.Close();
    }

    [Fact]
    public void DescendantCanBecomeRootWithoutLeavingTheSameScene()
    {
        var oldRoot = new Canvas();
        var incoming = new Canvas();
        var leaf = new LayoutNode();
        incoming.Children.Add(leaf);
        oldRoot.Children.Add(incoming);
        var scene = new UIScene(oldRoot);

        scene.Root = incoming;

        Assert.Null(incoming.Parent);
        Assert.Null(oldRoot.Scene);
        Assert.Same(scene, incoming.Scene);
        Assert.Same(scene, leaf.Scene);
    }

    [Fact]
    public void RootTransferAcrossDifferentOwnerThreadsFailsBeforeMutation()
    {
        var ready = new ManualResetEventSlim();
        var release = new ManualResetEventSlim();
        UIScene source = null!;
        Canvas incoming = null!;
        var thread = new Thread(() =>
        {
            incoming = new Canvas();
            source = new UIScene(incoming);
            source.Open();
            ready.Set();
            release.Wait();
            source.Close();
        });
        thread.Start();
        ready.Wait();
        var oldRoot = new Canvas();
        var target = new UIScene(oldRoot);
        target.Open();
        try
        {
            var error = Record.Exception(() => target.Root = incoming);

            Assert.IsType<InvalidOperationException>(error);
            Assert.Same(incoming, source.Root);
            Assert.Same(source, incoming.Scene);
            Assert.Same(oldRoot, target.Root);
            Assert.Same(target, oldRoot.Scene);
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
        var scene = new UIScene(oldRoot);
        Exception? error = null;
        oldRoot.MeasureAction = () =>
            error = Record.Exception(() => scene.Root = replacement);
        scene.Open();

        scene.PrepareFrame(new Size(20, 20));

        Assert.IsType<InvalidOperationException>(error);
        Assert.Same(oldRoot, scene.Root);
        Assert.Same(scene, oldRoot.Scene);
        Assert.Null(replacement.Scene);
        scene.Close();
    }

    [Fact]
    public void AssigningCurrentRootIsANoOpBeforeThreadValidation()
    {
        var root = new LayoutNode();
        var scene = new UIScene(root);
        scene.Open();
        Exception? error = null;
        var thread = new Thread(() =>
            error = Record.Exception(() => scene.Root = root));

        thread.Start();
        thread.Join();

        Assert.Null(error);
        Assert.Same(root, scene.Root);
        Assert.Same(scene, root.Scene);
        scene.Close();
    }

    [Fact]
    public void OpenAndCloseExposeExpectedRootStatesToCallbacks()
    {
        var root = new LayoutNode();
        var states = new List<string>();
        RecordingScene scene = null!;
        scene = new RecordingScene(root)
        {
            Opening = () =>
                states.Add(ReferenceEquals(root.Scene, scene) ? "opening-associated" : "opening-unassociated"),
            Opened = () =>
                states.Add(ReferenceEquals(root.Scene, scene) ? "opened-associated" : "opened-unassociated"),
            Closing = () =>
                states.Add(ReferenceEquals(root.Scene, scene) ? "closing-associated" : "closing-unassociated"),
            Closed = () =>
                states.Add(ReferenceEquals(root.Scene, scene) ? "closed-associated" : "closed-unassociated")
        };

        scene.Open();
        scene.Close();

        Assert.Equal(
            ["opening-associated", "opened-associated", "closing-associated", "closed-associated"],
            states);
        Assert.Same(scene, root.Scene);
    }

    [Fact]
    public void RootCanChangeInEveryLifecycleCallback()
    {
        var openingRoot = new LayoutNode();
        var openedRoot = new LayoutNode();
        var closingRoot = new LayoutNode();
        var closedRoot = new LayoutNode();
        RecordingScene scene = null!;
        scene = new RecordingScene
        {
            Opening = () => scene.Root = openingRoot,
            Opened = () => scene.Root = openedRoot,
            Closing = () => scene.Root = closingRoot,
            Closed = () => scene.Root = closedRoot
        };

        scene.Open();
        scene.Close();

        Assert.Null(openingRoot.Scene);
        Assert.Null(openedRoot.Scene);
        Assert.Null(closingRoot.Scene);
        Assert.Same(closedRoot, scene.Root);
        Assert.Same(scene, closedRoot.Scene);
    }

    [Fact]
    public void NeverOpenedAndClosedSceneCloseOperationsAreNoOps()
    {
        var scene = new UIScene(new LayoutNode());

        scene.Close();
        scene.Open();
        scene.Close();
        scene.Close();

        Assert.Same(scene, scene.Root!.Scene);
    }

    [Fact]
    public void ClosedSceneCanOpenOnAnotherThread()
    {
        var scene = new UIScene(new LayoutNode());
        scene.Open();
        scene.Close();
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                scene.Open();
                scene.Close();
            }
            catch (Exception exception)
            {
                error = exception;
            }
        });

        thread.Start();
        thread.Join();

        Assert.Null(error);
        Assert.Same(scene, scene.Root!.Scene);
    }

    [Fact]
    public void OpeningAnAlreadyOpenSceneFailsWithoutChangingState()
    {
        var scene = new UIScene(new LayoutNode());
        scene.Open();

        Assert.Throws<InvalidOperationException>(scene.Open);
        Assert.Same(scene, scene.Root!.Scene);

        scene.Close();
    }

    [Fact]
    public void PostRequiresAnOpenScene()
    {
        var scene = new UIScene(new LayoutNode());

        Assert.Throws<InvalidOperationException>(() => scene.Post(() => { }));
        scene.Open();
        scene.Close();
        Assert.Throws<InvalidOperationException>(() => scene.Post(() => { }));
    }

    [Fact]
    public void OpeningAndOpenedCanPostUntilUpdate()
    {
        var calls = 0;
        RecordingScene scene = null!;
        scene = new RecordingScene(new LayoutNode())
        {
            Opening = () => scene.Post(() => calls++),
            Opened = () => scene.Post(() => calls++)
        };

        scene.Open();

        Assert.Equal(0, calls);
        scene.PrepareFrame(new Size(20, 20));
        Assert.Equal(2, calls);
        scene.Close();
    }

    [Fact]
    public void PostPreservesFifoAndDefersActionsPostedDuringUpdate()
    {
        var calls = new List<string>();
        var scene = new RecordingScene(new LayoutNode());
        scene.Opened = () => scene.Post(() =>
        {
            calls.Add("first");
            scene.Post(() => calls.Add("deferred"));
        });

        scene.Open();
        scene.Post(() => calls.Add("second"));
        scene.PrepareFrame(new Size(20, 20));

        Assert.Equal(["first", "second"], calls);
        scene.PrepareFrame(new Size(20, 20));
        Assert.Equal(["first", "second", "deferred"], calls);
        scene.Close();
    }

    [Fact]
    public void PostCanBeCalledFromBackgroundThread()
    {
        var scene = new UIScene(new LayoutNode());
        var calls = 0;
        scene.Open();

        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                scene.Post(() => calls++);
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
        scene.PrepareFrame(new Size(20, 20));
        Assert.Equal(1, calls);
        scene.Close();
    }

    [Fact]
    public void ActionFailureStopsTheBatchAndPreservesRemainingActions()
    {
        var expected = new InvalidOperationException("action failed");
        var calls = new List<string>();
        var scene = new UIScene(new LayoutNode());
        scene.Open();
        scene.Post(() =>
        {
            calls.Add("failing");
            throw expected;
        });
        scene.Post(() => calls.Add("remaining"));

        var actual = Assert.Throws<InvalidOperationException>(() => scene.PrepareFrame(new Size(20, 20)));

        Assert.Same(expected, actual);
        Assert.Equal(["failing"], calls);
        scene.PrepareFrame(new Size(20, 20));
        Assert.Equal(["failing", "remaining"], calls);
        scene.Close();
    }

    [Fact]
    public void CloseRejectsNewPostsAndClearsPendingActions()
    {
        var calls = 0;
        Exception? postError = null;
        RecordingScene scene = null!;
        scene = new RecordingScene(new LayoutNode())
        {
            Closing = () => postError = Record.Exception(() => scene.Post(() => calls++))
        };
        scene.Open();
        scene.Post(() => calls++);

        scene.Close();
        scene.Open();
        scene.PrepareFrame(new Size(20, 20));

        Assert.Equal(0, calls);
        Assert.IsType<InvalidOperationException>(postError);
        scene.Close();
    }

    [Fact]
    public void CloseInsidePostRejectsSameSceneReopenUntilUpdateReturns()
    {
        var root = new LayoutNode();
        var scene = new UIScene(root);
        Exception? reopenError = null;
        scene.Open();
        scene.PrepareFrame(new Size(20, 20));
        var arrangeCalls = root.ArrangeCalls;
        scene.Post(() =>
        {
            scene.Close();
            reopenError = Record.Exception(scene.Open);
        });

        scene.PrepareFrame(new Size(20, 20));

        Assert.IsType<InvalidOperationException>(reopenError);
        Assert.Equal(arrangeCalls, root.ArrangeCalls);
        Assert.Same(scene, root.Scene);
        scene.Open();
        scene.Close();
    }

    [Fact]
    public void CloseInsidePostRejectsBackgroundReopenUntilUpdateReturns()
    {
        var scene = new UIScene(new LayoutNode());
        var closed = new ManualResetEventSlim();
        var attempted = new ManualResetEventSlim();
        Exception? reopenError = null;
        scene.Open();
        scene.Post(() =>
        {
            scene.Close();
            closed.Set();
            attempted.Wait();
        });
        var thread = new Thread(() =>
        {
            closed.Wait();
            reopenError = Record.Exception(scene.Open);
            attempted.Set();
        });
        thread.Start();

        scene.PrepareFrame(new Size(20, 20));
        thread.Join();

        Assert.IsType<InvalidOperationException>(reopenError);
        Exception? afterUpdateError = null;
        var afterUpdateThread = new Thread(() =>
        {
            afterUpdateError = Record.Exception(() =>
            {
                scene.Open();
                scene.Close();
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
        var scene = new UIScene(root);
        scene.Open();
        scene.PrepareFrame(new Size(20, 20));
        var arrangeCalls = root.ArrangeCalls;
        scene.Post(() =>
        {
            scene.Close();
            throw expected;
        });

        var actual = Assert.Throws<InvalidOperationException>(() => scene.PrepareFrame(new Size(20, 20)));

        Assert.Same(expected, actual);
        Assert.Equal(arrangeCalls, root.ArrangeCalls);
        Assert.Same(scene, root.Scene);
    }

    [Fact]
    public void OpeningFailurePreservesStateAndStopsLifecycleCallbacks()
    {
        var postedCalls = 0;
        var callbacks = new List<string>();
        var expected = new InvalidOperationException("opening failed");
        RecordingScene scene = null!;
        scene = new RecordingScene(new LayoutNode())
        {
            Opening = () =>
            {
                scene.Post(() => postedCalls++);
                throw expected;
            },
            Opened = () => callbacks.Add("opened"),
            Closing = () => callbacks.Add("closing"),
            Closed = () => callbacks.Add("closed")
        };

        var actual = Assert.Throws<InvalidOperationException>(scene.Open);

        Assert.Same(expected, actual);
        Assert.Same(scene, scene.Root!.Scene);
        Assert.True(scene.IsOpen());
        Assert.Empty(callbacks);
        Assert.Throws<InvalidOperationException>(scene.Open);
        scene.PrepareFrame(new Size(20, 20));
        Assert.Equal(1, postedCalls);
        scene.Close();
    }

    [Fact]
    public void OpenedFailurePreservesStateWithoutAutomaticClose()
    {
        var postedCalls = 0;
        var callbacks = new List<string>();
        var expected = new InvalidOperationException("opened failed");
        RecordingScene scene = null!;
        scene = new RecordingScene(new LayoutNode())
        {
            Opened = () =>
            {
                scene.Post(() => postedCalls++);
                throw expected;
            },
            Closing = () => callbacks.Add("closing"),
            Closed = () => callbacks.Add("closed")
        };

        var actual = Assert.Throws<InvalidOperationException>(scene.Open);

        Assert.Same(expected, actual);
        Assert.Same(scene, scene.Root!.Scene);
        Assert.True(scene.IsOpen());
        Assert.Empty(callbacks);
        Assert.Throws<InvalidOperationException>(scene.Open);
        scene.PrepareFrame(new Size(20, 20));
        Assert.Equal(1, postedCalls);
        scene.Close();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CloseFailurePreservesOriginalExceptionAndStopsLifecycle(bool failOnClosed)
    {
        var expected = new InvalidOperationException("close failed");
        var callbacks = new List<string>();
        var scene = new RecordingScene(new LayoutNode())
        {
            Closing = () =>
            {
                callbacks.Add("closing");
                if (!failOnClosed)
                    throw expected;
            },
            Closed = () =>
            {
                callbacks.Add("closed");
                throw expected;
            }
        };
        scene.Open();

        var actual = Assert.Throws<InvalidOperationException>(scene.Close);

        Assert.Same(expected, actual);
        string[] expectedCallbacks = failOnClosed ? ["closing", "closed"] : ["closing"];
        Assert.Equal(expectedCallbacks, callbacks);
        Assert.True(scene.IsOpen());
        Assert.Throws<InvalidOperationException>(() => scene.Post(() => { }));
    }

    [Fact]
    public void UpdateMeasuresAndArrangesZeroOriginViewport()
    {
        var root = new LayoutNode();
        var scene = new UIScene(root);
        scene.Open();

        scene.PrepareFrame(new Size(100, 80));

        Assert.Equal(new Size(100, 80), root.LastMeasureConstraint);
        Assert.Equal(new Size(100, 80), root.LastArrangeSize);
        Assert.Equal(new Rect(0, 0, 100, 80), root.LayoutBounds);
        scene.Close();
    }

    [Fact]
    public void OpenSceneOperationsRejectWrongThread()
    {
        var scene = new UIScene(new LayoutNode());
        scene.Open();
        Exception? updateError = null;
        Exception? closeError = null;
        var thread = new Thread(() =>
        {
            updateError = Record.Exception(() => scene.PrepareFrame(new Size(20, 20)));
            closeError = Record.Exception(scene.Close);
        });

        thread.Start();
        thread.Join();

        Assert.IsType<InvalidOperationException>(updateError);
        Assert.IsType<InvalidOperationException>(closeError);
        scene.Close();
    }

    private sealed class RecordingScene(UIElement? root = null) : UIScene(root)
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

    [Fact]
    public void StyleSheetsDefaultToEngineBaseAndRejectNull()
    {
        var scene = new UIScene(new ProbeNode());
        Assert.Single(scene.BaseStyleSheets);
        Assert.Empty(scene.StyleSheets);
        Assert.Same(StyleResolver.Default, scene.StyleResolver);
        Assert.Throws<ArgumentNullException>(() => scene.SetStyleSheets(null!));
        Assert.Throws<ArgumentNullException>(() => scene.SetBaseStyleSheets(null!));
    }

    [Fact]
    public void AssigningSameStyleSheetSequenceIsNoOp()
    {
        var node = new ProbeNode();
        var scene = new UIScene(node);
        var sheets = SourceStyleSheets;
        var notifications = 0;
        node.PropertyChanged += (_, e) =>
            notifications += ReferenceEquals(e.Property, ProbeNode.ValueProperty) ? 1 : 0;

        scene.SetStyleSheets(sheets);
        scene.Root!.UpdateStyles();
        Assert.Equal(1, notifications);
        Assert.Equal(1, node.Value);

        var resolver = scene.StyleResolver;
        scene.SetStyleSheets(sheets);
        scene.Root!.UpdateStyles();
        Assert.Same(resolver, scene.StyleResolver);
        Assert.Equal(1, notifications);
        Assert.Equal(1, node.Value);
    }

    [Fact]
    public void StyleSheetReplacementNotifiesEachNodeBeforeUpdatingTheNext()
    {
        var first = new ProbeNode { ValueUnderNewStyles = 2 };
        var second = new ProbeNode { ValueUnderNewStyles = 2 };
        var root = new Panel();
        root.Children.Add(first);
        root.Children.Add(second);
        var scene = new UIScene(root);

        first.PropertyChanged += (_, _) => Assert.Equal(0, second.Value);

        scene.SetStyleSheets(NewStyleSheets);
        scene.Root!.UpdateStyles();

        Assert.Equal(first.ValueUnderNewStyles, first.Value);
        Assert.Equal(second.ValueUnderNewStyles, second.Value);
    }

    [Fact]
    public void StyleSheetChangeFailureKeepsTheCurrentSnapshotAndLaterNodesPending()
    {
        var first = new ProbeNode { ValueUnderNewStyles = 2 };
        var second = new ProbeNode { ValueUnderNewStyles = 2 };
        var root = new Panel();
        root.Children.Add(first);
        root.Children.Add(second);
        var scene = new UIScene(root);

        first.PropertyChanged += (_, e) =>
        {
            if (ReferenceEquals(e.Property, ProbeNode.ValueProperty))
                throw new InvalidOperationException("first-fail");
        };

        scene.SetStyleSheets(NewStyleSheets);
        var exception = Assert.Throws<InvalidOperationException>(scene.Root!.UpdateStyles);
        Assert.Equal("first-fail", exception.Message);
        Assert.Equal(0, second.Value);
        Assert.Equal(2, first.Value);
        Assert.False(second.IsStyleSubtreeValid);
    }

    [Fact]
    public void StyleSheetChangeStopsAtFirstNotificationFailure()
    {
        var first = new ProbeNode();
        var second = new ProbeNode();
        var third = new ProbeNode();
        var root = new Panel();
        root.Children.Add(first);
        root.Children.Add(second);
        root.Children.Add(third);
        var scene = new UIScene(root);
        var firstError = new InvalidOperationException("first");
        var secondError = new InvalidOperationException("second");
        var secondNotified = false;
        var thirdNotified = false;
        first.PropertyChanged += (_, e) =>
        {
            if (ReferenceEquals(e.Property, ProbeNode.ValueProperty))
                throw firstError;
        };
        second.PropertyChanged += (_, e) =>
        {
            if (ReferenceEquals(e.Property, ProbeNode.ValueProperty))
            {
                secondNotified = true;
                throw secondError;
            }
        };
        third.PropertyChanged += (_, e) =>
            thirdNotified |= ReferenceEquals(e.Property, ProbeNode.ValueProperty);

        scene.SetStyleSheets(NewStyleSheets);
        var error = Assert.Throws<InvalidOperationException>(scene.Root!.UpdateStyles);

        Assert.Same(firstError, error);
        Assert.False(secondNotified);
        Assert.False(thirdNotified);
        Assert.Equal(2, first.Value);
        Assert.Equal(0, second.Value);
        Assert.Equal(0, third.Value);
    }

    [Fact]
    public void StyleNotificationPropagatesCallbackAggregateWithoutUnwrapping()
    {
        var node = new ProbeNode();
        var scene = new UIScene(node);
        var expected = new AggregateException(new InvalidOperationException("callback failed"));
        node.PropertyChanged += (_, args) =>
        {
            if (ReferenceEquals(args.Property, ProbeNode.ValueProperty))
                throw expected;
        };

        scene.SetStyleSheets(NewStyleSheets);
        var actual = Assert.Throws<AggregateException>(scene.Root!.UpdateStyles);

        Assert.Same(expected, actual);
        Assert.Equal(2, node.Value);
    }

    [Fact]
    public void StyleSheetApplicationRejectsReplacementButAllowsStyleInput()
    {
        var first = new ProbeNode { ValueUnderNewStyles = 2 };
        var second = new ProbeNode { ValueUnderNewStyles = 2 };
        var root = new Panel();
        root.Children.Add(first);
        root.Children.Add(second);
        var scene = new UIScene(root);

        Exception? sheetError = null;
        Exception? styleError = null;
        first.PropertyChanged += (_, e) =>
        {
            if (ReferenceEquals(e.Property, ProbeNode.ValueProperty))
            {
                sheetError = Record.Exception(() => scene.SetStyleSheets(NewStyleSheets));
                styleError = Record.Exception(() => second.StyleId = "x");
            }
        };

        scene.SetStyleSheets(NewStyleSheets);
        scene.Open();
        try
        {
            scene.PrepareFrame(new Size(100, 100));

            Assert.IsType<InvalidOperationException>(sheetError);
            Assert.Null(styleError);
        }
        finally
        {
            scene.Close();
        }
    }

    [Fact]
    public void OpenSceneStyleSheetsRejectWrongThreadButAllowCurrentSnapshot()
    {
        var sheets = SourceStyleSheets;
        var replacement = TargetStyleSheets;
        var scene = new UIScene(new ProbeNode());
        scene.SetStyleSheets(sheets);
        var snapshot = scene.StyleSheets;
        scene.Open();
        Exception? changeError = null;
        Exception? sameValueError = null;
        var thread = new Thread(() =>
        {
            changeError = Record.Exception(() => scene.SetStyleSheets(replacement));
            sameValueError = Record.Exception(() => scene.SetStyleSheets(snapshot));
        });

        thread.Start();
        thread.Join();

        Assert.IsType<InvalidOperationException>(changeError);
        Assert.Null(sameValueError);
        Assert.Same(snapshot, scene.StyleSheets);
        scene.Close();
    }

    [Fact]
    public void StyleSheetsCannotChangeDuringLayout()
    {
        UIScene scene = null!;
        Exception? error = null;
        var root = new LayoutNode
        {
            MeasureAction = () => error = Record.Exception(() => scene.SetStyleSheets(SourceStyleSheets))
        };
        scene = new UIScene(root);
        scene.Open();

        scene.PrepareFrame(new Size(20, 20));

        Assert.IsType<InvalidOperationException>(error);
        Assert.Same(StyleResolver.Default, scene.StyleResolver);
        scene.Close();
    }

    [Fact]
    public void StyleSheetsCannotChangeDuringDrawing()
    {
        UIScene scene = null!;
        Exception? error = null;
        var root = new DrawingNode
        {
            DrawAction = () => error = Record.Exception(() => scene.SetStyleSheets(SourceStyleSheets))
        };
        scene = new UIScene(root);
        scene.Open();
        scene.PrepareFrame(new Size(20, 20));

        _ = scene.CreateDrawCommandList();

        Assert.IsType<InvalidOperationException>(error);
        Assert.Same(StyleResolver.Default, scene.StyleResolver);
        scene.Close();
    }

    [Fact]
    public void ClosedSceneStyleChangesApplyWhenFrameUpdatesResume()
    {
        var node = new ProbeNode();
        var scene = new UIScene(node);
        scene.Open();
        scene.Close();

        scene.SetStyleSheets(TargetStyleSheets);
        Assert.Same(scene, node.Scene);
        Assert.Equal(0, node.Value);

        scene.Open();
        try
        {
            scene.PrepareFrame(new Size(100, 100));
            Assert.Equal(2, node.Value);
        }
        finally
        {
            scene.Close();
        }
    }

    [Fact]
    public void StyleCalculationFailureKeepsNewResolverAndPreviousSnapshot()
    {
        var node = new ThrowingStyleNode();
        var scene = new UIScene(node);
        var sheets = new[]
        {
            new StyleSheet([
                new StyleRule(
                    StyleSelector.For<ThrowingStyleNode>(),
                    [StyleSetter.Create(ThrowingStyleNode.ValueProperty, new GuardedValue(0.5))])
            ])
        };
        GuardedValue.FailNextComparison = true;
        try
        {
            scene.SetStyleSheets(sheets);
            Assert.Throws<InvalidOperationException>(scene.Root!.UpdateStyles);
        }
        finally
        {
            GuardedValue.FailNextComparison = false;
        }

        Assert.NotSame(StyleResolver.Default, scene.StyleResolver);
        Assert.Equal(1, node.Opacity);
    }

    [Fact]
    public void StyleSheetChangeDoesNotNotifyLocallyMaskedProperty()
    {
        var node = new ProbeNode();
        var scene = new UIScene(node);
        scene.SetStyleSheets(SourceStyleSheets);
        scene.Root!.UpdateStyles();
        node.Value = 5;
        Assert.Equal(5, node.Value);

        var notifications = 0;
        node.PropertyChanged += (_, e) =>
            notifications += ReferenceEquals(e.Property, ProbeNode.ValueProperty) ? 1 : 0;

        scene.SetStyleSheets(TargetStyleSheets);
        scene.Root!.UpdateStyles();

        Assert.Equal(5, node.Value);
        Assert.Equal(0, notifications);
    }

    [Fact]
    public void CrossSceneMigrationReadsTargetStyleSheets()
    {
        var node = new ProbeNode();
        var source = new UIScene(node);
        source.SetStyleSheets(SourceStyleSheets);
        source.Root!.UpdateStyles();
        var target = new UIScene();
        target.SetStyleSheets(TargetStyleSheets);

        Assert.Equal(1, node.Value);
        Assert.Same(source, node.Scene);

        target.Root = node;
        target.Root!.UpdateStyles();

        Assert.Same(target, node.Scene);
        Assert.Equal(2, node.Value);
    }

    [Fact]
    public void ParentCrossSceneMigrationReadsTargetStyleSheets()
    {
        var node = new ProbeNode();
        var sourceRoot = new Panel();
        sourceRoot.Children.Add(node);
        var source = new UIScene(sourceRoot);
        source.SetStyleSheets(SourceStyleSheets);
        var targetRoot = new Panel();
        var target = new UIScene(targetRoot);
        target.SetStyleSheets(TargetStyleSheets);

        targetRoot.Children.Add(node);
        target.Root!.UpdateStyles();

        Assert.Same(source, sourceRoot.Scene);
        Assert.Same(target, node.Scene);
        Assert.Equal(2, node.Value);
    }

    [Fact]
    public void FailedCrossSceneStyleApplicationOccursAfterTransferAndInputCleanup()
    {
        var node = new FailOnceStyleNode { Focusable = true };
        var sourceSheets = new[]
        {
            new StyleSheet([
                new StyleRule(
                    StyleSelector.For<FailOnceStyleNode>(),
                    [StyleSetter.Create(FailOnceStyleNode.ValueProperty, new GuardedValue(1))])
            ])
        };
        var targetSheets = new[]
        {
            new StyleSheet([
                new StyleRule(
                    StyleSelector.For<FailOnceStyleNode>(),
                    [StyleSetter.Create(FailOnceStyleNode.ValueProperty, new GuardedValue(2))]),
                new StyleRule(
                    StyleSelector.For<FailOnceStyleNode>([StyleSelectorCondition.Pseudo(PseudoClass.Disabled)]),
                    [StyleSetter.Create(UIElement.OpacityProperty, 0.5)])
            ])
        };
        var source = new UIScene(node);
        source.SetStyleSheets(sourceSheets);
        var target = new UIScene();
        target.SetStyleSheets(targetSheets);
        source.Open();
        target.Open();
        source.PrepareFrame(new Size(20, 20));
        Assert.True(node.Focus());
        Assert.Equal(1d, node.Value);
        GuardedValue.FailNextComparison = true;
        try
        {
            target.Root = node;
            Assert.Throws<InvalidOperationException>(target.Root!.UpdateStyles);
        }
        finally
        {
            GuardedValue.FailNextComparison = false;
        }

        Assert.Null(source.Root);
        Assert.Same(target, node.Scene);
        Assert.False(node.IsFocused);
        Assert.Null(source.FocusedNode);
        Assert.Equal(1d, node.Value);
    }

    private sealed class ProbeNode : UIElement
    {
        public static readonly Property<int> ValueProperty =
            Property.Register<ProbeNode, int>(nameof(Value));

        public int Value
        {
            get => GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public int ValueUnderNewStyles { get; set; }
    }

    private static StyleSheet[] NewStyleSheets =>
    [
        new StyleSheet([
            new StyleRule(
                StyleSelector.For<ProbeNode>(),
                [StyleSetter.Create(ProbeNode.ValueProperty, 2)])
        ])
    ];

    private static StyleSheet[] SourceStyleSheets =>
    [
        new StyleSheet([
            new StyleRule(
                StyleSelector.For<ProbeNode>(),
                [StyleSetter.Create(ProbeNode.ValueProperty, 1)])
        ])
    ];

    private static StyleSheet[] TargetStyleSheets =>
    [
        new StyleSheet([
            new StyleRule(
                StyleSelector.For<ProbeNode>(),
                [StyleSetter.Create(ProbeNode.ValueProperty, 2)])
        ])
    ];

    private sealed class LayoutNode : UIElement
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

    private sealed class DrawingNode : UIElement
    {
        internal Action? DrawAction { get; set; }

        protected override void DrawCore(DrawingContext context) => DrawAction?.Invoke();
    }

    private sealed class ThrowingStyleNode : UIElement
    {
        internal static readonly Property<GuardedValue> ValueProperty =
            Property.Register<ThrowingStyleNode, GuardedValue>("Value", new GuardedValue(0),
                comparer: EqualityComparer<GuardedValue>.Default);
    }

    private sealed class FailOnceStyleNode : UIElement
    {
        internal static readonly Property<GuardedValue> ValueProperty =
            Property.Register<FailOnceStyleNode, GuardedValue>("Value", new GuardedValue(0),
                comparer: EqualityComparer<GuardedValue>.Default);

        internal double Value => GetValue(ValueProperty).Number;
    }

    private sealed class GuardedValue(double number) : IEquatable<GuardedValue>
    {
        internal static bool FailNextComparison;

        internal double Number => number;

        public bool Equals(GuardedValue? other)
        {
            if (FailNextComparison)
            {
                FailNextComparison = false;
                throw new InvalidOperationException("guarded value comparison failed");
            }

            return other is not null && number.Equals(other.Number);
        }

        public override bool Equals(object? obj) => obj is GuardedValue other && Equals(other);

        public override int GetHashCode() => number.GetHashCode();
    }
}
