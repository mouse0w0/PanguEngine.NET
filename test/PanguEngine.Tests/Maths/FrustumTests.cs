using PanguEngine.Maths;
using Silk.NET.Maths;

namespace PanguEngine.Tests.Maths;

public sealed class FrustumTests
{
    [Fact]
    public void IntersectsBoxesInsideOrContainingClipVolume()
    {
        var frustum = Frustum<float>.CreateFromZeroToOne(Matrix4X4<float>.Identity);

        Assert.True(frustum.Intersects(new Box3D<float>(-0.5f, -0.5f, 0.25f, 0.5f, 0.5f, 0.75f)));
        Assert.True(frustum.Intersects(new Box3D<float>(-2, -2, -1, 2, 2, 2)));
    }

    [Fact]
    public void ConstructorPreservesPlanes()
    {
        var left = new Plane<float>(1, 0, 0, 1);
        var right = new Plane<float>(-1, 0, 0, 1);
        var bottom = new Plane<float>(0, 1, 0, 1);
        var top = new Plane<float>(0, -1, 0, 1);
        var near = new Plane<float>(0, 0, 1, 0);
        var far = new Plane<float>(0, 0, -1, 1);

        var frustum = new Frustum<float>(left, right, bottom, top, near, far);

        Assert.Equal(left, frustum.LeftPlane);
        Assert.Equal(right, frustum.RightPlane);
        Assert.Equal(bottom, frustum.BottomPlane);
        Assert.Equal(top, frustum.TopPlane);
        Assert.Equal(near, frustum.NearPlane);
        Assert.Equal(far, frustum.FarPlane);
    }

    [Fact]
    public void FactoriesExtractExpectedPlanesFromMatrixColumns()
    {
        var clipTransform = new Matrix4X4<float>
        {
            M11 = 2,
            M12 = 11,
            M13 = 23,
            M14 = 41,
            M21 = 3,
            M22 = 13,
            M23 = 29,
            M24 = 43,
            M31 = 5,
            M32 = 17,
            M33 = 31,
            M34 = 47,
            M41 = 7,
            M42 = 19,
            M43 = 37,
            M44 = 53
        };

        var zeroToOne = Frustum<float>.CreateFromZeroToOne(clipTransform);
        var negativeOneToOne = Frustum<float>.CreateFromNegativeOneToOne(clipTransform);

        Assert.Equal(new Plane<float>(43, 46, 52, 60), zeroToOne.LeftPlane);
        Assert.Equal(new Plane<float>(39, 40, 42, 46), zeroToOne.RightPlane);
        Assert.Equal(new Plane<float>(52, 56, 64, 72), zeroToOne.BottomPlane);
        Assert.Equal(new Plane<float>(30, 30, 30, 34), zeroToOne.TopPlane);
        Assert.Equal(new Plane<float>(23, 29, 31, 37), zeroToOne.NearPlane);
        Assert.Equal(new Plane<float>(18, 14, 16, 16), zeroToOne.FarPlane);
        Assert.Equal(zeroToOne.LeftPlane, negativeOneToOne.LeftPlane);
        Assert.Equal(zeroToOne.RightPlane, negativeOneToOne.RightPlane);
        Assert.Equal(zeroToOne.BottomPlane, negativeOneToOne.BottomPlane);
        Assert.Equal(zeroToOne.TopPlane, negativeOneToOne.TopPlane);
        Assert.Equal(new Plane<float>(64, 72, 78, 90), negativeOneToOne.NearPlane);
        Assert.Equal(zeroToOne.FarPlane, negativeOneToOne.FarPlane);
    }

    [Fact]
    public void IntersectsBoxUsingMixedSignPositiveVertex()
    {
        var plane = new Plane<float>(1, -1, 1, -1.5f);
        var frustum = new Frustum<float>(plane, plane, plane, plane, plane, plane);

        Assert.True(frustum.Intersects(new Box3D<float>(0, 0, 0, 1, 2, 1)));
    }

    [Fact]
    public void CreatesNegativeOneToOneClipVolume()
    {
        var frustum = Frustum<float>.CreateFromNegativeOneToOne(Matrix4X4<float>.Identity);

        Assert.True(frustum.Intersects(new Box3D<float>(-0.5f, -0.5f, -0.75f, 0.5f, 0.5f, -0.25f)));
        Assert.True(frustum.Intersects(new Box3D<float>(-0.5f, -0.5f, -2, 0.5f, 0.5f, -1)));
        Assert.True(frustum.Intersects(new Box3D<float>(-0.5f, -0.5f, 1, 0.5f, 0.5f, 2)));
        Assert.False(frustum.Intersects(new Box3D<float>(-0.5f, -0.5f, -2, 0.5f, 0.5f, -1.1f)));
        Assert.False(frustum.Intersects(new Box3D<float>(-0.5f, -0.5f, 1.1f, 0.5f, 0.5f, 2)));
    }

    [Fact]
    public void UsesMatrixColumnsForTranslatedNegativeOneToOneClipVolume()
    {
        var clipTransform = Matrix4X4<float>.Identity;
        clipTransform.M43 = 2;
        var frustum = Frustum<float>.CreateFromNegativeOneToOne(clipTransform);

        Assert.True(frustum.Intersects(new Box3D<float>(-0.5f, -0.5f, -2.5f, 0.5f, 0.5f, -1.5f)));
        Assert.False(frustum.Intersects(new Box3D<float>(-0.5f, -0.5f, -0.5f, 0.5f, 0.5f, 0.5f)));
    }

    [Fact]
    public void SupportsDoublePrecision()
    {
        var frustum = Frustum<double>.CreateFromZeroToOne(Matrix4X4<double>.Identity);

        Assert.True(frustum.Intersects(new Box3D<double>(-0.5, -0.5, 0.25, 0.5, 0.5, 0.75)));
        Assert.False(frustum.Intersects(new Box3D<double>(1.1, -0.5, 0.25, 2, 0.5, 0.75)));
    }

    [Fact]
    public void UsesMatrixColumnsForTranslatedClipVolume()
    {
        var clipTransform = Matrix4X4<float>.Identity;
        clipTransform.M41 = 2;
        var frustum = Frustum<float>.CreateFromZeroToOne(clipTransform);

        Assert.True(frustum.Intersects(new Box3D<float>(-2.5f, -0.5f, 0.25f, -1.5f, 0.5f, 0.75f)));
        Assert.False(frustum.Intersects(new Box3D<float>(-0.5f, -0.5f, 0.25f, 0.5f, 0.5f, 0.75f)));
    }

    [Theory]
    [InlineData(-2, -0.5f, 0.25f, -1.1f, 0.5f, 0.75f)]
    [InlineData(1.1f, -0.5f, 0.25f, 2, 0.5f, 0.75f)]
    [InlineData(-0.5f, -2, 0.25f, 0.5f, -1.1f, 0.75f)]
    [InlineData(-0.5f, 1.1f, 0.25f, 0.5f, 2, 0.75f)]
    [InlineData(-0.5f, -0.5f, -1, 0.5f, 0.5f, -0.1f)]
    [InlineData(-0.5f, -0.5f, 1.1f, 0.5f, 0.5f, 2)]
    public void DoesNotIntersectBoxOutsideClipPlane(
        float minX,
        float minY,
        float minZ,
        float maxX,
        float maxY,
        float maxZ)
    {
        var frustum = Frustum<float>.CreateFromZeroToOne(Matrix4X4<float>.Identity);

        Assert.False(frustum.Intersects(new Box3D<float>(minX, minY, minZ, maxX, maxY, maxZ)));
    }

    [Theory]
    [InlineData(-2, -0.5f, 0.25f, -1, 0.5f, 0.75f)]
    [InlineData(1, -0.5f, 0.25f, 2, 0.5f, 0.75f)]
    [InlineData(-0.5f, -2, 0.25f, 0.5f, -1, 0.75f)]
    [InlineData(-0.5f, 1, 0.25f, 0.5f, 2, 0.75f)]
    [InlineData(-0.5f, -0.5f, -1, 0.5f, 0.5f, 0)]
    [InlineData(-0.5f, -0.5f, 1, 0.5f, 0.5f, 2)]
    [InlineData(0.5f, -0.5f, 0.25f, 1.5f, 0.5f, 0.75f)]
    [InlineData(-0.5f, -0.5f, -0.5f, 0.5f, 0.5f, 0.5f)]
    public void IntersectsBoxTouchingOrCrossingClipPlane(
        float minX,
        float minY,
        float minZ,
        float maxX,
        float maxY,
        float maxZ)
    {
        var frustum = Frustum<float>.CreateFromZeroToOne(Matrix4X4<float>.Identity);

        Assert.True(frustum.Intersects(new Box3D<float>(minX, minY, minZ, maxX, maxY, maxZ)));
    }
}