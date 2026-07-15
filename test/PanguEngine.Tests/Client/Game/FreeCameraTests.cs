using PanguEngine.Client.Game;
using Silk.NET.Maths;

namespace PanguEngine.Tests.Client.Game;

public sealed class FreeCameraTests
{
    [Fact]
    public void DefaultsLookTowardNegativeZAndDown()
    {
        var camera = new FreeCamera();

        AssertVector(new Vector3D<double>(8, 6, 24), camera.CurrentPosition);
        Assert.Equal(0, camera.Forward.X);
        Assert.True(camera.Forward.Y < 0);
        Assert.True(camera.Forward.Z < 0);
    }

    [Fact]
    public void MouseDeltaUpdatesYawPitchAndClampsPitch()
    {
        var camera = new FreeCamera();

        camera.ApplyMouseDelta(new Vector2D<float>(10, -2000));

        Assert.Equal(-90 + 10 * FreeCamera.MouseSensitivity, camera.Yaw, 4);
        Assert.Equal(FreeCamera.MaxPitch, camera.Pitch, 4);
    }

    [Fact]
    public void DiagonalMovementHasFixedTickDistance()
    {
        var camera = new FreeCamera();

        camera.Move(1, 1);

        Assert.Equal(
            FreeCamera.MoveDistancePerTick,
            Distance(camera.PreviousPosition, camera.CurrentPosition),
            4);
    }

    [Fact]
    public void OpposingInputDoesNotMove()
    {
        var camera = new FreeCamera();
        var initial = camera.CurrentPosition;

        camera.Move(0, 0);

        AssertVector(initial, camera.PreviousPosition);
        AssertVector(initial, camera.CurrentPosition);
    }

    [Fact]
    public void StationaryTickSynchronizesPreviousToCurrent()
    {
        var camera = new FreeCamera();
        camera.Move(1, 0);
        var current = camera.CurrentPosition;

        camera.Move(0, 0);

        AssertVector(current, camera.PreviousPosition);
        AssertVector(current, camera.CurrentPosition);
    }

    [Fact]
    public void ConsecutiveTicksMaintainPreviousAndCurrentPositions()
    {
        var camera = new FreeCamera();
        camera.Move(1, 0);
        var first = camera.CurrentPosition;

        camera.Move(1, 0);

        AssertVector(first, camera.PreviousPosition);
        Assert.Equal(FreeCamera.MoveDistancePerTick, Distance(first, camera.CurrentPosition), 4);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(0.5, 0.5)]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    public void InterpolatedPositionClampsAlpha(double alpha, double expectedFraction)
    {
        var camera = new FreeCamera();
        camera.Move(1, 0);
        var expected = camera.PreviousPosition
                       + (camera.CurrentPosition - camera.PreviousPosition) * expectedFraction;

        AssertVector(expected, camera.GetInterpolatedPosition(alpha));
    }

    [Fact]
    public void ProjectionMapsNearAndFarPlanesToVulkanDepthRange()
    {
        var projection = FreeCamera.CreateProjectionMatrix(16d / 9d);

        var nearClip = new Vector4D<double>(0, 0, -FreeCamera.NearPlane, 1) * projection;
        var farClip = new Vector4D<double>(0, 0, -FreeCamera.FarPlane, 1) * projection;

        Assert.Equal(0, nearClip.Z / nearClip.W, 4);
        Assert.Equal(1, farClip.Z / farClip.W, 4);
    }

    [Fact]
    public void ProjectionUsesProvidedAspectRatio()
    {
        var square = FreeCamera.CreateProjectionMatrix(1);
        var wide = FreeCamera.CreateProjectionMatrix(2);

        Assert.Equal(square.M11 / 2, wide.M11, 4);
        Assert.Equal(square.M22, wide.M22, 4);
    }

    [Fact]
    public void ProjectionMapsPositiveViewYTowardTopOfVulkanViewport()
    {
        var projection = FreeCamera.CreateProjectionMatrix(1);

        var clip = new Vector4D<double>(0, 1, -1, 1) * projection;

        Assert.True(clip.Y / clip.W < 0);
    }

    [Fact]
    public void DefaultViewTransformsCameraToOriginAndForwardToNegativeZ()
    {
        var camera = new FreeCamera();
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

    [Fact]
    public void ViewProjectionUsesLatestRotationWithoutInterpolation()
    {
        var camera = new FreeCamera();
        camera.Move(1, 0);
        var before = camera.CreateViewProjection(1, 0.5);

        camera.ApplyMouseDelta(new Vector2D<float>(10, 0));
        var after = camera.CreateViewProjection(1, 0.5);

        Assert.NotEqual(before, after);
    }

    private static double Distance(Vector3D<double> left, Vector3D<double> right)
    {
        var delta = right - left;
        return Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y + delta.Z * delta.Z);
    }

    private static void AssertVector(Vector3D<double> expected, Vector3D<double> actual)
    {
        Assert.Equal(expected.X, actual.X, 4);
        Assert.Equal(expected.Y, actual.Y, 4);
        Assert.Equal(expected.Z, actual.Z, 4);
    }
}