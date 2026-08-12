using Silk.NET.Maths;

namespace PanguEngine.Audio;

/// <summary>
/// Controls and reports the state of a tracked sound.
/// </summary>
public class SoundInstance
{
    private readonly int _ownerThreadId = Environment.CurrentManagedThreadId;
    private ISoundInstanceOwner? _owner;
    private SoundInstanceState _state;

    internal SoundInstance(
        ISoundInstanceOwner? owner,
        SoundInstanceState state,
        float volume,
        float pitch,
        bool isLooping,
        bool isSpatial,
        Vector3D<double> position,
        float referenceDistance,
        float maxDistance,
        float rolloffFactor)
    {
        _state = state;
        IsSpatial = isSpatial;
        if (isSpatial)
        {
            MaxDistance = maxDistance;
            ReferenceDistance = referenceDistance;
            Position = position;
            RolloffFactor = rolloffFactor;
        }
        Volume = volume;
        Pitch = pitch;
        IsLooping = isLooping;
        _owner = owner;
    }

    /// <summary>The current lifecycle state.</summary>
    public SoundInstanceState State
    {
        get
        {
            CheckThread();
            return _state;
        }
    }

    /// <summary>Whether the sound is actively playing.</summary>
    public bool IsPlaying
    {
        get
        {
            CheckThread();
            return _state == SoundInstanceState.Playing;
        }
    }

    /// <summary>Whether the sound is currently paused.</summary>
    public bool IsPaused
    {
        get
        {
            CheckThread();
            return _state == SoundInstanceState.Paused;
        }
    }

    /// <summary>Whether the sound repeats until stopped or terminated.</summary>
    public bool IsLooping
    {
        get
        {
            CheckThread();
            return field;
        }
        private init;
    }

    /// <summary>Whether the sound uses world-space positioning and attenuation.</summary>
    public bool IsSpatial
    {
        get
        {
            CheckThread();
            return field;
        }
        private init;
    }

    /// <summary>The last requested non-negative volume multiplier.</summary>
    public float Volume
    {
        get
        {
            CheckThread();
            return field;
        }
        set
        {
            CheckThread();
            ValidateVolume(value);
            field = value;
            _owner?.SetVolume(this);
        }
    }

    /// <summary>The last requested positive pitch multiplier.</summary>
    public float Pitch
    {
        get
        {
            CheckThread();
            return field;
        }
        set
        {
            CheckThread();
            ValidatePitch(value);
            field = value;
            _owner?.MarkSettingsDirty(this);
        }
    }

    /// <summary>The last requested world-space sound position.</summary>
    public Vector3D<double> Position
    {
        get
        {
            CheckThread();
            EnsureSpatial();
            return field;
        }
        set
        {
            CheckThread();
            EnsureSpatial();
            ValidatePosition(value);
            field = value;
            NotifySettingsChanged();
        }
    }

    /// <summary>The last requested positive distance at which attenuation begins.</summary>
    public float ReferenceDistance
    {
        get
        {
            CheckThread();
            EnsureSpatial();
            return field;
        }
        set
        {
            CheckThread();
            EnsureSpatial();
            ValidateDistance(value);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, MaxDistance);
            field = value;
            NotifySettingsChanged();
        }
    }

    /// <summary>The last requested positive distance beyond which attenuation is clamped.</summary>
    public float MaxDistance
    {
        get
        {
            CheckThread();
            EnsureSpatial();
            return field;
        }
        set
        {
            CheckThread();
            EnsureSpatial();
            ValidateDistance(value);
            ArgumentOutOfRangeException.ThrowIfLessThan(value, ReferenceDistance);
            field = value;
            NotifySettingsChanged();
        }
    }

    /// <summary>The last requested non-negative attenuation rate.</summary>
    public float RolloffFactor
    {
        get
        {
            CheckThread();
            EnsureSpatial();
            return field;
        }
        set
        {
            CheckThread();
            EnsureSpatial();
            ValidateRolloffFactor(value);
            field = value;
            NotifySettingsChanged();
        }
    }

    /// <summary>Pauses the sound if it is active.</summary>
    public void Pause()
    {
        CheckThread();
        _owner?.Pause(this);
    }

    /// <summary>Resumes the sound if it is active and not category-paused.</summary>
    public void Resume()
    {
        CheckThread();
        _owner?.Resume(this);
    }

    /// <summary>Stops the sound if it is still active.</summary>
    public void Stop()
    {
        CheckThread();
        _owner?.Stop(this);
    }

    internal void Complete(SoundInstanceState state)
    {
        CheckThread();
        if (_state is not SoundInstanceState.Playing and not SoundInstanceState.Paused)
            return;
        if (state is SoundInstanceState.Playing or SoundInstanceState.Paused)
            throw new ArgumentOutOfRangeException(nameof(state));

        _state = state;
        _owner = null;
    }

    internal void SetPlaybackState(SoundInstanceState state)
    {
        CheckThread();
        if (state is not SoundInstanceState.Playing and not SoundInstanceState.Paused)
            throw new ArgumentOutOfRangeException(nameof(state));
        if (_state is SoundInstanceState.Completed
            or SoundInstanceState.Stopped
            or SoundInstanceState.Stolen
            or SoundInstanceState.Rejected)
            return;
        _state = state;
    }

    protected void NotifySettingsChanged()
    {
        _owner?.MarkSettingsDirty(this);
    }

    protected void CheckThread()
    {
        if (Environment.CurrentManagedThreadId != _ownerThreadId)
            throw new InvalidOperationException("Sound instances must be accessed from their creating thread.");
    }

    protected static void ValidateVolume(float value)
    {
        if (!float.IsFinite(value) || value < 0)
            throw new ArgumentOutOfRangeException(nameof(value), value, "Volume must be finite and non-negative.");
    }

    protected static void ValidatePitch(float value)
    {
        if (!float.IsFinite(value) || value <= 0)
            throw new ArgumentOutOfRangeException(nameof(value), value, "Pitch must be finite and positive.");
    }

    internal static void ValidateSpatialParameters(
        bool isSpatial,
        Vector3D<double> position,
        float referenceDistance,
        float maxDistance,
        float rolloffFactor)
    {
        if (!isSpatial)
            return;
        ValidatePosition(position);
        ValidateDistance(referenceDistance);
        ValidateDistance(maxDistance);
        ValidateRolloffFactor(rolloffFactor);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(referenceDistance, maxDistance);
    }

    private void EnsureSpatial()
    {
        if (!IsSpatial)
            throw new InvalidOperationException("This sound instance is not spatial.");
    }

    private static void ValidatePosition(Vector3D<double> value)
    {
        if (!double.IsFinite(value.X) || !double.IsFinite(value.Y) || !double.IsFinite(value.Z))
            throw new ArgumentOutOfRangeException(nameof(value), "Position components must be finite.");
    }

    private static void ValidateDistance(float value)
    {
        if (!float.IsFinite(value) || value <= 0)
            throw new ArgumentOutOfRangeException(nameof(value), value, "Distance must be finite and positive.");
    }

    private static void ValidateRolloffFactor(float value)
    {
        if (!float.IsFinite(value) || value < 0)
            throw new ArgumentOutOfRangeException(nameof(value), value, "Rolloff factor must be finite and non-negative.");
    }
}

internal interface ISoundInstanceOwner
{
    void Stop(SoundInstance instance);
    void Pause(SoundInstance instance);
    void Resume(SoundInstance instance);
    void SetVolume(SoundInstance instance);
    void MarkSettingsDirty(SoundInstance instance);
}
