using PanguEngine.Client.Game;
using PanguEngine.Registries;

namespace PanguEngine.Network.Packets;

/// <summary>Defines how a registered Play packet sent to the client is read, written, and handled.</summary>
public sealed class ClientboundPacketType
{
    private ClientboundPacketType(Type packetClass, PacketReader<ClientboundPacket> read,
        PacketWriter<ClientboundPacket> write, Action<ClientPacketContext, ClientboundPacket> handle)
    {
        PacketClass = packetClass;
        Read = read;
        Write = write;
        Handle = handle;
    }

    internal Type PacketClass { get; }
    internal PacketReader<ClientboundPacket> Read { get; }
    internal PacketWriter<ClientboundPacket> Write { get; }
    internal Action<ClientPacketContext, ClientboundPacket> Handle { get; }

    /// <summary>Creates a definition for a packet received on the client game thread after world loading.</summary>
    /// <typeparam name="T">The concrete packet type.</typeparam>
    /// <param name="read">Reads a packet of exactly the specified type from its payload.</param>
    /// <param name="write">Writes the packet payload.</param>
    /// <param name="handle">Handles the packet with the active client game.</param>
    /// <returns>The definition to register in the clientbound packet registry.</returns>
    public static ClientboundPacketType Create<T>(PacketReader<T> read, PacketWriter<T> write,
        Action<ClientGame, T> handle) where T : ClientboundPacket =>
        CreateWithContext(read, write, (context, packet) => handle(context.Game!, packet));

    internal static ClientboundPacketType CreateWithContext<T>(PacketReader<T> read, PacketWriter<T> write,
        Action<ClientPacketContext, T> handle) where T : ClientboundPacket
    {
        if (typeof(T).IsAbstract)
            throw new InvalidOperationException($"Play packet type '{typeof(T)}' must be concrete.");
        return new(typeof(T), (ref reader) =>
            {
                var packet = read(ref reader);
                if (packet is null || packet.GetType() != typeof(T))
                    throw new InvalidOperationException($"Play packet reader did not produce '{typeof(T)}'.");
                return packet;
            }, (packet, ref writer) => write((T)packet, ref writer),
            (context, packet) => handle(context, (T)packet));
    }

    internal static void ValidateRegistration(IRegistry<ClientboundPacketType> registry, ResourceKey key,
        ClientboundPacketType type)
    {
        foreach (var entry in registry.Entries)
        {
            if (entry.Value.PacketClass == type.PacketClass)
                throw new InvalidOperationException($"Play packet class '{type.PacketClass}' is already registered in '{registry.Key}'.");
        }
    }
}
