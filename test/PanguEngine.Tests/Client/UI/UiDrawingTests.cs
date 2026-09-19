using System.ComponentModel;
using System.Runtime.ExceptionServices;
using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Drawing;
using PanguEngine.Client.UI.Rendering;

namespace PanguEngine.Tests.Client.UI;

public sealed class UiDrawingTests
{
    [Fact]
    public void OpacityPropertyUsesExpectedOwnerDefaultAndRenderInvalidation()
    {
        var node = new DrawingNode();

        Assert.Equal(typeof(UiNode), UiNode.OpacityProperty.OwnerType);
        Assert.Equal(typeof(UiNode), UiNode.OpacityProperty.TargetType);
        Assert.Equal(1, UiNode.OpacityProperty.DefaultValue);
        Assert.Equal(UiPropertyInvalidation.Render, UiNode.OpacityProperty.Invalidation);
        Assert.Equal(1, node.Opacity);
    }

    [Fact]
    public void EmptyAndNonDrawableRootsProduceNoCommands()
    {
        Assert.Empty(new UiScreen().CreateDrawCommandList());

        var unarranged = new DrawingNode
        {
            DrawAction = DrawUnitRectangle
        };
        Assert.Empty(new UiScreen(unarranged).CreateDrawCommandList());

        var hidden = new DrawingNode
        {
            Visibility = Visibility.Hidden,
            DrawAction = DrawUnitRectangle
        };
        var hiddenScreen = new UiScreen(hidden);
        Arrange(hidden, new Rect(0, 0, 10, 10));
        Assert.Empty(hiddenScreen.CreateDrawCommandList());

        var collapsed = new DrawingNode
        {
            Visibility = Visibility.Collapsed,
            DrawAction = DrawUnitRectangle
        };
        var collapsedScreen = new UiScreen(collapsed);
        Arrange(collapsed, new Rect(0, 0, 10, 10));
        Assert.Empty(collapsedScreen.CreateDrawCommandList());

        var transparent = new DrawingNode
        {
            Opacity = 0,
            DrawAction = DrawUnitRectangle
        };
        var transparentScreen = new UiScreen(transparent);
        Arrange(transparent, new Rect(0, 0, 10, 10));
        Assert.Empty(transparentScreen.CreateDrawCommandList());
    }

    [Fact]
    public void ClosedScreenRecordsScaleOnlyAroundNonEmptyCommands()
    {
        var emptyScreen = new UiScreen { Scale = 1.5 };
        var node = new DrawingNode { DrawAction = DrawUnitRectangle };
        var screen = new UiScreen(node) { Scale = 2 };
        Arrange(node, new Rect(0, 0, 10, 10));

        var empty = emptyScreen.CreateDrawCommandList();
        var nonEmpty = screen.CreateDrawCommandList();

        Assert.Empty(empty);
        Assert.Single(nonEmpty.OfType<UiFillRectangleCommand>());
        var transform = Assert.Single(nonEmpty.OfType<UiPushTransformCommand>());
        Assert.Equal(Point.Zero, transform.Translation);
        Assert.Equal(2, transform.Scale);
    }

    [Fact]
    public void ScaleChangeProducesEmptySnapshotUntilLayoutUpdates()
    {
        var node = new DrawingNode { DrawAction = DrawUnitRectangle };
        var screen = new UiScreen(node);
        screen.Open();
        screen.Update(new Size(100, 100));
        var before = screen.CreateDrawCommandList();

        screen.Scale = 2;
        var pending = screen.CreateDrawCommandList();
        screen.Update(new Size(100, 100));
        var after = screen.CreateDrawCommandList();

        Assert.Single(before.OfType<UiFillRectangleCommand>());
        Assert.Empty(before.OfType<UiPushTransformCommand>());
        Assert.Empty(pending);
        Assert.Single(after.OfType<UiFillRectangleCommand>());
        Assert.Equal(2, Assert.Single(after.OfType<UiPushTransformCommand>()).Scale);
        screen.Close();
    }

    [Fact]
    public void LayoutRoundingChangeProducesEmptySnapshotUntilLayoutUpdates()
    {
        var node = new DrawingNode { DrawAction = DrawUnitRectangle };
        var screen = new UiScreen(node);
        screen.Open();
        screen.Update(new Size(100, 100));
        var before = screen.CreateDrawCommandList();

        screen.UseLayoutRounding = false;
        var pending = screen.CreateDrawCommandList();
        screen.Update(new Size(100, 100));
        var after = screen.CreateDrawCommandList();

        Assert.Single(before.OfType<UiFillRectangleCommand>());
        Assert.Empty(pending);
        Assert.Single(after.OfType<UiFillRectangleCommand>());
        screen.Close();
    }

    [Fact]
    public void OpenEmptyScreenDoesNotLeaveAnEmptyScaleScope()
    {
        var screen = new UiScreen { Scale = 2 };
        screen.Open();
        screen.Update(new Size(100, 100));

        var commands = screen.CreateDrawCommandList();

        Assert.Empty(commands);
        screen.Close();
    }

    [Fact]
    public void CustomFractionalDrawBoundsRemainUnroundedThroughBuilder()
    {
        var node = new DrawingNode
        {
            DrawAction = context => context.FillRectangle(
                new Rect(0.3, 0.7, 3.1, 4.1),
                new Color(255, 255, 255))
        };
        var screen = new UiScreen(node) { Scale = 1.5 };
        Arrange(node, new Rect(0, 0, 10, 10));
        var commands = screen.CreateDrawCommandList();

        Assert.Equal(
            new Rect(0.3, 0.7, 3.1, 4.1),
            Assert.Single(commands.OfType<UiFillRectangleCommand>()).Bounds);

        var builder = new UiDrawBuilder();
        builder.Build(commands, 100, 100, false);

        Assert.Equal(new UiVertex(0.45f, 1.05f, 1, 1, 1, 1), builder.Vertices[0]);
        Assert.Equal(new UiVertex(5.1f, 7.2f, 1, 1, 1, 1), builder.Vertices[2]);
    }

    [Fact]
    public void NestedNodesEmitLocalBoundsAndBuilderResolvesPositions()
    {
        var root = new DrawingParent
        {
            DrawAction = context =>
                context.FillRectangle(new Rect(1, 2, 3, 4), new Color(1, 0, 0))
        };
        var first = new DrawingNode
        {
            DrawAction = context =>
                context.FillRectangle(new Rect(2, 3, 4, 5), new Color(2, 0, 0))
        };
        var second = new DrawingNode
        {
            DrawAction = context =>
                context.FillRectangle(new Rect(3, 4, 5, 6), new Color(3, 0, 0))
        };
        root.Add(first);
        root.Add(second);
        var screen = new UiScreen(root);
        Arrange(root, new Rect(10, 20, 100, 100));
        Arrange(first, new Rect(5, 6, 20, 20));
        Arrange(second, new Rect(7, 8, 20, 20));

        var list = screen.CreateDrawCommandList();
        var commands = list.OfType<UiFillRectangleCommand>().ToArray();

        Assert.Equal(
            [new Color(1, 0, 0), new Color(2, 0, 0), new Color(3, 0, 0)],
            commands.Select(command => command.Color));
        Assert.Equal(new Rect(1, 2, 3, 4), commands[0].Bounds);
        Assert.Equal(new Rect(2, 3, 4, 5), commands[1].Bounds);
        Assert.Equal(new Rect(3, 4, 5, 6), commands[2].Bounds);

        var builder = new UiDrawBuilder();
        builder.Build(list, 400, 300, false);

        Assert.Equal(11f, builder.Vertices[0].X);
        Assert.Equal(22f, builder.Vertices[0].Y);
        Assert.Equal(14f, builder.Vertices[2].X);
        Assert.Equal(26f, builder.Vertices[2].Y);
        Assert.Equal(17f, builder.Vertices[4].X);
        Assert.Equal(29f, builder.Vertices[4].Y);
        Assert.Equal(21f, builder.Vertices[6].X);
        Assert.Equal(34f, builder.Vertices[6].Y);
        Assert.Equal(20f, builder.Vertices[8].X);
        Assert.Equal(32f, builder.Vertices[8].Y);
        Assert.Equal(25f, builder.Vertices[10].X);
        Assert.Equal(38f, builder.Vertices[10].Y);
    }

    [Fact]
    public void RegionBackgroundUsesBorderInnerBoundsWithoutVisibleBorder()
    {
        var background = new Color(10, 20, 30, 140);
        var region = new DrawingRegion
        {
            Background = new SolidColorBrush(background),
            BorderThickness = new Thickness(2, 3, 4, 5)
        };
        var screen = new UiScreen(region);
        Arrange(region, new Rect(5, 7, 20, 30));

        var command = Assert.Single(screen.CreateDrawCommandList().OfType<UiFillRectangleCommand>());

        Assert.Equal(new Rect(2, 3, 14, 22), command.Bounds);
        Assert.Equal(background, command.Color);
    }

    [Fact]
    public void RegionDrawsDecorationAndCustomContentBeforeChild()
    {
        var background = new Color(10, 20, 30, 140);
        var border = new Color(40, 50, 60, 170);
        var customColor = new Color(65, 75, 85);
        var childColor = new Color(70, 80, 90);
        var region = new DrawingRegion
        {
            Background = new SolidColorBrush(background),
            BorderBrush = new SolidColorBrush(border),
            BorderThickness = new Thickness(10, 20, 30, 40),
            DrawAction = context =>
                context.FillRectangle(new Rect(2, 3, 1, 1), customColor)
        };
        region.Add(new DrawingNode
        {
            DrawAction = context =>
                context.FillRectangle(new Rect(0, 0, 1, 1), childColor)
        });
        var screen = new UiScreen(region);
        Arrange(region, new Rect(5, 7, 100, 120));

        var commands = screen.CreateDrawCommandList()
            .OfType<UiFillRectangleCommand>()
            .ToArray();

        Assert.Equal(
            [background, border, border, border, border, customColor, childColor],
            commands.Select(command => command.Color));
        Assert.Equal(new Rect(10, 20, 60, 60), commands[0].Bounds);
        Assert.Equal(new Rect(0, 0, 100, 20), commands[1].Bounds);
        Assert.Equal(new Rect(70, 20, 30, 60), commands[2].Bounds);
        Assert.Equal(new Rect(0, 80, 100, 40), commands[3].Bounds);
        Assert.Equal(new Rect(0, 20, 10, 60), commands[4].Bounds);
        Assert.Equal(new Rect(2, 3, 1, 1), commands[5].Bounds);
        Assert.Equal(new Rect(0, 0, 1, 1), commands[6].Bounds);
    }

    [Fact]
    public void OversizedRegionBorderPartitionsWithoutOverlap()
    {
        var region = new DrawingRegion
        {
            BorderBrush = new SolidColorBrush(40, 50, 60, 170),
            BorderThickness = new Thickness(25, 20, 30, 15)
        };
        var screen = new UiScreen(region);
        Arrange(region, new Rect(0, 0, 40, 30));

        var commands = screen.CreateDrawCommandList()
            .OfType<UiFillRectangleCommand>()
            .ToArray();

        Assert.Equal(2, commands.Length);
        Assert.Equal(new Rect(0, 0, 40, 20), commands[0].Bounds);
        Assert.Equal(new Rect(0, 20, 40, 10), commands[1].Bounds);
    }

    [Fact]
    public void PartialRegionBorderEmitsOnlyVisibleEdge()
    {
        var region = new DrawingRegion
        {
            BorderBrush = new SolidColorBrush(40, 50, 60),
            BorderThickness = new Thickness(4, 0, 0, 0)
        };
        var screen = new UiScreen(region);
        Arrange(region, new Rect(0, 0, 20, 30));

        var command = Assert.Single(screen.CreateDrawCommandList().OfType<UiFillRectangleCommand>());

        Assert.Equal(new Rect(0, 0, 4, 30), command.Bounds);
    }

    [Fact]
    public void RegionDrawingUsesCommittedBorderInnerBounds()
    {
        var background = new Color(10, 20, 30, 140);
        var border = new Color(40, 50, 60, 170);
        var region = new DrawingRegion
        {
            Background = new SolidColorBrush(background),
            BorderBrush = new SolidColorBrush(border),
            BorderThickness = new Thickness(0.6)
        };
        var screen = new UiScreen(region) { Scale = 1.25 };
        Arrange(region, new Rect(0, 0, 40, 30));

        var commands = screen.CreateDrawCommandList()
            .OfType<UiFillRectangleCommand>()
            .ToArray();

        Assert.Equal(5, commands.Length);
        Assert.Equal(background, commands[0].Color);
        AssertBounds(new Rect(0.8, 0.8, 38.4, 28.8), commands[0].Bounds);
        AssertBounds(new Rect(0, 0, 40, 0.8), commands[1].Bounds);
        AssertBounds(new Rect(39.2, 0.8, 0.8, 28.8), commands[2].Bounds);
        AssertBounds(new Rect(0, 29.6, 40, 0.8), commands[3].Bounds);
        AssertBounds(new Rect(0, 0.8, 0.8, 28.8), commands[4].Bounds);
    }

    [Fact]
    public void InvalidatedRegionHidesPriorDecorationFromDrawing()
    {
        var region = new DrawingRegion
        {
            Background = new SolidColorBrush(10, 20, 30),
            BorderBrush = new SolidColorBrush(40, 50, 60),
            BorderThickness = new Thickness(2)
        };
        var screen = new UiScreen(region);
        Arrange(region, new Rect(0, 0, 20, 20));
        Assert.NotEmpty(screen.CreateDrawCommandList());

        region.InvalidateArrange();

        Assert.Empty(screen.CreateDrawCommandList());
    }

    [Fact]
    public void RegionWithoutBaseSkipsDecorationButStillDrawsSelfAndChildren()
    {
        var customColor = new Color(70, 80, 90);
        var childColor = new Color(100, 110, 120);
        var region = new DrawingRegion
        {
            DrawBase = false,
            Background = new SolidColorBrush(10, 20, 30),
            BorderBrush = new SolidColorBrush(40, 50, 60),
            BorderThickness = new Thickness(2),
            DrawAction = context =>
                context.FillRectangle(new Rect(0, 0, 1, 1), customColor)
        };
        region.Add(new DrawingNode
        {
            DrawAction = context =>
                context.FillRectangle(new Rect(0, 0, 1, 1), childColor)
        });
        var screen = new UiScreen(region);
        Arrange(region, new Rect(0, 0, 20, 20));

        var list = screen.CreateDrawCommandList();
        var commands = list.OfType<UiFillRectangleCommand>().ToArray();

        Assert.Equal([customColor, childColor], commands.Select(command => command.Color));
        Assert.Equal(new Rect(0, 0, 1, 1), commands[0].Bounds);
        Assert.Equal(new Rect(0, 0, 1, 1), commands[1].Bounds);

        var builder = new UiDrawBuilder();
        builder.Build(list, 100, 100, false);
        Assert.Equal(0f, builder.Vertices[0].X);
        Assert.Equal(0f, builder.Vertices[0].Y);
        Assert.Equal(2f, builder.Vertices[4].X);
        Assert.Equal(2f, builder.Vertices[4].Y);
    }

    [Fact]
    public void RegionDecorationUsesInheritedClipAndOpacity()
    {
        var parent = new DrawingParent { ClipToBounds = true, Opacity = 0.5 };
        var region = new DrawingRegion
        {
            Opacity = 0.5,
            Background = new SolidColorBrush(10, 20, 30)
        };
        parent.Add(region);
        var screen = new UiScreen(parent);
        Arrange(parent, new Rect(10, 20, 30, 30));
        Arrange(region, new Rect(20, 20, 40, 40));

        var list = screen.CreateDrawCommandList();
        var command = Assert.Single(list.OfType<UiFillRectangleCommand>());

        Assert.Equal(new Rect(0, 0, 40, 40), command.Bounds);
        Assert.Equal(new Rect(0, 0, 30, 30), Assert.Single(list.OfType<UiPushClipCommand>()).Clip);

        var builder = new UiDrawBuilder();
        builder.Build(list, 100, 100, false);
        Assert.Equal(new UiScissor(10, 20, 30, 30), Assert.Single(builder.Batches.ToArray()).Scissor);
        Assert.Equal(0.25f, builder.Vertices[0].A);
    }

    [Theory]
    [InlineData(ImageStretch.Fill, 0, 0, 100, 100)]
    [InlineData(ImageStretch.None, -50, 0, 200, 100)]
    [InlineData(ImageStretch.Uniform, 0, 25, 100, 50)]
    [InlineData(ImageStretch.UniformToFill, -50, 0, 200, 100)]
    public void ImageBrushBackgroundUsesExpectedStretch(
        ImageStretch stretch,
        double x,
        double y,
        double width,
        double height)
    {
        var image = CreateImage(200, 100);
        var region = new DrawingRegion
        {
            Background = new ImageBrush(image, stretch)
        };
        var screen = new UiScreen(region);
        Arrange(region, new Rect(0, 0, 100, 100));

        var list = screen.CreateDrawCommandList();
        var command = Assert.Single(list.OfType<UiDrawImageCommand>());

        Assert.Equal(new Rect(x, y, width, height), command.Bounds);
        Assert.Equal(new Rect(0, 0, 200, 100), command.SourceRect);
        Assert.Equal(new Rect(0, 0, 100, 100), Assert.Single(list.OfType<UiPushClipCommand>()).Clip);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ImageBrushBackgroundUsesExactLogicalStretchGeometry(
        bool useLayoutRounding)
    {
        var region = new DrawingRegion
        {
            Background = new ImageBrush(
                CreateImage(101, 99),
                ImageStretch.None)
        };
        var screen = new UiScreen(region)
        {
            Scale = 1.5,
            UseLayoutRounding = useLayoutRounding
        };
        Arrange(region, new Rect(0, 0, 100, 100));

        var list = screen.CreateDrawCommandList();
        var command = Assert.Single(list.OfType<UiDrawImageCommand>());

        Assert.Equal(new Rect(-0.5, 0.5, 101, 99), command.Bounds);
        Assert.Equal(new Rect(0, 0, 100, 100), Assert.Single(list.OfType<UiPushClipCommand>()).Clip);
    }

    [Theory]
    [InlineData(ImageStretch.Fill, 0, 0, 100, 80, true)]
    [InlineData(ImageStretch.None, -50, -10, 200, 100, true)]
    [InlineData(ImageStretch.Uniform, 0, 15, 100, 50, false)]
    [InlineData(ImageStretch.UniformToFill, -30, 0, 160, 80, true)]
    public void ImageBrushBorderUsesOneContinuousOuterMapping(
        ImageStretch stretch,
        double x,
        double y,
        double width,
        double height,
        bool includesBottom)
    {
        var image = CreateImage(200, 100);
        var region = new DrawingRegion
        {
            BorderBrush = new ImageBrush(image, stretch),
            BorderThickness = new Thickness(10, 20, 30, 15)
        };
        var screen = new UiScreen(region);
        Arrange(region, new Rect(5, 7, 100, 80));

        var list = screen.CreateDrawCommandList();
        var commands = list.OfType<UiDrawImageCommand>().ToArray();

        var expectedClips = new List<Rect>
        {
            new(0, 0, 100, 20),
            new(70, 20, 30, 45),
            new(0, 65, 100, 15),
            new(0, 20, 10, 45)
        };

        Assert.Equal(expectedClips.Count, commands.Length);
        Assert.All(commands, command =>
        {
            Assert.Same(image, command.Image);
            Assert.Equal(new Rect(x, y, width, height), command.Bounds);
            Assert.Equal(new Rect(0, 0, 200, 100), command.SourceRect);
        });
        Assert.Equal(
            expectedClips,
            list.OfType<UiPushClipCommand>().Select(command => command.Clip));

        var builder = new UiDrawBuilder();
        builder.Build(list, 200, 200, false,
            _ => new UiImageRenderBinding(1, 200, 100, new UiImageAtlasRegion(0, 0, 200, 100)));
        Assert.Equal(includesBottom ? 4 : 3, builder.RectangleCount);
    }

    [Fact]
    public void NineSliceBackgroundMapsSubregionInRowMajorOrder()
    {
        var image = CreateImage(30, 30);
        var region = new DrawingRegion
        {
            Background = new NineSliceImageBrush(
                image,
                new Rect(5, 4, 20, 18),
                new ImageSlice(3, 2, 5, 4))
        };
        var screen = new UiScreen(region);
        Arrange(region, new Rect(0, 0, 40, 30));

        var list = screen.CreateDrawCommandList();
        var commands = list.OfType<UiDrawImageCommand>().ToArray();
        var sourceX = new[] { 5d, 8, 20, 25 };
        var sourceY = new[] { 4d, 6, 18, 22 };
        var destinationX = new[] { 0d, 3, 35, 40 };
        var destinationY = new[] { 0d, 2, 26, 30 };

        Assert.Equal(9, commands.Length);
        for (var row = 0; row < 3; row++)
        {
            for (var column = 0; column < 3; column++)
            {
                var command = commands[row * 3 + column];
                Assert.Same(image, command.Image);
                Assert.Equal(ImageSamplingMode.Linear, command.SamplingMode);
                AssertBounds(
                    RectFromCuts(destinationX, destinationY, column, row),
                    command.Bounds);
                AssertBounds(
                    RectFromCuts(sourceX, sourceY, column, row),
                    command.SourceRect);
            }
        }

        Assert.Empty(list.OfType<UiPushClipCommand>());
    }

    [Fact]
    public void NineSliceBackgroundShrinksFixedEdgesForSmallTarget()
    {
        var image = CreateImage(20, 12);
        var region = new DrawingRegion
        {
            Background = new NineSliceImageBrush(
                image,
                new ImageSlice(7, 4, 5, 4))
        };
        var screen = new UiScreen(region);
        Arrange(region, new Rect(0, 0, 10, 6));

        var commands = screen.CreateDrawCommandList()
            .OfType<UiDrawImageCommand>()
            .ToArray();
        var split = 70d / 12;

        Assert.Equal(4, commands.Length);
        AssertBounds(new Rect(0, 0, split, 3), commands[0].Bounds);
        AssertBounds(new Rect(split, 0, 10 - split, 3), commands[1].Bounds);
        AssertBounds(new Rect(0, 3, split, 3), commands[2].Bounds);
        AssertBounds(new Rect(split, 3, 10 - split, 3), commands[3].Bounds);
        Assert.Equal(
            [
                new Rect(0, 0, 7, 4),
                new Rect(15, 0, 5, 4),
                new Rect(0, 8, 7, 4),
                new Rect(15, 8, 5, 4)
            ],
            commands.Select(command => command.SourceRect));
    }

    [Fact]
    public void NineSliceBorderMapsSourceEdgesToCommittedBorderBounds()
    {
        var image = CreateImage(30, 24);
        var region = new DrawingRegion
        {
            BorderBrush = new NineSliceImageBrush(
                image,
                new ImageSlice(4, 3, 6, 5)),
            BorderThickness = new Thickness(10, 20, 30, 15)
        };
        var screen = new UiScreen(region);
        Arrange(region, new Rect(5, 7, 100, 80));

        var commands = screen.CreateDrawCommandList()
            .OfType<UiDrawImageCommand>()
            .ToArray();
        var expectedBounds = new[]
        {
            new Rect(0, 0, 10, 20),
            new Rect(10, 0, 60, 20),
            new Rect(70, 0, 30, 20),
            new Rect(0, 20, 10, 45),
            new Rect(70, 20, 30, 45),
            new Rect(0, 65, 10, 15),
            new Rect(10, 65, 60, 15),
            new Rect(70, 65, 30, 15)
        };
        var expectedSources = new[]
        {
            new Rect(0, 0, 4, 3),
            new Rect(4, 0, 20, 3),
            new Rect(24, 0, 6, 3),
            new Rect(0, 3, 4, 16),
            new Rect(24, 3, 6, 16),
            new Rect(0, 19, 4, 5),
            new Rect(4, 19, 20, 5),
            new Rect(24, 19, 6, 5)
        };

        Assert.Equal(8, commands.Length);
        for (var index = 0; index < commands.Length; index++)
        {
            AssertBounds(expectedBounds[index], commands[index].Bounds);
            AssertBounds(expectedSources[index], commands[index].SourceRect);
        }

        Assert.DoesNotContain(commands, command =>
            command.Bounds == new Rect(10, 20, 60, 45));
    }

    [Fact]
    public void NineSliceBorderLeavesTargetEdgeTransparentForZeroSourceSlice()
    {
        var region = new DrawingRegion
        {
            BorderBrush = new NineSliceImageBrush(
                CreateImage(20, 20),
                new ImageSlice(0, 4, 4, 4)),
            BorderThickness = new Thickness(5)
        };
        var screen = new UiScreen(region);
        Arrange(region, new Rect(0, 0, 20, 20));

        var commands = screen.CreateDrawCommandList()
            .OfType<UiDrawImageCommand>()
            .ToArray();

        Assert.Equal(5, commands.Length);
        Assert.All(commands, command => Assert.True(command.SourceRect.Width > 0));
        Assert.DoesNotContain(commands, command => command.Bounds.X == 0);
    }

    [Fact]
    public void NineSliceBorderUsesCollapsedCommittedInnerBounds()
    {
        var region = new DrawingRegion
        {
            BorderBrush = new NineSliceImageBrush(
                CreateImage(30, 24),
                new ImageSlice(4, 3, 6, 5)),
            BorderThickness = new Thickness(25, 20, 30, 15)
        };
        var screen = new UiScreen(region);
        Arrange(region, new Rect(0, 0, 40, 30));

        var commands = screen.CreateDrawCommandList()
            .OfType<UiDrawImageCommand>()
            .ToArray();

        Assert.Equal(4, commands.Length);
        Assert.Equal(
            [
                new Rect(0, 0, 25, 20),
                new Rect(25, 0, 15, 20),
                new Rect(0, 20, 25, 10),
                new Rect(25, 20, 15, 10)
            ],
            commands.Select(command => command.Bounds));
    }

    [Fact]
    public void NineSliceBackgroundUsesInheritedClipAndOpacity()
    {
        var parent = new DrawingParent { ClipToBounds = true, Opacity = 0.5 };
        var region = new DrawingRegion
        {
            Opacity = 0.5,
            Background = new NineSliceImageBrush(
                CreateImage(12, 12),
                new ImageSlice(2))
        };
        parent.Add(region);
        var screen = new UiScreen(parent);
        Arrange(parent, new Rect(10, 20, 30, 30));
        Arrange(region, new Rect(20, 20, 40, 40));

        var list = screen.CreateDrawCommandList();
        var commands = list.OfType<UiDrawImageCommand>().ToArray();

        Assert.Equal(9, commands.Length);
        Assert.Equal(new Rect(0, 0, 30, 30), Assert.Single(list.OfType<UiPushClipCommand>()).Clip);

        var builder = new UiDrawBuilder();
        builder.Build(
            list,
            200,
            200,
            false,
            _ => new UiImageRenderBinding(1, 12, 12, new UiImageAtlasRegion(0, 0, 12, 12)));
        Assert.Equal(4, builder.RectangleCount);
        Assert.Equal(new UiScissor(10, 20, 30, 30), Assert.Single(builder.Batches.ToArray()).Scissor);
        Assert.Equal(0.25f, builder.Vertices[0].A);
    }

    [Fact]
    public void RegionDecorationPropertiesCannotChangeWhileDrawing()
    {
        var region = new DrawingRegion();
        Exception? brushError = null;
        Exception? thicknessError = null;
        region.DrawAction = _ =>
        {
            brushError = Record.Exception(() =>
                region.BorderBrush = new SolidColorBrush(10, 20, 30));
            thicknessError = Record.Exception(() =>
                region.BorderThickness = new Thickness(1));
        };
        var screen = new UiScreen(region);
        Arrange(region, new Rect(0, 0, 20, 20));

        _ = screen.CreateDrawCommandList();

        Assert.IsType<InvalidOperationException>(brushError);
        Assert.IsType<InvalidOperationException>(thicknessError);
        Assert.Null(region.BorderBrush);
        Assert.Equal(Thickness.Zero, region.BorderThickness);
    }

    [Fact]
    public void CommandListDoesNotTrackLaterNodeChanges()
    {
        var firstColor = new Color(1, 2, 3);
        var secondColor = new Color(4, 5, 6);
        var node = new DrawingNode
        {
            DrawAction = context =>
                context.FillRectangle(new Rect(0, 0, 2, 3), firstColor)
        };
        var screen = new UiScreen(node);
        Arrange(node, new Rect(5, 7, 10, 10));

        var firstSnapshot = screen.CreateDrawCommandList();
        node.DrawAction = context =>
            context.FillRectangle(new Rect(1, 1, 4, 5), secondColor);
        var secondSnapshot = screen.CreateDrawCommandList();

        var firstCommand = Assert.Single(firstSnapshot.OfType<UiFillRectangleCommand>());
        Assert.Equal(new Rect(0, 0, 2, 3), firstCommand.Bounds);
        Assert.Equal(firstColor, firstCommand.Color);
        var secondCommand = Assert.Single(secondSnapshot.OfType<UiFillRectangleCommand>());
        Assert.Equal(new Rect(1, 1, 4, 5), secondCommand.Bounds);
        Assert.Equal(secondColor, secondCommand.Color);
    }

    [Fact]
    public void MoveCommandsChangeOnlySubsequentDrawingOrder()
    {
        var root = new DrawingParent();
        var first = CreateColorNode(1);
        var second = CreateColorNode(2);
        root.Add(first);
        root.Add(second);
        var screen = new UiScreen(root);
        Arrange(root, new Rect(0, 0, 10, 10));
        Arrange(first, new Rect(0, 0, 10, 10));
        Arrange(second, new Rect(0, 0, 10, 10));

        var original = screen.CreateDrawCommandList();
        first.MoveToFront();
        Arrange(root, new Rect(0, 0, 10, 10));
        var reordered = screen.CreateDrawCommandList();

        Assert.Equal(
            [new Color(1, 0, 0), new Color(2, 0, 0)],
            original.OfType<UiFillRectangleCommand>().Select(command => command.Color));
        Assert.Equal(
            [new Color(2, 0, 0), new Color(1, 0, 0)],
            reordered.OfType<UiFillRectangleCommand>().Select(command => command.Color));
    }

    [Fact]
    public void ParentAndContextClipsIntersectWithoutChangingOriginalBounds()
    {
        var root = new DrawingParent { ClipToBounds = true };
        var child = new DrawingNode
        {
            DrawAction = context =>
            {
                using var clip = context.PushClip(new Rect(5, 5, 20, 20));
                context.FillRectangle(
                    new Rect(0, 0, 40, 40),
                    new Color(10, 20, 30));
            }
        };
        root.Add(child);
        var screen = new UiScreen(root);
        Arrange(root, new Rect(10, 20, 30, 30));
        Arrange(child, new Rect(20, 20, 40, 40));

        var list = screen.CreateDrawCommandList();
        var command = Assert.Single(list.OfType<UiFillRectangleCommand>());

        Assert.Equal(new Rect(0, 0, 40, 40), command.Bounds);

        var builder = new UiDrawBuilder();
        builder.Build(list, 200, 200, false);
        Assert.Equal(new UiScissor(35, 45, 5, 5), Assert.Single(builder.Batches.ToArray()).Scissor);
    }

    [Fact]
    public void ClipToBoundsOnlyClipsDescendants()
    {
        var root = new DrawingParent
        {
            ClipToBounds = true,
            DrawAction = context =>
                context.FillRectangle(new Rect(20, 0, 5, 5), new Color(1, 0, 0))
        };
        var child = new DrawingNode
        {
            DrawAction = context =>
                context.FillRectangle(new Rect(20, 0, 5, 5), new Color(2, 0, 0))
        };
        root.Add(child);
        var screen = new UiScreen(root);
        Arrange(root, new Rect(0, 0, 10, 10));
        Arrange(child, new Rect(0, 0, 10, 10));

        var list = screen.CreateDrawCommandList();
        var commands = list.OfType<UiFillRectangleCommand>().ToArray();

        Assert.Equal(2, commands.Length);
        Assert.Equal(new Rect(20, 0, 5, 5), commands[0].Bounds);
        Assert.Equal(new Rect(20, 0, 5, 5), commands[1].Bounds);

        var builder = new UiDrawBuilder();
        builder.Build(list, 100, 100, false);
        Assert.Equal(1, builder.RectangleCount);
        Assert.Equal(1 / 255f, builder.Vertices[0].R);
        Assert.Equal(0f, builder.Vertices[0].G);
    }

    [Fact]
    public void UnclippedParentAllowsOverflowingDescendant()
    {
        var root = new DrawingParent();
        var child = new DrawingNode
        {
            DrawAction = context =>
                context.FillRectangle(new Rect(20, 0, 5, 5), new Color(1, 0, 0))
        };
        root.Add(child);
        var screen = new UiScreen(root);
        Arrange(root, new Rect(0, 0, 10, 10));
        Arrange(child, new Rect(0, 0, 10, 10));

        var list = screen.CreateDrawCommandList();
        var command = Assert.Single(list.OfType<UiFillRectangleCommand>());

        Assert.Equal(new Rect(20, 0, 5, 5), command.Bounds);

        var builder = new UiDrawBuilder();
        builder.Build(list, 100, 100, false);
        Assert.Equal(1, builder.RectangleCount);
    }

    [Fact]
    public void StateDependentCullingIsDeferredToBuilder()
    {
        var node = new DrawingNode
        {
            DrawAction = context =>
            {
                context.FillRectangle(Rect.Zero, new Color(1, 1, 1));
                context.FillRectangle(new Rect(0, 0, 1, 1), new Color(1, 1, 1, 0));
                using (context.PushClip(Rect.Zero))
                    context.FillRectangle(new Rect(0, 0, 1, 1), new Color(1, 1, 1));
                using (context.PushClip(new Rect(5, 5, 1, 1)))
                    context.FillRectangle(new Rect(0, 0, 1, 1), new Color(1, 1, 1));
                using (context.PushOpacity(0))
                    context.FillRectangle(new Rect(0, 0, 1, 1), new Color(1, 1, 1));
            }
        };
        var screen = new UiScreen(node);
        Arrange(node, new Rect(0, 0, 10, 10));

        var list = screen.CreateDrawCommandList();

        Assert.Equal(3, list.OfType<UiFillRectangleCommand>().Count());

        var builder = new UiDrawBuilder();
        builder.Build(list, 10, 10, false);
        Assert.Empty(builder.Vertices.ToArray());
        Assert.Empty(builder.Batches.ToArray());
    }

    [Fact]
    public void BrushRectangleOverloadUsesExistingSolidCommandSemantics()
    {
        var color = new Color(10, 20, 30, 140);
        var node = new DrawingNode
        {
            Opacity = 0.5,
            DrawAction = context =>
            {
                using var clip = context.PushClip(new Rect(1, 2, 3, 4));
                context.FillRectangle(
                    new Rect(0, 0, 5, 6),
                    new SolidColorBrush(color));
            }
        };
        var screen = new UiScreen(node);
        Arrange(node, new Rect(10, 20, 10, 10));

        var list = screen.CreateDrawCommandList();
        var command = Assert.Single(list.OfType<UiFillRectangleCommand>());

        Assert.Equal(new Rect(0, 0, 5, 6), command.Bounds);
        Assert.Equal(color, command.Color);
        Assert.Equal(new Rect(1, 2, 3, 4), Assert.Single(list.OfType<UiPushClipCommand>()).Clip);
        Assert.Equal(0.5, Assert.Single(list.OfType<UiPushOpacityCommand>()).Opacity);
    }

    [Fact]
    public void NodeAndContextOpacityMultiplyWithoutChangingColorAlpha()
    {
        var color = new Color(10, 20, 30, 128);
        var root = new DrawingParent { Opacity = 0.5 };
        var child = new DrawingNode
        {
            Opacity = 0.5,
            DrawAction = context =>
            {
                using var opacity = context.PushOpacity(0.25);
                context.FillRectangle(new Rect(0, 0, 10, 10), color);
            }
        };
        root.Add(child);
        var screen = new UiScreen(root);
        Arrange(root, new Rect(0, 0, 10, 10));
        Arrange(child, new Rect(0, 0, 10, 10));

        var list = screen.CreateDrawCommandList();
        var command = Assert.Single(list.OfType<UiFillRectangleCommand>());

        Assert.Equal(color, command.Color);
        Assert.Equal(new Rect(0, 0, 10, 10), command.Bounds);

        var builder = new UiDrawBuilder();
        builder.Build(list, 100, 100, false);
        Assert.Equal((float)(128 / 255.0 * 0.0625), builder.Vertices[0].A);
    }

    [Fact]
    public void InvalidNodeOpacityFailsBeforeItsDrawCore()
    {
        var drawCalls = 0;
        var propertyNode = new DrawingNode
        {
            Opacity = -1,
            DrawAction = _ => drawCalls++
        };
        var propertyScreen = new UiScreen(propertyNode);
        Arrange(propertyNode, new Rect(0, 0, 10, 10));

        Assert.Throws<InvalidOperationException>(propertyScreen.CreateDrawCommandList);

        var setValueNode = new DrawingNode
        {
            DrawAction = _ => drawCalls++
        };
        var setValueScreen = new UiScreen(setValueNode);
        Arrange(setValueNode, new Rect(0, 0, 10, 10));
        setValueNode.SetValue(UiNode.OpacityProperty, double.NaN);

        Assert.Throws<InvalidOperationException>(setValueScreen.CreateDrawCommandList);

        var bindingNode = new DrawingNode
        {
            DrawAction = _ => drawCalls++
        };
        var bindingScreen = new UiScreen(bindingNode);
        Arrange(bindingNode, new Rect(0, 0, 10, 10));
        var source = new OpacitySource { Opacity = double.PositiveInfinity };
        bindingNode.Bind(UiNode.OpacityProperty, source, item => item.Opacity);

        Assert.Throws<InvalidOperationException>(bindingScreen.CreateDrawCommandList);
        Assert.Equal(0, drawCalls);
    }

    [Fact]
    public void PushOpacityRejectsInvalidValuesWithoutLosingCurrentState()
    {
        var errors = new List<Exception?>();
        var node = new DrawingNode
        {
            DrawAction = context =>
            {
                errors.Add(Record.Exception(() => { _ = context.PushOpacity(double.NaN); }));
                errors.Add(Record.Exception(() => { _ = context.PushOpacity(double.PositiveInfinity); }));
                errors.Add(Record.Exception(() => { _ = context.PushOpacity(-0.01); }));
                errors.Add(Record.Exception(() => { _ = context.PushOpacity(1.01); }));
                context.FillRectangle(new Rect(0, 0, 1, 1), new Color(1, 1, 1));
            }
        };
        var screen = new UiScreen(node);
        Arrange(node, new Rect(0, 0, 10, 10));

        var list = screen.CreateDrawCommandList();

        Assert.All(errors, error => Assert.IsType<ArgumentOutOfRangeException>(error));
        Assert.Equal(
            new Rect(0, 0, 1, 1),
            Assert.Single(list.OfType<UiFillRectangleCommand>()).Bounds);
    }

    [Fact]
    public void ScopeRequiresLifoAndSingleDisposal()
    {
        Exception? outOfOrderError = null;
        Exception? duplicateError = null;
        var node = new DrawingNode
        {
            DrawAction = context =>
            {
                var first = context.PushClip(new Rect(0, 0, 5, 5));
                var second = context.PushOpacity(0.5);
                try
                {
                    first.Dispose();
                }
                catch (Exception exception)
                {
                    outOfOrderError = exception;
                }

                second.Dispose();
                first.Dispose();
                try
                {
                    first.Dispose();
                }
                catch (Exception exception)
                {
                    duplicateError = exception;
                }
            }
        };
        var screen = new UiScreen(node);
        Arrange(node, new Rect(0, 0, 10, 10));

        _ = screen.CreateDrawCommandList();
        default(UiDrawingScope).Dispose();

        Assert.IsType<InvalidOperationException>(outOfOrderError);
        Assert.IsType<InvalidOperationException>(duplicateError);
    }

    [Fact]
    public void MissingScopeStopsGenerationAndLaterGenerationCanRecover()
    {
        var node = new DrawingNode
        {
            DrawAction = context =>
                _ = context.PushClip(new Rect(0, 0, 1, 1))
        };
        var screen = new UiScreen(node);
        Arrange(node, new Rect(0, 0, 10, 10));

        Assert.Throws<InvalidOperationException>(screen.CreateDrawCommandList);

        node.DrawAction = DrawUnitRectangle;
        Assert.Single(screen.CreateDrawCommandList().OfType<UiFillRectangleCommand>());
    }

    [Fact]
    public void ContextCannotBeUsedAfterDrawCoreReturns()
    {
        UiDrawingContext? captured = null;
        var node = new DrawingNode
        {
            DrawAction = context => captured = context
        };
        var screen = new UiScreen(node);
        Arrange(node, new Rect(0, 0, 10, 10));

        _ = screen.CreateDrawCommandList();

        Assert.Throws<InvalidOperationException>(() =>
            captured!.FillRectangle(
                new Rect(0, 0, 1, 1),
                new Color(1, 1, 1)));
    }

    [Fact]
    public void DrawCoreExceptionPreservesInstanceAndDrawingStateRecovers()
    {
        var expected = new InvalidOperationException("draw failed");
        var node = new DrawingNode
        {
            DrawAction = _ => throw expected
        };
        var screen = new UiScreen(node);
        Arrange(node, new Rect(0, 0, 10, 10));

        var actual = Assert.Throws<InvalidOperationException>(screen.CreateDrawCommandList);

        Assert.Same(expected, actual);
        node.DrawAction = DrawUnitRectangle;
        Assert.Single(screen.CreateDrawCommandList().OfType<UiFillRectangleCommand>());
    }

    [Fact]
    public void DrawingRejectsRootTreeLayoutAndPropertyMutationsBeforeCommit()
    {
        var root = new DrawingParent();
        var first = new DrawingNode();
        var second = new DrawingNode();
        var added = new DrawingNode();
        var replacement = new DrawingNode();
        root.Add(first);
        root.Add(second);
        var screen = new UiScreen(root);
        Arrange(root, new Rect(0, 0, 100, 100));
        Arrange(first, new Rect(0, 0, 10, 10));
        Arrange(second, new Rect(20, 0, 10, 10));
        screen.Open();
        screen.Update(new Size(100, 100));
        var errors = new List<Exception?>();
        root.DrawAction = _ =>
        {
            errors.Add(Record.Exception(() => screen.Root = replacement));
            errors.Add(Record.Exception(() => root.Add(added)));
            errors.Add(Record.Exception(() => root.Remove(first)));
            errors.Add(Record.Exception(root.Clear));
            errors.Add(Record.Exception(first.MoveToFront));
            errors.Add(Record.Exception(() => root.Measure(new Size(100, 100))));
            errors.Add(Record.Exception(() => root.Arrange(new Rect(0, 0, 100, 100))));
            errors.Add(Record.Exception(root.InvalidateMeasure));
            errors.Add(Record.Exception(root.InvalidateArrange));
            errors.Add(Record.Exception(() => root.Opacity = 0.5));
            errors.Add(Record.Exception(() => root.Width = 50));
            errors.Add(Record.Exception(() => root.Focusable = true));
            errors.Add(Record.Exception(() => screen.Scale = 2));
            errors.Add(Record.Exception(() => screen.UseLayoutRounding = false));
        };

        _ = screen.CreateDrawCommandList();

        Assert.Equal(14, errors.Count);
        Assert.All(errors, error => Assert.IsType<InvalidOperationException>(error));
        Assert.Same(root, screen.Root);
        Assert.Equal(new UiNode[] { first, second }, root.Children);
        Assert.Null(added.Parent);
        Assert.Null(replacement.Screen);
        Assert.Equal(1, root.Opacity);
        Assert.True(double.IsNaN(root.Width));
        Assert.False(root.Focusable);
        Assert.Equal(1, screen.Scale);
        Assert.True(screen.UseLayoutRounding);
        Assert.True(root.IsMeasureValid);
        Assert.True(root.IsArrangeValid);
        screen.Close();
    }

    [Fact]
    public void ExistingNoOpTreeOperationsRemainNoOpsWhileDrawing()
    {
        var root = new DrawingParent();
        var first = new DrawingNode();
        var emptyFront = new DrawingParent();
        var foreign = new DrawingNode();
        var independent = new DrawingNode();
        root.Add(first);
        root.Add(emptyFront);
        var screen = new UiScreen(root);
        Arrange(root, new Rect(0, 0, 100, 100));
        Arrange(first, new Rect(0, 0, 10, 10));
        Arrange(emptyFront, new Rect(20, 0, 10, 10));
        var errors = new List<Exception?>();
        root.DrawAction = _ =>
        {
            errors.Add(Record.Exception(() => screen.Root = root));
            errors.Add(Record.Exception(independent.MoveToFront));
            errors.Add(Record.Exception(emptyFront.MoveToFront));
            errors.Add(Record.Exception(() => root.Remove(foreign)));
            errors.Add(Record.Exception(emptyFront.Clear));
        };

        _ = screen.CreateDrawCommandList();

        Assert.All(errors, Assert.Null);
        Assert.Equal(new UiNode[] { first, emptyFront }, root.Children);
        Assert.Null(foreign.Parent);
        Assert.Null(independent.Parent);
    }

    [Fact]
    public void DrawingRejectsManagerLifecycleOperationsBeforeManagerStateChanges()
    {
        var manager = new UiManager();
        var root = new DrawingNode();
        var screen = new UiScreen(root);
        manager.Open(screen);
        Arrange(root, new Rect(0, 0, 10, 10));
        var errors = new List<Exception?>();
        root.DrawAction = _ =>
        {
            errors.Add(Record.Exception(() => manager.Open(new UiScreen())));
            errors.Add(Record.Exception(manager.Close));
            errors.Add(Record.Exception(manager.Destroy));
        };

        _ = screen.CreateDrawCommandList();

        Assert.All(errors, error => Assert.IsType<InvalidOperationException>(error));
        Assert.Same(screen, manager.CurrentScreen);
        manager.Close();
    }

    [Fact]
    public void ClosedDrawingScreenCannotOpenUntilGenerationReturns()
    {
        var manager = new UiManager();
        var node = new DrawingNode();
        var screen = new UiScreen(node);
        Arrange(node, new Rect(0, 0, 10, 10));
        Exception? openError = null;
        node.DrawAction = _ =>
            openError = Record.Exception(() => manager.Open(screen));

        _ = screen.CreateDrawCommandList();

        Assert.IsType<InvalidOperationException>(openError);
        Assert.Null(manager.CurrentScreen);
        manager.Open(screen);
        manager.Close();
    }

    [Fact]
    public void DrawingRejectsReentrantGeneration()
    {
        Exception? error = null;
        var node = new DrawingNode();
        var screen = new UiScreen(node);
        node.DrawAction = _ =>
            error = Record.Exception(screen.CreateDrawCommandList);
        Arrange(node, new Rect(0, 0, 10, 10));

        _ = screen.CreateDrawCommandList();

        Assert.IsType<InvalidOperationException>(error);
    }

    [Fact]
    public void DrawingAllowsPostButDoesNotRunItSynchronously()
    {
        var calls = 0;
        var node = new DrawingNode();
        var screen = new UiScreen(node);
        node.DrawAction = _ => screen.Post(() => calls++);
        Arrange(node, new Rect(0, 0, 10, 10));
        screen.Open();

        _ = screen.CreateDrawCommandList();
        Assert.Equal(0, calls);

        screen.Update(new Size(10, 10));
        Assert.Equal(1, calls);
        screen.Close();
    }

    [Fact]
    public void OpenScreenRequiresOwnerThreadAndClosedScreenAllowsBackgroundDrawing()
    {
        var node = new DrawingNode { DrawAction = DrawUnitRectangle };
        var screen = new UiScreen(node);
        Arrange(node, new Rect(0, 0, 10, 10));
        screen.Open();

        var openError = RunOnBackgroundThread(() =>
            Record.Exception(screen.CreateDrawCommandList));

        Assert.IsType<InvalidOperationException>(openError);
        screen.Close();

        var closedCount = RunOnBackgroundThread(() =>
            screen.CreateDrawCommandList().OfType<UiFillRectangleCommand>().Count());
        Assert.Equal(1, closedCount);
    }

    [Fact]
    public void CoordinateOverflowFailsAtBuild()
    {
        var node = new DrawingNode
        {
            DrawAction = context =>
                context.FillRectangle(
                    new Rect(double.MaxValue, 0, 1, 1),
                    new Color(1, 1, 1))
        };
        var screen = new UiScreen(node);
        Arrange(node, new Rect(double.MaxValue, 0, 1, 1));

        var list = screen.CreateDrawCommandList();

        var builder = new UiDrawBuilder();
        Assert.Throws<InvalidOperationException>(() => builder.Build(list, 100, 100, false));
    }

    [Fact]
    public void OverflowingLayoutWithNoOutputDoesNotComputeCoordinates()
    {
        var drawCalls = 0;
        var root = new DrawingParent();
        var child = new DrawingNode
        {
            DrawAction = _ => drawCalls++
        };
        root.Add(child);
        var screen = new UiScreen(root);
        Arrange(root, new Rect(double.MaxValue, 0, 1, 1));
        Arrange(child, new Rect(double.MaxValue, 0, 1, 1));

        var list = screen.CreateDrawCommandList();

        Assert.Empty(list);
        Assert.Equal(1, drawCalls);

        var builder = new UiDrawBuilder();
        builder.Build(list, 100, 100, false);
        Assert.Empty(builder.Vertices.ToArray());
    }

    [Fact]
    public void PushClipCoordinateOverflowFailsAtBuild()
    {
        var node = new DrawingNode
        {
            DrawAction = context =>
            {
                using (context.PushClip(new Rect(double.MaxValue, 0, 1, 1)))
                    context.FillRectangle(new Rect(0, 0, 1, 1), new Color(1, 1, 1));
            }
        };
        var screen = new UiScreen(node);
        Arrange(node, new Rect(double.MaxValue, 0, 1, 1));

        var list = screen.CreateDrawCommandList();

        var builder = new UiDrawBuilder();
        Assert.Throws<InvalidOperationException>(() => builder.Build(list, 100, 100, false));
    }

    [Fact]
    public void DrawImageUsesLocalBoundsClipAndOpacityState()
    {
        var image = UiImage.FromRgba(new byte[16], 2, 2);
        var node = new DrawingNode
        {
            Opacity = 0.5,
            DrawAction = context =>
            {
                using (context.PushClip(new Rect(1, 2, 3, 4)))
                    context.DrawImage(new Rect(0, 0, 5, 6), image, new Rect(0, 0, 1, 2));
            }
        };
        var screen = new UiScreen(node);
        Arrange(node, new Rect(10, 20, 10, 10));

        var list = screen.CreateDrawCommandList();
        var command = Assert.Single(list.OfType<UiDrawImageCommand>());

        Assert.Same(image, command.Image);
        Assert.Equal(new Rect(0, 0, 5, 6), command.Bounds);
        Assert.Equal(new Rect(0, 0, 1, 2), command.SourceRect);
        Assert.Equal(new Rect(1, 2, 3, 4), Assert.Single(list.OfType<UiPushClipCommand>()).Clip);
        Assert.Equal(0.5, Assert.Single(list.OfType<UiPushOpacityCommand>()).Opacity);
    }

    private static DrawingNode CreateColorNode(byte red) =>
        new()
        {
            DrawAction = context =>
                context.FillRectangle(
                    new Rect(0, 0, 1, 1),
                    new Color(red, 0, 0))
        };

    private static void DrawUnitRectangle(UiDrawingContext context) =>
        context.FillRectangle(
            new Rect(0, 0, 1, 1),
            new Color(1, 1, 1));

    private static void AssertBounds(Rect expected, Rect actual)
    {
        Assert.Equal(expected.X, actual.X, 12);
        Assert.Equal(expected.Y, actual.Y, 12);
        Assert.Equal(expected.Width, actual.Width, 12);
        Assert.Equal(expected.Height, actual.Height, 12);
    }

    private static Rect RectFromCuts(
        IReadOnlyList<double> x,
        IReadOnlyList<double> y,
        int column,
        int row) =>
        new(
            x[column],
            y[row],
            x[column + 1] - x[column],
            y[row + 1] - y[row]);

    private static UiImage CreateImage(int width, int height) =>
        UiImage.FromRgba(new byte[checked(width * height * 4)], width, height);

    private static void Arrange(UiNode node, Rect bounds)
    {
        node.Measure(new Size(bounds.Width, bounds.Height));
        node.Arrange(bounds);
    }

    private static T RunOnBackgroundThread<T>(Func<T> action)
    {
        T result = default!;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = action();
            }
            catch (Exception exception)
            {
                error = exception;
            }
        });
        thread.Start();
        thread.Join();

        if (error is not null)
            ExceptionDispatchInfo.Capture(error).Throw();

        return result;
    }

    private sealed class DrawingNode : UiNode
    {
        internal Action<UiDrawingContext>? DrawAction { get; set; }

        protected override void DrawCore(UiDrawingContext context) =>
            DrawAction?.Invoke(context);
    }

    private sealed class DrawingRegion : Region
    {
        internal bool DrawBase { get; set; } = true;
        internal Action<UiDrawingContext>? DrawAction { get; set; }

        internal void Add(UiNode child) =>
            AddChild(child);

        protected override void DrawCore(UiDrawingContext context)
        {
            if (DrawBase)
                base.DrawCore(context);
            DrawAction?.Invoke(context);
        }
    }

    private sealed class DrawingParent : Parent
    {
        internal Action<UiDrawingContext>? DrawAction { get; set; }

        internal void Add(UiNode child) =>
            AddChild(child);

        internal bool Remove(UiNode child) =>
            RemoveChild(child);

        internal void Clear() =>
            ClearChildren();

        protected override void DrawCore(UiDrawingContext context) =>
            DrawAction?.Invoke(context);
    }

    private sealed class OpacitySource : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public double Opacity
        {
            get => field;
            set
            {
                if (field.Equals(value))
                    return;

                field = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Opacity)));
            }
        }
    }
}
