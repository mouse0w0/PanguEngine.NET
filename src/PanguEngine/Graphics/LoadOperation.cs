namespace PanguEngine.Graphics;

/// <summary>
/// Specifies how a render target is loaded at the beginning of rendering.
/// </summary>
public enum LoadOperation
{
    /// <summary>
    /// Loads the existing render target contents.
    /// </summary>
    Load,

    /// <summary>
    /// Clears the render target contents.
    /// </summary>
    Clear,

    /// <summary>
    /// Leaves the initial render target contents undefined.
    /// </summary>
    DontCare
}