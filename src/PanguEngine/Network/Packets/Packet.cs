using PanguEngine.Network.Serialization;

namespace PanguEngine.Network.Packets;

/// <summary>Represents a network message with a binary payload.</summary>
internal abstract record Packet
{
    /// <summary>Writes this message's payload.</summary>
    internal abstract void Write(ref Writer writer);
}
