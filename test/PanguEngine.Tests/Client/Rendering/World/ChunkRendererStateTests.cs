using PanguEngine.Client.Rendering.World;
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
}