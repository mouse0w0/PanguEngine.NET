using Microsoft.Extensions.Logging;
using PanguEngine.Audio.Backend;
using PanguEngine.Audio.Decoding;
using PanguEngine.Registries;
using PanguEngine.Resources;
using Silk.NET.Maths;

namespace PanguEngine.Audio;

/// <summary>
/// Loads sound events and controls client audio playback.
/// </summary>
public sealed class AudioSystem
{
    private static readonly Action<ILogger, Exception?> LogInitialListenerFailure = LoggerMessage.Define(
        LogLevel.Warning,
        new EventId(1, nameof(LogInitialListenerFailure)),
        "Audio output failed while setting the initial listener; using null audio");

    private static readonly Action<ILogger, Exception?> LogReleaseFailure = LoggerMessage.Define(
        LogLevel.Warning,
        new EventId(2, nameof(LogReleaseFailure)),
        "An audio resource could not be fully released");

    private readonly int _ownerThreadId = Environment.CurrentManagedThreadId;
    private readonly IRegistry<SoundCategory> _categories;
    private readonly IAudioRandom _random;
    private readonly ILogger _logger;
    private readonly ISoundInstanceOwner _instanceOwner;
    private readonly Dictionary<SoundCategory, float> _categoryVolumes;
    private readonly HashSet<SoundCategory> _pausedCategories = new(ReferenceEqualityComparer.Instance);
    private readonly List<ActiveSound> _active = [];
    private readonly IAudioBackend _backend;
    private readonly SoundEventManager _eventManager;
    private AudioListenerState _listener = new(
        Vector3D<double>.Zero,
        new Vector3D<double>(0, 0, -1),
        Vector3D<double>.UnitY);
    private float _masterVolume = 1;
    private long _nextSequence;
    private bool _loaded;
    private bool _ready;
    private bool _isUiPaused;
    private bool _destroyed;

    internal AudioSystem(
        ResourceManager resources,
        IRegistry<SoundCategory> categories,
        IRegistry<SoundEvent> registry,
        ILogger logger,
        IAudioBackend? backend = null,
        IAudioRandom? random = null,
        IReadOnlyDictionary<string, IAudioDecoder>? decoders = null)
    {
        if (!categories.IsFrozen)
            throw new InvalidOperationException("The sound category registry must be frozen before creating audio.");

        _categories = categories;
        _logger = logger;
        _random = random ?? new AudioRandom(new Random());
        _instanceOwner = new InstanceOwner(this);
        var selectedDecoders = decoders ?? new Dictionary<string, IAudioDecoder>(StringComparer.Ordinal)
        {
            [".ogg"] = new OggVorbisDecoder()
        };
        _categoryVolumes = new Dictionary<SoundCategory, float>(ReferenceEqualityComparer.Instance);
        foreach (var entry in categories.Entries)
            _categoryVolumes.Add(entry.Value, 1);
        _backend = backend ?? AudioBackendFactory.Create(logger);

        if (_backend.IsAvailable)
        {
            try
            {
                _backend.SetListener(_listener);
            }
            catch (AudioBackendException exception)
            {
                LogInitialListenerFailure(_logger, exception);
                TryCleanup(_backend.Destroy);
                _backend = new NullAudioBackend();
            }
        }

        _eventManager = new SoundEventManager(
            resources,
            categories,
            registry,
            _backend,
            selectedDecoders,
            logger);
    }

    /// <summary>Whether an audio output backend is currently available.</summary>
    public bool IsAvailable
    {
        get
        {
            CheckThread();
            return !_destroyed && _backend.IsAvailable;
        }
    }

    /// <summary>Whether this audio system has been destroyed.</summary>
    public bool IsDestroyed
    {
        get
        {
            CheckThread();
            return _destroyed;
        }
    }

    /// <summary>Whether sounds affected by a user interface pause are currently paused.</summary>
    public bool IsUiPaused
    {
        get
        {
            CheckUsable();
            return _isUiPaused;
        }
        set
        {
            CheckUsable();
            if (_isUiPaused == value)
                return;

            _isUiPaused = value;
            foreach (var active in _active.Where(active => !active.SoundEvent.Category.IgnoresUiPause))
                ApplyEffectivePause(active);
        }
    }

    /// <summary>The master volume shared by every sound category.</summary>
    public float MasterVolume
    {
        get
        {
            CheckUsable();
            return _masterVolume;
        }
        set
        {
            CheckUsable();
            ValidateMixerVolume(value, nameof(value));
            _masterVolume = value;
            UpdateActiveGains(static _ => true);
        }
    }

    /// <summary>
    /// Gets the volume applied to a sound category.
    /// </summary>
    /// <param name="category">The category to query.</param>
    /// <returns>The category volume.</returns>
    public float GetCategoryVolume(SoundCategory category)
    {
        CheckUsable();
        ValidateCategory(category);
        return _categoryVolumes[category];
    }

    /// <summary>
    /// Sets the volume applied to a sound category.
    /// </summary>
    /// <param name="category">The category to update.</param>
    /// <param name="volume">The volume from zero to one.</param>
    public void SetCategoryVolume(SoundCategory category, float volume)
    {
        CheckUsable();
        ValidateCategory(category);
        ValidateMixerVolume(volume, nameof(volume));
        _categoryVolumes[category] = volume;
        UpdateActiveGains(active => active.SoundEvent.Category == category);
    }

    /// <summary>Gets whether a sound category is paused.</summary>
    /// <param name="category">The category to query.</param>
    /// <returns>Whether the category is paused.</returns>
    public bool IsCategoryPaused(SoundCategory category)
    {
        CheckUsable();
        ValidateCategory(category);
        return IsCategoryEffectivelyPaused(category);
    }

    /// <summary>Pauses one or more sound categories.</summary>
    /// <param name="categories">The registered categories to pause.</param>
    public void PauseCategories(params SoundCategory[] categories) => SetCategoriesPaused(categories, paused: true);

    /// <summary>Resumes one or more sound categories.</summary>
    /// <param name="categories">The registered categories to resume.</param>
    public void ResumeCategories(params SoundCategory[] categories) => SetCategoriesPaused(categories, paused: false);

    /// <summary>
    /// Updates the world-space audio listener.
    /// </summary>
    /// <param name="listener">The new listener state.</param>
    public void SetListener(AudioListenerState listener)
    {
        CheckUsable();
        var validated = new AudioListenerState(listener.Position, listener.Forward, listener.Up);
        _listener = validated;
        if (!_backend.IsAvailable)
            return;
        _backend.SetListener(validated);
    }

    /// <summary>Plays a non-spatial one-shot sound.</summary>
    /// <param name="soundEvent">The registered sound event to play.</param>
    /// <param name="volume">The non-negative volume multiplier.</param>
    /// <param name="pitch">The positive pitch multiplier.</param>
    /// <param name="seed">The optional deterministic playback seed.</param>
    public void Play(SoundEvent soundEvent, float volume = 1, float pitch = 1, long? seed = null) =>
        _ = Start(
            soundEvent,
            volume,
            pitch,
            isSpatial: false,
            position: Vector3D<double>.Zero,
            referenceDistance: 1,
            maxDistance: 16,
            rolloffFactor: 0,
            looping: false,
            tracked: false,
            seed: seed);

    /// <summary>Plays a spatial one-shot sound.</summary>
    /// <param name="soundEvent">The registered sound event to play.</param>
    /// <param name="position">The world-space sound position.</param>
    /// <param name="referenceDistance">The positive distance at which attenuation begins.</param>
    /// <param name="maxDistance">The positive distance beyond which attenuation is clamped.</param>
    /// <param name="rolloffFactor">The non-negative attenuation rate.</param>
    /// <param name="volume">The non-negative volume multiplier.</param>
    /// <param name="pitch">The positive pitch multiplier.</param>
    /// <param name="seed">The optional deterministic playback seed.</param>
    public void PlayAt(
        SoundEvent soundEvent,
        Vector3D<double> position,
        float referenceDistance = 1,
        float maxDistance = 16,
        float rolloffFactor = 1,
        float volume = 1,
        float pitch = 1,
        long? seed = null) =>
        _ = Start(
            soundEvent,
            volume,
            pitch,
            isSpatial: true,
            position,
            referenceDistance,
            maxDistance,
            rolloffFactor,
            looping: false,
            tracked: false,
            seed: seed);

    /// <summary>Plays and returns a tracked non-spatial sound.</summary>
    /// <param name="soundEvent">The registered sound event to play.</param>
    /// <param name="looping">Whether the sound repeats.</param>
    /// <param name="volume">The non-negative volume multiplier.</param>
    /// <param name="pitch">The positive pitch multiplier.</param>
    /// <param name="seed">The optional deterministic playback seed.</param>
    /// <returns>The tracked sound instance.</returns>
    public SoundInstance PlayTracked(
        SoundEvent soundEvent,
        bool looping = false,
        float volume = 1,
        float pitch = 1,
        long? seed = null) =>
        Start(
            soundEvent,
            volume,
            pitch,
            isSpatial: false,
            position: Vector3D<double>.Zero,
            referenceDistance: 1,
            maxDistance: 16,
            rolloffFactor: 0,
            looping: looping,
            tracked: true,
            seed: seed)!;

    /// <summary>Plays and returns a tracked spatial sound.</summary>
    /// <param name="soundEvent">The registered sound event to play.</param>
    /// <param name="position">The world-space sound position.</param>
    /// <param name="referenceDistance">The positive distance at which attenuation begins.</param>
    /// <param name="maxDistance">The positive distance beyond which attenuation is clamped.</param>
    /// <param name="rolloffFactor">The non-negative attenuation rate.</param>
    /// <param name="volume">The non-negative volume multiplier.</param>
    /// <param name="pitch">The positive pitch multiplier.</param>
    /// <param name="looping">Whether the sound repeats.</param>
    /// <param name="seed">The optional deterministic playback seed.</param>
    /// <returns>The tracked sound instance.</returns>
    public SoundInstance PlayTrackedAt(
        SoundEvent soundEvent,
        Vector3D<double> position,
        float referenceDistance = 1,
        float maxDistance = 16,
        float rolloffFactor = 1,
        float volume = 1,
        float pitch = 1,
        bool looping = false,
        long? seed = null) =>
        Start(
            soundEvent,
            volume,
            pitch,
            isSpatial: true,
            position,
            referenceDistance,
            maxDistance,
            rolloffFactor,
            looping,
            tracked: true,
            seed: seed)!;

    internal void Load()
    {
        CheckUsable();
        if (_loaded)
            throw new InvalidOperationException("Audio content is already loaded.");
        _eventManager.Load();
        _loaded = true;
    }

    internal void MarkReady()
    {
        CheckUsable();
        if (!_loaded)
            throw new InvalidOperationException("Audio content must be loaded before it is marked ready.");
        if (_ready)
            throw new InvalidOperationException("Audio playback is already ready.");
        _ready = true;
    }

    internal void Update()
    {
        CheckUsable();
        if (!_loaded)
            return;

        foreach (var active in _active.ToArray())
        {
            if (_backend.GetState(active.Source) == AudioBackendSourceState.Stopped)
            {
                Terminate(active, SoundInstanceState.Completed, stopSource: false);
                continue;
            }

            if (active.SettingsDirty)
            {
                _backend.Update(active.Source, CreateSettings(active));
                active.SettingsDirty = false;
            }
        }
    }

    internal void Destroy()
    {
        CheckThread();
        if (_destroyed)
            return;

        foreach (var active in _active.ToArray())
        {
            TryCleanup(() => _backend.Stop(active.Source));
            TryCleanup(() => _backend.ReturnSource(active.Source));
            active.Instance?.Complete(SoundInstanceState.Stopped);
        }

        TryCleanup(_eventManager.Destroy);
        TryCleanup(_backend.Destroy);
        _active.Clear();
        _ready = false;
        _pausedCategories.Clear();
        _isUiPaused = false;
        _destroyed = true;
    }

    private void Stop(SoundInstance instance)
    {
        CheckUsable();
        var active = FindActive(instance);
        Terminate(active, SoundInstanceState.Stopped, stopSource: true);
    }

    private void Pause(SoundInstance instance)
    {
        CheckUsable();
        var active = FindActive(instance);
        if (active.InstancePaused)
            return;

        active.InstancePaused = true;
        ApplyEffectivePause(active);
    }

    private void Resume(SoundInstance instance)
    {
        CheckUsable();
        var active = FindActive(instance);
        if (!active.InstancePaused)
            return;

        active.InstancePaused = false;
        ApplyEffectivePause(active);
    }

    private void SetVolume(SoundInstance instance)
    {
        CheckUsable();
        var active = FindActive(instance);
        _backend.Update(active.Source, CreateSettings(active));
    }

    private void MarkSettingsDirty(SoundInstance instance)
    {
        CheckUsable();
        FindActive(instance).SettingsDirty = true;
    }

    private SoundInstance? Start(
        SoundEvent soundEvent,
        float volume,
        float pitch,
        bool isSpatial,
        Vector3D<double> position,
        float referenceDistance,
        float maxDistance,
        float rolloffFactor,
        bool looping,
        bool tracked,
        long? seed)
    {
        CheckPlayRequest(
            soundEvent,
            volume,
            pitch,
            isSpatial,
            position,
            referenceDistance,
            maxDistance,
            rolloffFactor);
        var categoryPaused = IsCategoryEffectivelyPaused(soundEvent.Category);
        if (categoryPaused && !looping
            || !_backend.IsAvailable)
            return CreateRejectedInstance(
                tracked,
                looping,
                volume,
                pitch,
                isSpatial,
                position,
                referenceDistance,
                maxDistance,
                rolloffFactor);

        var effectiveSeed = seed ?? _random.NextSeed();
        if (!_eventManager.TrySelect(soundEvent, isSpatial, effectiveSeed, out var variant))
            return CreateRejectedInstance(
                tracked,
                looping,
                volume,
                pitch,
                isSpatial,
                position,
                referenceDistance,
                maxDistance,
                rolloffFactor);

        var variantVolume = variant.MinVolume < variant.MaxVolume
            ? RandomRange(variant.MinVolume, variant.MaxVolume, AudioSeedDeriver.DeriveVolume(effectiveSeed))
            : variant.MinVolume;
        var variantPitch = variant.MinPitch < variant.MaxPitch
            ? RandomRange(variant.MinPitch, variant.MaxPitch, AudioSeedDeriver.DerivePitch(effectiveSeed))
            : variant.MinPitch;
        var sequence = _nextSequence++;
        var request = new AllocationCandidate(
            IsOutOfRange(isSpatial, position, maxDistance),
            categoryPaused
                ? 0
                : EstimateAudibleGain(
                    soundEvent.Category,
                    variantVolume,
                    volume,
                    isSpatial,
                    position,
                    referenceDistance,
                    maxDistance,
                    rolloffFactor),
            looping,
            sequence,
            null);

        if (!_backend.TryRentSource(out var source))
        {
            var victim = FindAllocationVictim(request);
            if (victim is null)
                return CreateRejectedInstance(
                    tracked,
                    looping,
                    volume,
                    pitch,
                    isSpatial,
                    position,
                    referenceDistance,
                    maxDistance,
                    rolloffFactor);
            Terminate(victim, SoundInstanceState.Stolen, stopSource: true);
            if (!_backend.TryRentSource(out source))
                throw new InvalidOperationException("The returned audio source was not available for reuse.");
        }

        var instance = tracked
            ? CreateInstance(
                _instanceOwner,
                looping,
                volume,
                pitch,
                isSpatial,
                position,
                referenceDistance,
                maxDistance,
                rolloffFactor,
                SoundInstanceState.Playing)
            : null;
        var active = new ActiveSound(
            soundEvent,
            source,
            looping,
            isSpatial,
            sequence,
            variantVolume,
            variantPitch,
            volume,
            pitch,
            position,
            referenceDistance,
            maxDistance,
            rolloffFactor,
            instance);
        _active.Add(active);
        _backend.Play(source, variant.Buffer, CreateSettings(active));
        if (categoryPaused)
        {
            _backend.Pause(source);
            active.IsPaused = true;
            instance?.SetPlaybackState(SoundInstanceState.Paused);
        }
        return instance;
    }

    private void CheckPlayRequest(
        SoundEvent soundEvent,
        float volume,
        float pitch,
        bool isSpatial,
        Vector3D<double> position,
        float referenceDistance,
        float maxDistance,
        float rolloffFactor)
    {
        CheckUsable();
        if (!_ready)
            throw new InvalidOperationException("Audio playback is not ready.");
        ArgumentNullException.ThrowIfNull(soundEvent);
        ValidatePlaybackVolume(volume);
        ValidatePlaybackPitch(pitch);
        if (!_eventManager.Contains(soundEvent))
            throw new InvalidOperationException("The sound event is not registered.");
        SoundInstance.ValidateSpatialParameters(
            isSpatial,
            position,
            referenceDistance,
            maxDistance,
            rolloffFactor);
    }

    private ActiveSound? FindAllocationVictim(AllocationCandidate request)
    {
        var worst = request;
        foreach (var active in _active)
        {
            var position = GetPosition(active);
            var candidate = new AllocationCandidate(
                IsOutOfRange(active.IsSpatial, position, GetMaxDistance(active)),
                IsEffectivelyPaused(active)
                    ? 0
                    : EstimateAudibleGain(
                        active.SoundEvent.Category,
                        active.VariantVolume,
                        GetInstanceVolume(active),
                        active.IsSpatial,
                        position,
                        GetReferenceDistance(active),
                        GetMaxDistance(active),
                        GetRolloffFactor(active)),
                active.IsLooping,
                active.Sequence,
                active);
            if (CompareAllocation(candidate, worst) < 0)
                worst = candidate;
        }
        return worst.Active;
    }

    private void Terminate(ActiveSound active, SoundInstanceState state, bool stopSource)
    {
        if (stopSource)
            _backend.Stop(active.Source);
        _backend.ReturnSource(active.Source);
        _active.Remove(active);
        active.Instance?.Complete(state);
    }

    private void UpdateActiveGains(Func<ActiveSound, bool> predicate)
    {
        if (!_backend.IsAvailable)
            return;
        foreach (var active in _active.Where(predicate))
            _backend.Update(active.Source, CreateSettings(active));
    }

    private void SetCategoriesPaused(SoundCategory[] categories, bool paused)
    {
        CheckUsable();
        var validated = ValidateCategories(categories);
        if (validated.Count == 0)
            return;

        foreach (var category in validated)
        {
            if (paused)
                _pausedCategories.Add(category);
            else
                _pausedCategories.Remove(category);
        }

        foreach (var active in _active.Where(active => validated.Contains(active.SoundEvent.Category)))
            ApplyEffectivePause(active);
    }

    private HashSet<SoundCategory> ValidateCategories(SoundCategory[] categories)
    {
        ArgumentNullException.ThrowIfNull(categories);
        var validated = new HashSet<SoundCategory>(ReferenceEqualityComparer.Instance);
        foreach (var category in categories)
        {
            ValidateCategory(category);
            validated.Add(category);
        }
        return validated;
    }

    private void ApplyEffectivePause(ActiveSound active)
    {
        var paused = IsEffectivelyPaused(active);
        if (paused == active.IsPaused)
            return;

        if (paused)
            _backend.Pause(active.Source);
        else
            _backend.Resume(active.Source);
        active.IsPaused = paused;
        active.Instance?.SetPlaybackState(paused
            ? SoundInstanceState.Paused
            : SoundInstanceState.Playing);
    }

    private bool IsEffectivelyPaused(ActiveSound active) =>
        active.InstancePaused || IsCategoryEffectivelyPaused(active.SoundEvent.Category);

    private bool IsCategoryEffectivelyPaused(SoundCategory category) =>
        _pausedCategories.Contains(category) || _isUiPaused && !category.IgnoresUiPause;

    private AudioSourceSettings CreateSettings(ActiveSound active)
    {
        var spatialPosition = GetPosition(active);
        var position = !active.IsSpatial
            ? Vector3D<float>.Zero
            : new Vector3D<float>(
                ToFiniteFloat(spatialPosition.X),
                ToFiniteFloat(spatialPosition.Y),
                ToFiniteFloat(spatialPosition.Z));
        return new AudioSourceSettings(
            IsRelative: !active.IsSpatial,
            IsLooping: active.IsLooping,
            Position: position,
            Gain: CalculateSourceGain(active.SoundEvent.Category, active.VariantVolume, GetInstanceVolume(active)),
            Pitch: CalculatePitch(active.VariantPitch, GetInstancePitch(active)),
            ReferenceDistance: active.IsSpatial ? GetReferenceDistance(active) : 1,
            MaxDistance: active.IsSpatial ? GetMaxDistance(active) : float.MaxValue,
            RolloffFactor: active.IsSpatial ? GetRolloffFactor(active) : 0);
    }

    private float CalculateSourceGain(SoundCategory category, float variantVolume, float instanceVolume) =>
        MultiplyFinite(variantVolume, instanceVolume, _categoryVolumes[category], _masterVolume);

    private float EstimateAudibleGain(
        SoundCategory category,
        float variantVolume,
        float instanceVolume,
        bool isSpatial,
        Vector3D<double> position,
        float referenceDistance,
        float maxDistance,
        float rolloffFactor)
    {
        var sourceGain = CalculateSourceGain(category, variantVolume, instanceVolume);
        if (!isSpatial || rolloffFactor <= 0)
            return sourceGain;

        var distance = Distance(_listener.Position, position);
        var clampedDistance = Math.Clamp(distance, referenceDistance, maxDistance);
        var denominator = referenceDistance
                          + rolloffFactor
                          * (clampedDistance - referenceDistance);
        var attenuation = referenceDistance / denominator;
        return MultiplyFinite(sourceGain, ToFiniteFloat(attenuation));
    }

    private bool IsOutOfRange(bool isSpatial, Vector3D<double> position, float maxDistance) =>
        isSpatial && Distance(_listener.Position, position) > maxDistance;

    private void TryCleanup(Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            LogReleaseFailure(_logger, exception);
        }
    }

    private ActiveSound FindActive(SoundInstance instance) =>
        _active.FirstOrDefault(active => ReferenceEquals(active.Instance, instance))
        ?? throw new InvalidOperationException("The sound instance is not active.");

    private static SoundInstance CreateInstance(
        ISoundInstanceOwner? owner,
        bool looping,
        float volume,
        float pitch,
        bool isSpatial,
        Vector3D<double> position,
        float referenceDistance,
        float maxDistance,
        float rolloffFactor,
        SoundInstanceState state) =>
        new(owner,
            state,
            volume,
            pitch,
            looping,
            isSpatial,
            position,
            referenceDistance,
            maxDistance,
            rolloffFactor);

    private static SoundInstance? CreateRejectedInstance(
        bool tracked,
        bool looping,
        float volume,
        float pitch,
        bool isSpatial,
        Vector3D<double> position,
        float referenceDistance,
        float maxDistance,
        float rolloffFactor) =>
        tracked
            ? CreateInstance(
                null,
                looping,
                volume,
                pitch,
                isSpatial,
                position,
                referenceDistance,
                maxDistance,
                rolloffFactor,
                SoundInstanceState.Rejected)
            : null;

    private static float GetInstanceVolume(ActiveSound active) =>
        active.Instance?.Volume ?? active.InstanceVolume;

    private static float GetInstancePitch(ActiveSound active) =>
        active.Instance?.Pitch ?? active.InstancePitch;

    private static Vector3D<double> GetPosition(ActiveSound active) =>
        active.Instance is not null && active.IsSpatial ? active.Instance.Position : active.Position;

    private static float GetReferenceDistance(ActiveSound active) =>
        active.Instance is not null && active.IsSpatial
            ? active.Instance.ReferenceDistance
            : active.ReferenceDistance;

    private static float GetMaxDistance(ActiveSound active) =>
        active.Instance is not null && active.IsSpatial
            ? active.Instance.MaxDistance
            : active.MaxDistance;

    private static float GetRolloffFactor(ActiveSound active) =>
        active.Instance is not null && active.IsSpatial
            ? active.Instance.RolloffFactor
            : active.RolloffFactor;

    private static float RandomRange(float minimum, float maximum, float value)
    {
        if (value is < 0 or >= 1 || !float.IsFinite(value))
            throw new InvalidOperationException("The audio random source returned a value outside [0, 1).");
        return minimum + (maximum - minimum) * value;
    }

    private static int CompareAllocation(AllocationCandidate left, AllocationCandidate right)
    {
        var result = right.IsOutOfRange.CompareTo(left.IsOutOfRange);
        if (result != 0)
            return result;
        result = left.AudibleGain.CompareTo(right.AudibleGain);
        if (result != 0)
            return result;
        result = right.IsLooping.CompareTo(left.IsLooping);
        if (result != 0)
            return result;
        return left.Sequence.CompareTo(right.Sequence);
    }

    private static float CalculatePitch(float variantPitch, float instancePitch) =>
        (float)Math.Clamp((double)variantPitch * instancePitch, 0.5, 2.0);

    private static float MultiplyFinite(params float[] values)
    {
        double result = 1;
        foreach (var value in values)
            result *= value;
        return result >= float.MaxValue ? float.MaxValue : (float)result;
    }

    private static double Distance(Vector3D<double> first, Vector3D<double> second)
    {
        var x = first.X - second.X;
        var y = first.Y - second.Y;
        var z = first.Z - second.Z;
        return Math.Sqrt(x * x + y * y + z * z);
    }

    private static float ToFiniteFloat(double value) =>
        (float)Math.Clamp(value, float.MinValue, float.MaxValue);

    private void ValidateCategory(SoundCategory category)
    {
        ArgumentNullException.ThrowIfNull(category);
        if (!_categories.TryGetKey(category, out _))
            throw new InvalidOperationException("The sound category is not registered.");
    }

    private static void ValidateMixerVolume(float volume, string parameterName)
    {
        if (!float.IsFinite(volume) || volume is < 0 or > 1)
            throw new ArgumentOutOfRangeException(parameterName, volume, "Mixer volume must be from zero to one.");
    }

    private static void ValidatePlaybackVolume(float volume)
    {
        if (!float.IsFinite(volume) || volume < 0)
            throw new ArgumentOutOfRangeException(nameof(volume), volume, "Volume must be finite and non-negative.");
    }

    private static void ValidatePlaybackPitch(float pitch)
    {
        if (!float.IsFinite(pitch) || pitch <= 0)
            throw new ArgumentOutOfRangeException(nameof(pitch), pitch, "Pitch must be finite and positive.");
    }

    private void CheckUsable()
    {
        CheckThread();
        ObjectDisposedException.ThrowIf(_destroyed, this);
    }

    private void CheckThread()
    {
        if (Environment.CurrentManagedThreadId != _ownerThreadId)
            throw new InvalidOperationException("The audio system must be used from its creating thread.");
    }

    private sealed class ActiveSound(
        SoundEvent soundEvent,
        AudioSourceHandle source,
        bool isLooping,
        bool isSpatial,
        long sequence,
        float variantVolume,
        float variantPitch,
        float instanceVolume,
        float instancePitch,
        Vector3D<double> position,
        float referenceDistance,
        float maxDistance,
        float rolloffFactor,
        SoundInstance? instance)
    {
        internal SoundEvent SoundEvent { get; } = soundEvent;
        internal AudioSourceHandle Source { get; } = source;
        internal bool IsLooping { get; } = isLooping;
        internal bool IsSpatial { get; } = isSpatial;
        internal long Sequence { get; } = sequence;
        internal float VariantVolume { get; } = variantVolume;
        internal float VariantPitch { get; } = variantPitch;
        internal float InstanceVolume { get; } = instanceVolume;
        internal float InstancePitch { get; } = instancePitch;
        internal Vector3D<double> Position { get; } = position;
        internal float ReferenceDistance { get; } = referenceDistance;
        internal float MaxDistance { get; } = maxDistance;
        internal float RolloffFactor { get; } = rolloffFactor;
        internal SoundInstance? Instance { get; } = instance;
        internal bool SettingsDirty { get; set; }
        internal bool InstancePaused { get; set; }
        internal bool IsPaused { get; set; }
    }

    private readonly record struct AllocationCandidate(
        bool IsOutOfRange,
        float AudibleGain,
        bool IsLooping,
        long Sequence,
        ActiveSound? Active);

    private sealed class InstanceOwner(AudioSystem system) : ISoundInstanceOwner
    {
        public void Stop(SoundInstance instance) => system.Stop(instance);
        public void Pause(SoundInstance instance) => system.Pause(instance);
        public void Resume(SoundInstance instance) => system.Resume(instance);
        public void SetVolume(SoundInstance instance) => system.SetVolume(instance);
        public void MarkSettingsDirty(SoundInstance instance) => system.MarkSettingsDirty(instance);
    }
}
