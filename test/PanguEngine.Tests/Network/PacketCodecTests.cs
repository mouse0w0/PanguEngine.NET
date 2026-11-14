using System.Net;
using System.Net.Sockets;
using PanguEngine.Network;
using PanguEngine.Network.Packets;
using PanguEngine.Network.Packets.Clientbound;
using PanguEngine.Network.Packets.Serverbound;
using PanguEngine.Registries;
using PanguEngine.Server.Game;
using PanguEngine.World.Blocks;
using PanguEngine.World.Chunking;

namespace PanguEngine.Tests.Network;

[Collection("Network globals")]
public sealed class PacketCodecTests
{
    [Theory]
    [InlineData(0u, 1)]
    [InlineData(127u, 1)]
    [InlineData(128u, 2)]
    [InlineData(16383u, 2)]
    [InlineData(16384u, 3)]
    [InlineData(uint.MaxValue, 5)]
    [InlineData(0x80000000u, 5)]
    [InlineData(0x7fffffffu, 5)]
    public void PlayerNetworkIdsRoundTripUsingVariableLengthEncoding(uint networkId, int byteCount)
    {
        var state = new PlayerState(networkId, new PlayerPose(new(1, 2, 3), 45, 10));
        var initial = new InitialWorldStatePacket(7, networkId, 0.05, [state], 0);
        var encodedInitial = PacketCodec.Encode(initial);
        var decodedInitial = Assert.IsType<InitialWorldStatePacket>(PacketCodec.ReadClientbound(encodedInitial.Span));

        Assert.Equal(networkId, decodedInitial.PlayerNetworkId);
        Assert.Equal(state, Assert.Single(decodedInitial.Players));
        Assert.Equal(1 + 8 + byteCount + 8 + 1 + byteCount + 40 + 1, encodedInitial.Length);

        var update = new WorldUpdatePacket(8, [state], [], []);
        var encodedUpdate = PacketCodec.Encode(update);
        var decodedUpdate = Assert.IsType<WorldUpdatePacket>(PacketCodec.ReadClientbound(encodedUpdate.Span));

        Assert.Equal(state, Assert.Single(decodedUpdate.Players));
        Assert.Equal(1 + 8 + 1 + byteCount + 40 + 1 + 1, encodedUpdate.Length);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(uint.MaxValue)]
    public void InitialWorldRejectsMissingControlledPlayer(uint networkId)
    {
        var initial = PacketCodec.Encode(new InitialWorldStatePacket(0, networkId, 0.05, [new(1, default)], 0));

        Assert.Throws<InvalidDataException>(() => PacketCodec.ReadClientbound(initial.Span));
    }

    [Fact]
    public void PlayerListRejectsDuplicateNetworkIds()
    {
        var update = PacketCodec.Encode(new WorldUpdatePacket(0, [new(0, default), new(0, default)], [], []));

        Assert.Throws<InvalidDataException>(() => PacketCodec.ReadClientbound(update.Span));
    }

    [Fact]
    public void DisconnectUsesIdFiveAndPreservesReason()
    {
        var packet = new DisconnectPacket("服务端主动断开连接。");
        var encoded = PacketCodec.Encode(packet);

        Assert.Equal(5, encoded.Span[0]);
        Assert.Equal(packet, PacketCodec.ReadClientbound(encoded.Span));
        Assert.Throws<InvalidDataException>(() => PacketCodec.ReadClientbound(new byte[] { 7 }));
    }

    [Fact]
    public void InputUsesExplicitIdAndRejectsTrailingData()
    {
        var input = new InputStatePacket(1, -1, 45, 20);
        var encoded = PacketCodec.Encode(input);
        Assert.Equal(1, encoded.Span[0]);
        Assert.Equal(input, PacketCodec.ReadServerbound(encoded.Span));
        var extra = encoded.ToArray().Concat(new byte[] { 0 }).ToArray();
        Assert.Throws<InvalidDataException>(() => PacketCodec.ReadServerbound(extra));
    }

    [Fact]
    public void ChunkRoundTripUsesBoundCanonicalStates()
    {
        TestRegistryBootstrap.Manager.AllocateIdTables();
        BlockStateIds.Build();
        var states = new BlockState[Chunk.Volume];
        Array.Fill(states, BuiltinBlocks.Air.DefaultState);
        states[37] = TestRegistryBootstrap.StatefulBlock.StateDefinition.States[^1];
        var chunk = new ChunkDataPacket(new ChunkPos(-2, 1, 3), states);
        var decoded = Assert.IsType<ChunkDataPacket>(PacketCodec.ReadClientbound(PacketCodec.Encode(chunk).Span));
        Assert.Equal(chunk.Position, decoded.Position);
        Assert.Same(states[37], decoded.GetBlock(37));
        Assert.Same(BuiltinBlocks.Air.DefaultState, decoded.GetBlock(38));
    }

    [Fact]
    public void RemoteHandshakeBindsServerOrderAndDerivesOnlyPublishedBlockStates()
    {
        var manager = TestRegistryBootstrap.Manager;
        manager.AllocateIdTables();
        BlockStateIds.Build();
        var table = BuiltinIdTables.Block;
        ResourceKey[] keys =
        [
            table.GetKey(TestRegistryBootstrap.StatefulBlock),
            table.GetKey(BuiltinBlocks.Stone),
            table.GetKey(BuiltinBlocks.Air)
        ];
        var tables = manager.ExportIdMappings().Tables.Select(mapping =>
            mapping.RegistryKey == table.Key ? new RegistryIdMapping(table.Key, keys) : mapping).ToArray();
        var manifest = new ContentManifestPacket(new RegistryIdMappings(tables));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var endpoint = (IPEndPoint)listener.LocalEndpoint;
            var connecting = Task.Run(() => RemoteConnection.Connect(endpoint, manager, []));
            using var accepted = listener.AcceptTcpClientAsync(deadline.Token).AsTask().GetAwaiter().GetResult();
            var connection = new TcpConnection(accepted);
            var game = new ServerGame();
            try
            {
                var session = new RemoteSession(connection, manifest, []);
                connection.WaitForFrame(TimeSpan.FromSeconds(5));
                session.Receive(game);
                var client = connecting.WaitAsync(deadline.Token).GetAwaiter().GetResult();
                try
                {
                    Assert.Same(TestRegistryBootstrap.CustomIdValue, TestRegistryBootstrap.CustomIdTable.Get(0));
                    var states = TestRegistryBootstrap.StatefulBlock.StateDefinition.States;
                    for (var i = 0; i < states.Count; i++)
                    {
                        Assert.Equal(i, BlockStateIds.GetId(states[i]));
                        Assert.Same(states[i], BlockStateIds.Get(i));
                    }
                    Assert.Equal(states.Count, BlockStateIds.GetId(BuiltinBlocks.Stone.DefaultState));
                    Assert.Equal(states.Count + 1, BlockStateIds.GetId(BuiltinBlocks.Air.DefaultState));
                    Assert.Throws<KeyNotFoundException>(() => BlockStateIds.GetId(BuiltinBlocks.Grass.DefaultState));
                }
                finally { client.Close(); }
            }
            finally
            {
                connection.Destroy();
                game.Destroy();
            }
        }
        finally
        {
            listener.Stop();
            manager.AllocateIdTables();
            BlockStateIds.Build();
        }
    }

    [Fact]
    public void WorldUpdateUsesSingleStateIdsAndRejectsUnknownStates()
    {
        TestRegistryBootstrap.Manager.AllocateIdTables();
        BlockStateIds.Build();
        var states = TestRegistryBootstrap.StatefulBlock.StateDefinition.States;
        var update = new WorldUpdatePacket(1, [],
            [new(new BlockPos(0, 0, 0), states[1]), new(new BlockPos(1, 0, 0), states[2]),
             new(new BlockPos(2, 0, 0), states[3])], []);

        var encoded = PacketCodec.Encode(update);
        var decoded = Assert.IsType<WorldUpdatePacket>(PacketCodec.ReadClientbound(encoded.Span));

        Assert.Equal(1 + 8 + 1 + 1 + 3 * 13 + 1, encoded.Length);
        Assert.Equal<WorldUpdatePacket.BlockChange>(update.Changes, decoded.Changes);
        var unknownState = encoded.ToArray();
        unknownState[^2] = 127;
        Assert.Throws<InvalidDataException>(() => PacketCodec.ReadClientbound(unknownState));
    }

}

[CollectionDefinition("Network globals", DisableParallelization = true)]
public sealed class NetworkGlobalsCollection;
