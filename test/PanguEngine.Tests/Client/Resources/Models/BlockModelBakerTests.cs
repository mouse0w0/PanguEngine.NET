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
                        face => new UnbakedFace(texture, null, face.Cull),
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