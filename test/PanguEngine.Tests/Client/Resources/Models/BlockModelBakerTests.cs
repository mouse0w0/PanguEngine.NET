using PanguEngine.Client.Resources.Models;
using PanguEngine.Graphics;
using PanguEngine.Registries;
using PanguEngine.World;
using Silk.NET.Maths;

namespace PanguEngine.Tests.Client.Resources.Models;

public sealed class BlockModelBakerTests
{
    [Fact]
    public void BakesUnitCubeCoordinatesAndUv()
    {
        var texture = ResourceKey.Create("test", "block/stone");
        var builder = new MaxRectsTextureAtlasBuilder<ResourceKey>(32, 32);
        builder.Add(texture, 16, 16, new byte[16 * 16 * 4]);
        var baker = new BlockModelBaker(builder.Build());
        var model = baker.Bake(CreateModel(texture, (Direction.Up, DirectionFlags.None)));
        var writer = new RecordingWriter();

        model.Emit(default, DirectionFlags.None, writer);

        Assert.Equal(
            [
                new Vector3D<float>(0, 1, 0),
                new Vector3D<float>(0, 1, 1),
                new Vector3D<float>(1, 1, 1),
                new Vector3D<float>(1, 1, 0)
            ],
            writer.Positions);
        Assert.All(writer.Normals, normal => Assert.Equal(new Vector3D<float>(0, 1, 0), normal));
        Assert.Equal(
            [
                new Vector2D<float>(0, 0),
                new Vector2D<float>(0, 1),
                new Vector2D<float>(1, 1),
                new Vector2D<float>(1, 0)
            ],
            writer.TexCoords);
        Assert.Equal([0u, 1u, 2u, 0u, 2u, 3u], writer.Indices);
    }

    [Fact]
    public void RotatesModelAroundBlockCenterWithoutChangingUv()
    {
        var texture = ResourceKey.Create("test", "block/stone");
        var builder = new MaxRectsTextureAtlasBuilder<ResourceKey>(32, 32);
        builder.Add(texture, 16, 16, new byte[16 * 16 * 4]);
        var baker = new BlockModelBaker(builder.Build());
        var source = CreateModel(
            texture,
            new BlockFaceUv(0, 0, 16, 16),
            0,
            (Direction.Up, DirectionFlags.Up));
        var unrotated = new RecordingWriter();
        var rotated = new RecordingWriter();

        baker.Bake(source).Emit(default, DirectionFlags.None, unrotated);
        baker.Bake(source, new BlockModelRotation(90, 0, 0))
            .Emit(default, DirectionFlags.None, rotated);

        Assert.Equal(
            [
                new Vector3D<float>(0, 1, 1),
                new Vector3D<float>(0, 0, 1),
                new Vector3D<float>(1, 0, 1),
                new Vector3D<float>(1, 1, 1)
            ],
            rotated.Positions);
        Assert.All(rotated.Normals, normal =>
            Assert.Equal(new Vector3D<float>(0, 0, 1), normal));
        Assert.Equal(unrotated.TexCoords, rotated.TexCoords);
        Assert.Equal(unrotated.Indices, rotated.Indices);
    }

    [Theory]
    [InlineData(Direction.Up, 90, 0, 0, DirectionFlags.Up, DirectionFlags.South, 0f, 0f, 1f)]
    [InlineData(Direction.North, 0, 90, 0, DirectionFlags.North, DirectionFlags.West, -1f, 0f, 0f)]
    [InlineData(Direction.East, 0, 0, 90, DirectionFlags.East, DirectionFlags.Up, 0f, 1f, 0f)]
    [InlineData(Direction.North, 90, 90, 90, DirectionFlags.North, DirectionFlags.West, -1f, 0f, 0f)]
    public void RotatesNormalAndCullDirection(
        Direction direction,
        int x,
        int y,
        int z,
        DirectionFlags originalCull,
        DirectionFlags rotatedCull,
        float normalX,
        float normalY,
        float normalZ)
    {
        var texture = ResourceKey.Create("test", "block/stone");
        var builder = new MaxRectsTextureAtlasBuilder<ResourceKey>(32, 32);
        builder.Add(texture, 16, 16, new byte[16 * 16 * 4]);
        var model = new BlockModelBaker(builder.Build()).Bake(
            CreateModel(
                texture,
                (direction, originalCull)),
            new BlockModelRotation(x, y, z));
        var visible = new RecordingWriter();
        var culled = new RecordingWriter();

        model.Emit(default, originalCull, visible);
        model.Emit(default, rotatedCull, culled);

        Assert.Equal(4u, visible.VertexCount);
        Assert.Equal(0u, culled.VertexCount);
        Assert.All(visible.Normals, normal =>
            Assert.Equal(new Vector3D<float>(normalX, normalY, normalZ), normal));
    }

    [Theory]
    [InlineData(0, 0, 1, 2, 3)]
    [InlineData(90, 3, 0, 1, 2)]
    [InlineData(180, 2, 3, 0, 1)]
    [InlineData(270, 1, 2, 3, 0)]
    public void RotatesExplicitUvCounterclockwise(
        int rotation,
        int first,
        int second,
        int third,
        int fourth)
    {
        var texture = ResourceKey.Create("test", "block/stone");
        var builder = new MaxRectsTextureAtlasBuilder<ResourceKey>(32, 32);
        builder.Add(texture, 16, 16, new byte[16 * 16 * 4]);
        var baker = new BlockModelBaker(builder.Build());
        var rotatedModel = baker.Bake(CreateModel(
            texture,
            new BlockFaceUv(0, 0, 16, 16),
            rotation,
            (Direction.Up, DirectionFlags.None)));
        var unrotatedModel = baker.Bake(CreateModel(
            texture,
            new BlockFaceUv(0, 0, 16, 16),
            0,
            (Direction.Up, DirectionFlags.None)));
        var rotatedWriter = new RecordingWriter();
        var unrotatedWriter = new RecordingWriter();

        rotatedModel.Emit(default, DirectionFlags.None, rotatedWriter);
        unrotatedModel.Emit(default, DirectionFlags.None, unrotatedWriter);

        var corners = new[]
        {
            new Vector2D<float>(0, 0),
            new Vector2D<float>(0, 1),
            new Vector2D<float>(1, 1),
            new Vector2D<float>(1, 0)
        };
        Assert.Equal(
            new[] { corners[first], corners[second], corners[third], corners[fourth] },
            rotatedWriter.TexCoords);
        Assert.Equal(unrotatedWriter.Positions, rotatedWriter.Positions);
        Assert.Equal(unrotatedWriter.Normals, rotatedWriter.Normals);
        Assert.Equal(unrotatedWriter.Indices, rotatedWriter.Indices);
    }

    [Fact]
    public void RotatesMirroredExplicitUvByCornerOrder()
    {
        var texture = ResourceKey.Create("test", "block/stone");
        var builder = new MaxRectsTextureAtlasBuilder<ResourceKey>(32, 32);
        builder.Add(texture, 16, 16, new byte[16 * 16 * 4]);
        var baker = new BlockModelBaker(builder.Build());
        var model = baker.Bake(CreateModel(
            texture,
            new BlockFaceUv(16, 4, 0, 12),
            90,
            (Direction.Up, DirectionFlags.None)));
        var writer = new RecordingWriter();

        model.Emit(default, DirectionFlags.None, writer);

        Assert.Equal(
            [
                new Vector2D<float>(0, 0.25f),
                new Vector2D<float>(1, 0.25f),
                new Vector2D<float>(1, 0.75f),
                new Vector2D<float>(0, 0.75f)
            ],
            writer.TexCoords);
    }

    [Fact]
    public void RotatesDegenerateExplicitUvByCornerOrder()
    {
        var texture = ResourceKey.Create("test", "block/stone");
        var builder = new MaxRectsTextureAtlasBuilder<ResourceKey>(32, 32);
        builder.Add(texture, 16, 16, new byte[16 * 16 * 4]);
        var baker = new BlockModelBaker(builder.Build());
        var model = baker.Bake(CreateModel(
            texture,
            new BlockFaceUv(8, 12, 8, 4),
            90,
            (Direction.Up, DirectionFlags.None)));
        var writer = new RecordingWriter();

        model.Emit(default, DirectionFlags.None, writer);

        Assert.Equal(
            [
                new Vector2D<float>(0.5f, 0.75f),
                new Vector2D<float>(0.5f, 0.75f),
                new Vector2D<float>(0.5f, 0.25f),
                new Vector2D<float>(0.5f, 0.25f)
            ],
            writer.TexCoords);
    }

    [Fact]
    public void MissingTextureRegionIsConsistencyError()
    {
        var texture = ResourceKey.Create("test", "block/missing");
        var atlas = new MaxRectsTextureAtlasBuilder<ResourceKey>(32, 32).Build();
        var baker = new BlockModelBaker(atlas);

        Assert.Throws<KeyNotFoundException>(() => baker.Bake(
            CreateModel(texture, (Direction.Up, DirectionFlags.None))));
    }

    private static ResolvedBlockModel CreateModel(
        ResourceKey texture,
        params (Direction Direction, DirectionFlags Cull)[] faces)
    {
        return CreateModel(texture, new BlockFaceUv(0, 0, 16, 16), 0, faces);
    }

    private static ResolvedBlockModel CreateModel(
        ResourceKey texture,
        BlockFaceUv uv,
        int rotation,
        params (Direction Direction, DirectionFlags Cull)[] faces)
    {
        return new ResolvedBlockModel(
            ResourceKey.Create("test", "block/model"),
            [
                new ResolvedBlockElement(
                    new Vector3D<float>(0, 0, 0),
                    new Vector3D<float>(16, 16, 16),
                    faces.ToDictionary(
                        face => face.Direction,
                        face => new ResolvedBlockFace(texture, uv, rotation, face.Cull)))
            ]);
    }

    private sealed class RecordingWriter : IBlockMeshWriter
    {
        public List<Vector3D<float>> Positions { get; } = [];
        public List<Vector3D<float>> Normals { get; } = [];
        public List<Vector2D<float>> TexCoords { get; } = [];
        public List<uint> Indices { get; } = [];

        public uint VertexCount => checked((uint)Positions.Count);

        public void WriteVertex(
            Vector3D<float> position,
            Vector2D<float> texCoord,
            Vector3D<float> normal)
        {
            Positions.Add(position);
            Normals.Add(normal);
            TexCoords.Add(texCoord);
        }

        public void WriteIndex(uint index)
        {
            Indices.Add(index);
        }
    }
}