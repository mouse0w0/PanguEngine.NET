using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Rendering;
using PanguEngine.Graphics;

namespace PanguEngine.Tests.Client.UI;

public sealed class UiDrawBuilderTests
{
    [Fact]
    public void UnifiedVertexInputMatchesFiftyTwoByteLayout()
    {
        Assert.Equal(52u, UiVertex.SizeInBytes);
        Assert.Equal(52, System.Runtime.InteropServices.Marshal.SizeOf<UiVertex>());
        Assert.Equal(52u, Assert.Single(UiVertex.VertexInput.Buffers).Stride);
        Assert.Equal(
            [
                (4u, VertexAttributeFormat.UInt32, 48u)
            ],
            UiVertex.VertexInput.Attributes.Skip(4)
                .Select(attribute => (attribute.Location, attribute.Format, attribute.Offset)));
    }

    [Fact]
    public void InvalidScaleFailsBeforePreviousResultChanges()
    {
        var builder = new UiDrawBuilder();
        builder.Build(Commands(Fill(new Rect(1, 2, 3, 4), new Color(1, 2, 3))), 100, 100, false);
        var previous = builder.Vertices.ToArray();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            builder.Build(Commands(double.NaN), 100, 100, false));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            builder.Build(Commands(0), 100, 100, false));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            builder.Build(Commands(-1), 100, 100, false));
        Assert.Equal(previous, builder.Vertices.ToArray());
    }

    [Fact]
    public void ZeroFramebufferProducesAnEmptyResult()
    {
        var builder = new UiDrawBuilder();
        var commands = Commands(Fill(new Rect(1, 2, 3, 4), new Color(1, 2, 3)));

        builder.Build(commands, 0, 100, false);

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
            Commands(1.5, Fill(new Rect(-2, 4, 12, 20), new Color(255, 0, 0))),
            12,
            20,
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
    public void ReusedBuilderUsesEachSnapshotScaleIndependently()
    {
        var command = Fill(new Rect(1, 2, 3, 4), new Color(255, 255, 255));
        var first = new UiDrawCommandList([command], 2);
        var second = new UiDrawCommandList([command], 0.5);
        var builder = new UiDrawBuilder();

        builder.Build(first, 100, 100, false);
        Assert.Equal(new UiVertex(2, 4, 1, 1, 1, 1), builder.Vertices[0]);

        builder.Build(second, 100, 100, false);
        Assert.Equal(new UiVertex(0.5f, 1, 1, 1, 1, 1), builder.Vertices[0]);
    }

    [Fact]
    public void CompletelyOutsideBoundsProduceNoGeometry()
    {
        var builder = new UiDrawBuilder();
        builder.Build(
            Commands(Fill(new Rect(20, 20, 5, 5), new Color(255, 255, 255))),
            10,
            10,
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
            Commands(double.MaxValue, Fill(new Rect(0, 0, double.MaxValue, 1), new Color(255, 255, 255))),
            64,
            48,
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
            Commands(1.5, Fill(
                new Rect(0, 0, 20, 20),
                new Color(255, 255, 255),
                new Rect(1.2, 2.2, 3.1, 4.1))),
            100,
            100,
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
            true);

        var vertex = builder.Vertices[0];
        const float channel = 128 / 255f;
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
            true);

        Assert.Equal(10 / 255f / 12.92f, builder.Vertices[0].R);
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
            false);

        Assert.Equal(12u, Assert.Single(builder.Batches.ToArray()).IndexCount);
    }

    [Fact]
    public void CapacityGrowthUsesRequiredValueNearIntegerLimit() =>
        Assert.Equal(
            int.MaxValue,
            UiDrawBuilder.GrowCapacity(int.MaxValue / 2 + 1, int.MaxValue));

    [Fact]
    public void ImageVerticesContainNormalizedUvAndTexelCenterBounds()
    {
        var image = UiImage.FromRgba(new byte[64], 4, 4);
        var builder = new UiDrawBuilder();

        builder.Build(
            Commands(new UiDrawImageCommand(
                new Rect(0, 0, 8, 8),
                image,
                new Rect(1, 1, 2, 2),
                ImageSamplingMode.Linear,
                null,
                0.5)),
            16,
            16,
            false,
            _ => new UiImageRenderBinding(
                17,
                16,
                16,
                new UiImageAtlasRegion(4, 8, 4, 4)));

        var first = builder.Vertices[0];
        Assert.Equal(5 / 16f, first.U);
        Assert.Equal(9 / 16f, first.V);
        Assert.Equal(5.5f / 16, first.ClampMinU);
        Assert.Equal(9.5f / 16, first.ClampMinV);
        Assert.Equal(6.5f / 16, first.ClampMaxU);
        Assert.Equal(10.5f / 16, first.ClampMaxV);
        Assert.Equal(0.5f, first.A);
        Assert.Equal(
            PackMaterialData(UiMaterialKind.ImageLinear, 17),
            first.MaterialData);
        Assert.Single(builder.Batches.ToArray());
    }

    [Fact]
    public void SubTexelImageSourceClampsToItsCenter()
    {
        var image = UiImage.FromRgba(new byte[4], 1, 1);
        var builder = new UiDrawBuilder();

        builder.Build(
            Commands(new UiDrawImageCommand(
                new Rect(0, 0, 1, 1),
                image,
                new Rect(0.25, 0.125, 0.5, 0.25),
                ImageSamplingMode.Linear,
                null,
                1)),
            10,
            10,
            false,
            _ => new UiImageRenderBinding(
                1,
                16,
                8,
                new UiImageAtlasRegion(4, 2, 1, 1)));

        var first = builder.Vertices[0];
        Assert.Equal(4.5f / 16, first.ClampMinU);
        Assert.Equal(first.ClampMinU, first.ClampMaxU);
        Assert.Equal(2.25f / 8, first.ClampMinV);
        Assert.Equal(first.ClampMinV, first.ClampMaxV);
    }

    [Fact]
    public void PendingImageDoesNotSplitCompatibleSolidBatches()
    {
        var image = UiImage.FromRgba(new byte[4], 1, 1);
        var clip = new Rect(0, 0, 10, 10);
        var builder = new UiDrawBuilder();

        builder.Build(
            Commands(
                Fill(new Rect(0, 0, 1, 1), new Color(1, 0, 0), clip),
                new UiDrawImageCommand(new Rect(1, 0, 1, 1), image, image.FullSourceRect,
                    ImageSamplingMode.Linear, clip, 1),
                Fill(new Rect(2, 0, 1, 1), new Color(0, 1, 0), clip)),
            10,
            10,
            false,
            static _ => null);

        Assert.Equal(12u, Assert.Single(builder.Batches.ToArray()).IndexCount);
    }

    [Fact]
    public void DifferentImageSlotsAndSamplingModesShareScissorBatch()
    {
        var firstImage = UiImage.FromRgba(new byte[4], 1, 1);
        var secondImage = UiImage.FromRgba(new byte[4], 1, 1);
        var builder = new UiDrawBuilder();

        builder.Build(
            Commands(
                new UiDrawImageCommand(new Rect(0, 0, 1, 1), firstImage, firstImage.FullSourceRect,
                    ImageSamplingMode.Linear, null, 1),
                new UiDrawImageCommand(new Rect(1, 0, 1, 1), firstImage, firstImage.FullSourceRect,
                    ImageSamplingMode.Linear, null, 1),
                new UiDrawImageCommand(new Rect(2, 0, 1, 1), firstImage, firstImage.FullSourceRect,
                    ImageSamplingMode.Nearest, null, 1),
                new UiDrawImageCommand(new Rect(3, 0, 1, 1), secondImage, secondImage.FullSourceRect,
                    ImageSamplingMode.Linear, null, 1)),
            10,
            10,
            false,
            command => command.Image == firstImage
                ? new UiImageRenderBinding(
                    command.SamplingMode == ImageSamplingMode.Linear ? 1u : 2u,
                    1,
                    1,
                    new UiImageAtlasRegion(0, 0, 1, 1))
                : new UiImageRenderBinding(3, 1, 1, new UiImageAtlasRegion(0, 0, 1, 1)));

        Assert.Equal(24u, Assert.Single(builder.Batches.ToArray()).IndexCount);
        Assert.Equal(
            [
                PackMaterialData(UiMaterialKind.ImageLinear, 1),
                PackMaterialData(UiMaterialKind.ImageLinear, 1),
                PackMaterialData(UiMaterialKind.ImageNearest, 2),
                PackMaterialData(UiMaterialKind.ImageLinear, 3)
            ],
            builder.Vertices.ToArray().Chunk(4).Select(vertices => vertices[0].MaterialData));
    }

    private static uint PackMaterialData(UiMaterialKind materialKind, uint textureIndex) =>
        (textureIndex << 8) | (uint)materialKind;

    private static UiDrawCommandList Commands(params UiDrawCommand[] commands) =>
        new(commands.ToList(), 1);

    private static UiDrawCommandList Commands(double scale, params UiDrawCommand[] commands) =>
        new(commands.ToList(), scale);

    private static UiFillRectangleCommand Fill(
        Rect bounds,
        Color color,
        Rect? clip = null,
        double opacity = 1) =>
        new(bounds, color, clip, opacity);

}
