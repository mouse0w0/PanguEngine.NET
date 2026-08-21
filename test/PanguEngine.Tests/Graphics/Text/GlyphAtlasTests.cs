using PanguEngine.Graphics;
using PanguEngine.Graphics.Text;
using GraphicsBuffer = PanguEngine.Graphics.Buffer;

namespace PanguEngine.Tests.Graphics.Text;

public sealed class GlyphAtlasTests
{
    [Fact]
    public void SameKeyRasterizesAndUploadsOnceWithTransparentPadding()
    {
        using var context = new GlyphAtlasContext();
        var key = context.CreateKey("A", 20);

        var first = Assert.IsType<GlyphAtlasEntry>(context.Atlas.Resolve(key));
        var second = Assert.IsType<GlyphAtlasEntry>(context.Atlas.Resolve(key));

        Assert.Same(first, second);
        Assert.Equal(1, context.Device.UploadCount);
        Assert.Equal(first.Region.Width + 2, context.Device.LastUploadRegion.Width);
        Assert.Equal(first.Region.Height + 2, context.Device.LastUploadRegion.Height);
        Assert.Equal(
            checked((int)((first.Region.Width + 2) * (first.Region.Height + 2))),
            context.Device.LastUploadData.Length);
        Assert.All(context.Device.GetUploadBorder(), value => Assert.Equal(0, value));
    }

    [Fact]
    public void EmptyBitmapDoesNotCreateGpuResources()
    {
        using var context = new GlyphAtlasContext();

        var entry = Assert.IsType<GlyphAtlasEntry>(context.Atlas.Resolve(context.CreateKey(" ", 20)));

        Assert.True(entry.IsEmpty);
        Assert.Equal(0, context.Device.TextureCount);
        Assert.Equal(0, context.Device.UploadCount);
    }

    [Fact]
    public void PublishedPageHasStableIdentityAndIsReused()
    {
        using var context = new GlyphAtlasContext(MutableGlyphUploadHandle.Ready());

        var first = Assert.IsType<GlyphAtlasEntry>(context.Atlas.Resolve(context.CreateKey("A", 20)));
        var firstPage = Assert.IsType<GlyphAtlasPage>(first.Page);
        var second = Assert.IsType<GlyphAtlasEntry>(context.Atlas.Resolve(context.CreateKey("B", 20)));
        var secondPage = Assert.IsType<GlyphAtlasPage>(second.Page);

        Assert.Same(firstPage, secondPage);
        Assert.Equal(firstPage.Id, secondPage.Id);
        Assert.Equal(firstPage.TextureIndex, secondPage.TextureIndex);
        Assert.NotEqual(0UL, firstPage.Id);
        Assert.Equal(0u, firstPage.TextureIndex);
        Assert.Equal(1, context.Device.TextureCount);
    }

    [Fact]
    public void FullRegularPageAppendsAnotherPageWithoutMovingPublishedEntry()
    {
        using var context = new GlyphAtlasContext(MutableGlyphUploadHandle.Ready());
        var keys = context.CreateKeys("ABCDEFGHIJKLMNOPQRSTUVWXYZ", 400);
        var first = Assert.IsType<GlyphAtlasEntry>(context.Atlas.Resolve(keys[0]));
        var firstPage = Assert.IsType<GlyphAtlasPage>(first.Page);
        var firstRegion = first.Region;

        var entries = keys.Skip(1)
            .Select(key => Assert.IsType<GlyphAtlasEntry>(context.Atlas.Resolve(key)))
            .ToArray();
        var nextPageEntry = Assert.Single(entries.Where(entry =>
            !ReferenceEquals(entry.Page, firstPage)).Take(1));

        Assert.NotSame(firstPage, nextPageEntry.Page);
        Assert.NotEqual(firstPage.TextureIndex, nextPageEntry.Page!.TextureIndex);
        Assert.Same(firstPage, first.Page);
        Assert.Equal(firstRegion, first.Region);
        Assert.True(context.Device.TextureCount >= 2);
    }

    [Fact]
    public void OversizedGlyphUsesExactDedicatedPage()
    {
        using var context = new GlyphAtlasContext(MutableGlyphUploadHandle.Ready());

        var entry = Assert.IsType<GlyphAtlasEntry>(context.Atlas.Resolve(context.CreateKey("盘", 3000)));
        var page = Assert.IsType<GlyphAtlasPage>(entry.Page);

        Assert.True(page.IsDedicated);
        Assert.Equal(entry.Region.Width + 2, page.Width);
        Assert.Equal(entry.Region.Height + 2, page.Height);
        Assert.Equal(TextureFormat.R8Unorm, page.Texture.Format);
    }

    [Fact]
    public void RasterizationFailureDoesNotCommitKeyOrGpuState()
    {
        using var context = new GlyphAtlasContext();
        using var foreign = new GlyphAtlasContext();
        var key = foreign.CreateKey("A", 20);

        Assert.Throws<ArgumentException>(() => context.Atlas.Resolve(key));
        Assert.Throws<ArgumentException>(() => context.Atlas.Resolve(key));

        Assert.Equal(0, context.Device.TextureCount);
        Assert.Equal(0, context.Device.UploadCount);
    }

    [Fact]
    public void ReadyUploadCanLaterBecomeFaulted()
    {
        var upload = new MutableGlyphUploadHandle();
        using var context = new GlyphAtlasContext(upload);
        var entry = Assert.IsType<GlyphAtlasEntry>(context.Atlas.Resolve(context.CreateKey("A", 20)));

        Assert.False(entry.IsUploadReady);
        upload.SetReady();
        Assert.True(entry.IsUploadReady);

        var failure = new InvalidOperationException("late");
        upload.SetFaulted(failure);

        Assert.True(entry.TryObserveUploadFailure(out var observed, out var first));
        Assert.Same(failure, observed);
        Assert.True(first);
        Assert.False(entry.IsUploadReady);
        Assert.True(entry.TryObserveUploadFailure(out observed, out first));
        Assert.Same(failure, observed);
        Assert.False(first);
    }

    [Fact]
    public void SynchronousUploadFailureIsCachedWithoutRetrying()
    {
        var failure = new InvalidOperationException("upload");
        using var context = new GlyphAtlasContext(uploadException: failure);
        var key = context.CreateKey("A", 20);

        var first = Assert.IsType<GlyphAtlasEntry>(context.Atlas.Resolve(key));
        var second = Assert.IsType<GlyphAtlasEntry>(context.Atlas.Resolve(key));

        Assert.Same(first, second);
        Assert.Equal(1, context.Device.UploadCount);
        Assert.True(first.TryObserveUploadFailure(out var observed, out var firstObservation));
        Assert.Same(failure, observed);
        Assert.True(firstObservation);
    }

    [Fact]
    public void PageCreationFailureRollsBackWithoutPublishingOrCaching()
    {
        var failure = new InvalidOperationException("view");
        using var context = new GlyphAtlasContext(viewException: failure);
        var key = context.CreateKey("A", 20);

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => context.Atlas.Resolve(key)));
        Assert.All(context.Device.Textures, texture => Assert.True(texture.IsDestroyed));
        Assert.All(context.Device.Views, view => Assert.True(view.IsDestroyed));

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => context.Atlas.Resolve(key)));
        Assert.Equal(2, context.Device.TextureCount);
    }

    [Fact]
    public void DestroyReleasesPageResourcesAndIsIdempotent()
    {
        var context = new GlyphAtlasContext(MutableGlyphUploadHandle.Ready());
        _ = context.Atlas.Resolve(context.CreateKey("A", 20));

        context.Atlas.Destroy();
        context.Atlas.Destroy();

        Assert.All(context.Device.Textures, texture => Assert.True(texture.IsDestroyed));
        Assert.All(context.Device.Views, view => Assert.True(view.IsDestroyed));
        context.Dispose();
    }

    [Fact]
    public void CapacityPendingGlyphCreatesPageWhenSlotBecomesAvailable()
    {
        using var context = new GlyphAtlasContext(hasFreeSlot: false);
        var key = context.CreateKey("A", 20);

        Assert.Null(context.Atlas.Resolve(key));
        Assert.Null(context.Atlas.Resolve(key));
        Assert.Equal(0, context.Device.TextureCount);
        Assert.Equal(0, context.Device.UploadCount);

        context.TextureRegistry.HasFreeSlot = true;
        var entry = Assert.IsType<GlyphAtlasEntry>(context.Atlas.Resolve(key));

        Assert.Equal(1, context.Device.TextureCount);
        Assert.Equal(1, context.Device.UploadCount);
        Assert.Equal(0u, entry.Page!.TextureIndex);
    }

    private sealed class GlyphAtlasContext : IDisposable
    {
        private static readonly string FontPath = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "Fonts",
            "SourceHanSansCN-Regular.otf");

        internal GlyphAtlasContext(
            MutableGlyphUploadHandle? upload = null,
            Exception? uploadException = null,
            Exception? viewException = null,
            bool hasFreeSlot = true)
        {
            FontManager = new FontManager();
            using var stream = File.OpenRead(FontPath);
            var font = Assert.Single(FontManager.Register(stream, 0));
            FontManager.DefaultFont = font;
            Face = FontManager.Match(font);
            LayoutEngine = new TextLayoutEngine(FontManager);
            Device = new GlyphTestGraphicsDevice(upload, uploadException, viewException);
            TextureRegistry = new GlyphTestTextureRegistry { HasFreeSlot = hasFreeSlot };
            Atlas = new GlyphAtlas(
                Device,
                FontManager,
                TextureRegistry);
        }

        internal GlyphTestGraphicsDevice Device { get; }
        internal FontManager FontManager { get; }
        internal GlyphAtlas Atlas { get; }
        internal GlyphTestTextureRegistry TextureRegistry { get; }
        private FontFace Face { get; }
        private TextLayoutEngine LayoutEngine { get; }

        internal GlyphRasterKey CreateKey(string text, uint pixelSize)
        {
            return Assert.Single(CreateKeys(text, pixelSize));
        }

        internal IReadOnlyList<GlyphRasterKey> CreateKeys(string text, uint pixelSize)
        {
            var layout = LayoutEngine.Layout(new TextLayoutRequest(
                text,
                Face.Font,
                16,
                double.PositiveInfinity,
                1,
                TextWrapping.NoWrap,
                TextAlignment.Left));
            return Assert.Single(layout.Lines).GlyphRuns
                .SelectMany(run => run.Glyphs.Select(glyph => new GlyphRasterKey(
                    run.FontFace,
                    pixelSize,
                    glyph.GlyphId,
                    GlyphRasterizationMode.Grayscale)))
                .ToArray();
        }

        public void Dispose()
        {
            Atlas.Destroy();
            FontManager.Destroy();
        }
    }

    private sealed class GlyphTestGraphicsDevice(
        MutableGlyphUploadHandle? upload,
        Exception? uploadException,
        Exception? viewException) : GraphicsDevice
    {
        private readonly MutableGlyphUploadHandle _upload = upload ?? new MutableGlyphUploadHandle();

        internal List<GlyphTestTexture> Textures { get; } = [];
        internal List<GlyphTestTextureView> Views { get; } = [];
        internal int TextureCount => Textures.Count;
        internal int UploadCount { get; private set; }
        internal byte[] LastUploadData { get; private set; } = [];
        internal TextureUploadRegion LastUploadRegion { get; private set; }

        public override uint MaxTextureDimension2D => 4096;
        public override uint MaxDrawIndirectCount => 1;

        public override GraphicsBuffer CreateBuffer(in BufferDescription description) =>
            throw new NotSupportedException();

        public override UploadHandle UploadBuffer<T>(GraphicsBuffer destination, ReadOnlySpan<T> data,
            ulong destinationOffset = 0) => throw new NotSupportedException();

        public override Texture CreateTexture(in TextureDescription description)
        {
            var texture = new GlyphTestTexture(description);
            Textures.Add(texture);
            return texture;
        }

        public override TextureView CreateTextureView(Texture texture, in TextureViewDescription description)
        {
            if (viewException is not null)
                throw viewException;
            var view = new GlyphTestTextureView(texture, description);
            Views.Add(view);
            return view;
        }

        public override UploadHandle UploadTexture(Texture destination, ReadOnlySpan<byte> data) =>
            throw new NotSupportedException();

        public override UploadHandle UploadTexture(
            Texture destination,
            ReadOnlySpan<byte> data,
            in TextureUploadRegion region)
        {
            UploadCount++;
            if (uploadException is not null)
                throw uploadException;
            LastUploadData = data.ToArray();
            LastUploadRegion = region;
            return _upload;
        }

        public override UploadHandle GenerateMipmaps(Texture texture) => throw new NotSupportedException();
        public override Sampler CreateSampler(in SamplerDescription description) => throw new NotSupportedException();
        public override Shader CreateShader(in ShaderDescription description) => throw new NotSupportedException();

        public override DescriptorSetLayout CreateDescriptorSetLayout(in DescriptorSetLayoutDescription description) =>
            throw new NotSupportedException();

        public override DescriptorSet CreateDescriptorSet(in DescriptorSetDescription description)
            => throw new NotSupportedException();

        public override ulong GetAlignedUniformSize(ulong rawSize) => rawSize;

        public override GraphicsPipeline CreateGraphicsPipeline(in GraphicsPipelineDescription description) =>
            throw new NotSupportedException();

        public override void WaitIdle()
        {
        }

        internal IEnumerable<byte> GetUploadBorder()
        {
            var width = checked((int)LastUploadRegion.Width);
            var height = checked((int)LastUploadRegion.Height);
            for (var x = 0; x < width; x++)
            {
                yield return LastUploadData[x];
                yield return LastUploadData[(height - 1) * width + x];
            }

            for (var y = 1; y < height - 1; y++)
            {
                yield return LastUploadData[y * width];
                yield return LastUploadData[y * width + width - 1];
            }
        }
    }

    private sealed class MutableGlyphUploadHandle : UploadHandle
    {
        private UploadState _state;
        private Exception? _exception;

        protected override UploadState State => _state;
        public override Exception? Exception => _exception;

        internal static MutableGlyphUploadHandle Ready()
        {
            var handle = new MutableGlyphUploadHandle();
            handle.SetReady();
            return handle;
        }

        internal void SetReady() => _state = UploadState.Ready;

        internal void SetFaulted(Exception exception)
        {
            _exception = exception;
            _state = UploadState.Faulted;
        }
    }

    private sealed class GlyphTestTexture(TextureDescription description) : Texture
    {
        public override TextureDimension Dimension => description.Dimension;
        public override TextureFormat Format => description.Format;
        public override uint Width => description.Width;
        public override uint Height => description.Height;
        public override uint Depth => description.Depth;
        public override uint MipLevels => description.MipLevels;
        public override uint ArrayLayers => description.ArrayLayers;
        public override TextureUsage Usage => description.Usage;
        public override TextureCreateFlags CreateFlags => description.Flags;
        public override void Destroy() => MarkDestroyed();
    }

    private sealed class GlyphTestTextureView(
        Texture texture,
        TextureViewDescription description) : TextureView
    {
        public override Texture Texture => texture;
        public override TextureViewDimension Dimension => description.Dimension;
        public override TextureFormat Format => texture.Format;
        public override uint Width => texture.Width;
        public override uint Height => texture.Height;
        public override uint Depth => texture.Depth;
        public override uint BaseMipLevel => description.BaseMipLevel;
        public override uint MipLevels => description.MipLevels;
        public override uint BaseArrayLayer => description.BaseArrayLayer;
        public override uint ArrayLayers => description.ArrayLayers;
        public override void Destroy() => MarkDestroyed();
    }

    private sealed class GlyphTestTextureRegistry : IGlyphTextureSlotRegistry
    {
        private uint _nextTextureIndex;

        public bool HasFreeSlot { get; set; }

        public bool TryRegister(TextureView view, out uint textureIndex)
        {
            if (!HasFreeSlot)
            {
                textureIndex = 0;
                return false;
            }

            textureIndex = _nextTextureIndex++;
            return true;
        }
    }
}
