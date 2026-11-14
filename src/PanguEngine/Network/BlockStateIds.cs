using PanguEngine.Registries;
using PanguEngine.World.Blocks;

namespace PanguEngine.Network;

/// <summary>Provides session identifiers for states of the bound block definitions.</summary>
internal static class BlockStateIds
{
    private static int[] _offsets = [];
    private static BlockState[] _states = [];

    /// <summary>Builds identifiers from the current authoritative block order.</summary>
    internal static void Build()
    {
        var blocks = BuiltinIdTables.Block;
        var offsets = new int[blocks.Count];
        var count = 0;
        foreach (var entry in blocks.Entries)
        {
            offsets[entry.Id] = count;
            count = checked(count + entry.Value.StateDefinition.States.Count);
        }
        var states = new BlockState[count];
        foreach (var entry in blocks.Entries)
        {
            var index = offsets[entry.Id];
            foreach (var state in entry.Value.StateDefinition.States)
                states[index++] = state;
        }
        _offsets = offsets;
        _states = states;
    }

    /// <summary>Gets the session identifier of a canonical block state.</summary>
    internal static int GetId(BlockState state) =>
        _offsets[BuiltinIdTables.Block.GetId(state.Block)] + state.StateIndex;

    /// <summary>Gets the canonical block state for a session identifier.</summary>
    internal static BlockState Get(int id) => (uint)id < (uint)_states.Length
        ? _states[id]
        : throw new InvalidDataException($"Unknown block state identifier {id}.");
}
