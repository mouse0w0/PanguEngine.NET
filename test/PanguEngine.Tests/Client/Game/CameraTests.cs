using PanguEngine.Client.Game;
using Silk.NET.Maths;

namespace PanguEngine.Tests.Client.Game;

public sealed class CameraTests
{
    [Fact]
    public void ConstructorInitializesPositionAndViewDirection()
    {
        var position = new Vector3D<double>(8, 6, 24);
        var camera = new Camera(position, -90, -20);

        Assert.Equal(position, camera.Position);
        Assert.Equal(0, camera.Forward.X);
        Assert.True(camera.Forward.Y < 0);
        Assert.True(camera.Forward.Z < 0);
    }

    [Fact]
    public void ProjectionMapsNearAndFarPlanesToVulkanDepthRange()
    {
        var camera = new Camera(default, -90, -20) { AspectRatio = 16d / 9d };
        var projection = camera.CreateProjectionMatrix();

        var nearClip = new Vector4D<double>(0, 0, -camera.NearPlane, 1) * projection;
        var farClip = new Vector4D<double>(0, 0, -camera.FarPlane, 1) * projection;

        Assert.Equal(0, nearClip.Z / nearClip.W, 4);
        Assert.Equal(1, farClip.Z / farClip.W, 4);
    }

    [Fact]
    public void ProjectionUsesAspectRatioProperty()
    {
        var camera = new Camera(default, -90, -20) { AspectRatio = 1 };

        var square = camera.CreateProjectionMatrix();
        camera.AspectRatio = 2;
        var wide = camera.CreateProjectionMatrix();

        Assert.Equal(square.M11 / 2, wide.M11, 4);
        Assert.Equal(square.M22, wide.M22, 4);
    }

    [Fact]
    public void ProjectionUsesFieldOfViewProperty()
    {
        var camera = new Camera(default, -90, -20) { FieldOfView = 70 };

        var defaultProjection = camera.CreateProjectionMatrix();
        camera.FieldOfView = 35;
        var narrowProjection = camera.CreateProjectionMatrix();

        Assert.NotEqual(defaultProjection.M11, narrowProjection.M11);
    }

    [Fact]
    public void ProjectionMapsPositiveViewYTowardTopOfVulkanViewport()
    {
        var camera = new Camera(default, -90, -20) { AspectRatio = 1 };
        var projection = camera.CreateProjectionMatrix();

        var clip = new Vector4D<double>(0, 1, -1, 1) * projection;

        Assert.True(clip.Y / clip.W < 0);
    }

    [Fact]
    public void ViewTransformsCameraToOriginAndForwardToNegativeZ()
    {
        var camera = new Camera(new Vector3D<double>(8, 6, 24), -90, 0);
        var view = camera.CreateViewMatrix();

        var cameraInView = new Vector4D<double>(camera.Position, 1) * view;
        var pointInView = new Vector4D<double>(camera.Position + camera.Forward, 1) * view;

        Assert.Equal(0, cameraInView.X, 4);
        Assert.Equal(0, cameraInView.Y, 4);
        Assert.Equal(0, cameraInView.Z, 4);
        Assert.Equal(0, pointInView.X, 4);
        Assert.Equal(0, pointInView.Y, 4);
        Assert.Equal(-1, pointInView.Z, 4);
    }

    [Fact]
    public void WorldRenderStateUsesCameraPosition()
    {
        var camera = new Camera(new Vector3D<double>(1_000_000_000_000, -32.5, -1_000_000_000_000), -90, -20);
        var position = new Vector3D<double>(1_000_000_000_001, -30.5, -999_999_999_997);
        camera.Position = position;

        var state = camera.CreateWorldRenderState();

        Assert.Equal(position, state.WorldOrigin);
    }

    [Fact]
    public void WorldRenderStateViewProjectionDoesNotDependOnWorldOrigin()
    {
        var originCamera = new Camera(default, -90, -20);
        var distantCamera = new Camera(
            new Vector3D<double>(1_000_000_000, -500, -1_000_000_000), -90, -20);

        var originState = originCamera.CreateWorldRenderState();
        var distantState = distantCamera.CreateWorldRenderState();

        Assert.Equal(originState.ViewProjection, distantState.ViewProjection);
    }

    [Fact]
    public void ViewProjectionUsesLatestRotationWithoutInterpolation()
    {
        var camera = new Camera(default, -90, -20) { AspectRatio = 1 };
        var controller = new PlayerInputController(camera);
        var before = camera.CreateViewProjection();

        controller.ApplyMouseDelta(new Vector2D<float>(10, 0));
        var after = camera.CreateViewProjection();

        Assert.NotEqual(before, after);
    }
}
