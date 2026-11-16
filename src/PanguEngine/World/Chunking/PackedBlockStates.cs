namespace PanguEngine.World.Chunking;

/// <summary>Addresses fixed-width block indices without spanning storage words.</summary>
internal static class PackedBlockStates
{
    internal static int WordCount(int bits) =>
        (Chunk.Volume + 64 / bits - 1) / (64 / bits);

    internal static int Get(ulong[] words, int bits, int index)
    {
        var valuesPerWord = 64 / bits;
        return (int)((words[index / valuesPerWord] >> (index % valuesPerWord * bits)) & ((1UL << bits) - 1));
    }

    internal static void Set(ulong[] words, int bits, int index, int value)
    {
        var valuesPerWord = 64 / bits;
        var word = index / valuesPerWord;
        var shift = index % valuesPerWord * bits;
        var mask = ((1UL << bits) - 1) << shift;
        words[word] = (words[word] & ~mask) | ((ulong)value << shift);
    }
}
