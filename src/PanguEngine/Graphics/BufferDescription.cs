namespace PanguEngine.Graphics;

/// <summary>
/// Describes a buffer to be created.
/// </summary>
/// <param name="Size">The size of the buffer in bytes.</param>
/// <param name="Usage">The usage flags for the buffer.</param>
/// <param name="MemoryUsage">The intended memory usage pattern.</param>
public readonly record struct BufferDescription(
    ulong Size,
    BufferUsage Usage,
    MemoryUsage MemoryUsage);