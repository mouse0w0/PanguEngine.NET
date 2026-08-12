using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using PanguEngine.Audio.Backend;
using PanguEngine.Audio.Decoding;
using Silk.NET.Core.Loader;
using Silk.NET.Maths;
using Silk.NET.OpenAL;

namespace PanguEngine.Audio.OpenAL;

internal sealed unsafe class OpenAlAudioBackend : IAudioBackend
{
    private const int MaximumSources = 64;
    private static readonly Action<ILogger, int, int, Exception?> LogReducedSourceCapacity =
        LoggerMessage.Define<int, int>(
            LogLevel.Information,
            new EventId(1, nameof(LogReducedSourceCapacity)),
            "OpenAL created {SourceCount} of {MaximumSources} requested audio sources");

    private static readonly Action<ILogger, string, Exception?> LogInitializationCleanupFailure =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(2, nameof(LogInitializationCleanupFailure)),
            "OpenAL initialization cleanup could not release {ResourceType}");

    private readonly int _ownerThreadId = Environment.CurrentManagedThreadId;
    private readonly ILogger _logger;
    private readonly List<uint> _sources = [];
    private readonly Stack<AudioSourceHandle> _availableSources = [];
    private readonly HashSet<uint> _rentedSources = [];
    private readonly HashSet<uint> _buffers = [];
    private ALContext? _alc;
    private AL? _al;
    private Device* _device;
    private Context* _context;
    private bool _destroyed;

    internal OpenAlAudioBackend(ILogger logger)
    {
        _logger = logger;
        try
        {
            _alc = ALContext.GetApi(soft: false);
            _al = AL.GetApi(soft: false);
            _device = _alc.OpenDevice(null!);
            if (_device == null)
                throw new AudioBackendInitializationException("OpenAL did not provide a default output device.");
            CheckAlcError("opening the default OpenAL device");

            _context = _alc.CreateContext(_device, null);
            if (_context == null)
                throw CreateInitializationException("OpenAL context creation failed.");
            CheckAlcError("creating the OpenAL context");

            if (!_alc.MakeContextCurrent(_context))
                throw CreateInitializationException("The OpenAL context could not be made current.");
            CheckAlcError("making the OpenAL context current");

            _al.DistanceModel(DistanceModel.InverseDistanceClamped);
            CheckAlError("setting the OpenAL distance model");
            CreateSources();
        }
        catch (Exception exception)
        {
            CleanupInitialization();
            if (exception is AudioBackendInitializationException)
                throw;
            if (IsInitializationFailure(exception))
                throw new AudioBackendInitializationException("OpenAL initialization failed.", exception);
            throw;
        }
    }

    public bool IsAvailable => !_destroyed;
    public int SourceCapacity => _sources.Count;

    public AudioBufferHandle CreateBuffer(PcmAudioData data)
    {
        CheckUsable();
        uint id = 0;
        try
        {
            id = _al!.GenBuffer();
            CheckAlError("creating an audio buffer");
            if (id == 0)
                throw new AudioBackendException("OpenAL returned an invalid audio buffer handle.");

            var format = data.Channels == 1 ? BufferFormat.Mono16 : BufferFormat.Stereo16;
            _al.BufferData(id, format, data.Samples, data.SampleRate);
            CheckAlError("uploading PCM data");
            _buffers.Add(id);
            return new AudioBufferHandle(id);
        }
        catch
        {
            if (id != 0)
            {
                _al!.DeleteBuffer(id);
                _ = _al.GetError();
            }
            throw;
        }
    }

    public void DestroyBuffer(AudioBufferHandle buffer)
    {
        CheckUsable();
        if (!_buffers.Contains(buffer.Value))
            throw new InvalidOperationException("The audio buffer is not owned by this backend.");

        _al!.DeleteBuffer(buffer.Value);
        CheckAlError("destroying an audio buffer");
        _buffers.Remove(buffer.Value);
    }

    public bool TryRentSource(out AudioSourceHandle source)
    {
        CheckUsable();
        if (!_availableSources.TryPop(out source))
            return false;

        _rentedSources.Add(source.Value);
        return true;
    }

    public void Play(AudioSourceHandle source, AudioBufferHandle buffer, AudioSourceSettings settings)
    {
        CheckUsable();
        EnsureRented(source);
        if (!_buffers.Contains(buffer.Value))
            throw new InvalidOperationException("The audio buffer is not owned by this backend.");

        ApplySettings(source, settings);
        _al!.SetSourceProperty(source.Value, SourceInteger.Buffer, buffer.Value);
        CheckAlError("binding an audio buffer");
        _al.SourcePlay(source.Value);
        CheckAlError("starting an audio source");
    }

    public void Pause(AudioSourceHandle source)
    {
        CheckUsable();
        EnsureRented(source);
        _al!.SourcePause(source.Value);
        CheckAlError("pausing an audio source");
    }

    public void Resume(AudioSourceHandle source)
    {
        CheckUsable();
        EnsureRented(source);
        _al!.SourcePlay(source.Value);
        CheckAlError("resuming an audio source");
    }

    public void Stop(AudioSourceHandle source)
    {
        CheckUsable();
        EnsureRented(source);
        _al!.SourceStop(source.Value);
        CheckAlError("stopping an audio source");
    }

    public void Update(AudioSourceHandle source, AudioSourceSettings settings)
    {
        CheckUsable();
        EnsureRented(source);
        ApplySettings(source, settings);
    }

    public AudioBackendSourceState GetState(AudioSourceHandle source)
    {
        CheckUsable();
        EnsureRented(source);
        _al!.GetSourceProperty(source.Value, GetSourceInteger.SourceState, out var stateValue);
        CheckAlError("querying an audio source");
        return (SourceState)stateValue switch
        {
            SourceState.Initial => AudioBackendSourceState.Initial,
            SourceState.Playing => AudioBackendSourceState.Playing,
            SourceState.Paused => AudioBackendSourceState.Paused,
            SourceState.Stopped => AudioBackendSourceState.Stopped,
            _ => throw new AudioBackendException($"OpenAL returned unknown source state 0x{stateValue:X}.")
        };
    }

    public void ReturnSource(AudioSourceHandle source)
    {
        CheckUsable();
        EnsureRented(source);
        ResetSource(source.Value);
        _rentedSources.Remove(source.Value);
        _availableSources.Push(source);
    }

    public void SetListener(AudioListenerState listener)
    {
        CheckUsable();
        var forward = Normalize(listener.Forward);
        var up = Normalize(listener.Up);

        _al!.SetListenerProperty(
            ListenerVector3.Position,
            ToFiniteFloat(listener.Position.X),
            ToFiniteFloat(listener.Position.Y),
            ToFiniteFloat(listener.Position.Z));
        CheckAlError("setting the audio listener position");

        var orientation = stackalloc float[6]
        {
            (float)forward.X, (float)forward.Y, (float)forward.Z,
            (float)up.X, (float)up.Y, (float)up.Z
        };
        _al.SetListenerProperty(ListenerFloatArray.Orientation, orientation);
        CheckAlError("setting the audio listener orientation");
    }

    public void Destroy()
    {
        if (_destroyed)
            return;
        CheckThread();
        _destroyed = true;

        AudioBackendException? failure = null;
        try
        {
            if (_sources.Count > 0 && _al is not null)
            {
                _al.SourceStop(_sources.ToArray());
                CheckAlError("stopping audio sources during shutdown");
            }
        }
        catch (AudioBackendException exception)
        {
            failure ??= exception;
        }

        try
        {
            if (_sources.Count > 0 && _al is not null)
            {
                _al.DeleteSources(_sources.ToArray());
                CheckAlError("destroying audio sources");
            }
        }
        catch (AudioBackendException exception)
        {
            failure ??= exception;
        }

        try
        {
            if (_buffers.Count > 0 && _al is not null)
            {
                _al.DeleteBuffers(_buffers.ToArray());
                CheckAlError("destroying audio buffers");
            }
        }
        catch (AudioBackendException exception)
        {
            failure ??= exception;
        }

        try
        {
            if (_context != null && _alc is not null)
            {
                if (!_alc.MakeContextCurrent(null))
                    throw CreateRuntimeAlcException("clearing the current OpenAL context");
                CheckAlcError("clearing the current OpenAL context");
            }
        }
        catch (AudioBackendException exception)
        {
            failure ??= exception;
        }

        try
        {
            if (_context != null && _alc is not null)
            {
                _alc.DestroyContext(_context);
                _context = null;
                CheckAlcError("destroying the OpenAL context");
            }
        }
        catch (AudioBackendException exception)
        {
            failure ??= exception;
        }

        try
        {
            if (_device != null && _alc is not null)
            {
                if (!_alc.CloseDevice(_device))
                    throw new AudioBackendException("OpenAL failed to close the output device.");
                _device = null;
            }
        }
        catch (AudioBackendException exception)
        {
            failure ??= exception;
        }

        _al?.Dispose();
        _alc?.Dispose();
        _al = null;
        _alc = null;
        _sources.Clear();
        _availableSources.Clear();
        _rentedSources.Clear();
        _buffers.Clear();

        if (failure is not null)
            throw failure;
    }

    private void CreateSources()
    {
        for (var i = 0; i < MaximumSources; i++)
        {
            var id = _al!.GenSource();
            var error = _al.GetError();
            if (error != AudioError.NoError || id == 0)
                break;

            _sources.Add(id);
            _availableSources.Push(new AudioSourceHandle(id));
        }

        if (_sources.Count == 0)
            throw new AudioBackendInitializationException("OpenAL could not create any audio sources.");
        if (_sources.Count < MaximumSources)
            LogReducedSourceCapacity(_logger, _sources.Count, MaximumSources, null);
    }

    private void ApplySettings(AudioSourceHandle source, AudioSourceSettings settings)
    {
        if (settings.Pitch is < 0.5f or > 2f)
            throw new InvalidOperationException("Backend pitch must be within the OpenAL range 0.5 to 2.0.");

        SetSource(source.Value, SourceBoolean.SourceRelative, settings.IsRelative, "setting source relativity");
        SetSource(source.Value, SourceBoolean.Looping, settings.IsLooping, "setting source looping");
        SetSource(source.Value, SourceVector3.Position, settings.Position, "setting source position");
        SetSource(source.Value, SourceFloat.Gain, settings.Gain, "setting source gain");
        SetSource(source.Value, SourceFloat.Pitch, settings.Pitch, "setting source pitch");
        SetSource(source.Value, SourceFloat.ReferenceDistance, settings.ReferenceDistance,
            "setting source reference distance");
        SetSource(source.Value, SourceFloat.MaxDistance, settings.MaxDistance, "setting source maximum distance");
        SetSource(source.Value, SourceFloat.RolloffFactor, settings.RolloffFactor,
            "setting source rolloff factor");
    }

    private void ResetSource(uint source)
    {
        _al!.SourceStop(source);
        CheckAlError("stopping a returned audio source");
        _al.SetSourceProperty(source, SourceInteger.Buffer, 0u);
        CheckAlError("unbinding a returned audio source");
        SetSource(source, SourceBoolean.Looping, false, "resetting source looping");
        SetSource(source, SourceBoolean.SourceRelative, false, "resetting source relativity");
        SetSource(source, SourceVector3.Position, Vector3D<float>.Zero, "resetting source position");
        SetSource(source, SourceFloat.Gain, 1, "resetting source gain");
        SetSource(source, SourceFloat.Pitch, 1, "resetting source pitch");
        SetSource(source, SourceFloat.ReferenceDistance, 1, "resetting source reference distance");
        SetSource(source, SourceFloat.MaxDistance, float.MaxValue, "resetting source maximum distance");
        SetSource(source, SourceFloat.RolloffFactor, 1, "resetting source rolloff factor");
    }

    private void SetSource(uint source, SourceBoolean parameter, bool value, string operation)
    {
        _al!.SetSourceProperty(source, parameter, value);
        CheckAlError(operation);
    }

    private void SetSource(uint source, SourceFloat parameter, float value, string operation)
    {
        _al!.SetSourceProperty(source, parameter, value);
        CheckAlError(operation);
    }

    private void SetSource(uint source, SourceVector3 parameter, Vector3D<float> value, string operation)
    {
        _al!.SetSourceProperty(source, parameter, value.X, value.Y, value.Z);
        CheckAlError(operation);
    }

    private void CheckUsable()
    {
        CheckThread();
        ObjectDisposedException.ThrowIf(_destroyed, this);
    }

    private void CheckThread()
    {
        if (Environment.CurrentManagedThreadId != _ownerThreadId)
            throw new InvalidOperationException("The audio backend must be used from its creating thread.");
    }

    private void EnsureRented(AudioSourceHandle source)
    {
        if (!_rentedSources.Contains(source.Value))
            throw new InvalidOperationException("The audio source is not rented from this backend.");
    }

    private void CheckAlError(string operation)
    {
        var error = _al!.GetError();
        if (error != AudioError.NoError)
            throw new AudioBackendException($"OpenAL error {error} while {operation}.");
    }

    private void CheckAlcError(string operation)
    {
        var error = _alc!.GetError(_device);
        if (error != ContextError.NoError)
            throw new AudioBackendException($"OpenAL context error {error} while {operation}.");
    }

    private AudioBackendInitializationException CreateInitializationException(string message)
    {
        var error = _device == null ? ContextError.NoError : _alc!.GetError(_device);
        return new AudioBackendInitializationException($"{message} Context error: {error}.");
    }

    private AudioBackendException CreateRuntimeAlcException(string operation)
    {
        var error = _device == null ? ContextError.NoError : _alc!.GetError(_device);
        return new AudioBackendException($"OpenAL context error {error} while {operation}.");
    }

    private void CleanupInitialization()
    {
        try
        {
            if (_sources.Count > 0 && _al is not null)
                _al.DeleteSources(_sources.ToArray());
        }
        catch (Exception exception)
        {
            LogInitializationCleanupFailure(_logger, "audio sources", exception);
        }

        try
        {
            if (_context != null && _alc is not null)
            {
                _alc.MakeContextCurrent(null);
                _alc.DestroyContext(_context);
            }
        }
        catch (Exception exception)
        {
            LogInitializationCleanupFailure(_logger, "audio context", exception);
        }

        try
        {
            if (_device != null && _alc is not null)
                _alc.CloseDevice(_device);
        }
        catch (Exception exception)
        {
            LogInitializationCleanupFailure(_logger, "audio device", exception);
        }

        _al?.Dispose();
        _alc?.Dispose();
        _al = null;
        _alc = null;
        _context = null;
        _device = null;
    }

    private static bool IsInitializationFailure(Exception exception) =>
        exception is AudioBackendInitializationException
            or AudioBackendException
            or FileNotFoundException
            or DllNotFoundException
            or EntryPointNotFoundException
            or SymbolLoadingException
            or BadImageFormatException
            or SEHException
        || exception.InnerException is not null && IsInitializationFailure(exception.InnerException);

    private static Vector3D<double> Normalize(Vector3D<double> value)
    {
        var absolute = Vector3D.Abs(value);
        var scale = Math.Max(absolute.X, Math.Max(absolute.Y, absolute.Z));
        return Vector3D.Normalize(value / scale);
    }

    private static float ToFiniteFloat(double value) =>
        (float)Math.Clamp(value, float.MinValue, float.MaxValue);
}
