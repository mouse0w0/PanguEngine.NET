namespace PanguEngine.World.Blocks;

/// <summary>
/// Defines the shared behavior of a block type.
/// </summary>
public class Block
{
    /// <summary>
    /// Creates a solid non-air block.
    /// </summary>
    public Block()
    {
        DefaultState = new BlockState(this);
    }

    /// <summary>The default state for this block type.</summary>
    public BlockState DefaultState { get; }

    /// <summary>Whether this block represents empty space.</summary>
    public virtual bool IsAir => false;

    /// <summary>
    /// Gets whether the specified face can occlude an adjacent block face.
    /// </summary>
    /// <param name="direction">The direction of the face to inspect.</param>
    /// <returns>Whether the face can occlude an adjacent block face.</returns>
    public virtual bool CanOccludeFace(Direction direction)
    {
        return true;
    }
}