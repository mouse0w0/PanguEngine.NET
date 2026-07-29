using Microsoft.Extensions.Logging.Abstractions;
using PanguEngine.Client.Rendering.World;
using PanguEngine.Client.Resources.Models;
using PanguEngine.Client.World;
using PanguEngine.Registries;
using PanguEngine.Resources;
using PanguEngine.World.Blocks;

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
            var snapshot = ChunkMeshSnapshot.Capture(new ClientWorld(), default);
            Assert.True(queue.TryReserveTaskSlot());
            Assert.True(queue.TryEnqueueReserved(new ChunkMeshBuildTask(snapshot, 1)));
            ChunkMeshBuildResult? result = null;

            var received = SpinWait.SpinUntil(
                () => queue.TryReadResult(out result),
                TimeSpan.FromSeconds(5));

            Assert.True(received);
            var completed = Assert.IsType<ChunkMeshBuildResult>(result);
            Assert.Null(completed.Exception);
            var mesh = Assert.IsType<ChunkMesh>(completed.Mesh);
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
            var snapshot = ChunkMeshSnapshot.Capture(world, default);
            Assert.True(queue.TryReserveTaskSlot());
            Assert.True(queue.TryEnqueueReserved(new ChunkMeshBuildTask(snapshot, 1)));
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

    private static BlockModelManager CreateModels(ResourceManager resources)
    {
        var blocks = new Registry<Block>(RegistryKeys.Block);
        return new BlockModelManager(resources, blocks, 4096, NullLogger.Instance);
    }
}