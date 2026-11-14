namespace PanguEngine.Network;

internal sealed class FrameQueue
{
    private readonly Queue<ReadOnlyMemory<byte>> _frames = [];
    private int _bytes;

    internal int Count => _frames.Count;

    internal bool TryEnqueue(ReadOnlyMemory<byte> frame)
    {
        if (_frames.Count >= NetworkLimits.MaxQueuedFrames || frame.Length > NetworkLimits.MaxQueuedBytes - _bytes)
            return false;
        _frames.Enqueue(frame);
        _bytes += frame.Length;
        return true;
    }

    internal bool TryPeek(out ReadOnlyMemory<byte> frame) => _frames.TryPeek(out frame);

    internal void Dequeue() => _bytes -= _frames.Dequeue().Length;
}
