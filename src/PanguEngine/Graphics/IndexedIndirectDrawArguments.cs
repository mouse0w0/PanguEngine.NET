using System.Runtime.InteropServices;

namespace PanguEngine.Graphics;

/// <summary>
/// Describes one indexed indirect draw command.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct IndexedIndirectDrawArguments(
    uint indexCount,
    uint instanceCount,
    uint firstIndex,
    int vertexOffset,
    uint firstInstance)
{
    /// <summary>
    /// Gets the size of one command in bytes.
    /// </summary>
    public const uint SizeInBytes = 20;

    /// <summary>
    /// Gets the number of indices to draw.
    /// </summary>
    public readonly uint IndexCount = indexCount;

    /// <summary>
    /// Gets the number of instances to draw.
    /// </summary>
    public readonly uint InstanceCount = instanceCount;

    /// <summary>
    /// Gets the first index in the bound index buffer.
    /// </summary>
    public readonly uint FirstIndex = firstIndex;

    /// <summary>
    /// Gets the signed offset added to each vertex index.
    /// </summary>
    public readonly int VertexOffset = vertexOffset;

    /// <summary>
    /// Gets the first instance identifier.
    /// </summary>
    public readonly uint FirstInstance = firstInstance;
}
