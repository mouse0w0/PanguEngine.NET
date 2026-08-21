using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Rendering;
using PanguEngine.Graphics.Text;

namespace PanguEngine.Tests.Client.UI;

public sealed class UiResourceManagerTests
{
    [Fact]
    public void FinalizedRegistrationIsRetiredOnlyWhenOwnerThreadDrains()
    {
        var manager = new UiResourceManager();
        var state = new TestState();
        var registration = CreateUnrootedRegistration(manager, state);

        CollectUntilDead(registration);
        Assert.Equal(0, state.RetireCount);

        manager.DrainFinalizedResources();

        Assert.Equal(1, state.RetireCount);
        Assert.Same(Thread.CurrentThread, state.RetireThread);
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

        Assert.Equal(1, state.RetireCount);
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
    public void DestroyReleasesAllLiveStatesAndIsIdempotent()
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
    public void ClosedManagerRejectsNewStateOperations()
    {
        var manager = new UiResourceManager();
        manager.Destroy();

        Assert.Throws<ObjectDisposedException>(() => manager.RegisterImage(CreateImage(), new TestState()));
        Assert.Throws<ObjectDisposedException>(manager.DrainFinalizedResources);
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
        Assert.Equal(1, state.RetireCount);
        manager.Destroy();
    }

    [Fact]
    public void ImageCanBeRegisteredWithTwoManagers()
    {
        var firstManager = new UiResourceManager();
        var secondManager = new UiResourceManager();
        var firstState = new TestState();
        var secondState = new TestState();
        var image = CreateImage();
        var firstRegistration = firstManager.RegisterImage(image, firstState);
        var secondRegistration = secondManager.RegisterImage(image, secondState);

        firstManager.EnqueueFinalized(firstRegistration.Id);
        secondManager.EnqueueFinalized(secondRegistration.Id);
        firstManager.DrainFinalizedResources();
        secondManager.DrainFinalizedResources();

        Assert.Equal(1, firstState.RetireCount);
        Assert.Equal(1, secondState.RetireCount);
        GC.KeepAlive(image);
        firstManager.Destroy();
        secondManager.Destroy();
    }

    [Fact]
    public void SmallImagesAreMaterializedOncePerManagerAndShareAtlasPage()
    {
        var firstDevice = new UiTestGraphicsDevice();
        var secondDevice = new UiTestGraphicsDevice();
        var firstManager = CreateManager(firstDevice);
        var secondManager = CreateManager(secondDevice);
        var firstImage = CreateImage();
        var secondImage = CreateImage();

        var first = Assert.IsType<UiImageRenderBinding>(firstManager.ResolveImageBinding(Command(firstImage)));
        var firstAgain = Assert.IsType<UiImageRenderBinding>(firstManager.ResolveImageBinding(Command(firstImage)));
        var sharedPage = Assert.IsType<UiImageRenderBinding>(firstManager.ResolveImageBinding(Command(secondImage)));
        var secondManagerBinding = Assert.IsType<UiImageRenderBinding>(
            secondManager.ResolveImageBinding(Command(firstImage)));

        Assert.Equal(first, firstAgain);
        Assert.Equal(first.TextureIndex, sharedPage.TextureIndex);
        Assert.Equal(2, firstDevice.Uploads.Count(upload => upload.Region is not null));
        Assert.Equal(1, secondDevice.Uploads.Count(upload => upload.Region is not null));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, firstImage.Pixels.ToArray());
        Assert.Equal(0u, secondManagerBinding.TextureIndex);
        firstManager.Destroy();
        secondManager.Destroy();
    }

    [Fact]
    public void PrepareFrameSynchronizesResourcesWithoutRequiringDrawGeometry()
    {
        var device = new UiTestGraphicsDevice();
        var manager = CreateManager(device);
        var image = CreateImage();
        _ = Assert.IsType<UiImageRenderBinding>(manager.ResolveImageBinding(Command(image)));

        manager.PrepareFrame(0);

        var update = Assert.Single(Assert.Single(device.DescriptorSets[0].Updates));
        Assert.Equal((0u, 0u), (update.Binding, update.ArrayElement));
        GC.KeepAlive(image);
        manager.Destroy();
    }

    [Fact]
    public void OversizedImageUsesStandaloneTextureRegion()
    {
        var device = new UiTestGraphicsDevice();
        var manager = CreateManager(device);
        var image = UiImage.FromRgba(new byte[1025 * 4], 1025, 1);

        var binding = Assert.IsType<UiImageRenderBinding>(manager.ResolveImageBinding(Command(image)));

        Assert.Equal((1025u, 1u), (binding.TextureWidth, binding.TextureHeight));
        Assert.Equal(new UiImageAtlasRegion(0, 0, 1025, 1), binding.Region);
        Assert.Single(device.Uploads.Where(upload => upload.Region is null).Skip(1));
        manager.Destroy();
    }

    [Fact]
    public void SynchronousUploadFailureIsLoggedAndRetiredOnce()
    {
        var device = new UiTestGraphicsDevice();
        var logger = new CapturingLogger();
        var manager = CreateManager(device, logger);
        var expected = new InvalidOperationException("upload failed");
        device.UploadException = expected;
        var command = Command(CreateImage());

        Assert.Null(manager.ResolveImageBinding(command));
        Assert.Null(manager.ResolveImageBinding(command));

        Assert.Equal(1, logger.ErrorCount);
        Assert.Same(expected, logger.LastException);
        manager.SynchronizeAfterBuild(0);
        manager.SynchronizeAfterBuild(1);
        manager.Destroy();
    }

    [Fact]
    public void ReadyImageUploadCanLaterFaultAndLogOnce()
    {
        var device = new UiTestGraphicsDevice();
        var logger = new CapturingLogger();
        var manager = CreateManager(device, logger);
        var upload = UiTestUploadHandle.Ready();
        device.UploadHandle = upload;
        var command = Command(CreateImage());

        Assert.NotNull(manager.ResolveImageBinding(command));
        var failure = new InvalidOperationException("late");
        upload.SetFaulted(failure);
        Assert.Null(manager.ResolveImageBinding(command));
        Assert.Null(manager.ResolveImageBinding(command));

        Assert.Equal(1, logger.ErrorCount);
        Assert.Same(failure, logger.LastException);
        manager.Destroy();
    }

    [Fact]
    public void PendingImageUploadPublishesWhenItBecomesReady()
    {
        var device = new UiTestGraphicsDevice();
        var manager = CreateManager(device);
        var upload = new UiTestUploadHandle();
        device.UploadHandle = upload;
        var image = CreateImage();
        var command = Command(image);

        Assert.Null(manager.ResolveImageBinding(command));
        manager.SynchronizeAfterBuild(0);
        Assert.Empty(device.DescriptorSets[0].Updates);

        upload.SetReady();
        Assert.NotNull(manager.ResolveImageBinding(command));
        manager.SynchronizeAfterBuild(0);

        var update = Assert.Single(Assert.Single(device.DescriptorSets[0].Updates));
        Assert.Equal((0u, 0u), (update.Binding, update.ArrayElement));

        GC.KeepAlive(image);
        manager.Destroy();
    }

    [Fact]
    public void ManagerWithoutFontManagerRejectsGlyphResolution()
    {
        using var fonts = new GlyphManagerContext();
        var manager = CreateManager(new UiTestGraphicsDevice());

        Assert.Throws<InvalidOperationException>(() =>
            manager.ResolveGlyphBinding(fonts.CreateKey("A", 16)));

        manager.Destroy();
    }

    [Fact]
    public void PendingGlyphPublishesThenObservesLateUploadFault()
    {
        using var fonts = new GlyphManagerContext();
        var device = new UiTestGraphicsDevice();
        var logger = new CapturingLogger();
        var manager = CreateManager(device, fonts.FontManager, logger);
        var upload = new UiTestUploadHandle();
        device.UploadHandle = upload;
        var key = fonts.CreateKey("A", 16);

        Assert.Null(manager.ResolveGlyphBinding(key));
        manager.SynchronizeAfterBuild(0);
        Assert.Empty(device.DescriptorSets[0].Updates);

        upload.SetReady();
        var ready = Assert.IsType<UiGlyphRenderBinding>(manager.ResolveGlyphBinding(key));
        Assert.Equal(0u, ready.TextureIndex);
        manager.SynchronizeAfterBuild(0);

        var update = Assert.Single(Assert.Single(device.DescriptorSets[0].Updates));
        Assert.Equal((0u, ready.TextureIndex), (update.Binding, update.ArrayElement));

        var failure = new InvalidOperationException("glyph late fault");
        upload.SetFaulted(failure);
        Assert.Null(manager.ResolveGlyphBinding(key));
        Assert.Null(manager.ResolveGlyphBinding(key));

        Assert.Equal(1, logger.ErrorCount);
        Assert.Same(failure, logger.LastException);
        manager.Destroy();
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
    private static WeakReference<UiImage> RegisterUnrootedImage(
        UiResourceManager manager,
        TestState state)
    {
        var image = CreateImage();
        manager.RegisterImage(image, state);
        return new WeakReference<UiImage>(image);
    }

    private static UiResourceManager CreateManager(
        UiTestGraphicsDevice device,
        ILogger? logger = null) =>
        new(device, new UiTestDescriptorSetLayout(default), 2, logger);

    private static UiResourceManager CreateManager(
        UiTestGraphicsDevice device,
        FontManager fontManager,
        ILogger? logger = null) =>
        new(device, fontManager, new UiTestDescriptorSetLayout(default), 2, logger);

    private static UiImage CreateImage() =>
        UiImage.FromRgba(new byte[] { 1, 2, 3, 4 }, 1, 1);

    private static UiDrawImageCommand Command(UiImage image) =>
        new(
            new Rect(0, 0, image.PixelWidth, image.PixelHeight),
            image,
            image.FullSourceRect,
            ImageSamplingMode.Linear,
            null,
            1);

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

    private sealed class TestState(Exception? destroyException = null) : IUiGpuResourceState
    {
        internal int RetireCount { get; private set; }
        internal int DestroyCount { get; private set; }
        internal Thread? RetireThread { get; private set; }

        public void Retire()
        {
            RetireCount++;
            RetireThread = Thread.CurrentThread;
        }

        public void Destroy()
        {
            DestroyCount++;
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

        public void Dispose() => FontManager.Destroy();
    }
}
