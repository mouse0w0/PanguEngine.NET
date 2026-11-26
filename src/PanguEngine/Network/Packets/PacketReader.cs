using PanguEngine.Network.Serialization;

namespace PanguEngine.Network.Packets;

/// <summary>Reads a packet from its binary payload.</summary>
/// <typeparam name="TPacket">The packet type produced by the reader.</typeparam>
/// <param name="reader">The reader positioned at the start of the payload.</param>
/// <returns>The decoded packet.</returns>
public delegate TPacket PacketReader<out TPacket>(ref Reader reader) where TPacket : Packet;
