using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Rendering;

namespace PanguEngine.Tests.Client.UI;

public sealed class UiDrawBuilderTests
{
    [Fact]
    public void InvalidScaleFailsBeforePreviousResultChanges()
    {
        var builder = new UiDrawBuilder();
        builder.Build(Commands(Fill(new Rect(1, 2, 3, 4), new Color(1, 2, 3))), 100, 100, 1, false);
        var previous = builder.Vertices.ToArray();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            builder.Build(UiDrawCommandList.Empty, 100, 100, double.NaN, false));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            builder.Build(UiDrawCommandList.Empty, 100, 100, 0, false));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            builder.Build(UiDrawCommandList.Empty, 100, 100, -1, false));
        Assert.Equal(previous, builder.Vertices.ToArray());
    }

    [Fact]
    public void ZeroFramebufferProducesAnEmptyResult()
    {
        var builder = new UiDrawBuilder();
        var commands = Commands(Fill(new Rect(1, 2, 3, 4), new Color(1, 2, 3)));

        builder.Build(commands, 0, 100, 1, false);

        Assert.Empty(builder.Vertices.ToArray());
        Assert.Empty(builder.Indices.ToArray());
        Assert.Empty(builder.Batches.ToArray());
        Assert.Equal(0, builder.RectangleCount);
    }

    [Fact]
    public void BoundsUseScaledFramebufferClampedPhysicalCoordinates()
    {
        var builder = new UiDrawBuilder();
        builder.Build(
            Commands(Fill(new Rect(-2, 4, 12, 20), new Color(255, 0, 0))),
            12,
            20,
            1.5,
            false);

        Assert.Equal(
            [
                new UiVertex(0, 6, 1, 0, 0, 1),
                new UiVertex(12, 6, 1, 0, 0, 1),
                new UiVertex(12, 20, 1, 0, 0, 1),
                new UiVertex(0, 20, 1, 0, 0, 1)
            ],
            builder.Vertices.ToArray());
    }

    [Fact]
    public void CompletelyOutsideBoundsProduceNoGeometry()
    {
        var builder = new UiDrawBuilder();
        builder.Build(
            Commands(Fill(new Rect(20, 20, 5, 5), new Color(255, 255, 255))),
            10,
            10,
            1,
            false);

        Assert.Empty(builder.Vertices.ToArray());
        Assert.Empty(builder.Indices.ToArray());
        Assert.Empty(builder.Batches.ToArray());
    }

    [Fact]
    public void ScalingOverflowSaturatesAtFramebufferWithoutInfiniteVertices()
    {
        var builder = new UiDrawBuilder();
        builder.Build(
            Commands(Fill(new Rect(0, 0, double.MaxValue, 1), new Color(255, 255, 255))),
            64,
            48,
            double.MaxValue,
            false);

        Assert.All(builder.Vertices.ToArray(), vertex =>
        {
            Assert.True(float.IsFinite(vertex.X));
            Assert.True(float.IsFinite(vertex.Y));
        });
    }

    [Fact]
    public void ClipUsesInclusivePhysicalScissorRounding()
    {
        var builder = new UiDrawBuilder();
        builder.Build(
            Commands(Fill(
                new Rect(0, 0, 20, 20),
                new Color(255, 255, 255),
                new Rect(1.2, 2.2, 3.1, 4.1))),
            100,
            100,
            1.5,
            false);

        var batch = Assert.Single(builder.Batches.ToArray());
        Assert.Equal(new UiScissor(1, 3, 6, 7), batch.Scissor);
    }

    [Fact]
    public void NullClipUsesFullFramebufferScissor()
    {
        var builder = new UiDrawBuilder();
        builder.Build(
            Commands(Fill(new Rect(0, 0, 5, 5), new Color(255, 255, 255))),
            80,
            60,
            1,
            false);

        Assert.Equal(new UiScissor(0, 0, 80, 60), Assert.Single(builder.Batches.ToArray()).Scissor);
    }

    [Fact]
    public void EmptyOrNonIntersectingScissorsProduceNoGeometry()
    {
        var builder = new UiDrawBuilder();
        builder.Build(
            Commands(
                Fill(new Rect(0, 0, 1, 1), new Color(255, 255, 255), new Rect(20, 20, 5, 5)),
                Fill(new Rect(0, 0, 1, 1), new Color(255, 255, 255), new Rect(1, 0, 1, 1))),
            10,
            10,
            1,
            false);

        Assert.Empty(builder.Vertices.ToArray());
        Assert.Empty(builder.Batches.ToArray());
    }

    [Fact]
    public void PartlyOutsideClipIsIntersectedWithFramebuffer()
    {
        var builder = new UiDrawBuilder();
        builder.Build(
            Commands(Fill(
                new Rect(0, 0, 5, 5),
                new Color(255, 255, 255),
                new Rect(-1, -1, 3, 3))),
            10,
            10,
            1,
            false);

        Assert.Equal(new UiScissor(0, 0, 2, 2), Assert.Single(builder.Batches.ToArray()).Scissor);
    }

    [Fact]
    public void UnormColorRemainsNormalizedAndAlphaUsesDoubleIntermediate()
    {
        var builder = new UiDrawBuilder();
        builder.Build(
            Commands(Fill(new Rect(0, 0, 1, 1), new Color(128, 64, 32, 128), opacity: 0.25)),
            10,
            10,
            1,
            false);

        var vertex = builder.Vertices[0];
        Assert.Equal(128 / 255f, vertex.R);
        Assert.Equal(64 / 255f, vertex.G);
        Assert.Equal(32 / 255f, vertex.B);
        Assert.Equal((float)(128 / 255.0 * 0.25), vertex.A);
    }

    [Fact]
    public void SrgbTargetConvertsRgbToLinearWithoutPremultiplying()
    {
        var builder = new UiDrawBuilder();
        builder.Build(
            Commands(Fill(new Rect(0, 0, 1, 1), new Color(128, 255, 0, 64), opacity: 0.5)),
            10,
            10,
            1,
            true);

        var vertex = builder.Vertices[0];
        var channel = 128 / 255f;
        var expectedRed = MathF.Pow((channel + 0.055f) / 1.055f, 2.4f);
        Assert.Equal(expectedRed, vertex.R);
        Assert.Equal(1, vertex.G);
        Assert.Equal(0, vertex.B);
        Assert.Equal((float)(64 / 255.0 * 0.5), vertex.A);
    }

    [Fact]
    public void SrgbLowChannelUsesLinearSegment()
    {
        var builder = new UiDrawBuilder();
        builder.Build(
            Commands(Fill(new Rect(0, 0, 1, 1), new Color(10, 0, 0))),
            10,
            10,
            1,
            true);

        Assert.Equal((10 / 255f) / 12.92f, builder.Vertices[0].R);
    }

    [Fact]
    public void RectanglesUseUInt32IndicesAndMergeOnlyConsecutiveEqualScissors()
    {
        var firstClip = new Rect(0, 0, 10, 10);
        var secondClip = new Rect(20, 0, 10, 10);
        var builder = new UiDrawBuilder();
        builder.Build(
            Commands(
                Fill(new Rect(0, 0, 2, 2), new Color(1, 0, 0), firstClip),
                Fill(new Rect(2, 0, 2, 2), new Color(2, 0, 0), firstClip),
                Fill(new Rect(20, 0, 2, 2), new Color(3, 0, 0), secondClip),
                Fill(new Rect(4, 0, 2, 2), new Color(4, 0, 0), firstClip)),
            100,
            100,
            1,
            false);

        Assert.Equal(
            new uint[] { 0, 1, 2, 2, 3, 0, 4, 5, 6, 6, 7, 4, 8, 9, 10, 10, 11, 8, 12, 13, 14, 14, 15, 12 },
            builder.Indices.ToArray());
        Assert.Equal(
            [
                new UiBatch(new UiScissor(0, 0, 10, 10), 0, 12),
                new UiBatch(new UiScissor(20, 0, 10, 10), 12, 6),
                new UiBatch(new UiScissor(0, 0, 10, 10), 18, 6)
            ],
            builder.Batches.ToArray());
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(0, 3, 4)]
    [InlineData(4, 4, 4)]
    [InlineData(4, 5, 8)]
    [InlineData(8, 17, 32)]
    public void CapacityGrowthIsDeterministic(int current, int required, int expected) =>
        Assert.Equal(expected, UiDrawBuilder.GrowCapacity(current, required));

    [Fact]
    public void InvisibleCommandDoesNotSplitEqualScissorBatch()
    {
        var clip = new Rect(0, 0, 10, 10);
        var builder = new UiDrawBuilder();
        builder.Build(
            Commands(
                Fill(new Rect(0, 0, 2, 2), new Color(1, 0, 0), clip),
                Fill(new Rect(50, 50, 2, 2), new Color(2, 0, 0), clip),
                Fill(new Rect(2, 0, 2, 2), new Color(3, 0, 0), clip)),
            20,
            20,
            1,
            false);

        Assert.Equal(12u, Assert.Single(builder.Batches.ToArray()).IndexCount);
    }

    [Fact]
    public void CapacityGrowthUsesRequiredValueNearIntegerLimit() =>
        Assert.Equal(
            int.MaxValue,
            UiDrawBuilder.GrowCapacity(int.MaxValue / 2 + 1, int.MaxValue));

    private static UiDrawCommandList Commands(params UiFillRectangleCommand[] commands) =>
        new(commands.Cast<UiDrawCommand>().ToList());

    private static UiFillRectangleCommand Fill(
        Rect bounds,
        Color color,
        Rect? clip = null,
        double opacity = 1) =>
        new(bounds, color, clip, opacity);
}
