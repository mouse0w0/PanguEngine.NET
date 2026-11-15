using System.Buffers;
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

        using var writer = new ChunkMeshWriter();
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

        return writer.Complete();
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
        foreach (var direction in Direction.Values)
        {
            var neighbor = snapshot.GetBlock(position.Offset(direction));
            if (neighbor.CanOccludeFace(direction.Opposite()))
                result |= direction.ToFlag();
        }

        return result;
    }

    private sealed class ChunkMeshWriter : IBlockMeshWriter, IDisposable
    {
        private ChunkVertex[] _vertices = [];
        private uint[] _indices = [];
        private int _vertexCount;
        private int _indexCount;

        public uint VertexCount => checked((uint)_vertexCount);

        public void WriteVertex(
            Vector3D<float> position,
            Vector2D<float> texCoord,
            Vector3D<float> normal)
        {
            if (_vertexCount == _vertices.Length)
            {
                var vertices = ArrayPool<ChunkVertex>.Shared.Rent(Math.Max(16, checked(_vertices.Length * 2)));
                _vertices.AsSpan(0, _vertexCount).CopyTo(vertices);
                if (_vertices.Length > 0)
                    ArrayPool<ChunkVertex>.Shared.Return(_vertices);
                _vertices = vertices;
            }

            _vertices[_vertexCount++] = new ChunkVertex(
                position.X,
                position.Y,
                position.Z,
                texCoord.X,
                texCoord.Y,
                normal.X,
                normal.Y,
                normal.Z);
        }

        public void WriteIndex(uint index)
        {
            if (_indexCount == _indices.Length)
            {
                var indices = ArrayPool<uint>.Shared.Rent(Math.Max(16, checked(_indices.Length * 2)));
                _indices.AsSpan(0, _indexCount).CopyTo(indices);
                if (_indices.Length > 0)
                    ArrayPool<uint>.Shared.Return(_indices);
                _indices = indices;
            }

            _indices[_indexCount++] = index;
        }

        internal ChunkMesh Complete()
        {
            var mesh = new ChunkMesh(_vertices, _vertexCount, _indices, _indexCount);
            _vertices = [];
            _indices = [];
            return mesh;
        }

        public void Dispose()
        {
            if (_vertices.Length > 0)
                ArrayPool<ChunkVertex>.Shared.Return(_vertices);
            if (_indices.Length > 0)
                ArrayPool<uint>.Shared.Return(_indices);
        }
    }
}
