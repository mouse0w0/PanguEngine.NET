using System.Net;
using System.Net.Sockets;
using PanguEngine.Network;

namespace PanguEngine.Tests.Network;

public sealed class TcpFramingTests
{
    [Fact]
    public async Task ReadsSplitPrefixAndConsecutiveFrames()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            using var client = new TcpClient();
            var accept = listener.AcceptTcpClientAsync(deadline.Token);
            await client.ConnectAsync((IPEndPoint)listener.LocalEndpoint, deadline.Token);
            using var server = await accept;
            var output = client.GetStream();
            await output.WriteAsync(new byte[] { 0x82 }, deadline.Token);
            await output.WriteAsync(new byte[] { 0x01 }, deadline.Token);
            var payload = Enumerable.Range(0, 130).Select(i => (byte)i).ToArray();
            await output.WriteAsync(payload, deadline.Token);
            await TcpFraming.WriteAsync(output, new byte[] { 7, 8 }, deadline.Token);
            Assert.Equal(payload, (await TcpFraming.ReadAsync(server.GetStream(), deadline.Token))!.Value.ToArray());
            Assert.Equal(new byte[] { 7, 8 }, (await TcpFraming.ReadAsync(server.GetStream(), deadline.Token))!.Value.ToArray());
        }
        finally { listener.Stop(); }
    }

    [Theory]
    [InlineData(new byte[] { 0 })]
    [InlineData(new byte[] { 0x80, 0x80, 0x80, 0x80, 0x10 })]
    [InlineData(new byte[] { 0x81, 0x80, 0x40 })]
    public async Task RejectsInvalidFrameLengthBeforeReadingBody(byte[] prefix)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            using var client = new TcpClient();
            var accept = listener.AcceptTcpClientAsync(deadline.Token);
            await client.ConnectAsync((IPEndPoint)listener.LocalEndpoint, deadline.Token);
            using var server = await accept;
            await client.GetStream().WriteAsync(prefix, deadline.Token);
            await Assert.ThrowsAsync<InvalidDataException>(() => TcpFraming.ReadAsync(server.GetStream(), deadline.Token));
        }
        finally { listener.Stop(); }
    }

    [Fact]
    public void QueueEnforcesCountAndByteBudgetsUntilConsumption()
    {
        var queue = new FrameQueue();
        for (var i = 0; i < NetworkLimits.MaxQueuedFrames; i++)
            Assert.True(queue.TryEnqueue(new byte[] { 1 }));
        Assert.False(queue.TryEnqueue(new byte[] { 2 }));
        Assert.True(queue.TryPeek(out var first));
        Assert.Equal(1, first.Span[0]);
        Assert.False(queue.TryEnqueue(new byte[] { 2 }));
        queue.Dequeue();
        Assert.True(queue.TryEnqueue(new byte[] { 2 }));

        var bytes = new FrameQueue();
        var frame = new byte[NetworkLimits.MaxFrameBytes];
        for (var i = 0; i < 8; i++)
            Assert.True(bytes.TryEnqueue(frame));
        Assert.False(bytes.TryEnqueue(new byte[] { 1 }));
    }

    [Theory]
    [InlineData(new byte[] { 0x80 })]
    [InlineData(new byte[] { 3, 1 })]
    public async Task RejectsConnectionEndingInsidePrefixOrPayload(byte[] partialFrame)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            using var client = new TcpClient();
            var accept = listener.AcceptTcpClientAsync(deadline.Token);
            await client.ConnectAsync((IPEndPoint)listener.LocalEndpoint, deadline.Token);
            using var server = await accept;
            await client.GetStream().WriteAsync(partialFrame, deadline.Token);
            client.Client.Shutdown(SocketShutdown.Send);
            await Assert.ThrowsAsync<EndOfStreamException>(() => TcpFraming.ReadAsync(server.GetStream(), deadline.Token));
        }
        finally { listener.Stop(); }
    }
}
