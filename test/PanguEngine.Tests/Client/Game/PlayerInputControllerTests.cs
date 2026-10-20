using PanguEngine.Client.Game;
using PanguEngine.Tests.World.Entities;
using PanguEngine.World.Entities.Components;
using Silk.NET.Maths;

namespace PanguEngine.Tests.Client.Game;

public sealed class PlayerInputControllerTests
{
    [Fact]
    public void MouseDeltaUpdatesPlayerRotationAndClampsPitch()
    {
        using var context = EntityTestContext.Create();
        var player = context.SpawnPlayer(default, -90, -20);
        var controller = new PlayerInputController(player);

        controller.ApplyMouseDelta(new Vector2D<float>(10, -2000));

        Assert.Equal(-90 + 10 * controller.MouseSensitivity, player.Rotation.Yaw, 4);
        Assert.Equal(controller.MaxPitch, player.Rotation.Pitch, 4);
    }

    [Fact]
    public void MouseRotationUsesConfigurableSensitivity()
    {
        using var context = EntityTestContext.Create();
        var player = context.SpawnPlayer(default, -90, -20);
        var controller = new PlayerInputController(player) { MouseSensitivity = 0.5 };

        controller.ApplyMouseDelta(new Vector2D<float>(2, 0));

        Assert.Equal(-89, player.Rotation.Yaw, 4);
    }

    [Fact]
    public void PitchUsesConfigurableBounds()
    {
        using var context = EntityTestContext.Create();
        var player = context.SpawnPlayer(default);
        var controller = new PlayerInputController(player)
        {
            MinPitch = -10,
            MaxPitch = 10
        };

        controller.ApplyMouseDelta(new Vector2D<float>(0, 1000));
        Assert.Equal(-10, player.Rotation.Pitch, 4);

        controller.ApplyMouseDelta(new Vector2D<float>(0, -1000));
        Assert.Equal(10, player.Rotation.Pitch, 4);
    }

    [Fact]
    public void SetMoveIntentReplacesPlayerIntent()
    {
        using var context = EntityTestContext.Create();
        var player = context.SpawnPlayer(default);
        var controller = new PlayerInputController(player);

        controller.SetMoveIntent(1, -1);

        Assert.Equal(new MoveIntentComponent(1, -1), player.MoveIntent);
    }
}
