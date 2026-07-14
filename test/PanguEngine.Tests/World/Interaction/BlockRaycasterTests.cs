using PanguEngine.World;
using PanguEngine.World.Blocks;
using PanguEngine.World.Chunking;
using PanguEngine.World.Interaction;
using Silk.NET.Maths;

namespace PanguEngine.Tests.World.Interaction;

public sealed class BlockRaycasterTests
{
    [Theory]
    [InlineData(1, 0, 0, 2, 0, 0, Direction.West)]
    [InlineData(-1, 0, 0, -2, 0, 0, Direction.East)]
    [InlineData(0, 1, 0, 0, 2, 0, Direction.Down)]
    [InlineData(0, -1, 0, 0, -2, 0, Direction.Up)]
    [InlineData(0, 0, 1, 0, 0, 2, Direction.North)]
    [InlineData(0, 0, -1, 0, 0, -2, Direction.South)]
    public void RaycastHitsFullBlockFromSixDirections(
        double directionX,
        double directionY,
        double directionZ,
        int blockX,
        int blockY,
        int blockZ,
        Direction expectedFace)
    {
        var blocks = CreateBlocks((new BlockPos(blockX, blockY, blockZ), new Block().DefaultState));

        var result = Raycast(
            blocks,
            new Vector3D<double>(0.5, 0.5, 0.5),
            new Vector3D<double>(directionX, directionY, directionZ),
            3);

        Assert.NotNull(result);
        Assert.Equal(new BlockPos(blockX, blockY, blockZ), result.Value.BlockPosition);
        Assert.Same(blocks.GetBlock(result.Value.BlockPosition), result.Value.BlockState);
        Assert.Equal(expectedFace, result.Value.Face);
        Assert.Equal(1.5d, result.Value.Distance);
        Assert.False(result.Value.IsInside);
    }

    [Fact]
    public void DirectionMagnitudeDoesNotChangeDistance()
    {
        var blocks = CreateBlocks((new BlockPos(3, 0, 0), new Block().DefaultState));

        var unit = Raycast(blocks, new Vector3D<double>(0.5, 0.5, 0.5), new Vector3D<double>(1, 0, 0), 2.5);
        var scaled = Raycast(blocks, new Vector3D<double>(0.5, 0.5, 0.5), new Vector3D<double>(100, 0, 0), 2.5);

        Assert.Equal(unit, scaled);
        Assert.NotNull(unit);
    }

    [Fact]
    public void RaycastReturnsFirstIntersectedBlock()
    {
        var blocks = CreateBlocks(
            (new BlockPos(2, 0, 0), new Block().DefaultState),
            (new BlockPos(3, 0, 0), new Block().DefaultState));

        var result = Raycast(blocks, new Vector3D<double>(0.5, 0.5, 0.5), new Vector3D<double>(1, 0, 0), 5);

        Assert.Equal(new BlockPos(2, 0, 0), result?.BlockPosition);
    }

    [Fact]
    public void RaycastIncludesMaximumDistanceEndpoint()
    {
        var blocks = CreateBlocks((new BlockPos(2, 0, 0), new Block().DefaultState));
        var origin = new Vector3D<double>(0.5, 0.5, 0.5);
        var direction = new Vector3D<double>(1, 0, 0);
        var ray = new Ray3D<double>(origin, direction);

        Assert.True(BlockRaycaster.TryRaycast(blocks, ray, 1.5, out var hit));
        Assert.Equal(1.5d, hit.Distance);
        Assert.False(BlockRaycaster.TryRaycast(blocks, ray, 1.499, out var miss));
        Assert.Equal(default(BlockHit), miss);
    }

    [Fact]
    public void ZeroDistanceCanEnterNegativeNeighborAtIntegerBoundary()
    {
        var blocks = CreateBlocks((new BlockPos(-1, 0, 0), new Block().DefaultState));

        var result = Raycast(blocks, new Vector3D<double>(0, 0.5, 0.5), new Vector3D<double>(-1, 0, 0), 0);

        Assert.Equal(new BlockPos(-1, 0, 0), result?.BlockPosition);
        Assert.Equal(Direction.East, result?.Face);
    }

    [Fact]
    public void DiagonalTraversalDoesNotHitSideTouchingVoxel()
    {
        var blocks = CreateBlocks(
            (new BlockPos(1, 0, 0), new Block().DefaultState),
            (new BlockPos(1, 1, 0), new Block().DefaultState));

        var result = Raycast(blocks, new Vector3D<double>(0.5, 0.5, 0.5), new Vector3D<double>(1, 1, 0), 3);

        Assert.Equal(new BlockPos(1, 1, 0), result?.BlockPosition);
        Assert.Equal(Direction.West, result?.Face);
    }

    [Fact]
    public void ThreeAxisCornerEntryPrefersXFace()
    {
        var blocks = CreateBlocks((new BlockPos(1, 1, 1), new Block().DefaultState));

        var result = Raycast(blocks, new Vector3D<double>(0.5, 0.5, 0.5), new Vector3D<double>(1, 1, 1), 3);

        Assert.Equal(new BlockPos(1, 1, 1), result?.BlockPosition);
        Assert.Equal(Direction.West, result?.Face);
    }

    [Fact]
    public void RaycastPassesThroughEmptyPartOfPartialShape()
    {
        var slab = new ShapeBlock(new BlockShape(new Box3D<double>(0, 0, 0, 1, 0.5, 1)));
        var blocks = CreateBlocks((new BlockPos(1, 0, 0), slab.DefaultState));

        var result = Raycast(blocks, new Vector3D<double>(0.5, 0.75, 0.5), new Vector3D<double>(1, 0, 0), 2);

        Assert.Null(result);
    }

    [Fact]
    public void RaycastHitsPartialShapeAndUsesActualBoxFace()
    {
        var slab = new ShapeBlock(new BlockShape(new Box3D<double>(0, 0, 0, 1, 0.5, 1)));
        var blocks = CreateBlocks((new BlockPos(0, 0, 0), slab.DefaultState));

        var result = Raycast(blocks, new Vector3D<double>(0.5, 1.5, 0.5), new Vector3D<double>(0, -1, 0), 2);

        Assert.Equal(new BlockPos(0, 0, 0), result?.BlockPosition);
        Assert.Equal(Direction.Up, result?.Face);
        Assert.Equal(new Vector3D<double>(0.5, 0.5, 0.5), result?.Point);
        Assert.Equal(1d, result?.Distance);
        Assert.False(result?.IsInside);
    }

    [Fact]
    public void EmptyShapeIsSkipped()
    {
        var empty = new ShapeBlock(BlockShape.Empty);
        var blocks = CreateBlocks(
            (new BlockPos(1, 0, 0), empty.DefaultState),
            (new BlockPos(2, 0, 0), new Block().DefaultState));

        var result = Raycast(blocks, new Vector3D<double>(0.5, 0.5, 0.5), new Vector3D<double>(1, 0, 0), 3);

        Assert.Equal(new BlockPos(2, 0, 0), result?.BlockPosition);
    }

    [Fact]
    public void OriginOnBoxSurfaceMovingInwardIsExternalZeroDistanceHit()
    {
        var state = new Block().DefaultState;
        var blocks = CreateBlocks((new BlockPos(0, 0, 0), state));
        var origin = new Vector3D<double>(0, 0.5, 0.5);

        var result = Raycast(blocks, origin, new Vector3D<double>(1, 0, 0), 0);

        Assert.Equal(new BlockPos(0, 0, 0), result?.BlockPosition);
        Assert.Same(state, result?.BlockState);
        Assert.Equal(Direction.West, result?.Face);
        Assert.Equal(origin, result?.Point);
        Assert.Equal(0d, result?.Distance);
        Assert.False(result?.IsInside);
    }

    [Fact]
    public void OriginInsideVoxelButOutsideShapeCanHitShapeInSameVoxel()
    {
        var slab = new ShapeBlock(new BlockShape(new Box3D<double>(0, 0, 0, 1, 0.5, 1)));
        var blocks = CreateBlocks((new BlockPos(0, 0, 0), slab.DefaultState));

        var result = Raycast(blocks, new Vector3D<double>(0.5, 0.75, 0.5), new Vector3D<double>(0, -1, 0), 1);

        Assert.Equal(Direction.Up, result?.Face);
    }

    [Fact]
    public void OriginInsideVoxelButOutsideShapeCanMissShape()
    {
        var slab = new ShapeBlock(new BlockShape(new Box3D<double>(0, 0, 0, 1, 0.5, 1)));
        var blocks = CreateBlocks((new BlockPos(0, 0, 0), slab.DefaultState));

        var result = Raycast(blocks, new Vector3D<double>(0.5, 0.75, 0.5), new Vector3D<double>(0, 1, 0), 1);

        Assert.Null(result);
    }

    [Fact]
    public void RaycastHandlesExtremelyLargeFiniteDirection()
    {
        var blocks = CreateBlocks((new BlockPos(1, 0, 0), new Block().DefaultState));

        var result = Raycast(blocks, new Vector3D<double>(0.5, 0.5, 0.5), new Vector3D<double>(double.MaxValue, 0, 0),
            1);

        Assert.Equal(new BlockPos(1, 0, 0), result?.BlockPosition);
    }

    [Theory]
    [MemberData(nameof(InvalidArguments))]
    public void RaycastRejectsInvalidArguments(
        Vector3D<double> origin,
        Vector3D<double> direction,
        double maxDistance)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Raycast(new TestBlockAccessor(), origin, direction, maxDistance));
    }

    [Fact]
    public void RaycastRejectsNullAccessor()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Raycast(null!, new Vector3D<double>(0, 0, 0), new Vector3D<double>(1, 0, 0), 1));
    }

    [Fact]
    public void RaycastReturnsNullForOriginOutsideBlockCoordinateRange()
    {
        var result = Raycast(
            new TestBlockAccessor(),
            new Vector3D<double>((double)int.MaxValue + 1, 0, 0),
            new Vector3D<double>(1, 0, 0),
            1);

        Assert.Null(result);
    }

    [Fact]
    public void RaycastHitsBlockWhenFaceOutsideWorldCannotBeRepresented()
    {
        var state = new Block().DefaultState;
        var blocks = CreateBlocks((new BlockPos(int.MaxValue, 0, 0), state));
        var origin = new Vector3D<double>((double)int.MaxValue + 0.5, 0.5, 0.5);

        var result = Raycast(
            blocks,
            origin,
            new Vector3D<double>(-1, 0, 0),
            1);

        Assert.Equal(new BlockPos(int.MaxValue, 0, 0), result?.BlockPosition);
        Assert.Same(state, result?.BlockState);
        Assert.Equal(Direction.East, result?.Face);
        Assert.Equal(origin, result?.Point);
        Assert.True(result?.IsInside);
    }

    [Fact]
    public void RaycastStopsWhenPositiveStepLeavesBlockCoordinateRange()
    {
        var result = Raycast(
            new TestBlockAccessor(),
            new Vector3D<double>(int.MaxValue - 0.5, 0.5, 0.5),
            new Vector3D<double>(1, 0, 0),
            2);

        Assert.Null(result);
    }

    [Fact]
    public void RayAlongBlockSeamChoosesFrontCandidateByCoordinateOrder()
    {
        var blocks = CreateBlocks(
            (new BlockPos(-1, 0, 0), new Block().DefaultState),
            (new BlockPos(0, 0, 0), new Block().DefaultState),
            (new BlockPos(0, 0, 1), new Block().DefaultState));

        var result = Raycast(
            blocks,
            new Vector3D<double>(0, 0.5, -1),
            new Vector3D<double>(0, 0, 1),
            5);

        Assert.Equal(new BlockPos(-1, 0, 0), result?.BlockPosition);
        Assert.Equal(Direction.North, result?.Face);
    }

    [Fact]
    public void FloatStepAlongRayKeepsSeamSelectionStable()
    {
        var blocks = CreateBlocks(
            (new BlockPos(-1, 0, 0), new Block().DefaultState),
            (new BlockPos(0, 0, 0), new Block().DefaultState));
        var z = -1f;

        for (var index = 0; index < 20; index++)
        {
            var result = Raycast(
                blocks,
                new Vector3D<double>(0, 0.5, z),
                new Vector3D<double>(0, 0, 1),
                5);

            Assert.Equal(new BlockPos(-1, 0, 0), result?.BlockPosition);
            z -= 0.1f;
        }
    }

    [Fact]
    public void RaycastDelegatesIntersectionToCustomShape()
    {
        var shape = new TestShape(
            new BlockShapeHit(new Vector3D<double>(0.25, 0.5, 0.5), Direction.West, 0.25, false));
        var state = new ShapeBlock(shape).DefaultState;
        var blocks = CreateBlocks((new BlockPos(2, 3, 4), state));

        var result = Raycast(
            blocks,
            new Vector3D<double>(2, 3.5, 4.5),
            new Vector3D<double>(1, 0, 0),
            1);

        Assert.Equal(new Vector3D<double>(2.25, 3.5, 4.5), result?.Point);
        Assert.Equal(0.25d, result?.Distance);
        Assert.Same(state, result?.BlockState);
    }

    [Fact]
    public void CustomShapeHitOutsideCandidateIntervalDoesNotHideCloserBlock()
    {
        var invalidShape = new TestShape(
            new BlockShapeHit(new Vector3D<double>(0.5, 0.5, 0.5), Direction.West, 2, false));
        var blocks = CreateBlocks(
            (new BlockPos(0, 0, 0), new ShapeBlock(invalidShape).DefaultState),
            (new BlockPos(1, 0, 0), new Block().DefaultState));

        var result = Raycast(
            blocks,
            new Vector3D<double>(0.5, 0.5, 0.5),
            new Vector3D<double>(1, 0, 0),
            5);

        Assert.Equal(new BlockPos(1, 0, 0), result?.BlockPosition);
    }

    [Fact]
    public void ShapeHitOneUlpBeforeDdaEntryIsNotFilteredOut()
    {
        var shape = new TestShape(
            new BlockShapeHit(
                new Vector3D<double>(0, 0.5, 0.5),
                Direction.West,
                Math.BitDecrement(0.5),
                false));
        var blocks = CreateBlocks((new BlockPos(1, 0, 0), new ShapeBlock(shape).DefaultState));

        var result = Raycast(
            blocks,
            new Vector3D<double>(0.5, 0.5, 0.5),
            new Vector3D<double>(1, 0, 0),
            2);

        Assert.Equal(new BlockPos(1, 0, 0), result?.BlockPosition);
    }

    [Fact]
    public void TraverseIncludesMaximumDistanceEndpoint()
    {
        var candidates = Traverse(
            new Vector3D<double>(0.5, 0.5, 0.5),
            new Vector3D<double>(1, 0, 0),
            2.5);

        Assert.Equal(
            [
                new BlockPos(0, 0, 0),
                new BlockPos(1, 0, 0),
                new BlockPos(2, 0, 0),
                new BlockPos(3, 0, 0)
            ],
            candidates);
    }

    [Fact]
    public void TraverseTiedMovingAxesAdvancesOnlyIntoDiagonalVoxel()
    {
        var candidates = Traverse(
            new Vector3D<double>(0.5, 0.5, 0.5),
            new Vector3D<double>(1, 1, 0),
            1);

        Assert.Equal(
            [
                new BlockPos(0, 0, 0),
                new BlockPos(1, 1, 0)
            ],
            candidates);
    }

    [Fact]
    public void TraverseOneStationaryBoundaryAxisEnumeratesBothSidesInCoordinateOrder()
    {
        var candidates = Traverse(
            new Vector3D<double>(0, 0.5, 0.5),
            new Vector3D<double>(0, 0, 1),
            0);

        Assert.Equal(
            [
                new BlockPos(-1, 0, 0),
                new BlockPos(0, 0, 0)
            ],
            candidates);
    }

    [Fact]
    public void TraverseTwoStationaryBoundaryAxesEnumeratesFourSidesInCoordinateOrder()
    {
        var candidates = Traverse(
            new Vector3D<double>(0, 0, 0.5),
            new Vector3D<double>(0, 0, 1),
            0);

        Assert.Equal(
            [
                new BlockPos(-1, -1, 0),
                new BlockPos(-1, 0, 0),
                new BlockPos(0, -1, 0),
                new BlockPos(0, 0, 0)
            ],
            candidates);
    }

    [Fact]
    public void TraverseNegativeIntegerOriginStartsInNegativeDirectionVoxelAtZeroDistance()
    {
        var candidates = Traverse(
            new Vector3D<double>(0, 0.5, 0.5),
            new Vector3D<double>(-1, 0, 0),
            0);

        Assert.Equal([new BlockPos(-1, 0, 0)], candidates);
    }

    [Fact]
    public void TraversePositiveIntegerOriginStartsInPositiveDirectionVoxelAtZeroDistance()
    {
        var candidates = Traverse(
            new Vector3D<double>(2, 0.5, 0.5),
            new Vector3D<double>(1, 0, 0),
            0);

        Assert.Equal([new BlockPos(2, 0, 0)], candidates);
    }

    [Fact]
    public void TraverseNegativeCoordinatesUsesFloorWithoutDuplicateCandidates()
    {
        var candidates = Traverse(
            new Vector3D<double>(-0.25, 0.5, 0.5),
            new Vector3D<double>(-1, 0, 0),
            1);

        Assert.Equal(
            [
                new BlockPos(-1, 0, 0),
                new BlockPos(-2, 0, 0)
            ],
            candidates);
    }

    [Fact]
    public void TraverseStopsAtMinimumBlockCoordinate()
    {
        var candidates = Traverse(
            new Vector3D<double>(int.MinValue + 0.5, 0.5, 0.5),
            new Vector3D<double>(-1, 0, 0),
            2);

        Assert.Equal([new BlockPos(int.MinValue, 0, 0)], candidates);
    }

    [Fact]
    public void TraverseDirectionMagnitudeDoesNotChangeCandidates()
    {
        var origin = new Vector3D<double>(0.5, 0.5, 0.5);

        Assert.Equal(
            Traverse(origin, new Vector3D<double>(1, 1, 0), 2),
            Traverse(origin, new Vector3D<double>(100, 100, 0), 2));
    }

    [Fact]
    public void EqualDistanceCandidatesUseXThenYThenZCoordinateOrder()
    {
        var shape = new TestShape(
            new BlockShapeHit(
                new Vector3D<double>(0, 0, 0.5),
                Direction.North,
                0.5,
                false));
        var state = new ShapeBlock(shape).DefaultState;
        var blocks = CreateBlocks(
            (new BlockPos(0, 0, -1), state),
            (new BlockPos(-1, 0, 0), state),
            (new BlockPos(-1, -1, 1), state),
            (new BlockPos(-1, -1, 2), state));

        var result = Raycast(
            blocks,
            new Vector3D<double>(0, 0, -1),
            new Vector3D<double>(0, 0, 1),
            4);

        Assert.Equal(new BlockPos(-1, -1, 1), result?.BlockPosition);
    }

    [Theory]
    [MemberData(nameof(InvalidArguments))]
    public void TraverseRejectsInvalidArgumentsAtCallTime(
        Vector3D<double> origin,
        Vector3D<double> direction,
        double maxDistance)
    {
        var ray = new Ray3D<double>(origin, direction);

        Assert.Throws<ArgumentOutOfRangeException>(() => BlockRaycaster.Traverse(ray, maxDistance));
    }

    public static TheoryData<Vector3D<double>, Vector3D<double>, double> InvalidArguments => new()
    {
        { new Vector3D<double>(double.NaN, 0, 0), new Vector3D<double>(1, 0, 0), 1 },
        { new Vector3D<double>(0, 0, 0), new Vector3D<double>(double.NaN, 0, 0), 1 },
        { new Vector3D<double>(0, 0, 0), new Vector3D<double>(double.PositiveInfinity, 0, 0), 1 },
        { new Vector3D<double>(0, 0, 0), new Vector3D<double>(0, 0, 0), 1 },
        { new Vector3D<double>(0, 0, 0), new Vector3D<double>(1, 0, 0), double.NaN },
        { new Vector3D<double>(0, 0, 0), new Vector3D<double>(1, 0, 0), double.PositiveInfinity },
        { new Vector3D<double>(0, 0, 0), new Vector3D<double>(1, 0, 0), -1 }
    };

    private static BlockPos[] Traverse(
        Vector3D<double> origin,
        Vector3D<double> direction,
        double maxDistance)
    {
        var ray = new Ray3D<double>(origin, direction);
        return BlockRaycaster.Traverse(ray, maxDistance).ToArray();
    }

    private static BlockHit? Raycast(
        IReadOnlyBlockAccessor blockAccessor,
        Vector3D<double> origin,
        Vector3D<double> direction,
        double maxDistance)
    {
        var ray = new Ray3D<double>(origin, direction);
        return BlockRaycaster.TryRaycast(blockAccessor, ray, maxDistance, out var hit)
            ? hit
            : null;
    }

    private static TestBlockAccessor CreateBlocks(params (BlockPos Position, BlockState State)[] entries)
    {
        return new TestBlockAccessor(entries);
    }

    private sealed class TestBlockAccessor : IReadOnlyBlockAccessor
    {
        private readonly Dictionary<BlockPos, BlockState> _blocks;

        internal TestBlockAccessor(params (BlockPos Position, BlockState State)[] entries)
        {
            _blocks = entries.ToDictionary(entry => entry.Position, entry => entry.State);
        }

        public BlockState GetBlock(BlockPos position)
        {
            return _blocks.GetValueOrDefault(position, BuiltinBlocks.Air.DefaultState);
        }
    }

    private sealed class ShapeBlock(IBlockShape shape) : Block
    {
        public override IBlockShape GetSelectionShape(
            BlockState state,
            IReadOnlyBlockAccessor blockAccessor,
            BlockPos position)
        {
            return shape;
        }
    }

    private sealed class TestShape(BlockShapeHit hit) : IBlockShape
    {
        public bool TryRaycast(in Ray3D<double> ray, double maxDistance, out BlockShapeHit result)
        {
            result = hit;
            return true;
        }

        public IReadOnlyList<Box3D<double>> GetSelectionBoxes()
        {
            return [];
        }
    }
}