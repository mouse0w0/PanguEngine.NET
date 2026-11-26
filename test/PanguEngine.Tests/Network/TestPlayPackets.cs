using PanguEngine.Network.Packets;
using PanguEngine.Registries;

namespace PanguEngine.Tests.Network;

internal static class TestPlayPackets
{
    private static readonly ServerboundPacketType EchoRequestType = ServerboundPacketType.Create(
        static (ref reader) => new EchoRequest(reader.ReadInt32()),
        static (packet, ref writer) => writer.WriteInt32(packet.Value),
        static (player, packet) => player.TrySendPacket(new EchoReply(player.Username, packet.Value)));

    private static readonly ClientboundPacketType EchoReplyType = ClientboundPacketType.Create(
        static (ref reader) => new EchoReply(reader.ReadString(32), reader.ReadInt32()),
        static (packet, ref writer) =>
        {
            writer.WriteString(packet.Username);
            writer.WriteInt32(packet.Value);
        }, static (_, _) => { });

    private static readonly ServerboundPacketType UnusedRequestType = ServerboundPacketType.Create(
        static (ref _) => new UnusedRequest(), static (_, ref _) => { }, static (_, _) => { });

    internal static void Register(RegistryManager manager)
    {
        var serverbound = manager.GetWritable<ServerboundPacketType>(RegistryKeys.ServerboundPacket);
        var clientbound = manager.GetWritable<ClientboundPacketType>(RegistryKeys.ClientboundPacket);
        serverbound.Register(ResourceKey.Create("test", "echo_request"), EchoRequestType);
        clientbound.Register(ResourceKey.Create("test", "echo_reply"), EchoReplyType);
        serverbound.Register(ResourceKey.Create("test", "unused_request"), UnusedRequestType);
    }

    internal sealed record EchoRequest(int Value) : ServerboundPacket
    {
        public override ServerboundPacketType Type => EchoRequestType;
    }

    internal sealed record EchoReply(string Username, int Value) : ClientboundPacket
    {
        public override ClientboundPacketType Type => EchoReplyType;
    }

    internal sealed record UnusedRequest : ServerboundPacket
    {
        public override ServerboundPacketType Type => UnusedRequestType;
    }
}
