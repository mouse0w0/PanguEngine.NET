using PanguEngine.Network.Packets;
using PanguEngine.Network.Packets.Clientbound;
using PanguEngine.Network.Packets.Serverbound;
using PanguEngine.Registries;
using PanguEngine.Server.Game;

namespace PanguEngine.Network;

/// <summary>Registers the standard Play packet definitions in the engine registries.</summary>
internal static class BuiltinPackets
{
    internal static readonly ServerboundPacketType InputState = ServerboundPacketType.CreateWithContext(
        InputStatePacket.Read, Packet.Write,
        static (context, packet) => ServerGame.ApplyInput(context.Player.Entity, packet));

    internal static readonly ServerboundPacketType BlockInteraction = ServerboundPacketType.CreateWithContext(
        BlockInteractionPacket.Read, Packet.Write,
        static (context, packet) => context.Results.Add(context.Game.ApplyInteraction(context.Player.Entity, packet)));

    internal static readonly ServerboundPacketType ServerboundHeartbeat = ServerboundPacketType.CreateWithContext(
        static (ref _) => new ServerboundHeartbeatPacket(), static (_, ref _) => { }, static (_, _) => { });

    internal static readonly ClientboundPacketType Disconnect = ClientboundPacketType.CreateWithContext(
        DisconnectPacket.Read, Packet.Write, static (_, packet) => throw new ConnectionClosedException(packet.Reason));

    internal static readonly ClientboundPacketType InitialWorldState = ClientboundPacketType.CreateWithContext(
        InitialWorldStatePacket.Read, Packet.Write, static (context, packet) => context.World.ApplyInitial(packet));

    internal static readonly ClientboundPacketType ChunkData = ClientboundPacketType.CreateWithContext(
        ChunkDataPacket.Read, Packet.Write, static (context, packet) => context.World.ApplyChunk(packet));

    internal static readonly ClientboundPacketType InitialWorldComplete = ClientboundPacketType.CreateWithContext(
        InitialWorldCompletePacket.Read, Packet.Write, static (_, _) => { });

    internal static readonly ClientboundPacketType WorldUpdate = ClientboundPacketType.CreateWithContext(
        WorldUpdatePacket.Read, Packet.Write, static (context, packet) => context.Session!.ApplyWorldUpdate(packet, context.Now));

    internal static readonly ClientboundPacketType ClientboundHeartbeat = ClientboundPacketType.CreateWithContext(
        static (ref _) => new ClientboundHeartbeatPacket(), static (_, ref _) => { }, static (_, _) => { });

    internal static void Register(IWritableRegistry<ServerboundPacketType> serverbound,
        IWritableRegistry<ClientboundPacketType> clientbound)
    {
        serverbound.Register(Key("input_state"), InputState);
        serverbound.Register(Key("block_interaction"), BlockInteraction);
        serverbound.Register(Key("heartbeat"), ServerboundHeartbeat);

        clientbound.Register(Key("disconnect"), Disconnect);
        clientbound.Register(Key("initial_world_state"), InitialWorldState);
        clientbound.Register(Key("chunk_data"), ChunkData);
        clientbound.Register(Key("initial_world_complete"), InitialWorldComplete);
        clientbound.Register(Key("world_update"), WorldUpdate);
        clientbound.Register(Key("heartbeat"), ClientboundHeartbeat);
    }

    private static ResourceKey Key(string name) => ResourceKey.Create("pangu", name);
}
