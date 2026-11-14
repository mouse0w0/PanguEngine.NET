using System.Buffers;
using PanguEngine.Network.Packets;
using PanguEngine.Network.Packets.Clientbound;
using PanguEngine.Network.Packets.Serverbound;
using PanguEngine.Network.Serialization;

namespace PanguEngine.Network;

internal static class PacketCodec
{
    internal const int ProtocolVersion = 5;

    internal static ReadOnlyMemory<byte> Encode(ServerboundPacket packet)
    {
        var buffer = new PacketBuffer();
        var writer = new Writer(buffer);
        writer.WriteVarUInt32(packet switch
        {
            InputStatePacket => 1, BlockInteractionPacket => 2,
            ClientHelloPacket => 3, ContentReadyPacket => 4,
            _ => throw new InvalidOperationException("Unregistered serverbound packet.")
        });
        packet.Write(ref writer);
        return buffer.WrittenMemory;
    }

    internal static ReadOnlyMemory<byte> Encode(ClientboundPacket packet)
    {
        var buffer = new PacketBuffer();
        var writer = new Writer(buffer);
        writer.WriteVarUInt32(packet switch
        {
            InitialWorldStatePacket => 1, ChunkDataPacket => 2,
            InitialWorldCompletePacket => 3, WorldUpdatePacket => 4,
            DisconnectPacket => 5, ContentManifestPacket => 6,
            _ => throw new InvalidOperationException("Unregistered clientbound packet.")
        });
        packet.Write(ref writer);
        return buffer.WrittenMemory;
    }

    internal static uint ReadId(ReadOnlySpan<byte> frame)
    {
        var reader = new Reader(frame);
        return reader.ReadVarUInt32();
    }

    internal static ServerboundPacket ReadServerbound(ReadOnlySpan<byte> frame)
    {
        var reader = new Reader(frame);
        var packet = reader.ReadVarUInt32() switch
        {
            1 => (ServerboundPacket)InputStatePacket.Read(ref reader),
            2 => BlockInteractionPacket.Read(ref reader),
            3 => ClientHelloPacket.Read(ref reader),
            4 => ContentReadyPacket.Read(ref reader),
            _ => throw new InvalidDataException("Unknown serverbound message identifier.")
        };
        reader.EnsureEnd();
        return packet;
    }

    internal static ClientboundPacket ReadClientbound(ReadOnlySpan<byte> frame)
    {
        var reader = new Reader(frame);
        var packet = reader.ReadVarUInt32() switch
        {
            1 => (ClientboundPacket)InitialWorldStatePacket.Read(ref reader),
            2 => ChunkDataPacket.Read(ref reader),
            3 => InitialWorldCompletePacket.Read(ref reader),
            4 => WorldUpdatePacket.Read(ref reader),
            5 => DisconnectPacket.Read(ref reader),
            6 => ContentManifestPacket.Read(ref reader),
            _ => throw new InvalidDataException("Unknown clientbound message identifier.")
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
