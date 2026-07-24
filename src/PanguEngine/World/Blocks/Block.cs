using PanguEngine.Registries;
using PanguEngine.World.Chunking;

namespace PanguEngine.World.Blocks;

/// <summary>
/// Defines the shared behavior of a block type.
/// </summary>
public class Block
{
    /// <summary>
    /// Creates a block with the specified state properties.
    /// Pass no arguments to create a stateless block with exactly one canonical state.
    /// </summary>
    public Block(params BlockProperty[] properties)
    {
        ArgumentNullException.ThrowIfNull(properties);
        StateDefinition = new BlockStateDefinition(this, properties);
        DefaultState = StateDefinition.States[0];
    }

    /// <summary>The state definition that owns all canonical states for this block.</summary>
    public BlockStateDefinition StateDefinition { get; }

    /// <summary>The default state used when this block is placed without explicit state selection.</summary>
    public BlockState DefaultState { get; private set; }

    /// <summary>Whether this block represents empty space.</summary>
    public virtual bool IsAir => false;

    /// <summary>
    /// Gets whether the specified face of <paramref name="state"/> can occlude an adjacent block face.
    /// </summary>
    /// <param name="state">The block state being evaluated.</param>
    /// <param name="direction">The direction of the face to inspect.</param>
    /// <returns>Whether the face can occlude an adjacent block face.</returns>
    public virtual bool CanOccludeFace(BlockState state, Direction direction) => true;

    /// <summary>
    /// Gets the selection shape for a block state at a world position.
    /// </summary>
    /// <param name="state">The block state.</param>
    /// <param name="blockAccessor">The block state accessor.</param>
    /// <param name="position">The world block position.</param>
    /// <returns>The selection shape.</returns>
    public virtual IBlockShape GetSelectionShape(
        BlockState state,
        IReadOnlyBlockAccessor blockAccessor,
        BlockPos position) => BlockShape.FullBlock;

    /// <summary>
    /// Sets the default state for this block. Must only be called from a subclass constructor,
    /// and the state must belong to this block's <see cref="StateDefinition"/>.
    /// </summary>
    /// <param name="state">A canonical state from this block's definition.</param>
    protected void SetDefaultState(BlockState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!ReferenceEquals(state.Block, this))
            throw new ArgumentException(
                "Default state must belong to this block.",
                nameof(state));
        DefaultState = state;
    }

    /// <summary>
    /// Returns the registered resource key of this block, or the type name if not yet registered.
    /// </summary>
    public override string ToString() =>
        BuiltinRegistries.Block.TryGetKey(this, out var key) ? key.ToString() : GetType().Name;
}