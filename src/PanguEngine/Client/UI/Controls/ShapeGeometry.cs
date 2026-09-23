using PanguEngine.Client.UI.Drawing.Geometry;

namespace PanguEngine.Client.UI.Controls;

/// <summary>
/// Holds the immutable fill and stroke meshes produced for a shape's arranged geometry.
/// </summary>
internal sealed class ShapeGeometry
{
    /// <summary>
    /// Gets a geometry with no fill or stroke area.
    /// </summary>
    internal static ShapeGeometry Empty { get; } =
        new(UiTriangleMesh.Empty, UiTriangleMesh.Empty, Rect.Zero);

    internal ShapeGeometry(
        UiTriangleMesh fillMesh,
        UiTriangleMesh strokeMesh,
        Rect naturalBounds)
    {
        FillMesh = fillMesh;
        StrokeMesh = strokeMesh;
        NaturalBounds = naturalBounds;
    }

    /// <summary>
    /// Gets the fill mesh in local drawing coordinates.
    /// </summary>
    internal UiTriangleMesh FillMesh { get; }

    /// <summary>
    /// Gets the stroke mesh in local drawing coordinates.
    /// </summary>
    internal UiTriangleMesh StrokeMesh { get; }

    /// <summary>
    /// Gets the natural drawing bounds with the top-left corner at the local origin.
    /// </summary>
    internal Rect NaturalBounds { get; }
}
