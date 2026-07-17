using PanguEngine.Client.Game;
using PanguEngine.Client.World;
using PanguEngine.World;
using PanguEngine.World.Blocks;
using PanguEngine.World.Chunking;
using PanguEngine.World.Interaction;

namespace PanguEngine.Tests.Client.Game;

public sealed class ClientGamePlacementTests
{
    [Theory]
    [InlineData(Direction.East, 1, 0, 0)]
    public void TryPlaceBlockPlacesStoneAdjacentToSelectedFace(
        Direction face,
        int offsetX,
        int offsetY,
        int offsetZ)
    {
        var world = new ClientWorld();
        var hitPosition = new BlockPos(32, 32, 32);
        world.SetBlock(hitPosition, BuiltinBlocks.Grass.DefaultState);
        var hit = CreateHit(hitPosition, face);

        var placed = ClientGame.TryPlaceBlock(world, hit);

        Assert.True(placed);
        Assert.Same(
            BuiltinBlocks.Stone.DefaultState,
            world.GetBlock(hitPosition.Offset(offsetX, offsetY, offsetZ)));
    }

    [Fact]
    public void TryPlaceBlockReturnsFalseWithoutSelection()
    {
        var world = new ClientWorld();
        var chunksBefore = GetChunkPositions(world);

        Assert.False(ClientGame.TryPlaceBlock(world, null));
        Assert.Equal(chunksBefore, GetChunkPositions(world));
    }

    [Fact]
    public void TryPlaceBlockDoesNotReplaceOccupiedTarget()
    {
        var world = new ClientWorld();
        var hitPosition = new BlockPos(32, 32, 32);
        var targetPosition = hitPosition.Offset(1, 0, 0);
        world.SetBlock(hitPosition, BuiltinBlocks.Grass.DefaultState);
        world.SetBlock(targetPosition, BuiltinBlocks.Dirt.DefaultState);

        var placed = ClientGame.TryPlaceBlock(world, CreateHit(hitPosition, Direction.East));

        Assert.False(placed);
        Assert.Same(BuiltinBlocks.Dirt.DefaultState, world.GetBlock(targetPosition));
    }

    private static BlockHit CreateHit(BlockPos position, Direction face)
    {
        return new BlockHit(
            position,
            BuiltinBlocks.Grass.DefaultState,
            default,
            face,
            0,
            false);
    }

    private static ChunkPos[] GetChunkPositions(ClientWorld world)
    {
        return world.Chunks.EnumerateChunks()
            .Select(chunk => chunk.Position)
            .OrderBy(position => position.X)
            .ThenBy(position => position.Y)
            .ThenBy(position => position.Z)
            .ToArray();
    }
}