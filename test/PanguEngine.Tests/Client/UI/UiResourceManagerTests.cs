using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Rendering;
using PanguEngine.Graphics;
using PanguEngine.Graphics.Text;
using GraphicsBuffer = PanguEngine.Graphics.Buffer;

namespace PanguEngine.Tests.Client.UI;

public sealed class UiResourceManagerTests
{
    [Fact]
    public void FinalizedRegistrationIsDestroyedOnlyWhenOwnerThreadDrains()
    {
        var manager = new UiResourceManager();
        var state = new TestState();
        var registration = CreateUnrootedRegistration(manager, state);

        CollectUntilDead(registration);

        Assert.Equal(0, state.DestroyCount);
        manager.DrainFinalizedResources();
        Assert.Equal(1, state.DestroyCount);
        Assert.Same(Thread.CurrentThread, state.DestroyThread);
        manager.Destroy();
    }

    [Fact]
    public void DuplicateAndUnknownNotificationsAreIgnored()
    {
        var manager = new UiResourceManager();
        var state = new TestState();
        var image = CreateImage();
        var registration = manager.RegisterImage(image, state);

        manager.EnqueueFinalized(registration.Id);
        manager.EnqueueFinalized(registration.Id);
        manager.EnqueueFinalized(ulong.MaxValue);
        manager.DrainFinalizedResources();

        Assert.Equal(1, state.DestroyCount);
        GC.KeepAlive(image);
        GC.KeepAlive(registration);
        manager.Destroy();
    }

    [Fact]
    public void StateOperationsRequireOwnerThread()
    {
        var manager = new UiResourceManager();
        var exceptions = new Exception?[3];
        var thread = new Thread(() =>
        {
            exceptions[0] = Record.Exception(() => manager.RegisterImage(CreateImage(), new TestState()));
            exceptions[1] = Record.Exception(manager.DrainFinalizedResources);
            exceptions[2] = Record.Exception(manager.Destroy);
        });

        thread.Start();
        thread.Join();

        Assert.All(exceptions, exception => Assert.IsType<InvalidOperationException>(exception));
        manager.Destroy();
    }

    [Fact]
    public void DestroyReleasesAllStatesAndIsIdempotent()
    {
        var manager = new UiResourceManager();
        var first = new TestState();
        var second = new TestState();
        var firstImage = CreateImage();
        var secondImage = CreateImage();
        var firstRegistration = manager.RegisterImage(firstImage, first);
        var secondRegistration = manager.RegisterImage(secondImage, second);

        manager.Destroy();
        manager.Destroy();

        Assert.Equal(1, first.DestroyCount);
        Assert.Equal(1, second.DestroyCount);
        GC.KeepAlive(firstImage);
        GC.KeepAlive(secondImage);
        GC.KeepAlive(firstRegistration);
        GC.KeepAlive(secondRegistration);
    }

    [Fact]
    public void DestroyContinuesAfterAStateFails()
    {
        var expected = new InvalidOperationException("destroy failed");
        var manager = new UiResourceManager();
        var failing = new TestState(expected);
        var surviving = new TestState();
        var failingImage = CreateImage();
        var survivingImage = CreateImage();
        var failingRegistration = manager.RegisterImage(failingImage, failing);
        var survivingRegistration = manager.RegisterImage(survivingImage, surviving);

        var exception = Assert.Throws<InvalidOperationException>(manager.Destroy);

        Assert.Same(expected, exception);
        Assert.Equal(1, failing.DestroyCount);
        Assert.Equal(1, surviving.DestroyCount);
        GC.KeepAlive(failingImage);
        GC.KeepAlive(survivingImage);
        GC.KeepAlive(failingRegistration);
        GC.KeepAlive(survivingRegistration);
    }

    [Fact]
    public void LateFinalizerNotificationAfterDestroyDoesNotDestroyTwice()
    {
        var manager = new UiResourceManager();
        var state = new TestState();
        var registration = DestroyWithRegistration(manager, state);

        CollectUntilDead(registration);

        Assert.False(registration.TryGetTarget(out _));
        Assert.Equal(1, state.DestroyCount);
    }

    [Fact]
    public void ClosedManagerRejectsNewStateOperations()
    {
        var manager = new UiResourceManager();
        manager.Destroy();

        Assert.Throws<ObjectDisposedException>(() => manager.RegisterImage(CreateImage(), new TestState()));
        Assert.Throws<ObjectDisposedException>(manager.DrainFinalizedResources);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<UiImageRegistration> CreateUnrootedRegistration(
        UiResourceManager manager,
        TestState state)
    {
        var registration = manager.RegisterImage(CreateImage(), state);
        return new WeakReference<UiImageRegistration>(registration);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<UiImageRegistration> DestroyWithRegistration(
        UiResourceManager manager,
        TestState state)
    {
        var registration = manager.RegisterImage(CreateImage(), state);
        manager.Destroy();
        return new WeakReference<UiImageRegistration>(registration);
    }

    [Fact]
    public void ManagerDoesNotKeepRegisteredImageAlive()
    {
        var manager = new UiResourceManager();
        var state = new TestState();
        var image = RegisterUnrootedImage(manager, state);

        CollectUntilDead(image);
        manager.DrainFinalizedResources();

        Assert.False(image.TryGetTarget(out _));
        Assert.Equal(1, state.DestroyCount);
        manager.Destroy();
    }

    [Fact]
    public void ImageCanBeRegisteredWithTwoManagers()
    {
        var firstManager = new UiResourceManager();
        var secondManager = new UiResourceManager();
        var firstState = new TestState();
        var secondState = new TestState();
        var references = RegisterSharedImage(firstManager, firstState, secondManager, secondState);

        CollectUntilDead(references.Image);
        CollectUntilDead(references.FirstRegistration);
        CollectUntilDead(references.SecondRegistration);
        firstManager.DrainFinalizedResources();
        secondManager.DrainFinalizedResources();

        Assert.False(references.Image.TryGetTarget(out _));
        Assert.Equal(1, firstState.DestroyCount);
        Assert.Equal(1, secondState.DestroyCount);
        firstManager.Destroy();
        secondManager.Destroy();
    }

    [Fact]
    public void ImageIsMaterializedOncePerManager()
    {
        var firstDevice = new ImageTestGraphicsDevice();
        var secondDevice = new ImageTestGraphicsDevice();
        var firstManager = new UiResourceManager(firstDevice);
        var secondManager = new UiResourceManager(secondDevice);
        var image = CreateImage();

        var first = firstManager.ResolveImage(image);
        var firstAgain = firstManager.ResolveImage(image);
        var second = secondManager.ResolveImage(image);

        Assert.Same(first, firstAgain);
        Assert.NotSame(first, second);
        Assert.Equal(1, firstDevice.UploadCount);
        Assert.Equal(1, secondDevice.UploadCount);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, image.Pixels.ToArray());
        firstManager.Destroy();
        secondManager.Destroy();
    }

    [Fact]
    public void RegisteringImageDoesNotDiscardPixels()
    {
        var manager = new UiResourceManager();
        var image = CreateImage();
        var registration = manager.RegisterImage(image, new TestState());

        Assert.Equal(new byte[] { 1, 2, 3, 4 }, image.Pixels.ToArray());

        GC.KeepAlive(registration);
        manager.Destroy();
    }

    [Fact]
    public void SynchronousUploadFailureIsLoggedAndSkippedOnce()
    {
        var expected = new InvalidOperationException("upload failed");
        var device = new ImageTestGraphicsDevice(expected);
        var logger = new CapturingLogger();
        var manager = new UiResourceManager(device, logger: logger);
        var image = CreateImage();
        var command = new UiDrawImageCommand(
            new Rect(0, 0, 1, 1),
            image,
            image.FullSourceRect,
            ImageSamplingMode.Linear,
            null,
            1);

        Assert.Null(manager.ResolveImageBinding(command));
        Assert.Null(manager.ResolveImageBinding(command));

        Assert.Equal(1, device.UploadCount);
        Assert.Equal(1, logger.ErrorCount);
        Assert.Same(expected, logger.LastException);
        manager.Destroy();
    }

    [Fact]
    public void AsynchronousUploadFailureIsLoggedAndSkippedOnce()
    {
        var expected = new InvalidOperationException("upload failed");
        var device = new ImageTestGraphicsDevice(uploadHandle: new ImageTestUploadHandle(expected));
        var logger = new CapturingLogger();
        var manager = new UiResourceManager(device, logger: logger);
        var image = CreateImage();
        var command = new UiDrawImageCommand(
            new Rect(0, 0, 1, 1),
            image,
            image.FullSourceRect,
            ImageSamplingMode.Linear,
            null,
            1);

        Assert.Null(manager.ResolveImageBinding(command));
        Assert.Null(manager.ResolveImageBinding(command));

        Assert.Equal(1, device.UploadCount);
        Assert.Equal(1, logger.ErrorCount);
        Assert.Same(expected, logger.LastException);
        manager.Destroy();
    }

    [Fact]
    public void ManagerWithoutGraphicsDeviceRejectsGlyphResolution()
    {
        using var fonts = new GlyphManagerContext();
        var manager = new UiResourceManager();

        Assert.Throws<InvalidOperationException>(() =>
            manager.ResolveGlyphBinding(fonts.CreateKey("A", 16)));

        manager.Destroy();
    }

    [Fact]
    public void GlyphBindingWaitsForUploadAndReadyCanBecomeFaulted()
    {
        var upload = new MutableImageTestUploadHandle();
        using var fonts = new GlyphManagerContext();
        var device = new ImageTestGraphicsDevice(uploadHandle: upload);
        var logger = new CapturingLogger();
        var manager = new UiResourceManager(
            device,
            fonts.FontManager,
            new ImageTestDescriptorSetLayout(),
            logger);
        var key = fonts.CreateKey("A", 16);

        Assert.Null(manager.ResolveGlyphBinding(key));
        upload.SetReady();
        var ready = Assert.IsType<UiGlyphRenderBinding>(manager.ResolveGlyphBinding(key));
        Assert.NotEqual(0UL, ready.ResourceId);

        var failure = new InvalidOperationException("late glyph upload");
        upload.SetFaulted(failure);
        Assert.Null(manager.ResolveGlyphBinding(key));
        Assert.Null(manager.ResolveGlyphBinding(key));

        Assert.Equal(1, device.UploadCount);
        Assert.Equal(1, logger.ErrorCount);
        Assert.Same(failure, logger.LastException);
        manager.Destroy();
    }

    [Fact]
    public void EmptyGlyphDoesNotCreateAtlasPageOrUpload()
    {
        using var fonts = new GlyphManagerContext();
        var device = new ImageTestGraphicsDevice();
        var manager = new UiResourceManager(
            device,
            fonts.FontManager,
            new ImageTestDescriptorSetLayout());

        Assert.Null(manager.ResolveGlyphBinding(fonts.CreateKey(" ", 16)));

        Assert.Equal(0, device.TextureCount);
        Assert.Equal(0, device.UploadCount);
        manager.Destroy();
    }

    [Fact]
    public void GlyphKeyFromDifferentFontManagerIsRejected()
    {
        using var firstFonts = new GlyphManagerContext();
        using var secondFonts = new GlyphManagerContext();
        var manager = new UiResourceManager(
            new ImageTestGraphicsDevice(),
            secondFonts.FontManager,
            new ImageTestDescriptorSetLayout());

        Assert.Throws<ArgumentException>(() =>
            manager.ResolveGlyphBinding(firstFonts.CreateKey("A", 16)));

        manager.Destroy();
    }

    [Fact]
    public void GlyphResolutionRequiresOwnerThread()
    {
        using var fonts = new GlyphManagerContext();
        var manager = new UiResourceManager(
            new ImageTestGraphicsDevice(),
            fonts.FontManager,
            new ImageTestDescriptorSetLayout());
        var key = fonts.CreateKey("A", 16);
        Exception? exception = null;
        var thread = new Thread(() =>
            exception = Record.Exception(() =>
                manager.ResolveGlyphBinding(key)));

        thread.Start();
        thread.Join();

        Assert.IsType<InvalidOperationException>(exception);
        manager.Destroy();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<UiImage> RegisterUnrootedImage(
        UiResourceManager manager,
        TestState state)
    {
        var image = CreateImage();
        manager.RegisterImage(image, state);
        return new WeakReference<UiImage>(image);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static SharedImageReferences RegisterSharedImage(
        UiResourceManager firstManager,
        TestState firstState,
        UiResourceManager secondManager,
        TestState secondState)
    {
        var image = CreateImage();
        var firstRegistration = firstManager.RegisterImage(image, firstState);
        var secondRegistration = secondManager.RegisterImage(image, secondState);
        return new SharedImageReferences(
            new WeakReference<UiImage>(image),
            new WeakReference<UiImageRegistration>(firstRegistration),
            new WeakReference<UiImageRegistration>(secondRegistration));
    }

    private static UiImage CreateImage() =>
        UiImage.FromRgba(new byte[] { 1, 2, 3, 4 }, 1, 1);

    private sealed class GlyphManagerContext : IDisposable
    {
        private static readonly string FontPath = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "Fonts",
            "SourceHanSansCN-Regular.otf");

        private readonly TextLayoutEngine _layoutEngine;
        private readonly FontFace _face;

        internal GlyphManagerContext()
        {
            FontManager = new FontManager();
            using var stream = File.OpenRead(FontPath);
            var font = Assert.Single(FontManager.Register(stream, 0));
            FontManager.DefaultFont = font;
            _face = FontManager.Match(font);
            _layoutEngine = new TextLayoutEngine(FontManager);
        }

        internal FontManager FontManager { get; }

        internal GlyphRasterKey CreateKey(string text, uint pixelSize)
        {
            var layout = _layoutEngine.Layout(new TextLayoutRequest(
                text,
                _face.Font,
                16,
                double.PositiveInfinity,
                1,
                TextWrapping.NoWrap,
                TextAlignment.Left));
            var glyph = Assert.Single(Assert.Single(layout.Lines).GlyphRuns).Glyphs[0];
            return new GlyphRasterKey(
                _face,
                pixelSize,
                glyph.GlyphId,
                GlyphRasterizationMode.Grayscale);
        }

        public void Dispose() => FontManager.Dispose();
    }

    private static void CollectUntilDead<T>(WeakReference<T> reference) where T : class
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        GC.KeepAlive(reference);
    }

    private readonly record struct SharedImageReferences(
        WeakReference<UiImage> Image,
        WeakReference<UiImageRegistration> FirstRegistration,
        WeakReference<UiImageRegistration> SecondRegistration);

    private sealed class TestState(Exception? destroyException = null) : IUiGpuResourceState
    {
        internal int DestroyCount { get; private set; }
        internal Thread? DestroyThread { get; private set; }

        public void Destroy()
        {
            DestroyCount++;
            DestroyThread = Thread.CurrentThread;
            if (destroyException is not null)
                throw destroyException;
        }
    }

    private sealed class CapturingLogger : ILogger
    {
        internal int ErrorCount { get; private set; }
        internal Exception? LastException { get; private set; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel != LogLevel.Error)
                return;
            ErrorCount++;
            LastException = exception;
        }
    }
}

internal sealed class ImageTestGraphicsDevice(
    Exception? uploadException = null,
    UploadHandle? uploadHandle = null) : GraphicsDevice
{
    internal int UploadCount { get; private set; }
    internal List<ImageTestTexture> Textures { get; } = [];
    internal int TextureCount => Textures.Count;

    public override uint MaxTextureDimension2D => 4096;
    public override uint MaxDrawIndirectCount => 1;

    public override GraphicsBuffer CreateBuffer(in BufferDescription description) =>
        throw new NotSupportedException();

    public override UploadHandle UploadBuffer<T>(
        GraphicsBuffer destination,
        ReadOnlySpan<T> data,
        ulong destinationOffset = 0) => throw new NotSupportedException();

    public override Texture CreateTexture(in TextureDescription description)
    {
        var texture = new ImageTestTexture(description);
        Textures.Add(texture);
        return texture;
    }

    public override TextureView CreateTextureView(
        Texture texture,
        in TextureViewDescription description) =>
        new ImageTestTextureView(texture, description);

    public override UploadHandle UploadTexture(Texture destination, ReadOnlySpan<byte> data)
    {
        UploadCount++;
        if (uploadException is not null)
            throw uploadException;
        return uploadHandle ?? ImageTestUploadHandle.Ready;
    }

    public override UploadHandle UploadTexture(
        Texture destination,
        ReadOnlySpan<byte> data,
        in TextureUploadRegion region)
    {
        UploadCount++;
        if (uploadException is not null)
            throw uploadException;
        return uploadHandle ?? ImageTestUploadHandle.Ready;
    }

    public override UploadHandle GenerateMipmaps(Texture texture) => throw new NotSupportedException();
    public override Sampler CreateSampler(in SamplerDescription description) => new ImageTestSampler();
    public override Shader CreateShader(in ShaderDescription description) => throw new NotSupportedException();

    public override DescriptorSetLayout CreateDescriptorSetLayout(
        in DescriptorSetLayoutDescription description) => throw new NotSupportedException();

    public override DescriptorSet CreateDescriptorSet(
        in DescriptorSetDescription description) => new ImageTestDescriptorSet();

    public override ulong GetAlignedUniformSize(ulong rawSize) => throw new NotSupportedException();

    public override GraphicsPipeline CreateGraphicsPipeline(
        in GraphicsPipelineDescription description) => throw new NotSupportedException();

    public override void WaitIdle() => throw new NotSupportedException();
}

internal sealed class ImageTestTexture(TextureDescription description) : Texture
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

    public override void Destroy()
    {
        if (!IsDestroyed)
            MarkDestroyed();
    }
}

internal sealed class ImageTestTextureView(
    Texture texture,
    TextureViewDescription description) : TextureView
{
    public override Texture Texture { get; } = texture;
    public override TextureViewDimension Dimension => description.Dimension;
    public override TextureFormat Format => texture.Format;
    public override uint Width => texture.Width;
    public override uint Height => texture.Height;
    public override uint Depth => texture.Depth;
    public override uint BaseMipLevel => description.BaseMipLevel;
    public override uint MipLevels => description.MipLevels;
    public override uint BaseArrayLayer => description.BaseArrayLayer;
    public override uint ArrayLayers => description.ArrayLayers;

    public override void Destroy()
    {
        if (!IsDestroyed)
            MarkDestroyed();
    }
}

internal sealed class ImageTestUploadHandle : UploadHandle
{
    internal static readonly ImageTestUploadHandle Ready = new();

    private readonly Exception? _exception;

    internal ImageTestUploadHandle(Exception? exception = null)
    {
        _exception = exception;
    }

    protected override UploadState State =>
        _exception is null ? UploadState.Ready : UploadState.Faulted;

    public override Exception? Exception => _exception;
}

internal sealed class MutableImageTestUploadHandle : UploadHandle
{
    private UploadState _state;
    private Exception? _exception;

    protected override UploadState State => _state;
    public override Exception? Exception => _exception;

    internal void SetReady() => _state = UploadState.Ready;

    internal void SetFaulted(Exception exception)
    {
        _exception = exception;
        _state = UploadState.Faulted;
    }
}

internal sealed class ImageTestSampler : Sampler
{
    public override void Destroy() => MarkDestroyed();
}

internal sealed class ImageTestDescriptorSetLayout : DescriptorSetLayout
{
    public override void Destroy() => MarkDestroyed();
}

internal sealed class ImageTestDescriptorSet : DescriptorSet
{
    public override void Destroy() => MarkDestroyed();
}
