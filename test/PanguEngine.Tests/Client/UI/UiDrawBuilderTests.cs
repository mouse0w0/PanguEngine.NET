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
        Assert.Equal(48, Marshal.OffsetOf<UiVertex>(nameof(UiVertex.DrawMetadata)).ToInt32());
        Assert.Equal(52u, Assert.Single(UiVertex.VertexInput.Buffers).Stride);
        Assert.Equal(
            [
                (4u, VertexAttributeFormat.UInt32, 48u)
            ],
            UiVertex.VertexInput.Attributes.Skip(4)
                .Select(attribute => (attribute.Location, attribute.Format, attribute.Offset)));
    }

    [Theory]
    [InlineData(0u, 0u, 0u, 0u)]
    [InlineData(1u, 255u, 0u, 0x3fdu)]
    [InlineData(2u, 17u, 0x12345u, 0x048d1446u)]
    [InlineData(0u, 0u, 0x200000u, 0x80000000u)]
    [InlineData(3u, 255u, 0x3fffffu, 0xffffffffu)]
    public void DrawMetadataPreservesAllComponentsAtPackingBoundaries(
        uint materialKind, uint textureIndex, uint drawDataIndex, uint expectedMetadata)
    {
        var vertex = new UiVertex(0, 0, 1, 1, 1, 1,
            materialKind: (UiMaterialKind)materialKind, textureIndex: textureIndex, drawDataIndex: drawDataIndex);
        Assert.Equal(expectedMetadata, vertex.DrawMetadata);
        Assert.Equal((UiMaterialKind)materialKind, vertex.MaterialKind);
        Assert.Equal(textureIndex, vertex.TextureIndex);
        Assert.Equal(drawDataIndex, vertex.DrawDataIndex);
        Assert.Equal(256u, UiTextureTable.SlotCount);
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
    public void ClipKeepsFractionalParametersAndRoundsItsQuadBoundsOutward()
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
        Assert.Equal(new UiScissor(0, 0, 100, 100), batch.Scissor);
        Assert.Equal(1f, builder.Vertices[0].X);
        Assert.Equal(3f, builder.Vertices[0].Y);
        Assert.Equal(7f, builder.Vertices[2].X);
        Assert.Equal(10f, builder.Vertices[2].Y);
        var data = builder.DrawData[(int)builder.Vertices[0].DrawDataIndex];
        var clip = builder.ClipData[(int)data.ClipIndex];
        Assert.Equal(1.8f, clip.Bounds.X, 6);
        Assert.Equal(3.3f, clip.Bounds.Y, 6);
        Assert.Equal(6.45f, clip.Bounds.Z, 6);
        Assert.Equal(9.45f, clip.Bounds.W, 6);
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

        Assert.Equal(new UiScissor(0, 0, 100, 100), Assert.Single(builder.Batches.ToArray()).Scissor);
        Assert.Equal(5f, builder.Vertices[0].X);
        Assert.Equal(5f, builder.Vertices[0].Y);
        Assert.Equal(10f, builder.Vertices[2].X);
        Assert.Equal(10f, builder.Vertices[2].Y);
        var data = builder.DrawData[(int)builder.Vertices[0].DrawDataIndex];
        var bounds = new List<System.Numerics.Vector4>();
        for (var index = data.ClipIndex; index != 0; index = builder.ClipData[(int)index].ParentIndex)
            bounds.Add(builder.ClipData[(int)index].Bounds);
        Assert.Contains(new System.Numerics.Vector4(5, 5, 15, 15), bounds);
        Assert.Contains(new System.Numerics.Vector4(0, 0, 10, 10), bounds);
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

        Assert.Equal(new UiScissor(0, 0, 100, 100), Assert.Single(builder.Batches.ToArray()).Scissor);
        Assert.Equal(4f, builder.Vertices[0].X);
        Assert.Equal(4f, builder.Vertices[0].Y);
        Assert.Equal(10f, builder.Vertices[2].X);
        Assert.Equal(10f, builder.Vertices[2].Y);
        var data = builder.DrawData[(int)builder.Vertices[0].DrawDataIndex];
        Assert.Equal(new System.Numerics.Vector4(0, 0, 10, 10), builder.ClipData[(int)data.ClipIndex].Bounds);
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

        Assert.Equal(new UiScissor(0, 0, 10, 10), Assert.Single(builder.Batches.ToArray()).Scissor);
        Assert.Equal(2f, builder.Vertices[2].X);
        Assert.Equal(2f, builder.Vertices[2].Y);
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
    public void RectanglesKeepDrawOrderAcrossDifferentClips()
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

        Assert.Equal(new[] { 1 / 255f, 2 / 255f, 3 / 255f, 4 / 255f }
                .SelectMany(color => Enumerable.Repeat(color, 6)),
            builder.Indices.ToArray().Select(index => builder.Vertices[(int)index].R));
        Assert.Equal(
            [new UiBatch(new UiScissor(0, 0, 100, 100), 0, 24)],
            builder.Batches.ToArray());
    }

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
        Assert.Equal(new UiVertex(1, 1, 0, 1, 0, 1, drawDataIndex: builder.Vertices[0].DrawDataIndex), builder.Vertices[0]);
        Assert.Equal(new UiVertex(0, 0, 0, 0, 1, 1), builder.Vertices[4]);
        Assert.Equal(
            [new UiBatch(new UiScissor(0, 0, 100, 100), 0, 12)],
            builder.Batches.ToArray());
        Assert.NotEqual(0u, builder.Vertices[0].DrawDataIndex);
        Assert.Equal(0u, builder.Vertices[4].DrawDataIndex);
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
        Assert.Equal(UiMaterialKind.ImageLinear, first.MaterialKind);
        Assert.Equal(17u, first.TextureIndex);
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
                (UiMaterialKind.ImageLinear, 1u),
                (UiMaterialKind.ImageLinear, 1u),
                (UiMaterialKind.ImageNearest, 2u),
                (UiMaterialKind.ImageLinear, 3u)
            ],
            builder.Vertices.ToArray().Chunk(4).Select(vertices => (vertices[0].MaterialKind, vertices[0].TextureIndex)));
    }

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
