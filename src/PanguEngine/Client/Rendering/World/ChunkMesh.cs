namespace PanguEngine.Client.Rendering.World;

internal sealed class ChunkMesh
{
    public ChunkMesh(ChunkVertex[] vertices)
    {
        ArgumentNullException.ThrowIfNull(vertices);
        Vertices = vertices;
    }

    public ChunkVertex[] Vertices { get; }

    public int VertexCount => Vertices.Length;

    public bool IsEmpty => VertexCount == 0;
}