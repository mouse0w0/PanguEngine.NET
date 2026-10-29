using PanguEngine.World;
using PanguEngine.World.Blocks;
using PanguEngine.World.Chunking;
using PanguEngine.World.Entities;
using PanguEngine.World.Entities.Components;
using PanguEngine.Network.Packets.Clientbound;

namespace PanguEngine.Client.World;

/// <summary>
/// Stores an independent display copy of the authoritative world.
/// </summary>
public sealed class ClientWorld : IReadOnlyBlockAccessor
{
    /// <summary>The chunks that store this world's block state.</summary>
    public ChunkManager Chunks { get; } = new();
    internal EntityManager Entities { get; } = new();
    internal Guid PlayerId { get; private set; }

    /// <summary>The latest authoritative simulation tick applied to this display copy.</summary>
    public long Tick { get; private set; }

    /// <summary>The controlled player entity in the client world.</summary>
    public PlayerEntity Player { get; private set; } = null!;

    /// <summary>Raised after a block state is stored.</summary>
    internal event Action<BlockPos>? BlockChanged;

    /// <summary>
    /// Gets a block state by world block position.
    /// </summary>
    /// <param name="position">The world block position.</param>
    /// <returns>The stored block state, or air when no chunk exists.</returns>
    public BlockState GetBlock(BlockPos position)
    {
        return Chunks.GetBlock(position);
    }

    /// <summary>
    /// Sets a block state by world block position.
    /// </summary>
    /// <param name="position">The world block position.</param>
    /// <param name="state">The block state to store.</param>
    internal void SetBlock(BlockPos position, BlockState state)
    {
        Chunks.SetBlock(position, state);
        BlockChanged?.Invoke(position);
    }

    /// <summary>
    /// Gets whether the block at the specified position is air.
    /// </summary>
    /// <param name="position">The world block position.</param>
    /// <returns>Whether the block state at the position is air.</returns>
    public bool IsAir(BlockPos position)
    {
        return GetBlock(position).IsAir;
    }

    /// <summary>
    /// Destroys client entities and releases the display world's change subscriptions.
    /// </summary>
    public void Destroy()
    {
        Entities.Destroy();
        BlockChanged = null;
    }

    internal void ApplyInitial(InitialWorldStatePacket initial)
    {
        Tick = initial.Tick;
        PlayerId = initial.PlayerId;
        Player = Entities.Spawn(BuiltinEntityTypes.Player, new EntitySpawnBuilder()
            .Add(BuiltinEntityComponents.Position, new PositionComponent(initial.Player.Position, initial.Player.Position))
            .Add(BuiltinEntityComponents.Rotation, new RotationComponent(initial.Player.Yaw, initial.Player.Pitch))
            .Add(BuiltinEntityComponents.MoveIntent, default));
        foreach (var snapshot in initial.Chunks)
        {
            var origin = snapshot.Position;
            var index = 0;
            for (var y = 0; y < Chunk.SizeY; y++)
            for (var z = 0; z < Chunk.SizeZ; z++)
            for (var x = 0; x < Chunk.SizeX; x++)
                Chunks.SetBlock(new BlockPos(origin.X * Chunk.SizeX + x,
                    origin.Y * Chunk.SizeY + y, origin.Z * Chunk.SizeZ + z), snapshot.GetBlock(index++));
        }
    }

    internal void Apply(WorldUpdatePacket update)
    {
        Tick = update.Tick;
        Entities.SetComponent(Player.Handle, BuiltinEntityComponents.Position,
            new PositionComponent(Player.Position.Current, update.Player.Position));
        Player.SetRotation(new RotationComponent(update.Player.Yaw, update.Player.Pitch));
        foreach (var change in update.Changes)
            Chunks.SetBlock(change.Position, change.State);
        foreach (var change in update.Changes)
            BlockChanged?.Invoke(change.Position);
    }
}
