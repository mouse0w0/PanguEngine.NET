using System.Buffers;
using PanguEngine.World.Blocks;

namespace PanguEngine.World.Chunking;

/// <summary>Provides an independent read-only representation of one chunk's block states.</summary>
internal sealed class BlockStateSnapshot(
    int bits,
    BlockState? singleState,
    BlockState[] entries,
    int entryCount,
    ulong[] words)
{
    internal static readonly BlockStateSnapshot Air = new(0, BuiltinBlocks.Air.DefaultState, [], 0, []);

    internal int Bits { get; } = bits;
    internal BlockState? SingleState { get; } = singleState;
    internal BlockState[] Entries { get; } = entries;
    internal int EntryCount { get; } = entryCount;
    internal ulong[] Words { get; } = words;
    internal int WordCount => Bits == 0 ? 0 : PackedBlockStates.WordCount(Bits);

    internal BlockState Get(int index)
    {
        if (Bits == 0)
            return SingleState!;
        var id = PackedBlockStates.Get(Words, Bits, index);
        return Bits <= 8 ? Entries[id] : BlockStateIds.Get(id);
    }

    /// <summary>Returns arrays owned by a pooled mesh capture after its last reader finishes.</summary>
    internal void ReturnPooledArrays()
    {
        if (Entries.Length != 0)
            ArrayPool<BlockState>.Shared.Return(Entries, clearArray: true);
        if (WordCount != 0)
            ArrayPool<ulong>.Shared.Return(Words);
    }
}
