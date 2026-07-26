using PanguEngine.World;
using PanguEngine.World.Blocks;
using PanguEngine.World.Chunking;

namespace PanguEngine.Tests.World.Blocks;

public sealed class BlockStateTests
{
    private static readonly BlockProperty<bool> Powered = BlockProperty.CreateBoolean("powered");
    private static readonly BlockProperty<int> Level = BlockProperty.CreateInteger("level", 0, 2);

    private static readonly BlockProperty<Direction> Facing =
        BlockProperty.CreateEnum("facing", Direction.North, Direction.South, Direction.West, Direction.East);

    // --- Stateless block ---

    [Fact]
    public void StatelessBlockHasExactlyOneState()
    {
        var block = new Block();

        Assert.Single(block.StateDefinition.States);
        Assert.Same(block.StateDefinition.States[0], block.DefaultState);
    }

    [Fact]
    public void StatelessBlockDefaultStatePropertiesIsEmpty()
    {
        var block = new Block();

        Assert.Empty(block.StateDefinition.Properties);
    }

    [Fact]
    public void StateDefinitionFindsPropertyIndexesByNameAndReference()
    {
        var block = new Block(Powered, Level);
        var definition = block.StateDefinition;

        Assert.Equal(0, definition.GetPropertyIndex("powered"));
        Assert.Equal(0, definition.GetPropertyIndex(Powered));
        Assert.Equal(1, definition.GetPropertyIndex("level"));
        Assert.Equal(-1, definition.GetPropertyIndex("Powered"));
        Assert.Equal(-1, definition.GetPropertyIndex("missing"));
    }

    [Fact]
    public void StateDefinitionRejectsForeignPropertyReferenceWithSameName()
    {
        var block = new Block(Powered);
        var foreign = BlockProperty.CreateBoolean("powered");

        Assert.Equal(-1, block.StateDefinition.GetPropertyIndex(foreign));
        Assert.False(block.DefaultState.Contains(foreign));
        Assert.Throws<ArgumentException>(() => block.DefaultState.Get(foreign));
    }

    // --- State count and cartesian product order ---

    [Fact]
    public void TwoPropertiesProduceCartesianProductStateCount()
    {
        var block = new Block(Powered, Level);

        // powered(2) x level(3) = 6
        Assert.Equal(6, block.StateDefinition.States.Count);
    }

    [Fact]
    public void StatesAreInCartesianProductOrder_LastPropertyFastest()
    {
        var block = new Block(Powered, Level);
        var states = block.StateDefinition.States;

        // Expected order: (false,0),(false,1),(false,2),(true,0),(true,1),(true,2)
        Assert.Equal(false, states[0].Get(Powered));
        Assert.Equal(0, states[0].Get(Level));
        Assert.Equal(false, states[1].Get(Powered));
        Assert.Equal(1, states[1].Get(Level));
        Assert.Equal(false, states[2].Get(Powered));
        Assert.Equal(2, states[2].Get(Level));
        Assert.Equal(true, states[3].Get(Powered));
        Assert.Equal(0, states[3].Get(Level));
        Assert.Equal(true, states[4].Get(Powered));
        Assert.Equal(1, states[4].Get(Level));
        Assert.Equal(true, states[5].Get(Powered));
        Assert.Equal(2, states[5].Get(Level));
    }

    // --- DefaultState is first combination ---

    [Fact]
    public void DefaultStateValuesEqualFirstAllowedValueForEachProperty()
    {
        var block = new Block(Powered, Level);
        var def = block.DefaultState;

        Assert.Equal(Powered.Values[0], def.Get(Powered));
        Assert.Equal(Level.Values[0], def.Get(Level));
    }

    // --- SetDefaultState ---

    [Fact]
    public void SetDefaultStateChangesToSpecifiedCanonicalState()
    {
        var block = new CustomDefaultBlock(Powered, Level);

        // CustomDefaultBlock sets default to (true, 1)
        Assert.Equal(true, block.DefaultState.Get(Powered));
        Assert.Equal(1, block.DefaultState.Get(Level));
    }

    [Fact]
    public void SetDefaultStateFromOtherBlockThrowsArgumentException()
    {
        var blockA = new Block(Powered);
        var blockB = new Block(Powered);
        var stateFromB = blockB.DefaultState;

        Assert.Throws<ArgumentException>(() => new BlockWithExternalState(blockA, stateFromB));
    }

    [Fact]
    public void SetDefaultStateNullThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new BlockWithNullDefault());
    }

    // --- Contains ---

    [Fact]
    public void ContainsReturnsTrueForOwnProperty()
    {
        var block = new Block(Powered);

        Assert.True(block.DefaultState.Contains(Powered));
    }

    [Fact]
    public void ContainsReturnsFalseForForeignProperty()
    {
        var block = new Block(Powered);

        Assert.False(block.DefaultState.Contains(Level));
    }

    [Fact]
    public void ContainsNullThrowsArgumentNullException()
    {
        var block = new Block(Powered);

        Assert.Throws<ArgumentNullException>(() => block.DefaultState.Contains(null!));
    }

    // --- Get ---

    [Fact]
    public void GetReturnsCurrentPropertyValue()
    {
        var block = new Block(Powered, Level);
        var state = block.StateDefinition.States[4]; // (true, 1)

        Assert.Equal(true, state.Get(Powered));
        Assert.Equal(1, state.Get(Level));
    }

    [Fact]
    public void GetWithForeignPropertyThrowsArgumentException()
    {
        var block = new Block(Powered);

        Assert.Throws<ArgumentException>(() => block.DefaultState.Get(Level));
    }

    [Fact]
    public void GetNullThrowsArgumentNullException()
    {
        var block = new Block(Powered);

        Assert.Throws<ArgumentNullException>(() => block.DefaultState.Get<bool>(null!));
    }

    // --- With ---

    [Fact]
    public void WithReturnsCanonicalStateWithNewValue()
    {
        var block = new Block(Powered, Level);
        var initial = block.DefaultState; // (false, 0)
        var next = initial.With(Powered, true);

        Assert.Equal(true, next.Get(Powered));
        Assert.Equal(0, next.Get(Level));
    }

    [Fact]
    public void WithCurrentValueReturnsSameInstance()
    {
        var block = new Block(Powered);
        var state = block.DefaultState;

        Assert.Same(state, state.With(Powered, false));
    }

    [Fact]
    public void WithReturnsSameCanonicalInstanceRegardlessOfPath()
    {
        var block = new Block(Powered, Level);
        // Path A: (false,0) -> With(Powered,true) -> (true,0) -> With(Level,2) -> (true,2)
        var viaA = block.DefaultState.With(Powered, true).With(Level, 2);
        // Path B: (false,0) -> With(Level,2) -> (false,2) -> With(Powered,true) -> (true,2)
        var viaB = block.DefaultState.With(Level, 2).With(Powered, true);

        Assert.Same(viaA, viaB);
    }

    [Fact]
    public void WithChainLocatesCorrectStateIndex()
    {
        var block = new Block(Powered, Level);
        // (true, 2) should be states[5] in the cartesian product order
        var state = block.DefaultState.With(Powered, true).With(Level, 2);

        Assert.Same(block.StateDefinition.States[5], state);
    }

    [Fact]
    public void WithForeignPropertyThrowsArgumentException()
    {
        var block = new Block(Powered);

        Assert.Throws<ArgumentException>(() => block.DefaultState.With(Level, 1));
    }

    [Fact]
    public void WithDisallowedValueThrowsArgumentException()
    {
        // Facing only allows North/South/West/East, not Up/Down
        var block = new Block(Facing);

        Assert.Throws<ArgumentException>(() => block.DefaultState.With(Facing, Direction.Up));
    }

    [Fact]
    public void WithNegativeIncrementFromLargeIndexReturnsCorrectState()
    {
        var block = new Block(Powered, Level);
        // Start at states[5]: (true, 2)
        var state = block.StateDefinition.States[5];

        // Switch back to (false, 0): states[0]
        var result = state.With(Powered, false).With(Level, 0);

        Assert.Same(block.StateDefinition.States[0], result);
        Assert.Equal(false, result.Get(Powered));
        Assert.Equal(0, result.Get(Level));
    }

    [Fact]
    public void WithNullPropertyThrowsArgumentNullException()
    {
        var block = new Block(Powered);

        Assert.Throws<ArgumentNullException>(() => block.DefaultState.With<bool>(null!, true));
    }

    // --- Shared property across blocks ---

    [Fact]
    public void SharedPropertyCanBeDeclaredOnMultipleBlocks()
    {
        var blockA = new Block(Powered);
        var blockB = new Block(Powered);

        Assert.True(blockA.DefaultState.Contains(Powered));
        Assert.True(blockB.DefaultState.Contains(Powered));
    }

    [Fact]
    public void WithOnSharedPropertyReturnsStateFromSameBlock()
    {
        var blockA = new Block(Powered);
        var blockB = new Block(Powered);
        var stateFromA = blockA.DefaultState;

        // Even though Powered is shared, With returns a state that belongs to blockA.
        var result = stateFromA.With(Powered, true);
        Assert.Same(blockA, result.Block);
        Assert.NotSame(blockB, result.Block);
    }

    // --- Reference equality invariant ---

    [Fact]
    public void CanonicalStatesUseReferenceEquality()
    {
        var block = new Block(Powered, Level);

        // Every state in the definition should be the exact same reference as retrieved via With
        foreach (var state in block.StateDefinition.States)
        {
            var poweredVal = state.Get(Powered);
            var levelVal = state.Get(Level);
            var retrieved = block.DefaultState.With(Powered, poweredVal).With(Level, levelVal);
            Assert.Same(state, retrieved);
        }
    }

    // --- Block constructor validation ---

    [Fact]
    public void NullPropertiesArrayThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new Block((BlockProperty[])null!));
    }

    [Fact]
    public void NullElementInPropertiesArrayThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Block(Powered, null!));
    }

    [Fact]
    public void DuplicatePropertyNameThrowsArgumentException()
    {
        var p1 = BlockProperty.CreateBoolean("powered");
        var p2 = BlockProperty.CreateBoolean("powered");

        Assert.Throws<ArgumentException>(() => new Block(p1, p2));
    }

    [Fact]
    public void StateCountExceeding65536ThrowsInvalidOperationException()
    {
        // 257 * 257 = 66049 > 65536
        var p1 = BlockProperty.CreateInteger("a", 0, 256); // 257 values
        var p2 = BlockProperty.CreateInteger("b", 0, 256); // 257 values

        Assert.Throws<InvalidOperationException>(() => new Block(p1, p2));
    }

    [Fact]
    public void StateCountExactly65536Succeeds()
    {
        // 256 * 256 = 65536
        var p1 = BlockProperty.CreateInteger("a", 0, 255); // 256 values
        var p2 = BlockProperty.CreateInteger("b", 0, 255); // 256 values
        var block = new Block(p1, p2);

        Assert.Equal(65536, block.StateDefinition.States.Count);
    }

    // --- Collection immutability ---

    [Fact]
    public void PropertiesCollectionIsReadOnly()
    {
        var block = new Block(Powered);

        Assert.IsAssignableFrom<IReadOnlyList<BlockProperty>>(block.StateDefinition.Properties);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<BlockProperty>)block.StateDefinition.Properties).Add(Level));
    }

    [Fact]
    public void StatesCollectionIsReadOnly()
    {
        var block = new Block(Powered);
        var dummy = new Block().DefaultState;

        Assert.IsAssignableFrom<IReadOnlyList<BlockState>>(block.StateDefinition.States);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<BlockState>)block.StateDefinition.States).Add(dummy));
    }

    // --- Occlusion state-awareness ---

    [Fact]
    public void OccludingBlockPassesStateToCanOccludeFace()
    {
        var block = new OcclusionAwareBlock(Powered);
        var stateOn = block.DefaultState.With(Powered, true);
        var stateOff = block.DefaultState; // powered=false

        Assert.True(stateOn.CanOccludeFace(Direction.North));
        Assert.False(stateOff.CanOccludeFace(Direction.North));
    }

    // --- Existing chunk round-trip ---

    [Fact]
    public void ChunkStoresAndReturnsCanonicalStateInstance()
    {
        var block = new Block(Powered);
        var state = block.DefaultState.With(Powered, true);
        var chunk = new Chunk(new ChunkPos(0, 0, 0));
        var pos = new BlockPos(1, 2, 3);

        chunk.SetBlock(pos, state);

        Assert.Same(state, chunk.GetBlock(pos));
    }

    // --- Helper subclasses ---

    private sealed class CustomDefaultBlock : Block
    {
        internal CustomDefaultBlock(BlockProperty<bool> powered, BlockProperty<int> level)
            : base(powered, level)
        {
            // Set default to (true, 1): states[4] in a (powered x level) block
            SetDefaultState(StateDefinition.States[4]);
        }
    }

    private sealed class BlockWithExternalState : Block
    {
        internal BlockWithExternalState(Block owner, BlockState foreignState)
            : base(BlockProperty.CreateBoolean("powered"))
        {
            // This should throw because foreignState.Block != this
            SetDefaultState(foreignState);
        }
    }

    private sealed class BlockWithNullDefault : Block
    {
        internal BlockWithNullDefault() : base(BlockProperty.CreateBoolean("powered"))
        {
            SetDefaultState(null!);
        }
    }

    private sealed class OcclusionAwareBlock : Block
    {
        internal OcclusionAwareBlock(BlockProperty<bool> powered) : base(powered)
        {
        }

        public override bool CanOccludeFace(BlockState state, Direction direction) =>
            state.Get((BlockProperty<bool>)StateDefinition.Properties[0]);
    }
}