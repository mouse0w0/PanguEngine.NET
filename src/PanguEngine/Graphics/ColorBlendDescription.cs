namespace PanguEngine.Graphics;

/// <summary>
/// Describes color blending state for a graphics pipeline.
/// </summary>
/// <param name="AlphaBlend">Whether standard alpha blending is enabled.</param>
public readonly record struct ColorBlendDescription(bool AlphaBlend = false);