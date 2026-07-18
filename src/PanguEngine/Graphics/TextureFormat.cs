namespace PanguEngine.Graphics;

/// <summary>
/// Pixel formats supported by texture resources.
/// </summary>
public enum TextureFormat
{
    /// <summary>
    /// No texture format is specified.
    /// </summary>
    Undefined,

    /// <summary>
    /// Four 8-bit unsigned normalized channels in RGBA order.
    /// </summary>
    R8G8B8A8Unorm,

    /// <summary>
    /// Four 8-bit normalized SRGB channels in RGBA order.
    /// </summary>
    R8G8B8A8Srgb,

    /// <summary>
    /// Four 8-bit unsigned normalized channels in BGRA order.
    /// </summary>
    B8G8R8A8Unorm,

    /// <summary>
    /// Four 8-bit normalized SRGB channels in BGRA order.
    /// </summary>
    B8G8R8A8Srgb,

    /// <summary>
    /// One 8-bit unsigned normalized channel.
    /// </summary>
    R8Unorm,

    /// <summary>
    /// One 32-bit floating-point depth component.
    /// </summary>
    Depth32Float,

    /// <summary>
    /// One 24-bit unsigned normalized depth component and one 8-bit stencil component.
    /// </summary>
    Depth24UnormStencil8
}