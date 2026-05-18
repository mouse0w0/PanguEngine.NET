namespace PanguEngine.Graphics;

/// <summary>
/// Pixel formats supported by texture resources.
/// </summary>
public enum TextureFormat
{
    /// <summary>
    /// Four 8-bit unsigned normalized channels in RGBA order.
    /// </summary>
    R8G8B8A8Unorm,

    /// <summary>
    /// Four 8-bit unsigned normalized channels in BGRA order.
    /// </summary>
    B8G8R8A8Unorm,

    /// <summary>
    /// One 8-bit unsigned normalized channel.
    /// </summary>
    R8Unorm
}