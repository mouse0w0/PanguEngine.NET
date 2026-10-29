using PanguEngine.Server.World;
using PanguEngine.World.Blocks;
using PanguEngine.World.Entities;
using PanguEngine.World.Entities.Components;
using PanguEngine.World.Interaction;
using PanguEngine.Network;
using PanguEngine.Network.Packets;
using PanguEngine.Network.Packets.Clientbound;
using PanguEngine.Network.Packets.Serverbound;
using Silk.NET.Maths;
using static PanguEngine.Network.Packets.Clientbound.WorldUpdatePacket;

namespace PanguEngine.Server.Game;

/// <summary>Owns the authoritative game state and player request processing.</summary>
public sealed class ServerGame
{
    internal const double StepSeconds = 1d / 20;
    private readonly PlayerFreeFlightUpdater _movement = new();
    private PlayerEntity? _player;

    internal ServerGame()
    {
        World = new ServerWorld();
        FlatWorldGenerator.Generate(World);
    }

    /// <summary>The authoritative world, accessed only from the server thread.</summary>
    public ServerWorld World { get; }

    internal long Tick { get; private set; }
    internal Guid PlayerId { get; private set; }

    internal void JoinLocal(LocalConnection connection)
    {
        PlayerId = Guid.NewGuid();
        var position = new Vector3D<double>(8, 22, 24);
        _player = World.Entities.Spawn(BuiltinEntityTypes.Player, new EntitySpawnBuilder()
            .Add(BuiltinEntityComponents.Position, new PositionComponent(position, position))
            .Add(BuiltinEntityComponents.Rotation, new RotationComponent(-90, -20))
            .Add(BuiltinEntityComponents.MoveIntent, default));
        var chunks = new List<InitialWorldStatePacket.ChunkSnapshot>(World.Chunks.Count);
        foreach (var chunk in World.Chunks.EnumerateChunks())
            chunks.Add(InitialWorldStatePacket.ChunkSnapshot.Capture(chunk));
        connection.Publish(new InitialWorldStatePacket(Tick, PlayerId, GetPose(_player), StepSeconds, chunks));
    }

    internal WorldUpdatePacket? Update(IReadOnlyList<ServerboundPacket> packets, bool advance = true)
    {
        if (_player is not { } player)
        {
            if (advance)
                Advance();
            return null;
        }

        var changes = new List<BlockChange>();
        var results = new List<InteractionResult>();
        foreach (var packet in packets)
        {
            if (packet is InputStatePacket input)
            {
                player.SetMoveIntent(new MoveIntentComponent(input.Forward, input.Right));
                player.SetRotation(new RotationComponent(input.Yaw, input.Pitch));
            }
            else if (packet is BlockInteractionPacket interaction)
                results.Add(Interact(player, interaction, changes));
        }
        if (advance)
            Advance();
        return new WorldUpdatePacket(Tick, GetPose(player), changes, results);
    }

    internal void ClearInput() => _player?.SetMoveIntent(default);

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

    private InteractionResult Interact(PlayerEntity player, BlockInteractionPacket packet, List<BlockChange> changes)
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
        changes.Add(new BlockChange(position, state));
        return new InteractionResult(packet.Operation, InteractionStatus.Success, position);
    }
}
