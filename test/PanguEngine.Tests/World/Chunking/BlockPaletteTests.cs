using PanguEngine.World.Blocks;
using PanguEngine.World.Chunking;

namespace PanguEngine.Tests.World.Chunking;

[Collection("Network globals")]
public sealed class BlockPaletteTests
{
    [Fact]
    public void LinearPaletteUsesTheValidEntryCountInsteadOfArrayCapacity()
    {
        var entries = new BlockState[16];
        entries[0] = BuiltinBlocks.Air.DefaultState;
        entries[1] = BuiltinBlocks.Stone.DefaultState;
        entries[2] = BuiltinBlocks.Dirt.DefaultState;
        IBlockPalette palette = new LinearBlockPalette(entries, 3);

        Assert.Equal(3, palette.Count);
        for (var i = 0; i < palette.Count; i++)
            Assert.Same(entries[i], palette.Get(i));
    }

    [Fact]
    public void SingleValueAcceptsItsStateButCannotInsertAnother()
    {
        IBlockPalette palette = new SingleBlockPalette(BuiltinBlocks.Air.DefaultState);

        Assert.True(palette.TryGetOrAdd(BuiltinBlocks.Air.DefaultState, out var id));
        Assert.Equal(0, id);
        Assert.False(palette.TryGetOrAdd(BuiltinBlocks.Stone.DefaultState, out _));
        Assert.Equal(1, palette.Count);
        Assert.Same(BuiltinBlocks.Air.DefaultState, palette.Get(0));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(32)]
    public void FullPaletteStillFindsExistingEntriesAndRejectsNewOnesWithoutMutation(int capacity)
    {
        IBlockPalette palette = capacity == 3 ? new LinearBlockPalette(capacity) : new HashBlockPalette(capacity);
        var states = new Block(BlockProperty.CreateInteger("variant", 0, capacity)).StateDefinition.States;
        for (var i = 0; i < capacity; i++)
        {
            Assert.True(palette.TryGetOrAdd(states[i], out var id));
            Assert.Equal(i, id);
        }

        Assert.True(palette.TryGetOrAdd(states[capacity - 1], out var existing));
        Assert.Equal(capacity - 1, existing);
        Assert.False(palette.TryGetOrAdd(states[capacity], out _));
        Assert.Equal(capacity, palette.Count);
        for (var i = 0; i < capacity; i++)
            Assert.Same(states[i], palette.Get(i));
    }

    [Fact]
    public void GlobalPaletteQueriesTheSessionWithoutInsertingOrHidingUnknownStates()
    {
        TestRegistryBootstrap.Manager.AllocateIdTables();
        BlockStateIds.Build();
        IBlockPalette palette = new GlobalBlockPalette();
        var count = palette.Count;
        var state = TestRegistryBootstrap.PaletteBlock.StateDefinition.States[^1];

        Assert.True(palette.TryGetOrAdd(state, out var id));
        Assert.Equal(BlockStateIds.GetId(state), id);
        Assert.Same(state, palette.Get(id));
        Assert.Throws<KeyNotFoundException>(() => palette.TryGetOrAdd(new Block().DefaultState, out _));
        Assert.Equal(count, palette.Count);
    }
}
