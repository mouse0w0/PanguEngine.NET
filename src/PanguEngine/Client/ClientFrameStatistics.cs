namespace PanguEngine.Client;

internal readonly record struct ClientFrameStatistics(
    int FramesPerSecond,
    double AverageFrameTimeMilliseconds,
    double OnePercentLowFrameTimeMilliseconds);
