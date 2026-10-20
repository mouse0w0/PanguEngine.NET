using PanguEngine.Client.Game;
using PanguEngine.Tests.World.Entities;
using PanguEngine.World.Entities.Components;
using Silk.NET.Maths;

namespace PanguEngine.Tests.Client.Game;

public sealed class CameraTests
{
    [Fact]
    public void ConstructorReadsPlayerTransform()
    {
        using var context = EntityTestContext.Create();
        var player = context.SpawnPlayer(new Vector3D<double>(8, 6, 24), -90, -20);
        var camera = new Camera(player);

        Assert.Equal(player.Position.Current, camera.GetInterpolatedPosition(1));
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
        using var context = EntityTestContext.Create();
        var player = context.SpawnPlayer(default);
        var position = new PositionComponent(Vector3D<double>.Zero, new Vector3D<double>(1, 0, 0));
        context.SetPosition(player, position);
        var camera = new Camera(player);
        var expected = position.Previous + (position.Current - position.Previous) * expectedFraction;

        Assert.Equal(expected, camera.GetInterpolatedPosition(alpha));
    }

    [Fact]
    public void ProjectionMapsNearAndFarPlanesToVulkanDepthRange()
    {
        using var context = EntityTestContext.Create();
        var camera = new Camera(context.SpawnPlayer(default)) { AspectRatio = 16d / 9d };
        var projection = camera.CreateProjectionMatrix();

        var nearClip = new Vector4D<double>(0, 0, -camera.NearPlane, 1) * projection;
        var farClip = new Vector4D<double>(0, 0, -camera.FarPlane, 1) * projection;

        Assert.Equal(0, nearClip.Z / nearClip.W, 4);
        Assert.Equal(1, farClip.Z / farClip.W, 4);
    }

    [Fact]
    public void ProjectionUsesAspectRatioProperty()
    {
        using var context = EntityTestContext.Create();
        var camera = new Camera(context.SpawnPlayer(default)) { AspectRatio = 1 };

        var square = camera.CreateProjectionMatrix();
        camera.AspectRatio = 2;
        var wide = camera.CreateProjectionMatrix();

        Assert.Equal(square.M11 / 2, wide.M11, 4);
        Assert.Equal(square.M22, wide.M22, 4);
    }

    [Fact]
    public void ProjectionUsesFieldOfViewProperty()
    {
        using var context = EntityTestContext.Create();
        var camera = new Camera(context.SpawnPlayer(default)) { FieldOfView = 70 };

        var defaultProjection = camera.CreateProjectionMatrix();
        camera.FieldOfView = 35;
        var narrowProjection = camera.CreateProjectionMatrix();

        Assert.NotEqual(defaultProjection.M11, narrowProjection.M11);
    }

    [Fact]
    public void ProjectionMapsPositiveViewYTowardTopOfVulkanViewport()
    {
        using var context = EntityTestContext.Create();
        var camera = new Camera(context.SpawnPlayer(default)) { AspectRatio = 1 };
        var projection = camera.CreateProjectionMatrix();

        var clip = new Vector4D<double>(0, 1, -1, 1) * projection;

        Assert.True(clip.Y / clip.W < 0);
    }

    [Fact]
    public void ViewTransformsPlayerToOriginAndForwardToNegativeZ()
    {
        using var context = EntityTestContext.Create();
        var player = context.SpawnPlayer(default, -90, 0);
        var camera = new Camera(player);
        var view = camera.CreateViewMatrix(1);

        var cameraInView = new Vector4D<double>(player.Position.Current, 1) * view;
        var pointInView = new Vector4D<double>(player.Position.Current + camera.Forward, 1) * view;

        Assert.Equal(0, cameraInView.X, 4);
        Assert.Equal(0, cameraInView.Y, 4);
        Assert.Equal(0, cameraInView.Z, 4);
        Assert.Equal(0, pointInView.X, 4);
        Assert.Equal(0, pointInView.Y, 4);
        Assert.Equal(-1, pointInView.Z, 4);
    }

    [Fact]
    public void WorldRenderStateUsesInterpolatedPlayerPosition()
    {
        using var context = EntityTestContext.Create();
        var start = new Vector3D<double>(1_000_000_000_000, -32.5, -1_000_000_000_000);
        var end = new Vector3D<double>(1_000_000_000_001, -30.5, -999_999_999_997);
        var player = context.SpawnPlayer(start, -90, -20);
        context.SetPosition(player, new PositionComponent(start, end));
        var camera = new Camera(player);

        var state = camera.CreateWorldRenderState(0.25);

        Assert.Equal(camera.GetInterpolatedPosition(0.25), state.WorldOrigin);
    }

    [Fact]
    public void WorldRenderStateViewProjectionDoesNotDependOnWorldOrigin()
    {
        using var originContext = EntityTestContext.Create();
        using var distantContext = EntityTestContext.Create();
        var originCamera = new Camera(originContext.SpawnPlayer(default, -90, -20));
        var distantCamera = new Camera(distantContext.SpawnPlayer(
            new Vector3D<double>(1_000_000_000, -500, -1_000_000_000), -90, -20));

        var originState = originCamera.CreateWorldRenderState(1);
        var distantState = distantCamera.CreateWorldRenderState(1);

        Assert.Equal(originState.ViewProjection, distantState.ViewProjection);
    }

    [Fact]
    public void ViewProjectionUsesLatestRotationWithoutInterpolation()
    {
        using var context = EntityTestContext.Create();
        var player = context.SpawnPlayer(default, -90, -20);
        var camera = new Camera(player) { AspectRatio = 1 };
        var controller = new PlayerInputController(player);
        var before = camera.CreateViewProjection(0.5);

        controller.ApplyMouseDelta(new Vector2D<float>(10, 0));
        var after = camera.CreateViewProjection(0.5);

        Assert.NotEqual(before, after);
    }
}
