namespace PanguEngine.Graphics;

/// <summary>
/// Specifies how a render target is stored at the end of rendering.
/// </summary>
public enum StoreOperation
{
    /// <summary>
    /// Stores the rendered contents.
    /// </summary>
    Store,

    /// <summary>
    /// Leaves the stored render target contents undefined.
    /// </summary>
    DontCare
}