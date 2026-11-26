using System.Buffers;
using PanguEngine.Network.Packets;
using PanguEngine.Network.Packets.Clientbound;
using PanguEngine.Network.Packets.Serverbound;
using PanguEngine.Network.Serialization;
using PanguEngine.Registries;

namespace PanguEngine.Network;

internal static class PacketCodec
{
    internal const int ProtocolVersion = 9;

    internal static ReadOnlyMemory<byte> Encode(ConnectionPhase phase, ServerboundPacket packet)
    {
        if (phase == ConnectionPhase.Play)
        {
            var type = packet.Type;
            return Encode(packet, (uint)BuiltinIdTables.ServerboundPacket.GetId(type), type.Write);
        }

        return Encode(packet, (phase, packet) switch
        {
            (ConnectionPhase.Handshake, ClientHelloPacket) => 2,
            (ConnectionPhase.Login, LoginRequestPacket) => 2,
            (ConnectionPhase.Configuration, ClientModsPacket) => 2,
            (ConnectionPhase.Configuration, ContentReadyPacket) => 3,
            _ => throw new InvalidOperationException("Serverbound packet is not registered in the current phase.")
        }, Packet.Write);
    }

    internal static ReadOnlyMemory<byte> Encode(ConnectionPhase phase, ClientboundPacket packet)
    {
        if (phase == ConnectionPhase.Play)
        {
            var type = packet.Type;
            return Encode(packet, (uint)BuiltinIdTables.ClientboundPacket.GetId(type), type.Write);
        }

        return Encode(packet, (phase, packet) switch
        {
            (ConnectionPhase.Handshake or ConnectionPhase.Login or ConnectionPhase.Configuration, DisconnectPacket) => 1,
            (ConnectionPhase.Handshake, LoginStartPacket) => 2,
            (ConnectionPhase.Login, LoginSuccessPacket) => 2,
            (ConnectionPhase.Configuration, ContentManifestPacket) => 2,
            (ConnectionPhase.Configuration, PlayStartPacket) => 3,
            _ => throw new InvalidOperationException("Clientbound packet is not registered in the current phase.")
        }, Packet.Write);
    }

    private static ReadOnlyMemory<byte> Encode<TPacket>(TPacket packet, uint id, PacketWriter<TPacket> write) where TPacket : Packet
    {
        var buffer = new PacketBuffer();
        var writer = new Writer(buffer);
        writer.WriteVarUInt32(id);
        write(packet, ref writer);
        return buffer.WrittenMemory;
    }

    internal static ServerboundPacket ReadServerbound(ConnectionPhase phase, ReadOnlySpan<byte> frame)
    {
        var reader = new Reader(frame);
        var id = reader.ReadVarUInt32();
        var packet = phase == ConnectionPhase.Play
            ? ReadPlayServerbound(id, ref reader)
            : (phase, id) switch
        {
            (ConnectionPhase.Handshake, 2) => ClientHelloPacket.Read(ref reader),
            (ConnectionPhase.Login, 2) => LoginRequestPacket.Read(ref reader),
            (ConnectionPhase.Configuration, 2) => ClientModsPacket.Read(ref reader),
            (ConnectionPhase.Configuration, 3) => ContentReadyPacket.Read(ref reader),
            _ => throw new InvalidDataException("Unknown serverbound message identifier in the current phase.")
        };
        reader.EnsureEnd();
        return packet;
    }

    internal static ClientboundPacket ReadClientbound(ConnectionPhase phase, ReadOnlySpan<byte> frame)
    {
        var reader = new Reader(frame);
        var id = reader.ReadVarUInt32();
        var packet = phase == ConnectionPhase.Play
            ? ReadPlayClientbound(id, ref reader)
            : (phase, id) switch
        {
            (ConnectionPhase.Handshake or ConnectionPhase.Login or ConnectionPhase.Configuration, 1) => DisconnectPacket.Read(ref reader),
            (ConnectionPhase.Handshake, 2) => LoginStartPacket.Read(ref reader),
            (ConnectionPhase.Login, 2) => LoginSuccessPacket.Read(ref reader),
            (ConnectionPhase.Configuration, 2) => ContentManifestPacket.Read(ref reader),
            (ConnectionPhase.Configuration, 3) => PlayStartPacket.Read(ref reader),
            _ => throw new InvalidDataException("Unknown clientbound message identifier in the current phase.")
        };
        reader.EnsureEnd();
        return packet;
    }

    private static ServerboundPacket ReadPlayServerbound(uint id, ref Reader reader)
    {
        var type = GetPacketType(BuiltinIdTables.ServerboundPacket, id);
        var packet = type.Read(ref reader);
        if (!ReferenceEquals(packet.Type, type))
            throw new InvalidOperationException($"Play packet reader for '{type.PacketClass}' returned a different type object.");
        return packet;
    }

    private static ClientboundPacket ReadPlayClientbound(uint id, ref Reader reader)
    {
        var type = GetPacketType(BuiltinIdTables.ClientboundPacket, id);
        var packet = type.Read(ref reader);
        if (!ReferenceEquals(packet.Type, type))
            throw new InvalidOperationException($"Play packet reader for '{type.PacketClass}' returned a different type object.");
        return packet;
    }

    internal static bool HasPlayId(ServerboundPacket packet)
    {
        var type = packet.Type;
        var packetClass = packet.GetType();
        if (type.PacketClass != packetClass)
            throw new InvalidOperationException($"Packet class '{packetClass}' does not return its Play packet type.");
        return HasPlayId(BuiltinIdTables.ServerboundPacket, BuiltinRegistries.ServerboundPacket, type);
    }

    internal static bool HasPlayId(ClientboundPacket packet)
    {
        var type = packet.Type;
        var packetClass = packet.GetType();
        if (type.PacketClass != packetClass)
            throw new InvalidOperationException($"Packet class '{packetClass}' does not return its Play packet type.");
        return HasPlayId(BuiltinIdTables.ClientboundPacket, BuiltinRegistries.ClientboundPacket, type);
    }

    private static bool HasPlayId<T>(RegistryIdTable<T> ids, IRegistry<T> registry, T type) where T : class
    {
        if (ids.TryGetId(type, out _))
            return true;
        if (!registry.TryGetKey(type, out _))
            throw new InvalidOperationException($"Play packet type is not registered in '{ids.Key}'.");
        return false;
    }

    private static T GetPacketType<T>(RegistryIdTable<T> table, uint id) where T : class =>
        id <= int.MaxValue && table.TryGet((int)id, out var type) ? type :
            throw new InvalidDataException($"Unknown Play packet identifier {id} in '{table.Key}'.");

    private sealed class PacketBuffer : IBufferWriter<byte>
    {
        private readonly ArrayBufferWriter<byte> _buffer = new();
        internal ReadOnlyMemory<byte> WrittenMemory => _buffer.WrittenMemory;
        public void Advance(int count) => _buffer.Advance(count);

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            var remaining = NetworkLimits.MaxFrameBytes - _buffer.WrittenCount;
            if (Math.Max(1, sizeHint) > remaining)
                throw new InvalidDataException("Encoded message exceeds the 1 MiB frame limit.");
            var memory = _buffer.GetMemory(sizeHint);
            return memory[..Math.Min(memory.Length, remaining)];
        }

        public Span<byte> GetSpan(int sizeHint = 0) => GetMemory(sizeHint).Span;
    }
}
