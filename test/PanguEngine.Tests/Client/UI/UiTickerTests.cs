using System.Runtime.CompilerServices;
using PanguEngine.Client.Huds;
using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Input;
using PanguEngine.Registries;

namespace PanguEngine.Tests.Client.UI;

public sealed class UiTickerTests
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
        var screen = new UiScreen(node);
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
    public void StoppedTickerIsNotRetainedByItsNodeOrScreen()
    {
        var node = new TickingNode();
        var screen = new UiScreen(node);
        screen.Open();
        try
        {
            var weakTicker = CreateStoppedTicker(node);
            ForceCollection();
            Assert.False(weakTicker.TryGetTarget(out _));
            GC.KeepAlive(node);
        }
        finally
        {
            screen.Close();
        }
    }

    [Fact]
    public void ActiveTickerRunsWithoutConsumerReferenceAndIsReleasedOnStop()
    {
        var node = new TickingNode();
        var screen = new UiScreen(node);
        var times = new List<TimeSpan>();
        screen.Open();
        try
        {
            var weakTicker = CreateStartedTicker(node, times.Add);
            ForceCollection();
            Prepare(screen, 10);
            Assert.Equal([TimeSpan.FromSeconds(10)], times);

            StopTicker(weakTicker);
            ForceCollection();
            Assert.False(weakTicker.TryGetTarget(out _));
            GC.KeepAlive(node);
        }
        finally
        {
            screen.Close();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LifecycleStopReleasesTickerWhileNodeAndScreenRemainAlive(bool close)
    {
        var node = new TickingNode();
        var screen = new UiScreen(node);
        screen.Open();
        try
        {
            var weakTicker = CreateStartedTicker(node, static _ => { });
            if (close)
                screen.Close();
            else
                screen.Root = null;

            ForceCollection();
            Assert.False(weakTicker.TryGetTarget(out _));
            GC.KeepAlive(node);
            GC.KeepAlive(screen);
        }
        finally
        {
            screen.Close();
        }
    }

    [Fact]
    public void StartAndStopAreIdempotentAndRestartMovesToEnd()
    {
        var node = new TickingNode();
        var calls = new List<string>();
        var first = node.AddTicker(_ => calls.Add("first"));
        var second = node.AddTicker(_ => calls.Add("second"));
        var screen = new UiScreen(node);
        screen.Open();
        try
        {
            first.Start();
            second.Start();
            first.Start();
            Prepare(screen, 10);
            Assert.Equal(["first", "second"], calls);

            calls.Clear();
            first.Stop();
            first.Stop();
            Assert.False(first.IsActive);
            first.Start();
            Prepare(screen, 11);
            Assert.Equal(["second", "first"], calls);
        }
        finally
        {
            screen.Close();
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
        var screen = new UiScreen(node);
        screen.Open();
        try
        {
            ticker.Start();
            screen.PrepareFrame(Viewport, TimeSpan.FromSeconds(10));
            screen.PrepareFrame(Viewport, TimeSpan.FromSeconds(10.02));
            screen.PrepareFrame(Viewport, TimeSpan.FromSeconds(10.05));
            Assert.Equal(5, distance, 8);
            Assert.Equal([
                TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10.02),
                TimeSpan.FromSeconds(10.05)
            ], times);

            ticker.Stop();
            previous = null;
            ticker.Start();
            Prepare(screen, 20);
            Prepare(screen, 20.01);
            Assert.Equal(6, distance, 8);
        }
        finally
        {
            screen.Close();
        }
    }

    [Fact]
    public void EqualTimestampsStillRepresentSeparateFrames()
    {
        var node = new TickingNode();
        var calls = 0;
        var ticker = node.AddTicker(_ => calls++);
        var screen = new UiScreen(node);
        screen.Open();
        try
        {
            ticker.Start();
            Prepare(screen, 10);
            Prepare(screen, 10);
            Assert.Equal(2, calls);
        }
        finally
        {
            screen.Close();
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
        var screen = new UiScreen(node);
        screen.Open();
        try
        {
            first.Start();
            second.Start();
            Prepare(screen, 10);
            Assert.Equal(["first"], calls);
            first.Stop();
            Prepare(screen, 11);
            Assert.Equal(restart ? ["first", "second"] : new[] { "first" }, calls);
        }
        finally
        {
            screen.Close();
        }
    }

    [Fact]
    public void CallbackStartsNewTaskAndRestartsItselfOnlyForNextFrame()
    {
        var node = new TickingNode();
        var calls = new List<string>();
        var second = node.AddTicker(_ => calls.Add("second"));
        UiTicker first = null!;
        first = node.AddTicker(_ =>
        {
            calls.Add("first:begin");
            first.Stop();
            second.Start();
            first.Start();
            calls.Add("first:end");
        });
        var screen = new UiScreen(node);
        screen.Open();
        try
        {
            first.Start();
            Prepare(screen, 10);
            Assert.Equal(["first:begin", "first:end"], calls);
            calls.Clear();
            Prepare(screen, 11);
            Assert.Equal(["second", "first:begin", "first:end"], calls);
        }
        finally
        {
            screen.Close();
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
        var screen = new CallbackScreen(node);
        screen.Open();
        try
        {
            if (fromPost)
                screen.Post(ticker.Start);
            else
                screen.FrameAction = ticker.Start;
            Prepare(screen, 10);
            Assert.Equal(fromPost ? 0 : 1, calls);
            Prepare(screen, 11);
            Assert.Equal(fromPost ? 1 : 2, calls);
        }
        finally
        {
            screen.Close();
        }
    }

    [Fact]
    public void TickerStartedByPostFromFrameUpdateWaitsUntilNextFrame()
    {
        var node = new TickingNode();
        var calls = new List<TimeSpan>();
        var ticker = node.AddTicker(calls.Add);
        var screen = new CallbackScreen(node);
        screen.FrameAction = () => screen.Post(ticker.Start);
        screen.Open();
        try
        {
            Prepare(screen, 10);
            Assert.True(ticker.IsActive);
            Assert.Empty(calls);

            Prepare(screen, 11);
            Assert.Equal([TimeSpan.FromSeconds(11)], calls);
        }
        finally
        {
            screen.Close();
        }
    }

    [Fact]
    public void HudAndScreenShareTimeAndUseStableScreenOrder()
    {
        var manager = new UiManager();
        var hudNode = MountHudNode(manager);
        var screenNode = new TickingNode();
        var screen = new GameScreen(screenNode);
        manager.Open(screen);
        var calls = new List<(string Name, TimeSpan Time)>();
        var regular = screenNode.AddTicker(now => calls.Add(("screen", now)));
        var hud = hudNode.AddTicker(now => calls.Add(("hud", now)));
        try
        {
            regular.Start();
            hud.Start();
            Prepare(manager, 10);
            Assert.Equal([("hud", TimeSpan.FromSeconds(10)), ("screen", TimeSpan.FromSeconds(10))], calls);
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
    public void CrossScreenStartAndRestartFollowTargetPreparationOrder(bool fromHud, bool restart)
    {
        var manager = new UiManager();
        var hudNode = MountHudNode(manager);
        var screenNode = new TickingNode();
        manager.Open(new GameScreen(screenNode));
        var source = fromHud ? hudNode : screenNode;
        var target = fromHud ? screenNode : hudNode;
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
        var manager = new UiManager();
        var hudNode = MountHudNode(manager);
        var screenNode = new TickingNode();
        var screen = new GameScreen(screenNode);
        manager.Open(screen);
        var expected = new InvalidOperationException("tick failed");
        var failure = hudNode.AddTicker(_ => throw expected);
        var calls = 0;
        var laterHud = hudNode.AddTicker(_ => calls++);
        var regular = screenNode.AddTicker(_ => calls++);
        try
        {
            failure.Start();
            laterHud.Start();
            regular.Start();
            Assert.Same(expected, Assert.Throws<InvalidOperationException>(() => Prepare(manager, 10)));
            Assert.True(failure.IsActive);
            Assert.Equal(0, calls);
            Assert.False(screenNode.IsArrangeValid);
            failure.Stop();
            Prepare(manager.Hud.Screen, 11);
            Prepare(screen, 11);
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
        var screen = new CallbackScreen(node) { FrameAction = () => events.Add("frame") };
        var ticker = node.AddTicker(_ =>
        {
            events.Add("tick");
            node.Width = 40;
        });
        screen.Open();
        try
        {
            screen.Post(() => events.Add("post"));
            ticker.Start();
            Prepare(screen, 10);
            Assert.Equal(["frame", "post", "tick", "measure", "measure", "measure"], events);
            Assert.Equal(40, node.LayoutBounds.Width);
            Assert.True(node.IsArrangeValid);
        }
        finally
        {
            screen.Close();
        }
    }

    [Fact]
    public void OpeningCanStartTickerBeforeInteractionActivation()
    {
        var node = new TickingNode();
        var calls = 0;
        var ticker = node.AddTicker(_ => calls++);
        var screen = new CallbackScreen(node) { OpeningAction = ticker.Start };
        screen.Open();
        try
        {
            Assert.True(ticker.IsActive);
            Prepare(screen, 10);
            Assert.Equal(1, calls);
        }
        finally
        {
            screen.Close();
        }
    }

    [Fact]
    public void WrongThreadCannotStartOrStopAnActiveRun()
    {
        var node = new TickingNode();
        var ticker = node.AddTicker(_ => { });
        var screen = new UiScreen(node);
        screen.Open();
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
            screen.Close();
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
        var screen = new UiScreen(root);
        var otherRoot = new Panel();
        var otherScreen = new UiScreen(otherRoot);
        var calls = 0;
        var ticker = node.AddTicker(_ => calls++);
        var childTicker = child.AddTicker(_ => calls++);
        screen.Open();
        otherScreen.Open();
        try
        {
            ticker.Start();
            childTicker.Start();
            switch (transfer)
            {
                case 0: firstParent.Children.Remove(node); break;
                case 1: secondParent.Children.Add(node); break;
                case 2: otherRoot.Children.Add(node); break;
                case 3: screen.Root = new Panel(); break;
                case 4: otherScreen.Root = root; break;
            }

            Assert.False(ticker.IsActive);
            Assert.False(childTicker.IsActive);
            if (transfer == 3)
                screen.Root = root;
            else if (node.Screen is null)
                firstParent.Children.Add(node);
            Prepare(screen, 10);
            Prepare(otherScreen, 10);
            Assert.Equal(0, calls);
            ticker.Start();
            Prepare(node.Screen!, 11);
            Assert.Equal(1, calls);
        }
        finally
        {
            otherScreen.Close();
            screen.Close();
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
        var screen = new UiScreen(root);
        screen.Open();
        try
        {
            ticker.Start();
            node.MoveToFront();
            node.MoveToBack();
            Assert.True(ticker.IsActive);
            Prepare(screen, 10);
            Assert.Equal(1, calls);
        }
        finally
        {
            screen.Close();
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
        var screen = new UiScreen(root);
        screen.Open();
        try
        {
            first.Start();
            second.Start();
            Prepare(screen, 10);
            Assert.Equal(["begin", "end"], calls);
            Assert.False(first.IsActive);
            Assert.False(second.IsActive);
        }
        finally
        {
            screen.Close();
        }
    }

    [Fact]
    public void CloseStopsBeforeCallbacksAndPreservesPointerCanceledBusinessCleanup()
    {
        var node = new TickingNode();
        var ticker = node.AddTicker(_ => { });
        var events = new List<string>();
        var screen = new CallbackScreen(node)
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
        screen.Open();
        Prepare(screen, 10);
        screen.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        ticker.Start();
        screen.Close();
        Assert.Equal(["closing", "commit"], events);
        Assert.Same(screen, node.Screen);
        screen.Open();
        try
        {
            Assert.False(ticker.IsActive);
        }
        finally
        {
            screen.Close();
        }
    }

    [Fact]
    public void ClosingFailureAlreadyStoppedAllRuns()
    {
        var node = new TickingNode();
        var ticker = node.AddTicker(_ => { });
        var expected = new InvalidOperationException("closing failed");
        var screen = new CallbackScreen(node) { ClosingAction = () => throw expected };
        screen.Open();
        ticker.Start();
        Assert.Same(expected, Assert.Throws<InvalidOperationException>(screen.Close));
        Assert.False(ticker.IsActive);
        Assert.Throws<InvalidOperationException>(ticker.Start);
        screen.ClosingAction = null;
        screen.Close();
    }

    [Fact]
    public void ClosedTreeCanRemoveStoppedTickerOnAnotherThread()
    {
        var root = new Panel();
        var node = new TickingNode();
        root.Children.Add(node);
        var screen = new UiScreen(root);
        var ticker = node.AddTicker(_ => { });
        screen.Open();
        ticker.Start();
        screen.Close();

        Assert.Null(OnOtherThread(() =>
        {
            ticker.Stop();
            root.Children.Remove(node);
        }));
        Assert.Null(node.Screen);
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
        var manager = new UiManager();
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
        var screen = new UiScreen(node);
        screen.Open();
        try
        {
            ticker.Start();
            screen.ProcessFocusChanged(false);
            node.Visibility = Visibility.Hidden;
            node.IsEnabled = false;
            Prepare(screen, 10);
            Assert.True(ticker.IsActive);
            Assert.Equal(1, calls);
        }
        finally
        {
            screen.Close();
        }
    }

    [Fact]
    public void OwnershipNotificationFailureLeavesUnchangedDescendantTickerActive()
    {
        var root = new TickingNode();
        var child = new TickingNode();
        root.Children.Add(child);
        var source = new UiScreen(root);
        var target = new UiScreen();
        var expected = new InvalidOperationException("ownership notification failed");
        var calls = new List<string>();
        var rootTicker = root.AddTicker(_ => calls.Add("root"));
        var childTicker = child.AddTicker(_ => calls.Add("child"));
        root.PropertyChanged += (_, args) =>
        {
            if (args.Property == UiNode.ScreenProperty && ReferenceEquals(root.Screen, target))
                throw expected;
        };
        source.Open();
        target.Open();
        try
        {
            rootTicker.Start();
            childTicker.Start();
            Assert.Same(expected, Assert.Throws<InvalidOperationException>(() => target.Root = root));
            Assert.Same(target, root.Screen);
            Assert.Same(source, child.Screen);
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
        var source = new UiScreen(root);
        var target = new UiScreen();
        var ticker = second.AddTicker(_ => { });
        first.PropertyChanged += (_, args) =>
        {
            if (args.Property == UiNode.ScreenProperty && ReferenceEquals(first.Screen, target))
                root.Children.Remove(second);
        };
        source.Open();
        target.Open();
        try
        {
            ticker.Start();
            Assert.Throws<InvalidOperationException>(() => target.Root = root);
            Assert.Null(second.Screen);
            Assert.Null(second.Parent);
            Assert.False(ticker.IsActive);
            Assert.Same(target, first.Screen);
        }
        finally
        {
            target.Close();
            source.Close();
        }
    }

    [Fact]
    public void IndependentScreenExceptionAllowsNextPreparation()
    {
        var node = new TickingNode();
        var screen = new UiScreen(node);
        var expected = new InvalidOperationException("independent tick failed");
        var ticker = node.AddTicker(_ => throw expected);
        screen.Open();
        try
        {
            ticker.Start();
            Assert.Same(expected, Assert.Throws<InvalidOperationException>(() => Prepare(screen, 10)));
            ticker.Stop();
            Prepare(screen, 11);
            Assert.True(node.IsArrangeValid);
        }
        finally
        {
            screen.Close();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StartUsesScreenOwnershipDuringParentNotification(bool startedBeforeRemoval)
    {
        var root = new Panel();
        var node = new TickingNode();
        var child = new TickingNode();
        node.Children.Add(child);
        root.Children.Add(node);
        var screen = new UiScreen(root);
        var ticker = node.AddTicker(_ => { });
        var childTicker = child.AddTicker(_ => { });
        var notifications = new List<string>();
        node.PropertyChanged += (_, args) =>
        {
            if (args.Property == UiNode.ParentProperty && node.Parent is null)
            {
                notifications.Add("parent");
                Assert.Same(screen, node.Screen);
                Assert.Equal(startedBeforeRemoval, ticker.IsActive);
                Assert.True(childTicker.IsActive);
                ticker.Start();
                Assert.True(ticker.IsActive);
            }

            if (args.Property == UiNode.ScreenProperty && node.Screen is null)
            {
                notifications.Add("node:screen");
                Assert.False(ticker.IsActive);
                Assert.Same(screen, child.Screen);
                Assert.True(childTicker.IsActive);
            }
        };
        child.PropertyChanged += (_, args) =>
        {
            if (args.Property == UiNode.ScreenProperty && child.Screen is null)
            {
                notifications.Add("child:screen");
                Assert.False(ticker.IsActive);
                Assert.False(childTicker.IsActive);
            }
        };
        screen.Open();
        try
        {
            if (startedBeforeRemoval)
                ticker.Start();
            childTicker.Start();
            root.Children.Remove(node);
            Assert.Equal(["parent", "node:screen", "child:screen"], notifications);
            Assert.Null(node.Screen);
            Assert.Null(child.Screen);
            Assert.False(ticker.IsActive);
            Assert.False(childTicker.IsActive);
        }
        finally
        {
            screen.Close();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<UiTicker> CreateInactiveTicker(TickingNode node) =>
        new(node.AddTicker(static _ => { }));

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<UiTicker> CreateStoppedTicker(TickingNode node)
    {
        var ticker = node.AddTicker(static _ => { });
        ticker.Start();
        ticker.Stop();
        return new WeakReference<UiTicker>(ticker);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<UiTicker> CreateStartedTicker(TickingNode node, Action<TimeSpan> onTick)
    {
        var ticker = node.AddTicker(onTick);
        ticker.Start();
        return new WeakReference<UiTicker>(ticker);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void StopTicker(WeakReference<UiTicker> weakTicker)
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

    private static void Prepare(UiScreen screen, double seconds) =>
        screen.PrepareFrame(Viewport, TimeSpan.FromSeconds(seconds));

    private static void Prepare(GameScreen screen, double seconds)
    {
        screen.UpdateFrame(0);
        screen.PrepareFrame(Viewport, TimeSpan.FromSeconds(seconds));
    }

    private static void Prepare(UiManager manager, double seconds) =>
        manager.UpdateFrame(Viewport, 0, TimeSpan.FromSeconds(seconds));

    private static TickingNode MountHudNode(UiManager manager)
    {
        var node = new TickingNode();
        Assert.IsType<Panel>(manager.Hud.Screen.Root).Children.Add(node);
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
        internal UiTicker AddTicker(Action<TimeSpan> onTick) => CreateTicker(onTick);
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

    private sealed class CallbackScreen(UiNode root) : GameScreen(root)
    {
        internal Action? OpeningAction { get; init; }
        internal Action? FrameAction { get; set; }
        internal Action? ClosingAction { get; set; }

        protected override void OnOpening() => OpeningAction?.Invoke();
        protected override void OnFrameUpdate(double alpha) => FrameAction?.Invoke();
        protected override void OnClosing() => ClosingAction?.Invoke();
    }

    private sealed class CallbackHud(UiNode root, Action destroy) : Hud(root)
    {
        protected override void OnDestroy() => destroy();
    }
}