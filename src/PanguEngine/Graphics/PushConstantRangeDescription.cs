using System.Diagnostics.CodeAnalysis;

namespace PanguEngine.Graphics;

/// <summary>
/// Describes a push constant range used by a graphics pipeline.
/// </summary>
public readonly record struct PushConstantRangeDescription
{
    /// <summary>
    /// Creates a push constant range description.
    /// </summary>
    /// <param name="stages">The shader stages that access the range.</param>
    /// <param name="offset">The byte offset of the range.</param>
    /// <param name="size">The size of the range in bytes.</param>
    [SetsRequiredMembers]
    public PushConstantRangeDescription(ShaderStageFlags stages, uint offset, uint size)
    {
        Stages = stages;
        Offset = offset;
        Size = size;
    }

    /// <summary>
    /// The shader stages that access the range.
    /// </summary>
    public required ShaderStageFlags Stages { get; init; }

    /// <summary>
    /// The byte offset of the range.
    /// </summary>
    public required uint Offset { get; init; }

    /// <summary>
    /// The size of the range in bytes.
    /// </summary>
    public required uint Size { get; init; }
}