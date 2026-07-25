using PanguEngine.Client.Resources.Models;
using PanguEngine.Client.World;
using PanguEngine.World;
using PanguEngine.World.Chunking;
using Silk.NET.Maths;

namespace PanguEngine.Client.Rendering.World;

internal sealed class ChunkMeshBuilder
{
    private readonly BlockModelManager _models;

    internal ChunkMeshBuilder(BlockModelManager models)
    {
        _models = models;
    }

    public ChunkMesh Build(ClientWorld world, Chunk chunk)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(chunk);

        var writer = new ChunkMeshWriter();
        foreach (var (localPosition, state) in chunk.EnumerateBlocks())
        {
            if (state.IsAir)
                continue;

            var worldPosition = ToWorldPosition(chunk.Position, localPosition);
            var cullMask = GetCullMask(world, worldPosition);
            _models.Get(state, worldPosition).Emit(
                new Vector3D<float>(localPosition.X, localPosition.Y, localPosition.Z),
                cullMask,
                writer);
        }

        return new ChunkMesh(writer.Vertices.ToArray(), writer.Indices.ToArray());
    }

    private static BlockPos ToWorldPosition(ChunkPos chunkPosition, BlockPos localPosition)
    {
        return new BlockPos(
            chunkPosition.X * Chunk.SizeX + localPosition.X,
            chunkPosition.Y * Chunk.SizeY + localPosition.Y,
            chunkPosition.Z * Chunk.SizeZ + localPosition.Z);
    }

    private static DirectionFlags GetCullMask(
        ClientWorld world,
        BlockPos position)
    {
        var result = DirectionFlags.None;
        foreach (var direction in Enum.GetValues<Direction>())
        {
            var neighbor = world.GetBlock(position.Offset(direction));
            if (neighbor.CanOccludeFace(direction.Opposite()))
                result |= direction.ToFlag();
        }

        return result;
    }

    private sealed class ChunkMeshWriter : IBlockMeshWriter
    {
        internal List<ChunkVertex> Vertices { get; } = [];

        internal List<uint> Indices { get; } = [];

        public uint VertexCount => checked((uint)Vertices.Count);

        public void WriteVertex(
            Vector3D<float> position,
            Vector2D<float> texCoord,
            Vector3D<float> normal)
        {
            Vertices.Add(new ChunkVertex(
                position.X,
                position.Y,
                position.Z,
                texCoord.X,
                texCoord.Y,
                normal.X,
                normal.Y,
                normal.Z));
        }

        public void WriteIndex(uint index)
        {
            Indices.Add(index);
        }
    }
}