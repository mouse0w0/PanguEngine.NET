using PanguEngine.World.Blocks;
using PanguEngine.World.Chunking;

namespace PanguEngine.Client.World;

internal static class FlatWorldGenerator
{
    private const int MinChunkCoordinate = -4;
    private const int MaxChunkCoordinate = 4;
    private const int StoneLayerTop = 11;
    private const int DirtLayerTop = 14;

    internal static void Generate(ClientWorld world)
    {
        for (var chunkZ = MinChunkCoordinate; chunkZ <= MaxChunkCoordinate; chunkZ++)
        {
            for (var chunkX = MinChunkCoordinate; chunkX <= MaxChunkCoordinate; chunkX++)
            {
                var originX = chunkX * Chunk.SizeX;
                var originZ = chunkZ * Chunk.SizeZ;
                for (var localZ = 0; localZ < Chunk.SizeZ; localZ++)
                {
                    for (var localX = 0; localX < Chunk.SizeX; localX++)
                    {
                        for (var y = 0; y < Chunk.SizeY; y++)
                        {
                            var state = y switch
                            {
                                <= StoneLayerTop => BuiltinBlocks.Stone.DefaultState,
                                <= DirtLayerTop => BuiltinBlocks.Dirt.DefaultState,
                                _ => BuiltinBlocks.Grass.DefaultState
                            };
                            world.SetBlock(new BlockPos(originX + localX, y, originZ + localZ), state);
                        }
                    }
                }
            }
        }
    }
}