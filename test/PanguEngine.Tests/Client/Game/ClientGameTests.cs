using System.Reflection;
using PanguEngine.Client.Game;
using PanguEngine.Client.World;
using PanguEngine.Network;
using PanguEngine.Network.Packets.Clientbound;

namespace PanguEngine.Tests.Client.Game;

public sealed class ClientGameTests
{
    internal static readonly Func<IClientConnection, ClientWorld, InitialWorldStatePacket> LoadInitialWorld =
        typeof(ClientGame).GetMethod("LoadInitialWorld", BindingFlags.NonPublic | BindingFlags.Static)!
            .CreateDelegate<Func<IClientConnection, ClientWorld, InitialWorldStatePacket>>();

    [Fact]
    public void PlayHeartbeatsDoNotAdvanceTheInitialWorldSequence()
    {
        var connection = new InMemoryConnection();
        var world = new ClientWorld();
        var initial = new InitialWorldStatePacket(0, 1, 0.05, [new(1, default)], 0);
        connection.Publish(new ClientboundHeartbeatPacket());
        connection.Publish(initial);
        connection.Publish(new ClientboundHeartbeatPacket());
        connection.Publish(new InitialWorldCompletePacket());

        Assert.Same(initial, LoadInitialWorld(connection, world));
        world.Destroy();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DisconnectPreservesReasonBeforeOrAfterInitialMetadata(bool receivedMetadata)
    {
        var connection = new InMemoryConnection();
        var world = new ClientWorld();
        if (receivedMetadata)
        {
            var playerNetworkId = 1u;
            connection.Publish(new InitialWorldStatePacket(0, playerNetworkId, 0.05, [new(playerNetworkId, default)], 1));
        }
        var reason = "Initial world loading was stopped.";
        connection.Publish(new DisconnectPacket(reason));

        var error = Assert.Throws<ConnectionClosedException>(() => LoadInitialWorld(connection, world));

        Assert.Equal(reason, error.Message);
        world.Destroy();
    }
}
