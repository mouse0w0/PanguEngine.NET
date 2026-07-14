using PanguEngine.World.Blocks;
using PanguEngine.World.Chunking;

namespace PanguEngine.Client.Rendering.World;

internal static class SelectionMeshBuilder
{
    internal const float Expansion = 0.002f;
    internal const float Thickness = 0.01f;

    private const float HalfThickness = Thickness / 2;

    internal static ChunkVertex[] Build(BlockPos position, IBlockShape shape)
    {
        var selectionBoxes = shape.GetSelectionBoxes();
        var vertices = new List<ChunkVertex>(selectionBoxes.Count * 432);
        foreach (var box in selectionBoxes)
        {
            var minX = position.X + (float)box.Min.X - Expansion;
            var minY = position.Y + (float)box.Min.Y - Expansion;
            var minZ = position.Z + (float)box.Min.Z - Expansion;
            var maxX = position.X + (float)box.Max.X + Expansion;
            var maxY = position.Y + (float)box.Max.Y + Expansion;
            var maxZ = position.Z + (float)box.Max.Z + Expansion;

            AddXEdge(vertices, minX, maxX, minY, minZ);
            AddXEdge(vertices, minX, maxX, minY, maxZ);
            AddXEdge(vertices, minX, maxX, maxY, minZ);
            AddXEdge(vertices, minX, maxX, maxY, maxZ);

            AddYEdge(vertices, minY, maxY, minX, minZ);
            AddYEdge(vertices, minY, maxY, minX, maxZ);
            AddYEdge(vertices, minY, maxY, maxX, minZ);
            AddYEdge(vertices, minY, maxY, maxX, maxZ);

            AddZEdge(vertices, minZ, maxZ, minX, minY);
            AddZEdge(vertices, minZ, maxZ, minX, maxY);
            AddZEdge(vertices, minZ, maxZ, maxX, minY);
            AddZEdge(vertices, minZ, maxZ, maxX, maxY);
        }

        return vertices.ToArray();
    }

    private static void AddXEdge(List<ChunkVertex> vertices, float minX, float maxX, float y, float z)
    {
        AddCuboid(vertices, minX, y - HalfThickness, z - HalfThickness, maxX, y + HalfThickness, z + HalfThickness);
    }

    private static void AddYEdge(List<ChunkVertex> vertices, float minY, float maxY, float x, float z)
    {
        AddCuboid(vertices, x - HalfThickness, minY, z - HalfThickness, x + HalfThickness, maxY, z + HalfThickness);
    }

    private static void AddZEdge(List<ChunkVertex> vertices, float minZ, float maxZ, float x, float y)
    {
        AddCuboid(vertices, x - HalfThickness, y - HalfThickness, minZ, x + HalfThickness, y + HalfThickness, maxZ);
    }

    private static void AddCuboid(
        List<ChunkVertex> vertices,
        float minX,
        float minY,
        float minZ,
        float maxX,
        float maxY,
        float maxZ)
    {
        AddQuad(vertices, (minX, minY, minZ), (minX, minY, maxZ), (maxX, minY, maxZ), (maxX, minY, minZ));
        AddQuad(vertices, (minX, maxY, minZ), (maxX, maxY, minZ), (maxX, maxY, maxZ), (minX, maxY, maxZ));
        AddQuad(vertices, (minX, minY, minZ), (maxX, minY, minZ), (maxX, maxY, minZ), (minX, maxY, minZ));
        AddQuad(vertices, (minX, minY, maxZ), (minX, maxY, maxZ), (maxX, maxY, maxZ), (maxX, minY, maxZ));
        AddQuad(vertices, (minX, minY, minZ), (minX, maxY, minZ), (minX, maxY, maxZ), (minX, minY, maxZ));
        AddQuad(vertices, (maxX, minY, minZ), (maxX, minY, maxZ), (maxX, maxY, maxZ), (maxX, maxY, minZ));
    }

    private static void AddQuad(
        List<ChunkVertex> vertices,
        (float X, float Y, float Z) first,
        (float X, float Y, float Z) second,
        (float X, float Y, float Z) third,
        (float X, float Y, float Z) fourth)
    {
        vertices.Add(CreateVertex(first));
        vertices.Add(CreateVertex(fourth));
        vertices.Add(CreateVertex(third));
        vertices.Add(CreateVertex(first));
        vertices.Add(CreateVertex(third));
        vertices.Add(CreateVertex(second));
    }

    private static ChunkVertex CreateVertex((float X, float Y, float Z) position)
    {
        return new ChunkVertex(position.X, position.Y, position.Z, 0, 0, 0, 1);
    }
}