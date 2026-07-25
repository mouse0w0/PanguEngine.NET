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

    internal BakedBlockModel Bake(
        ResolvedBlockModel model,
        BlockModelRotation rotation = default)
    {
        var elements = model.Elements;
        var faceCount = elements.Sum(element => element.Faces.Count);
        var vertices = new List<BakedVertex>(faceCount * 4);
        var indices = new List<uint>(faceCount * FaceIndices.Length);
        var faces = new List<BakedFaceRange>(faceCount);

        foreach (var element in elements)
        {
            foreach (var (direction, face) in element.Faces)
            {
                var cull = RotateCull(face.Cull, rotation);

                var vertexStart = vertices.Count;
                var uv = face.Uv;
                var region = _atlas.GetRegion(face.Texture);
                var positions = GetFacePositions(element.From, element.To, direction);
                var normal = RotateVector(GetNormal(direction), rotation);
                var textureCoordinates = new[]
                {
                    new Vector2D<float>(uv.U0, uv.V0),
                    new Vector2D<float>(uv.U0, uv.V1),
                    new Vector2D<float>(uv.U1, uv.V1),
                    new Vector2D<float>(uv.U1, uv.V0)
                };
                var rotationOffset = face.Rotation / 90;
                for (var index = 0; index < 4; index++)
                {
                    var position = positions[index];
                    var textureCoordinate = textureCoordinates[(index - rotationOffset + 4) % 4];
                    var localU = textureCoordinate.X * ModelScale;
                    var localV = textureCoordinate.Y * ModelScale;
                    var scaledPosition = new Vector3D<float>(
                        position.X * ModelScale,
                        position.Y * ModelScale,
                        position.Z * ModelScale);
                    vertices.Add(new BakedVertex(
                        RotatePosition(scaledPosition, rotation),
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

    private static Vector3D<float> RotatePosition(
        Vector3D<float> position,
        BlockModelRotation rotation)
    {
        var rotated = RotateVector(
            new Vector3D<float>(
                position.X - 0.5f,
                position.Y - 0.5f,
                position.Z - 0.5f),
            rotation);
        return new Vector3D<float>(
            rotated.X + 0.5f,
            rotated.Y + 0.5f,
            rotated.Z + 0.5f);
    }

    private static Vector3D<float> RotateVector(
        Vector3D<float> value,
        BlockModelRotation rotation)
    {
        for (var turn = 0; turn < rotation.X / 90; turn++)
            value = new Vector3D<float>(value.X, -value.Z, value.Y);
        for (var turn = 0; turn < rotation.Y / 90; turn++)
            value = new Vector3D<float>(value.Z, value.Y, -value.X);
        for (var turn = 0; turn < rotation.Z / 90; turn++)
            value = new Vector3D<float>(-value.Y, value.X, value.Z);
        return value;
    }

    private static DirectionFlags RotateCull(
        DirectionFlags cull,
        BlockModelRotation rotation)
    {
        var result = DirectionFlags.None;
        foreach (var direction in Enum.GetValues<Direction>())
        {
            if ((cull & direction.ToFlag()) == DirectionFlags.None)
                continue;
            result |= GetDirection(RotateVector(GetNormal(direction), rotation)).ToFlag();
        }

        return result;
    }

    private static Direction GetDirection(Vector3D<float> value)
    {
        return value switch
        {
            { X: 0, Y: -1, Z: 0 } => Direction.Down,
            { X: 0, Y: 1, Z: 0 } => Direction.Up,
            { X: 0, Y: 0, Z: -1 } => Direction.North,
            { X: 0, Y: 0, Z: 1 } => Direction.South,
            { X: -1, Y: 0, Z: 0 } => Direction.West,
            { X: 1, Y: 0, Z: 0 } => Direction.East,
            _ => throw new InvalidOperationException("Rotated block model direction is not axis-aligned.")
        };
    }
}