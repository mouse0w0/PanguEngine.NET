using System.Net;
using System.Net.Sockets;
using System.Buffers;
using PanguEngine.Network;
using PanguEngine.Network.Packets;
using PanguEngine.Network.Packets.Clientbound;
using PanguEngine.Network.Packets.Serverbound;
using PanguEngine.Network.Serialization;
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
        var source = new Chunk(new ChunkPos(-2, 1, 3));
        var state = TestRegistryBootstrap.StatefulBlock.StateDefinition.States[^1];
        source.SetBlock(new BlockPos(5, 0, 2), state);
        var chunk = ChunkDataPacket.Capture(source);
        var decoded = Assert.IsType<ChunkDataPacket>(PacketCodec.ReadClientbound(PacketCodec.Encode(chunk).Span));
        Assert.Equal(chunk.Position, decoded.Position);
        Assert.Same(state, decoded.GetBlock(37));
        Assert.Same(BuiltinBlocks.Air.DefaultState, decoded.GetBlock(38));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(17)]
    [InlineData(129)]
    [InlineData(257)]
    public void ChunkRoundTripPreservesStoredEncoding(int count)
    {
        TestRegistryBootstrap.Manager.AllocateIdTables();
        BlockStateIds.Build();
        var states = TestRegistryBootstrap.PaletteBlock.StateDefinition.States;
        var storage = new BlockStateStorage(states[0]);
        for (var i = 0; i < Chunk.Volume; i++)
            storage.Set(i, states[i % count]);
        var source = new ChunkDataPacket(default, storage.Capture());

        var decoded = Assert.IsType<ChunkDataPacket>(PacketCodec.ReadClientbound(PacketCodec.Encode(source).Span));

        Assert.Equal(source.Blocks.Bits, decoded.Blocks.Bits);
        Assert.Equal(source.Blocks.EntryCount, decoded.Blocks.EntryCount);
        Assert.Same(states[0], decoded.GetBlock(0));
        Assert.Same(states[(Chunk.Volume - 1) % count], decoded.GetBlock(Chunk.Volume - 1));
    }

    [Fact]
    public void ChunkRoundTripAllowsFewEntriesAfterGrowthCompaction()
    {
        TestRegistryBootstrap.Manager.AllocateIdTables();
        BlockStateIds.Build();
        var states = TestRegistryBootstrap.PaletteBlock.StateDefinition.States;
        var storage = new BlockStateStorage(states[0]);
        for (var i = 1; i <= 16; i++)
            storage.Set(0, states[i]);
        var packet = new ChunkDataPacket(default, storage.Capture());

        var decoded = Assert.IsType<ChunkDataPacket>(PacketCodec.ReadClientbound(PacketCodec.Encode(packet).Span));

        Assert.Equal(5, decoded.Blocks.Bits);
        Assert.Equal(3, decoded.Blocks.EntryCount);
        Assert.Same(states[16], decoded.GetBlock(0));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(9)]
    [InlineData(32)]
    public void ChunkRejectsUnsupportedOrMismatchedBitWidths(byte bits)
    {
        TestRegistryBootstrap.Manager.AllocateIdTables();
        BlockStateIds.Build();
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new Writer(buffer);
        WriteChunkHeader(ref writer, bits);

        Assert.Throws<InvalidDataException>(() => PacketCodec.ReadClientbound(buffer.WrittenSpan));
    }

    [Fact]
    public void ChunkRejectsInvalidPaletteAndIndices()
    {
        TestRegistryBootstrap.Manager.AllocateIdTables();
        BlockStateIds.Build();
        var air = BlockStateIds.GetId(BuiltinBlocks.Air.DefaultState);
        var stone = BlockStateIds.GetId(BuiltinBlocks.Stone.DefaultState);

        Assert.Throws<InvalidDataException>(() => PacketCodec.ReadClientbound(LocalChunkFrame([], 0)));
        Assert.Throws<InvalidDataException>(() => PacketCodec.ReadClientbound(LocalChunkFrame(Enumerable.Range(0, 17).ToArray(), 0)));
        Assert.Throws<InvalidDataException>(() => PacketCodec.ReadClientbound(LocalChunkFrame([air, air], 0)));
        Assert.Throws<InvalidDataException>(() => PacketCodec.ReadClientbound(LocalChunkFrame([BlockStateIds.Count], 0)));
        Assert.Throws<InvalidDataException>(() => PacketCodec.ReadClientbound(LocalChunkFrame([air, stone], 2)));
        var valid = LocalChunkFrame([air, stone], 0);
        Assert.Throws<EndOfStreamException>(() => PacketCodec.ReadClientbound(valid.AsSpan(0, valid.Length - 1)));

        var buffer = new ArrayBufferWriter<byte>();
        var writer = new Writer(buffer);
        WriteChunkHeader(ref writer, (byte)BlockStateIds.Bits);
        for (var i = 0; i < PackedBlockStates.WordCount(BlockStateIds.Bits); i++)
            writer.WriteUInt64(i == 0 ? (ulong)BlockStateIds.Count : 0);
        Assert.Throws<InvalidDataException>(() => PacketCodec.ReadClientbound(buffer.WrittenSpan));
    }

    private static byte[] LocalChunkFrame(int[] ids, ulong firstWord)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new Writer(buffer);
        WriteChunkHeader(ref writer, 4);
        writer.WriteCount(ids.Length);
        foreach (var id in ids)
            writer.WriteCount(id);
        for (var i = 0; i < PackedBlockStates.WordCount(4); i++)
            writer.WriteUInt64(i == 0 ? firstWord : 0);
        return buffer.WrittenSpan.ToArray();
    }

    private static void WriteChunkHeader(ref Writer writer, byte bits)
    {
        writer.WriteVarUInt32(2);
        writer.WriteInt32(0);
        writer.WriteInt32(0);
        writer.WriteInt32(0);
        writer.WriteByte(bits);
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
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new Writer(buffer);
        writer.WriteVarUInt32(4);
        writer.WriteInt64(1);
        writer.WriteCount(0);
        writer.WriteCount(1);
        PacketData.WritePosition(ref writer, default);
        writer.WriteCount(BlockStateIds.Count);
        writer.WriteCount(0);
        Assert.Throws<InvalidDataException>(() => PacketCodec.ReadClientbound(buffer.WrittenSpan));
    }

}

[CollectionDefinition("Network globals", DisableParallelization = true)]
public sealed class NetworkGlobalsCollection;
