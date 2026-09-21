using System.Diagnostics;

namespace PanguEngine.Client;

/// <summary>
/// Tracks intervals between presented frames and produces a rolling statistics snapshot.
/// </summary>
internal sealed class ClientFrameTimeTracker
{
    private const int WindowCapacity = 240;
    private const double MaxRecordedFrameTimeMilliseconds = 1000;

    private readonly Func<double> _getTime;
    private readonly double[] _frameTimes = new double[WindowCapacity];
    private readonly double[] _scratch = new double[WindowCapacity];
    private double _lastTimestamp;
    private bool _hasTimestamp;
    private int _count;
    private int _nextIndex;

    internal ClientFrameTimeTracker()
        : this(CreateDefaultTimeSource())
    {
    }

    internal ClientFrameTimeTracker(Func<double> getTime)
    {
        ArgumentNullException.ThrowIfNull(getTime);
        _getTime = getTime;
    }

    /// <summary>
    /// Records that a frame was presented and adds the interval from the previous presented frame.
    /// </summary>
    internal void RecordPresentedFrame()
    {
        var timestamp = _getTime();
        if (_hasTimestamp)
        {
            var frameTimeMilliseconds = (timestamp - _lastTimestamp) * 1000;
            if (double.IsFinite(frameTimeMilliseconds) &&
                frameTimeMilliseconds > 0 &&
                frameTimeMilliseconds <= MaxRecordedFrameTimeMilliseconds)
            {
                _frameTimes[_nextIndex] = frameTimeMilliseconds;
                _nextIndex = (_nextIndex + 1) % WindowCapacity;
                if (_count < WindowCapacity)
                    _count++;
            }
        }

        _lastTimestamp = timestamp;
        _hasTimestamp = true;
    }

    /// <summary>
    /// Creates a statistics snapshot over the recorded frame interval window.
    /// </summary>
    internal ClientFrameStatistics CreateSnapshot()
    {
        if (_count == 0)
            return default;

        var sum = 0d;
        for (var index = 0; index < _count; index++)
            sum += _frameTimes[index];
        var average = sum / _count;

        Array.Copy(_frameTimes, _scratch, _count);
        Array.Sort(_scratch, 0, _count);
        var lowCount = Math.Max(1, _count / 100);
        var lowSum = 0d;
        for (var index = 0; index < lowCount; index++)
            lowSum += _scratch[_count - 1 - index];

        return new ClientFrameStatistics(
            (int)Math.Round(1000d / average),
            average,
            lowSum / lowCount);
    }

    private static Func<double> CreateDefaultTimeSource()
    {
        var stopwatch = Stopwatch.StartNew();
        return () => stopwatch.Elapsed.TotalSeconds;
    }
}
