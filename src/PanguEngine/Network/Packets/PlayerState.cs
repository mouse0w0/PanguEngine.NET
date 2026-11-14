namespace PanguEngine.Network.Packets;

internal readonly record struct PlayerState(uint NetworkId, PlayerPose Pose);
