using PanguEngine.World.Blocks;
using PanguEngine.World.Chunking;

namespace PanguEngine.Network.Packets.Clientbound;

internal sealed record WorldUpdatePacket : ClientboundPacket
{
    internal WorldUpdatePacket(long tick, PlayerPose player,
        IReadOnlyList<BlockChange> changes, IReadOnlyList<InteractionResult> results)
    {
        Tick = tick;
        Player = player;
        Changes = changes;
        Results = results;
    }

    internal long Tick { get; }
    internal PlayerPose Player { get; }
    internal IReadOnlyList<BlockChange> Changes { get; }
    internal IReadOnlyList<InteractionResult> Results { get; }

    internal enum InteractionStatus { Success, NoHit, Occupied }

    internal readonly record struct BlockChange(BlockPos Position, BlockState State);

    internal readonly record struct InteractionResult(
        BlockOperation Operation, InteractionStatus Status, BlockPos Position)
    {
        internal bool IsSuccess => Status == InteractionStatus.Success;
    }
}
