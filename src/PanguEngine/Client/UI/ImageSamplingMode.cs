namespace PanguEngine.Client.UI;

/// <summary>
/// Specifies the interpolation used when sampling an image.
/// </summary>
public enum ImageSamplingMode
{
    /// <summary>Uses linear interpolation between neighboring texels.</summary>
    Linear,

    /// <summary>Uses the nearest texel without interpolation.</summary>
    Nearest
}
