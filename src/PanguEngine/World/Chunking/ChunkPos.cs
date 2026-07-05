namespace PanguEngine.World.Chunking;

/// <summary>
/// Identifies a chunk position in chunk space.
/// </summary>
/// <param name="X">The chunk X coordinate.</param>
/// <param name="Y">The chunk Y coordinate.</param>
/// <param name="Z">The chunk Z coordinate.</param>
public readonly record struct ChunkPos(int X, int Y, int Z)
{
    /// <summary>
    /// Offsets this chunk position by the specified component values.
    /// </summary>
    /// <param name="x">The X offset.</param>
    /// <param name="y">The Y offset.</param>
    /// <param name="z">The Z offset.</param>
    /// <returns>The offset chunk position.</returns>
    public ChunkPos Offset(int x, int y, int z)
    {
        return new ChunkPos(X + x, Y + y, Z + z);
    }

    /// <summary>
    /// Offsets this chunk position by another chunk position.
    /// </summary>
    /// <param name="offset">The offset to add.</param>
    /// <returns>The offset chunk position.</returns>
    public ChunkPos Offset(ChunkPos offset)
    {
        return Offset(offset.X, offset.Y, offset.Z);
    }

    /// <summary>
    /// Adds two chunk positions component-wise.
    /// </summary>
    /// <param name="left">The left chunk position.</param>
    /// <param name="right">The right chunk position.</param>
    /// <returns>The component-wise sum.</returns>
    public static ChunkPos operator +(ChunkPos left, ChunkPos right)
    {
        return new ChunkPos(left.X + right.X, left.Y + right.Y, left.Z + right.Z);
    }

    /// <summary>
    /// Subtracts two chunk positions component-wise.
    /// </summary>
    /// <param name="left">The left chunk position.</param>
    /// <param name="right">The right chunk position.</param>
    /// <returns>The component-wise difference.</returns>
    public static ChunkPos operator -(ChunkPos left, ChunkPos right)
    {
        return new ChunkPos(left.X - right.X, left.Y - right.Y, left.Z - right.Z);
    }
}