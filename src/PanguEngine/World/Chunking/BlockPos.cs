using PanguEngine.World.Blocks;
using Silk.NET.Maths;

namespace PanguEngine.World.Chunking;

/// <summary>
/// Identifies a block position.
/// </summary>
/// <param name="X">The world X coordinate.</param>
/// <param name="Y">The world Y coordinate.</param>
/// <param name="Z">The world Z coordinate.</param>
public readonly record struct BlockPos(int X, int Y, int Z)
{
    /// <summary>
    /// Converts this block position to its containing chunk position.
    /// </summary>
    /// <returns>The chunk position containing this block position.</returns>
    public ChunkPos ToChunkPos()
    {
        return new ChunkPos(X >> Chunk.BitsX, Y >> Chunk.BitsY, Z >> Chunk.BitsZ);
    }

    /// <summary>
    /// Converts this block position to chunk-local block coordinates.
    /// </summary>
    /// <returns>The chunk-local block coordinates.</returns>
    public BlockPos ToChunkLocalPos()
    {
        return new BlockPos(X & Chunk.MaskX, Y & Chunk.MaskY, Z & Chunk.MaskZ);
    }

    /// <summary>
    /// Converts this block position to a Silk.NET vector.
    /// </summary>
    /// <returns>The vector with matching components.</returns>
    public Vector3D<int> ToVector3D()
    {
        return new Vector3D<int>(X, Y, Z);
    }

    /// <summary>
    /// Creates a block position from a Silk.NET vector.
    /// </summary>
    /// <param name="value">The vector to convert.</param>
    /// <returns>The block position with matching components.</returns>
    public static BlockPos FromVector3D(Vector3D<int> value)
    {
        return new BlockPos(value.X, value.Y, value.Z);
    }

    /// <summary>
    /// Offsets this block position by the specified component values.
    /// </summary>
    /// <param name="x">The X offset.</param>
    /// <param name="y">The Y offset.</param>
    /// <param name="z">The Z offset.</param>
    /// <returns>The offset block position.</returns>
    public BlockPos Offset(int x, int y, int z)
    {
        return new BlockPos(X + x, Y + y, Z + z);
    }

    /// <summary>
    /// Offsets this block position by another block position.
    /// </summary>
    /// <param name="offset">The offset to add.</param>
    /// <returns>The offset block position.</returns>
    public BlockPos Offset(BlockPos offset)
    {
        return Offset(offset.X, offset.Y, offset.Z);
    }

    /// <summary>
    /// Offsets this block position by the specified distance in a direction.
    /// </summary>
    /// <param name="direction">The direction to move.</param>
    /// <param name="distance">The signed distance to move.</param>
    /// <returns>The offset block position.</returns>
    public BlockPos Offset(Direction direction, int distance = 1)
    {
        return direction switch
        {
            Direction.Down => Down(distance),
            Direction.Up => Up(distance),
            Direction.North => North(distance),
            Direction.South => South(distance),
            Direction.West => West(distance),
            Direction.East => East(distance),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
        };
    }

    /// <summary>
    /// Offsets this block position upward by the specified distance.
    /// </summary>
    /// <param name="distance">The signed distance to move.</param>
    /// <returns>The offset block position.</returns>
    public BlockPos Up(int distance = 1) => Offset(0, distance, 0);

    /// <summary>
    /// Offsets this block position downward by the specified distance.
    /// </summary>
    /// <param name="distance">The signed distance to move.</param>
    /// <returns>The offset block position.</returns>
    public BlockPos Down(int distance = 1) => Offset(0, -distance, 0);

    /// <summary>
    /// Offsets this block position northward by the specified distance.
    /// </summary>
    /// <param name="distance">The signed distance to move.</param>
    /// <returns>The offset block position.</returns>
    public BlockPos North(int distance = 1) => Offset(0, 0, -distance);

    /// <summary>
    /// Offsets this block position southward by the specified distance.
    /// </summary>
    /// <param name="distance">The signed distance to move.</param>
    /// <returns>The offset block position.</returns>
    public BlockPos South(int distance = 1) => Offset(0, 0, distance);

    /// <summary>
    /// Offsets this block position westward by the specified distance.
    /// </summary>
    /// <param name="distance">The signed distance to move.</param>
    /// <returns>The offset block position.</returns>
    public BlockPos West(int distance = 1) => Offset(-distance, 0, 0);

    /// <summary>
    /// Offsets this block position eastward by the specified distance.
    /// </summary>
    /// <param name="distance">The signed distance to move.</param>
    /// <returns>The offset block position.</returns>
    public BlockPos East(int distance = 1) => Offset(distance, 0, 0);

    /// <summary>
    /// Adds two block positions component-wise.
    /// </summary>
    /// <param name="left">The left block position.</param>
    /// <param name="right">The right block position.</param>
    /// <returns>The component-wise sum.</returns>
    public static BlockPos operator +(BlockPos left, BlockPos right)
    {
        return new BlockPos(left.X + right.X, left.Y + right.Y, left.Z + right.Z);
    }

    /// <summary>
    /// Subtracts two block positions component-wise.
    /// </summary>
    /// <param name="left">The left block position.</param>
    /// <param name="right">The right block position.</param>
    /// <returns>The component-wise difference.</returns>
    public static BlockPos operator -(BlockPos left, BlockPos right)
    {
        return new BlockPos(left.X - right.X, left.Y - right.Y, left.Z - right.Z);
    }
}