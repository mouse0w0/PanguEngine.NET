namespace PanguEngine.Graphics;

/// <summary>
/// A two-dimensional texture resource.
/// </summary>
public abstract class Texture2D : Texture
{
    /// <summary>
    /// Gets the texture depth in pixels.
    /// </summary>
    public sealed override uint Depth => 1;

    /// <summary>
    /// Gets the number of array layers.
    /// </summary>
    public sealed override uint ArrayLayers => 1;
}