using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Drawing;
using PanguEngine.Input;

namespace PanguEngine.Tests.Client.UI;

public sealed class GameScreenTests
{
    [Fact]
    public void GameBehaviorDefaultsToDisabledAndCanBeConfigured()
    {
        var defaultScreen = new GameScreen();
        var configuredScreen = new GameScreen
        {
            PausesGame = true,
            CloseOnEscape = true
        };

        Assert.False(defaultScreen.PausesGame);
        Assert.False(defaultScreen.CloseOnEscape);
        Assert.Same(BuiltinInputContexts.Ui, defaultScreen.InputContext);
        Assert.True(configuredScreen.PausesGame);
        Assert.True(configuredScreen.CloseOnEscape);
    }

    [Fact]
    public void FrameUpdateReceivesAlphaBeforePostsTickersAndLayout()
    {
        var events = new List<string>();
        var root = new TickingPanel(events);
        var screen = new RecordingGameScreen(root)
        {
            FixedAction = () => events.Add("fixed"),
            FrameAction = alpha =>
            {
                Assert.Equal(0.25, alpha);
                events.Add("frame");
            }
        };
        var ticker = root.AddTicker(time =>
        {
            Assert.Equal(TimeSpan.FromSeconds(10), time);
            events.Add("ticker");
        });
        screen.Open();
        try
        {
            ticker.Start();
            screen.Post(() => events.Add("post"));

            screen.Update();
            Assert.Equal(["fixed"], events);
            Assert.False(root.IsMeasureValid);

            screen.UpdateFrame(0.25);
            Assert.Equal(["fixed", "frame"], events);
            Assert.False(root.IsMeasureValid);
            screen.PrepareFrame(new Size(100, 100), TimeSpan.FromSeconds(10));

            Assert.Equal(["fixed", "frame", "post", "ticker", "layout"], events);
        }
        finally
        {
            screen.Close();
        }
    }

    [Fact]
    public void ClosingFromFrameUpdateStopsManagerUiPreparation()
    {
        var events = new List<string>();
        var root = new TickingPanel(events);
        var screen = new RecordingGameScreen(root);
        screen.FrameAction = _ =>
        {
            events.Add("frame");
            screen.Close();
        };
        var ticker = root.AddTicker(_ => events.Add("ticker"));
        var manager = new UiManager();
        manager.Open(screen);
        try
        {
            ticker.Start();
            manager.UpdateFrame(new Size(100, 100), 0.5, TimeSpan.FromSeconds(10));

            Assert.Equal(["frame"], events);
            Assert.False(ticker.IsActive);
            Assert.False(screen.IsOpen());
        }
        finally
        {
            manager.Destroy();
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void UiPreparationDoesNotRequireGameFrameUpdate(bool gameScreen)
    {
        var events = new List<string>();
        var root = new TickingPanel(events);
        UiScreen screen = gameScreen
            ? new RecordingGameScreen(root) { FrameAction = _ => throw new InvalidOperationException("game update") }
            : new UiScreen(root);
        var ticker = root.AddTicker(time =>
        {
            Assert.Equal(TimeSpan.FromSeconds(10), time);
            events.Add("ticker");
        });
        screen.Open();
        try
        {
            ticker.Start();
            screen.Post(() => events.Add("post"));
            screen.PrepareFrame(new Size(100, 100), TimeSpan.FromSeconds(10));

            Assert.Equal(["post", "ticker", "layout"], events);
            Assert.Same(screen, root.Screen);
        }
        finally
        {
            screen.Close();
        }
    }

    [Fact]
    public void NullRootGameScreenCompletesLifecycleAndUpdate()
    {
        var calls = 0;
        var fixedUpdates = 0;
        var frameUpdates = 0;
        var screen = new RecordingGameScreen
        {
            FixedAction = () => fixedUpdates++,
            FrameAction = _ => frameUpdates++
        };
        screen.Open();
        screen.Post(() => calls++);

        screen.Update();
        Assert.Equal(0, calls);
        screen.UpdateFrame(0);
        screen.PrepareFrame(new Size(20, 20));
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
        var root = new TickingPanel(events);
        var screen = new RecordingGameScreen(root)
        {
            FixedAction = () => events.Add("fixed"),
            FrameAction = _ => events.Add("frame")
        };
        screen.Open();
        screen.Post(() => events.Add("post"));

        screen.Update();

        Assert.Equal(["fixed"], events);
        Assert.False(root.IsMeasureValid);

        screen.UpdateFrame(0.25);
        screen.PrepareFrame(new Size(100, 100));

        Assert.Equal(["fixed", "frame", "post", "layout"], events);
        screen.Close();
    }

    [Theory]
    [InlineData(Visibility.Hidden)]
    [InlineData(Visibility.Collapsed)]
    public void VisibilityDoesNotSuppressUpdates(Visibility visibility)
    {
        var fixedUpdates = 0;
        var frameUpdates = 0;
        var screen = new RecordingGameScreen(new Panel { Visibility = visibility })
        {
            FixedAction = () => fixedUpdates++,
            FrameAction = _ => frameUpdates++
        };
        screen.Open();

        screen.Update();
        screen.UpdateFrame(0);
        screen.PrepareFrame(new Size(100, 100));

        Assert.Equal(1, fixedUpdates);
        Assert.Equal(1, frameUpdates);
        screen.Close();
    }

    [Fact]
    public void PostsFromFrameCallbackRunDuringSameFramePreparation()
    {
        var events = new List<string>();
        var screen = new RecordingGameScreen();
        screen.FrameAction = _ =>
        {
            events.Add("frame");
            screen.Post(() => events.Add("posted"));
        };
        screen.Open();

        screen.UpdateFrame(0);
        Assert.Equal(["frame"], events);
        screen.PrepareFrame(new Size(10, 10));
        Assert.Equal(["frame", "posted"], events);
        screen.UpdateFrame(0);
        screen.PrepareFrame(new Size(10, 10));
        Assert.Equal(["frame", "posted", "frame", "posted"], events);
        screen.Close();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void UpdatingScreenRejectsReentryAndDirectDrawing(bool fixedUpdate)
    {
        var screen = new RecordingGameScreen();
        var calls = 0;

        void Check()
        {
            calls++;
            Assert.Throws<InvalidOperationException>(screen.Update);
            Assert.Throws<InvalidOperationException>(() => screen.UpdateFrame(0));
            Assert.Throws<InvalidOperationException>(() => screen.PrepareFrame(new Size(10, 10)));
            Assert.Throws<InvalidOperationException>(() => screen.CreateDrawCommandList());
            Assert.Throws<InvalidOperationException>(() => new UiDrawCommandList().Append(screen));
        }

        screen.FixedAction = Check;
        screen.FrameAction = _ => Check();
        screen.Open();

        if (fixedUpdate)
            screen.Update();
        else
            screen.UpdateFrame(0);

        Assert.Equal(1, calls);
        Assert.Null(Record.Exception(() => screen.CreateDrawCommandList()));
        screen.Close();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ClosingDuringCallbackCannotReopenSameScreenUntilCallbackReturns(bool fixedUpdate)
    {
        var screen = new RecordingGameScreen();

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
            screen.UpdateFrame(0);

        Assert.False(screen.IsOpen());
        screen.Open();
        screen.Close();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FrameUpdateRejectsReentryAndWrongThreadWithoutInvokingCallback(bool fromOtherThread)
    {
        var alphas = new List<double>();
        var screen = new RecordingGameScreen();
        screen.FrameAction = alpha =>
        {
            alphas.Add(alpha);
            Exception? error = null;
            void Update() => error = Record.Exception(() => screen.UpdateFrame(0.9));

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
        screen.Open();
        try
        {
            screen.UpdateFrame(0.25);
            screen.UpdateFrame(0.5);

            Assert.Equal([0.25, 0.5], alphas);
        }
        finally
        {
            screen.Close();
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CallbackFailureStopsRemainingWorkAndAllowsNextUpdate(bool fixedUpdate)
    {
        var events = new List<string>();
        var expected = new InvalidOperationException("update failed");
        var root = new TickingPanel(events);
        var screen = new RecordingGameScreen(root)
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
        var ticker = root.AddTicker(_ => events.Add("ticker"));
        screen.Open();
        try
        {
            ticker.Start();
            screen.Post(() => events.Add("post"));

            var actual = fixedUpdate
                ? Assert.Throws<InvalidOperationException>(screen.Update)
                : Assert.Throws<InvalidOperationException>(() => screen.UpdateFrame(0.25));

            Assert.Same(expected, actual);
            Assert.Equal(fixedUpdate ? new[] { "fixed" } : ["frame"], events);
            Assert.False(screen.IsUpdating);
            Assert.False(root.IsMeasureValid);
            Assert.False(root.IsArrangeValid);
            Assert.True(ticker.IsActive);

            events.Clear();
            screen.FixedAction = () => events.Add("fixed");
            screen.FrameAction = alpha =>
            {
                Assert.Equal(0.5, alpha);
                events.Add("frame");
            };
            screen.Update();
            screen.UpdateFrame(0.5);
            screen.PrepareFrame(new Size(100, 100), TimeSpan.FromSeconds(11));

            Assert.Equal(["fixed", "frame", "post", "ticker", "layout"], events);
        }
        finally
        {
            screen.Close();
        }
    }

    [Fact]
    public void FrameCallbackFailureStopsManagerUiPreparationAndAllowsNextFrame()
    {
        var events = new List<string>();
        var expected = new InvalidOperationException("frame update failed");
        var root = new TickingPanel(events);
        var screen = new RecordingGameScreen(root)
        {
            FrameAction = _ =>
            {
                events.Add("frame");
                throw expected;
            }
        };
        var ticker = root.AddTicker(_ => events.Add("ticker"));
        var manager = new UiManager();
        manager.Open(screen);
        try
        {
            ticker.Start();
            screen.Post(() => events.Add("post"));

            var actual = Assert.Throws<InvalidOperationException>(() =>
                manager.UpdateFrame(new Size(100, 100), 0.25, TimeSpan.FromSeconds(10)));

            Assert.Same(expected, actual);
            Assert.Equal(["frame"], events);
            Assert.False(screen.IsUpdating);
            Assert.True(ticker.IsActive);
            Assert.False(root.IsMeasureValid);
            Assert.False(root.IsArrangeValid);

            events.Clear();
            screen.FrameAction = alpha =>
            {
                Assert.Equal(0.5, alpha);
                events.Add("frame");
            };
            manager.UpdateFrame(new Size(100, 100), 0.5, TimeSpan.FromSeconds(11));

            Assert.Equal(["frame", "post", "ticker", "layout"], events);
        }
        finally
        {
            manager.Destroy();
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OpenScreenRejectsUpdatesFromAnotherThreadWhenIdle(bool fixedUpdate)
    {
        var fixedUpdates = 0;
        var alphas = new List<double>();
        var screen = new RecordingGameScreen
        {
            FixedAction = () => fixedUpdates++,
            FrameAction = alphas.Add
        };
        screen.Open();
        try
        {
            Exception? error = null;
            var thread = new Thread(() => error = Record.Exception(() =>
            {
                if (fixedUpdate)
                    screen.Update();
                else
                    screen.UpdateFrame(0.9);
            }));
            thread.Start();
            thread.Join();

            Assert.IsType<InvalidOperationException>(error);
            Assert.Equal(0, fixedUpdates);
            Assert.Empty(alphas);
            Assert.False(screen.IsUpdating);

            screen.Update();
            screen.UpdateFrame(0.25);

            Assert.Equal(1, fixedUpdates);
            Assert.Equal([0.25], alphas);
        }
        finally
        {
            screen.Close();
        }
    }

    private sealed class RecordingGameScreen(UiNode? root = null) : GameScreen(root)
    {
        internal Action? FixedAction { get; set; }
        internal Action<double>? FrameAction { get; set; }

        protected override void OnFixedUpdate() => FixedAction?.Invoke();
        protected override void OnFrameUpdate(double alpha) => FrameAction?.Invoke(alpha);
    }

    private sealed class TickingPanel(List<string> events) : Panel
    {
        internal UiTicker AddTicker(Action<TimeSpan> onTick) => CreateTicker(onTick);

        protected override Size MeasureContent(Size availableSize)
        {
            events.Add("layout");
            return new Size(40, 30);
        }
    }
}
