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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DisconnectPreservesReasonBeforeOrAfterInitialMetadata(bool receivedMetadata)
    {
        var connection = new LocalConnection();
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
