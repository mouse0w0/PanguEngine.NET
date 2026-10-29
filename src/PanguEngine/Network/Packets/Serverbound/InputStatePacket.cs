namespace PanguEngine.Network.Packets.Serverbound;

internal sealed record InputStatePacket(double Forward, double Right, double Yaw, double Pitch) : ServerboundPacket;
