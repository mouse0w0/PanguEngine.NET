using Microsoft.Extensions.Logging.Abstractions;
using PanguEngine.Client.Rendering.World;
using PanguEngine.Client.Resources.Models;
using PanguEngine.Client.World;
using PanguEngine.Registries;
using PanguEngine.Resources;
using PanguEngine.World.Blocks;
using PanguEngine.World.Chunking;

namespace PanguEngine.Tests.Client.Rendering.World;

public sealed class ChunkMeshBuildQueueTests
{
    [Fact]
    public void TaskReservationsAreBounded()
    {
        using var resources = new ResourceManager([]);
        var queue = new ChunkMeshBuildQueue(CreateModels(resources), 1, 2, 2);
        try
        {
            Assert.True(queue.TryReserveTaskSlot());
            Assert.True(queue.TryReserveTaskSlot());
            Assert.False(queue.TryReserveTaskSlot());
            queue.ReleaseReservedTaskSlot();
            queue.ReleaseReservedTaskSlot();
        }
        finally
        {
            queue.Dispose();
        }
    }

    [Fact]
    public void WorkerBuildsCpuMeshFromSnapshot()
    {
        using var resources = new ResourceManager([]);
        var queue = new ChunkMeshBuildQueue(CreateModels(resources), 1, 1, 1);
        try
        {
            using (var batch = new ChunkMeshSnapshotBatch())
            {
                var snapshot = batch.Capture(new ClientWorld(), default);
                Assert.True(queue.TryReserveTaskSlot());
                Assert.True(queue.TryEnqueueReserved(new ChunkMeshBuildTask(snapshot, 1)));
            }
            ChunkMeshBuildResult? result = null;

            var received = SpinWait.SpinUntil(
                () => queue.TryReadResult(out result),
                TimeSpan.FromSeconds(5));

            Assert.True(received);
            var completed = Assert.IsType<ChunkMeshBuildResult>(result);
            Assert.Null(completed.Exception);
            using var mesh = Assert.IsType<ChunkMesh>(completed.Mesh);
            Assert.True(mesh.IsEmpty);
        }
        finally
        {
            queue.Dispose();
        }
    }

    [Fact]
    public void WorkerReturnsBuildFailure()
    {
        using var resources = new ResourceManager([]);
        var queue = new ChunkMeshBuildQueue(CreateModels(resources), 1, 1, 1);
        try
        {
            var world = new ClientWorld();
            world.SetBlock(default, BuiltinBlocks.Stone.DefaultState);
            using (var batch = new ChunkMeshSnapshotBatch())
            {
                var snapshot = batch.Capture(world, default);
                Assert.True(queue.TryReserveTaskSlot());
                Assert.True(queue.TryEnqueueReserved(new ChunkMeshBuildTask(snapshot, 1)));
            }
            ChunkMeshBuildResult? result = null;

            var received = SpinWait.SpinUntil(
                () => queue.TryReadResult(out result),
                TimeSpan.FromSeconds(5));

            Assert.True(received);
            var completed = Assert.IsType<ChunkMeshBuildResult>(result);
            Assert.Null(completed.Mesh);
            Assert.IsType<InvalidOperationException>(completed.Exception?.SourceException);
        }
        finally
        {
            queue.Dispose();
        }
    }

    [Fact]
    public void DestroyStopsAcceptingReservations()
    {
        using var resources = new ResourceManager([]);
        var queue = new ChunkMeshBuildQueue(CreateModels(resources), 1, 1, 1);

        queue.Dispose();

        Assert.False(queue.TryReserveTaskSlot());
    }

    [Fact]
    public void CompletedTaskDoesNotReturnBuffersWhilePublisherIsStillCapturing()
    {
        using var resources = new ResourceManager([]);
        using var queue = new ChunkMeshBuildQueue(CreateModels(resources), 1, 1, 1);
        var world = new ClientWorld();
        world.SetBlock(new BlockPos(16, 0, 0), BuiltinBlocks.Stone.DefaultState);
        using var batch = new ChunkMeshSnapshotBatch();
        var first = batch.Capture(world, default);
        Assert.True(queue.TryReserveTaskSlot());
        Assert.True(queue.TryEnqueueReserved(new ChunkMeshBuildTask(first, 1)));
        ChunkMeshBuildResult? firstResult = null;
        Assert.True(SpinWait.SpinUntil(() => queue.TryReadResult(out firstResult), TimeSpan.FromSeconds(5)));
        var completed = Assert.IsType<ChunkMeshBuildResult>(firstResult);
        Assert.Null(completed.Exception);
        completed.Mesh!.Dispose();

        var second = batch.Capture(world, new ChunkPos(1, 0, 0));

        Assert.Same(BuiltinBlocks.Stone.DefaultState, second.GetBlock(default));
        Assert.True(queue.TryReserveTaskSlot());
        Assert.True(queue.TryEnqueueReserved(new ChunkMeshBuildTask(second, 2)));
        ChunkMeshBuildResult? secondResult = null;
        Assert.True(SpinWait.SpinUntil(() => queue.TryReadResult(out secondResult), TimeSpan.FromSeconds(5)));
        Assert.IsType<InvalidOperationException>(Assert.IsType<ChunkMeshBuildResult>(secondResult).Exception?.SourceException);
    }

    [Fact]
    public void RunningBuildKeepsSnapshotAliveAfterPublisherFinishes()
    {
        using var resources = new ResourceManager([]);
        using var entered = new ManualResetEventSlim();
        using var resume = new ManualResetEventSlim();
        using var queue = new ChunkMeshBuildQueue(CreateModels(resources), 1, 1, 1);
        var world = new ClientWorld();
        world.SetBlock(default, BuiltinBlocks.Stone.DefaultState);
        world.SetBlock(new BlockPos(1, 0, 0), new BlockingOccluder(entered, resume).DefaultState);
        try
        {
            ChunkMeshSnapshot snapshot;
            using (var batch = new ChunkMeshSnapshotBatch())
            {
                snapshot = batch.Capture(world, default);
                Assert.True(queue.TryReserveTaskSlot());
                Assert.True(queue.TryEnqueueReserved(new ChunkMeshBuildTask(snapshot, 1)));
                Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
            }

            Assert.Same(BuiltinBlocks.Stone.DefaultState, snapshot.GetBlock(default));
        }
        finally
        {
            resume.Set();
        }
        ChunkMeshBuildResult? result = null;
        Assert.True(SpinWait.SpinUntil(() => queue.TryReadResult(out result), TimeSpan.FromSeconds(5)));
        Assert.IsType<InvalidOperationException>(Assert.IsType<ChunkMeshBuildResult>(result).Exception?.SourceException);
    }

    private sealed class BlockingOccluder(ManualResetEventSlim entered, ManualResetEventSlim resume) : Block
    {
        public override bool CanOccludeFace(BlockState state, PanguEngine.World.Direction direction)
        {
            entered.Set();
            if (!resume.Wait(TimeSpan.FromSeconds(5)))
                throw new TimeoutException("The test did not release the mesh builder.");
            return true;
        }
    }

    private static BlockModelManager CreateModels(ResourceManager resources)
    {
        var blocks = new Registry<Block>(RegistryKeys.Block);
        return new BlockModelManager(resources, blocks, 4096, NullLogger.Instance);
    }
}
