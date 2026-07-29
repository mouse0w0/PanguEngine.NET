using PanguEngine.Client.Resources.Models;
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

    public ChunkMesh Build(ChunkMeshSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var writer = new ChunkMeshWriter();
        for (var y = 0; y < Chunk.SizeY; y++)
        {
            for (var z = 0; z < Chunk.SizeZ; z++)
            {
                for (var x = 0; x < Chunk.SizeX; x++)
                {
                    var localPosition = new BlockPos(x, y, z);
                    var state = snapshot.GetBlock(localPosition);
                    if (state.IsAir)
                        continue;

                    var worldPosition = ToWorldPosition(snapshot.Position, localPosition);
                    var cullMask = GetCullMask(snapshot, localPosition);
                    _models.Get(state, worldPosition).Emit(
                        new Vector3D<float>(x, y, z),
                        cullMask,
                        writer);
                }
            }
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
        ChunkMeshSnapshot snapshot,
        BlockPos position)
    {
        var result = DirectionFlags.None;
        foreach (var direction in Enum.GetValues<Direction>())
        {
            var neighbor = snapshot.GetBlock(position.Offset(direction));
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