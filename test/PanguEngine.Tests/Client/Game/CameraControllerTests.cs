using PanguEngine.Client.Game;
using Silk.NET.Maths;

namespace PanguEngine.Tests.Client.Game;

public sealed class CameraControllerTests
{
    [Fact]
    public void MouseDeltaUpdatesYawPitchAndClampsPitch()
    {
        var camera = CreateCamera();
        var controller = new CameraController(camera);

        controller.ApplyMouseDelta(new Vector2D<float>(10, -2000));

        Assert.Equal(-90 + 10 * controller.MouseSensitivity, camera.Yaw, 4);
        Assert.Equal(controller.MaxPitch, camera.Pitch, 4);
    }

    [Fact]
    public void DiagonalMovementHasFixedTickDistance()
    {
        var camera = CreateCamera();
        var controller = new CameraController(camera);

        controller.Move(1, 1);

        Assert.Equal(
            controller.MoveDistancePerTick,
            Distance(camera.PreviousPosition, camera.CurrentPosition),
            4);
    }

    [Fact]
    public void MovementUsesMoveDistancePerTickProperty()
    {
        var camera = CreateCamera();
        var controller = new CameraController(camera)
        {
            MoveDistancePerTick = 1.25d
        };

        controller.Move(1, 0);

        Assert.Equal(1.25d, Distance(camera.PreviousPosition, camera.CurrentPosition), 4);
    }

    [Fact]
    public void MouseRotationUsesMouseSensitivityProperty()
    {
        var camera = CreateCamera();
        var controller = new CameraController(camera)
        {
            MouseSensitivity = 0.5d
        };

        controller.ApplyMouseDelta(new Vector2D<float>(2, 0));

        Assert.Equal(-89d, camera.Yaw, 4);
    }

    [Fact]
    public void PitchUsesConfigurableBounds()
    {
        var defaultController = new CameraController(CreateCamera());

        Assert.Equal(-89d, defaultController.MinPitch);
        Assert.Equal(89d, defaultController.MaxPitch);

        var camera = CreateCamera();
        var controller = new CameraController(camera)
        {
            MinPitch = -10d,
            MaxPitch = 10d
        };

        Assert.Equal(-10d, controller.MinPitch);
        Assert.Equal(10d, controller.MaxPitch);

        controller.ApplyMouseDelta(new Vector2D<float>(0, 1000));
        Assert.Equal(-10d, camera.Pitch, 4);

        controller.ApplyMouseDelta(new Vector2D<float>(0, -1000));
        Assert.Equal(10d, camera.Pitch, 4);
    }

    [Fact]
    public void OpposingInputDoesNotMove()
    {
        var camera = CreateCamera();
        var controller = new CameraController(camera);
        var initial = camera.CurrentPosition;

        controller.Move(0, 0);

        AssertVector(initial, camera.PreviousPosition);
        AssertVector(initial, camera.CurrentPosition);
    }

    [Fact]
    public void StationaryTickSynchronizesPreviousToCurrent()
    {
        var camera = CreateCamera();
        var controller = new CameraController(camera);
        controller.Move(1, 0);
        var current = camera.CurrentPosition;

        controller.Move(0, 0);

        AssertVector(current, camera.PreviousPosition);
        AssertVector(current, camera.CurrentPosition);
    }

    [Fact]
    public void ConsecutiveTicksMaintainPreviousAndCurrentPositions()
    {
        var camera = CreateCamera();
        var controller = new CameraController(camera);
        controller.Move(1, 0);
        var first = camera.CurrentPosition;

        controller.Move(1, 0);

        AssertVector(first, camera.PreviousPosition);
        Assert.Equal(
            controller.MoveDistancePerTick,
            Distance(first, camera.CurrentPosition),
            4);
    }

    [Fact]
    public void ViewProjectionUsesLatestRotationWithoutInterpolation()
    {
        var camera = CreateCamera();
        camera.AspectRatio = 1;
        var controller = new CameraController(camera);
        controller.Move(1, 0);
        var before = camera.CreateViewProjection(0.5);

        controller.ApplyMouseDelta(new Vector2D<float>(10, 0));
        var after = camera.CreateViewProjection(0.5);

        Assert.NotEqual(before, after);
    }

    private static double Distance(Vector3D<double> left, Vector3D<double> right)
    {
        var delta = right - left;
        return Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y + delta.Z * delta.Z);
    }

    private static Camera CreateCamera()
    {
        return new Camera(Vector3D<double>.Zero, -90, -20);
    }

    private static void AssertVector(Vector3D<double> expected, Vector3D<double> actual)
    {
        Assert.Equal(expected.X, actual.X, 4);
        Assert.Equal(expected.Y, actual.Y, 4);
        Assert.Equal(expected.Z, actual.Z, 4);
    }
}