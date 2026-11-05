using PanguEngine.Client.UI;
using PanguEngine.Input;
using PanguEngine.UI;
using PanguEngine.UI.Controls;
using PanguEngine.UI.Drawing;

namespace PanguEngine.Tests.UI;

[Collection(UIToolkitCollection.Name)]
public sealed class GameUISceneTests
{
    [Fact]
    public void GameBehaviorDefaultsToDisabledAndCanBeConfigured()
    {
        var defaultScene = new GameUIScene();
        var configuredScene = new GameUIScene
        {
            PausesGame = true,
            CloseOnEscape = true
        };

        Assert.False(defaultScene.PausesGame);
        Assert.False(defaultScene.CloseOnEscape);
        Assert.Same(BuiltinInputContexts.Ui, defaultScene.InputContext);
        Assert.True(configuredScene.PausesGame);
        Assert.True(configuredScene.CloseOnEscape);
    }

    [Fact]
    [UIThreadTest]
    public void FrameUpdateReceivesAlphaAfterToolkitAndBeforeLayout()
    {
        var events = new List<string>();
        var root = new TickingPanel(events);
        var scene = new RecordingGameUIScene(root)
        {
            FixedAction = () => events.Add("fixed"),
            FrameAction = alpha =>
            {
                Assert.Equal(0.25, alpha);
                events.Add("frame");
            }
        };
        var ticker = UIToolkit.CreateTicker(time =>
        {
            Assert.Equal(TimeSpan.FromSeconds(10), time);
            events.Add("ticker");
        });
        scene.Open();
        try
        {
            ticker.Start();
            UIToolkit.Post(() => events.Add("post"));

            scene.Update();
            Assert.Equal(["fixed"], events);
            Assert.False(root.IsMeasureValid);

            UIToolkit.UpdateFrame(TimeSpan.FromSeconds(10));
            scene.UpdateFrame(0.25);
            Assert.Equal(["fixed", "post", "ticker", "frame"], events);
            Assert.False(root.IsMeasureValid);
            scene.PrepareFrame(new Size(100, 100));

            Assert.Equal(["fixed", "post", "ticker", "frame", "layout"], events);
        }
        finally
        {
            ticker.Stop();
            scene.Close();
        }
    }

    [Fact]
    [UIThreadTest]
    public void ClosingFromFrameUpdateStopsManagerUiPreparation()
    {
        var events = new List<string>();
        var root = new TickingPanel(events);
        var scene = new RecordingGameUIScene(root);
        scene.FrameAction = _ =>
        {
            events.Add("frame");
            scene.Close();
        };
        var ticker = UIToolkit.CreateTicker(_ => events.Add("ticker"));
        var manager = new UIManager();
        manager.Open(scene);
        try
        {
            ticker.Start();
            manager.UpdateFrame(new Size(100, 100), 0.5, TimeSpan.FromSeconds(10));

            Assert.Equal(["ticker", "frame"], events);
            Assert.True(ticker.IsActive);
            Assert.False(scene.IsOpen);
        }
        finally
        {
            ticker.Stop();
            manager.Destroy();
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [UIThreadTest]
    public void UiPreparationDoesNotRequireGameFrameUpdate(bool gameScene)
    {
        var events = new List<string>();
        var root = new TickingPanel(events);
        UIScene scene = gameScene
            ? new RecordingGameUIScene(root) { FrameAction = _ => throw new InvalidOperationException("game update") }
            : new UIScene(root);
        var ticker = UIToolkit.CreateTicker(time =>
        {
            Assert.Equal(TimeSpan.FromSeconds(10), time);
            events.Add("ticker");
        });
        scene.Open();
        try
        {
            ticker.Start();
            UIToolkit.Post(() => events.Add("post"));
            UIToolkit.UpdateFrame(TimeSpan.FromSeconds(10));
            scene.PrepareFrame(new Size(100, 100));

            Assert.Equal(["post", "ticker", "layout"], events);
            Assert.Same(scene, root.Scene);
        }
        finally
        {
            ticker.Stop();
            scene.Close();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [UIThreadTest]
    public void TickerStartedFromGameFrameWaitsForTheNextToolkitFrame(bool post)
    {
        var times = new List<TimeSpan>();
        var ticker = UIToolkit.CreateTicker(times.Add);
        var scene = new RecordingGameUIScene
        {
            FrameAction = _ =>
            {
                if (post)
                    UIToolkit.Post(ticker.Start);
                else
                    ticker.Start();
            }
        };
        var manager = new UIManager();
        manager.Open(scene);
        try
        {
            manager.UpdateFrame(new Size(100, 100), 0, TimeSpan.FromSeconds(10));
            Assert.Empty(times);
            manager.UpdateFrame(new Size(100, 100), 0, TimeSpan.FromSeconds(11));
            Assert.Equal([TimeSpan.FromSeconds(11)], times);
        }
        finally
        {
            ticker.Stop();
            manager.Destroy();
        }
    }

    [Fact]
    [UIThreadTest]
    public void NullRootGameUISceneCompletesLifecycleAndUpdate()
    {
        var calls = 0;
        var fixedUpdates = 0;
        var frameUpdates = 0;
        var scene = new RecordingGameUIScene
        {
            FixedAction = () => fixedUpdates++,
            FrameAction = _ => frameUpdates++
        };
        scene.Open();
        UIToolkit.Post(() => calls++);

        scene.Update();
        Assert.Equal(0, calls);
        UIToolkit.UpdateFrame(TimeSpan.Zero);
        scene.UpdateFrame(0);
        scene.PrepareFrame(new Size(20, 20));
        scene.Close();

        Assert.Equal(1, calls);
        Assert.Equal(1, fixedUpdates);
        Assert.Equal(1, frameUpdates);
        Assert.Null(scene.Root);
    }

    [Fact]
    [UIThreadTest]
    public void FixedUpdateDoesNotDrainPostsOrLayout()
    {
        var events = new List<string>();
        var root = new TickingPanel(events);
        var scene = new RecordingGameUIScene(root)
        {
            FixedAction = () => events.Add("fixed"),
            FrameAction = _ => events.Add("frame")
        };
        scene.Open();
        UIToolkit.Post(() => events.Add("post"));

        scene.Update();

        Assert.Equal(["fixed"], events);
        Assert.False(root.IsMeasureValid);

        UIToolkit.UpdateFrame(TimeSpan.Zero);
        scene.UpdateFrame(0.25);
        scene.PrepareFrame(new Size(100, 100));

        Assert.Equal(["fixed", "post", "frame", "layout"], events);
        scene.Close();
    }

    [Theory]
    [InlineData(Visibility.Hidden)]
    [InlineData(Visibility.Collapsed)]
    [UIThreadTest]
    public void VisibilityDoesNotSuppressUpdates(Visibility visibility)
    {
        var fixedUpdates = 0;
        var frameUpdates = 0;
        var scene = new RecordingGameUIScene(new Panel { Visibility = visibility })
        {
            FixedAction = () => fixedUpdates++,
            FrameAction = _ => frameUpdates++
        };
        scene.Open();

        scene.Update();
        scene.UpdateFrame(0);
        scene.PrepareFrame(new Size(100, 100));

        Assert.Equal(1, fixedUpdates);
        Assert.Equal(1, frameUpdates);
        scene.Close();
    }

    [Fact]
    [UIThreadTest]
    public void PostsFromFrameCallbackRunDuringTheNextToolkitUpdate()
    {
        var events = new List<string>();
        var scene = new RecordingGameUIScene();
        scene.FrameAction = _ =>
        {
            events.Add("frame");
            UIToolkit.Post(() => events.Add("posted"));
        };
        var manager = new UIManager();
        manager.Open(scene);
        try
        {
            manager.UpdateFrame(new Size(10, 10), 0, TimeSpan.Zero);
            Assert.Equal(["frame"], events);
            manager.UpdateFrame(new Size(10, 10), 0, TimeSpan.FromSeconds(1));
            Assert.Equal(["frame", "posted", "frame"], events);
        }
        finally
        {
            manager.Destroy();
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [UIThreadTest]
    public void UpdatingSceneRejectsReentryAndDirectDrawing(bool fixedUpdate)
    {
        var scene = new RecordingGameUIScene();
        var calls = 0;

        void Check()
        {
            calls++;
            Assert.Throws<InvalidOperationException>(scene.Update);
            Assert.Throws<InvalidOperationException>(() => scene.UpdateFrame(0));
            Assert.Throws<InvalidOperationException>(() => scene.PrepareFrame(new Size(10, 10)));
            Assert.Throws<InvalidOperationException>(() => scene.CreateDrawCommandList());
            Assert.Throws<InvalidOperationException>(() => new DrawCommandList().Append(scene));
        }

        scene.FixedAction = Check;
        scene.FrameAction = _ => Check();
        scene.Open();

        if (fixedUpdate)
            scene.Update();
        else
            scene.UpdateFrame(0);

        Assert.Equal(1, calls);
        Assert.Null(Record.Exception(() => scene.CreateDrawCommandList()));
        scene.Close();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [UIThreadTest]
    public void ClosingDuringCallbackCannotReopenSameSceneUntilCallbackReturns(bool fixedUpdate)
    {
        var scene = new RecordingGameUIScene();

        void CloseAndReopen()
        {
            scene.Close();
            Assert.Throws<InvalidOperationException>(scene.Open);
        }

        scene.FixedAction = CloseAndReopen;
        scene.FrameAction = _ => CloseAndReopen();
        scene.Open();

        if (fixedUpdate)
            scene.Update();
        else
            scene.UpdateFrame(0);

        Assert.False(scene.IsOpen);
        scene.Open();
        scene.Close();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [UIThreadTest]
    public void FrameUpdateRejectsReentryAndWrongThreadWithoutInvokingCallback(bool fromOtherThread)
    {
        var alphas = new List<double>();
        var scene = new RecordingGameUIScene();
        scene.FrameAction = alpha =>
        {
            alphas.Add(alpha);
            Exception? error = null;
            void Update() => error = Record.Exception(() => scene.UpdateFrame(0.9));

            if (fromOtherThread)
            {
                var thread = new Thread(Update);
                thread.Start();
                thread.Join();
            }
            else
            {
                Update();
            }

            Assert.IsType<InvalidOperationException>(error);
        };
        scene.Open();
        try
        {
            scene.UpdateFrame(0.25);
            scene.UpdateFrame(0.5);

            Assert.Equal([0.25, 0.5], alphas);
        }
        finally
        {
            scene.Close();
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [UIThreadTest]
    public void CallbackFailureStopsRemainingWorkAndAllowsNextUpdate(bool fixedUpdate)
    {
        var events = new List<string>();
        var expected = new InvalidOperationException("update failed");
        var root = new TickingPanel(events);
        var scene = new RecordingGameUIScene(root)
        {
            FixedAction = () =>
            {
                events.Add("fixed");
                if (fixedUpdate)
                    throw expected;
            },
            FrameAction = _ =>
            {
                events.Add("frame");
                throw expected;
            }
        };
        var ticker = UIToolkit.CreateTicker(_ => events.Add("ticker"));
        scene.Open();
        try
        {
            ticker.Start();
            UIToolkit.Post(() => events.Add("post"));

            var actual = fixedUpdate
                ? Assert.Throws<InvalidOperationException>(scene.Update)
                : Assert.Throws<InvalidOperationException>(() => scene.UpdateFrame(0.25));

            Assert.Same(expected, actual);
            Assert.Equal(fixedUpdate ? new[] { "fixed" } : ["frame"], events);
            Assert.False(scene.IsUpdating);
            Assert.False(root.IsMeasureValid);
            Assert.False(root.IsArrangeValid);
            Assert.True(ticker.IsActive);

            events.Clear();
            scene.FixedAction = () => events.Add("fixed");
            scene.FrameAction = alpha =>
            {
                Assert.Equal(0.5, alpha);
                events.Add("frame");
            };
            scene.Update();
            UIToolkit.UpdateFrame(TimeSpan.FromSeconds(11));
            scene.UpdateFrame(0.5);
            scene.PrepareFrame(new Size(100, 100));

            Assert.Equal(["fixed", "post", "ticker", "frame", "layout"], events);
        }
        finally
        {
            ticker.Stop();
            scene.Close();
        }
    }

    [Fact]
    [UIThreadTest]
    public void FrameCallbackFailureStopsManagerUiPreparationAndAllowsNextFrame()
    {
        var events = new List<string>();
        var expected = new InvalidOperationException("frame update failed");
        var root = new TickingPanel(events);
        var scene = new RecordingGameUIScene(root)
        {
            FrameAction = _ =>
            {
                events.Add("frame");
                throw expected;
            }
        };
        var ticker = UIToolkit.CreateTicker(_ => events.Add("ticker"));
        var manager = new UIManager();
        manager.Open(scene);
        try
        {
            ticker.Start();
            UIToolkit.Post(() => events.Add("post"));

            var actual = Assert.Throws<InvalidOperationException>(() =>
                manager.UpdateFrame(new Size(100, 100), 0.25, TimeSpan.FromSeconds(10)));

            Assert.Same(expected, actual);
            Assert.Equal(["post", "ticker", "frame"], events);
            Assert.False(scene.IsUpdating);
            Assert.True(ticker.IsActive);
            Assert.False(root.IsMeasureValid);
            Assert.False(root.IsArrangeValid);

            events.Clear();
            scene.FrameAction = alpha =>
            {
                Assert.Equal(0.5, alpha);
                events.Add("frame");
            };
            manager.UpdateFrame(new Size(100, 100), 0.5, TimeSpan.FromSeconds(11));

            Assert.Equal(["ticker", "frame", "layout"], events);
        }
        finally
        {
            ticker.Stop();
            manager.Destroy();
        }
    }

    [Theory(Skip = UIThreadTestAttribute.DebugOnly)]
    [InlineData(true)]
    [InlineData(false)]
    [UIThreadTest]
    public void OpenSceneRejectsUpdatesFromAnotherThreadWhenIdle(bool fixedUpdate)
    {
        var fixedUpdates = 0;
        var alphas = new List<double>();
        var scene = new RecordingGameUIScene
        {
            FixedAction = () => fixedUpdates++,
            FrameAction = alphas.Add
        };
        scene.Open();
        try
        {
            Exception? error = null;
            var thread = new Thread(() => error = Record.Exception(() =>
            {
                if (fixedUpdate)
                    scene.Update();
                else
                    scene.UpdateFrame(0.9);
            }));
            thread.Start();
            thread.Join();

            Assert.IsType<InvalidOperationException>(error);
            Assert.Equal(0, fixedUpdates);
            Assert.Empty(alphas);
            Assert.False(scene.IsUpdating);

            scene.Update();
            scene.UpdateFrame(0.25);

            Assert.Equal(1, fixedUpdates);
            Assert.Equal([0.25], alphas);
        }
        finally
        {
            scene.Close();
        }
    }

    private sealed class RecordingGameUIScene(UIElement? root = null) : GameUIScene(root)
    {
        internal Action? FixedAction { get; set; }
        internal Action<double>? FrameAction { get; set; }

        protected override void OnFixedUpdate() => FixedAction?.Invoke();
        protected override void OnFrameUpdate(double alpha) => FrameAction?.Invoke(alpha);
    }

    private sealed class TickingPanel(List<string> events) : Panel
    {
        protected override Size MeasureContent(Size availableSize)
        {
            events.Add("layout");
            return new Size(40, 30);
        }
    }
}
