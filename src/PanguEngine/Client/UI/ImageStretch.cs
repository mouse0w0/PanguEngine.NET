namespace PanguEngine.Client.UI;

/// <summary>
/// Specifies how an image is fitted into its destination bounds.
/// </summary>
public enum ImageStretch
{
    /// <summary>Displays the image at its intrinsic size.</summary>
    None,

    /// <summary>Fills the destination independently on each axis.</summary>
    Fill,

    /// <summary>Fits the complete image while preserving its aspect ratio.</summary>
    Uniform,

    /// <summary>Covers the destination while preserving its aspect ratio.</summary>
    UniformToFill
}
