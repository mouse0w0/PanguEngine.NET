using PanguEngine.Client.Game;
using Silk.NET.Maths;

namespace PanguEngine.Tests.Client.Game;

public sealed class PlayerInputControllerTests
{
    [Fact]
    public void MouseDeltaUpdatesCameraAnglesAndClampsPitch()
    {
        var camera = new Camera(default, -90, -20);
        var controller = new PlayerInputController(camera);

        controller.ApplyMouseDelta(new Vector2D<float>(10, -2000));

        Assert.Equal(-90 + 10 * controller.MouseSensitivity, camera.Yaw, 4);
        Assert.Equal(controller.MaxPitch, camera.Pitch, 4);
    }

    [Fact]
    public void MouseRotationUsesConfigurableSensitivity()
    {
        var camera = new Camera(default, -90, -20);
        var controller = new PlayerInputController(camera) { MouseSensitivity = 0.5 };

        controller.ApplyMouseDelta(new Vector2D<float>(2, 0));

        Assert.Equal(-89, camera.Yaw, 4);
    }

    [Fact]
    public void PitchUsesConfigurableBounds()
    {
        var camera = new Camera(default, -90, -20);
        var controller = new PlayerInputController(camera)
        {
            MinPitch = -10,
            MaxPitch = 10
        };

        controller.ApplyMouseDelta(new Vector2D<float>(0, 1000));
        Assert.Equal(-10, camera.Pitch, 4);

        controller.ApplyMouseDelta(new Vector2D<float>(0, -1000));
        Assert.Equal(10, camera.Pitch, 4);
    }
}
