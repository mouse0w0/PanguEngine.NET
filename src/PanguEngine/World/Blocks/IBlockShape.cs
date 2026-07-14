using Silk.NET.Maths;

namespace PanguEngine.World.Blocks;

/// <summary>
/// Defines the raycast and selection geometry of a block.
/// </summary>
public interface IBlockShape
{
    /// <summary>
    /// Finds the nearest intersection with a block-local ray.
    /// </summary>
    /// <param name="ray">The normalized block-local ray.</param>
    /// <param name="maxDistance">The maximum distance in world units.</param>
    /// <param name="hit">
    /// The nearest block-local intersection with a finite distance in the inclusive range from zero to
    /// <paramref name="maxDistance"/> when one exists.
    /// </param>
    /// <returns>Whether the ray intersects this shape.</returns>
    bool TryRaycast(in Ray3D<double> ray, double maxDistance, out BlockShapeHit hit);

    /// <summary>
    /// Gets the unexpanded block-local boxes used to render this shape's selection outline.
    /// </summary>
    /// <returns>The selection boxes in stable order.</returns>
    IReadOnlyList<Box3D<double>> GetSelectionBoxes();
}