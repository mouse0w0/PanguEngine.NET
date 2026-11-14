using System.Buffers;
using System.Net.Sockets;
using PanguEngine.Network.Serialization;

namespace PanguEngine.Network;

internal static class TcpFraming
{
    internal static async Task<ReadOnlyMemory<byte>?> ReadAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(NetworkLimits.FrameTimeout);
        var prefix = new byte[5];
        if (await stream.ReadAsync(prefix.AsMemory(0, 1), deadline.Token).ConfigureAwait(false) == 0)
            return null;
        deadline.CancelAfter(NetworkLimits.FrameTimeout);
        var count = 1;
        while ((prefix[count - 1] & 0x80) != 0 && count < prefix.Length)
        {
            await stream.ReadExactlyAsync(prefix.AsMemory(count, 1), deadline.Token).ConfigureAwait(false);
            count++;
        }
        var length = ReadLength(prefix.AsSpan(0, count));
        var frame = new byte[length];
        await stream.ReadExactlyAsync(frame, deadline.Token).ConfigureAwait(false);
        return frame;
    }

    internal static async Task WriteAsync(NetworkStream stream, ReadOnlyMemory<byte> frame, CancellationToken cancellationToken)
    {
        if (frame.Length is < 1 or > NetworkLimits.MaxFrameBytes)
            throw new InvalidDataException("Frame content must be between 1 byte and 1 MiB.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(NetworkLimits.FrameTimeout);
        var buffer = new ArrayBufferWriter<byte>(5);
        WriteLength(buffer, frame.Length);
        await stream.WriteAsync(buffer.WrittenMemory, deadline.Token).ConfigureAwait(false);
        await stream.WriteAsync(frame, deadline.Token).ConfigureAwait(false);
    }

    private static int ReadLength(ReadOnlySpan<byte> prefix)
    {
        var reader = new Reader(prefix);
        var length = reader.ReadCount(NetworkLimits.MaxFrameBytes);
        if (length == 0)
            throw new InvalidDataException("A frame must contain a message identifier.");
        return length;
    }

    private static void WriteLength(IBufferWriter<byte> buffer, int length)
    {
        var writer = new Writer(buffer);
        writer.WriteCount(length);
    }
}
