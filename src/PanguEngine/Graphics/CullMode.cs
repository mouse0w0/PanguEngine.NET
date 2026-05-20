namespace PanguEngine.Graphics;

/// <summary>
/// Identifies which triangle faces are culled by rasterization.
/// </summary>
public enum CullMode
{
    /// <summary>
    /// Back-facing triangles are culled.
    /// </summary>
    Back,

    /// <summary>
    /// No triangle faces are culled.
    /// </summary>
    None,

    /// <summary>
    /// Front-facing triangles are culled.
    /// </summary>
    Front
}