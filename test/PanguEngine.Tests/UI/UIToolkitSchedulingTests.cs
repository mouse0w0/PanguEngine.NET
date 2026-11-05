using PanguEngine.UI;
using PanguEngine.UI.Controls;

namespace PanguEngine.Tests.UI;

[Collection(UIToolkitCollection.Name)]
public sealed class UIToolkitSchedulingTests
{
    [Fact]
    [UIThreadTest]
    public void PostStartsTickerBeforeTheSnapshotAndNestedPostWaits()
    {
        var calls = new List<string>();
        var ticker = UIToolkit.CreateTicker(_ => calls.Add("tick"));
        UIToolkit.Post(() =>
        {
            calls.Add("post");
            ticker.Start();
            UIToolkit.Post(() => calls.Add("deferred"));
        });

        UIToolkit.UpdateFrame(TimeSpan.Zero);
        Assert.Equal(["post", "tick"], calls);
        UIToolkit.UpdateFrame(TimeSpan.Zero);
        Assert.Equal(["post", "tick", "deferred", "tick"], calls);
        ticker.Stop();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [UIThreadTest]
    public void SceneClosureAndDetachmentDoNotControlTickerExecution(bool closeScene)
    {
        var ticks = new List<TimeSpan>();
        var root = new Panel();
        var node = new Panel();
        root.Children.Add(node);
        var ticker = UIToolkit.CreateTicker(ticks.Add);
        var scene = new UIScene(root);
        void SetUnavailable(bool unavailable)
        {
            if (closeScene)
            {
                if (unavailable)
                    scene.Close();
                else
                    scene.Open();
            }
            else if (unavailable)
            {
                root.Children.Remove(node);
            }
            else
            {
                root.Children.Add(node);
            }
        }

        ticker.Start();
        Assert.True(ticker.IsActive);
        UIToolkit.UpdateFrame(TimeSpan.Zero);
        Assert.Equal([TimeSpan.Zero], ticks);
        scene.Open();
        try
        {
            UIToolkit.UpdateFrame(TimeSpan.FromSeconds(1));
            UIToolkit.UpdateFrame(TimeSpan.FromSeconds(2));
            SetUnavailable(true);
            Assert.True(ticker.IsActive);
            UIToolkit.UpdateFrame(TimeSpan.FromSeconds(3));
            Assert.Equal([0d, 1d, 2d, 3d], ticks.Select(time => time.TotalSeconds));
            SetUnavailable(false);
            UIToolkit.UpdateFrame(TimeSpan.FromSeconds(10));
            Assert.Equal([0d, 1d, 2d, 3d, 10d], ticks.Select(time => time.TotalSeconds));

            SetUnavailable(true);
            ticker.Stop();
            SetUnavailable(false);
            UIToolkit.UpdateFrame(TimeSpan.FromSeconds(11));
            Assert.Equal(5, ticks.Count);
            Assert.False(ticker.IsActive);
        }
        finally
        {
            ticker.Stop();
            scene.Close();
        }
    }

    [Fact]
    [UIThreadTest]
    public void PostIsIndependentOfSceneClosureAndRunsBeforeLayout()
    {
        var root = new Panel();
        var scene = new UIScene(root);
        scene.Open();
        UIToolkit.Post(() => root.Width = 30);
        scene.Close();
        scene.Open();
        scene.PrepareFrame(new Size(100, 100));
        Assert.NotEqual(30, root.Width);
        UIToolkit.UpdateFrame(TimeSpan.Zero);
        scene.PrepareFrame(new Size(100, 100));
        Assert.Equal(30, root.LayoutBounds.Width);
        scene.Close();
    }

    [Fact]
    [UIThreadTest]
    public void PostFailurePreservesRemainingActionsAndSkipsTickers()
    {
        var expected = new InvalidOperationException("post failed");
        var calls = new List<string>();
        var ticker = UIToolkit.CreateTicker(_ => calls.Add("tick"));
        ticker.Start();
        UIToolkit.Post(() => throw expected);
        UIToolkit.Post(() => calls.Add("remaining"));

        Assert.Same(expected, Assert.Throws<InvalidOperationException>(() => UIToolkit.UpdateFrame(TimeSpan.Zero)));
        Assert.Empty(calls);
        UIToolkit.UpdateFrame(TimeSpan.Zero);
        Assert.Equal(["remaining", "tick"], calls);
        ticker.Stop();
    }

    [Fact]
    [UIThreadTest]
    public void BackgroundPostIsDeferredAndDoesNotExtendTheCurrentBatch()
    {
        var calls = new List<string>();
        Exception? error = null;
        UIToolkit.Post(() =>
        {
            calls.Add("first");
            var thread = new Thread(() => error = Record.Exception(() => UIToolkit.Post(() => calls.Add("background"))));
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(5)));
        });
        UIToolkit.Post(() => calls.Add("second"));

        UIToolkit.UpdateFrame(TimeSpan.Zero);
        Assert.Null(error);
        Assert.Equal(["first", "second"], calls);
        UIToolkit.UpdateFrame(TimeSpan.Zero);
        Assert.Equal(["first", "second", "background"], calls);
    }

    [Fact]
    public void ShutdownEndsRunsAndDropsPostsBeforeTheNextLifecycle()
    {
        var calls = 0;
        var ticker = UIToolkit.CreateTicker(_ => calls++);
        Assert.Throws<InvalidOperationException>(ticker.Start);
        Assert.Throws<InvalidOperationException>(() => UIToolkit.Post(() => calls++));
        using (var context = new TextTestContext())
        {
            ticker.Start();
            UIToolkit.Post(() => calls++);
        }

        Assert.False(ticker.IsActive);
        Assert.Throws<InvalidOperationException>(() => UIToolkit.Post(() => calls++));
        Assert.Throws<InvalidOperationException>(() => UIToolkit.UpdateFrame(TimeSpan.Zero));
        using var next = new TextTestContext();
        UIToolkit.UpdateFrame(TimeSpan.Zero);
        Assert.Equal(0, calls);
    }

    [Fact]
    [UIThreadTest]
    public void FrameUpdateCannotBeReenteredFromPostsOrTickers()
    {
        var errors = new List<Exception?>();
        UIToolkit.Post(() => errors.Add(Record.Exception(() => UIToolkit.UpdateFrame(TimeSpan.Zero))));
        var ticker = UIToolkit.CreateTicker(_ => errors.Add(Record.Exception(() => UIToolkit.UpdateFrame(TimeSpan.Zero))));
        ticker.Start();

        UIToolkit.UpdateFrame(TimeSpan.Zero);
        Assert.Equal(2, errors.Count);
        Assert.All(errors, error => Assert.IsType<InvalidOperationException>(error));
        ticker.Stop();
    }
}
