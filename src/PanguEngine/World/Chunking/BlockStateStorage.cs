using System.Buffers;
using PanguEngine.World.Blocks;

namespace PanguEngine.World.Chunking;

/// <summary>Stores mutable chunk states with single-value, local-palette, or global indices.</summary>
internal sealed class BlockStateStorage
{
    private IBlockPalette _palette;
    private ulong[] _words = [];
    private int _bits;

    internal BlockStateStorage(BlockState initialState)
    {
        _palette = new SingleBlockPalette(initialState);
    }

    private BlockStateStorage(int bits)
    {
        _bits = bits;
        _palette = CreatePalette(bits);
        _words = new ulong[PackedBlockStates.WordCount(bits)];
    }

    internal BlockStateStorage(BlockStateSnapshot source)
    {
        _bits = source.Bits;
        _palette = _bits == 0 ? new SingleBlockPalette(source.SingleState!) : CreatePalette(_bits);
        if (_bits == 0)
            return;

        _words = new ulong[source.WordCount];
        source.Words.AsSpan(0, source.WordCount).CopyTo(_words);
        if (_bits <= 8)
        {
            for (var i = 0; i < source.EntryCount; i++)
                _palette.TryGetOrAdd(source.Entries[i], out _);
        }
    }

    internal BlockState Get(int index)
    {
        var id = _bits == 0 ? 0 : PackedBlockStates.Get(_words, _bits, index);
        return _palette.Get(id);
    }

    internal void Set(int index, BlockState state)
    {
        if (!_palette.TryGetOrAdd(state, out var id))
        {
            Grow();
            Set(index, state);
            return;
        }
        if (_bits != 0)
            PackedBlockStates.Set(_words, _bits, index, id);
    }

    /// <summary>Copies the current encoding without rebuilding its palette or reverse lookup.</summary>
    internal BlockStateSnapshot Capture(bool pooled = false)
    {
        if (_bits == 0)
            return new BlockStateSnapshot(0, _palette.Get(0), [], 0, []);

        var paletteCount = _bits <= 8 ? _palette.Count : 0;
        var entries = paletteCount == 0 ? [] : pooled
            ? ArrayPool<BlockState>.Shared.Rent(paletteCount)
            : new BlockState[paletteCount];
        var words = pooled ? ArrayPool<ulong>.Shared.Rent(_words.Length) : new ulong[_words.Length];
        for (var i = 0; i < paletteCount; i++)
            entries[i] = _palette.Get(i);
        _words.AsSpan().CopyTo(words);
        return new BlockStateSnapshot(_bits, null, entries, paletteCount, words);
    }

    private void Grow()
    {
        var bits = _bits == 0 ? 4 : _bits == 8 ? BlockStateIds.Bits : _bits + 1;
        var replacement = new BlockStateStorage(bits);
        if (_bits == 0)
        {
            replacement._palette.TryGetOrAdd(_palette.Get(0), out _);
        }
        else
        {
            for (var i = 0; i < Chunk.Volume; i++)
                replacement.Set(i, Get(i));
        }

        _bits = replacement._bits;
        _palette = replacement._palette;
        _words = replacement._words;
    }

    private static IBlockPalette CreatePalette(int bits) => bits switch
    {
        4 => new LinearBlockPalette(16),
        >= 5 and <= 8 => new HashBlockPalette(1 << bits),
        _ => new GlobalBlockPalette()
    };
}
