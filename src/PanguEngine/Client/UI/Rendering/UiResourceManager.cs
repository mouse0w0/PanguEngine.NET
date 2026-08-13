using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PanguEngine.Graphics;

namespace PanguEngine.Client.UI.Rendering;

internal interface IUiGpuResourceState
{
    void Destroy();
}

internal sealed class UiImageRegistration
{
    private readonly WeakReference<UiResourceManager> _manager;

    internal UiImageRegistration(UiResourceManager manager, ulong id)
    {
        _manager = new WeakReference<UiResourceManager>(manager);
        Id = id;
    }

    internal ulong Id { get; }

    ~UiImageRegistration()
    {
        if (_manager.TryGetTarget(out var manager))
            manager.EnqueueFinalized(Id);
    }
}

internal sealed class UiResourceManager
{
    private static readonly Action<ILogger, ulong, Exception?> LogImageUploadFailure = LoggerMessage.Define<ulong>(
        LogLevel.Error,
        new EventId(1, nameof(LogImageUploadFailure)),
        "UI image resource {ResourceId} upload failed; subsequent draws will be skipped");

    private readonly GraphicsDevice? _device;
    private readonly DescriptorSetLayout? _imageDescriptorLayout;
    private readonly ILogger _logger;
    private readonly Thread _ownerThread = Thread.CurrentThread;
    private readonly Dictionary<ulong, IUiGpuResourceState> _states = [];
    private readonly ConditionalWeakTable<UiImage, UiImageRegistration> _imageRegistrations = new();
    private readonly ConcurrentQueue<ulong> _finalizedIds = new();
    private ulong _nextId;
    private int _accepting = 1;
    private int _inFlightEnqueues;
    private bool _destroyed;
    private Sampler? _linearSampler;
    private Sampler? _nearestSampler;

    internal UiResourceManager()
    {
        _logger = NullLogger.Instance;
    }

    internal UiResourceManager(
        GraphicsDevice device,
        DescriptorSetLayout? imageDescriptorLayout = null,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(device);
        _device = device;
        _imageDescriptorLayout = imageDescriptorLayout;
        _logger = logger ?? NullLogger.Instance;
    }

    internal UiImageRegistration RegisterImage(
        UiImage image,
        IUiGpuResourceState state)
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
            _states.Remove(registration.Id);
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

    internal GpuImage ResolveImage(UiImage image)
    {
        VerifyOwnerThread();
        ObjectDisposedException.ThrowIf(_destroyed, this);
        ArgumentNullException.ThrowIfNull(image);
        var device = _device ?? throw new InvalidOperationException(
            "The UI resource manager has no graphics device.");

        if (_imageRegistrations.TryGetValue(image, out var registration))
        {
            if (!_states.TryGetValue(registration.Id, out var existingState) ||
                existingState is not GpuImage imageState)
            {
                throw new InvalidOperationException("The UI image registration has no live GPU state.");
            }

            return imageState;
        }

        Texture? texture = null;
        TextureView? view = null;
        UiImageRegistration? newRegistration = null;
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
                Usage = TextureUsage.TransferDestination | TextureUsage.Sampled
            });
            view = device.CreateTextureView(
                texture,
                new TextureViewDescription(
                    TextureViewDimension.Type2D,
                    0,
                    1,
                    0,
                    1));
            GpuImage state;
            try
            {
                var upload = device.UploadTexture(texture, image.Pixels.Span);
                state = new GpuImage(texture, view, upload);
            }
            catch (Exception exception)
            {
                view.Destroy();
                texture.Destroy();
                view = null;
                texture = null;
                state = new GpuImage(exception);
            }

            newRegistration = RegisterImage(image, state);
            state.ResourceId = newRegistration.Id;
            return state;
        }
        catch
        {
            if (newRegistration is not null)
                _states.Remove(newRegistration.Id);
            view?.Destroy();
            texture?.Destroy();
            throw;
        }
    }

    internal Sampler GetSampler(bool nearest)
    {
        VerifyOwnerThread();
        ObjectDisposedException.ThrowIf(_destroyed, this);
        var device = _device ?? throw new InvalidOperationException(
            "The UI resource manager has no graphics device.");
        if (_linearSampler is not null && _nearestSampler is not null)
            return nearest ? _nearestSampler : _linearSampler;

        Sampler? linear = null;
        Sampler? nearestSampler = null;
        try
        {
            linear = device.CreateSampler(CreateSamplerDescription(FilterMode.Linear));
            nearestSampler = device.CreateSampler(CreateSamplerDescription(FilterMode.Nearest));
            _linearSampler = linear;
            _nearestSampler = nearestSampler;
            return nearest ? nearestSampler : linear;
        }
        catch
        {
            nearestSampler?.Destroy();
            linear?.Destroy();
            throw;
        }
    }

    internal UiImageRenderBinding? ResolveImageBinding(UiDrawImageCommand command)
    {
        VerifyOwnerThread();
        ObjectDisposedException.ThrowIf(_destroyed, this);
        ArgumentNullException.ThrowIfNull(command);

        var state = ResolveImage(command.Image);
        if (state.TryObserveUploadFailure(out var uploadFailure, out var firstObservation))
        {
            if (firstObservation)
                LogImageUploadFailure(_logger, state.ResourceId, uploadFailure);
            return null;
        }
        if (!state.IsUploadReady)
            return null;

        var layout = _imageDescriptorLayout ?? throw new InvalidOperationException(
            "The UI resource manager has no image descriptor layout.");
        var descriptorSet = state.GetDescriptorSet(
            command.SamplingMode,
            layout,
            this);
        return new UiImageRenderBinding(state.ResourceId, descriptorSet);
    }

    internal DescriptorSet CreateImageDescriptorSet(
        GpuImage state,
        ImageSamplingMode samplingMode,
        DescriptorSetLayout layout)
    {
        VerifyOwnerThread();
        var device = _device ?? throw new InvalidOperationException(
            "The UI resource manager has no graphics device.");
        var sampler = GetSampler(samplingMode == ImageSamplingMode.Nearest);
        return device.CreateDescriptorSet(new DescriptorSetDescription(
            layout,
            [DescriptorSetBinding.CombinedImageSampler(0, state.View, sampler)]));
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
        DrainFinalizedResourcesCore(errors.Add);

        foreach (var state in _states.Values)
        {
            try
            {
                state.Destroy();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
        }

        _states.Clear();
        DestroySampler(ref _nearestSampler, errors);
        DestroySampler(ref _linearSampler, errors);
        while (_finalizedIds.TryDequeue(out _))
        {
        }

        if (errors.Count > 0)
            ExceptionDispatchInfo.Capture(errors[0]).Throw();
    }

    private void DrainFinalizedResourcesCore(Action<Exception>? onException)
    {
        while (_finalizedIds.TryDequeue(out var id))
        {
            if (!_states.Remove(id, out var state))
                continue;

            try
            {
                state.Destroy();
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

    private static SamplerDescription CreateSamplerDescription(FilterMode filter) =>
        new(
            filter,
            filter,
            MipmapMode.Nearest,
            WrapMode.ClampToEdge,
            WrapMode.ClampToEdge,
            WrapMode.ClampToEdge,
            1,
            0,
            0,
            0);

    private static void DestroySampler(ref Sampler? sampler, List<Exception> errors)
    {
        if (sampler is null)
            return;

        try
        {
            sampler.Destroy();
        }
        catch (Exception exception)
        {
            errors.Add(exception);
        }

        sampler = null;
    }
}

internal sealed class GpuImage : IUiGpuResourceState
{
    private readonly Texture? _texture;
    private readonly TextureView? _view;
    private readonly UploadHandle? _upload;
    private Exception? _uploadFailure;
    private bool _uploadFailureReported;

    internal GpuImage(
        Texture texture,
        TextureView view,
        UploadHandle upload)
    {
        _texture = texture;
        _view = view;
        _upload = upload;
    }

    internal GpuImage(Exception uploadFailure)
    {
        _uploadFailure = uploadFailure;
    }

    internal ulong ResourceId { get; set; }
    internal TextureView View => _view!;
    internal bool IsUploadReady => _upload?.IsReady == true;
    private DescriptorSet? _linearDescriptorSet;
    private DescriptorSet? _nearestDescriptorSet;

    internal bool TryObserveUploadFailure(
        out Exception? uploadFailure,
        out bool firstObservation)
    {
        if (_uploadFailure is null && _upload?.IsFaulted == true)
            _uploadFailure = _upload.Exception!;

        uploadFailure = _uploadFailure;
        firstObservation = uploadFailure is not null && !_uploadFailureReported;
        if (firstObservation)
            _uploadFailureReported = true;
        return uploadFailure is not null;
    }

    internal DescriptorSet GetDescriptorSet(
        ImageSamplingMode samplingMode,
        DescriptorSetLayout layout,
        UiResourceManager manager)
    {
        var cached = samplingMode switch
        {
            ImageSamplingMode.Linear => _linearDescriptorSet,
            ImageSamplingMode.Nearest => _nearestDescriptorSet,
            _ => throw new InvalidOperationException("Image sampling mode has an undefined value.")
        };
        if (cached is not null)
            return cached;

        var descriptorSet = manager.CreateImageDescriptorSet(this, samplingMode, layout);
        if (samplingMode == ImageSamplingMode.Linear)
            _linearDescriptorSet = descriptorSet;
        else
            _nearestDescriptorSet = descriptorSet;
        return descriptorSet;
    }

    public void Destroy()
    {
        _linearDescriptorSet?.Destroy();
        _nearestDescriptorSet?.Destroy();
        _view?.Destroy();
        _texture?.Destroy();
    }
}
