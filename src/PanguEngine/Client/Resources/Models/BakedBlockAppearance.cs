using System.Text;
using PanguEngine.Registries;
using PanguEngine.World.Blocks;
using PanguEngine.World.Chunking;

namespace PanguEngine.Client.Resources.Models;

internal sealed class BakedBlockAppearance
{
    private readonly Dictionary<BlockState, WeightedVariant> _variants;
    private readonly ulong _salt;

    internal BakedBlockAppearance(
        ResourceKey blockKey,
        IReadOnlyDictionary<BlockState, IReadOnlyList<BakedBlockAppearanceEntry>> variants)
    {
        _salt = CreateSalt(blockKey);
        _variants = new Dictionary<BlockState, WeightedVariant>(variants.Count);
        foreach (var (state, entries) in variants)
            _variants.Add(state, new WeightedVariant(entries));
    }

    internal BakedBlockModel Get(BlockState state, BlockPos position) =>
        _variants[state].Get(_salt, position);

    private static ulong ComputeHash(ulong salt, BlockPos position)
    {
        var hash = MixCoordinate(salt, position.X);
        hash = MixCoordinate(hash, position.Y);
        return MixCoordinate(hash, position.Z);
    }

    private static ulong CreateSalt(ResourceKey blockKey)
    {
        unchecked
        {
            var hash = 14695981039346656037UL;
            foreach (var value in Encoding.UTF8.GetBytes(blockKey.ToString()))
            {
                hash ^= value;
                hash *= 1099511628211UL;
            }

            return hash;
        }
    }

    private static ulong MixCoordinate(ulong hash, int coordinate)
    {
        unchecked
        {
            hash ^= (uint)coordinate;
            hash = (hash ^ (hash >> 30)) * 0xBF58476D1CE4E5B9UL;
            hash = (hash ^ (hash >> 27)) * 0x94D049BB133111EBUL;
            return hash ^ (hash >> 31);
        }
    }

    private sealed class WeightedVariant
    {
        private readonly WeightedModel[] _models;
        private readonly int _totalWeight;

        internal WeightedVariant(IReadOnlyList<BakedBlockAppearanceEntry> entries)
        {
            _models = new WeightedModel[entries.Count];
            var totalWeight = 0;
            for (var index = 0; index < entries.Count; index++)
            {
                totalWeight += entries[index].Weight;
                _models[index] = new WeightedModel(entries[index].Model, totalWeight);
            }

            _totalWeight = totalWeight;
        }

        internal BakedBlockModel Get(ulong salt, BlockPos position)
        {
            if (_models.Length == 1)
                return _models[0].Model;

            var bucket = ComputeHash(salt, position) % (ulong)_totalWeight;
            foreach (var model in _models)
            {
                if (bucket < (ulong)model.CumulativeWeight)
                    return model.Model;
            }

            throw new InvalidOperationException("Block appearance weight selection is inconsistent.");
        }
    }

    private readonly record struct WeightedModel(
        BakedBlockModel Model,
        int CumulativeWeight);
}

internal readonly record struct BakedBlockAppearanceEntry(
    BakedBlockModel Model,
    int Weight);