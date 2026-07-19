using PanguEngine.Graphics;
using PanguEngine.Registries;
using PanguEngine.World;
using Silk.NET.Maths;

namespace PanguEngine.Client.Resources.Models;

internal sealed class BlockModelBaker
{
    private const float ModelScale = 1f / 16f;
    private static readonly uint[] FaceIndices = [0, 1, 2, 0, 2, 3];

    private readonly TextureAtlas<ResourceKey> _atlas;

    internal BlockModelBaker(TextureAtlas<ResourceKey> atlas)
    {
        _atlas = atlas;
    }

    internal BakedBlockModel Bake(UnbakedBlockModel model)
    {
        var elements = model.Elements!;
        var faceCount = elements.Sum(element => element.Faces.Count);
        var vertices = new List<BakedVertex>(faceCount * 4);
        var indices = new List<uint>(faceCount * FaceIndices.Length);
        var faces = new List<BakedFaceRange>(faceCount);

        foreach (var element in elements)
        {
            foreach (var (directionName, face) in element.Faces)
            {
                var direction = ParseDirection(directionName, model.SourceKey);
                var texture = face.Texture is BlockTextureValue.Resource resource
                    ? resource.Key
                    : throw new InvalidDataException(
                        $"Block model '{model.SourceKey}' face '{directionName}' has an unresolved texture variable.");
                var cull = DirectionFlags.None;
                foreach (var value in face.Cull)
                    cull |= ParseDirection(value, model.SourceKey).ToFlag();

                var vertexStart = vertices.Count;
                var uv = face.Uv ?? GetAutomaticUv(element.From, element.To, direction);
                var region = _atlas.GetRegion(texture);
                var positions = GetFacePositions(element.From, element.To, direction);
                var normal = GetNormal(direction);
                var textureCoordinates = new[]
                {
                    new Vector2D<float>(uv[0], uv[1]),
                    new Vector2D<float>(uv[0], uv[3]),
                    new Vector2D<float>(uv[2], uv[3]),
                    new Vector2D<float>(uv[2], uv[1])
                };
                for (var index = 0; index < 4; index++)
                {
                    var position = positions[index];
                    var localU = textureCoordinates[index].X * ModelScale;
                    var localV = textureCoordinates[index].Y * ModelScale;
                    vertices.Add(new BakedVertex(
                        new Vector3D<float>(
                            position.X * ModelScale,
                            position.Y * ModelScale,
                            position.Z * ModelScale),
                        new Vector2D<float>(
                            region.U0 + localU * (region.U1 - region.U0),
                            region.V0 + localV * (region.V1 - region.V0)),
                        normal));
                }

                var indexStart = indices.Count;
                indices.AddRange(FaceIndices);
                faces.Add(new BakedFaceRange(vertexStart, 4, indexStart, FaceIndices.Length, cull));
            }
        }

        return new BakedBlockModel(vertices.ToArray(), indices.ToArray(), faces.ToArray());
    }

    private static Direction ParseDirection(string value, ResourceKey modelKey)
    {
        return value switch
        {
            "down" => Direction.Down,
            "up" => Direction.Up,
            "north" => Direction.North,
            "south" => Direction.South,
            "west" => Direction.West,
            "east" => Direction.East,
            _ => throw new InvalidDataException($"Block model '{modelKey}' has unknown direction '{value}'.")
        };
    }

    private static float[] GetAutomaticUv(
        Vector3D<float> from,
        Vector3D<float> to,
        Direction direction)
    {
        return direction switch
        {
            Direction.Down => [from.X, 16 - to.Z, to.X, 16 - from.Z],
            Direction.Up => [from.X, from.Z, to.X, to.Z],
            Direction.North => [16 - to.X, 16 - to.Y, 16 - from.X, 16 - from.Y],
            Direction.South => [from.X, 16 - to.Y, to.X, 16 - from.Y],
            Direction.West => [from.Z, 16 - to.Y, to.Z, 16 - from.Y],
            Direction.East => [16 - to.Z, 16 - to.Y, 16 - from.Z, 16 - from.Y],
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
        };
    }

    private static Vector3D<float>[] GetFacePositions(
        Vector3D<float> from,
        Vector3D<float> to,
        Direction direction)
    {
        return direction switch
        {
            Direction.Down =>
            [
                new Vector3D<float>(from.X, from.Y, to.Z),
                new Vector3D<float>(from.X, from.Y, from.Z),
                new Vector3D<float>(to.X, from.Y, from.Z),
                new Vector3D<float>(to.X, from.Y, to.Z)
            ],
            Direction.Up =>
            [
                new Vector3D<float>(from.X, to.Y, from.Z),
                new Vector3D<float>(from.X, to.Y, to.Z),
                new Vector3D<float>(to.X, to.Y, to.Z),
                new Vector3D<float>(to.X, to.Y, from.Z)
            ],
            Direction.North =>
            [
                new Vector3D<float>(to.X, to.Y, from.Z),
                new Vector3D<float>(to.X, from.Y, from.Z),
                new Vector3D<float>(from.X, from.Y, from.Z),
                new Vector3D<float>(from.X, to.Y, from.Z)
            ],
            Direction.South =>
            [
                new Vector3D<float>(from.X, to.Y, to.Z),
                new Vector3D<float>(from.X, from.Y, to.Z),
                new Vector3D<float>(to.X, from.Y, to.Z),
                new Vector3D<float>(to.X, to.Y, to.Z)
            ],
            Direction.West =>
            [
                new Vector3D<float>(from.X, to.Y, from.Z),
                new Vector3D<float>(from.X, from.Y, from.Z),
                new Vector3D<float>(from.X, from.Y, to.Z),
                new Vector3D<float>(from.X, to.Y, to.Z)
            ],
            Direction.East =>
            [
                new Vector3D<float>(to.X, to.Y, to.Z),
                new Vector3D<float>(to.X, from.Y, to.Z),
                new Vector3D<float>(to.X, from.Y, from.Z),
                new Vector3D<float>(to.X, to.Y, from.Z)
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
        };
    }

    private static Vector3D<float> GetNormal(Direction direction)
    {
        return direction switch
        {
            Direction.Down => new Vector3D<float>(0, -1, 0),
            Direction.Up => new Vector3D<float>(0, 1, 0),
            Direction.North => new Vector3D<float>(0, 0, -1),
            Direction.South => new Vector3D<float>(0, 0, 1),
            Direction.West => new Vector3D<float>(-1, 0, 0),
            Direction.East => new Vector3D<float>(1, 0, 0),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
        };
    }
}