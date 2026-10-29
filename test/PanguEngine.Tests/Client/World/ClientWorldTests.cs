using PanguEngine.Client.World;
using PanguEngine.Tests.World.Entities;
using PanguEngine.World.Blocks;
using PanguEngine.World.Chunking;
using PanguEngine.World.Entities.Components;
using PanguEngine.Network.Packets;
using PanguEngine.Network.Packets.Clientbound;
using Silk.NET.Maths;

namespace PanguEngine.Tests.Client.World;

public sealed class ClientWorldTests
{
    [Fact]
    public void ConstructorCreatesEmptyWorld()
    {
        var world = new ClientWorld();

        Assert.Empty(world.Chunks.EnumerateChunks());
    }

    [Fact]
    public void IsAirReflectsStoredBlockState()
    {
        var world = new ClientWorld();
        var position = new BlockPos(32, 4, 32);

        Assert.True(world.IsAir(position));

        world.SetBlock(position, BuiltinBlocks.Stone.DefaultState);

        Assert.False(world.IsAir(position));
    }

    [Fact]
    public void SetBlockNotifiesAfterStateIsStored()
    {
        var world = new ClientWorld();
        var position = new BlockPos(32, 4, 32);
        var notifications = new List<BlockPos>();
        BlockState? observedState = null;
        world.BlockChanged += changedPosition =>
        {
            notifications.Add(changedPosition);
            observedState = world.GetBlock(changedPosition);
        };

        world.SetBlock(position, BuiltinBlocks.Stone.DefaultState);

        Assert.Equal([position], notifications);
        Assert.Same(BuiltinBlocks.Stone.DefaultState, observedState);
    }

    [Fact]
    public void InitialSnapshotEstablishesStateWithoutEditNotifications()
    {
        var chunkPosition = new ChunkPos(2, 1, -3);
        var localPosition = new BlockPos(1, 2, 3);
        var states = new BlockState[Chunk.Volume];
        Array.Fill(states, BuiltinBlocks.Air.DefaultState);
        var index = localPosition.X | (localPosition.Z << Chunk.BitsX) | (localPosition.Y << (Chunk.BitsX + Chunk.BitsZ));
        states[index] = BuiltinBlocks.Stone.DefaultState;
        var position = new BlockPos(
            chunkPosition.X * Chunk.SizeX + localPosition.X,
            chunkPosition.Y * Chunk.SizeY + localPosition.Y,
            chunkPosition.Z * Chunk.SizeZ + localPosition.Z);
        var world = new ClientWorld();
        var notifications = 0;
        world.BlockChanged += _ => notifications++;
        world.ApplyInitial(new InitialWorldStatePacket(7, Guid.NewGuid(), default, 0.05,
            [new InitialWorldStatePacket.ChunkSnapshot(chunkPosition, states)]));

        Assert.Same(BuiltinBlocks.Stone.DefaultState, world.GetBlock(position));
        Assert.Equal(0, notifications);
        Assert.Equal(7L, world.Tick);
    }

    [Fact]
    public void PlayerReplicaHasIndependentStorageAndLifetime()
    {
        using var context = EntityTestContext.Create();
        var source = context.SpawnPlayer(new Vector3D<double>(8, 22, 24));
        var initial = new InitialWorldStatePacket(7, Guid.NewGuid(),
            new PlayerPose(source.Position.Current, source.Rotation.Yaw, source.Rotation.Pitch), 0.05, []);
        var world = new ClientWorld();
        world.ApplyInitial(initial);

        Assert.Equal(initial.PlayerId, world.PlayerId);
        Assert.Same(source.Type, world.Player.Type);
        Assert.NotSame(source, world.Player);
        var pose = new PlayerPose(new Vector3D<double>(9, 22, 24), 45, 10);
        world.Apply(new WorldUpdatePacket(8, pose, [], []));

        Assert.Equal(pose.Position, world.Player.Position.Current);
        Assert.Equal(new RotationComponent(pose.Yaw, pose.Pitch), world.Player.Rotation);
        Assert.Equal(initial.Player.Position, source.Position.Current);
        Assert.Equal(new RotationComponent(initial.Player.Yaw, initial.Player.Pitch), source.Rotation);
        world.Destroy();
        Assert.False(world.Player.IsAlive);
        Assert.True(source.IsAlive);
    }
}
