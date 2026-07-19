using Silk.NET.Maths;

namespace PanguEngine.Client.Resources.Models;

internal interface IBlockMeshWriter
{
    uint VertexCount { get; }

    void WriteVertex(
        Vector3D<float> position,
        Vector2D<float> texCoord,
        Vector3D<float> normal);

    void WriteIndex(uint index);
}