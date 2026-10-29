using Silk.NET.Maths;

namespace PanguEngine.Network.Packets;

internal readonly record struct PlayerPose(Vector3D<double> Position, double Yaw, double Pitch);
