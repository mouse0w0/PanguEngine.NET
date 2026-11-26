using System.Text;

namespace PanguEngine.Network.Packets;

/// <summary>Represents a message sent from a server to a client.</summary>
public abstract record ClientboundPacket : Packet
{
    public virtual ClientboundPacketType Type =>
        throw new InvalidOperationException($"Packet class '{GetType()}' does not have a Play packet type.");

    protected override bool PrintMembers(StringBuilder builder) => base.PrintMembers(builder);
}
