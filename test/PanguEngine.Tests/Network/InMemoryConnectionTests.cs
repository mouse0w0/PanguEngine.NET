using PanguEngine.World.Blocks;
using PanguEngine.World.Chunking;
using PanguEngine.Network;
using PanguEngine.Network.Packets.Clientbound;
using PanguEngine.Network.Packets.Serverbound;

namespace PanguEngine.Tests.Network;

public sealed class InMemoryConnectionTests
{
    [Fact]
    public void CapturedChunkRemainsStableWhenWorldChanges()
    {
        var chunk = new Chunk(default);
        var position = new BlockPos(1, 2, 3);
        chunk.SetBlock(position, BuiltinBlocks.Stone.DefaultState);
        var snapshot = ChunkDataPacket.Capture(chunk);

        chunk.SetBlock(position, BuiltinBlocks.Air.DefaultState);

        var index = position.X | (position.Z << Chunk.BitsX) | (position.Y << (Chunk.BitsX + Chunk.BitsZ));
        Assert.Same(BuiltinBlocks.Stone.DefaultState, snapshot.GetBlock(index));
    }

    [Fact]
    public void CompletionFaultWinsOverAlreadyQueuedMessages()
    {
        var connection = new InMemoryConnection();
        var error = new InvalidOperationException("server failed");
        connection.Publish(new WorldUpdatePacket(1, [], [], []));
        connection.Complete(error);
        connection.Close();

        Assert.Same(error, Assert.Throws<InvalidOperationException>(() => connection.WaitForPacket()));
        Assert.Same(error, Assert.Throws<InvalidOperationException>(() => connection.TryReceive(out _)));
        Assert.Same(error, Assert.Throws<InvalidOperationException>(connection.ThrowIfFaulted));
        Assert.Same(error, Assert.Throws<InvalidOperationException>(() =>
            connection.TrySend(new InputStatePacket(0, 0, 0, 0))));
    }
}
