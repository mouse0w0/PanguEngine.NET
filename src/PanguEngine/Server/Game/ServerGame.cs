using PanguEngine.Network.Packets;
using PanguEngine.Network.Packets.Clientbound;
using PanguEngine.Network.Packets.Serverbound;
using PanguEngine.Server.World;
using PanguEngine.World.Blocks;
using PanguEngine.World.Entities;
using PanguEngine.World.Entities.Components;
using PanguEngine.World.Interaction;
using Silk.NET.Maths;
using static PanguEngine.Network.Packets.Clientbound.WorldUpdatePacket;

namespace PanguEngine.Server.Game;

/// <summary>Owns the authoritative game state and player request processing.</summary>
public sealed class ServerGame
{
    internal const double StepSeconds = 1d / 20;
    private readonly PlayerFreeFlightUpdater _movement = new();
    private readonly Dictionary<uint, PlayerEntity> _players = [];
    private List<BlockChange> _changes = [];

    internal ServerGame()
    {
        World = new ServerWorld();
        FlatWorldGenerator.Generate(World);
    }

    /// <summary>The authoritative world, accessed only from the server thread.</summary>
    public ServerWorld World { get; }

    internal long Tick { get; private set; }

    /// <summary>Creates a player entity in the authoritative world.</summary>
    internal PlayerEntity SpawnPlayerEntity()
    {
        var position = new Vector3D<double>(8, 22, 24);
        var entity = World.Entities.Spawn(BuiltinEntityTypes.Player, new EntitySpawnBuilder()
            .Add(BuiltinEntityComponents.Position, new PositionComponent(position, position))
            .Add(BuiltinEntityComponents.Rotation, new RotationComponent(-90, -20))
            .Add(BuiltinEntityComponents.MoveIntent, default));
        _players.Add(entity.NetworkId, entity);
        return entity;
    }

    /// <summary>Removes a player entity from the authoritative world.</summary>
    internal void RemovePlayerEntity(PlayerEntity entity)
    {
        World.Entities.Destroy(entity.Handle);
        _players.Remove(entity.NetworkId);
    }

    internal InitialWorldStatePacket CreateInitial(uint playerNetworkId) =>
        new(Tick, playerNetworkId, StepSeconds, CapturePlayers(), World.Chunks.Count);

    internal IEnumerable<ChunkDataPacket> CaptureChunks()
    {
        foreach (var chunk in World.Chunks.EnumerateChunks())
            yield return ChunkDataPacket.Capture(chunk);
    }

    internal void BeginTick() => _changes = [];

    internal IReadOnlyList<InteractionResult> Apply(Player player, IReadOnlyList<ServerboundPacket> packets, bool shutdown = false)
    {
        var results = new List<InteractionResult>();
        var context = new ServerPacketContext(this, player, results);
        foreach (var packet in packets)
        {
            if (shutdown && !IsFinalInput(packet))
                continue;
            packet.Type.Handle(context, packet);
        }
        return results;
    }

    /// <summary>Identifies gameplay input that is still applied during the final server round.</summary>
    internal static bool IsFinalInput(ServerboundPacket packet) => packet is InputStatePacket or BlockInteractionPacket;

    internal static void ApplyInput(PlayerEntity player, InputStatePacket input)
    {
        player.SetMoveIntent(new MoveIntentComponent(input.Forward, input.Right));
        player.SetRotation(new RotationComponent(input.Yaw, input.Pitch));
    }

    internal WorldUpdatePacket FinishTick(bool advance = true)
    {
        if (advance)
            Advance();
        return new WorldUpdatePacket(Tick, CapturePlayers(), _changes, []);
    }

    internal void ClearInput()
    {
        foreach (var player in _players.Values)
            player.SetMoveIntent(default);
    }

    private PlayerState[] CapturePlayers() =>
        _players.Select(entry => new PlayerState(entry.Key, GetPose(entry.Value))).ToArray();

    internal void Destroy() => World.Destroy();

    private void Advance()
    {
        _movement.Update(World.Entities);
        Tick++;
    }

    private static PlayerPose GetPose(PlayerEntity player)
    {
        var rotation = player.Rotation;
        return new PlayerPose(player.Position.Current, rotation.Yaw, rotation.Pitch);
    }

    internal InteractionResult ApplyInteraction(PlayerEntity player, BlockInteractionPacket packet)
    {
        var ray = new Ray3D<double>(player.Position.Current, EntityPoseMath.GetForward(packet.Yaw, packet.Pitch));
        if (!BlockRaycaster.TryRaycast(World, ray, 5, out var hit))
            return new InteractionResult(packet.Operation, InteractionStatus.NoHit, default);

        var position = packet.Operation == BlockOperation.Break
            ? hit.BlockPosition
            : hit.BlockPosition.Offset(hit.Face);
        if (packet.Operation == BlockOperation.Place && !World.GetBlock(position).IsAir)
            return new InteractionResult(packet.Operation, InteractionStatus.Occupied, position);

        var state = packet.Operation == BlockOperation.Break
            ? BuiltinBlocks.Air.DefaultState
            : BuiltinBlocks.Stone.DefaultState;
        World.SetBlock(position, state);
        _changes.Add(new BlockChange(position, state));
        return new InteractionResult(packet.Operation, InteractionStatus.Success, position);
    }
}
