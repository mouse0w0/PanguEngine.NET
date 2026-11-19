using System.Buffers;
using PanguEngine.Network.Packets;
using PanguEngine.Network.Packets.Clientbound;
using PanguEngine.Network.Packets.Serverbound;
using PanguEngine.Network.Serialization;

namespace PanguEngine.Network;

internal static class PacketCodec
{
    internal const int ProtocolVersion = 8;

    internal static ReadOnlyMemory<byte> Encode(ConnectionPhase phase, ServerboundPacket packet)
    {
        var buffer = new PacketBuffer();
        var writer = new Writer(buffer);
        writer.WriteVarUInt32((phase, packet) switch
        {
            (ConnectionPhase.Handshake, ClientHelloPacket) => 2,
            (ConnectionPhase.Login, LoginRequestPacket) => 2,
            (ConnectionPhase.Configuration, ClientModsPacket) => 2,
            (ConnectionPhase.Configuration, ContentReadyPacket) => 3,
            (ConnectionPhase.Play, InputStatePacket) => 2,
            (ConnectionPhase.Play, BlockInteractionPacket) => 3,
            _ => throw new InvalidOperationException("Serverbound packet is not registered in the current phase.")
        });
        packet.Write(ref writer);
        return buffer.WrittenMemory;
    }

    internal static ReadOnlyMemory<byte> Encode(ConnectionPhase phase, ClientboundPacket packet)
    {
        var buffer = new PacketBuffer();
        var writer = new Writer(buffer);
        writer.WriteVarUInt32((phase, packet) switch
        {
            (ConnectionPhase.Handshake or ConnectionPhase.Login or ConnectionPhase.Configuration or ConnectionPhase.Play, DisconnectPacket) => 1,
            (ConnectionPhase.Handshake, LoginStartPacket) => 2,
            (ConnectionPhase.Login, LoginSuccessPacket) => 2,
            (ConnectionPhase.Configuration, ContentManifestPacket) => 2,
            (ConnectionPhase.Play, InitialWorldStatePacket) => 2,
            (ConnectionPhase.Play, ChunkDataPacket) => 3,
            (ConnectionPhase.Play, InitialWorldCompletePacket) => 4,
            (ConnectionPhase.Play, WorldUpdatePacket) => 5,
            _ => throw new InvalidOperationException("Clientbound packet is not registered in the current phase.")
        });
        packet.Write(ref writer);
        return buffer.WrittenMemory;
    }

    internal static uint ReadId(ReadOnlySpan<byte> frame)
    {
        var reader = new Reader(frame);
        return reader.ReadVarUInt32();
    }

    internal static ServerboundPacket ReadServerbound(ConnectionPhase phase, ReadOnlySpan<byte> frame)
    {
        var reader = new Reader(frame);
        var packet = (phase, reader.ReadVarUInt32()) switch
        {
            (ConnectionPhase.Handshake, 2) => (ServerboundPacket)ClientHelloPacket.Read(ref reader),
            (ConnectionPhase.Login, 2) => LoginRequestPacket.Read(ref reader),
            (ConnectionPhase.Configuration, 2) => ClientModsPacket.Read(ref reader),
            (ConnectionPhase.Configuration, 3) => ContentReadyPacket.Read(ref reader),
            (ConnectionPhase.Play, 2) => InputStatePacket.Read(ref reader),
            (ConnectionPhase.Play, 3) => BlockInteractionPacket.Read(ref reader),
            _ => throw new InvalidDataException("Unknown serverbound message identifier in the current phase.")
        };
        reader.EnsureEnd();
        return packet;
    }

    internal static ClientboundPacket ReadClientbound(ConnectionPhase phase, ReadOnlySpan<byte> frame)
    {
        var reader = new Reader(frame);
        var packet = (phase, reader.ReadVarUInt32()) switch
        {
            (ConnectionPhase.Handshake or ConnectionPhase.Login or ConnectionPhase.Configuration or ConnectionPhase.Play, 1) => (ClientboundPacket)DisconnectPacket.Read(ref reader),
            (ConnectionPhase.Handshake, 2) => LoginStartPacket.Read(ref reader),
            (ConnectionPhase.Login, 2) => LoginSuccessPacket.Read(ref reader),
            (ConnectionPhase.Configuration, 2) => ContentManifestPacket.Read(ref reader),
            (ConnectionPhase.Play, 2) => InitialWorldStatePacket.Read(ref reader),
            (ConnectionPhase.Play, 3) => ChunkDataPacket.Read(ref reader),
            (ConnectionPhase.Play, 4) => InitialWorldCompletePacket.Read(ref reader),
            (ConnectionPhase.Play, 5) => WorldUpdatePacket.Read(ref reader),
            _ => throw new InvalidDataException("Unknown clientbound message identifier in the current phase.")
        };
        reader.EnsureEnd();
        return packet;
    }

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
