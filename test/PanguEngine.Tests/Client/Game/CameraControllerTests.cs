using PanguEngine.Client.Game;
using Silk.NET.Maths;

namespace PanguEngine.Tests.Client.Game;

public sealed class CameraControllerTests
{
    [Fact]
    public void MouseDeltaUpdatesYawPitchAndClampsPitch()
    {
        var camera = new Camera();
        var controller = new CameraController(camera);

        controller.ApplyMouseDelta(new Vector2D<float>(10, -2000));

        Assert.Equal(-90 + 10 * CameraController.MouseSensitivity, camera.Yaw, 4);
        Assert.Equal(CameraController.MaxPitch, camera.Pitch, 4);
    }

    [Fact]
    public void DiagonalMovementHasFixedTickDistance()
    {
        var camera = new Camera();
        var controller = new CameraController(camera);

        controller.Move(1, 1);

        Assert.Equal(
            CameraController.MoveDistancePerTick,
            Distance(camera.PreviousPosition, camera.CurrentPosition),
            4);
    }

    [Fact]
    public void OpposingInputDoesNotMove()
    {
        var camera = new Camera();
        var controller = new CameraController(camera);
        var initial = camera.CurrentPosition;

        controller.Move(0, 0);

        AssertVector(initial, camera.PreviousPosition);
        AssertVector(initial, camera.CurrentPosition);
    }

    [Fact]
    public void StationaryTickSynchronizesPreviousToCurrent()
    {
        var camera = new Camera();
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
        var camera = new Camera();
        var controller = new CameraController(camera);
        controller.Move(1, 0);
        var first = camera.CurrentPosition;

        controller.Move(1, 0);

        AssertVector(first, camera.PreviousPosition);
        Assert.Equal(
            CameraController.MoveDistancePerTick,
            Distance(first, camera.CurrentPosition),
            4);
    }

    [Fact]
    public void ViewProjectionUsesLatestRotationWithoutInterpolation()
    {
        var camera = new Camera { AspectRatio = 1 };
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

    private static void AssertVector(Vector3D<double> expected, Vector3D<double> actual)
    {
        Assert.Equal(expected.X, actual.X, 4);
        Assert.Equal(expected.Y, actual.Y, 4);
        Assert.Equal(expected.Z, actual.Z, 4);
    }
}