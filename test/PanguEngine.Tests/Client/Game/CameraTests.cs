using PanguEngine.Client.Game;
using Silk.NET.Maths;

namespace PanguEngine.Tests.Client.Game;

public sealed class CameraTests
{
    [Fact]
    public void ConstructorInitializesTransform()
    {
        var position = new Vector3D<double>(8, 6, 24);
        var camera = new Camera(position, -90, -20);

        AssertVector(position, camera.PreviousPosition);
        AssertVector(position, camera.CurrentPosition);
        Assert.Equal(-90, camera.Yaw);
        Assert.Equal(-20, camera.Pitch);
        Assert.Equal(0, camera.Forward.X);
        Assert.True(camera.Forward.Y < 0);
        Assert.True(camera.Forward.Z < 0);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(0.5, 0.5)]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    public void InterpolatedPositionClampsAlpha(double alpha, double expectedFraction)
    {
        var camera = CreateCamera();
        camera.MoveTo(camera.CurrentPosition + new Vector3D<double>(1, 0, 0));
        var expected = camera.PreviousPosition
                       + (camera.CurrentPosition - camera.PreviousPosition) * expectedFraction;

        AssertVector(expected, camera.GetInterpolatedPosition(alpha));
    }

    [Fact]
    public void MoveToPreservesPreviousPositionAndSetsAbsoluteCurrentPosition()
    {
        var camera = CreateCamera();
        var target = new Vector3D<double>(1, 2, 3);

        camera.MoveTo(target);

        AssertVector(Vector3D<double>.Zero, camera.PreviousPosition);
        AssertVector(target, camera.CurrentPosition);
    }

    [Fact]
    public void TeleportSynchronizesPreviousAndCurrentPositions()
    {
        var camera = CreateCamera();
        var target = new Vector3D<double>(1, 2, 3);

        camera.MoveBy(new Vector3D<double>(4, 0, 0));
        camera.Teleport(target);

        AssertVector(target, camera.PreviousPosition);
        AssertVector(target, camera.CurrentPosition);
    }

    [Fact]
    public void ProjectionMapsNearAndFarPlanesToVulkanDepthRange()
    {
        var camera = CreateCamera();
        camera.AspectRatio = 16d / 9d;
        var projection = camera.CreateProjectionMatrix();

        var nearClip = new Vector4D<double>(0, 0, -camera.NearPlane, 1) * projection;
        var farClip = new Vector4D<double>(0, 0, -camera.FarPlane, 1) * projection;

        Assert.Equal(0, nearClip.Z / nearClip.W, 4);
        Assert.Equal(1, farClip.Z / farClip.W, 4);
    }

    [Fact]
    public void ProjectionUsesAspectRatioProperty()
    {
        var camera = CreateCamera();

        Assert.Equal(1, camera.AspectRatio);

        var square = camera.CreateProjectionMatrix();
        camera.AspectRatio = 2;
        var wide = camera.CreateProjectionMatrix();

        Assert.Equal(square.M11 / 2, wide.M11, 4);
        Assert.Equal(square.M22, wide.M22, 4);
    }

    [Fact]
    public void ProjectionUsesFieldOfViewProperty()
    {
        var camera = CreateCamera();

        Assert.Equal(70d, camera.FieldOfView);
        var defaultProjection = camera.CreateProjectionMatrix();

        camera.FieldOfView = 35d;
        var narrowProjection = camera.CreateProjectionMatrix();

        Assert.NotEqual(defaultProjection.M11, narrowProjection.M11);
    }

    [Fact]
    public void ProjectionMapsPositiveViewYTowardTopOfVulkanViewport()
    {
        var camera = CreateCamera();
        camera.AspectRatio = 1;
        var projection = camera.CreateProjectionMatrix();

        var clip = new Vector4D<double>(0, 1, -1, 1) * projection;

        Assert.True(clip.Y / clip.W < 0);
    }

    [Fact]
    public void ViewTransformsCameraToOriginAndForwardToNegativeZ()
    {
        var camera = CreateCamera();
        var view = camera.CreateViewMatrix(1);

        var cameraInView = new Vector4D<double>(camera.CurrentPosition, 1) * view;
        var pointInView = new Vector4D<double>(camera.CurrentPosition + camera.Forward, 1) * view;

        Assert.Equal(0, cameraInView.X, 4);
        Assert.Equal(0, cameraInView.Y, 4);
        Assert.Equal(0, cameraInView.Z, 4);
        Assert.Equal(0, pointInView.X, 4);
        Assert.Equal(0, pointInView.Y, 4);
        Assert.Equal(-1, pointInView.Z, 4);
    }

    private static void AssertVector(Vector3D<double> expected, Vector3D<double> actual)
    {
        Assert.Equal(expected.X, actual.X, 4);
        Assert.Equal(expected.Y, actual.Y, 4);
        Assert.Equal(expected.Z, actual.Z, 4);
    }

    private static Camera CreateCamera()
    {
        return new Camera(Vector3D<double>.Zero, -90, -20);
    }
}