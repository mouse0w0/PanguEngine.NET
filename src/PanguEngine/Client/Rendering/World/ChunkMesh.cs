namespace PanguEngine.Client.Rendering.World;

internal sealed class ChunkMesh
{
    public ChunkMesh(ChunkVertex[] vertices, uint[] indices)
    {
        ArgumentNullException.ThrowIfNull(vertices);
        ArgumentNullException.ThrowIfNull(indices);
        Vertices = vertices;
        Indices = indices;
    }

    public ChunkVertex[] Vertices { get; }

    public uint[] Indices { get; }

    public int VertexCount => Vertices.Length;

    public int IndexCount => Indices.Length;

    public bool IsEmpty => VertexCount == 0;
}