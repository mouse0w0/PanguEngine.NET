using PanguEngine.World.Blocks;
using PanguEngine.World.Chunking;
using PanguEngine.Network.Serialization;

namespace PanguEngine.Network.Packets.Clientbound;

internal sealed record WorldUpdatePacket(long Tick, IReadOnlyList<PlayerState> Players,
    IReadOnlyList<WorldUpdatePacket.BlockChange> Changes,
    IReadOnlyList<WorldUpdatePacket.InteractionResult> Results) : ClientboundPacket
{
    internal override void Write(ref Writer writer)
    {
        writer.WriteInt64(Tick);
        PacketData.WritePlayers(ref writer, Players);
        writer.WriteCount(Changes.Count);
        foreach (var change in Changes)
        {
            PacketData.WritePosition(ref writer, change.Position);
            PacketData.WriteBlock(ref writer, change.State);
        }
        writer.WriteCount(Results.Count);
        foreach (var result in Results)
        {
            writer.WriteByte((byte)result.Operation);
            writer.WriteByte((byte)result.Status);
            PacketData.WritePosition(ref writer, result.Position);
        }
    }

    internal static WorldUpdatePacket Read(ref Reader reader)
    {
        var tick = reader.ReadInt64();
        var players = PacketData.ReadPlayers(ref reader);
        var changes = new BlockChange[reader.ReadCount(reader.RemainingLength / 13)];
        for (var i = 0; i < changes.Length; i++)
            changes[i] = new BlockChange(PacketData.ReadPosition(ref reader), PacketData.ReadBlock(ref reader));
        var results = new InteractionResult[reader.ReadCount(reader.RemainingLength / 14)];
        for (var i = 0; i < results.Length; i++)
        {
            var operation = (BlockOperation)reader.ReadByte();
            var status = (InteractionStatus)reader.ReadByte();
            if (operation is not (BlockOperation.Break or BlockOperation.Place) ||
                status is not (InteractionStatus.Success or InteractionStatus.NoHit or InteractionStatus.Occupied))
                throw new InvalidDataException("Invalid interaction result.");
            results[i] = new InteractionResult(operation, status, PacketData.ReadPosition(ref reader));
        }
        if (tick < 0)
            throw new InvalidDataException("Invalid simulation tick.");
        return new WorldUpdatePacket(tick, players, changes, results);
    }

    internal enum InteractionStatus { Success, NoHit, Occupied }

    internal readonly record struct BlockChange(BlockPos Position, BlockState State);

    internal readonly record struct InteractionResult(
        BlockOperation Operation, InteractionStatus Status, BlockPos Position)
    {
        internal bool IsSuccess => Status == InteractionStatus.Success;
    }
}
