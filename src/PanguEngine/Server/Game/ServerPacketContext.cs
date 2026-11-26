using static PanguEngine.Network.Packets.Clientbound.WorldUpdatePacket;

namespace PanguEngine.Server.Game;

/// <summary>Provides the sender and per-player state for a Play handler.</summary>
internal readonly record struct ServerPacketContext(ServerGame Game, Player Player, List<InteractionResult> Results);
