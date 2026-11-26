using PanguEngine.Registries;
using PanguEngine.Server;
using PanguEngine.Server.Game;

namespace PanguEngine.Network.Packets;

/// <summary>Defines how a registered Play packet sent to the server is read, written, and handled.</summary>
public sealed class ServerboundPacketType
{
    private ServerboundPacketType(Type packetClass, PacketReader<ServerboundPacket> read,
        PacketWriter<ServerboundPacket> write, Action<ServerPacketContext, ServerboundPacket> handle)
    {
        PacketClass = packetClass;
        Read = read;
        Write = write;
        Handle = handle;
    }

    internal Type PacketClass { get; }
    internal PacketReader<ServerboundPacket> Read { get; }
    internal PacketWriter<ServerboundPacket> Write { get; }
    internal Action<ServerPacketContext, ServerboundPacket> Handle { get; }

    /// <summary>Creates a definition for a packet received on the server game thread.</summary>
    /// <typeparam name="T">The concrete packet type.</typeparam>
    /// <param name="read">Reads a packet of exactly the specified type from its payload.</param>
    /// <param name="write">Writes the packet payload.</param>
    /// <param name="handle">Handles the packet with the player who sent it.</param>
    /// <returns>The definition to register in the serverbound packet registry.</returns>
    public static ServerboundPacketType Create<T>(PacketReader<T> read, PacketWriter<T> write,
        Action<Player, T> handle) where T : ServerboundPacket =>
        CreateWithContext(read, write, (context, packet) => handle(context.Player, packet));

    internal static ServerboundPacketType CreateWithContext<T>(PacketReader<T> read, PacketWriter<T> write,
        Action<ServerPacketContext, T> handle) where T : ServerboundPacket
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

    internal static void ValidateRegistration(IRegistry<ServerboundPacketType> registry, ResourceKey key,
        ServerboundPacketType type)
    {
        foreach (var entry in registry.Entries)
        {
            if (entry.Value.PacketClass == type.PacketClass)
                throw new InvalidOperationException($"Play packet class '{type.PacketClass}' is already registered in '{registry.Key}'.");
        }
    }
}
