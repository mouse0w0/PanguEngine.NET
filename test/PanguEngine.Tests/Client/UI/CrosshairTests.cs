using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Drawing;
using PanguEngine.Client.UI.Rendering;

namespace PanguEngine.Tests.Client.UI;

public sealed class CrosshairTests
{
    [Fact]
    public void DefaultsExposeExpectedPropertiesAndInvalidation()
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

        Assert.Equal(UiPropertyInvalidation.Render, Crosshair.ColorProperty.Invalidation);
        Assert.Equal(
            UiPropertyInvalidation.Measure | UiPropertyInvalidation.Render,
            Crosshair.LengthProperty.Invalidation);
        Assert.Equal(
            UiPropertyInvalidation.Measure | UiPropertyInvalidation.Render,
            Crosshair.ShapeProperty.Invalidation);
        Assert.Equal(
            UiPropertyInvalidation.Measure | UiPropertyInvalidation.Render,
            Crosshair.UseUiScaleProperty.Invalidation);
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
        var screen = CreateScreen(crosshair);

        var commands = screen.CreateDrawCommandList().OfType<UiFillRectangleCommand>().ToArray();

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

        screen.Close();
    }

    [Fact]
    public void CenterDotAddsOutlinedAndFilledCommands()
    {
        var crosshair = new Crosshair
        {
            ShowCenterDot = true,
            CenterDotSize = 4
        };
        var screen = CreateScreen(crosshair);

        var commands = screen.CreateDrawCommandList().OfType<UiFillRectangleCommand>().ToArray();

        Assert.Equal(10, commands.Length);
        Assert.Contains(commands, command => command.Bounds == new Rect(-3, -3, 6, 6));
        Assert.Contains(commands, command => command.Bounds == new Rect(-2, -2, 4, 4));

        screen.Close();
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
        var screen = CreateScreen(crosshair);

        Assert.Equal(38, crosshair.DesiredSize.Width);
        Assert.Equal(38, crosshair.DesiredSize.Height);
        Assert.Equal(new Rect(81, 31, 38, 38), crosshair.LayoutBounds);

        screen.Close();
    }

    [Fact]
    public void ThicknessWidensArmsAndKeepsSymmetricExtent()
    {
        var crosshair = new Crosshair { Thickness = 6 };
        var screen = CreateScreen(crosshair);

        var commands = screen.CreateDrawCommandList().OfType<UiFillRectangleCommand>().ToArray();

        Assert.Contains(commands, command =>
            command.Color == new Color(255, 255, 255) &&
            command.Bounds.Width == 8 &&
            command.Bounds.Height == 6);
        Assert.Contains(commands, command =>
            command.Color == new Color(255, 255, 255) &&
            command.Bounds.Width == 6 &&
            command.Bounds.Height == 8);
        Assert.Equal(24, crosshair.DesiredSize.Width);

        screen.Close();
    }

    [Fact]
    public void CrosshairRemainsCenteredAcrossViewportAndScale()
    {
        var root = new Panel();
        var crosshair = new Crosshair();
        root.Children.Add(crosshair);
        var screen = new UiScreen(root) { UseLayoutRounding = false, Scale = 2 };
        screen.Open();

        screen.PrepareFrame(new Size(240, 160), 0);

        Assert.Equal(60, crosshair.LayoutBounds.X + crosshair.LayoutBounds.Width / 2);
        Assert.Equal(40, crosshair.LayoutBounds.Y + crosshair.LayoutBounds.Height / 2);

        screen.Close();
    }

    [Fact]
    public void FixedGeometryIgnoresUiScale()
    {
        var root = new Panel();
        var crosshair = new Crosshair();
        root.Children.Add(crosshair);
        var screen = new UiScreen(root) { UseLayoutRounding = false, Scale = 2 };
        screen.Open();

        screen.PrepareFrame(new Size(240, 160), 0);

        Assert.Equal(12, crosshair.DesiredSize.Width);
        Assert.Equal(12, crosshair.DesiredSize.Height);

        var commands = screen.CreateDrawCommandList().OfType<UiFillRectangleCommand>().ToArray();
        Assert.Contains(commands, command =>
            command.Color == new Color(255, 255, 255) &&
            command.Bounds.Width == 8 &&
            command.Bounds.Height == 2);

        screen.Close();
    }

    [Fact]
    public void UseUiScaleKeepsLogicalGeometry()
    {
        var root = new Panel();
        var crosshair = new Crosshair { UseUiScale = true };
        root.Children.Add(crosshair);
        var screen = new UiScreen(root) { UseLayoutRounding = false, Scale = 2 };
        screen.Open();

        screen.PrepareFrame(new Size(240, 160), 0);

        Assert.Equal(24, crosshair.DesiredSize.Width);
        Assert.Equal(24, crosshair.DesiredSize.Height);

        screen.Close();
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
        var screen = new UiScreen(root) { UseLayoutRounding = false, Scale = scale };
        screen.Open();
        try
        {
            screen.PrepareFrame(new Size(240, 160), 0);
            var builder = new UiDrawBuilder();

            builder.Build(screen.CreateDrawCommandList(), 240, 160, false);

            var vertices = builder.Vertices.ToArray();
            Assert.Equal(8, builder.RectangleCount);
            Assert.Equal(120f - expectedSize / 2, vertices.Min(vertex => vertex.X));
            Assert.Equal(120f + expectedSize / 2, vertices.Max(vertex => vertex.X));
            Assert.Equal(80f - expectedSize / 2, vertices.Min(vertex => vertex.Y));
            Assert.Equal(80f + expectedSize / 2, vertices.Max(vertex => vertex.Y));
        }
        finally
        {
            screen.Close();
        }
    }

    [Theory]
    [InlineData("Length")]
    [InlineData("Thickness")]
    [InlineData("Gap")]
    [InlineData("OutlineThickness")]
    [InlineData("CenterDotSize")]
    public void InvalidDimensionsFailDuringLayout(string propertyName)
    {
        foreach (var value in new[] { -1d, double.NaN, double.PositiveInfinity })
        {
            var crosshair = new Crosshair();
            ApplyValue(crosshair, propertyName, value);
            var screen = new UiScreen(crosshair);
            screen.Open();

            Assert.Throws<InvalidOperationException>(() => screen.PrepareFrame(new Size(200, 100), 0));

            screen.Close();
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

    private static UiScreen CreateScreen(Crosshair crosshair)
    {
        var root = new Panel();
        root.Children.Add(crosshair);
        var screen = new UiScreen(root) { UseLayoutRounding = false };
        screen.Open();
        screen.PrepareFrame(new Size(200, 100), 0);
        return screen;
    }
}
