using PanguEngine.Client.Rendering.World;
using PanguEngine.Client.World;
using PanguEngine.World.Blocks;
using PanguEngine.World.Chunking;

namespace PanguEngine.Tests.Client.Rendering.World;

public sealed class ChunkMeshSnapshotTests
{
    [Fact]
    public void CompleteNeighborChunksRemainReadableAfterWorldChanges()
    {
        var world = new ClientWorld();
        var corner = new BlockPos(-16, 31, -16);
        world.SetBlock(corner, BuiltinBlocks.Stone.DefaultState);
        using var batch = new ChunkMeshSnapshotBatch();
        var snapshot = batch.Capture(world, default);

        world.SetBlock(corner, BuiltinBlocks.Air.DefaultState);

        Assert.Same(BuiltinBlocks.Stone.DefaultState, snapshot.GetBlock(corner));
        Assert.Same(BuiltinBlocks.Air.DefaultState, snapshot.GetBlock(new BlockPos(31, 31, 31)));
    }

    [Fact]
    public void OverlappingTasksReuseTheirBatchCaptureButNotOtherBatches()
    {
        var world = new ClientWorld();
        world.SetBlock(default, BuiltinBlocks.Stone.DefaultState);
        using var firstBatch = new ChunkMeshSnapshotBatch();
        var first = firstBatch.Capture(world, default);
        world.SetBlock(default, BuiltinBlocks.Air.DefaultState);

        var overlapping = firstBatch.Capture(world, new ChunkPos(1, 0, 0));
        using var nextBatch = new ChunkMeshSnapshotBatch();
        var fresh = nextBatch.Capture(world, new ChunkPos(1, 0, 0));

        Assert.Same(BuiltinBlocks.Stone.DefaultState, first.GetBlock(default));
        Assert.Same(BuiltinBlocks.Stone.DefaultState, overlapping.GetBlock(new BlockPos(-16, 0, 0)));
        Assert.Same(BuiltinBlocks.Air.DefaultState, fresh.GetBlock(new BlockPos(-16, 0, 0)));
    }
}
