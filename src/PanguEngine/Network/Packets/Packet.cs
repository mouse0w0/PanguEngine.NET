using PanguEngine.Network.Serialization;

namespace PanguEngine.Network.Packets;

/// <summary>Represents a network message with a binary payload.</summary>
public abstract record Packet
{
    /// <summary>Writes the payload of a fixed-protocol packet.</summary>
    internal virtual void Write(ref Writer writer) =>
        throw new InvalidOperationException("Play packets are written through their registered writer.");

    internal static void Write(Packet packet, ref Writer writer) => packet.Write(ref writer);
}
