using PanguEngine.World.Blocks;
using PanguEngine.World.Chunking;
using PanguEngine.Network;
using PanguEngine.Network.Packets;
using PanguEngine.Network.Packets.Clientbound;
using PanguEngine.Network.Packets.Serverbound;

namespace PanguEngine.Tests.Network;

public sealed class LocalConnectionTests
{
    [Fact]
    public void RequestAcceptanceTracksPauseResumeAndNormalCompletion()
    {
        var connection = new LocalConnection();
        List<ServerboundPacket> packets = [];
        Assert.True(connection.TrySend(new BlockInteractionPacket(BlockOperation.Break, 0, 0)));
        connection.SetAcceptingRequests(false, clearPending: true);
        Assert.False(connection.TrySend(new InputStatePacket(0, 0, 0, 0)));
        connection.DrainServerboundPackets(packets);
        Assert.Empty(packets);
        connection.SetAcceptingRequests(true, clearPending: false);
        Assert.True(connection.TrySend(new InputStatePacket(0, 0, 0, 0)));
        connection.DrainServerboundPackets(packets);
        Assert.Single(packets);
        connection.Complete();
        Assert.False(connection.TrySend(new InputStatePacket(0, 0, 0, 0)));
    }

    [Fact]
    public void PacketBatchPreservesOrderAndDoesNotIncludeLaterPackets()
    {
        var connection = new LocalConnection();
        List<ServerboundPacket> packets = [];
        connection.TrySend(new BlockInteractionPacket(BlockOperation.Place, 0, 0));
        connection.TrySend(new BlockInteractionPacket(BlockOperation.Break, 0, 0));
        connection.DrainServerboundPackets(packets);
        connection.TrySend(new InputStatePacket(0, 0, 0, 0));

        Assert.Equal([BlockOperation.Place, BlockOperation.Break],
            packets.Select(packet => Assert.IsType<BlockInteractionPacket>(packet).Operation));
        packets.Clear();
        connection.DrainServerboundPackets(packets);
        Assert.IsType<InputStatePacket>(Assert.Single(packets));
    }

    [Fact]
    public void CapturedChunkRemainsStableWhenWorldChanges()
    {
        var chunk = new Chunk(default);
        var position = new BlockPos(1, 2, 3);
        chunk.SetBlock(position, BuiltinBlocks.Stone.DefaultState);
        var snapshot = InitialWorldStatePacket.ChunkSnapshot.Capture(chunk);

        chunk.SetBlock(position, BuiltinBlocks.Air.DefaultState);

        var index = position.X | (position.Z << Chunk.BitsX) | (position.Y << (Chunk.BitsX + Chunk.BitsZ));
        Assert.Same(BuiltinBlocks.Stone.DefaultState, snapshot.GetBlock(index));
    }

    [Fact]
    public void CompletionFaultWinsOverAlreadyQueuedMessages()
    {
        var connection = new LocalConnection();
        var error = new InvalidOperationException("server failed");
        connection.Publish(new WorldUpdatePacket(1, default, [], []));
        connection.Complete(error);

        Assert.Same(error, Assert.Throws<InvalidOperationException>(() => connection.WaitForPacket()));
        Assert.Same(error, Assert.Throws<InvalidOperationException>(() => connection.TryReceive(out _)));
        Assert.Same(error, Assert.Throws<InvalidOperationException>(() =>
            connection.TrySend(new InputStatePacket(0, 0, 0, 0))));
    }
}
