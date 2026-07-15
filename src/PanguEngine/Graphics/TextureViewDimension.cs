namespace PanguEngine.Graphics;

/// <summary>
/// Describes how a texture view interprets its texture subresources.
/// </summary>
public enum TextureViewDimension
{
    /// <summary>
    /// A single one-dimensional texture layer.
    /// </summary>
    Type1D,

    /// <summary>
    /// An array of one-dimensional texture layers.
    /// </summary>
    Type1DArray,

    /// <summary>
    /// A single two-dimensional texture layer.
    /// </summary>
    Type2D,

    /// <summary>
    /// An array of two-dimensional texture layers.
    /// </summary>
    Type2DArray,

    /// <summary>
    /// A three-dimensional texture.
    /// </summary>
    Type3D,

    /// <summary>
    /// A cube texture composed of six two-dimensional layers.
    /// </summary>
    Cube,

    /// <summary>
    /// An array of cube textures.
    /// </summary>
    CubeArray
}