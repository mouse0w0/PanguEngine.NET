using PanguEngine.Client.World;
using PanguEngine.World.Blocks;
using PanguEngine.World.Chunking;

namespace PanguEngine.Client.Rendering.World;

internal sealed class ChunkMeshSnapshot
{
    private const int Border = 1;
    private const int SizeX = Chunk.SizeX + Border * 2;
    private const int SizeY = Chunk.SizeY + Border * 2;
    private const int SizeZ = Chunk.SizeZ + Border * 2;
    private readonly BlockState[] _blocks;

    private ChunkMeshSnapshot(ChunkPos position, BlockState[] blocks)
    {
        Position = position;
        _blocks = blocks;
    }

    internal ChunkPos Position { get; }

    internal static ChunkMeshSnapshot Capture(ClientWorld world, ChunkPos position)
    {
        ArgumentNullException.ThrowIfNull(world);

        var blocks = new BlockState[SizeX * SizeY * SizeZ];
        var originX = position.X * Chunk.SizeX;
        var originY = position.Y * Chunk.SizeY;
        var originZ = position.Z * Chunk.SizeZ;
        var index = 0;
        for (var y = -Border; y < Chunk.SizeY + Border; y++)
        {
            for (var z = -Border; z < Chunk.SizeZ + Border; z++)
            {
                for (var x = -Border; x < Chunk.SizeX + Border; x++)
                {
                    blocks[index++] = world.GetBlock(new BlockPos(
                        originX + x,
                        originY + y,
                        originZ + z));
                }
            }
        }

        return new ChunkMeshSnapshot(position, blocks);
    }

    internal BlockState GetBlock(BlockPos localPosition)
    {
        var x = localPosition.X + Border;
        var y = localPosition.Y + Border;
        var z = localPosition.Z + Border;
        return _blocks[x + SizeX * (z + SizeZ * y)];
    }
}