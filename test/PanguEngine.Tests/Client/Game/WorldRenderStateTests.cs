using PanguEngine.Client.Game;
using Silk.NET.Maths;

namespace PanguEngine.Tests.Client.Game;

public sealed class WorldRenderStateTests
{
    [Fact]
    public void ConvertsToTranslatedWorldPositionBeforeConvertingToFloat()
    {
        var worldOrigin = new Vector3D<double>(
            1_000_000_000_000.25,
            -1_000_000_000_000.5,
            1_000_000_000_001.75);
        var renderState = new WorldRenderState(
            new Vector3D<double>(
                1_000_000_000_000,
                -1_000_000_000_000,
                1_000_000_000_000),
            default);

        var translatedWorldPosition = renderState.ToTranslatedWorldPosition(worldOrigin);

        Assert.Equal(0.25f, translatedWorldPosition.X);
        Assert.Equal(-0.5f, translatedWorldPosition.Y);
        Assert.Equal(1.75f, translatedWorldPosition.Z);
        Assert.Equal(0f, translatedWorldPosition.W);
    }
}