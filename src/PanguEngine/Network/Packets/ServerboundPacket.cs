using System.Text;

namespace PanguEngine.Network.Packets;

/// <summary>Represents a message sent from a client to a server.</summary>
public abstract record ServerboundPacket : Packet
{
    public virtual ServerboundPacketType Type =>
        throw new InvalidOperationException($"Packet class '{GetType()}' does not have a Play packet type.");

    protected override bool PrintMembers(StringBuilder builder) => base.PrintMembers(builder);
}
