using System.Runtime.ExceptionServices;
using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Huds;
using PanguEngine.Registries;
using PanguEngine.Threading;
using PanguEngine.UI;
using PanguEngine.UI.Controls;
using PanguEngine.UI.Drawing;

namespace PanguEngine.Tests.UI;

public sealed class UIManagerTests
{
    [Fact]
    public void ManagerStartsWithoutScene()
    {
        var manager = new UIManager();

        Assert.Null(manager.CurrentScene);
        Assert.NotNull(manager.Hud);
        manager.Destroy();
    }

    [Fact]
    public void CurrentSceneOperationsKeepHudMounted()
    {
        var manager = new UIManager();
        var hudRoot = new TestNode();
        var key = InitializeHud(manager, hudRoot);
        var scene = new GameUIScene(new TestNode());

        manager.Open(scene);
        manager.UpdateFrame(new Size(200, 100), 0);

        var hud = manager.Hud.Get(key);
        Assert.Same(hudRoot, hud.Root);
        Assert.Same(manager.Hud.Scene.Root, hudRoot.Parent);
        Assert.Same(manager.Hud.Scene, hudRoot.Scene);
        Assert.Same(scene, manager.CurrentScene);

        manager.Close();
        manager.Destroy();
    }

    [Fact]
    public void UpdateLayoutsHudWhenCurrentSceneIsAbsent()
    {
        var manager = new UIManager();
        var hudRoot = new TestNode { CoreDesiredSize = new Size(5, 5) };
        var key = InitializeHud(manager, hudRoot);

        manager.UpdateFrame(new Size(200, 100), 0);

        Assert.True(manager.Hud.Get(key).Root.IsArrangeValid);
        manager.Destroy();
    }

    [Fact]
    public void PlainGameUISceneCanBeOpenedWithoutSubclassing()
    {
        var manager = new UIManager();
        var root = new TestNode();
        var scene = new GameUIScene(root);

        manager.Open(scene);

        Assert.Same(scene, manager.CurrentScene);
        Assert.Same(scene, root.Scene);
    }

    [Fact]
    public void OpenPublishesCurrentSceneAfterOpened()
    {
        var manager = new UIManager();
        var root = new TestNode();
        GameUIScene? openingCurrent = null;
        GameUIScene? openedCurrent = null;
        var scene = new RecordingScene(root);
        scene.Opening = () => openingCurrent = manager.CurrentScene;
        scene.Opened = () => openedCurrent = manager.CurrentScene;

        manager.Open(scene);

        Assert.Null(openingCurrent);
        Assert.Null(openedCurrent);
        Assert.Same(scene, manager.CurrentScene);
        Assert.Same(scene, root.Scene);
    }

    [Fact]
    public void CloseClearsCurrentSceneBeforeClosingCallbacks()
    {
        var manager = new UIManager();
        var root = new TestNode();
        GameUIScene? closingCurrent = null;
        GameUIScene? closedCurrent = null;
        var scene = new RecordingScene(root);
        scene.Closing = () => closingCurrent = manager.CurrentScene;
        scene.Closed = () => closedCurrent = manager.CurrentScene;
        manager.Open(scene);

        manager.Close();

        Assert.Null(closingCurrent);
        Assert.Null(closedCurrent);
        Assert.Null(manager.CurrentScene);
        Assert.Same(scene, root.Scene);
    }

    [Fact]
    public void ReplaceFullyClosesOldSceneBeforeOpeningNewScene()
    {
        var manager = new UIManager();
        var oldRoot = new TestNode();
        var newRoot = new TestNode();
        var events = new List<string>();
        var oldScene = new RecordingScene(oldRoot);
        var newScene = new RecordingScene(newRoot);
        oldScene.Closing = () => Record("old-closing");
        oldScene.Closed = () => Record("old-closed");
        newScene.Opening = () => Record("new-opening");
        newScene.Opened = () => Record("new-opened");
        manager.Open(oldScene);

        manager.Open(newScene);

        Assert.Equal(["old-closing", "old-closed", "new-opening", "new-opened"], events);
        Assert.Same(oldScene, oldRoot.Scene);
        Assert.Same(newScene, newRoot.Scene);
        Assert.Same(newScene, manager.CurrentScene);

        void Record(string name)
        {
            events.Add(name);
        }
    }

    [Fact]
    public void OpeningCurrentSceneIsANoOp()
    {
        var manager = new UIManager();
        var openedCalls = 0;
        var closingCalls = 0;
        var scene = new RecordingScene(new TestNode());
        scene.Opened = () => openedCalls++;
        scene.Closing = () => closingCalls++;

        manager.Open(scene);
        manager.Open(scene);

        Assert.Equal(1, openedCalls);
        Assert.Equal(0, closingCalls);
        Assert.Same(scene, manager.CurrentScene);
    }

    [Fact]
    public void ClosedSceneCanBeOpenedAgain()
    {
        var manager = new UIManager();
        var openedCalls = 0;
        var scene = new RecordingScene(new TestNode());
        scene.Opened = () => openedCalls++;

        manager.Open(scene);
        manager.Close();
        manager.Open(scene);

        Assert.Equal(2, openedCalls);
        Assert.Same(scene, manager.CurrentScene);
    }

    [Fact]
    public void OpenRejectsAlreadyOpenSceneWithoutReplacingCurrentScene()
    {
        var manager = new UIManager();
        var current = new GameUIScene(new TestNode());
        manager.Open(current);
        var unavailable = new GameUIScene(new TestNode());
        unavailable.Open();

        Assert.Throws<ArgumentNullException>(() => manager.Open(null!));
        Assert.Throws<InvalidOperationException>(() => manager.Open(unavailable));

        Assert.Same(current, manager.CurrentScene);
        unavailable.Close();
        manager.Close();
    }

    [Fact]
    public void BoundSceneCanBeOpenedByAnotherManagerAfterItCloses()
    {
        var first = new UIManager();
        var second = new UIManager();
        var scene = new GameUIScene(new TestNode());
        first.Open(scene);

        Assert.Throws<InvalidOperationException>(() => second.Open(scene));
        Assert.Same(scene, first.CurrentScene);
        Assert.Null(second.CurrentScene);

        first.Close();
        second.Open(scene);

        Assert.Same(scene, second.CurrentScene);
        second.Close();
    }

    [Fact]
    public void LifecycleCallbacksRejectUpdateAndDestroy()
    {
        var manager = new UIManager();
        var errors = new List<Exception?>();
        var scene = new RecordingScene(new TestNode());
        scene.Opening = Capture;
        scene.Opened = Capture;
        scene.Closing = Capture;
        scene.Closed = Capture;

        manager.Open(scene);
        manager.Close();

        Assert.Equal(8, errors.Count);
        Assert.All(errors, error => Assert.IsType<InvalidOperationException>(error));

        void Capture()
        {
            errors.Add(Record.Exception(manager.Update));
            errors.Add(Record.Exception(manager.Destroy));
        }
    }

    [Theory]
    [InlineData("opening")]
    [InlineData("opened")]
    [InlineData("closing")]
    [InlineData("closed")]
    public void LifecycleCallbackQueuesSceneRequests(string hook)
    {
        var manager = new UIManager();
        var first = new RecordingScene(new TestNode());
        var second = new RecordingScene(new TestNode());
        var third = new RecordingScene(new TestNode());
        var notifications = new List<(GameUIScene? Old, GameUIScene? New)>();
        manager.CurrentSceneChanged += (oldScene, newScene) => notifications.Add((oldScene, newScene));
        GameUIScene? currentDuringCallback = null;
        Action callback = () =>
        {
            currentDuringCallback = manager.CurrentScene;
            manager.Open(second);
            manager.Close();
            manager.Close();
            manager.Open(third);
            Assert.Same(currentDuringCallback, manager.CurrentScene);
        };

        switch (hook)
        {
            case "opening":
                first.Opening = callback;
                break;
            case "opened":
                first.Opened = callback;
                break;
            case "closing":
                first.Closing = callback;
                break;
            case "closed":
                first.Closed = callback;
                break;
        }

        manager.Open(first);
        if (hook is "closing" or "closed")
            manager.Close();

        Assert.Null(currentDuringCallback);
        Assert.Same(third, manager.CurrentScene);
        var expected = hook is "closing" or "closed"
            ? new (GameUIScene? Old, GameUIScene? New)[]
            {
                (null, first), (first, null), (null, second), (second, null), (null, third)
            }
            : new (GameUIScene? Old, GameUIScene? New)[]
            {
                (null, first), (first, second), (second, null), (null, third)
            };
        Assert.Equal(expected, notifications);
        manager.Destroy();
    }

    [Fact]
    public void NotificationSubscribersObserveEachTransitionNewScene()
    {
        var manager = new UIManager();
        var first = new GameUIScene(new TestNode());
        var second = new GameUIScene(new TestNode());
        var third = new GameUIScene(new TestNode());
        var events = new List<(string Subscriber, GameUIScene? New)>();
        var queued = false;
        manager.CurrentSceneChanged += (_, newScene) =>
        {
            events.Add(("first", newScene));
            if (!queued && ReferenceEquals(newScene, first))
            {
                queued = true;
                Assert.Throws<InvalidOperationException>(manager.Update);
                Assert.Throws<InvalidOperationException>(() => manager.AppendDrawCommands(new DrawCommandList()));
                Assert.Throws<InvalidOperationException>(manager.Destroy);
                manager.Open(second);
                manager.Open(third);
            }
        };
        manager.CurrentSceneChanged += (_, newScene) =>
        {
            events.Add(("second", newScene));
            Assert.Same(manager.CurrentScene, newScene);
        };

        manager.Open(first);
        manager.Close();

        var expected = new (string Subscriber, GameUIScene? New)[]
        {
            ("first", first), ("second", first),
            ("first", second), ("second", second),
            ("first", third), ("second", third),
            ("first", null), ("second", null)
        };
        Assert.Equal(expected, events);
        manager.Destroy();
    }

    [Fact]
    public void NotificationCanCloseAndReopenSameScene()
    {
        var manager = new UIManager();
        var scene = new RecordingScene(new TestNode());
        var openedCalls = 0;
        var closedCalls = 0;
        scene.Opened = () => openedCalls++;
        scene.Closed = () => closedCalls++;
        var queued = false;
        manager.CurrentSceneChanged += (_, newScene) =>
        {
            if (queued || !ReferenceEquals(newScene, scene))
                return;

            queued = true;
            manager.Close();
            manager.Open(scene);
            manager.Open(scene);
        };

        manager.Open(scene);

        Assert.Same(scene, manager.CurrentScene);
        Assert.Equal(2, openedCalls);
        Assert.Equal(1, closedCalls);
        manager.Destroy();
    }

    [Fact]
    public void LifecycleCallbackFailureDropsQueuedRequests()
    {
        var manager = new UIManager();
        var expected = new InvalidOperationException("opening");
        var second = new RecordingScene(new TestNode());
        var secondOpened = 0;
        second.Opened = () => secondOpened++;
        var first = new RecordingScene(new TestNode())
        {
            Opening = () =>
            {
                manager.Open(second);
                manager.Close();
                throw expected;
            }
        };

        var actual = Assert.Throws<InvalidOperationException>(() => manager.Open(first));

        Assert.Same(expected, actual);
        Assert.Null(manager.CurrentScene);
        Assert.Equal(0, secondOpened);
        manager.Destroy();
        Assert.Throws<ObjectDisposedException>(() => manager.Open(new GameUIScene(new TestNode())));
    }

    [Fact]
    public void NotificationFailureDropsQueuedRequests()
    {
        var manager = new UIManager();
        var expected = new InvalidOperationException("changed");
        var first = new RecordingScene(new TestNode());
        var second = new RecordingScene(new TestNode());
        var secondOpened = 0;
        second.Opened = () => secondOpened++;
        var queued = false;
        manager.CurrentSceneChanged += (_, newScene) =>
        {
            if (queued || !ReferenceEquals(newScene, first))
                return;

            queued = true;
            manager.Open(second);
            throw expected;
        };

        var actual = Assert.Throws<InvalidOperationException>(() => manager.Open(first));

        Assert.Same(expected, actual);
        Assert.Same(first, manager.CurrentScene);
        Assert.Equal(0, secondOpened);
        manager.Destroy();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DequeuedTargetFailureKeepsPreviouslyCommittedScene(bool precedingSuccess)
    {
        var manager = new UIManager();
        var owner = new UIManager();
        var first = new RecordingScene(new TestNode());
        var middle = new GameUIScene(new TestNode());
        var closingCalls = 0;
        first.Closing = () => closingCalls++;
        var occupied = new GameUIScene(new TestNode());
        var tail = new RecordingScene(new TestNode());
        var tailOpened = 0;
        tail.Opened = () => tailOpened++;
        owner.Open(occupied);
        var queued = false;
        manager.CurrentSceneChanged += (_, newScene) =>
        {
            if (queued || !ReferenceEquals(newScene, first))
                return;

            queued = true;
            if (precedingSuccess)
                manager.Open(middle);
            manager.Open(occupied);
            manager.Open(tail);
        };
        var changes = new List<(GameUIScene? Old, GameUIScene? New)>();
        manager.CurrentSceneChanged += (oldScene, newScene) => changes.Add((oldScene, newScene));

        Assert.Throws<InvalidOperationException>(() => manager.Open(first));

        Assert.Same(precedingSuccess ? middle : first, manager.CurrentScene);
        Assert.Equal(precedingSuccess ? 1 : 0, closingCalls);
        (GameUIScene? Old, GameUIScene? New)[] expected = precedingSuccess
            ? [(null, first), (first, middle)]
            : [(null, first)];
        Assert.Equal(expected, changes);
        Assert.Equal(0, tailOpened);
        owner.Close();
        owner.Destroy();
        manager.Destroy();
    }

    [Fact]
    public void CloseFailureStopsSceneChangeNotifications()
    {
        var manager = new UIManager();
        var closeError = new InvalidOperationException("close");
        var notifyError = new InvalidOperationException("notify");
        var failedChangeNotified = false;
        var oldScene = new RecordingScene(new TestNode())
        {
            Closing = () => throw closeError
        };
        var next = new RecordingScene(new TestNode());
        var tail = new RecordingScene(new TestNode());
        var openedCalls = 0;
        next.Opened = () => openedCalls++;
        tail.Opened = () => openedCalls++;
        var queued = false;
        manager.CurrentSceneChanged += (_, newScene) =>
        {
            if (!queued && ReferenceEquals(newScene, oldScene))
            {
                queued = true;
                manager.Open(next);
                manager.Open(tail);
                return;
            }

            failedChangeNotified = true;
            throw notifyError;
        };

        var actual = Assert.Throws<InvalidOperationException>(() => manager.Open(oldScene));

        Assert.Same(closeError, actual);
        Assert.False(failedChangeNotified);
        Assert.Null(manager.CurrentScene);
        Assert.Equal(0, openedCalls);
        manager.Destroy();
    }

    [Fact]
    public void DestroyLifecycleCallbacksRejectNestedManagerOperations()
    {
        var manager = new UIManager();
        var errors = new List<Exception?>();
        var scene = new RecordingScene(new TestNode());
        scene.Closing = Capture;
        scene.Closed = Capture;
        manager.Open(scene);

        manager.Destroy();

        Assert.Equal(8, errors.Count);
        Assert.All(errors, error => Assert.IsType<InvalidOperationException>(error));
        Assert.Throws<ObjectDisposedException>(() => manager.Open(new GameUIScene(new TestNode())));

        void Capture() => CaptureNestedOperationErrors(manager, errors);
    }

    [Fact]
    public void LayoutCallbacksRejectNestedManagerOperations()
    {
        var manager = new UIManager();
        var errors = new List<Exception?>();
        var root = new TestNode
        {
            CoreDesiredSize = new Size(5, 5),
            MeasureAction = () => CaptureNestedOperationErrors(manager, errors),
            ArrangeAction = () => CaptureNestedOperationErrors(manager, errors)
        };
        var scene = new GameUIScene(root);
        manager.Open(scene);

        manager.UpdateFrame(new Size(20, 20), 0);

        Assert.Equal(8, errors.Count);
        Assert.All(errors, error => Assert.IsType<InvalidOperationException>(error));
        Assert.Same(scene, manager.CurrentScene);
        manager.Close();
    }

    [Fact]
    public void HudLayoutRejectsNestedManagerOperations()
    {
        var manager = new UIManager();
        var errors = new List<Exception?>();
        var node = new TestNode
        {
            CoreDesiredSize = new Size(5, 5),
            MeasureAction = () => CaptureNestedOperationErrors(manager, errors),
            ArrangeAction = () => CaptureNestedOperationErrors(manager, errors)
        };
        InitializeHud(manager, node);

        manager.UpdateFrame(new Size(20, 20), 0);

        Assert.Equal(8, errors.Count);
        Assert.All(errors, error => Assert.IsType<InvalidOperationException>(error));
        manager.Destroy();
    }

    [Fact]
    public void ScenePostRunsBeforeManagerLayout()
    {
        var manager = new UIManager();
        var events = new List<string>();
        var root = new TestNode
        {
            CoreDesiredSize = new Size(5, 5),
            MeasureAction = () => events.Add("measure"),
            ArrangeAction = () => events.Add("arrange")
        };
        var scene = new GameUIScene(root);
        manager.Open(scene);
        scene.Post(() =>
        {
            scene.Scale = 2;
            events.Add("post");
        });

        manager.UpdateFrame(new Size(20, 10), 0);

        Assert.Equal(["post", "measure", "arrange"], events);
        Assert.Equal(new Size(10, 5), root.LastMeasureConstraint);
        manager.Close();
    }

    [Fact]
    public void PostedLayoutRoundingChangeAppliesDuringTheSameUpdate()
    {
        var manager = new UIManager();
        var root = new TestNode { CoreDesiredSize = new Size(10.25, 10.25) };
        var scene = new GameUIScene(root);
        manager.Open(scene);
        scene.Post(() => scene.UseLayoutRounding = false);

        manager.UpdateFrame(new Size(100, 100), 0);

        Assert.Equal(new Size(10.25, 10.25), root.DesiredSize);
        manager.Close();
    }

    [Fact]
    public void LayoutRoundingChangeInvalidatesAndReflowsLayout()
    {
        var manager = new UIManager();
        var root = new TestNode { CoreDesiredSize = new Size(10.25, 10.25) };
        var scene = new GameUIScene(root);
        manager.Open(scene);
        manager.UpdateFrame(new Size(100, 100), 0);
        Assert.Equal(new Size(11, 11), root.DesiredSize);

        scene.UseLayoutRounding = false;

        Assert.False(root.IsMeasureValid);
        Assert.False(root.IsArrangeValid);
        manager.UpdateFrame(new Size(100, 100), 0);
        Assert.Equal(new Size(10.25, 10.25), root.DesiredSize);
        manager.Close();
    }

    [Fact]
    public void UpdateAppliesCurrentSceneScaleAndReflowsWhenScaleChanges()
    {
        var manager = new UIManager();
        var root = new TestNode { CoreDesiredSize = new Size(5, 5) };
        var scene = new GameUIScene(root) { Scale = 2 };
        manager.Open(scene);

        manager.UpdateFrame(new Size(100, 80), 0);

        Assert.Equal(new Size(50, 40), root.LastMeasureConstraint);
        Assert.Equal(new Rect(0, 0, 50, 40), root.LayoutBounds);

        scene.Scale = 4;
        Assert.Equal(new Size(50, 40), root.LastMeasureConstraint);
        Assert.False(root.IsMeasureValid);
        Assert.False(root.IsArrangeValid);

        manager.UpdateFrame(new Size(100, 80), 0);

        Assert.Equal(new Size(25, 20), root.LastMeasureConstraint);
        Assert.Equal(new Rect(0, 0, 25, 20), root.LayoutBounds);
        manager.Close();
    }

    [Fact]
    public void ScenesKeepIndependentScaleConfigurations()
    {
        var manager = new UIManager();
        var firstRoot = new TestNode { CoreDesiredSize = new Size(5, 5) };
        var secondRoot = new TestNode { CoreDesiredSize = new Size(5, 5) };
        var first = new GameUIScene(firstRoot) { Scale = 2 };
        var second = new GameUIScene(secondRoot) { Scale = 4 };

        manager.Open(first);
        manager.UpdateFrame(new Size(100, 80), 0);
        manager.Open(second);
        manager.UpdateFrame(new Size(100, 80), 0);

        Assert.Equal(2, first.Scale);
        Assert.Equal(new Size(50, 40), firstRoot.LastMeasureConstraint);
        Assert.Equal(4, second.Scale);
        Assert.Equal(new Size(25, 20), secondRoot.LastMeasureConstraint);
        manager.Close();
    }

    [Fact]
    public void ReopeningReusesLayoutCacheUntilLayoutEnvironmentChanges()
    {
        var manager = new UIManager();
        var root = new TestNode { CoreDesiredSize = new Size(10.25, 10.25) };
        var scene = new GameUIScene(root);
        manager.Open(scene);
        manager.UpdateFrame(new Size(100, 100), 0);
        manager.Close();
        var measureCalls = root.MeasureCalls;
        var arrangeCalls = root.ArrangeCalls;

        manager.Open(scene);
        manager.UpdateFrame(new Size(100, 100), 0);
        manager.Close();

        Assert.Equal(measureCalls, root.MeasureCalls);
        Assert.Equal(arrangeCalls, root.ArrangeCalls);

        scene.UseLayoutRounding = false;
        manager.Open(scene);
        manager.UpdateFrame(new Size(100, 100), 0);

        Assert.Equal(measureCalls + 1, root.MeasureCalls);
        Assert.Equal(arrangeCalls + 1, root.ArrangeCalls);
        manager.Close();
    }

    [Fact]
    public void ScenePostRejectsSynchronousSceneChanges()
    {
        var manager = new UIManager();
        var root = new TestNode { CoreDesiredSize = new Size(5, 5) };
        var scene = new GameUIScene(root);
        var replacement = new GameUIScene();
        manager.Open(scene);
        scene.Post(() =>
        {
            Assert.Throws<InvalidOperationException>(manager.Close);
            Assert.Throws<InvalidOperationException>(() => manager.Open(replacement));
        });

        manager.UpdateFrame(new Size(20, 10), 0);

        Assert.Same(scene, manager.CurrentScene);
        Assert.False(replacement.IsOpen());
        Assert.Equal(1, root.MeasureCalls);
        Assert.Equal(1, root.ArrangeCalls);
        manager.Destroy();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DispatchedReplacementKeepsOldSceneUntilQueueRuns(bool fromFrameCallback)
    {
        var queue = new EngineWorkQueue();
        var dispatcher = new EngineDispatcher(queue);
        var manager = new UIManager();
        var events = new List<string>();
        var oldScene = new RecordingScene(new TestNode
        {
            CoreDesiredSize = new Size(10, 10),
            DrawAction = DrawRectangle
        });
        var replacement = new RecordingScene(new TestNode
        {
            CoreDesiredSize = new Size(10, 10),
            MeasureAction = () => events.Add("layout"),
            DrawAction = DrawRectangle
        }) { FrameAction = _ => events.Add("frame") };
        Task change = Task.CompletedTask;
        manager.Open(oldScene);

        void Replace()
        {
            change = dispatcher.InvokeAsync(() =>
            {
                manager.Open(replacement);
                replacement.Post(() => events.Add("post"));
            });
        }

        if (fromFrameCallback)
            oldScene.FrameAction = _ => Replace();
        else
            oldScene.Post(Replace);

        manager.UpdateFrame(new Size(20, 10), 0);

        Assert.Same(oldScene, oldScene.Root!.Scene);
        Assert.Same(oldScene, manager.CurrentScene);
        Assert.False(change.IsCompleted);
        Assert.False(replacement.IsOpen());
        Assert.Empty(events);
        Assert.False(replacement.Root!.IsMeasureValid);
        var commands = new DrawCommandList();
        manager.AppendDrawCommands(commands);
        Assert.Single(commands.OfType<FillRectangleCommand>());

        queue.RunPending();
        Assert.True(change.IsCompletedSuccessfully);
        Assert.Same(replacement, manager.CurrentScene);
        Assert.False(oldScene.IsOpen());

        manager.UpdateFrame(new Size(20, 10), 0);

        Assert.Equal(["frame", "post", "layout"], events);
        Assert.True(replacement.Root!.IsArrangeValid);
        commands.Clear();
        manager.AppendDrawCommands(commands);
        Assert.Single(commands.OfType<FillRectangleCommand>());
        manager.Destroy();
        queue.Destroy();
        await change;
    }

    [Fact]
    public void UpdateUsesViewportBoundsAndExistingLayoutCaches()
    {
        var manager = new UIManager();
        var root = new TestNode { CoreDesiredSize = new Size(5, 5) };
        manager.Open(new GameUIScene(root));

        manager.UpdateFrame(new Size(20, 10), 0);

        Assert.Equal(new Size(20, 10), root.LastMeasureConstraint);
        Assert.Equal(new Size(20, 10), root.LastArrangeSize);
        Assert.Equal(new Rect(0, 0, 20, 10), root.LayoutBounds);
        Assert.Equal(1, root.MeasureCalls);
        Assert.Equal(1, root.ArrangeCalls);

        manager.UpdateFrame(new Size(20, 10), 0);
        Assert.Equal(1, root.MeasureCalls);
        Assert.Equal(1, root.ArrangeCalls);

        manager.UpdateFrame(new Size(30, 15), 0);
        Assert.Equal(2, root.MeasureCalls);
        Assert.Equal(2, root.ArrangeCalls);
        Assert.Equal(new Rect(0, 0, 30, 15), root.LayoutBounds);
        manager.Close();
    }

    [Fact]
    public void InvalidViewportDoesNotUpdateScene()
    {
        var manager = new UIManager();
        var calls = 0;
        var scene = new GameUIScene(new TestNode());
        manager.Open(scene);
        scene.Post(() => calls++);

        Assert.Throws<ArgumentOutOfRangeException>(() => manager.UpdateFrame(Size.Infinite, 0));
        Assert.Equal(0, calls);

        manager.UpdateFrame(new Size(20, 10), 0);
        Assert.Equal(1, calls);
        manager.Close();
    }

    [Fact]
    public void WrongThreadManagerOperationsFailWithoutChangingState()
    {
        var manager = new UIManager();
        var scene = new GameUIScene(new TestNode());
        manager.Open(scene);

        var errors = RunOnBackgroundThread(() =>
            (Open: Record.Exception(() => manager.Open(new GameUIScene(new TestNode()))),
                Close: Record.Exception(manager.Close),
                Update: Record.Exception(() => manager.UpdateFrame(new Size(20, 20), 0)),
                Destroy: Record.Exception(manager.Destroy)));

        Assert.IsType<InvalidOperationException>(errors.Open);
        Assert.IsType<InvalidOperationException>(errors.Close);
        Assert.IsType<InvalidOperationException>(errors.Update);
        Assert.IsType<InvalidOperationException>(errors.Destroy);
        Assert.Same(scene, manager.CurrentScene);
        manager.Close();
    }

    [Fact]
    public void CurrentSceneChangedPublishesOnlyFinalTransitions()
    {
        var manager = new UIManager();
        var first = new GameUIScene(new TestNode());
        var second = new GameUIScene(new TestNode());
        var changes = new List<(GameUIScene? Old, GameUIScene? New)>();
        manager.CurrentSceneChanged += (oldScene, newScene) => changes.Add((oldScene, newScene));

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
    public void FailedReplacementStopsBeforePublishingSceneChange()
    {
        var manager = new UIManager();
        var expected = new InvalidOperationException("closing");
        var oldScene = new RecordingScene(new TestNode())
        {
            Closing = () => throw expected
        };
        var replacement = new GameUIScene(new TestNode());
        var changes = new List<(GameUIScene? Old, GameUIScene? New)>();
        manager.CurrentSceneChanged += (old, current) => changes.Add((old, current));
        manager.Open(oldScene);
        changes.Clear();

        var actual = Assert.Throws<InvalidOperationException>(() => manager.Open(replacement));

        Assert.Same(expected, actual);
        Assert.Null(manager.CurrentScene);
        Assert.Empty(changes);
    }

    [Fact]
    public void DestroyStopsBeforeHudAndNotificationWhenSceneCloseFails()
    {
        var manager = new UIManager();
        var expected = new InvalidOperationException("closing");
        var scene = new RecordingScene(new TestNode())
        {
            Closing = () => throw expected
        };
        var changes = new List<(GameUIScene? Old, GameUIScene? New)>();
        manager.CurrentSceneChanged += (old, current) => changes.Add((old, current));
        manager.Open(scene);
        changes.Clear();

        var actual = Assert.Throws<InvalidOperationException>(manager.Destroy);

        Assert.Same(expected, actual);
        Assert.Null(manager.CurrentScene);
        Assert.True(manager.Hud.Scene.IsOpen());
        Assert.Empty(changes);
    }

    [Fact]
    public void DestroyClosesSceneAndIsIdempotent()
    {
        var manager = new UIManager();
        var scene = new GameUIScene(new TestNode());
        manager.Open(scene);

        manager.Destroy();
        manager.Destroy();

        Assert.Null(manager.CurrentScene);
        Assert.Same(scene, scene.Root!.Scene);
        Assert.Throws<ObjectDisposedException>(() => manager.Open(new GameUIScene(new TestNode())));
        Assert.Throws<ObjectDisposedException>(manager.Close);
        Assert.Throws<ObjectDisposedException>(() => manager.UpdateFrame(new Size(20, 20), 0));
    }

    [Fact]
    public void DestroyDuringScenePostIsRejectedAndLayoutContinues()
    {
        var manager = new UIManager();
        var root = new TestNode { CoreDesiredSize = new Size(5, 5) };
        var scene = new GameUIScene(root);
        var events = new List<string>();
        manager.Open(scene);
        scene.Post(() =>
        {
            events.Add("current-start");
            Assert.Throws<InvalidOperationException>(manager.Destroy);
            events.Add("current-end");
        });
        scene.Post(() => events.Add("remaining"));

        manager.UpdateFrame(new Size(20, 10), 0);

        Assert.Equal(["current-start", "current-end", "remaining"], events);
        Assert.Equal(1, root.MeasureCalls);
        Assert.Equal(1, root.ArrangeCalls);
        Assert.Same(scene, manager.CurrentScene);
        manager.Destroy();
    }

    [Fact]
    public async Task HoverRefreshCanDispatchSceneReplacement()
    {
        var queue = new EngineWorkQueue();
        var dispatcher = new EngineDispatcher(queue);
        var manager = new UIManager();
        var root = new Canvas();
        var leaf = new TestNode { Width = 20, Height = 20, DrawAction = DrawRectangle };
        root.Children.Add(leaf);
        var scene = new GameUIScene(root);
        var replacement = new GameUIScene(new TestNode
        {
            CoreDesiredSize = new Size(10, 10),
            DrawAction = DrawRectangle
        });
        Exception? updateError = null;
        Task change = Task.CompletedTask;
        leaf.PointerExited += (_, _) =>
        {
            updateError = Record.Exception(() => manager.UpdateFrame(new Size(100, 100), 0));
            Assert.Throws<InvalidOperationException>(() => manager.Open(replacement));
            change = dispatcher.InvokeAsync(() => manager.Open(replacement));
        };
        manager.Open(scene);
        manager.UpdateFrame(new Size(100, 100), 0);
        manager.ProcessPointerMoved(new Point(5, 5));
        leaf.IsHitTestVisible = false;

        manager.UpdateFrame(new Size(100, 100), 0);

        Assert.IsType<InvalidOperationException>(updateError);
        Assert.Same(scene, manager.CurrentScene);
        Assert.False(change.IsCompleted);
        var commands = new DrawCommandList();
        manager.AppendDrawCommands(commands);
        Assert.Single(commands.OfType<FillRectangleCommand>());

        queue.RunPending();
        Assert.True(change.IsCompletedSuccessfully);
        Assert.Same(replacement, manager.CurrentScene);
        manager.UpdateFrame(new Size(100, 100), 0);
        commands.Clear();
        manager.AppendDrawCommands(commands);
        Assert.Single(commands.OfType<FillRectangleCommand>());
        manager.Destroy();
        queue.Destroy();
        await change;
    }

    [Fact]
    public void ClosedManagerRejectsOperationsWithObjectDisposedException()
    {
        var manager = new UIManager();
        manager.Destroy();

        Assert.Throws<ObjectDisposedException>(() => manager.Open(new GameUIScene(new TestNode())));
        Assert.Throws<ObjectDisposedException>(manager.Close);
        Assert.Throws<ObjectDisposedException>(() => manager.UpdateFrame(new Size(20, 10), 0));
        Assert.Throws<ObjectDisposedException>(() => manager.ProcessFocusChanged(false));
        Assert.Throws<ObjectDisposedException>(() => manager.ProcessFocusChanged(true));
    }

    [Fact]
    public void HudPreparationCompletesBeforeScenePreparation()
    {
        var events = new List<string>();
        var manager = new UIManager();
        var hud = new RecordingHud(new TestNode { MeasureAction = () => events.Add("hud-layout") })
        {
            FixedAction = () => events.Add("hud-fixed"),
            FrameAction = alpha =>
            {
                Assert.Equal(0.75, alpha);
                events.Add("hud-frame");
            }
        };
        var definitions = new Registry<HudDefinition>(RegistryKeys.Hud);
        definitions.Register(ResourceKey.Create("test", "updates"), new HudDefinition(() => hud));
        definitions.Freeze();
        manager.InitializeHud(definitions);
        manager.Hud.Post(() => events.Add("hud-post"));
        var scene = new RecordingScene(new TestNode { MeasureAction = () => events.Add("scene-layout") })
        {
            FixedAction = () => events.Add("scene-fixed"),
            FrameAction = alpha =>
            {
                Assert.Equal(0.75, alpha);
                events.Add("scene-frame");
            }
        };
        manager.Open(scene);
        scene.Post(() => events.Add("scene-post"));

        manager.Update();
        Assert.Equal(["hud-fixed", "scene-fixed"], events);
        events.Clear();
        manager.UpdateFrame(new Size(100, 100), 0.75);

        Assert.Equal(["hud-frame", "hud-post", "hud-layout", "scene-frame", "scene-post", "scene-layout"], events);
        events.Clear();
        manager.UpdateFrame(new Size(100, 100), 0.75);
        Assert.Equal(["hud-frame", "scene-frame"], events);
        manager.Destroy();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HudPostAndFrameCallbackCanDispatchSceneOpening(bool fromFrameCallback)
    {
        var queue = new EngineWorkQueue();
        var dispatcher = new EngineDispatcher(queue);
        var events = new List<string>();
        var manager = new UIManager();
        var hud = new RecordingHud(new TestNode());
        var scene = new RecordingScene(new TestNode
        {
            CoreDesiredSize = new Size(10, 10),
            MeasureAction = () => events.Add("layout"),
            DrawAction = DrawRectangle
        }) { FrameAction = _ => events.Add("scene-frame") };
        Task change = Task.CompletedTask;

        void OpenScene()
        {
            hud.FrameAction = null;
            events.Add("hud-request");
            change = dispatcher.InvokeAsync(() =>
            {
                manager.Open(scene);
                scene.Post(() => events.Add("scene-post"));
            });
        }

        if (fromFrameCallback)
            hud.FrameAction = _ => OpenScene();
        else
            manager.Hud.Post(OpenScene);
        var definitions = new Registry<HudDefinition>(RegistryKeys.Hud);
        definitions.Register(ResourceKey.Create("test", "opener"), new HudDefinition(() => hud));
        definitions.Freeze();
        manager.InitializeHud(definitions);

        manager.UpdateFrame(new Size(100, 100), 0);

        Assert.Equal(["hud-request"], events);
        Assert.Null(manager.CurrentScene);
        Assert.False(change.IsCompleted);
        Assert.False(scene.Root!.IsMeasureValid);
        var commands = new DrawCommandList();
        manager.AppendDrawCommands(commands);
        Assert.Empty(commands.OfType<FillRectangleCommand>());

        queue.RunPending();
        Assert.True(change.IsCompletedSuccessfully);
        manager.UpdateFrame(new Size(100, 100), 0);

        Assert.Equal(["hud-request", "scene-frame", "scene-post", "layout"], events);
        Assert.True(scene.Root!.IsArrangeValid);
        commands.Clear();
        manager.AppendDrawCommands(commands);
        Assert.Single(commands.OfType<FillRectangleCommand>());
        manager.Destroy();
        queue.Destroy();
        await change;
    }

    [Fact]
    public async Task DispatchedCloseRunsOutsideFramePreparation()
    {
        var queue = new EngineWorkQueue();
        var dispatcher = new EngineDispatcher(queue);
        var manager = new UIManager();
        Task change = Task.CompletedTask;
        var scene = new RecordingScene(new TestNode())
        {
            FrameAction = _ => change = dispatcher.InvokeAsync(manager.Close)
        };
        manager.Open(scene);
        manager.UpdateFrame(new Size(100, 100), 0);

        Assert.Same(scene, manager.CurrentScene);
        Assert.True(scene.Root!.IsArrangeValid);
        Assert.False(change.IsCompleted);

        queue.RunPending();

        Assert.Null(manager.CurrentScene);
        Assert.False(scene.IsOpen());
        manager.Destroy();
        queue.Destroy();
        await change;
    }

    [Fact]
    public async Task DispatchedOpeningFailureIsReportedThroughTask()
    {
        var queue = new EngineWorkQueue();
        var dispatcher = new EngineDispatcher(queue);
        var manager = new UIManager();
        var failure = new InvalidOperationException("opening failed");
        var replacement = new RecordingScene
        {
            Opening = () => throw failure
        };
        Task change = Task.CompletedTask;
        var scene = new RecordingScene
        {
            FrameAction = _ => change = dispatcher.InvokeAsync(() => manager.Open(replacement))
        };
        manager.Open(scene);
        manager.UpdateFrame(new Size(100, 100), 0);

        Assert.Same(scene, manager.CurrentScene);
        queue.RunPending();
        Assert.Null(manager.CurrentScene);
        manager.Destroy();
        queue.Destroy();

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => change);
        Assert.Same(failure, actual);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void HudUpdateFailureStopsAllLaterStagesAndAllowsNextUpdate(bool fixedUpdate)
    {
        var events = new List<string>();
        var failure = new InvalidOperationException("HUD update failed");
        var shouldThrow = true;
        var hudRoot = new TestNode { MeasureAction = () => events.Add("hud-layout") };
        var firstHud = new RecordingHud(hudRoot)
        {
            FixedAction = () =>
            {
                events.Add("first-fixed");
                if (shouldThrow && fixedUpdate)
                    throw failure;
            },
            FrameAction = _ =>
            {
                events.Add("first-frame");
                if (shouldThrow && !fixedUpdate)
                    throw failure;
            }
        };
        var laterHud = new RecordingHud(new Panel())
        {
            FixedAction = () => events.Add("later-fixed"),
            FrameAction = _ => events.Add("later-frame")
        };
        var definitions = new Registry<HudDefinition>(RegistryKeys.Hud);
        definitions.Register(ResourceKey.Create("test", "first"), new HudDefinition(() => firstHud));
        definitions.Register(ResourceKey.Create("test", "later"), new HudDefinition(() => laterHud));
        definitions.Freeze();
        var root = new TestNode { MeasureAction = () => events.Add("scene-layout") };
        var scene = new RecordingScene(root)
        {
            FixedAction = () => events.Add("scene-fixed"),
            FrameAction = _ => events.Add("scene-frame")
        };
        var hudTicker = hudRoot.AddTicker(_ => events.Add("hud-ticker"));
        var ticker = root.AddTicker(_ => events.Add("scene-ticker"));
        var manager = new UIManager();
        try
        {
            manager.InitializeHud(definitions);
            manager.Open(scene);
            manager.Hud.Post(() => events.Add("hud-post"));
            scene.Post(() => events.Add("scene-post"));
            hudTicker.Start();
            ticker.Start();

            var actual = fixedUpdate
                ? Assert.Throws<InvalidOperationException>(manager.Update)
                : Assert.Throws<InvalidOperationException>(() =>
                    manager.UpdateFrame(new Size(100, 100), 0.25, TimeSpan.FromSeconds(10)));

            Assert.Same(failure, actual);
            Assert.Equal(fixedUpdate ? new[] { "first-fixed" } : ["first-frame"], events);
            Assert.False(manager.Hud.Scene.IsUpdating);
            Assert.False(scene.IsUpdating);
            Assert.False(hudRoot.IsMeasureValid);
            Assert.False(root.IsMeasureValid);
            Assert.True(hudTicker.IsActive);
            Assert.True(ticker.IsActive);

            shouldThrow = false;
            events.Clear();
            manager.Update();
            Assert.Equal(["first-fixed", "later-fixed", "scene-fixed"], events);
            events.Clear();
            manager.UpdateFrame(new Size(100, 100), 0.5, TimeSpan.FromSeconds(11));
            Assert.Equal(
                [
                    "first-frame", "later-frame", "hud-post", "hud-ticker", "hud-layout",
                    "scene-frame", "scene-post", "scene-ticker", "scene-layout"
                ],
                events);
        }
        finally
        {
            manager.Destroy();
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void UpdateFailureRestoresGuardsAndAllowsCleanup(bool fixedUpdate)
    {
        var manager = new UIManager();
        var failure = new InvalidOperationException("update failed");
        var scene = new RecordingScene(new TestNode())
        {
            FixedAction = () => throw failure,
            FrameAction = _ => throw failure
        };
        manager.Open(scene);

        var actual = fixedUpdate
            ? Assert.Throws<InvalidOperationException>(manager.Update)
            : Assert.Throws<InvalidOperationException>(() => manager.UpdateFrame(new Size(100, 100), 0));

        Assert.Same(failure, actual);
        Assert.Same(scene, manager.CurrentScene);
        manager.Destroy();
        Assert.Null(manager.CurrentScene);
    }

    [Fact]
    public void ManagerRejectsNestedUpdateAndRecordingDuringPost()
    {
        var manager = new UIManager();
        manager.Hud.Post(() =>
        {
            Assert.Throws<InvalidOperationException>(() => manager.Open(new GameUIScene()));
            Assert.Throws<InvalidOperationException>(manager.Close);
            Assert.Throws<InvalidOperationException>(manager.Update);
            Assert.Throws<InvalidOperationException>(manager.Destroy);
            Assert.Throws<InvalidOperationException>(() => manager.UpdateFrame(new Size(10, 10), 0));
            Assert.Throws<InvalidOperationException>(() => manager.AppendDrawCommands(new DrawCommandList()));
        });

        manager.UpdateFrame(new Size(10, 10), 0);
        manager.Destroy();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void UpdateCallbacksRejectSynchronousSceneChangesAndDestruction(bool fixedUpdate)
    {
        var manager = new UIManager();
        var root = new TestNode();
        var replacement = new GameUIScene();
        var calls = 0;

        void Check()
        {
            calls++;
            Assert.Throws<InvalidOperationException>(() => manager.Open(replacement));
            Assert.Throws<InvalidOperationException>(manager.Close);
            Assert.Throws<InvalidOperationException>(manager.Destroy);
        }

        var scene = new RecordingScene(root)
        {
            FixedAction = Check,
            FrameAction = _ => Check()
        };
        manager.Open(scene);

        if (fixedUpdate)
            manager.Update();
        else
            manager.UpdateFrame(new Size(10, 10), 0);

        Assert.Same(scene, manager.CurrentScene);
        Assert.Equal(1, calls);
        Assert.False(replacement.IsOpen());
        Assert.True(scene.IsOpen());
        Assert.Equal(!fixedUpdate, root.IsMeasureValid);
        manager.Destroy();
        Assert.Throws<ObjectDisposedException>(manager.Update);
        Assert.Throws<ObjectDisposedException>(() => manager.AppendDrawCommands(new DrawCommandList()));
        manager.Destroy();
    }

    private static void DrawRectangle(DrawingContext context) =>
        context.FillRectangle(new Rect(0, 0, 10, 10), new Color(255, 255, 255));

    private static void CaptureNestedOperationErrors(
        UIManager manager,
        ICollection<Exception?> errors)
    {
        errors.Add(Record.Exception(() => manager.Open(new GameUIScene(new TestNode()))));
        errors.Add(Record.Exception(manager.Close));
        errors.Add(Record.Exception(manager.Destroy));
        errors.Add(Record.Exception(() => manager.UpdateFrame(new Size(10, 10), 0)));
    }

    private static ResourceKey InitializeHud(UIManager manager, UIElement root)
    {
        var key = ResourceKey.Create("test", "hud");
        var definitions = new Registry<HudDefinition>(RegistryKeys.Hud);
        definitions.Register(key, new HudDefinition(() => new RecordingHud(root)));
        definitions.Freeze();
        manager.InitializeHud(definitions);
        return key;
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

    private sealed class TestNode : UIElement
    {
        internal Size CoreDesiredSize { get; set; }
        internal Size LastMeasureConstraint { get; private set; }
        internal Size LastArrangeSize { get; private set; }
        internal Action? MeasureAction { get; set; }
        internal Action? ArrangeAction { get; set; }
        internal Action<DrawingContext>? DrawAction { get; set; }
        internal int MeasureCalls { get; private set; }
        internal int ArrangeCalls { get; private set; }

        internal Ticker AddTicker(Action<TimeSpan> onTick) => CreateTicker(onTick);

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

        protected override void DrawCore(DrawingContext context) => DrawAction?.Invoke(context);
    }

    private sealed class RecordingHud(UIElement root) : Hud(root)
    {
        internal Action? FixedAction { get; init; }
        internal Action<double>? FrameAction { get; set; }

        protected override void OnFixedUpdate() => FixedAction?.Invoke();
        protected override void OnFrameUpdate(double alpha) => FrameAction?.Invoke(alpha);
    }

    private sealed class RecordingScene(UIElement? root = null) : GameUIScene(root)
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