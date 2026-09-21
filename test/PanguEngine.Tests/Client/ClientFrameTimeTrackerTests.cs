using PanguEngine.Client;

namespace PanguEngine.Tests.Client;

public sealed class ClientFrameTimeTrackerTests
{
    [Fact]
    public void FirstPresentedFrameDoesNotProduceStatistics()
    {
        var time = 0d;
        var tracker = new ClientFrameTimeTracker(() => time);

        tracker.RecordPresentedFrame();

        Assert.Equal(0, tracker.CreateSnapshot().FramesPerSecond);
    }

    [Fact]
    public void SnapshotUsesPresentedFrameIntervals()
    {
        var time = 0d;
        var tracker = new ClientFrameTimeTracker(() => time);
        tracker.RecordPresentedFrame();
        for (var index = 0; index < 99; index++)
        {
            time += 1d / 60;
            tracker.RecordPresentedFrame();
        }

        time += 0.03;
        tracker.RecordPresentedFrame();

        var statistics = tracker.CreateSnapshot();

        Assert.Equal(60, statistics.FramesPerSecond);
        Assert.Equal(16.8, statistics.AverageFrameTimeMilliseconds, 3);
        Assert.Equal(30, statistics.OnePercentLowFrameTimeMilliseconds, 3);
    }

    [Fact]
    public void LongStallIntervalsAreIgnored()
    {
        var time = 0d;
        var tracker = new ClientFrameTimeTracker(() => time);
        tracker.RecordPresentedFrame();
        for (var index = 0; index < 60; index++)
        {
            time += 1d / 60;
            tracker.RecordPresentedFrame();
        }

        var before = tracker.CreateSnapshot();

        time += 5;
        tracker.RecordPresentedFrame();

        Assert.Equal(before, tracker.CreateSnapshot());
    }
}
