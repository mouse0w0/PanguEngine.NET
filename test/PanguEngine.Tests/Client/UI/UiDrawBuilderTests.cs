using System.Runtime.InteropServices;
using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Drawing;
using PanguEngine.Client.UI.Rendering;
using PanguEngine.Graphics;

namespace PanguEngine.Tests.Client.UI;

public sealed class UiDrawBuilderTests
{
    [Fact]
    public void UnifiedVertexInputMatchesFiftyTwoByteLayout()
    {
        Assert.Equal(52u, UiVertex.SizeInBytes);
        Assert.Equal(52, Marshal.SizeOf<UiVertex>());
        Assert.Equal(52u, Assert.Single(UiVertex.VertexInput.Buffers).Stride);
        Assert.Equal(
            [
                (4u, VertexAttributeFormat.UInt32, 48u)
            ],
            UiVertex.VertexInput.Attributes.Skip(4)
                .Select(attribute => (attribute.Location, attribute.Format, attribute.Offset)));
    }

    [Fact]
    public void NonFiniteTransformCompositionFailsAndClearsGeometry()
    {
        var builder = new UiDrawBuilder();
        builder.Build(
            CommandList(Fill(new Rect(1, 2, 3, 4), new Color(255, 255, 255))),
            100,
            100,
            false);

        var overflow = CommandList(
            Transform(new Point(double.MaxValue, 0), 1),
            Transform(new Point(double.MaxValue, 0), 1),
            Fill(new Rect(0, 0, 1, 1), new Color(255, 255, 255)));

        Assert.Throws<InvalidOperationException>(() => builder.Build(overflow, 100, 100, false));
        Assert.Empty(builder.Vertices.ToArray());
        Assert.Empty(builder.Indices.ToArray());
        Assert.Empty(builder.Batches.ToArray());

        builder.Build(
            CommandList(Fill(new Rect(1, 2, 3, 4), new Color(255, 255, 255))),
            100,
            100,
            false);
        Assert.Single(builder.Batches.ToArray());
    }

    [Fact]
    public void TransformScaleUnderflowToZeroFailsAndClearsGeometry()
    {
        var builder = new UiDrawBuilder();
        var commands = CommandList(
            Transform(Point.Zero, 1e-200),
            Transform(Point.Zero, 1e-200),
            Fill(new Rect(0, 0, 1, 1), new Color(255, 255, 255)));

        Assert.Throws<InvalidOperationException>(() => builder.Build(commands, 100, 100, false));
        Assert.Empty(builder.Vertices.ToArray());
        Assert.Empty(builder.Batches.ToArray());
    }

    [Fact]
    public void ZeroFramebufferProducesAnEmptyResult()
    {
        var builder = new UiDrawBuilder();
        var commands = CommandList(Fill(new Rect(1, 2, 3, 4), new Color(1, 2, 3)));

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
            CommandList(
                Transform(Point.Zero, 1.5),
                Fill(new Rect(-2, 4, 12, 20), new Color(255, 0, 0)),
                Pop),
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
    public void NestedTransformsComposeScaleAndTranslation()
    {
        var builder = new UiDrawBuilder();
        builder.Build(
            CommandList(
                Transform(new Point(10, 0), 2),
                Transform(new Point(1, 2), 3),
                Fill(new Rect(1, 1, 1, 1), new Color(255, 255, 255)),
                Pop,
                Pop),
            200,
            200,
            false);

        Assert.Equal(new UiVertex(18, 10, 1, 1, 1, 1), builder.Vertices[0]);
        Assert.Equal(new UiVertex(24, 16, 1, 1, 1, 1), builder.Vertices[2]);
    }

    [Fact]
    public void ReusedBuilderUsesEachStreamStateIndependently()
    {
        var command = Fill(new Rect(1, 2, 3, 4), new Color(255, 255, 255));
        var first = CommandList(Transform(Point.Zero, 2), command, Pop);
        var second = CommandList(Transform(Point.Zero, 0.5), command, Pop);
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
            CommandList(Fill(new Rect(20, 20, 5, 5), new Color(255, 255, 255))),
            10,
            10,
            false);

        Assert.Empty(builder.Vertices.ToArray());
        Assert.Empty(builder.Indices.ToArray());
        Assert.Empty(builder.Batches.ToArray());
    }

    [Fact]
    public void ScalingClampsToFramebufferWithoutInfiniteVertices()
    {
        var builder = new UiDrawBuilder();
        builder.Build(
            CommandList(
                Transform(Point.Zero, 1e6),
                Fill(new Rect(0, 0, 1e6, 1), new Color(255, 255, 255)),
                Pop),
            64,
            48,
            false);

        Assert.All(builder.Vertices.ToArray(), vertex =>
        {
            Assert.True(float.IsFinite(vertex.X));
            Assert.True(float.IsFinite(vertex.Y));
        });
        Assert.Equal(new UiVertex(0, 0, 1, 1, 1, 1), builder.Vertices[0]);
        Assert.Equal(new UiVertex(64, 48, 1, 1, 1, 1), builder.Vertices[2]);
    }

    [Fact]
    public void ClipUsesInclusivePhysicalScissorRounding()
    {
        var builder = new UiDrawBuilder();
        builder.Build(
            CommandList(
                Transform(Point.Zero, 1.5),
                Clip(new Rect(1.2, 2.2, 3.1, 4.1)),
                Fill(new Rect(0, 0, 20, 20), new Color(255, 255, 255)),
                Pop,
                Pop),
            100,
            100,
            false);

        var batch = Assert.Single(builder.Batches.ToArray());
        Assert.Equal(new UiScissor(1, 3, 6, 7), batch.Scissor);
    }

    [Fact]
    public void FractionalZeroAreaClipProducesNoGeometry()
    {
        var builder = new UiDrawBuilder();
        builder.Build(
            CommandList(
                Clip(new Rect(5.5, 0, 0, 10)),
                Fill(new Rect(0, 0, 20, 20), new Color(255, 255, 255)),
                Pop),
            10,
            10,
            false);

        Assert.Empty(builder.Vertices.ToArray());
        Assert.Empty(builder.Batches.ToArray());
    }

    [Fact]
    public void NullClipUsesFullFramebufferScissor()
    {
        var builder = new UiDrawBuilder();
        builder.Build(
            CommandList(Fill(new Rect(0, 0, 5, 5), new Color(255, 255, 255))),
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
            CommandList(
                Clip(new Rect(20, 20, 5, 5)),
                Fill(new Rect(0, 0, 1, 1), new Color(255, 255, 255)),
                Pop,
                Clip(new Rect(1, 0, 1, 1)),
                Fill(new Rect(0, 0, 1, 1), new Color(255, 255, 255)),
                Pop),
            10,
            10,
            false);

        Assert.Empty(builder.Vertices.ToArray());
        Assert.Empty(builder.Batches.ToArray());
    }

    [Fact]
    public void NestedClipsIntersectAtPushTime()
    {
        var builder = new UiDrawBuilder();
        builder.Build(
            CommandList(
                Clip(new Rect(0, 0, 10, 10)),
                Transform(new Point(5, 5), 1),
                Clip(new Rect(0, 0, 10, 10)),
                Fill(new Rect(0, 0, 20, 20), new Color(255, 255, 255)),
                Pop,
                Pop,
                Pop),
            100,
            100,
            false);

        Assert.Equal(new UiScissor(5, 5, 5, 5), Assert.Single(builder.Batches.ToArray()).Scissor);
    }

    [Fact]
    public void ClipKeepsItsPushTimeTransformWhenLaterTransformsChange()
    {
        var builder = new UiDrawBuilder();
        builder.Build(
            CommandList(
                Transform(Point.Zero, 2),
                Clip(new Rect(0, 0, 5, 5)),
                Transform(new Point(2, 2), 1),
                Fill(new Rect(0, 0, 5, 5), new Color(255, 255, 255)),
                Pop,
                Pop,
                Pop),
            100,
            100,
            false);

        Assert.Equal(new UiScissor(0, 0, 10, 10), Assert.Single(builder.Batches.ToArray()).Scissor);
        Assert.Equal(new UiVertex(4, 4, 1, 1, 1, 1), builder.Vertices[0]);
        Assert.Equal(new UiVertex(14, 14, 1, 1, 1, 1), builder.Vertices[2]);
    }

    [Fact]
    public void PartlyOutsideClipIsIntersectedWithFramebuffer()
    {
        var builder = new UiDrawBuilder();
        builder.Build(
            CommandList(
                Clip(new Rect(-1, -1, 3, 3)),
                Fill(new Rect(0, 0, 5, 5), new Color(255, 255, 255)),
                Pop),
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
            CommandList(
                Opacity(0.25),
                Fill(new Rect(0, 0, 1, 1), new Color(128, 64, 32, 128)),
                Pop),
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
            CommandList(
                Opacity(0.5),
                Fill(new Rect(0, 0, 1, 1), new Color(128, 255, 0, 64)),
                Pop),
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
            CommandList(Fill(new Rect(0, 0, 1, 1), new Color(10, 0, 0))),
            10,
            10,
            true);

        Assert.Equal(10 / 255f / 12.92f, builder.Vertices[0].R);
    }

    [Fact]
    public void NestedOpacityScopesRestorePreviousAlphaAndKeepOneBatch()
    {
        var builder = new UiDrawBuilder();
        builder.Build(
            CommandList(
                Opacity(0.5),
                Fill(new Rect(0, 0, 1, 1), new Color(255, 255, 255)),
                Opacity(0.5),
                Fill(new Rect(1, 0, 1, 1), new Color(255, 255, 255)),
                Pop,
                Fill(new Rect(2, 0, 1, 1), new Color(255, 255, 255)),
                Pop,
                Fill(new Rect(3, 0, 1, 1), new Color(255, 255, 255))),
            10,
            10,
            false);

        Assert.Equal(
            [0.5f, 0.25f, 0.5f, 1f],
            builder.Vertices.ToArray().Chunk(4).Select(vertices => vertices[0].A));
        Assert.Equal(24u, Assert.Single(builder.Batches.ToArray()).IndexCount);
    }

    [Fact]
    public void TransformScopesDoNotSplitEqualScissorBatch()
    {
        var builder = new UiDrawBuilder();
        builder.Build(
            CommandList(
                Transform(Point.Zero, 1),
                Fill(new Rect(0, 0, 1, 1), new Color(1, 0, 0)),
                Transform(new Point(2, 0), 1),
                Fill(new Rect(0, 0, 1, 1), new Color(2, 0, 0)),
                Pop,
                Fill(new Rect(0, 0, 1, 1), new Color(3, 0, 0)),
                Pop),
            10,
            10,
            false);

        Assert.Equal(18u, Assert.Single(builder.Batches.ToArray()).IndexCount);
    }

    [Fact]
    public void RectanglesUseUInt32IndicesAndMergeOnlyConsecutiveEqualScissors()
    {
        var firstClip = new Rect(0, 0, 10, 10);
        var secondClip = new Rect(20, 0, 10, 10);
        var builder = new UiDrawBuilder();
        builder.Build(
            CommandList(
                Clip(firstClip),
                Fill(new Rect(0, 0, 2, 2), new Color(1, 0, 0)),
                Fill(new Rect(2, 0, 2, 2), new Color(2, 0, 0)),
                Pop,
                Clip(secondClip),
                Fill(new Rect(20, 0, 2, 2), new Color(3, 0, 0)),
                Pop,
                Clip(firstClip),
                Fill(new Rect(4, 0, 2, 2), new Color(4, 0, 0)),
                Pop),
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
            CommandList(
                Clip(clip),
                Fill(new Rect(0, 0, 2, 2), new Color(1, 0, 0)),
                Fill(new Rect(50, 50, 2, 2), new Color(2, 0, 0)),
                Fill(new Rect(2, 0, 2, 2), new Color(3, 0, 0)),
                Pop),
            20,
            20,
            false);

        Assert.Equal(12u, Assert.Single(builder.Batches.ToArray()).IndexCount);
    }

    [Fact]
    public void PreciseClipRejectsGeometryInsideOnlyTheRoundedScissor()
    {
        var image = UiImage.FromRgba(new byte[4], 1, 1);
        var builder = new UiDrawBuilder();
        var resolutions = 0;
        builder.Build(
            CommandList(
                Clip(new Rect(2.2, 0, 0.1, 10)),
                Fill(new Rect(2.05, 0, 0.05, 1), new Color(255, 255, 255)),
                Image(new Rect(2.05, 0, 0.05, 1), image, image.FullSourceRect, ImageSamplingMode.Linear),
                Pop),
            100, 100, false,
            _ =>
            {
                resolutions++;
                return new UiImageRenderBinding(1, 1, 1, new UiImageAtlasRegion(0, 0, 1, 1));
            });

        Assert.Equal(0, resolutions);
        Assert.Empty(builder.Vertices.ToArray());
    }

    [Fact]
    public void EmptyIntersectionStaysEmptyUntilItsScopeIsPopped()
    {
        var builder = new UiDrawBuilder();
        builder.Build(
            CommandList(
                Clip(new Rect(1, 1, 5, 5)),
                Clip(new Rect(10, 10, 5, 5)),
                Transform(new Point(1, 1), 2),
                Clip(new Rect(0, 0, 20, 20)),
                Fill(new Rect(0, 0, 20, 20), new Color(255, 0, 0)),
                Pop, Pop, Pop,
                Fill(new Rect(0, 0, 20, 20), new Color(0, 255, 0)),
                Pop,
                Fill(new Rect(0, 0, 20, 20), new Color(0, 0, 255))),
            100, 100, false);

        Assert.Equal(2, builder.RectangleCount);
        Assert.Equal(new UiVertex(0, 0, 0, 1, 0, 1), builder.Vertices[0]);
        Assert.Equal(new UiVertex(0, 0, 0, 0, 1, 1), builder.Vertices[4]);
        Assert.Equal(
            [new UiBatch(new UiScissor(1, 1, 5, 5), 0, 6), new UiBatch(new UiScissor(0, 0, 100, 100), 6, 6)],
            builder.Batches.ToArray());
    }

    [Fact]
    public void ResolverExceptionDiscardsPartialGeometryAndRestoresBuilderState()
    {
        var image = UiImage.FromRgba(new byte[4], 1, 1);
        var expected = new InvalidOperationException("resolver failed");
        var builder = new UiDrawBuilder();
        var commands = CommandList(
            Transform(new Point(5, 6), 2),
            Fill(new Rect(0, 0, 2, 2), new Color(255, 255, 255)),
            Image(new Rect(0, 0, 2, 2), image, image.FullSourceRect, ImageSamplingMode.Linear),
            Pop);

        Assert.Same(expected, Assert.Throws<InvalidOperationException>(() =>
            builder.Build(commands, 100, 100, false, _ => throw expected)));
        Assert.Empty(builder.Vertices.ToArray());
        Assert.Empty(builder.Indices.ToArray());
        Assert.Empty(builder.Batches.ToArray());

        builder.Build(CommandList(Fill(new Rect(1, 2, 3, 4), new Color(255, 255, 255))), 100, 100, false);
        Assert.Equal(new UiVertex(1, 2, 1, 1, 1, 1), builder.Vertices[0]);
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
            CommandList(
                Opacity(0.5),
                Image(new Rect(0, 0, 8, 8), image, new Rect(1, 1, 2, 2), ImageSamplingMode.Linear),
                Pop),
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
    public void ImageCommandsArePositionedByEffectiveTransform()
    {
        var image = UiImage.FromRgba(new byte[4], 1, 1);
        var builder = new UiDrawBuilder();

        builder.Build(
            CommandList(
                Transform(new Point(4, 6), 2),
                Image(new Rect(1, 1, 2, 2), image, image.FullSourceRect, ImageSamplingMode.Linear),
                Pop),
            100,
            100,
            false,
            _ => new UiImageRenderBinding(1, 1, 1, new UiImageAtlasRegion(0, 0, 1, 1)));

        Assert.Equal(6, builder.Vertices[0].X);
        Assert.Equal(8, builder.Vertices[0].Y);
        Assert.Equal(10, builder.Vertices[2].X);
        Assert.Equal(12, builder.Vertices[2].Y);
    }

    [Fact]
    public void SourceRectKeepsPixelUvUnderTransform()
    {
        var image = UiImage.FromRgba(new byte[64], 4, 4);
        var builder = new UiDrawBuilder();

        builder.Build(
            CommandList(
                Transform(new Point(10, 20), 2),
                Image(new Rect(0, 0, 8, 8), image, new Rect(1, 1, 2, 2), ImageSamplingMode.Linear),
                Pop),
            64,
            64,
            false,
            _ => new UiImageRenderBinding(5, 16, 16, new UiImageAtlasRegion(4, 8, 4, 4)));

        var first = builder.Vertices[0];
        Assert.Equal(5 / 16f, first.U);
        Assert.Equal(9 / 16f, first.V);
        Assert.Equal(5.5f / 16, first.ClampMinU);
        Assert.Equal(9.5f / 16, first.ClampMinV);
        Assert.Equal(6.5f / 16, first.ClampMaxU);
        Assert.Equal(10.5f / 16, first.ClampMaxV);
        Assert.Equal(10, first.X);
        Assert.Equal(20, first.Y);
    }

    [Fact]
    public void ZeroOpacityDoesNotResolveImage()
    {
        var image = UiImage.FromRgba(new byte[4], 1, 1);
        var resolutions = 0;
        var builder = new UiDrawBuilder();

        builder.Build(
            CommandList(
                Opacity(0),
                Image(new Rect(0, 0, 1, 1), image, image.FullSourceRect, ImageSamplingMode.Linear),
                Pop),
            10,
            10,
            false,
            _ =>
            {
                resolutions++;
                return new UiImageRenderBinding(1, 1, 1, new UiImageAtlasRegion(0, 0, 1, 1));
            });

        Assert.Equal(0, resolutions);
        Assert.Empty(builder.Vertices.ToArray());
    }

    [Fact]
    public void SubTexelImageSourceClampsToItsCenter()
    {
        var image = UiImage.FromRgba(new byte[4], 1, 1);
        var builder = new UiDrawBuilder();

        builder.Build(
            CommandList(
                Image(
                    new Rect(0, 0, 1, 1),
                    image,
                    new Rect(0.25, 0.125, 0.5, 0.25),
                    ImageSamplingMode.Linear)),
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
        var builder = new UiDrawBuilder();

        builder.Build(
            CommandList(
                Fill(new Rect(0, 0, 1, 1), new Color(1, 0, 0)),
                Image(new Rect(1, 0, 1, 1), image, image.FullSourceRect, ImageSamplingMode.Linear),
                Fill(new Rect(2, 0, 1, 1), new Color(0, 1, 0))),
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
            CommandList(
                Image(new Rect(0, 0, 1, 1), firstImage, firstImage.FullSourceRect, ImageSamplingMode.Linear),
                Image(new Rect(1, 0, 1, 1), firstImage, firstImage.FullSourceRect, ImageSamplingMode.Linear),
                Image(new Rect(2, 0, 1, 1), firstImage, firstImage.FullSourceRect, ImageSamplingMode.Nearest),
                Image(new Rect(3, 0, 1, 1), secondImage, secondImage.FullSourceRect, ImageSamplingMode.Linear)),
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

    private static UiDrawCommandList CommandList(params UiDrawCommand[] commands) =>
        new UiDrawCommandList([.. commands]);

    private static UiPushTransformCommand Transform(Point translation, double scale = 1) =>
        new(translation, scale);

    private static UiPushClipCommand Clip(Rect clip) =>
        new(clip);

    private static UiPushOpacityCommand Opacity(double opacity) =>
        new(opacity);

    private static UiDrawCommand Pop => UiPopCommand.Instance;

    private static UiFillRectangleCommand Fill(Rect bounds, Color color) =>
        new(bounds, color);

    private static UiDrawImageCommand Image(
        Rect bounds,
        UiImage image,
        Rect sourceRect,
        ImageSamplingMode samplingMode) =>
        new(bounds, image, sourceRect, samplingMode);
}
