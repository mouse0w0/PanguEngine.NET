using System.Runtime.ExceptionServices;
using PanguEngine.Graphics;

namespace PanguEngine.Client.UI.Rendering;

internal sealed class UiImageAtlas
{
    private const uint PageSize = 1024;

    private readonly GraphicsDevice _graphicsDevice;
    private readonly UiTextureTable _textureTable;
    private readonly List<UiImageAtlasPage> _pages = [];
    private readonly List<RetiringRegion> _retiringRegions = [];
    private ulong _nextPageId;
    private bool _destroyed;

    internal UiImageAtlas(GraphicsDevice graphicsDevice, UiTextureTable textureTable)
    {
        _graphicsDevice = graphicsDevice;
        _textureTable = textureTable;
    }

    internal UiImageAtlasEntry? TryCreate(UiImage image)
    {
        ObjectDisposedException.ThrowIf(_destroyed, this);
        var width = checked((uint)image.PixelWidth);
        var height = checked((uint)image.PixelHeight);
        foreach (var page in _pages)
        {
            if (page.TryAllocate(width, height, out var region))
                return Upload(image, page, region);
        }

        if (!_textureTable.HasFreeSlot)
            return null;

        var newPage = CreatePage();
        if (newPage is null)
            return null;
        if (!newPage.TryAllocate(width, height, out var newRegion))
            throw new InvalidOperationException("A new UI image atlas page could not fit an admitted image.");
        return Upload(image, newPage, newRegion);
    }

    internal void Retire(UiImageAtlasEntry entry)
    {
        if (entry.IsRetired)
            return;
        entry.IsRetired = true;
        entry.Page.ActiveRegionCount--;
        entry.Page.RetiringRegionCount++;
        _retiringRegions.Add(new RetiringRegion(
            entry,
            new bool[_textureTable.FrameSlotCount]));
        Array.Fill(_retiringRegions[^1].PendingFrames, true);
    }

    internal void AdvanceFrame(uint frameSlot)
    {
        var frameIndex = checked((int)frameSlot);
        for (var index = _retiringRegions.Count - 1; index >= 0; index--)
        {
            var retirement = _retiringRegions[index];
            retirement.PendingFrames[frameIndex] = false;
            if (retirement.PendingFrames.Contains(true) ||
                retirement.Entry.Upload is { IsCompleted: false })
            {
                continue;
            }

            var page = retirement.Entry.Page;
            page.Free(retirement.Entry.Region);
            page.RetiringRegionCount--;
            _retiringRegions.RemoveAt(index);
        }

        RetireExtraEmptyPages();
    }

    internal void Destroy()
    {
        if (_destroyed)
            return;
        _destroyed = true;

        var errors = new List<Exception>();
        for (var index = _pages.Count - 1; index >= 0; index--)
        {
            try
            {
                _pages[index].Destroy();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
        }
        _pages.Clear();
        _retiringRegions.Clear();
        if (errors.Count != 0)
            ExceptionDispatchInfo.Capture(errors[0]).Throw();
    }

    private UiImageAtlasPage? CreatePage()
    {
        Texture? texture = null;
        TextureView? view = null;
        try
        {
            texture = _graphicsDevice.CreateTexture(new TextureDescription
            {
                Dimension = TextureDimension.Type2D,
                Format = TextureFormat.R8G8B8A8Srgb,
                Width = PageSize,
                Height = PageSize,
                Depth = 1,
                MipLevels = 1,
                ArrayLayers = 1,
                Usage = TextureUsage.Sampled | TextureUsage.TransferDestination
            });
            view = _graphicsDevice.CreateTextureView(
                texture,
                new TextureViewDescription(TextureViewDimension.Type2D, 0, 1, 0, 1));
            if (!_textureTable.TryRegister(view, out var textureSlot))
            {
                view.Destroy();
                texture.Destroy();
                return null;
            }

            var page = new UiImageAtlasPage(++_nextPageId, texture, view, textureSlot);
            _pages.Add(page);
            return page;
        }
        catch
        {
            view?.Destroy();
            texture?.Destroy();
            throw;
        }
    }

    private UiImageAtlasEntry Upload(
        UiImage image,
        UiImageAtlasPage page,
        UiImageAtlasRegion region)
    {
        page.ActiveRegionCount++;
        UploadHandle? upload = null;
        Exception? uploadFailure = null;
        try
        {
            upload = _graphicsDevice.UploadTexture(
                page.Texture,
                image.Pixels.Span,
                TextureUploadRegion.Region2D(region.X, region.Y, region.Width, region.Height));
        }
        catch (Exception exception)
        {
            uploadFailure = exception;
        }
        return new UiImageAtlasEntry(page, region, upload, uploadFailure);
    }

    private void RetireExtraEmptyPages()
    {
        var emptyPages = _pages
            .Where(page => page.ActiveRegionCount == 0 && page.RetiringRegionCount == 0)
            .OrderBy(page => page.Id)
            .ToArray();
        for (var index = 1; index < emptyPages.Length; index++)
        {
            var page = emptyPages[index];
            _pages.Remove(page);
            _textureTable.Retire(page.TextureSlot, page.Destroy);
        }
    }

    private sealed class RetiringRegion(UiImageAtlasEntry entry, bool[] pendingFrames)
    {
        internal UiImageAtlasEntry Entry { get; } = entry;
        internal bool[] PendingFrames { get; } = pendingFrames;
    }
}

internal sealed class UiImageAtlasPage(
    ulong id,
    Texture texture,
    TextureView textureView,
    UiTextureSlot textureSlot)
{
    private readonly UiImageAtlasAllocator _allocator = new(1024, 1024);
    private bool _destroyed;

    internal ulong Id { get; } = id;
    internal Texture Texture { get; } = texture;
    internal TextureView TextureView { get; } = textureView;
    internal UiTextureSlot TextureSlot { get; } = textureSlot;
    internal int ActiveRegionCount { get; set; }
    internal int RetiringRegionCount { get; set; }

    internal bool TryAllocate(uint width, uint height, out UiImageAtlasRegion region) =>
        _allocator.TryAllocate(width, height, out region);

    internal void Free(UiImageAtlasRegion region) => _allocator.Free(region);

    internal void Destroy()
    {
        if (_destroyed)
            return;
        _destroyed = true;

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

internal sealed class UiImageAtlasEntry(
    UiImageAtlasPage page,
    UiImageAtlasRegion region,
    UploadHandle? upload,
    Exception? uploadFailure)
{
    private Exception? _uploadFailure = uploadFailure;
    private bool _uploadFailureObserved;

    internal UiImageAtlasPage Page { get; } = page;
    internal UiImageAtlasRegion Region { get; } = region;
    internal UploadHandle? Upload { get; } = upload;
    internal bool IsRetired { get; set; }
    internal bool IsUploadReady => _uploadFailure is null && Upload?.IsReady == true;

    internal bool TryObserveUploadFailure(out Exception? failure, out bool firstObservation)
    {
        if (_uploadFailure is null && Upload?.IsFaulted == true)
        {
            _uploadFailure = Upload.Exception ?? new InvalidOperationException(
                "The UI image atlas upload faulted without reporting an exception.");
        }

        failure = _uploadFailure;
        firstObservation = failure is not null && !_uploadFailureObserved;
        if (firstObservation)
            _uploadFailureObserved = true;
        return failure is not null;
    }
}
