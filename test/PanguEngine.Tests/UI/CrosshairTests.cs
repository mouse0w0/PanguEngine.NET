using PanguEngine.UI;
using PanguEngine.UI.Controls;
using PanguEngine.UI.Drawing;
using PanguEngine.UI.Rendering;
using PanguEngine.UI.Styling;

namespace PanguEngine.Tests.UI;

public sealed class CrosshairTests
{
    [Fact]
    public void ParentColorDoesNotAffectCrosshairAndOwnCssColorCanBeCleared()
    {
        var root = new Panel { Color = new Color(10, 20, 30) };
        var crosshair = new Crosshair { OutlineThickness = 0, OutlineColor = new Color(0, 0, 0, 0) };
        root.Children.Add(crosshair);
        var scene = new UIScene(root) { UseLayoutRounding = false };
        scene.SetBaseStyleSheets([]);
        scene.Open();
        scene.PrepareFrame(new Size(200, 100));

        Assert.NotEmpty(scene.CreateDrawCommandList().OfType<FillRectangleCommand>());
        Assert.All(scene.CreateDrawCommandList().OfType<FillRectangleCommand>(),
            command => Assert.Equal(new Color(255, 255, 255), command.Color));

        scene.SetStyleSheets([StyleSheet.Parse("Crosshair { color: #123456; }")]);
        scene.PrepareFrame(new Size(200, 100));
        root.Color = new Color(40, 50, 60);

        Assert.All(scene.CreateDrawCommandList().OfType<FillRectangleCommand>(),
            command => Assert.Equal(new Color(0x12, 0x34, 0x56), command.Color));
        crosshair.Color = new Color(70, 80, 90);
        crosshair.ClearValue(Crosshair.ColorProperty);
        Assert.Equal(new Color(0x12, 0x34, 0x56), crosshair.Color);

        scene.SetStyleSheets([]);
        scene.PrepareFrame(new Size(200, 100));

        Assert.Equal(new Color(255, 255, 255), crosshair.Color);
        Assert.All(scene.CreateDrawCommandList().OfType<FillRectangleCommand>(),
            command => Assert.Equal(new Color(255, 255, 255), command.Color));
        scene.Close();
    }

    [Fact]
    public void DefaultsExposeExpectedProperties()
    {
        var crosshair = new Crosshair();

        Assert.Equal(new Color(255, 255, 255), crosshair.Color);
        Assert.Equal(8, crosshair.Length);
        Assert.Equal(2, crosshair.Thickness);
        Assert.Equal(3, crosshair.Gap);
        Assert.Equal(new Color(0, 0, 0), crosshair.OutlineColor);
        Assert.Equal(1, crosshair.OutlineThickness);
        Assert.Equal(CrosshairShape.Cross, crosshair.Shape);
        Assert.False(crosshair.ShowCenterDot);
        Assert.Equal(2, crosshair.CenterDotSize);
        Assert.False(crosshair.UseUiScale);
    }

    [Theory]
    [InlineData(CrosshairShape.Cross, 8)]
    [InlineData(CrosshairShape.T, 6)]
    [InlineData(CrosshairShape.InvertedT, 6)]
    public void ShapesProduceExpectedArmCommands(CrosshairShape shape, int commandCount)
    {
        var crosshair = new Crosshair
        {
            Shape = shape,
            Length = 8,
            Thickness = 2,
            Gap = 3,
            OutlineThickness = 1
        };
        var scene = CreateScene(crosshair);

        var commands = scene.CreateDrawCommandList().OfType<FillRectangleCommand>().ToArray();

        Assert.Equal(commandCount, commands.Length);
        Assert.Equal(commandCount / 2, commands.Count(command => command.Color == new Color(0, 0, 0)));
        Assert.Equal(commandCount / 2, commands.Count(command => command.Color == new Color(255, 255, 255)));
        Assert.Equal(24, crosshair.DesiredSize.Width);
        Assert.Equal(24, crosshair.DesiredSize.Height);
        Assert.Equal(new Rect(88, 38, 24, 24), crosshair.LayoutBounds);

        var verticalCommands = commands.Where(command => command.Bounds.Width <= 4).ToArray();
        if (shape == CrosshairShape.T)
            Assert.All(verticalCommands, command => Assert.True(command.Bounds.Y >= 0));
        else if (shape == CrosshairShape.InvertedT)
            Assert.All(verticalCommands, command => Assert.True(command.Bounds.Y + command.Bounds.Height <= 0));
        else
        {
            Assert.Contains(verticalCommands, command => command.Bounds.Y < 0);
            Assert.Contains(verticalCommands, command => command.Bounds.Y + command.Bounds.Height > 0);
        }

        scene.Close();
    }

    [Fact]
    public void CenterDotAddsOutlinedAndFilledCommands()
    {
        var crosshair = new Crosshair
        {
            ShowCenterDot = true,
            CenterDotSize = 4
        };
        var scene = CreateScene(crosshair);

        var commands = scene.CreateDrawCommandList().OfType<FillRectangleCommand>().ToArray();

        Assert.Equal(10, commands.Length);
        Assert.Contains(commands, command => command.Bounds == new Rect(-3, -3, 6, 6));
        Assert.Contains(commands, command => command.Bounds == new Rect(-2, -2, 4, 4));

        scene.Close();
    }

    [Fact]
    public void GeometryChangesUpdateDesiredSizeAndRemainCentered()
    {
        var crosshair = new Crosshair
        {
            Length = 12,
            Gap = 5,
            OutlineThickness = 2
        };
        var scene = CreateScene(crosshair);

        Assert.Equal(38, crosshair.DesiredSize.Width);
        Assert.Equal(38, crosshair.DesiredSize.Height);
        Assert.Equal(new Rect(81, 31, 38, 38), crosshair.LayoutBounds);

        scene.Close();
    }

    [Fact]
    public void ThicknessWidensArmsAndKeepsSymmetricExtent()
    {
        var crosshair = new Crosshair { Thickness = 6 };
        var scene = CreateScene(crosshair);

        var commands = scene.CreateDrawCommandList().OfType<FillRectangleCommand>().ToArray();

        Assert.Contains(commands, command =>
            command.Color == new Color(255, 255, 255) &&
            command.Bounds.Width == 8 &&
            command.Bounds.Height == 6);
        Assert.Contains(commands, command =>
            command.Color == new Color(255, 255, 255) &&
            command.Bounds.Width == 6 &&
            command.Bounds.Height == 8);
        Assert.Equal(24, crosshair.DesiredSize.Width);

        scene.Close();
    }

    [Fact]
    public void CrosshairRemainsCenteredAcrossViewportAndScale()
    {
        var root = new Panel();
        var crosshair = new Crosshair();
        root.Children.Add(crosshair);
        var scene = new UIScene(root) { UseLayoutRounding = false, Scale = 2 };
        scene.Open();

        scene.PrepareFrame(new Size(240, 160));

        Assert.Equal(60, crosshair.LayoutBounds.X + crosshair.LayoutBounds.Width / 2);
        Assert.Equal(40, crosshair.LayoutBounds.Y + crosshair.LayoutBounds.Height / 2);

        scene.Close();
    }

    [Fact]
    public void FixedGeometryIgnoresUiScale()
    {
        var root = new Panel();
        var crosshair = new Crosshair();
        root.Children.Add(crosshair);
        var scene = new UIScene(root) { UseLayoutRounding = false, Scale = 2 };
        scene.Open();

        scene.PrepareFrame(new Size(240, 160));

        Assert.Equal(12, crosshair.DesiredSize.Width);
        Assert.Equal(12, crosshair.DesiredSize.Height);

        var commands = scene.CreateDrawCommandList().OfType<FillRectangleCommand>().ToArray();
        Assert.Contains(commands, command =>
            command.Color == new Color(255, 255, 255) &&
            command.Bounds.Width == 8 &&
            command.Bounds.Height == 2);

        scene.Close();
    }

    [Fact]
    public void UseUiScaleKeepsLogicalGeometry()
    {
        var root = new Panel();
        var crosshair = new Crosshair { UseUiScale = true };
        root.Children.Add(crosshair);
        var scene = new UIScene(root) { UseLayoutRounding = false, Scale = 2 };
        scene.Open();

        scene.PrepareFrame(new Size(240, 160));

        Assert.Equal(24, crosshair.DesiredSize.Width);
        Assert.Equal(24, crosshair.DesiredSize.Height);

        scene.Close();
    }

    [Fact]
    public void RepeatedPropertyAssignmentRaisesSingleChange()
    {
        var crosshair = new Crosshair();
        var changes = 0;
        crosshair.PropertyChanged += (_, _) => changes++;

        crosshair.Length = 12;
        crosshair.Length = 12;
        crosshair.Color = new Color(10, 20, 30);
        crosshair.Color = new Color(10, 20, 30);

        Assert.Equal(2, changes);
        Assert.Equal(12, crosshair.Length);
        Assert.Equal(new Color(10, 20, 30), crosshair.Color);
    }

    [Theory]
    [InlineData(false, 0.5, 24f)]
    [InlineData(false, 1.5, 24f)]
    [InlineData(false, 2, 24f)]
    [InlineData(true, 0.5, 12f)]
    [InlineData(true, 1.5, 36f)]
    [InlineData(true, 2, 48f)]
    public void BuiltGeometryPreservesPhysicalCenterAndScaling(bool useUiScale, double scale, float expectedSize)
    {
        var root = new Panel();
        root.Children.Add(new Crosshair { UseUiScale = useUiScale });
        var scene = new UIScene(root) { UseLayoutRounding = false, Scale = scale };
        scene.Open();
        try
        {
            scene.PrepareFrame(new Size(240, 160));
            var builder = new DrawBuilder();

            builder.Build(scene.CreateDrawCommandList(), 240, 160, false);

            var vertices = builder.Vertices.ToArray();
            Assert.Equal(8, builder.RectangleCount);
            Assert.Equal(120f - expectedSize / 2, vertices.Min(vertex => vertex.X));
            Assert.Equal(120f + expectedSize / 2, vertices.Max(vertex => vertex.X));
            Assert.Equal(80f - expectedSize / 2, vertices.Min(vertex => vertex.Y));
            Assert.Equal(80f + expectedSize / 2, vertices.Max(vertex => vertex.Y));
        }
        finally
        {
            scene.Close();
        }
    }

    [Theory]
    [InlineData("Length")]
    [InlineData("Thickness")]
    [InlineData("Gap")]
    [InlineData("OutlineThickness")]
    [InlineData("CenterDotSize")]
    public void InvalidDimensionsFailOnWrite(string propertyName)
    {
        foreach (var value in new[] { -1d, double.NaN, double.PositiveInfinity })
        {
            var crosshair = new Crosshair();
            var error = Assert.Throws<ArgumentException>(() => ApplyValue(crosshair, propertyName, value));
            Assert.Equal("value", error.ParamName);
            Assert.Contains(propertyName, error.Message);
        }
    }

    private static void ApplyValue(Crosshair crosshair, string propertyName, double value)
    {
        switch (propertyName)
        {
            case "Length":
                crosshair.Length = value;
                break;
            case "Thickness":
                crosshair.Thickness = value;
                break;
            case "Gap":
                crosshair.Gap = value;
                break;
            case "OutlineThickness":
                crosshair.OutlineThickness = value;
                break;
            case "CenterDotSize":
                crosshair.CenterDotSize = value;
                break;
        }
    }

    private static UIScene CreateScene(Crosshair crosshair)
    {
        var root = new Panel();
        root.Children.Add(crosshair);
        var scene = new UIScene(root) { UseLayoutRounding = false };
        scene.Open();
        scene.PrepareFrame(new Size(200, 100));
        return scene;
    }
}