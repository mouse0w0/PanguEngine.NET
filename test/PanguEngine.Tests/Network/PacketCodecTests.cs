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
using PanguEngine.Server;
using PanguEngine.World.Blocks;
using PanguEngine.World.Chunking;

namespace PanguEngine.Tests.Network;

[Collection("Network globals")]
public sealed class PacketCodecTests
{
    [Fact]
    public async Task InvalidUsernameEncodingClosesTheEstablishedConnection()
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var endpoint = (IPEndPoint)listener.LocalEndpoint;
            var connecting = Task.Run(() => TcpClientConnection.Connect(endpoint, "\uD800", TestRegistryBootstrap.Manager, []));
            using var server = await listener.AcceptTcpClientAsync(stop.Token);
            var stream = server.GetStream();
            var frame = await TcpFraming.ReadAsync(stream, stop.Token);
            Assert.IsType<ClientHelloPacket>(PacketCodec.ReadServerbound(ConnectionPhase.Handshake, frame!.Value.Span));
            await TcpFraming.WriteAsync(stream, PacketCodec.Encode(ConnectionPhase.Handshake, new LoginStartPacket()), stop.Token);

            await Assert.ThrowsAsync<ConnectionClosedException>(() => connecting.WaitAsync(stop.Token));
            Assert.Null(await TcpFraming.ReadAsync(stream, stop.Token));
        }
        finally { listener.Stop(); }
    }

    [Fact]
    public async Task WrongConfigurationResponseClosesTheClientConnection()
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var endpoint = (IPEndPoint)listener.LocalEndpoint;
            var connecting = Task.Run(() => TcpClientConnection.Connect(endpoint, "Alice", TestRegistryBootstrap.Manager, []));
            using var server = await listener.AcceptTcpClientAsync(stop.Token);
            var stream = server.GetStream();
            var frame = await TcpFraming.ReadAsync(stream, stop.Token);
            Assert.IsType<ClientHelloPacket>(PacketCodec.ReadServerbound(ConnectionPhase.Handshake, frame!.Value.Span));
            await TcpFraming.WriteAsync(stream, PacketCodec.Encode(ConnectionPhase.Handshake, new LoginStartPacket()), stop.Token);
            frame = await TcpFraming.ReadAsync(stream, stop.Token);
            Assert.IsType<LoginRequestPacket>(PacketCodec.ReadServerbound(ConnectionPhase.Login, frame!.Value.Span));
            await TcpFraming.WriteAsync(stream, PacketCodec.Encode(ConnectionPhase.Login, new LoginSuccessPacket()), stop.Token);
            frame = await TcpFraming.ReadAsync(stream, stop.Token);
            Assert.IsType<ClientModsPacket>(PacketCodec.ReadServerbound(ConnectionPhase.Configuration, frame!.Value.Span));
            await TcpFraming.WriteAsync(stream, PacketCodec.Encode(ConnectionPhase.Play, new InitialWorldCompletePacket()), stop.Token);

            await Assert.ThrowsAsync<ConnectionClosedException>(() => connecting.WaitAsync(stop.Token));
            Assert.Null(await TcpFraming.ReadAsync(stream, stop.Token));
        }
        finally { listener.Stop(); }
    }

    [Fact]
    public void PacketIdsAreScopedToTheCurrentPhaseAndDirection()
    {
        var start = PacketCodec.Encode(ConnectionPhase.Handshake, new LoginStartPacket());
        var success = PacketCodec.Encode(ConnectionPhase.Login, new LoginSuccessPacket());
        Assert.Equal(new byte[] { 2 }, start.ToArray());
        Assert.Equal(start.ToArray(), success.ToArray());
        Assert.IsType<LoginStartPacket>(PacketCodec.ReadClientbound(ConnectionPhase.Handshake, start.Span));
        Assert.IsType<LoginSuccessPacket>(PacketCodec.ReadClientbound(ConnectionPhase.Login, start.Span));

        var ready = PacketCodec.Encode(ConnectionPhase.Configuration, new ContentReadyPacket());
        Assert.Equal(new byte[] { 3 }, ready.ToArray());
        Assert.IsType<ContentReadyPacket>(PacketCodec.ReadServerbound(ConnectionPhase.Configuration, ready.Span));
        Assert.Throws<InvalidDataException>(() => PacketCodec.ReadServerbound(ConnectionPhase.Login, ready.Span));
        var input = new InputStatePacket(0, 0, 0, 0);
        Assert.Throws<InvalidOperationException>(() => PacketCodec.Encode(ConnectionPhase.Configuration, input));
        Assert.Throws<InvalidOperationException>(() => PacketCodec.Encode(ConnectionPhase.Login, new LoginStartPacket()));
    }

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
        var encodedInitial = PacketCodec.Encode(ConnectionPhase.Play, initial);
        var decodedInitial = Assert.IsType<InitialWorldStatePacket>(PacketCodec.ReadClientbound(ConnectionPhase.Play, encodedInitial.Span));

        Assert.Equal(networkId, decodedInitial.PlayerNetworkId);
        Assert.Equal(state, Assert.Single(decodedInitial.Players));
        Assert.Equal(1 + 8 + byteCount + 8 + 1 + byteCount + 40 + 1, encodedInitial.Length);

        var update = new WorldUpdatePacket(8, [state], [], []);
        var encodedUpdate = PacketCodec.Encode(ConnectionPhase.Play, update);
        var decodedUpdate = Assert.IsType<WorldUpdatePacket>(PacketCodec.ReadClientbound(ConnectionPhase.Play, encodedUpdate.Span));

        Assert.Equal(state, Assert.Single(decodedUpdate.Players));
        Assert.Equal(1 + 8 + 1 + byteCount + 40 + 1 + 1, encodedUpdate.Length);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(uint.MaxValue)]
    public void InitialWorldRejectsMissingControlledPlayer(uint networkId)
    {
        var initial = PacketCodec.Encode(ConnectionPhase.Play, new InitialWorldStatePacket(0, networkId, 0.05, [new(1, default)], 0));

        Assert.Throws<InvalidDataException>(() => PacketCodec.ReadClientbound(ConnectionPhase.Play, initial.Span));
    }

    [Fact]
    public void PlayerListRejectsDuplicateNetworkIds()
    {
        var update = PacketCodec.Encode(ConnectionPhase.Play, new WorldUpdatePacket(0, [new(0, default), new(0, default)], [], []));

        Assert.Throws<InvalidDataException>(() => PacketCodec.ReadClientbound(ConnectionPhase.Play, update.Span));
    }

    [Fact]
    public void DisconnectUsesIdOneAndPreservesReason()
    {
        var packet = new DisconnectPacket("服务端主动断开连接。");
        var encoded = PacketCodec.Encode(ConnectionPhase.Play, packet);

        Assert.Equal(1, encoded.Span[0]);
        Assert.Equal(packet, PacketCodec.ReadClientbound(ConnectionPhase.Play, encoded.Span));
        Assert.Throws<InvalidDataException>(() => PacketCodec.ReadClientbound(ConnectionPhase.Play, new byte[] { 9 }));
    }

    [Fact]
    public void InputUsesExplicitIdAndRejectsTrailingData()
    {
        var input = new InputStatePacket(1, -1, 45, 20);
        var encoded = PacketCodec.Encode(ConnectionPhase.Play, input);
        Assert.Equal(2, encoded.Span[0]);
        Assert.Equal(input, PacketCodec.ReadServerbound(ConnectionPhase.Play, encoded.Span));
        var extra = encoded.ToArray().Concat(new byte[] { 0 }).ToArray();
        Assert.Throws<InvalidDataException>(() => PacketCodec.ReadServerbound(ConnectionPhase.Play, extra));
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
        var decoded = Assert.IsType<ChunkDataPacket>(PacketCodec.ReadClientbound(ConnectionPhase.Play, PacketCodec.Encode(ConnectionPhase.Play, chunk).Span));
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

        var decoded = Assert.IsType<ChunkDataPacket>(PacketCodec.ReadClientbound(ConnectionPhase.Play, PacketCodec.Encode(ConnectionPhase.Play, source).Span));

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

        var decoded = Assert.IsType<ChunkDataPacket>(PacketCodec.ReadClientbound(ConnectionPhase.Play, PacketCodec.Encode(ConnectionPhase.Play, packet).Span));

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

        Assert.Throws<InvalidDataException>(() => PacketCodec.ReadClientbound(ConnectionPhase.Play, buffer.WrittenSpan));
    }

    [Fact]
    public void ChunkRejectsInvalidPaletteAndIndices()
    {
        TestRegistryBootstrap.Manager.AllocateIdTables();
        BlockStateIds.Build();
        var air = BlockStateIds.GetId(BuiltinBlocks.Air.DefaultState);
        var stone = BlockStateIds.GetId(BuiltinBlocks.Stone.DefaultState);

        Assert.Throws<InvalidDataException>(() => PacketCodec.ReadClientbound(ConnectionPhase.Play, LocalChunkFrame([], 0)));
        Assert.Throws<InvalidDataException>(() => PacketCodec.ReadClientbound(ConnectionPhase.Play, LocalChunkFrame(Enumerable.Range(0, 17).ToArray(), 0)));
        Assert.Throws<InvalidDataException>(() => PacketCodec.ReadClientbound(ConnectionPhase.Play, LocalChunkFrame([air, air], 0)));
        Assert.Throws<InvalidDataException>(() => PacketCodec.ReadClientbound(ConnectionPhase.Play, LocalChunkFrame([BlockStateIds.Count], 0)));
        Assert.Throws<InvalidDataException>(() => PacketCodec.ReadClientbound(ConnectionPhase.Play, LocalChunkFrame([air, stone], 2)));
        var valid = LocalChunkFrame([air, stone], 0);
        Assert.Throws<EndOfStreamException>(() => PacketCodec.ReadClientbound(ConnectionPhase.Play, valid.AsSpan(0, valid.Length - 1)));

        var buffer = new ArrayBufferWriter<byte>();
        var writer = new Writer(buffer);
        WriteChunkHeader(ref writer, (byte)BlockStateIds.Bits);
        for (var i = 0; i < PackedBlockStates.WordCount(BlockStateIds.Bits); i++)
            writer.WriteUInt64(i == 0 ? (ulong)BlockStateIds.Count : 0);
        Assert.Throws<InvalidDataException>(() => PacketCodec.ReadClientbound(ConnectionPhase.Play, buffer.WrittenSpan));
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
        writer.WriteVarUInt32(3);
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
            var connecting = Task.Run(() => TcpClientConnection.Connect(endpoint, "Alice", manager, []));
            using var accepted = listener.AcceptTcpClientAsync(deadline.Token).AsTask().GetAwaiter().GetResult();
            var connection = new TcpConnection(accepted);
            try
            {
                var serverConnection = new TcpServerConnection(connection);
                var names = new PlayerNames();
                var session = new RemoteSession(serverConnection, serverConnection.PrepareAsync(manifest, [], names), names);
                var client = connecting.WaitAsync(deadline.Token).GetAwaiter().GetResult();
                try
                {
                    Assert.True(session.Preparation.WaitAsync(deadline.Token).GetAwaiter().GetResult().IsReady);
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
            finally { connection.Destroy(); }
        }
        finally
        {
            listener.Stop();
            manager.AllocateIdTables();
            BlockStateIds.Build();
        }
    }

    [Fact]
    public async Task RemoteHandshakeRejectsAnIncompatibleMappingAsAConnectionFailure()
    {
        var manager = new RegistryManager();
        var registry = new Registry<object>(ResourceKey.Parse("test:content"));
        manager.Register(registry);
        manager.RegisterIdTable(new RegistryIdTable<object>(registry));
        manager.FreezeAll();
        await AssertRemoteMappingFailure(manager, new RegistryIdMappings([]), localFailure: false);
    }

    [Fact]
    public async Task RemoteHandshakeDoesNotWrapAnUnfrozenLocalRegistryAsAConnectionFailure()
    {
        var manager = new RegistryManager();
        var registry = new Registry<object>(ResourceKey.Parse("test:content"));
        manager.Register(registry);
        manager.RegisterIdTable(new RegistryIdTable<object>(registry));
        await AssertRemoteMappingFailure(manager, new RegistryIdMappings([new(registry.Key, [])]), localFailure: true);
    }

    private static async Task AssertRemoteMappingFailure(RegistryManager manager, RegistryIdMappings mappings, bool localFailure)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var address = (IPEndPoint)listener.LocalEndpoint;
            var connecting = Task.Run(() => TcpClientConnection.Connect(address, "Alice", manager, []));
            var connection = new TcpConnection(await listener.AcceptTcpClientAsync(deadline.Token));
            try
            {
                var endpoint = new TcpServerConnection(connection);
                var preparing = endpoint.PrepareAsync(new ContentManifestPacket(mappings), [], new PlayerNames());
                if (localFailure)
                {
                    var error = await Assert.ThrowsAsync<InvalidOperationException>(() => connecting.WaitAsync(deadline.Token));
                    Assert.Contains("must be frozen", error.Message, StringComparison.Ordinal);
                    connection.Close("The test server stopped.");
                }
                else
                {
                    var error = await Assert.ThrowsAsync<ConnectionClosedException>(() => connecting.WaitAsync(deadline.Token));
                    Assert.Contains("Mapping registries do not match", error.Message, StringComparison.Ordinal);
                }
                var (username, isReady) = await preparing.WaitAsync(deadline.Token);
                Assert.Equal("Alice", username);
                Assert.False(isReady);
            }
            finally { connection.Destroy(); }
        }
        finally { listener.Stop(); }
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

        var encoded = PacketCodec.Encode(ConnectionPhase.Play, update);
        var decoded = Assert.IsType<WorldUpdatePacket>(PacketCodec.ReadClientbound(ConnectionPhase.Play, encoded.Span));

        Assert.Equal(1 + 8 + 1 + 1 + 3 * 13 + 1, encoded.Length);
        Assert.Equal<WorldUpdatePacket.BlockChange>(update.Changes, decoded.Changes);
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new Writer(buffer);
        writer.WriteVarUInt32(5);
        writer.WriteInt64(1);
        writer.WriteCount(0);
        writer.WriteCount(1);
        PacketData.WritePosition(ref writer, default);
        writer.WriteCount(BlockStateIds.Count);
        writer.WriteCount(0);
        Assert.Throws<InvalidDataException>(() => PacketCodec.ReadClientbound(ConnectionPhase.Play, buffer.WrittenSpan));
    }

}

[CollectionDefinition("Network globals", DisableParallelization = true)]
public sealed class NetworkGlobalsCollection;
