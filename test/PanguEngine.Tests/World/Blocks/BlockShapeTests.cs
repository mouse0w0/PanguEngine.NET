using PanguEngine.Client.World;
using PanguEngine.World;
using PanguEngine.World.Blocks;
using PanguEngine.World.Chunking;
using Silk.NET.Maths;

namespace PanguEngine.Tests.World.Blocks;

public sealed class BlockShapeTests
{
    [Fact]
    public void EmptyDoesNotIntersectAndHasNoSelectionBoxes()
    {
        var ray = new Ray3D<double>(
            new Vector3D<double>(-1, 0.5, 0.5),
            new Vector3D<double>(1, 0, 0));

        Assert.False(BlockShape.Empty.TryRaycast(ray, 2, out _));
        Assert.Empty(BlockShape.Empty.GetSelectionBoxes());
    }

    [Fact]
    public void FullBlockIntersectsUnitBoxAndReturnsUnitSelectionBox()
    {
        var ray = new Ray3D<double>(
            new Vector3D<double>(-1, 0.5, 0.5),
            new Vector3D<double>(1, 0, 0));

        Assert.True(BlockShape.FullBlock.TryRaycast(ray, 2, out var hit));
        Assert.Equal(new Vector3D<double>(0, 0.5, 0.5), hit.Point);
        Assert.Equal(1, hit.Distance);
        Assert.Equal(Direction.West, hit.Face);
        Assert.False(hit.IsInside);
        var selectionBox = Assert.Single(BlockShape.FullBlock.GetSelectionBoxes());
        Assert.Equal(new Vector3D<double>(0, 0, 0), selectionBox.Min);
        Assert.Equal(new Vector3D<double>(1, 1, 1), selectionBox.Max);
    }

    [Fact]
    public void ConstructorCopiesBoxes()
    {
        var first = new Box3D<double>(0, 0, 0, 0.25, 1, 1);
        var second = new Box3D<double>(0.75, 0, 0, 1, 1, 1);
        var boxes = new[] { first, second };
        var shape = new BlockShape(boxes);

        boxes[0] = second;

        var ray = new Ray3D<double>(
            new Vector3D<double>(-1, 0.5, 0.5),
            new Vector3D<double>(1, 0, 0));
        Assert.True(shape.TryRaycast(ray, 2, out var hit));
        Assert.Equal(new Vector3D<double>(0, 0.5, 0.5), hit.Point);
        Assert.Equal(first, shape.GetSelectionBoxes()[0]);
        Assert.Equal(second, shape.GetSelectionBoxes()[1]);
    }

    [Fact]
    public void MultipleBoxesChooseNearestHitRegardlessOfDeclarationOrder()
    {
        var shape = new BlockShape(
            new Box3D<double>(0.5, 0, 0, 1, 1, 1),
            new Box3D<double>(0, 0, 0, 1, 0.9, 1));
        var ray = new Ray3D<double>(
            new Vector3D<double>(-1, 2, 0.5),
            Vector3D.Normalize(new Vector3D<double>(1, -1, 0)));

        Assert.True(shape.TryRaycast(ray, 3, out var hit));
        Assert.Equal(Direction.Up, hit.Face);
    }

    [Fact]
    public void EqualDistanceBoxesUseDeclarationOrder()
    {
        var shape = new BlockShape(
            new Box3D<double>(0.25, 0, 0, 1, 1, 1),
            new Box3D<double>(0, 0, 0, 1, 0.75, 1));
        var ray = new Ray3D<double>(
            new Vector3D<double>(-1, 2, 0.5),
            Vector3D.Normalize(new Vector3D<double>(1, -1, 0)));

        Assert.True(shape.TryRaycast(ray, 3, out var hit));
        Assert.Equal(Direction.West, hit.Face);
        Assert.Equal(new Vector3D<double>(0.25, 0.75, 0.5), hit.Point);
    }

    [Theory]
    [InlineData(1, 1, 0, Direction.West)]
    [InlineData(0, 1, 1, Direction.Down)]
    [InlineData(1, 1, 1, Direction.West)]
    public void OriginStrictlyInsideUsesStableOppositeDominantDirectionFace(
        double directionX,
        double directionY,
        double directionZ,
        Direction expectedFace)
    {
        var ray = new Ray3D<double>(
            new Vector3D<double>(0.5, 0.5, 0.5),
            Vector3D.Normalize(new Vector3D<double>(directionX, directionY, directionZ)));

        Assert.True(BlockShape.FullBlock.TryRaycast(ray, 0, out var hit));
        Assert.Equal(0, hit.Distance);
        Assert.Equal(expectedFace, hit.Face);
        Assert.True(hit.IsInside);
    }

    [Fact]
    public void RayAlongBoxFaceHasPositiveLengthIntersection()
    {
        var ray = new Ray3D<double>(
            new Vector3D<double>(0, -1, 0.5),
            new Vector3D<double>(0, 1, 0));

        Assert.True(BlockShape.FullBlock.TryRaycast(ray, 2, out var hit));
        Assert.Equal(new Vector3D<double>(0, 0, 0.5), hit.Point);
        Assert.Equal(Direction.Down, hit.Face);
    }

    [Fact]
    public void OriginOnSurfaceMovingOutwardHasNoPositiveLengthIntersection()
    {
        var ray = new Ray3D<double>(
            new Vector3D<double>(0, 0.5, 0.5),
            new Vector3D<double>(-1, 0, 0));

        Assert.False(BlockShape.FullBlock.TryRaycast(ray, 0, out _));
    }

    [Theory]
    [MemberData(nameof(InvalidBoxes))]
    public void ConstructorRejectsInvalidBox(Box3D<double> box)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BlockShape(box));
    }

    [Fact]
    public void ConstructorRejectsNullInput()
    {
        Assert.Throws<ArgumentNullException>(() => new BlockShape((Box3D<double>[]?)null!));
    }

    [Fact]
    public void DefaultBlockReturnsFullBlockShape()
    {
        var block = new Block();
        var world = new ClientWorld();

        Assert.Same(BlockShape.FullBlock, block.DefaultState.GetSelectionShape(world, default));
    }

    [Fact]
    public void AirReturnsEmptyShape()
    {
        var world = new ClientWorld();

        Assert.Same(
            BlockShape.Empty,
            BuiltinBlocks.Air.DefaultState.GetSelectionShape(world, default));
    }

    [Fact]
    public void BlockStateForwardsShapeContextToBlock()
    {
        var block = new ContextBlock();
        var state = block.DefaultState;
        var world = new ClientWorld();
        var position = new BlockPos(2, 3, 4);

        var shape = state.GetSelectionShape(world, position);

        Assert.Same(BlockShape.Empty, shape);
        Assert.Same(state, block.SeenState);
        Assert.Same(world, block.SeenBlocks);
        Assert.Equal(position, block.SeenPosition);
    }

    public static TheoryData<Box3D<double>> InvalidBoxes => new()
    {
        new Box3D<double>(double.NaN, 0, 0, 1, 1, 1),
        new Box3D<double>(0, 0, 0, double.PositiveInfinity, 1, 1),
        new Box3D<double>(-0.01, 0, 0, 1, 1, 1),
        new Box3D<double>(0, 0, 0, 1.01, 1, 1),
        new Box3D<double>(0, 0, 0, 0, 1, 1),
        new Box3D<double>(0.75, 0, 0, 0.25, 1, 1),
        new Box3D<double>(0, 0.5, 0, 1, 0.5, 1),
        new Box3D<double>(0, 0, 0.75, 1, 1, 0.25)
    };

    private sealed class ContextBlock : Block
    {
        internal BlockState? SeenState { get; private set; }

        internal IReadOnlyBlockAccessor? SeenBlocks { get; private set; }

        internal BlockPos SeenPosition { get; private set; }

        public override IBlockShape GetSelectionShape(
            BlockState state,
            IReadOnlyBlockAccessor blockAccessor,
            BlockPos position)
        {
            SeenState = state;
            SeenBlocks = blockAccessor;
            SeenPosition = position;
            return BlockShape.Empty;
        }
    }
}