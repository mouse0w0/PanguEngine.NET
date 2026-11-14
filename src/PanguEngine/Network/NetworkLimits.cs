namespace PanguEngine.Network;

internal static class NetworkLimits
{
    internal const int MaxConnections = 16;
    internal const int MaxFrameBytes = 1024 * 1024;
    internal const int MaxQueuedFrames = 512;
    internal const int MaxQueuedBytes = 8 * 1024 * 1024;
    internal static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);
    internal static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(15);
    internal static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(5);
    internal static readonly TimeSpan FrameTimeout = TimeSpan.FromSeconds(30);
    internal static readonly TimeSpan InitialTimeout = TimeSpan.FromSeconds(60);
}
