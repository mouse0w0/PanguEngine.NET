using System.Runtime.CompilerServices;
using PanguEngine.Client.UI;
using PanguEngine.UI;

namespace PanguEngine.Tests.UI;

[Collection(UIToolkitCollection.Name)]
public sealed class TickerTests
{
    [Fact]
    public void CreationDoesNotStartAndRequiresCallback()
    {
        var ticker = UIToolkit.CreateTicker(_ => { });

        Assert.False(ticker.IsActive);
        Assert.Throws<ArgumentNullException>(() => UIToolkit.CreateTicker(null!));
        Assert.Throws<InvalidOperationException>(ticker.Start);
        ticker.Stop();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [UIThreadTest]
    public void InactiveAndStoppedTickersAreNotRetained(bool start)
    {
        var weakTicker = CreateCollectibleTicker(start);

        ForceCollection();

        Assert.False(weakTicker.TryGetTarget(out _));
    }

    [Fact]
    [UIThreadTest]
    public void ActiveTickerRunsWithoutConsumerReferenceAndIsReleasedOnStop()
    {
        var times = new List<TimeSpan>();
        var weakTicker = CreateStartedTicker(times.Add);
        ForceCollection();

        UIToolkit.UpdateFrame(TimeSpan.FromSeconds(10));
        Assert.Equal([TimeSpan.FromSeconds(10)], times);

        StopTicker(weakTicker);
        ForceCollection();
        Assert.False(weakTicker.TryGetTarget(out _));
    }

    [Fact]
    [UIThreadTest]
    public void StartAndStopAreIdempotentAndRestartMovesToTheEnd()
    {
        var calls = new List<string>();
        var first = UIToolkit.CreateTicker(_ => calls.Add("first"));
        var second = UIToolkit.CreateTicker(_ => calls.Add("second"));
        try
        {
            first.Start();
            second.Start();
            first.Start();
            UIToolkit.UpdateFrame(TimeSpan.Zero);
            Assert.Equal(["first", "second"], calls);

            calls.Clear();
            first.Stop();
            first.Stop();
            Assert.False(first.IsActive);
            first.Start();
            UIToolkit.UpdateFrame(TimeSpan.Zero);
            Assert.Equal(["second", "first"], calls);
        }
        finally
        {
            first.Stop();
            second.Stop();
        }
    }

    [Fact]
    [UIThreadTest]
    public void ConsumerIntegratesExplicitTimeAndResetsBaselineForNewRun()
    {
        TimeSpan? previous = null;
        var distance = 0d;
        var times = new List<TimeSpan>();
        var ticker = UIToolkit.CreateTicker(now =>
        {
            times.Add(now);
            if (previous is { } baseline)
                distance += 100 * (now - baseline).TotalSeconds;
            previous = now;
        });
        try
        {
            ticker.Start();
            UIToolkit.UpdateFrame(TimeSpan.FromSeconds(10));
            UIToolkit.UpdateFrame(TimeSpan.FromSeconds(10.02));
            UIToolkit.UpdateFrame(TimeSpan.FromSeconds(10.05));
            Assert.Equal(5, distance, 8);
            Assert.Equal([
                TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10.02),
                TimeSpan.FromSeconds(10.05)
            ], times);

            ticker.Stop();
            previous = null;
            ticker.Start();
            UIToolkit.UpdateFrame(TimeSpan.FromSeconds(20));
            UIToolkit.UpdateFrame(TimeSpan.FromSeconds(20.01));
            Assert.Equal(6, distance, 8);
        }
        finally
        {
            ticker.Stop();
        }
    }

    [Fact]
    [UIThreadTest]
    public void EqualTimestampsStillRepresentSeparateFrames()
    {
        var calls = 0;
        var ticker = UIToolkit.CreateTicker(_ => calls++);
        try
        {
            ticker.Start();
            UIToolkit.UpdateFrame(TimeSpan.FromSeconds(10));
            UIToolkit.UpdateFrame(TimeSpan.FromSeconds(10));
            Assert.Equal(2, calls);
        }
        finally
        {
            ticker.Stop();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [UIThreadTest]
    public void StoppingPendingRunSkipsItEvenIfRestarted(bool restart)
    {
        var calls = new List<string>();
        var second = UIToolkit.CreateTicker(_ => calls.Add("second"));
        var first = UIToolkit.CreateTicker(_ =>
        {
            calls.Add("first");
            second.Stop();
            if (restart)
                second.Start();
        });
        try
        {
            first.Start();
            second.Start();
            UIToolkit.UpdateFrame(TimeSpan.Zero);
            Assert.Equal(["first"], calls);
            first.Stop();
            UIToolkit.UpdateFrame(TimeSpan.Zero);
            Assert.Equal(restart ? ["first", "second"] : new[] { "first" }, calls);
        }
        finally
        {
            first.Stop();
            second.Stop();
        }
    }

    [Fact]
    [UIThreadTest]
    public void CallbackStartsNewTaskAndRestartsItselfOnlyForNextFrame()
    {
        var calls = new List<string>();
        var second = UIToolkit.CreateTicker(_ => calls.Add("second"));
        Ticker first = null!;
        first = UIToolkit.CreateTicker(_ =>
        {
            calls.Add("first:begin");
            first.Stop();
            second.Start();
            first.Start();
            calls.Add("first:end");
        });
        try
        {
            first.Start();
            UIToolkit.UpdateFrame(TimeSpan.Zero);
            Assert.Equal(["first:begin", "first:end"], calls);
            calls.Clear();
            UIToolkit.UpdateFrame(TimeSpan.Zero);
            Assert.Equal(["second", "first:begin", "first:end"], calls);
        }
        finally
        {
            first.Stop();
            second.Stop();
        }
    }

    [Fact]
    [UIThreadTest]
    public void TickersShareAbsoluteTimeInRegistrationOrder()
    {
        var calls = new List<(string Name, TimeSpan Time)>();
        var first = UIToolkit.CreateTicker(time => calls.Add(("first", time)));
        var second = UIToolkit.CreateTicker(time => calls.Add(("second", time)));
        try
        {
            first.Start();
            second.Start();
            UIToolkit.UpdateFrame(TimeSpan.FromSeconds(10));
            Assert.Equal([("first", TimeSpan.FromSeconds(10)), ("second", TimeSpan.FromSeconds(10))], calls);
        }
        finally
        {
            first.Stop();
            second.Stop();
        }
    }

    [Fact]
    [UIThreadTest]
    public void CallbackFailurePropagatesAndLeavesLaterRunsForTheNextFrame()
    {
        var expected = new InvalidOperationException("tick failed");
        var failure = UIToolkit.CreateTicker(_ => throw expected);
        var calls = 0;
        var later = UIToolkit.CreateTicker(_ => calls++);
        try
        {
            failure.Start();
            later.Start();
            Assert.Same(expected, Assert.Throws<InvalidOperationException>(() => UIToolkit.UpdateFrame(TimeSpan.Zero)));
            Assert.True(failure.IsActive);
            Assert.True(later.IsActive);
            Assert.Equal(0, calls);

            failure.Stop();
            UIToolkit.UpdateFrame(TimeSpan.Zero);
            Assert.Equal(1, calls);
        }
        finally
        {
            failure.Stop();
            later.Stop();
        }
    }

    [Fact]
    [UIThreadTest]
    public void TickerRunsAfterPostsAndBeforeGameFrameAndAllLayoutRetries()
    {
        var events = new List<string>();
        var node = new MeasuringNode(events);
        var scene = new CallbackScene(node, events);
        var ticker = UIToolkit.CreateTicker(_ =>
        {
            events.Add("tick");
            node.Width = 40;
        });
        scene.Open();
        try
        {
            UIToolkit.Post(() => events.Add("post"));
            ticker.Start();
            UIToolkit.UpdateFrame(TimeSpan.FromSeconds(10));
            scene.UpdateFrame(0);
            scene.PrepareFrame(new Size(100, 100));

            Assert.Equal(["post", "tick", "frame", "measure", "measure", "measure"], events);
            Assert.Equal(40, node.LayoutBounds.Width);
            Assert.True(node.IsArrangeValid);
        }
        finally
        {
            ticker.Stop();
            scene.Close();
        }
    }

    [Fact(Skip = UIThreadTestAttribute.DebugOnly)]
    [UIThreadTest]
    public void WrongThreadCannotStartOrStopAnActiveRun()
    {
        var ticker = UIToolkit.CreateTicker(_ => { });
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
            ticker.Stop();
        }

        Assert.Null(OnOtherThread(ticker.Stop));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<Ticker> CreateCollectibleTicker(bool start)
    {
        var ticker = UIToolkit.CreateTicker(static _ => { });
        if (start)
        {
            ticker.Start();
            ticker.Stop();
        }
        return new WeakReference<Ticker>(ticker);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<Ticker> CreateStartedTicker(Action<TimeSpan> onTick)
    {
        var ticker = UIToolkit.CreateTicker(onTick);
        ticker.Start();
        return new WeakReference<Ticker>(ticker);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void StopTicker(WeakReference<Ticker> reference)
    {
        Assert.True(reference.TryGetTarget(out var ticker));
        ticker!.Stop();
    }

    private static void ForceCollection()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private sealed class CallbackScene(UIElement root, List<string> events) : GameUIScene(root)
    {
        protected override void OnFrameUpdate(double alpha) => events.Add("frame");
    }

    private sealed class MeasuringNode(List<string> events) : UIElement
    {
        private int _measures;

        protected override Size MeasureCore(Size availableSize)
        {
            events.Add("measure");
            if (_measures++ < 2)
                InvalidateMeasure();
            return new Size(40, 30);
        }
    }

    private static Exception? OnOtherThread(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() => error = Record.Exception(action));
        thread.Start();
        thread.Join();
        return error;
    }
}
