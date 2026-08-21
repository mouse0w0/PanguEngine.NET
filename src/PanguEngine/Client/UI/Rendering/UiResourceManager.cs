using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PanguEngine.Graphics;
using PanguEngine.Graphics.Text;

namespace PanguEngine.Client.UI.Rendering;

internal interface IUiGpuResourceState
{
    void Retire();
    void Destroy();
}

internal sealed class UiImageRegistration
{
    private readonly UiResourceManager _manager;

    internal UiImageRegistration(UiResourceManager manager, ulong id)
    {
        _manager = manager;
        Id = id;
    }

    internal ulong Id { get; }

    ~UiImageRegistration()
    {
        _manager.EnqueueFinalized(Id);
    }
}

internal sealed class UiResourceManager
{
    private static readonly Action<ILogger, ulong, Exception?> LogImageUploadFailure = LoggerMessage.Define<ulong>(
        LogLevel.Error,
        new EventId(1, nameof(LogImageUploadFailure)),
        "UI image resource {ResourceId} upload failed; subsequent draws will be skipped");
    private static readonly Action<ILogger, uint, Exception?> LogGlyphUploadFailure = LoggerMessage.Define<uint>(
        LogLevel.Error,
        new EventId(2, nameof(LogGlyphUploadFailure)),
        "UI glyph {GlyphId} upload failed; subsequent draws will be skipped");

    private readonly GraphicsDevice? _device;
    private readonly FontManager? _fontManager;
    private readonly ILogger _logger;
    private readonly Thread _ownerThread = Thread.CurrentThread;
    private readonly Dictionary<ulong, IUiGpuResourceState> _states = [];
    private readonly ConditionalWeakTable<UiImage, UiImageRegistration> _imageRegistrations = new();
    private readonly ConcurrentQueue<ulong> _finalizedIds = new();
    private readonly UiTextureTable? _textureTable;
    private readonly UiImageAtlas? _imageAtlas;
    private GlyphAtlas? _glyphAtlas;
    private ulong _nextId;
    private int _accepting = 1;
    private int _inFlightEnqueues;
    private bool _destroyed;

    internal UiResourceManager()
    {
        _logger = NullLogger.Instance;
    }

    internal UiResourceManager(
        GraphicsDevice device,
        DescriptorSetLayout descriptorSetLayout,
        uint frameSlotCount,
        ILogger? logger = null)
        : this(device, descriptorSetLayout, frameSlotCount, logger, null)
    {
    }

    internal UiResourceManager(
        GraphicsDevice device,
        FontManager fontManager,
        DescriptorSetLayout descriptorSetLayout,
        uint frameSlotCount,
        ILogger? logger = null)
        : this(device, descriptorSetLayout, frameSlotCount, logger, VerifyFontManager(fontManager))
    {
    }

    private UiResourceManager(
        GraphicsDevice device,
        DescriptorSetLayout descriptorSetLayout,
        uint frameSlotCount,
        ILogger? logger,
        FontManager? fontManager)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(descriptorSetLayout);
        _device = device;
        _fontManager = fontManager;
        _logger = logger ?? NullLogger.Instance;
        _textureTable = new UiTextureTable(device, descriptorSetLayout, frameSlotCount);
        _imageAtlas = new UiImageAtlas(device, _textureTable);
    }

    private static FontManager VerifyFontManager(FontManager fontManager)
    {
        ArgumentNullException.ThrowIfNull(fontManager);
        fontManager.VerifyServiceAccess();
        return fontManager;
    }

    internal UiImageRegistration RegisterImage(UiImage image, IUiGpuResourceState state)
    {
        VerifyOwnerThread();
        ObjectDisposedException.ThrowIf(_destroyed, this);
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(state);
        if (_imageRegistrations.TryGetValue(image, out _))
            throw new InvalidOperationException("The UI image is already registered with this resource manager.");

        var id = checked(++_nextId);
        var registration = new UiImageRegistration(this, id);
        _states.Add(id, state);
        try
        {
            _imageRegistrations.Add(image, registration);
            return registration;
        }
        catch
        {
            _states.Remove(id);
            throw;
        }
    }

    internal void EnqueueFinalized(ulong id)
    {
        Interlocked.Increment(ref _inFlightEnqueues);
        try
        {
            if (Volatile.Read(ref _accepting) != 0)
                _finalizedIds.Enqueue(id);
        }
        finally
        {
            Interlocked.Decrement(ref _inFlightEnqueues);
        }
    }

    internal UiImageRenderBinding? ResolveImageBinding(UiDrawImageCommand command)
    {
        VerifyOwnerThread();
        ObjectDisposedException.ThrowIf(_destroyed, this);
        ArgumentNullException.ThrowIfNull(command);

        var state = ResolveImageState(command.Image);
        if (state is null)
            return null;
        if (state.TryObserveUploadFailure(out var failure, out var firstObservation))
        {
            if (firstObservation)
                LogImageUploadFailure(_logger, state.ResourceId, failure);
            state.Retire();
            return null;
        }
        if (!state.IsUploadReady)
            return null;
        state.Publish();
        return state.Binding;
    }

    internal UiGlyphRenderBinding? ResolveGlyphBinding(GlyphRasterKey key)
    {
        VerifyOwnerThread();
        ObjectDisposedException.ThrowIf(_destroyed, this);
        var entry = GetGlyphAtlas().Resolve(key);
        if (entry is null || entry.IsEmpty)
            return null;
        if (entry.TryObserveUploadFailure(out var uploadFailure, out var firstObservation))
        {
            if (firstObservation)
                LogGlyphUploadFailure(_logger, key.GlyphId, uploadFailure);
            return null;
        }
        if (!entry.IsUploadReady)
            return null;

        var page = entry.Page!;
        _textureTable!.Publish(page.TextureIndex);
        return new UiGlyphRenderBinding(
            page.TextureIndex,
            page.Width,
            page.Height,
            entry.Region,
            entry.BearingX,
            entry.BearingY);
    }

    internal void PrepareFrame(uint frameSlot)
    {
        VerifyOwnerThread();
        ObjectDisposedException.ThrowIf(_destroyed, this);
        DrainFinalizedResourcesCore(null);
        _imageAtlas!.AdvanceFrame(frameSlot);
        _textureTable!.SynchronizeFrame(frameSlot);
    }

    internal void SynchronizeAfterBuild(uint frameSlot)
    {
        VerifyOwnerThread();
        ObjectDisposedException.ThrowIf(_destroyed, this);
        _imageAtlas!.AdvanceFrame(frameSlot);
        _textureTable!.SynchronizeFrame(frameSlot);
    }

    internal DescriptorSet GetTextureDescriptorSet(uint frameSlot)
    {
        VerifyOwnerThread();
        ObjectDisposedException.ThrowIf(_destroyed, this);
        return _textureTable!.GetDescriptorSet(frameSlot);
    }

    internal void DrainFinalizedResources()
    {
        VerifyOwnerThread();
        ObjectDisposedException.ThrowIf(_destroyed, this);
        DrainFinalizedResourcesCore(null);
    }

    internal void Destroy()
    {
        VerifyOwnerThread();
        if (_destroyed)
            return;

        Interlocked.Exchange(ref _accepting, 0);
        var spinWait = new SpinWait();
        while (Volatile.Read(ref _inFlightEnqueues) != 0)
            spinWait.SpinOnce();

        _destroyed = true;
        _imageRegistrations.Clear();
        var errors = new List<Exception>();
        TryInvoke(() => _textureTable?.DestroyDescriptorSets(), errors);
        foreach (var state in _states.Values)
            TryInvoke(state.Destroy, errors);
        _states.Clear();
        TryInvoke(() => _imageAtlas?.Destroy(), errors);
        TryInvoke(() => _glyphAtlas?.Destroy(), errors);
        _glyphAtlas = null;
        TryInvoke(() => _textureTable?.DestroyOwnedResources(), errors);
        while (_finalizedIds.TryDequeue(out _))
        {
        }

        if (errors.Count != 0)
            ExceptionDispatchInfo.Capture(errors[0]).Throw();
    }

    private IUiImageGpuResourceState? ResolveImageState(UiImage image)
    {
        var device = _device ?? throw new InvalidOperationException(
            "The UI resource manager has no graphics device.");
        var textureTable = _textureTable!;
        if (_imageRegistrations.TryGetValue(image, out var registration))
        {
            if (!_states.TryGetValue(registration.Id, out var existingState) ||
                existingState is not IUiImageGpuResourceState imageState)
            {
                throw new InvalidOperationException("The UI image registration has no live GPU state.");
            }
            return imageState;
        }

        IUiImageGpuResourceState? state;
        if (image.PixelWidth <= 1024 && image.PixelHeight <= 1024)
        {
            var entry = _imageAtlas!.TryCreate(image);
            state = entry is null ? null : new AtlasImageState(_imageAtlas, textureTable, entry);
        }
        else
        {
            state = TryCreateStandaloneImage(device, textureTable, image);
        }
        if (state is null)
            return null;

        try
        {
            var newRegistration = RegisterImage(image, state);
            state.ResourceId = newRegistration.Id;
            return state;
        }
        catch
        {
            state.Retire();
            throw;
        }
    }

    private static StandaloneImageState? TryCreateStandaloneImage(
        GraphicsDevice device,
        UiTextureTable textureTable,
        UiImage image)
    {
        if (!textureTable.HasFreeSlot)
            return null;

        Texture? texture = null;
        TextureView? view = null;
        try
        {
            texture = device.CreateTexture(new TextureDescription
            {
                Dimension = TextureDimension.Type2D,
                Format = TextureFormat.R8G8B8A8Srgb,
                Width = checked((uint)image.PixelWidth),
                Height = checked((uint)image.PixelHeight),
                Depth = 1,
                MipLevels = 1,
                ArrayLayers = 1,
                Usage = TextureUsage.Sampled | TextureUsage.TransferDestination
            });
            view = device.CreateTextureView(
                texture,
                new TextureViewDescription(TextureViewDimension.Type2D, 0, 1, 0, 1));
            if (!textureTable.TryRegister(view, out var slot))
            {
                view.Destroy();
                texture.Destroy();
                return null;
            }

            UploadHandle? upload = null;
            Exception? uploadFailure = null;
            try
            {
                upload = device.UploadTexture(texture, image.Pixels.Span);
            }
            catch (Exception exception)
            {
                uploadFailure = exception;
            }
            return new StandaloneImageState(
                textureTable,
                texture,
                view,
                slot,
                upload,
                uploadFailure);
        }
        catch
        {
            view?.Destroy();
            texture?.Destroy();
            throw;
        }
    }

    private GlyphAtlas GetGlyphAtlas()
    {
        if (_glyphAtlas is not null)
            return _glyphAtlas;
        var device = _device ?? throw new InvalidOperationException(
            "The UI resource manager has no graphics device.");
        var fontManager = _fontManager ?? throw new InvalidOperationException(
            "The UI resource manager has no font manager.");
        _glyphAtlas = new GlyphAtlas(device, fontManager, _textureTable!);
        return _glyphAtlas;
    }

    private void DrainFinalizedResourcesCore(Action<Exception>? onException)
    {
        while (_finalizedIds.TryDequeue(out var id))
        {
            if (!_states.Remove(id, out var state))
                continue;
            try
            {
                state.Retire();
            }
            catch (Exception exception)
            {
                if (onException is null)
                    ExceptionDispatchInfo.Capture(exception).Throw();
                onException(exception);
            }
        }
    }

    private void VerifyOwnerThread()
    {
        if (!ReferenceEquals(Thread.CurrentThread, _ownerThread))
            throw new InvalidOperationException("UI resource manager access must occur on its owner thread.");
    }

    private static void TryInvoke(Action action, List<Exception> errors)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            errors.Add(exception);
        }
    }
}

internal interface IUiImageGpuResourceState : IUiGpuResourceState
{
    ulong ResourceId { get; set; }
    bool IsUploadReady { get; }
    UiImageRenderBinding Binding { get; }
    void Publish();
    bool TryObserveUploadFailure(out Exception? failure, out bool firstObservation);
}

internal sealed class AtlasImageState(
    UiImageAtlas atlas,
    UiTextureTable textureTable,
    UiImageAtlasEntry entry) : IUiImageGpuResourceState
{
    private bool _retired;

    public ulong ResourceId { get; set; }
    public bool IsUploadReady => entry.IsUploadReady;
    public UiImageRenderBinding Binding => new(
        entry.Page.TextureSlot.Index,
        entry.Page.Texture.Width,
        entry.Page.Texture.Height,
        entry.Region);

    public bool TryObserveUploadFailure(out Exception? failure, out bool firstObservation) =>
        entry.TryObserveUploadFailure(out failure, out firstObservation);

    public void Publish() => textureTable.Publish(entry.Page.TextureSlot);

    public void Retire()
    {
        if (_retired)
            return;
        _retired = true;
        atlas.Retire(entry);
    }

    public void Destroy()
    {
    }
}

internal sealed class StandaloneImageState : IUiImageGpuResourceState
{
    private readonly UiTextureTable _textureTable;
    private readonly Texture _texture;
    private readonly TextureView _view;
    private readonly UiTextureSlot _slot;
    private readonly UploadHandle? _upload;
    private Exception? _uploadFailure;
    private bool _failureObserved;
    private bool _retired;
    private bool _destroyed;

    internal StandaloneImageState(
        UiTextureTable textureTable,
        Texture texture,
        TextureView view,
        UiTextureSlot slot,
        UploadHandle? upload,
        Exception? uploadFailure)
    {
        _textureTable = textureTable;
        _texture = texture;
        _view = view;
        _slot = slot;
        _upload = upload;
        _uploadFailure = uploadFailure;
    }

    public ulong ResourceId { get; set; }
    public bool IsUploadReady => _uploadFailure is null && _upload?.IsReady == true;
    public UiImageRenderBinding Binding => new(
        _slot.Index,
        _texture.Width,
        _texture.Height,
        new UiImageAtlasRegion(0, 0, _texture.Width, _texture.Height));

    public bool TryObserveUploadFailure(out Exception? failure, out bool firstObservation)
    {
        if (_uploadFailure is null && _upload?.IsFaulted == true)
        {
            _uploadFailure = _upload.Exception ?? new InvalidOperationException(
                "The UI image upload faulted without reporting an exception.");
        }
        failure = _uploadFailure;
        firstObservation = failure is not null && !_failureObserved;
        if (firstObservation)
            _failureObserved = true;
        return failure is not null;
    }

    public void Publish() => _textureTable.Publish(_slot);

    public void Retire()
    {
        if (_retired)
            return;
        _retired = true;
        _textureTable.Retire(_slot, DestroyBacking);
    }

    public void Destroy() => DestroyBacking();

    private void DestroyBacking()
    {
        if (_destroyed)
            return;
        _destroyed = true;

        Exception? firstFailure = null;
        try
        {
            _view.Destroy();
        }
        catch (Exception exception)
        {
            firstFailure = exception;
        }
        try
        {
            _texture.Destroy();
        }
        catch (Exception exception)
        {
            firstFailure ??= exception;
        }
        if (firstFailure is not null)
            ExceptionDispatchInfo.Capture(firstFailure).Throw();
    }
}
