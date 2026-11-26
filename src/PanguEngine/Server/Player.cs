using System.Security.Cryptography;
using System.Text;
using PanguEngine.Network;
using PanguEngine.Network.Packets;
using PanguEngine.Server.Game;
using PanguEngine.World.Entities;

namespace PanguEngine.Server;

/// <summary>Represents a server player that has entered the play phase.</summary>
/// <remarks>Access the associated entity from the server game thread.</remarks>
public sealed class Player
{
    private static readonly Guid OfflineNamespace = new("4f1384dc-d72c-53df-b82b-4347779fc205");

    internal Player(string username, PlayerEntity entity, RemoteSession session)
    {
        Id = CreateOfflineId(username);
        Username = username;
        Entity = entity;
        Session = session;
    }

    /// <summary>The stable identifier derived from the unauthenticated username.</summary>
    public Guid Id { get; }

    /// <summary>The accepted username with its original casing.</summary>
    public string Username { get; }

    /// <summary>The player's character in the authoritative world.</summary>
    public PlayerEntity Entity { get; }

    /// <summary>The server session through which this player entered Play.</summary>
    /// <remarks>This reference does not indicate whether the player is still online.</remarks>
    internal RemoteSession Session { get; }

    /// <summary>Queues a registered custom Play packet for this player.</summary>
    /// <param name="packet">The packet to send. Do not modify it or its payload after queueing it.</param>
    /// <returns>Whether the packet was queued, not whether the player received it.</returns>
    /// <remarks>Call from the server game thread.</remarks>
    public bool TrySendPacket(ClientboundPacket packet) =>
        PacketCodec.HasPlayId(packet) && Session.TrySendCustom(packet);

    private static Guid CreateOfflineId(string username)
    {
        var nameBytes = Encoding.UTF8.GetBytes(username.ToLowerInvariant());
        var input = new byte[16 + nameBytes.Length];
        OfflineNamespace.TryWriteBytes(input, bigEndian: true, out _);
        nameBytes.CopyTo(input, 16);
        var hash = SHA1.HashData(input);
        hash[6] = (byte)((hash[6] & 0x0f) | 0x50);
        hash[8] = (byte)((hash[8] & 0x3f) | 0x80);
        return new Guid(hash.AsSpan(0, 16), bigEndian: true);
    }
}
