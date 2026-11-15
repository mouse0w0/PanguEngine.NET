using System.Buffers;

namespace PanguEngine.Client.Rendering.World;

/// <summary>
/// Owns rented mesh buffers until they are returned once after consumption.
/// </summary>
internal sealed class ChunkMesh(ChunkVertex[] vertices, int vertexCount, uint[] indices, int indexCount)
    : IDisposable
{
    public ReadOnlySpan<ChunkVertex> Vertices => vertices.AsSpan(0, VertexCount);

    public ReadOnlySpan<uint> Indices => indices.AsSpan(0, IndexCount);

    public int VertexCount { get; } = vertexCount;

    public int IndexCount { get; } = indexCount;

    public bool IsEmpty => VertexCount == 0;

    public void Dispose()
    {
        if (vertices.Length > 0)
            ArrayPool<ChunkVertex>.Shared.Return(vertices);
        if (indices.Length > 0)
            ArrayPool<uint>.Shared.Return(indices);
    }
}
