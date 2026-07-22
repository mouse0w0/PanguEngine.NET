using System.Numerics;
using Silk.NET.Maths;

namespace PanguEngine.Maths;

/// <summary>
/// Represents a frustum defined by six inward-facing planes.
/// </summary>
/// <typeparam name="T">The IEEE 754 floating-point scalar type.</typeparam>
public readonly struct Frustum<T>
    where T : unmanaged, IFloatingPointIeee754<T>
{
    /// <summary>
    /// Creates a frustum from six inward-facing planes.
    /// </summary>
    /// <param name="leftPlane">The left clipping plane.</param>
    /// <param name="rightPlane">The right clipping plane.</param>
    /// <param name="bottomPlane">The bottom clipping plane.</param>
    /// <param name="topPlane">The top clipping plane.</param>
    /// <param name="nearPlane">The near clipping plane.</param>
    /// <param name="farPlane">The far clipping plane.</param>
    public Frustum(
        Plane<T> leftPlane,
        Plane<T> rightPlane,
        Plane<T> bottomPlane,
        Plane<T> topPlane,
        Plane<T> nearPlane,
        Plane<T> farPlane)
    {
        LeftPlane = leftPlane;
        RightPlane = rightPlane;
        BottomPlane = bottomPlane;
        TopPlane = topPlane;
        NearPlane = nearPlane;
        FarPlane = farPlane;
    }

    /// <summary>The left clipping plane.</summary>
    public Plane<T> LeftPlane { get; }

    /// <summary>The right clipping plane.</summary>
    public Plane<T> RightPlane { get; }

    /// <summary>The bottom clipping plane.</summary>
    public Plane<T> BottomPlane { get; }

    /// <summary>The top clipping plane.</summary>
    public Plane<T> TopPlane { get; }

    /// <summary>The near clipping plane.</summary>
    public Plane<T> NearPlane { get; }

    /// <summary>The far clipping plane.</summary>
    public Plane<T> FarPlane { get; }

    /// <summary>
    /// Creates a frustum for a clip transform whose normalized depth range is zero to one.
    /// </summary>
    /// <param name="clipTransform">The transform from the tested coordinate space to clip space.</param>
    /// <returns>The extracted frustum.</returns>
    /// <remarks>
    /// The transform uses the Silk.NET row-vector convention. Extracted planes are not normalized.
    /// </remarks>
    public static Frustum<T> CreateFromZeroToOne(Matrix4X4<T> clipTransform)
    {
        return new Frustum<T>(
            new Plane<T>(clipTransform.Column1 + clipTransform.Column4),
            new Plane<T>(clipTransform.Column4 - clipTransform.Column1),
            new Plane<T>(clipTransform.Column2 + clipTransform.Column4),
            new Plane<T>(clipTransform.Column4 - clipTransform.Column2),
            new Plane<T>(clipTransform.Column3),
            new Plane<T>(clipTransform.Column4 - clipTransform.Column3));
    }

    /// <summary>
    /// Creates a frustum for a clip transform whose normalized depth range is negative one to one.
    /// </summary>
    /// <param name="clipTransform">The transform from the tested coordinate space to clip space.</param>
    /// <returns>The extracted frustum.</returns>
    /// <remarks>
    /// The transform uses the Silk.NET row-vector convention. Extracted planes are not normalized.
    /// </remarks>
    public static Frustum<T> CreateFromNegativeOneToOne(Matrix4X4<T> clipTransform)
    {
        return new Frustum<T>(
            new Plane<T>(clipTransform.Column1 + clipTransform.Column4),
            new Plane<T>(clipTransform.Column4 - clipTransform.Column1),
            new Plane<T>(clipTransform.Column2 + clipTransform.Column4),
            new Plane<T>(clipTransform.Column4 - clipTransform.Column2),
            new Plane<T>(clipTransform.Column3 + clipTransform.Column4),
            new Plane<T>(clipTransform.Column4 - clipTransform.Column3));
    }

    /// <summary>
    /// Determines whether an axis-aligned box intersects this frustum.
    /// </summary>
    /// <param name="bounds">The axis-aligned box to test.</param>
    /// <returns>True when the box is not fully outside any frustum plane.</returns>
    /// <remarks>A box touching a frustum plane is considered intersecting.</remarks>
    public bool Intersects(Box3D<T> bounds)
    {
        return Intersects(LeftPlane, bounds)
               && Intersects(RightPlane, bounds)
               && Intersects(BottomPlane, bounds)
               && Intersects(TopPlane, bounds)
               && Intersects(NearPlane, bounds)
               && Intersects(FarPlane, bounds);
    }

    private static bool Intersects(Plane<T> plane, Box3D<T> bounds)
    {
        var positiveVertex = new Vector3D<T>(
            plane.Normal.X >= T.Zero ? bounds.Max.X : bounds.Min.X,
            plane.Normal.Y >= T.Zero ? bounds.Max.Y : bounds.Min.Y,
            plane.Normal.Z >= T.Zero ? bounds.Max.Z : bounds.Min.Z);
        var value = plane.Normal.X * positiveVertex.X
                    + plane.Normal.Y * positiveVertex.Y
                    + plane.Normal.Z * positiveVertex.Z
                    + plane.Distance;
        return value >= T.Zero;
    }
}