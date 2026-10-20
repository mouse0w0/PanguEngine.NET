using PanguEngine.Client.Game;
using PanguEngine.Tests.World.Entities;
using PanguEngine.World.Entities.Components;
using Silk.NET.Maths;

namespace PanguEngine.Tests.Client.Game;

public sealed class PlayerFreeFlightUpdaterTests
{
    [Fact]
    public void DiagonalMovementHasFixedTickDistance()
    {
        using var context = EntityTestContext.Create();
        var player = context.SpawnPlayer(default, -90, 0);
        player.SetMoveIntent(new MoveIntentComponent(1, 1));
        var updater = new PlayerFreeFlightUpdater();

        updater.Update(context.Entities);

        Assert.Equal(
            updater.MoveDistancePerTick,
            Distance(player.Position.Previous, player.Position.Current),
            4);
    }

    [Fact]
    public void MovementUsesConfiguredTickDistance()
    {
        using var context = EntityTestContext.Create();
        var player = context.SpawnPlayer(default, -90, 0);
        player.SetMoveIntent(new MoveIntentComponent(1, 0));
        var updater = new PlayerFreeFlightUpdater { MoveDistancePerTick = 1.25 };

        updater.Update(context.Entities);

        Assert.Equal(1.25, Distance(player.Position.Previous, player.Position.Current), 4);
    }

    [Fact]
    public void StationaryTickSynchronizesPreviousToCurrent()
    {
        using var context = EntityTestContext.Create();
        var player = context.SpawnPlayer(default, -90, 0);
        var updater = new PlayerFreeFlightUpdater();
        player.SetMoveIntent(new MoveIntentComponent(1, 0));
        updater.Update(context.Entities);
        var current = player.Position.Current;

        player.SetMoveIntent(default);
        updater.Update(context.Entities);

        Assert.Equal(current, player.Position.Previous);
        Assert.Equal(current, player.Position.Current);
    }

    [Fact]
    public void ForwardMovementIncludesPitch()
    {
        using var context = EntityTestContext.Create();
        var player = context.SpawnPlayer(default, -90, 45);
        player.SetMoveIntent(new MoveIntentComponent(1, 0));

        new PlayerFreeFlightUpdater().Update(context.Entities);

        Assert.True(player.Position.Current.Y > 0);
    }

    [Fact]
    public void UpdateProcessesEveryPlayerRow()
    {
        using var context = EntityTestContext.Create();
        var first = context.SpawnPlayer(default, -90, 0);
        var second = context.SpawnPlayer(new Vector3D<double>(4, 0, 0), -90, 0);
        first.SetMoveIntent(new MoveIntentComponent(1, 0));
        second.SetMoveIntent(new MoveIntentComponent(-1, 0));

        new PlayerFreeFlightUpdater().Update(context.Entities);

        Assert.NotEqual(first.Position.Previous, first.Position.Current);
        Assert.NotEqual(second.Position.Previous, second.Position.Current);
    }

    private static double Distance(Vector3D<double> left, Vector3D<double> right)
    {
        var delta = right - left;
        return Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y + delta.Z * delta.Z);
    }
}
