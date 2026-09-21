using PanguEngine.Input;
using Silk.NET.Maths;

namespace PanguEngine.Tests.Input;

public sealed class InputActionValueTests
{
    [Fact]
    public void FactoriesExposeCanonicalTypedViews()
    {
        Assert.True(InputActionValue.FromButton(true).Button);
        Assert.Equal(2d, InputActionValue.FromAxis1D(2).Axis1D);
        Assert.Equal(
            new Vector2D<double>(2, 3),
            InputActionValue.FromAxis2D(new Vector2D<double>(2, 3)).Axis2D);
        Assert.Equal(
            new Vector3D<double>(2, 3, 4),
            InputActionValue.FromAxis3D(new Vector3D<double>(2, 3, 4)).Axis3D);
        Assert.Equal(Vector3D<double>.Zero, InputActionValue.Zero.Axis3D);
    }
}
