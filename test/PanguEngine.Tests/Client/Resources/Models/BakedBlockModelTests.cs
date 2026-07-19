using PanguEngine.Client.Resources.Models;
using PanguEngine.Graphics;
using PanguEngine.Registries;
using PanguEngine.World;
using Silk.NET.Maths;

namespace PanguEngine.Tests.Client.Resources.Models;

public sealed class BakedBlockModelTests
{
    [Fact]
    public void CullsOnlyWhenAllFaceFlagsArePresent()
    {
        var texture = ResourceKey.Create("test", "block/stone");
        var atlasBuilder = new MaxRectsTextureAtlasBuilder<ResourceKey>(32, 32);
        atlasBuilder.Add(texture, 16, 16, new byte[16 * 16 * 4]);
        var model = new BlockModelBaker(atlasBuilder.Build()).Bake(
            CreateModel(new BlockTextureValue.Resource(texture), ("up", ["up", "north"])));

        var partial = new RecordingWriter();
        model.Emit(default, DirectionFlags.Up, partial);
        Assert.Equal(4u, partial.VertexCount);

        var complete = new RecordingWriter();
        model.Emit(default, DirectionFlags.Up | DirectionFlags.North, complete);
        Assert.Equal(0u, complete.VertexCount);
    }

    [Fact]
    public void RelocatesIndicesFromWriterVertexOffset()
    {
        var texture = ResourceKey.Create("test", "block/stone");
        var atlasBuilder = new MaxRectsTextureAtlasBuilder<ResourceKey>(32, 32);
        atlasBuilder.Add(texture, 16, 16, new byte[16 * 16 * 4]);
        var model = new BlockModelBaker(atlasBuilder.Build()).Bake(
            CreateModel(new BlockTextureValue.Resource(texture), ("up", []), ("down", [])));
        var writer = new RecordingWriter();
        writer.AddExistingVertices(3);

        model.Emit(default, DirectionFlags.None, writer);

        Assert.Equal([3u, 4u, 5u, 3u, 5u, 6u, 7u, 8u, 9u, 7u, 9u, 10u], writer.Indices);
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
        private readonly List<Vector3D<float>> _positions = [];
        public List<uint> Indices { get; } = [];
        public uint VertexCount => checked((uint)_positions.Count);

        public void AddExistingVertices(int count)
        {
            for (var index = 0; index < count; index++)
                _positions.Add(default);
        }

        public void WriteVertex(Vector3D<float> position, Vector2D<float> texCoord, Vector3D<float> normal)
        {
            _positions.Add(position);
        }

        public void WriteIndex(uint index)
        {
            Indices.Add(index);
        }
    }
}