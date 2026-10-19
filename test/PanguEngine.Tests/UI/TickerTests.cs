using System.Runtime.CompilerServices;
using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Huds;
using PanguEngine.Input;
using PanguEngine.Registries;
using PanguEngine.UI;
using PanguEngine.UI.Controls;

namespace PanguEngine.Tests.UI;

public sealed class TickerTests
{
    private static readonly Size Viewport = new(100, 100);

    [Fact]
    public void CreationDoesNotStartAndRequiresCallback()
    {
        var node = new TickingNode();
        var ticker = node.AddTicker(_ => { });

        Assert.False(ticker.IsActive);
        Assert.Throws<ArgumentNullException>(() => node.AddTicker(null!));
        Assert.Throws<InvalidOperationException>(ticker.Start);
        ticker.Stop();
        var scene = new UIScene(node);
        Assert.Throws<InvalidOperationException>(ticker.Start);
    }

    [Fact]
    public void InactiveTickerIsNotRetainedByItsNode()
    {
        var node = new TickingNode();
        var weakTicker = CreateInactiveTicker(node);

        ForceCollection();

        Assert.False(weakTicker.TryGetTarget(out _));
        GC.KeepAlive(node);
    }

    [Fact]
    public void StoppedTickerIsNotRetainedByItsNodeOrScene()
    {
        var node = new TickingNode();
        var scene = new UIScene(node);
        scene.Open();
        try
        {
            var weakTicker = CreateStoppedTicker(node);
            ForceCollection();
            Assert.False(weakTicker.TryGetTarget(out _));
            GC.KeepAlive(node);
        }
        finally
        {
            scene.Close();
        }
    }

    [Fact]
    public void ActiveTickerRunsWithoutConsumerReferenceAndIsReleasedOnStop()
    {
        var node = new TickingNode();
        var scene = new UIScene(node);
        var times = new List<TimeSpan>();
        scene.Open();
        try
        {
            var weakTicker = CreateStartedTicker(node, times.Add);
            ForceCollection();
            Prepare(scene, 10);
            Assert.Equal([TimeSpan.FromSeconds(10)], times);

            StopTicker(weakTicker);
            ForceCollection();
            Assert.False(weakTicker.TryGetTarget(out _));
            GC.KeepAlive(node);
        }
        finally
        {
            scene.Close();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LifecycleStopReleasesTickerWhileNodeAndSceneRemainAlive(bool close)
    {
        var node = new TickingNode();
        var scene = new UIScene(node);
        scene.Open();
        try
        {
            var weakTicker = CreateStartedTicker(node, static _ => { });
            if (close)
                scene.Close();
            else
                scene.Root = null;

            ForceCollection();
            Assert.False(weakTicker.TryGetTarget(out _));
            GC.KeepAlive(node);
            GC.KeepAlive(scene);
        }
        finally
        {
            scene.Close();
        }
    }

    [Fact]
    public void StartAndStopAreIdempotentAndRestartMovesToEnd()
    {
        var node = new TickingNode();
        var calls = new List<string>();
        var first = node.AddTicker(_ => calls.Add("first"));
        var second = node.AddTicker(_ => calls.Add("second"));
        var scene = new UIScene(node);
        scene.Open();
        try
        {
            first.Start();
            second.Start();
            first.Start();
            Prepare(scene, 10);
            Assert.Equal(["first", "second"], calls);

            calls.Clear();
            first.Stop();
            first.Stop();
            Assert.False(first.IsActive);
            first.Start();
            Prepare(scene, 11);
            Assert.Equal(["second", "first"], calls);
        }
        finally
        {
            scene.Close();
        }
    }

    [Fact]
    public void ConsumerIntegratesExplicitTimeAndResetsBaselineForNewRun()
    {
        var node = new TickingNode();
        TimeSpan? previous = null;
        var distance = 0d;
        var times = new List<TimeSpan>();
        var ticker = node.AddTicker(now =>
        {
            times.Add(now);
            if (previous is { } baseline)
                distance += 100 * (now - baseline).TotalSeconds;
            previous = now;
        });
        var scene = new UIScene(node);
        scene.Open();
        try
        {
            ticker.Start();
            scene.PrepareFrame(Viewport, TimeSpan.FromSeconds(10));
            scene.PrepareFrame(Viewport, TimeSpan.FromSeconds(10.02));
            scene.PrepareFrame(Viewport, TimeSpan.FromSeconds(10.05));
            Assert.Equal(5, distance, 8);
            Assert.Equal([
                TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10.02),
                TimeSpan.FromSeconds(10.05)
            ], times);

            ticker.Stop();
            previous = null;
            ticker.Start();
            Prepare(scene, 20);
            Prepare(scene, 20.01);
            Assert.Equal(6, distance, 8);
        }
        finally
        {
            scene.Close();
        }
    }

    [Fact]
    public void EqualTimestampsStillRepresentSeparateFrames()
    {
        var node = new TickingNode();
        var calls = 0;
        var ticker = node.AddTicker(_ => calls++);
        var scene = new UIScene(node);
        scene.Open();
        try
        {
            ticker.Start();
            Prepare(scene, 10);
            Prepare(scene, 10);
            Assert.Equal(2, calls);
        }
        finally
        {
            scene.Close();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StoppingPendingRunSkipsItEvenIfRestarted(bool restart)
    {
        var node = new TickingNode();
        var calls = new List<string>();
        var second = node.AddTicker(_ => calls.Add("second"));
        var first = node.AddTicker(_ =>
        {
            calls.Add("first");
            second.Stop();
            if (restart)
                second.Start();
        });
        var scene = new UIScene(node);
        scene.Open();
        try
        {
            first.Start();
            second.Start();
            Prepare(scene, 10);
            Assert.Equal(["first"], calls);
            first.Stop();
            Prepare(scene, 11);
            Assert.Equal(restart ? ["first", "second"] : new[] { "first" }, calls);
        }
        finally
        {
            scene.Close();
        }
    }

    [Fact]
    public void CallbackStartsNewTaskAndRestartsItselfOnlyForNextFrame()
    {
        var node = new TickingNode();
        var calls = new List<string>();
        var second = node.AddTicker(_ => calls.Add("second"));
        Ticker first = null!;
        first = node.AddTicker(_ =>
        {
            calls.Add("first:begin");
            first.Stop();
            second.Start();
            first.Start();
            calls.Add("first:end");
        });
        var scene = new UIScene(node);
        scene.Open();
        try
        {
            first.Start();
            Prepare(scene, 10);
            Assert.Equal(["first:begin", "first:end"], calls);
            calls.Clear();
            Prepare(scene, 11);
            Assert.Equal(["second", "first:begin", "first:end"], calls);
        }
        finally
        {
            scene.Close();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TickersStartedByFrameUpdatesRunImmediatelyAndPostsWaitUntilNextFrame(bool fromPost)
    {
        var node = new TickingNode();
        var calls = 0;
        var ticker = node.AddTicker(_ => calls++);
        var scene = new CallbackScene(node);
        scene.Open();
        try
        {
            if (fromPost)
                scene.Post(ticker.Start);
            else
                scene.FrameAction = ticker.Start;
            Prepare(scene, 10);
            Assert.Equal(fromPost ? 0 : 1, calls);
            Prepare(scene, 11);
            Assert.Equal(fromPost ? 1 : 2, calls);
        }
        finally
        {
            scene.Close();
        }
    }

    [Fact]
    public void TickerStartedByPostFromFrameUpdateWaitsUntilNextFrame()
    {
        var node = new TickingNode();
        var calls = new List<TimeSpan>();
        var ticker = node.AddTicker(calls.Add);
        var scene = new CallbackScene(node);
        scene.FrameAction = () => scene.Post(ticker.Start);
        scene.Open();
        try
        {
            Prepare(scene, 10);
            Assert.True(ticker.IsActive);
            Assert.Empty(calls);

            Prepare(scene, 11);
            Assert.Equal([TimeSpan.FromSeconds(11)], calls);
        }
        finally
        {
            scene.Close();
        }
    }

    [Fact]
    public void HudAndSceneShareTimeAndUseStableSceneOrder()
    {
        var manager = new UIManager();
        var hudNode = MountHudNode(manager);
        var sceneNode = new TickingNode();
        var scene = new GameUIScene(sceneNode);
        manager.Open(scene);
        var calls = new List<(string Name, TimeSpan Time)>();
        var regular = sceneNode.AddTicker(now => calls.Add(("scene", now)));
        var hud = hudNode.AddTicker(now => calls.Add(("hud", now)));
        try
        {
            regular.Start();
            hud.Start();
            Prepare(manager, 10);
            Assert.Equal([("hud", TimeSpan.FromSeconds(10)), ("scene", TimeSpan.FromSeconds(10))], calls);
        }
        finally
        {
            manager.Destroy();
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void CrossSceneStartAndRestartFollowTargetPreparationOrder(bool fromHud, bool restart)
    {
        var manager = new UIManager();
        var hudNode = MountHudNode(manager);
        var sceneNode = new TickingNode();
        manager.Open(new GameUIScene(sceneNode));
        var source = fromHud ? hudNode : sceneNode;
        var target = fromHud ? sceneNode : hudNode;
        var calls = new List<TimeSpan>();
        var targetTicker = target.AddTicker(calls.Add);
        var sourceTicker = source.AddTicker(_ =>
        {
            if (restart)
                targetTicker.Stop();
            targetTicker.Start();
        });
        try
        {
            if (restart)
                targetTicker.Start();
            sourceTicker.Start();
            Prepare(manager, 10);
            Assert.Equal(fromHud || restart ? new[] { TimeSpan.FromSeconds(10) } : [], calls);
            calls.Clear();
            sourceTicker.Stop();
            Prepare(manager, 11);
            Assert.Equal([TimeSpan.FromSeconds(11)], calls);
        }
        finally
        {
            manager.Destroy();
        }
    }

    [Fact]
    public void HudExceptionStopsRemainingWorkAndAllowsNextFrame()
    {
        var manager = new UIManager();
        var hudNode = MountHudNode(manager);
        var sceneNode = new TickingNode();
        var scene = new GameUIScene(sceneNode);
        manager.Open(scene);
        var expected = new InvalidOperationException("tick failed");
        var failure = hudNode.AddTicker(_ => throw expected);
        var calls = 0;
        var laterHud = hudNode.AddTicker(_ => calls++);
        var regular = sceneNode.AddTicker(_ => calls++);
        try
        {
            failure.Start();
            laterHud.Start();
            regular.Start();
            Assert.Same(expected, Assert.Throws<InvalidOperationException>(() => Prepare(manager, 10)));
            Assert.True(failure.IsActive);
            Assert.Equal(0, calls);
            Assert.False(sceneNode.IsArrangeValid);
            failure.Stop();
            Prepare(manager.Hud.Scene, 11);
            Prepare(scene, 11);
            Assert.Equal(2, calls);
            Prepare(manager, 12);
            Assert.Equal(4, calls);
        }
        finally
        {
            manager.Destroy();
        }
    }

    [Fact]
    public void TickerRunsAfterFrameUpdateAndBeforeAllLayoutRetries()
    {
        var events = new List<string>();
        var node = new MeasuringNode(events);
        var scene = new CallbackScene(node) { FrameAction = () => events.Add("frame") };
        var ticker = node.AddTicker(_ =>
        {
            events.Add("tick");
            node.Width = 40;
        });
        scene.Open();
        try
        {
            scene.Post(() => events.Add("post"));
            ticker.Start();
            Prepare(scene, 10);
            Assert.Equal(["frame", "post", "tick", "measure", "measure", "measure"], events);
            Assert.Equal(40, node.LayoutBounds.Width);
            Assert.True(node.IsArrangeValid);
        }
        finally
        {
            scene.Close();
        }
    }

    [Fact]
    public void OpeningCanStartTickerBeforeInteractionActivation()
    {
        var node = new TickingNode();
        var calls = 0;
        var ticker = node.AddTicker(_ => calls++);
        var scene = new CallbackScene(node) { OpeningAction = ticker.Start };
        scene.Open();
        try
        {
            Assert.True(ticker.IsActive);
            Prepare(scene, 10);
            Assert.Equal(1, calls);
        }
        finally
        {
            scene.Close();
        }
    }

    [Fact]
    public void WrongThreadCannotStartOrStopAnActiveRun()
    {
        var node = new TickingNode();
        var ticker = node.AddTicker(_ => { });
        var scene = new UIScene(node);
        scene.Open();
        try
        {
            Assert.IsType<InvalidOperationException>(OnOtherThread(ticker.Start));
            ticker.Start();
            Assert.IsType<InvalidOperationException>(OnOtherThread(ticker.Start));
            Assert.IsType<InvalidOperationException>(OnOtherThread(ticker.Stop));
            Assert.True(ticker.IsActive);
        }
        finally
        {
            scene.Close();
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void DetachAndTransferStopSubtreeRunsWithoutRestoringThem(int transfer)
    {
        var root = new Panel();
        var firstParent = new Panel();
        var secondParent = new Panel();
        var node = new TickingNode();
        var child = new TickingNode();
        node.Children.Add(child);
        firstParent.Children.Add(node);
        root.Children.Add(firstParent);
        root.Children.Add(secondParent);
        var scene = new UIScene(root);
        var otherRoot = new Panel();
        var otherScene = new UIScene(otherRoot);
        var calls = 0;
        var ticker = node.AddTicker(_ => calls++);
        var childTicker = child.AddTicker(_ => calls++);
        scene.Open();
        otherScene.Open();
        try
        {
            ticker.Start();
            childTicker.Start();
            switch (transfer)
            {
                case 0: firstParent.Children.Remove(node); break;
                case 1: secondParent.Children.Add(node); break;
                case 2: otherRoot.Children.Add(node); break;
                case 3: scene.Root = new Panel(); break;
                case 4: otherScene.Root = root; break;
            }

            Assert.False(ticker.IsActive);
            Assert.False(childTicker.IsActive);
            if (transfer == 3)
                scene.Root = root;
            else if (node.Scene is null)
                firstParent.Children.Add(node);
            Prepare(scene, 10);
            Prepare(otherScene, 10);
            Assert.Equal(0, calls);
            ticker.Start();
            Prepare(node.Scene!, 11);
            Assert.Equal(1, calls);
        }
        finally
        {
            otherScene.Close();
            scene.Close();
        }
    }

    [Fact]
    public void SiblingReorderingDoesNotStopTicker()
    {
        var root = new Panel();
        var node = new TickingNode();
        root.Children.Add(node);
        root.Children.Add(new Panel());
        var calls = 0;
        var ticker = node.AddTicker(_ => calls++);
        var scene = new UIScene(root);
        scene.Open();
        try
        {
            ticker.Start();
            node.MoveToFront();
            node.MoveToBack();
            Assert.True(ticker.IsActive);
            Prepare(scene, 10);
            Assert.Equal(1, calls);
        }
        finally
        {
            scene.Close();
        }
    }

    [Fact]
    public void DetachingDuringCallbackLetsItReturnAndSkipsOtherOwnedRuns()
    {
        var root = new Panel();
        var node = new TickingNode();
        root.Children.Add(node);
        var calls = new List<string>();
        var first = node.AddTicker(_ =>
        {
            calls.Add("begin");
            root.Children.Remove(node);
            calls.Add("end");
        });
        var second = node.AddTicker(_ => calls.Add("second"));
        var scene = new UIScene(root);
        scene.Open();
        try
        {
            first.Start();
            second.Start();
            Prepare(scene, 10);
            Assert.Equal(["begin", "end"], calls);
            Assert.False(first.IsActive);
            Assert.False(second.IsActive);
        }
        finally
        {
            scene.Close();
        }
    }

    [Fact]
    public void CloseStopsBeforeCallbacksAndPreservesPointerCanceledBusinessCleanup()
    {
        var node = new TickingNode();
        var ticker = node.AddTicker(_ => { });
        var events = new List<string>();
        var scene = new CallbackScene(node)
        {
            ClosingAction = () =>
            {
                events.Add("closing");
                Assert.False(ticker.IsActive);
                Assert.Throws<InvalidOperationException>(ticker.Start);
            }
        };
        node.PointerCanceled += (_, _) =>
        {
            Assert.False(ticker.IsActive);
            ticker.Stop();
            events.Add("commit");
        };
        scene.Open();
        Prepare(scene, 10);
        scene.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        ticker.Start();
        scene.Close();
        Assert.Equal(["closing", "commit"], events);
        Assert.Same(scene, node.Scene);
        scene.Open();
        try
        {
            Assert.False(ticker.IsActive);
        }
        finally
        {
            scene.Close();
        }
    }

    [Fact]
    public void ClosingFailureAlreadyStoppedAllRuns()
    {
        var node = new TickingNode();
        var ticker = node.AddTicker(_ => { });
        var expected = new InvalidOperationException("closing failed");
        var scene = new CallbackScene(node) { ClosingAction = () => throw expected };
        scene.Open();
        ticker.Start();
        Assert.Same(expected, Assert.Throws<InvalidOperationException>(scene.Close));
        Assert.False(ticker.IsActive);
        Assert.Throws<InvalidOperationException>(ticker.Start);
        scene.ClosingAction = null;
        scene.Close();
    }

    [Fact]
    public void ClosedTreeCanRemoveStoppedTickerOnAnotherThread()
    {
        var root = new Panel();
        var node = new TickingNode();
        root.Children.Add(node);
        var scene = new UIScene(root);
        var ticker = node.AddTicker(_ => { });
        scene.Open();
        ticker.Start();
        scene.Close();

        Assert.Null(OnOtherThread(() =>
        {
            ticker.Stop();
            root.Children.Remove(node);
        }));
        Assert.Null(node.Scene);
        Assert.False(ticker.IsActive);
    }

    [Fact]
    public void HudStopsBeforeDestroyCallbackEvenWhenItThrows()
    {
        var node = new TickingNode();
        var ticker = node.AddTicker(_ => { });
        var expected = new InvalidOperationException("destroy failed");
        var hud = new CallbackHud(node, () =>
        {
            Assert.False(ticker.IsActive);
            Assert.Throws<InvalidOperationException>(ticker.Start);
            throw expected;
        });
        var definitions = new Registry<HudDefinition>(RegistryKeys.Hud);
        definitions.Register(ResourceKey.Create("test", "ticker"), new HudDefinition(() => hud));
        definitions.Freeze();
        var manager = new UIManager();
        manager.InitializeHud(definitions);
        ticker.Start();

        Assert.Same(expected, Assert.Throws<InvalidOperationException>(manager.Destroy));
        Assert.False(ticker.IsActive);
        manager.Destroy();
    }

    [Fact]
    public void FocusLossVisibilityAndEnabledStateDoNotAutomaticallyStopTicker()
    {
        var node = new TickingNode();
        var calls = 0;
        var ticker = node.AddTicker(_ => calls++);
        var scene = new UIScene(node);
        scene.Open();
        try
        {
            ticker.Start();
            scene.ProcessFocusChanged(false);
            node.Visibility = Visibility.Hidden;
            node.IsEnabled = false;
            Prepare(scene, 10);
            Assert.True(ticker.IsActive);
            Assert.Equal(1, calls);
        }
        finally
        {
            scene.Close();
        }
    }

    [Fact]
    public void OwnershipNotificationFailureLeavesUnchangedDescendantTickerActive()
    {
        var root = new TickingNode();
        var child = new TickingNode();
        root.Children.Add(child);
        var source = new UIScene(root);
        var target = new UIScene();
        var expected = new InvalidOperationException("ownership notification failed");
        var calls = new List<string>();
        var rootTicker = root.AddTicker(_ => calls.Add("root"));
        var childTicker = child.AddTicker(_ => calls.Add("child"));
        root.PropertyChanged += (_, args) =>
        {
            if (args.Property == UIElement.SceneProperty && ReferenceEquals(root.Scene, target))
                throw expected;
        };
        source.Open();
        target.Open();
        try
        {
            rootTicker.Start();
            childTicker.Start();
            Assert.Same(expected, Assert.Throws<InvalidOperationException>(() => target.Root = root));
            Assert.Same(target, root.Scene);
            Assert.Same(source, child.Scene);
            Assert.False(rootTicker.IsActive);
            Assert.True(childTicker.IsActive);
            Prepare(source, 10);
            Assert.Equal(["child"], calls);
        }
        finally
        {
            target.Close();
            source.Close();
        }
    }

    [Fact]
    public void ChildRemovedDuringOwnershipNotificationStopsItsTicker()
    {
        var root = new Panel();
        var first = new TickingNode();
        var second = new TickingNode();
        root.Children.Add(first);
        root.Children.Add(second);
        var source = new UIScene(root);
        var target = new UIScene();
        var ticker = second.AddTicker(_ => { });
        first.PropertyChanged += (_, args) =>
        {
            if (args.Property == UIElement.SceneProperty && ReferenceEquals(first.Scene, target))
                root.Children.Remove(second);
        };
        source.Open();
        target.Open();
        try
        {
            ticker.Start();
            Assert.Throws<InvalidOperationException>(() => target.Root = root);
            Assert.Null(second.Scene);
            Assert.Null(second.Parent);
            Assert.False(ticker.IsActive);
            Assert.Same(target, first.Scene);
        }
        finally
        {
            target.Close();
            source.Close();
        }
    }

    [Fact]
    public void IndependentSceneExceptionAllowsNextPreparation()
    {
        var node = new TickingNode();
        var scene = new UIScene(node);
        var expected = new InvalidOperationException("independent tick failed");
        var ticker = node.AddTicker(_ => throw expected);
        scene.Open();
        try
        {
            ticker.Start();
            Assert.Same(expected, Assert.Throws<InvalidOperationException>(() => Prepare(scene, 10)));
            ticker.Stop();
            Prepare(scene, 11);
            Assert.True(node.IsArrangeValid);
        }
        finally
        {
            scene.Close();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StartUsesSceneOwnershipDuringParentNotification(bool startedBeforeRemoval)
    {
        var root = new Panel();
        var node = new TickingNode();
        var child = new TickingNode();
        node.Children.Add(child);
        root.Children.Add(node);
        var scene = new UIScene(root);
        var ticker = node.AddTicker(_ => { });
        var childTicker = child.AddTicker(_ => { });
        var notifications = new List<string>();
        node.PropertyChanged += (_, args) =>
        {
            if (args.Property == UIElement.ParentProperty && node.Parent is null)
            {
                notifications.Add("parent");
                Assert.Same(scene, node.Scene);
                Assert.Equal(startedBeforeRemoval, ticker.IsActive);
                Assert.True(childTicker.IsActive);
                ticker.Start();
                Assert.True(ticker.IsActive);
            }

            if (args.Property == UIElement.SceneProperty && node.Scene is null)
            {
                notifications.Add("node:scene");
                Assert.False(ticker.IsActive);
                Assert.Same(scene, child.Scene);
                Assert.True(childTicker.IsActive);
            }
        };
        child.PropertyChanged += (_, args) =>
        {
            if (args.Property == UIElement.SceneProperty && child.Scene is null)
            {
                notifications.Add("child:scene");
                Assert.False(ticker.IsActive);
                Assert.False(childTicker.IsActive);
            }
        };
        scene.Open();
        try
        {
            if (startedBeforeRemoval)
                ticker.Start();
            childTicker.Start();
            root.Children.Remove(node);
            Assert.Equal(["parent", "node:scene", "child:scene"], notifications);
            Assert.Null(node.Scene);
            Assert.Null(child.Scene);
            Assert.False(ticker.IsActive);
            Assert.False(childTicker.IsActive);
        }
        finally
        {
            scene.Close();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<Ticker> CreateInactiveTicker(TickingNode node) =>
        new(node.AddTicker(static _ => { }));

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<Ticker> CreateStoppedTicker(TickingNode node)
    {
        var ticker = node.AddTicker(static _ => { });
        ticker.Start();
        ticker.Stop();
        return new WeakReference<Ticker>(ticker);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<Ticker> CreateStartedTicker(TickingNode node, Action<TimeSpan> onTick)
    {
        var ticker = node.AddTicker(onTick);
        ticker.Start();
        return new WeakReference<Ticker>(ticker);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void StopTicker(WeakReference<Ticker> weakTicker)
    {
        Assert.True(weakTicker.TryGetTarget(out var ticker));
        ticker!.Stop();
    }

    private static void ForceCollection()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private static void Prepare(UIScene scene, double seconds) =>
        scene.PrepareFrame(Viewport, TimeSpan.FromSeconds(seconds));

    private static void Prepare(GameUIScene scene, double seconds)
    {
        scene.UpdateFrame(0);
        scene.PrepareFrame(Viewport, TimeSpan.FromSeconds(seconds));
    }

    private static void Prepare(UIManager manager, double seconds) =>
        manager.UpdateFrame(Viewport, 0, TimeSpan.FromSeconds(seconds));

    private static TickingNode MountHudNode(UIManager manager)
    {
        var node = new TickingNode();
        Assert.IsType<Panel>(manager.Hud.Scene.Root).Children.Add(node);
        return node;
    }

    private static Exception? OnOtherThread(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() => error = Record.Exception(action));
        thread.Start();
        thread.Join();
        return error;
    }

    private class TickingNode : Panel
    {
        internal Ticker AddTicker(Action<TimeSpan> onTick) => CreateTicker(onTick);
    }

    private sealed class MeasuringNode(List<string> events) : TickingNode
    {
        private int _measures;

        protected override Size MeasureContent(Size availableSize)
        {
            events.Add("measure");
            if (_measures++ < 2)
                InvalidateMeasure();
            return new Size(40, 30);
        }
    }

    private sealed class CallbackScene(UIElement root) : GameUIScene(root)
    {
        internal Action? OpeningAction { get; init; }
        internal Action? FrameAction { get; set; }
        internal Action? ClosingAction { get; set; }

        protected override void OnOpening() => OpeningAction?.Invoke();
        protected override void OnFrameUpdate(double alpha) => FrameAction?.Invoke();
        protected override void OnClosing() => ClosingAction?.Invoke();
    }

    private sealed class CallbackHud(UIElement root, Action destroy) : Hud(root)
    {
        protected override void OnDestroy() => destroy();
    }
}