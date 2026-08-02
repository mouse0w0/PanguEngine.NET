using PanguEngine.Client.Rendering.World;
using PanguEngine.Graphics;
using PanguEngine.World.Chunking;
using Silk.NET.Maths;

namespace PanguEngine.Tests.Client.Rendering.World;

public sealed class ChunkRendererStateTests
{
    [Fact]
    public void RepeatedInvalidationKeepsOneDirtyEntryAndAdvancesVersion()
    {
        var state = new ChunkMeshUpdateState();
        var position = new ChunkPos(1, 2, 3);
        state.Register(position);
        state.Invalidate(position);
        state.Invalidate(position);
        var tickets = new ChunkMeshBuildTicket[1];

        Assert.Equal(1, state.DirtyCount);
        Assert.Equal(1, state.CollectNearest(default, tickets));
        var ticket = tickets[0];
        Assert.Equal(position, ticket.Position);
        Assert.Equal(2, ticket.Version);
    }

    [Fact]
    public void InvalidationDuringFlightSchedulesLatestVersionAfterCompletion()
    {
        var state = new ChunkMeshUpdateState();
        var position = new ChunkPos(0, 0, 0);
        state.Register(position);
        state.Invalidate(position);
        var tickets = new ChunkMeshBuildTicket[1];
        Assert.Equal(1, state.CollectNearest(default, tickets));
        var first = tickets[0];
        state.MarkDispatched(first);

        state.Invalidate(position);

        Assert.Equal(0, state.CollectNearest(default, tickets));
        Assert.False(state.Complete(first.Position, first.Version));
        Assert.Equal(1, state.CollectNearest(default, tickets));
        var second = tickets[0];
        Assert.Equal(2, second.Version);
    }

    [Fact]
    public void InvalidationIgnoresUnknownChunk()
    {
        var state = new ChunkMeshUpdateState();

        state.Invalidate(new ChunkPos(1, 0, 0));

        Assert.Equal(0, state.DirtyCount);
    }

    [Fact]
    public void CollectsNearestDirtyChunksInDistanceOrder()
    {
        var state = new ChunkMeshUpdateState();
        var near = new ChunkPos(0, 0, 0);
        var middle = new ChunkPos(2, 0, 0);
        var far = new ChunkPos(4, 0, 0);
        state.Register(far);
        state.Register(middle);
        state.Register(near);
        state.Invalidate(far);
        state.Invalidate(middle);
        state.Invalidate(near);
        var cameraPosition = new Vector3D<double>(8, 8, 8);
        var tickets = new ChunkMeshBuildTicket[2];

        Assert.Equal(2, state.CollectNearest(cameraPosition, tickets));
        Assert.Equal(near, tickets[0].Position);
        Assert.Equal(middle, tickets[1].Position);
    }

    [Fact]
    public void CurrentCompletionClearsInFlightWithoutRequeueing()
    {
        var state = new ChunkMeshUpdateState();
        var position = new ChunkPos(0, 0, 0);
        state.Register(position);
        state.Invalidate(position);
        var tickets = new ChunkMeshBuildTicket[1];
        Assert.Equal(1, state.CollectNearest(default, tickets));
        var ticket = tickets[0];
        state.MarkDispatched(ticket);

        var isCurrent = state.Complete(ticket.Position, ticket.Version);

        Assert.True(isCurrent);
        Assert.False(state.IsDirty(position));
        Assert.False(state.IsInFlight(position));
    }

    [Fact]
    public void DirtyEntryRemainsUntilDispatchIsConfirmed()
    {
        var state = new ChunkMeshUpdateState();
        var position = new ChunkPos(0, 0, 0);
        state.Register(position);
        state.Invalidate(position);
        var tickets = new ChunkMeshBuildTicket[1];
        Assert.Equal(1, state.CollectNearest(default, tickets));
        var ticket = tickets[0];

        Assert.True(state.IsDirty(position));

        state.MarkDispatched(ticket);

        Assert.False(state.IsDirty(position));
        Assert.True(state.IsInFlight(position));
    }

    [Fact]
    public void ReleasedInstanceSlotIsReused()
    {
        var slots = new ChunkInstanceSlotPool();
        Assert.Equal(0u, slots.Acquire());
        Assert.Equal(1u, slots.Acquire());

        slots.Release(0);

        Assert.Equal(0u, slots.Acquire());
    }

    [Fact]
    public void IndirectCommandsAreGroupedByPageWithStableInstanceSlots()
    {
        var device = new TestGraphicsDevice(16);
        var arena = new ChunkMeshArena(device, 8, 8);
        var first = arena.Allocate(CreateMesh(2, 3));
        var second = arena.Allocate(CreateMesh(2, 4));
        var otherPage = arena.Allocate(CreateMesh(6, 2));
        ChunkDrawCandidate[] candidates =
        [
            new ChunkDrawCandidate(
                first.Page,
                first.IndexCount,
                first.FirstIndex,
                checked((int)first.VertexOffset),
                5),
            new ChunkDrawCandidate(
                otherPage.Page,
                otherPage.IndexCount,
                otherPage.FirstIndex,
                checked((int)otherPage.VertexOffset),
                9),
            new ChunkDrawCandidate(
                second.Page,
                second.IndexCount,
                second.FirstIndex,
                checked((int)second.VertexOffset),
                7)
        ];
        List<IndexedIndirectDrawArguments> commands = [];
        List<ChunkPageDrawRange> ranges = [];

        var builder = new ChunkIndirectDrawBuilder();

        builder.Build(candidates, commands, ranges);

        Assert.Equal(3, commands.Count);
        Assert.Equal(2, ranges.Count);
        Assert.Equal(0ul, ranges[0].Offset);
        Assert.Equal(2u, ranges[0].DrawCount);
        Assert.Equal(40ul, ranges[1].Offset);
        Assert.Equal(1u, ranges[1].DrawCount);
        Assert.Equal(first.IndexCount, commands[0].IndexCount);
        Assert.Equal(1u, commands[0].InstanceCount);
        Assert.Equal(first.FirstIndex, commands[0].FirstIndex);
        Assert.Equal(checked((int)first.VertexOffset), commands[0].VertexOffset);
        Assert.Equal(5u, commands[0].FirstInstance);
        Assert.Equal(second.IndexCount, commands[1].IndexCount);
        Assert.Equal(1u, commands[1].InstanceCount);
        Assert.Equal(second.FirstIndex, commands[1].FirstIndex);
        Assert.Equal(checked((int)second.VertexOffset), commands[1].VertexOffset);
        Assert.Equal(7u, commands[1].FirstInstance);
        Assert.Equal(9u, commands[2].FirstInstance);
    }

    [Fact]
    public void EmptyMeshTransitionReleasesAllocationAndSlot()
    {
        var device = new TestGraphicsDevice(16);
        var arena = new ChunkMeshArena(device, 8, 8);
        var record = new ChunkMeshRecord(arena.Allocate(CreateMesh(2, 2)), 7);

        var transition = ChunkMeshRecordTransitionPlanner.Plan(record, CreateMesh(0, 0));

        Assert.Equal(ChunkMeshRecordTransitionKind.Remove, transition.Kind);
        Assert.Equal(7u, transition.InstanceSlot);
    }

    [Fact]
    public void OversizedReplacementTransitionPreservesInstanceSlot()
    {
        var device = new TestGraphicsDevice(16);
        var arena = new ChunkMeshArena(device, 8, 8);
        var record = new ChunkMeshRecord(arena.Allocate(CreateMesh(2, 2)), 7);

        var transition = ChunkMeshRecordTransitionPlanner.Plan(record, CreateMesh(3, 2));

        Assert.Equal(ChunkMeshRecordTransitionKind.Replace, transition.Kind);
        Assert.Equal(7u, transition.InstanceSlot);
    }

    private static ChunkMesh CreateMesh(int vertexCount, int indexCount)
    {
        return new ChunkMesh(new ChunkVertex[vertexCount], new uint[indexCount]);
    }
}
