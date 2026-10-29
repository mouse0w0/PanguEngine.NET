namespace PanguEngine.Network.Packets.Serverbound;

internal sealed record BlockInteractionPacket(BlockOperation Operation, double Yaw, double Pitch) : ServerboundPacket;
