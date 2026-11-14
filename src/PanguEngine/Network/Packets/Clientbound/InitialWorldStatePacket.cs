using PanguEngine.Network.Serialization;

namespace PanguEngine.Network.Packets.Clientbound;

internal sealed record InitialWorldStatePacket(long Tick, uint PlayerNetworkId, double StepSeconds,
    IReadOnlyList<PlayerState> Players, int ChunkCount) : ClientboundPacket
{
    internal PlayerPose Player => Players.Single(player => player.NetworkId == PlayerNetworkId).Pose;

    internal override void Write(ref Writer writer)
    {
        writer.WriteInt64(Tick);
        writer.WriteVarUInt32(PlayerNetworkId);
        writer.WriteDouble(StepSeconds);
        PacketData.WritePlayers(ref writer, Players);
        writer.WriteCount(ChunkCount);
    }

    internal static InitialWorldStatePacket Read(ref Reader reader)
    {
        var tick = reader.ReadInt64();
        var playerNetworkId = reader.ReadVarUInt32();
        var step = reader.ReadDouble();
        var players = PacketData.ReadPlayers(ref reader);
        var count = reader.ReadCount(int.MaxValue);
        if (tick < 0 || !double.IsFinite(step) || step <= 0 || !players.Any(player => player.NetworkId == playerNetworkId))
            throw new InvalidDataException("Invalid initial world metadata.");
        return new InitialWorldStatePacket(tick, playerNetworkId, step, players, count);
    }
}
