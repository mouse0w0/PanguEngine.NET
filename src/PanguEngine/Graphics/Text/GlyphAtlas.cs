using System.Runtime.ExceptionServices;

namespace PanguEngine.Graphics.Text;

internal interface IGlyphTextureSlotRegistry
{
    bool HasFreeSlot { get; }
    bool TryRegister(TextureView view, out uint textureIndex);
}

internal sealed class GlyphAtlas : GraphicsResource
{
    private const uint PageSize = 1024;
    private const string CleanupFailuresDataKey = "GlyphAtlas.CleanupFailures";

    private readonly GraphicsDevice _graphicsDevice;
    private readonly FontManager _fontManager;
    private readonly IGlyphTextureSlotRegistry _textureRegistry;
    private readonly Thread _ownerThread = Thread.CurrentThread;
    private readonly Dictionary<GlyphRasterKey, GlyphAtlasEntry> _entries = [];
    private readonly Dictionary<GlyphRasterKey, GlyphBitmap> _allocationPending = [];
    private readonly List<GlyphAtlasPage> _pages = [];
    private ulong _nextPageId;

    internal GlyphAtlas(
        GraphicsDevice graphicsDevice,
        FontManager fontManager,
        IGlyphTextureSlotRegistry textureRegistry)
    {
        _graphicsDevice = graphicsDevice;
        _fontManager = fontManager;
        _textureRegistry = textureRegistry;
    }

    internal GlyphAtlasEntry? Resolve(in GlyphRasterKey key)
    {
        VerifyOwnerThread();
        ObjectDisposedException.ThrowIf(IsDestroyed, this);
        if (_entries.TryGetValue(key, out var cached))
            return cached;

        if (!_allocationPending.TryGetValue(key, out var bitmap))
        {
            bitmap = _fontManager.Rasterize(
                key.FontFace,
                key.PixelSize,
                key.GlyphId,
                key.Mode);
            if (bitmap.Width == 0 || bitmap.Height == 0)
            {
                var empty = GlyphAtlasEntry.Empty(bitmap);
                _entries.Add(key, empty);
                return empty;
            }
        }

        if (!TryAllocatePageRegion(bitmap, out var page, out var region))
        {
            _allocationPending[key] = bitmap;
            return null;
        }

        var uploadRegion = TextureUploadRegion.Region2D(
            region.X - 1,
            region.Y - 1,
            region.Width + 2,
            region.Height + 2);
        var uploadData = AddTransparentPadding(bitmap);
        UploadHandle? upload = null;
        Exception? uploadFailure = null;
        try
        {
            upload = _graphicsDevice.UploadTexture(page.Texture, uploadData, uploadRegion);
        }
        catch (Exception exception)
        {
            uploadFailure = exception;
        }

        var entry = new GlyphAtlasEntry(bitmap, page, region, upload, uploadFailure);
        _allocationPending.Remove(key);
        _entries.Add(key, entry);
        return entry;
    }

    public override void Destroy()
    {
        VerifyOwnerThread();
        if (IsDestroyed)
            return;
        MarkDestroyed();

        Exception? firstFailure = null;
        for (var index = _pages.Count - 1; index >= 0; index--)
        {
            try
            {
                _pages[index].Destroy();
            }
            catch (Exception exception)
            {
                firstFailure ??= exception;
            }
        }

        _entries.Clear();
        _allocationPending.Clear();
        _pages.Clear();
        if (firstFailure is not null)
            ExceptionDispatchInfo.Capture(firstFailure).Throw();
    }

    private bool TryAllocatePageRegion(
        GlyphBitmap bitmap,
        out GlyphAtlasPage page,
        out GlyphAtlasRegion region)
    {
        var paddedWidth = checked((uint)bitmap.Width + 2);
        var paddedHeight = checked((uint)bitmap.Height + 2);
        var dedicated = paddedWidth > PageSize || paddedHeight > PageSize;

        if (!dedicated)
        {
            foreach (var existing in _pages)
            {
                if (!existing.IsDedicated && existing.TryAllocate(
                        (uint)bitmap.Width,
                        (uint)bitmap.Height,
                        out region))
                {
                    page = existing;
                    return true;
                }
            }
        }

        if (!_textureRegistry.HasFreeSlot)
        {
            page = null!;
            region = default;
            return false;
        }

        page = CreatePage(
            dedicated ? paddedWidth : PageSize,
            dedicated ? paddedHeight : PageSize,
            dedicated);
        if (!page.TryAllocate((uint)bitmap.Width, (uint)bitmap.Height, out region))
            throw new InvalidOperationException("A newly created glyph atlas page could not fit its glyph.");
        return true;
    }

    private GlyphAtlasPage CreatePage(uint width, uint height, bool dedicated)
    {
        Texture? texture = null;
        TextureView? view = null;
        try
        {
            texture = _graphicsDevice.CreateTexture(new TextureDescription
            {
                Dimension = TextureDimension.Type2D,
                Format = TextureFormat.R8Unorm,
                Width = width,
                Height = height,
                Depth = 1,
                MipLevels = 1,
                ArrayLayers = 1,
                Usage = TextureUsage.Sampled | TextureUsage.TransferDestination
            });
            view = _graphicsDevice.CreateTextureView(
                texture,
                new TextureViewDescription(TextureViewDimension.Type2D, 0, 1, 0, 1));
            if (!_textureRegistry.TryRegister(view, out var textureIndex))
                throw new InvalidOperationException("The UI texture table had no slot for a glyph atlas page.");

            var page = new GlyphAtlasPage(
                ++_nextPageId,
                width,
                height,
                dedicated,
                texture,
                view,
                textureIndex);
            _pages.Add(page);
            return page;
        }
        catch (Exception exception)
        {
            var cleanupFailures = new List<Exception>();
            TryDestroy(view, cleanupFailures);
            TryDestroy(texture, cleanupFailures);
            if (cleanupFailures.Count != 0)
                exception.Data[CleanupFailuresDataKey] = cleanupFailures.ToArray();
            throw;
        }
    }

    private static byte[] AddTransparentPadding(GlyphBitmap bitmap)
    {
        var paddedWidth = checked(bitmap.Width + 2);
        var padded = new byte[checked(paddedWidth * (bitmap.Height + 2))];
        for (var row = 0; row < bitmap.Height; row++)
        {
            bitmap.Pixels.Span.Slice(row * bitmap.Width, bitmap.Width).CopyTo(
                padded.AsSpan((row + 1) * paddedWidth + 1, bitmap.Width));
        }
        return padded;
    }

    private void VerifyOwnerThread()
    {
        if (Thread.CurrentThread != _ownerThread)
            throw new InvalidOperationException("Glyph atlas access must remain on its owner thread.");
    }

    private static void TryDestroy(GraphicsResource? resource, List<Exception> cleanupFailures)
    {
        try
        {
            resource?.Destroy();
        }
        catch (Exception exception)
        {
            cleanupFailures.Add(exception);
        }
    }
}

internal sealed class GlyphAtlasPage(
    ulong id,
    uint width,
    uint height,
    bool isDedicated,
    Texture texture,
    TextureView textureView,
    uint textureIndex) : GraphicsResource
{
    private readonly GlyphAtlasAllocator _allocator = new(width, height);

    internal ulong Id { get; } = id;
    internal uint Width { get; } = width;
    internal uint Height { get; } = height;
    internal bool IsDedicated { get; } = isDedicated;
    internal Texture Texture { get; } = texture;
    internal TextureView TextureView { get; } = textureView;
    internal uint TextureIndex { get; } = textureIndex;

    internal bool TryAllocate(uint width, uint height, out GlyphAtlasRegion region) =>
        _allocator.TryAllocate(width, height, out region);

    public override void Destroy()
    {
        if (IsDestroyed)
            return;
        MarkDestroyed();

        Exception? firstFailure = null;
        try
        {
            TextureView.Destroy();
        }
        catch (Exception exception)
        {
            firstFailure = exception;
        }
        try
        {
            Texture.Destroy();
        }
        catch (Exception exception)
        {
            firstFailure ??= exception;
        }
        if (firstFailure is not null)
            ExceptionDispatchInfo.Capture(firstFailure).Throw();
    }
}

internal sealed class GlyphAtlasEntry
{
    private readonly UploadHandle? _upload;
    private Exception? _uploadFailure;
    private bool _failureObserved;

    internal GlyphAtlasEntry(
        GlyphBitmap bitmap,
        GlyphAtlasPage page,
        GlyphAtlasRegion region,
        UploadHandle? upload,
        Exception? uploadFailure)
    {
        BearingX = bitmap.Left;
        BearingY = bitmap.Top;
        Page = page;
        Region = region;
        _upload = upload;
        _uploadFailure = uploadFailure;
    }

    private GlyphAtlasEntry(GlyphBitmap bitmap)
    {
        BearingX = bitmap.Left;
        BearingY = bitmap.Top;
        IsEmpty = true;
    }

    internal int BearingX { get; }
    internal int BearingY { get; }
    internal GlyphAtlasPage? Page { get; }
    internal GlyphAtlasRegion Region { get; }
    internal bool IsEmpty { get; }
    internal bool IsUploadReady =>
        _uploadFailure is null &&
        _upload is not null &&
        !_upload.IsFaulted &&
        _upload.IsReady;

    internal static GlyphAtlasEntry Empty(GlyphBitmap bitmap) => new(bitmap);

    internal bool TryObserveUploadFailure(
        out Exception? failure,
        out bool firstObservation)
    {
        if (_uploadFailure is null && _upload?.IsFaulted == true)
        {
            _uploadFailure = _upload.Exception ?? new InvalidOperationException(
                "The glyph texture upload faulted without reporting an exception.");
        }

        failure = _uploadFailure;
        if (failure is null)
        {
            firstObservation = false;
            return false;
        }

        firstObservation = !_failureObserved;
        _failureObserved = true;
        return true;
    }
}
