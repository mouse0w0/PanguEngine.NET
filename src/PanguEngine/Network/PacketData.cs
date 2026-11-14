using PanguEngine.Network.Packets;
using PanguEngine.Network.Serialization;
using PanguEngine.Registries;
using PanguEngine.World.Blocks;
using PanguEngine.World.Chunking;
using Silk.NET.Maths;

namespace PanguEngine.Network;

internal static class PacketData
{
    internal static string ReadString(ref Reader reader) => reader.ReadString(reader.RemainingLength);

    internal static ResourceKey ReadKey(ref Reader reader)
    {
        var text = ReadString(ref reader);
        return ResourceKey.TryParse(text, out var key) ? key : throw new InvalidDataException("Invalid resource key.");
    }

    internal static void WritePose(ref Writer writer, PlayerPose pose)
    {
        writer.WriteDouble(pose.Position.X);
        writer.WriteDouble(pose.Position.Y);
        writer.WriteDouble(pose.Position.Z);
        writer.WriteDouble(pose.Yaw);
        writer.WriteDouble(pose.Pitch);
    }

    internal static PlayerPose ReadPose(ref Reader reader)
    {
        var position = new Vector3D<double>(reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble());
        var yaw = reader.ReadDouble();
        var pitch = reader.ReadDouble();
        if (!double.IsFinite(position.X) || !double.IsFinite(position.Y) || !double.IsFinite(position.Z) ||
            !double.IsFinite(yaw) || !double.IsFinite(pitch) || pitch is < -89 or > 89)
            throw new InvalidDataException("Invalid player pose.");
        return new PlayerPose(position, yaw, pitch);
    }

    internal static void WritePlayers(ref Writer writer, IReadOnlyList<PlayerState> players)
    {
        writer.WriteCount(players.Count);
        foreach (var player in players)
        {
            writer.WriteVarUInt32(player.NetworkId);
            WritePose(ref writer, player.Pose);
        }
    }

    internal static PlayerState[] ReadPlayers(ref Reader reader)
    {
        var count = reader.ReadCount(Math.Min(NetworkLimits.MaxConnections, reader.RemainingLength / 41));
        var players = new PlayerState[count];
        var ids = new HashSet<uint>();
        for (var i = 0; i < count; i++)
        {
            var id = reader.ReadVarUInt32();
            if (!ids.Add(id))
                throw new InvalidDataException("Duplicate player network identifier.");
            players[i] = new PlayerState(id, ReadPose(ref reader));
        }
        return players;
    }

    internal static void WritePosition(ref Writer writer, BlockPos position)
    {
        writer.WriteInt32(position.X);
        writer.WriteInt32(position.Y);
        writer.WriteInt32(position.Z);
    }

    internal static BlockPos ReadPosition(ref Reader reader) =>
        new(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());

    internal static void WriteBlock(ref Writer writer, BlockState state) =>
        writer.WriteCount(BlockStateIds.GetId(state));

    internal static BlockState ReadBlock(ref Reader reader) => BlockStateIds.Get(reader.ReadCount(int.MaxValue));
}
