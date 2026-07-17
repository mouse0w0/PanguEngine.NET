using PanguEngine.Client.World;
using PanguEngine.World;
using PanguEngine.World.Blocks;
using PanguEngine.World.Chunking;

namespace PanguEngine.Client.Rendering.World;

internal sealed class ChunkMeshBuilder
{
    public ChunkMesh Build(ClientWorld world, Chunk chunk)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(chunk);

        var vertices = new List<ChunkVertex>();
        foreach (var (localPosition, state) in chunk.EnumerateBlocks())
        {
            if (state.IsAir)
                continue;

            var worldPosition = ToWorldPosition(chunk.Position, localPosition);
            AddVisibleFaces(vertices, world, worldPosition, state, chunk.Position);
        }

        return vertices.Count == 0
            ? new ChunkMesh(Array.Empty<ChunkVertex>())
            : new ChunkMesh(vertices.ToArray());
    }

    private static BlockPos ToWorldPosition(ChunkPos chunkPosition, BlockPos localPosition)
    {
        return new BlockPos(
            chunkPosition.X * Chunk.SizeX + localPosition.X,
            chunkPosition.Y * Chunk.SizeY + localPosition.Y,
            chunkPosition.Z * Chunk.SizeZ + localPosition.Z);
    }

    private static void AddVisibleFaces(
        List<ChunkVertex> vertices,
        ClientWorld world,
        BlockPos position,
        BlockState state,
        ChunkPos chunkPosition)
    {
        if (world.GetBlock(position.Offset(0, -1, 0)).IsAir)
            AddFace(vertices, position, state, Direction.Down, chunkPosition);
        if (world.GetBlock(position.Offset(0, 1, 0)).IsAir)
            AddFace(vertices, position, state, Direction.Up, chunkPosition);
        if (world.GetBlock(position.Offset(0, 0, -1)).IsAir)
            AddFace(vertices, position, state, Direction.North, chunkPosition);
        if (world.GetBlock(position.Offset(0, 0, 1)).IsAir)
            AddFace(vertices, position, state, Direction.South, chunkPosition);
        if (world.GetBlock(position.Offset(-1, 0, 0)).IsAir)
            AddFace(vertices, position, state, Direction.West, chunkPosition);
        if (world.GetBlock(position.Offset(1, 0, 0)).IsAir)
            AddFace(vertices, position, state, Direction.East, chunkPosition);
    }

    private static void AddFace(
        List<ChunkVertex> vertices,
        BlockPos position,
        BlockState state,
        Direction direction,
        ChunkPos chunkPosition)
    {
        var x0 = position.X - chunkPosition.X * Chunk.SizeX;
        var y0 = position.Y - chunkPosition.Y * Chunk.SizeY;
        var z0 = position.Z - chunkPosition.Z * Chunk.SizeZ;
        var x1 = x0 + 1;
        var y1 = y0 + 1;
        var z1 = z0 + 1;
        var color = ApplyShade(GetColor(state), GetFaceShade(direction));

        switch (direction)
        {
            case Direction.Down:
                AddQuad(vertices, color, (x0, y0, z0), (x0, y0, z1), (x1, y0, z1), (x1, y0, z0));
                break;
            case Direction.Up:
                AddQuad(vertices, color, (x0, y1, z0), (x1, y1, z0), (x1, y1, z1), (x0, y1, z1));
                break;
            case Direction.North:
                AddQuad(vertices, color, (x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0));
                break;
            case Direction.South:
                AddQuad(vertices, color, (x0, y0, z1), (x0, y1, z1), (x1, y1, z1), (x1, y0, z1));
                break;
            case Direction.West:
                AddQuad(vertices, color, (x0, y0, z0), (x0, y1, z0), (x0, y1, z1), (x0, y0, z1));
                break;
            case Direction.East:
                AddQuad(vertices, color, (x1, y0, z0), (x1, y0, z1), (x1, y1, z1), (x1, y1, z0));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
        }
    }

    private static void AddQuad(
        List<ChunkVertex> vertices,
        (float R, float G, float B, float A) color,
        (float X, float Y, float Z) first,
        (float X, float Y, float Z) second,
        (float X, float Y, float Z) third,
        (float X, float Y, float Z) fourth)
    {
        vertices.Add(CreateVertex(first, color));
        vertices.Add(CreateVertex(fourth, color));
        vertices.Add(CreateVertex(third, color));
        vertices.Add(CreateVertex(first, color));
        vertices.Add(CreateVertex(third, color));
        vertices.Add(CreateVertex(second, color));
    }

    private static ChunkVertex CreateVertex(
        (float X, float Y, float Z) position,
        (float R, float G, float B, float A) color)
    {
        return new ChunkVertex(
            position.X,
            position.Y,
            position.Z,
            color.R,
            color.G,
            color.B,
            color.A);
    }

    private static (float R, float G, float B, float A) GetColor(BlockState state)
    {
        if (ReferenceEquals(state.Block, BuiltinBlocks.Grass))
            return (0.2f, 0.72f, 0.25f, 1f);
        if (ReferenceEquals(state.Block, BuiltinBlocks.Dirt))
            return (0.45f, 0.28f, 0.12f, 1f);
        if (ReferenceEquals(state.Block, BuiltinBlocks.Stone))
            return (0.55f, 0.55f, 0.55f, 1f);

        return (1f, 1f, 1f, 1f);
    }

    private static float GetFaceShade(Direction direction)
    {
        return direction switch
        {
            Direction.Down => 0.5f,
            Direction.Up => 1f,
            Direction.North or Direction.South => 0.8f,
            Direction.West or Direction.East => 0.6f,
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
        };
    }

    private static (float R, float G, float B, float A) ApplyShade(
        (float R, float G, float B, float A) color,
        float shade)
    {
        return (color.R * shade, color.G * shade, color.B * shade, color.A);
    }
}