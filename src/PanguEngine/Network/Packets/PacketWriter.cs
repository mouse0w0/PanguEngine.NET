using PanguEngine.Network.Serialization;

namespace PanguEngine.Network.Packets;

/// <summary>Writes the payload of a packet.</summary>
/// <typeparam name="TPacket">The packet type accepted by this writer.</typeparam>
/// <param name="packet">The packet to write.</param>
/// <param name="writer">The writer positioned at the start of the payload.</param>
public delegate void PacketWriter<in TPacket>(TPacket packet, ref Writer writer) where TPacket : Packet;
