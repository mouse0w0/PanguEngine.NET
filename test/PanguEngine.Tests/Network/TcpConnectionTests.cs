using System.Net;
using System.Net.Sockets;
using PanguEngine.Network;
using PanguEngine.Network.Packets.Clientbound;

namespace PanguEngine.Tests.Network;

public sealed class TcpConnectionTests
{
    [Fact]
    public async Task TcpClientReturnsQueuedPacketsBeforeReportingClosure()
    {
        var timeout = TimeSpan.FromSeconds(5);
        using var stop = new CancellationTokenSource(timeout);
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync((IPEndPoint)listener.LocalEndpoint, stop.Token);
            using var accepted = await listener.AcceptTcpClientAsync(stop.Token);
            var connection = new TcpConnection(accepted);
            IClientConnection receiver = new TcpClientConnection(new TcpConnection(client));
            try
            {
                Assert.False(receiver.TryReceive(out _));
                var endpoint = new TcpServerConnection(connection);
                Assert.True(endpoint.TrySend(new LoginStartPacket()));
                endpoint.Disconnect("The test server closed.");

                Assert.True(SpinWait.SpinUntil(() => receiver.IsClosed, timeout));
                Assert.IsType<LoginStartPacket>(receiver.WaitForPacket(timeout));
                var notice = Assert.IsType<DisconnectPacket>(receiver.WaitForPacket(timeout));
                Assert.Equal("The test server closed.", notice.Reason);
                Assert.Throws<ConnectionClosedException>(() => receiver.WaitForPacket(timeout));
                Assert.Throws<ConnectionClosedException>(() => receiver.TryReceive(out _));
            }
            finally
            {
                receiver.Close();
                connection.Destroy();
            }
        }
        finally { listener.Stop(); }
    }

    [Fact]
    public async Task AsyncWaitTracksQueuedFramesAndWakesOnClose()
    {
        var timeout = TimeSpan.FromSeconds(5);
        using var stop = new CancellationTokenSource(timeout);
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync((IPEndPoint)listener.LocalEndpoint, stop.Token);
            using var accepted = await listener.AcceptTcpClientAsync(stop.Token);
            var connection = new TcpConnection(accepted);
            try
            {
                var waiting = connection.WaitForFrameAsync(timeout);
                Assert.False(waiting.IsCompleted);
                await TcpFraming.WriteAsync(client.GetStream(), new byte[] { 0 }, stop.Token);
                await waiting.WaitAsync(timeout);
                Assert.True(connection.TryPeek(out var frame));
                Assert.Equal(new byte[] { 0 }, frame.ToArray());
                connection.Consume();

                waiting = connection.WaitForFrameAsync(timeout);
                Assert.False(waiting.IsCompleted);
                await TcpFraming.WriteAsync(client.GetStream(), new byte[] { 2 }, stop.Token);
                await waiting.WaitAsync(timeout);
                connection.Consume();

                await Assert.ThrowsAsync<ConnectionClosedException>(() => connection.WaitForFrameAsync(TimeSpan.FromMilliseconds(20)));
                waiting = connection.WaitForFrameAsync(timeout);
                Assert.False(waiting.IsCompleted);
                connection.Close("Test complete.");
                await Assert.ThrowsAsync<ConnectionClosedException>(() => waiting.WaitAsync(timeout));
            }
            finally { connection.Destroy(); }
        }
        finally { listener.Stop(); }
    }
}
