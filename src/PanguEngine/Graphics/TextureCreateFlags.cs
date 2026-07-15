namespace PanguEngine.Graphics;

/// <summary>
/// Specifies texture creation capabilities.
/// </summary>
[Flags]
public enum TextureCreateFlags
{
    /// <summary>
    /// No additional texture creation capabilities.
    /// </summary>
    None = 0,

    /// <summary>
    /// The texture can be interpreted through cube texture views.
    /// </summary>
    CubeCompatible = 1 << 0
}