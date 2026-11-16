using PanguEngine.World.Blocks;
using PanguEngine.World.Chunking;

namespace PanguEngine.Tests.World.Chunking;

[Collection("Network globals")]
public sealed class BlockStateStorageTests
{
    [Fact]
    public void SnapshotUsesTheProvidedEntriesAndValidEntryCount()
    {
        var entries = new BlockState[16];
        entries[0] = BuiltinBlocks.Air.DefaultState;
        entries[1] = BuiltinBlocks.Stone.DefaultState;
        var words = new ulong[PackedBlockStates.WordCount(4)];
        PackedBlockStates.Set(words, 4, 37, 1);

        var snapshot = new BlockStateSnapshot(4, null, entries, 2, words);

        Assert.Same(entries, snapshot.Entries);
        Assert.Equal(2, snapshot.EntryCount);
        Assert.Same(BuiltinBlocks.Stone.DefaultState, snapshot.Get(37));
        Assert.Same(BuiltinBlocks.Air.DefaultState, snapshot.Get(38));
    }

    [Fact]
    public void SingleValueUsesZeroBitStorageUntilTheStateChanges()
    {
        var storage = new BlockStateStorage(BuiltinBlocks.Air.DefaultState);
        storage.Set(37, BuiltinBlocks.Air.DefaultState);

        var snapshot = storage.Capture();

        Assert.Equal(0, snapshot.Bits);
        Assert.Empty(snapshot.Words);
        Assert.Equal(0, snapshot.EntryCount);
        Assert.Empty(snapshot.Entries);
        Assert.Same(BuiltinBlocks.Air.DefaultState, snapshot.Get(Chunk.Volume - 1));
        storage.Set(37, BuiltinBlocks.Stone.DefaultState);
        Assert.Equal(4, storage.Capture().Bits);
        Assert.Same(BuiltinBlocks.Air.DefaultState, storage.Get(36));
        Assert.Same(BuiltinBlocks.Stone.DefaultState, storage.Get(37));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void LocalIndicesRoundTripAcrossWordBoundariesAndLastSlot(int bits)
    {
        var count = (1 << (bits - 1)) + 1;
        var states = new Block(BlockProperty.CreateInteger("variant", 0, count - 1)).StateDefinition.States;
        var storage = new BlockStateStorage(states[0]);
        for (var i = 0; i < Chunk.Volume; i++)
            storage.Set(i, states[i % count]);

        var snapshot = storage.Capture();

        Assert.Equal(bits, snapshot.Bits);
        Assert.Equal((Chunk.Volume + 64 / bits - 1) / (64 / bits), snapshot.WordCount);
        for (var i = 0; i < Chunk.Volume; i++)
            Assert.Same(states[i % count], snapshot.Get(i));
    }

    [Fact]
    public void GrowthCompactsHistoricalEntriesWithoutUndoingTheNewWidth()
    {
        var states = new Block(BlockProperty.CreateInteger("variant", 0, 31)).StateDefinition.States;
        var storage = new BlockStateStorage(states[0]);
        for (var i = 1; i < 16; i++)
            storage.Set(0, states[i]);
        storage.Set(0, states[16]);

        var snapshot = storage.Capture();

        Assert.Equal(5, snapshot.Bits);
        Assert.Equal(3, snapshot.EntryCount);
        Assert.Same(states[16], snapshot.Get(0));
        Assert.Same(states[0], snapshot.Get(Chunk.Volume - 1));
        storage.Set(0, states[15]);
        Assert.Same(states[15], storage.Get(0));
        Assert.Same(states[16], snapshot.Get(0));
    }

    [Fact]
    public void SnapshotAndImportedWritableStorageOwnIndependentArrays()
    {
        var states = new Block(BlockProperty.CreateInteger("variant", 0, 31)).StateDefinition.States;
        var storage = new BlockStateStorage(states[0]);
        for (var i = 0; i < states.Count; i++)
            storage.Set(i, states[i]);
        var snapshot = storage.Capture();
        var imported = new BlockStateStorage(snapshot);

        storage.Set(3, states[4]);
        imported.Set(4, states[5]);

        Assert.Same(states[3], snapshot.Get(3));
        Assert.Same(states[4], snapshot.Get(4));
        Assert.Same(states[3], imported.Get(3));
        Assert.Same(states[4], storage.Get(4));
    }

    [Fact]
    public void FullLocalPaletteConvertsToBoundGlobalStateIds()
    {
        TestRegistryBootstrap.Manager.AllocateIdTables();
        BlockStateIds.Build();
        var states = TestRegistryBootstrap.PaletteBlock.StateDefinition.States;
        var storage = new BlockStateStorage(states[0]);
        for (var i = 0; i < Chunk.Volume; i++)
            storage.Set(i, states[i % states.Count]);

        var snapshot = storage.Capture();

        Assert.Equal(BlockStateIds.Bits, snapshot.Bits);
        Assert.True(snapshot.Bits > 8);
        Assert.Equal(0, snapshot.EntryCount);
        Assert.Empty(snapshot.Entries);
        for (var i = 0; i < Chunk.Volume; i++)
            Assert.Same(states[i % states.Count], snapshot.Get(i));
        var chunk = new Chunk(default);
        chunk.SetBlock(default, BuiltinBlocks.Stone.DefaultState);
        chunk.ReplaceBlocks(snapshot);
        Assert.Same(states[^1], chunk.GetBlock(new BlockPos(15, 15, 15)));
        storage.Set(Chunk.Volume - 1, states[0]);
        Assert.Same(states[^1], snapshot.Get(Chunk.Volume - 1));
    }
}
