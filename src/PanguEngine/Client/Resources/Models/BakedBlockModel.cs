using PanguEngine.World;
using Silk.NET.Maths;

namespace PanguEngine.Client.Resources.Models;

internal sealed class BakedBlockModel
{
    private readonly BakedVertex[] _vertices;
    private readonly uint[] _indices;
    private readonly BakedFaceRange[] _faces;

    internal BakedBlockModel(
        BakedVertex[] vertices,
        uint[] indices,
        BakedFaceRange[] faces)
    {
        _vertices = vertices;
        _indices = indices;
        _faces = faces;
    }

    internal void Emit(
        Vector3D<float> position,
        DirectionFlags cullMask,
        IBlockMeshWriter writer)
    {
        foreach (var face in _faces)
        {
            if (face.Cull != DirectionFlags.None && (cullMask & face.Cull) == face.Cull)
                continue;

            var vertexOffset = writer.VertexCount;
            for (var vertexIndex = 0; vertexIndex < face.VertexCount; vertexIndex++)
            {
                var vertex = _vertices[face.VertexStart + vertexIndex];
                writer.WriteVertex(
                    new Vector3D<float>(
                        vertex.Position.X + position.X,
                        vertex.Position.Y + position.Y,
                        vertex.Position.Z + position.Z),
                    vertex.TexCoord,
                    vertex.Normal);
            }

            for (var index = 0; index < face.IndexCount; index++)
                writer.WriteIndex(checked(vertexOffset + _indices[face.IndexStart + index]));
        }
    }
}

internal readonly record struct BakedVertex(
    Vector3D<float> Position,
    Vector2D<float> TexCoord,
    Vector3D<float> Normal);

internal readonly record struct BakedFaceRange(
    int VertexStart,
    int VertexCount,
    int IndexStart,
    int IndexCount,
    DirectionFlags Cull);