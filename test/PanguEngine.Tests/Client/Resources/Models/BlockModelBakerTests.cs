using PanguEngine.Client.Resources.Models;
using PanguEngine.Graphics;
using PanguEngine.Registries;
using PanguEngine.World;
using Silk.NET.Maths;

namespace PanguEngine.Tests.Client.Resources.Models;

public sealed class BlockModelBakerTests
{
    [Fact]
    public void BakesUnitCubeCoordinatesAndAutomaticUv()
    {
        var texture = ResourceKey.Create("test", "block/stone");
        var builder = new MaxRectsTextureAtlasBuilder<ResourceKey>(32, 32);
        builder.Add(texture, 16, 16, new byte[16 * 16 * 4]);
        var baker = new BlockModelBaker(builder.Build());
        var model = baker.Bake(CreateModel(new BlockTextureValue.Resource(texture), ("up", [])));
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
            new BlockTextureValue.Resource(texture),
            [0, 0, 16, 16],
            rotation,
            ("up", [])));
        var unrotatedModel = baker.Bake(CreateModel(
            new BlockTextureValue.Resource(texture),
            [0, 0, 16, 16],
            0,
            ("up", [])));
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

    [Theory]
    [InlineData("down")]
    [InlineData("up")]
    [InlineData("north")]
    [InlineData("south")]
    [InlineData("west")]
    [InlineData("east")]
    public void RotatesAutomaticUvCounterclockwiseFromEveryFace(string direction)
    {
        var texture = ResourceKey.Create("test", "block/stone");
        var builder = new MaxRectsTextureAtlasBuilder<ResourceKey>(32, 32);
        builder.Add(texture, 16, 16, new byte[16 * 16 * 4]);
        var baker = new BlockModelBaker(builder.Build());
        var model = baker.Bake(CreateModel(
            new BlockTextureValue.Resource(texture),
            null,
            90,
            (direction, [])));
        var writer = new RecordingWriter();

        model.Emit(default, DirectionFlags.None, writer);

        Assert.Equal(
            [
                new Vector2D<float>(1, 0),
                new Vector2D<float>(0, 0),
                new Vector2D<float>(0, 1),
                new Vector2D<float>(1, 1)
            ],
            writer.TexCoords);
    }

    [Fact]
    public void RotatesMirroredExplicitUvByCornerOrder()
    {
        var texture = ResourceKey.Create("test", "block/stone");
        var builder = new MaxRectsTextureAtlasBuilder<ResourceKey>(32, 32);
        builder.Add(texture, 16, 16, new byte[16 * 16 * 4]);
        var baker = new BlockModelBaker(builder.Build());
        var model = baker.Bake(CreateModel(
            new BlockTextureValue.Resource(texture),
            [16, 4, 0, 12],
            90,
            ("up", [])));
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
            new BlockTextureValue.Resource(texture),
            [8, 12, 8, 4],
            90,
            ("up", [])));
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
            CreateModel(new BlockTextureValue.Resource(texture), ("up", []))));
    }

    [Fact]
    public void UnresolvedTextureVariableIsConsistencyError()
    {
        var atlas = new MaxRectsTextureAtlasBuilder<ResourceKey>(32, 32).Build();
        var baker = new BlockModelBaker(atlas);

        Assert.Throws<InvalidDataException>(() => baker.Bake(
            CreateModel(new BlockTextureValue.Variable("all"), ("up", []))));
    }

    private static UnbakedBlockModel CreateModel(
        BlockTextureValue texture,
        params (string Direction, string[] Cull)[] faces)
    {
        return CreateModel(texture, null, 0, faces);
    }

    private static UnbakedBlockModel CreateModel(
        BlockTextureValue texture,
        float[]? uv,
        int rotation,
        params (string Direction, string[] Cull)[] faces)
    {
        return new UnbakedBlockModel(
            ResourceKey.Create("test", "block/model"),
            null,
            new Dictionary<string, BlockTextureValue>(StringComparer.Ordinal),
            [
                new UnbakedElement(
                    new Vector3D<float>(0, 0, 0),
                    new Vector3D<float>(16, 16, 16),
                    faces.ToDictionary(
                        face => face.Direction,
                        face => new UnbakedFace(texture, uv, rotation, face.Cull),
                        StringComparer.Ordinal))
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