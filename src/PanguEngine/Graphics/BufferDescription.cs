using System.Diagnostics.CodeAnalysis;

namespace PanguEngine.Graphics;

/// <summary>
/// Describes a buffer to be created.
/// </summary>
public readonly record struct BufferDescription
{
    /// <summary>
    /// Creates a buffer description.
    /// </summary>
    /// <param name="size">The size of the buffer in bytes.</param>
    /// <param name="usage">The usage flags for the buffer.</param>
    /// <param name="memoryUsage">The intended memory usage pattern.</param>
    [SetsRequiredMembers]
    public BufferDescription(ulong size, BufferUsage usage, MemoryUsage memoryUsage)
    {
        Size = size;
        Usage = usage;
        MemoryUsage = memoryUsage;
    }

    /// <summary>
    /// The size of the buffer in bytes.
    /// </summary>
    public required ulong Size { get; init; }

    /// <summary>
    /// The usage flags for the buffer.
    /// </summary>
    public required BufferUsage Usage { get; init; }

    /// <summary>
    /// The intended memory usage pattern.
    /// </summary>
    public required MemoryUsage MemoryUsage { get; init; }
}