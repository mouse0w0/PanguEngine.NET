namespace PanguEngine.Graphics;

/// <summary>
/// Describes color blending state for a graphics pipeline.
/// </summary>
public readonly record struct ColorBlendDescription
{
    public ColorBlendDescription()
    {
    }

    /// <summary>
    /// Whether standard alpha blending is enabled.
    /// </summary>
    public bool AlphaBlend { get; init; } = false;
}