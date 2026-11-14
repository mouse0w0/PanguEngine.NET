using PanguEngine.World.Entities;

namespace PanguEngine.Tests.World.Entities;

public sealed class EntityPoseMathTests
{
    [Theory]
    [InlineData(double.MaxValue)]
    [InlineData(double.MinValue)]
    [InlineData(1e308)]
    [InlineData(-1e308)]
    public void ExtremeFiniteYawProducesFiniteUnitDirection(double yaw)
    {
        var direction = EntityPoseMath.GetForward(yaw, 20);
        Assert.True(double.IsFinite(direction.X));
        Assert.True(double.IsFinite(direction.Y));
        Assert.True(double.IsFinite(direction.Z));
        Assert.Equal(1d, direction.X * direction.X + direction.Y * direction.Y + direction.Z * direction.Z, 10);
        Assert.Equal(EntityPoseMath.GetForward(yaw % 360d, 20), direction);
    }
}
