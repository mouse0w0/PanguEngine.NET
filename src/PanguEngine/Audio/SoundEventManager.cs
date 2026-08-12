using Microsoft.Extensions.Logging;
using PanguEngine.Audio.Backend;
using PanguEngine.Audio.Decoding;
using PanguEngine.Registries;
using PanguEngine.Resources;

namespace PanguEngine.Audio;

internal sealed class SoundEventManager
{
    private static readonly Action<ILogger, ResourceKey, string, Exception?> LogResourceLoadFailure =
        LoggerMessage.Define<ResourceKey, string>(
            LogLevel.Warning,
            new EventId(1, nameof(LogResourceLoadFailure)),
            "Audio resource '{ResourceKey}' referenced by sound events {SoundEvents} could not be loaded");

    private static readonly Action<ILogger, ResourceKey, string, Exception?> LogUnavailableEvent =
        LoggerMessage.Define<ResourceKey, string>(
            LogLevel.Warning,
            new EventId(2, nameof(LogUnavailableEvent)),
            "Sound event '{SoundEvent}' has no available {PlaybackMode} variants");

    private static readonly Action<ILogger, ResourceKey, SoundCategory, Exception?> LogUnregisteredCategory =
        LoggerMessage.Define<ResourceKey, SoundCategory>(
            LogLevel.Warning,
            new EventId(3, nameof(LogUnregisteredCategory)),
            "Sound event '{SoundEvent}' uses unregistered sound category '{SoundCategory}' and is unavailable");

    private static readonly Action<ILogger, ResourceKey, ResourceKey, Exception?> LogDefinitionLoadFailure =
        LoggerMessage.Define<ResourceKey, ResourceKey>(
            LogLevel.Warning,
            new EventId(4, nameof(LogDefinitionLoadFailure)),
            "Sound event '{SoundEvent}' definition '{Definition}' could not be loaded and the event is unavailable");

    private readonly ResourceManager _resources;
    private readonly IRegistry<SoundCategory> _categories;
    private readonly IRegistry<SoundEvent> _events;
    private readonly IAudioBackend _backend;
    private readonly IReadOnlyDictionary<string, IAudioDecoder> _decoders;
    private readonly ILogger _logger;
    private readonly HashSet<SoundEvent> _warned2D = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<SoundEvent> _warned3D = new(ReferenceEqualityComparer.Instance);
    private Dictionary<SoundEvent, BakedSoundEvent>? _snapshot;
    private HashSet<AudioBufferHandle>? _buffers;
    private bool _destroyed;

    internal SoundEventManager(
        ResourceManager resources,
        IRegistry<SoundCategory> categories,
        IRegistry<SoundEvent> events,
        IAudioBackend backend,
        IReadOnlyDictionary<string, IAudioDecoder> decoders,
        ILogger logger)
    {
        _resources = resources;
        _categories = categories;
        _events = events;
        _backend = backend;
        _decoders = decoders;
        _logger = logger;
    }

    internal void Load()
    {
        ObjectDisposedException.ThrowIf(_destroyed, this);
        if (_snapshot is not null)
            throw new InvalidOperationException("Sound events are already loaded.");
        if (!_categories.IsFrozen)
            throw new InvalidOperationException("The sound category registry must be frozen before loading.");
        if (!_events.IsFrozen)
            throw new InvalidOperationException("The sound event registry must be frozen before loading.");

        var bakedEvents = new Dictionary<SoundEvent, BakedSoundEvent>(ReferenceEqualityComparer.Instance);
        var definitions = new Dictionary<SoundEvent, IReadOnlyList<SoundVariant>>(ReferenceEqualityComparer.Instance);
        var definitionLoader = new JsonSoundEventLoader(_resources);
        foreach (var entry in _events.Entries)
        {
            if (!_categories.TryGetKey(entry.Value.Category, out _))
            {
                LogUnregisteredCategory(_logger, entry.Key, entry.Value.Category, null);
                bakedEvents.Add(entry.Value, new BakedSoundEvent(entry.Key));
                continue;
            }

            bakedEvents.Add(entry.Value, new BakedSoundEvent(entry.Key));
            try
            {
                definitions.Add(entry.Value, definitionLoader.Load(entry.Key));
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException)
            {
                LogDefinitionLoadFailure(
                    _logger,
                    entry.Key,
                    JsonSoundEventLoader.GetDefinitionKey(entry.Key),
                    exception);
            }
        }

        if (!_backend.IsAvailable)
        {
            _snapshot = bakedEvents;
            _buffers = [];
            return;
        }

        var references = CollectReferences(definitions, bakedEvents);
        var loadedResources = new Dictionary<ResourceKey, LoadedSoundResource>();
        try
        {
            foreach (var pair in references)
            {
                try
                {
                    var extension = Path.GetExtension(pair.Key.Path).ToLowerInvariant();
                    if (!_decoders.TryGetValue(extension, out var decoder))
                        throw new InvalidDataException($"No audio decoder is registered for extension '{extension}'.");

                    using var stream = _resources.Open(pair.Key);
                    var data = decoder.Decode(stream);
                    var twoDimensionalBuffer = _backend.CreateBuffer(data);
                    var threeDimensionalBuffer = twoDimensionalBuffer;
                    try
                    {
                        if (data.Channels == 2)
                            threeDimensionalBuffer = _backend.CreateBuffer(DownmixToMono(data));
                    }
                    catch
                    {
                        _backend.DestroyBuffer(twoDimensionalBuffer);
                        throw;
                    }
                    loadedResources.Add(
                        pair.Key,
                        new LoadedSoundResource(twoDimensionalBuffer, threeDimensionalBuffer));
                }
                catch (Exception exception) when (IsContentFailure(exception))
                {
                    LogResourceLoadFailure(
                        _logger,
                        pair.Key,
                        string.Join(", ", pair.Value.Select(static reference => reference.EventKey)),
                        exception);
                }
            }
        }
        catch
        {
            foreach (var buffer in CollectBuffers(loadedResources.Values))
                _backend.DestroyBuffer(buffer);
            throw;
        }

        foreach (var pair in references)
        {
            if (!loadedResources.TryGetValue(pair.Key, out var resource))
                continue;
            foreach (var reference in pair.Value)
            {
                var bakedEvent = bakedEvents[reference.SoundEvent];
                var twoDimensionalVariant = new BakedSoundVariant(
                    resource.TwoDimensionalBuffer,
                    reference.Variant.Weight,
                    reference.Variant.MinVolume,
                    reference.Variant.MaxVolume,
                    reference.Variant.MinPitch,
                    reference.Variant.MaxPitch);
                var threeDimensionalVariant = twoDimensionalVariant with
                {
                    Buffer = resource.ThreeDimensionalBuffer
                };
                bakedEvent.Add(twoDimensionalVariant, threeDimensionalVariant);
            }
        }

        _snapshot = bakedEvents;
        _buffers = CollectBuffers(loadedResources.Values);
    }

    internal bool Contains(SoundEvent soundEvent)
    {
        ObjectDisposedException.ThrowIf(_destroyed, this);
        return GetSnapshot().ContainsKey(soundEvent);
    }

    internal bool TrySelect(
        SoundEvent soundEvent,
        bool spatial,
        long seed,
        out BakedSoundVariant selected)
    {
        ObjectDisposedException.ThrowIf(_destroyed, this);
        if (!GetSnapshot().TryGetValue(soundEvent, out var bakedEvent))
            throw new InvalidOperationException("The sound event is not registered.");

        var variants = spatial ? bakedEvent.ThreeDimensional : bakedEvent.TwoDimensional;
        if (variants.Count == 0)
        {
            var warned = spatial ? _warned3D : _warned2D;
            if (warned.Add(soundEvent))
                LogUnavailableEvent(_logger, bakedEvent.Key, spatial ? "3D" : "2D", null);
            selected = default;
            return false;
        }

        var totalWeight = spatial
            ? bakedEvent.ThreeDimensionalTotalWeight
            : bakedEvent.TwoDimensionalTotalWeight;
        var value = AudioSeedDeriver.DeriveVariant(seed, totalWeight);
        long cumulativeWeight = 0;
        foreach (var variant in variants)
        {
            cumulativeWeight += variant.Weight;
            if (value < cumulativeWeight)
            {
                selected = variant;
                return true;
            }
        }

        throw new InvalidOperationException("The audio random source returned a value outside the requested range.");
    }

    internal void Destroy()
    {
        if (_destroyed)
            return;
        _destroyed = true;
        AudioBackendException? failure = null;
        if (_buffers is not null)
        {
            foreach (var buffer in _buffers)
            {
                try
                {
                    _backend.DestroyBuffer(buffer);
                }
                catch (AudioBackendException exception)
                {
                    failure ??= exception;
                }
            }
            _buffers.Clear();
        }
        _snapshot?.Clear();
        _warned2D.Clear();
        _warned3D.Clear();
        if (failure is not null)
            throw failure;
    }

    private static Dictionary<ResourceKey, List<SoundResourceReference>> CollectReferences(
        IReadOnlyDictionary<SoundEvent, IReadOnlyList<SoundVariant>> definitions,
        IReadOnlyDictionary<SoundEvent, BakedSoundEvent> bakedEvents)
    {
        var references = new Dictionary<ResourceKey, List<SoundResourceReference>>();
        foreach (var definition in definitions)
        {
            foreach (var variant in definition.Value)
            {
                if (!references.TryGetValue(variant.Resource, out var resourceReferences))
                {
                    resourceReferences = [];
                    references.Add(variant.Resource, resourceReferences);
                }
                resourceReferences.Add(new SoundResourceReference(
                    bakedEvents[definition.Key].Key,
                    definition.Key,
                    variant));
            }
        }
        return references;
    }

    private Dictionary<SoundEvent, BakedSoundEvent> GetSnapshot() =>
        _snapshot ?? throw new InvalidOperationException("Sound events have not been loaded.");

    private static bool IsContentFailure(Exception exception) =>
        exception is IOException or InvalidDataException or ArgumentException;

    private static PcmAudioData DownmixToMono(PcmAudioData stereo)
    {
        var samples = new short[stereo.Samples.Length / 2];
        for (var source = 0; source < stereo.Samples.Length; source += 2)
            samples[source / 2] = (short)((stereo.Samples[source] + stereo.Samples[source + 1]) / 2);
        return new PcmAudioData(samples, 1, stereo.SampleRate);
    }

    private static HashSet<AudioBufferHandle> CollectBuffers(IEnumerable<LoadedSoundResource> resources) =>
        resources
            .SelectMany(static resource => new[]
            {
                resource.TwoDimensionalBuffer,
                resource.ThreeDimensionalBuffer
            })
            .ToHashSet();

    private sealed class BakedSoundEvent(ResourceKey key)
    {
        internal ResourceKey Key { get; } = key;
        internal List<BakedSoundVariant> TwoDimensional { get; } = [];
        internal List<BakedSoundVariant> ThreeDimensional { get; } = [];
        internal long TwoDimensionalTotalWeight { get; private set; }
        internal long ThreeDimensionalTotalWeight { get; private set; }

        internal void Add(BakedSoundVariant twoDimensional, BakedSoundVariant threeDimensional)
        {
            TwoDimensional.Add(twoDimensional);
            TwoDimensionalTotalWeight += twoDimensional.Weight;
            ThreeDimensional.Add(threeDimensional);
            ThreeDimensionalTotalWeight += threeDimensional.Weight;
        }
    }

    private readonly record struct LoadedSoundResource(
        AudioBufferHandle TwoDimensionalBuffer,
        AudioBufferHandle ThreeDimensionalBuffer);
    private readonly record struct SoundResourceReference(
        ResourceKey EventKey,
        SoundEvent SoundEvent,
        SoundVariant Variant);
}

internal readonly record struct BakedSoundVariant(
    AudioBufferHandle Buffer,
    int Weight,
    float MinVolume,
    float MaxVolume,
    float MinPitch,
    float MaxPitch);
