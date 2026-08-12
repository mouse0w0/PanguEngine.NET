using Microsoft.Extensions.Logging.Abstractions;
using PanguEngine.Audio;
using PanguEngine.Audio.Backend;
using PanguEngine.Audio.Decoding;
using PanguEngine.Registries;
using PanguEngine.Resources;
using Silk.NET.Maths;
using System.Text;

namespace PanguEngine.Tests.Audio;

public sealed class AudioSystemTests
{
    [Fact]
    public void AudioSeedDeriver_UsesStableProtocolVectors()
    {
        Assert.Equal(0UL, AudioSeedDeriver.Mix(0));
        Assert.Equal(0xE220A8397B1DCDAFUL, AudioSeedDeriver.Mix(0x9E3779B97F4A7C15UL));
        Assert.Equal(0, AudioSeedDeriver.ToUnitSingle(0x000000FFFFFFFFFFUL));
        Assert.Equal(0.99999994f, AudioSeedDeriver.ToUnitSingle(0xFFFFFF0000000000UL));

        Assert.Equal(1, AudioSeedDeriver.DeriveVariant(0, 2));
        Assert.Equal(0, AudioSeedDeriver.DeriveVariant(unchecked((long)0x61C8864680B583EBUL), 2));
        Assert.Equal(1, AudioSeedDeriver.DeriveVariant(unchecked((long)0x61C8864680B583EBUL), 3));
    }

    [Fact]
    public void AudioSeedDeriver_SeededDomainsAreIndependent()
    {
        const long seed = 123456789;
        var expectedVariant = AudioSeedDeriver.DeriveVariant(seed, 97);
        var expectedVolume = AudioSeedDeriver.DeriveVolume(seed);
        var expectedPitch = AudioSeedDeriver.DerivePitch(seed);

        Assert.Equal(expectedPitch, AudioSeedDeriver.DerivePitch(seed));
        Assert.Equal(expectedVariant, AudioSeedDeriver.DeriveVariant(seed, 97));
        Assert.Equal(expectedVolume, AudioSeedDeriver.DeriveVolume(seed));
    }

    [Fact]
    public void Play_BeforeReady_Throws()
    {
        using var fixture = CreateFixture(sourceCapacity: 1);
        fixture.System.Load();

        Assert.Throws<InvalidOperationException>(() => fixture.System.Play(fixture.SoundEvent));
    }

    [Fact]
    public void NullBackend_ReturnsRejectedTrackedInstance()
    {
        using var fixture = CreateFixture(backend: new NullAudioBackend());
        fixture.System.Load();
        fixture.System.MarkReady();

        var instance = fixture.System.PlayTracked(fixture.SoundEvent);

        Assert.Equal(SoundInstanceState.Rejected, instance.State);
    }

    [Fact]
    public void InitialListenerFailure_UsesNullBackend()
    {
        var backend = new TestAudioBackend(1) { ThrowOnSetListener = true };
        using var fixture = CreateFixture(backend: backend);

        Assert.False(fixture.System.IsAvailable);
        Assert.Equal(1, backend.DestroyCount);
    }

    [Fact]
    public void PlaybackParameters_RejectInvalidValues()
    {
        using var fixture = CreateFixture(sourceCapacity: 1);
        fixture.System.Load();
        fixture.System.MarkReady();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            fixture.System.PlayTracked(fixture.SoundEvent, volume: float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            fixture.System.PlayTracked(fixture.SoundEvent, volume: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            fixture.System.PlayTracked(fixture.SoundEvent, pitch: float.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            fixture.System.PlayTracked(fixture.SoundEvent, pitch: 0));
    }

    [Fact]
    public void SpatialInstance_StoresAndUpdatesSpatialProperties()
    {
        using var fixture = CreateFixture(sourceCapacity: 1);
        fixture.System.Load();
        fixture.System.MarkReady();
        var instance = fixture.System.PlayTrackedAt(
            fixture.SoundEvent,
            new Vector3D<double>(1, 2, 3),
            referenceDistance: 2,
            maxDistance: 8,
            rolloffFactor: 0.5f,
            looping: true);

        Assert.True(instance.IsSpatial);
        Assert.Equal(new Vector3D<double>(1, 2, 3), instance.Position);
        Assert.Equal(2, instance.ReferenceDistance);
        Assert.Equal(8, instance.MaxDistance);
        Assert.Equal(0.5f, instance.RolloffFactor);

        instance.Position = new Vector3D<double>(4, 5, 6);
        instance.ReferenceDistance = 3;
        instance.MaxDistance = 10;
        instance.RolloffFactor = 0.25f;
        fixture.System.Update();

        var backend = fixture.Backend!;
        Assert.Equal(new Vector3D<float>(4, 5, 6), backend.LastSettings.Position);
        Assert.Equal(3, backend.LastSettings.ReferenceDistance);
        Assert.Equal(10, backend.LastSettings.MaxDistance);
        Assert.Equal(0.25f, backend.LastSettings.RolloffFactor);
    }

    [Fact]
    public void NonSpatialInstance_RejectsSpatialProperties()
    {
        using var fixture = CreateFixture(sourceCapacity: 1);
        fixture.System.Load();
        fixture.System.MarkReady();
        var instance = fixture.System.PlayTracked(fixture.SoundEvent);

        Assert.False(instance.IsSpatial);
        Assert.Throws<InvalidOperationException>(() => _ = instance.Position);
        Assert.Throws<InvalidOperationException>(() => instance.Position = Vector3D<double>.Zero);
        Assert.Throws<InvalidOperationException>(() => _ = instance.ReferenceDistance);
        Assert.Throws<InvalidOperationException>(() => instance.ReferenceDistance = 1);
        Assert.Throws<InvalidOperationException>(() => _ = instance.MaxDistance);
        Assert.Throws<InvalidOperationException>(() => instance.MaxDistance = 1);
        Assert.Throws<InvalidOperationException>(() => _ = instance.RolloffFactor);
        Assert.Throws<InvalidOperationException>(() => instance.RolloffFactor = 1);
    }

    [Fact]
    public void SpatialPlayback_RejectsInvalidParameters()
    {
        using var fixture = CreateFixture(sourceCapacity: 1);
        fixture.System.Load();
        fixture.System.MarkReady();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            fixture.System.PlayAt(fixture.SoundEvent, new Vector3D<double>(double.NaN, 0, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            fixture.System.PlayAt(fixture.SoundEvent, Vector3D<double>.Zero, referenceDistance: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            fixture.System.PlayAt(fixture.SoundEvent, Vector3D<double>.Zero, maxDistance: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            fixture.System.PlayAt(fixture.SoundEvent, Vector3D<double>.Zero, rolloffFactor: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            fixture.System.PlayAt(
                fixture.SoundEvent,
                Vector3D<double>.Zero,
                referenceDistance: 2,
                maxDistance: 1));
    }

    [Fact]
    public void Update_CompletesNaturallyStoppedInstance()
    {
        using var fixture = CreateFixture(sourceCapacity: 1);
        fixture.System.Load();
        fixture.System.MarkReady();
        var instance = fixture.System.PlayTracked(fixture.SoundEvent);
        fixture.Backend!.SetAllStates(AudioBackendSourceState.Stopped);

        fixture.System.Update();

        Assert.Equal(SoundInstanceState.Completed, instance.State);
    }

    [Fact]
    public void Stop_IsIdempotentAndRetainsTerminalValues()
    {
        using var fixture = CreateFixture(sourceCapacity: 1);
        fixture.System.Load();
        fixture.System.MarkReady();
        var instance = fixture.System.PlayTracked(fixture.SoundEvent, looping: true);

        instance.Stop();
        instance.Volume = 0.25f;
        instance.Pitch = 1.5f;
        instance.Stop();

        Assert.Equal(SoundInstanceState.Stopped, instance.State);
        Assert.Equal(0.25f, instance.Volume);
        Assert.Equal(1.5f, instance.Pitch);
        Assert.Equal(1, fixture.Backend!.ReturnCount);
    }

    [Fact]
    public void InstancePauseResume_UpdatesStateAndIsIdempotent()
    {
        using var fixture = CreateFixture(sourceCapacity: 1);
        fixture.System.Load();
        fixture.System.MarkReady();
        var instance = fixture.System.PlayTracked(fixture.SoundEvent, looping: true);

        instance.Pause();
        instance.Pause();

        Assert.Equal(SoundInstanceState.Paused, instance.State);
        Assert.True(instance.IsPaused);
        Assert.False(instance.IsPlaying);
        Assert.Equal(1, fixture.Backend!.PauseCount);

        fixture.System.Update();

        instance.Resume();
        instance.Resume();

        Assert.Equal(SoundInstanceState.Playing, instance.State);
        Assert.False(instance.IsPaused);
        Assert.True(instance.IsPlaying);
        Assert.Equal(1, fixture.Backend.ResumeCount);
    }

    [Fact]
    public void PauseReasons_AreIndependent()
    {
        using var fixture = CreateFixture(sourceCapacity: 1);
        fixture.System.Load();
        fixture.System.MarkReady();
        var instance = fixture.System.PlayTracked(fixture.SoundEvent, looping: true);

        fixture.System.PauseCategories(BuiltinSoundCategories.SoundEffects, BuiltinSoundCategories.Ambient);
        instance.Resume();
        fixture.System.ResumeCategories(BuiltinSoundCategories.Ambient);

        Assert.Equal(SoundInstanceState.Paused, instance.State);
        Assert.True(fixture.System.IsCategoryPaused(BuiltinSoundCategories.SoundEffects));

        fixture.System.ResumeCategories(BuiltinSoundCategories.SoundEffects);
        Assert.Equal(SoundInstanceState.Playing, instance.State);

        instance.Pause();
        fixture.System.PauseCategories(BuiltinSoundCategories.SoundEffects);
        fixture.System.ResumeCategories(BuiltinSoundCategories.SoundEffects);

        Assert.Equal(SoundInstanceState.Paused, instance.State);
        instance.Resume();
        Assert.Equal(SoundInstanceState.Playing, instance.State);
    }

    [Fact]
    public void UiPause_PausesOnlyCategoriesThatDoNotIgnoreIt()
    {
        using var pausedFixture = CreateFixture(sourceCapacity: 1);
        pausedFixture.System.Load();
        pausedFixture.System.MarkReady();
        var pausedInstance = pausedFixture.System.PlayTracked(pausedFixture.SoundEvent, looping: true);

        pausedFixture.System.IsUiPaused = true;

        Assert.True(pausedFixture.System.IsUiPaused);
        Assert.True(pausedFixture.System.IsCategoryPaused(BuiltinSoundCategories.SoundEffects));
        Assert.Equal(SoundInstanceState.Paused, pausedInstance.State);

        using var playingFixture = CreateFixture(
            sourceCapacity: 1,
            category: BuiltinSoundCategories.UserInterface);
        playingFixture.System.Load();
        playingFixture.System.MarkReady();
        var playingInstance = playingFixture.System.PlayTracked(playingFixture.SoundEvent, looping: true);

        playingFixture.System.IsUiPaused = true;

        Assert.False(playingFixture.System.IsCategoryPaused(BuiltinSoundCategories.UserInterface));
        Assert.Equal(SoundInstanceState.Playing, playingInstance.State);
        Assert.Equal(0, playingFixture.Backend!.PauseCount);
    }

    [Fact]
    public void UiManualAndInstancePauseReasons_AreIndependent()
    {
        using var fixture = CreateFixture(sourceCapacity: 1);
        fixture.System.Load();
        fixture.System.MarkReady();
        var instance = fixture.System.PlayTracked(fixture.SoundEvent, looping: true);

        fixture.System.IsUiPaused = true;
        fixture.System.PauseCategories(BuiltinSoundCategories.SoundEffects);
        fixture.System.IsUiPaused = false;

        Assert.True(fixture.System.IsCategoryPaused(BuiltinSoundCategories.SoundEffects));
        Assert.Equal(SoundInstanceState.Paused, instance.State);

        fixture.System.ResumeCategories(BuiltinSoundCategories.SoundEffects);
        Assert.False(fixture.System.IsCategoryPaused(BuiltinSoundCategories.SoundEffects));
        Assert.Equal(SoundInstanceState.Playing, instance.State);

        instance.Pause();
        fixture.System.IsUiPaused = true;
        fixture.System.IsUiPaused = false;

        Assert.Equal(SoundInstanceState.Paused, instance.State);
        instance.Resume();
        Assert.Equal(SoundInstanceState.Playing, instance.State);
        Assert.Equal(2, fixture.Backend!.PauseCount);
        Assert.Equal(2, fixture.Backend.ResumeCount);
    }

    [Fact]
    public void UiPauseExemption_DoesNotIgnoreManualCategoryPause()
    {
        using var fixture = CreateFixture(
            sourceCapacity: 1,
            category: BuiltinSoundCategories.UserInterface);
        fixture.System.Load();
        fixture.System.MarkReady();
        var instance = fixture.System.PlayTracked(fixture.SoundEvent, looping: true);

        fixture.System.IsUiPaused = true;
        fixture.System.PauseCategories(BuiltinSoundCategories.UserInterface);
        fixture.System.IsUiPaused = false;

        Assert.True(fixture.System.IsCategoryPaused(BuiltinSoundCategories.UserInterface));
        Assert.Equal(SoundInstanceState.Paused, instance.State);

        fixture.System.ResumeCategories(BuiltinSoundCategories.UserInterface);
        Assert.Equal(SoundInstanceState.Playing, instance.State);
    }

    [Fact]
    public void UiPausedCategory_RejectsOneShotsAndStartsLoopsPaused()
    {
        using var fixture = CreateFixture(sourceCapacity: 1);
        fixture.System.Load();
        fixture.System.MarkReady();
        fixture.System.IsUiPaused = true;

        fixture.System.Play(fixture.SoundEvent);
        Assert.Equal(0, fixture.Backend!.PlayCount);

        var instance = fixture.System.PlayTracked(fixture.SoundEvent, looping: true);

        Assert.Equal(SoundInstanceState.Paused, instance.State);
        Assert.Equal(1, fixture.Backend.PlayCount);
        Assert.Equal(1, fixture.Backend.PauseCount);
    }

    [Fact]
    public void RuntimeBackendFailure_PropagatesWithoutDestroyingBackend()
    {
        using var fixture = CreateFixture(sourceCapacity: 1);
        fixture.System.Load();
        fixture.System.MarkReady();
        var instance = fixture.System.PlayTracked(fixture.SoundEvent, looping: true);
        var backend = fixture.Backend!;
        backend.ThrowOnPause = true;

        Assert.Throws<AudioBackendException>(instance.Pause);

        Assert.True(fixture.System.IsAvailable);
        Assert.Equal(0, backend.DestroyCount);
    }

    [Fact]
    public void PauseCategories_ValidatesAllCategoriesBeforeChangingState()
    {
        using var fixture = CreateFixture();
        var unknown = new SoundCategory();

        Assert.Throws<InvalidOperationException>(() =>
            fixture.System.PauseCategories(BuiltinSoundCategories.SoundEffects, unknown));
        Assert.False(fixture.System.IsCategoryPaused(BuiltinSoundCategories.SoundEffects));
    }

    [Fact]
    public void PausedCategory_RejectsOneShotsAndStartsLoopsPaused()
    {
        using var fixture = CreateFixture(sourceCapacity: 1);
        fixture.System.Load();
        fixture.System.MarkReady();
        fixture.System.PauseCategories(BuiltinSoundCategories.SoundEffects);

        fixture.System.Play(fixture.SoundEvent);
        Assert.Equal(0, fixture.Backend!.PlayCount);

        var instance = fixture.System.PlayTracked(fixture.SoundEvent, looping: true);

        Assert.Equal(SoundInstanceState.Paused, instance.State);
        Assert.True(instance.IsPaused);
        Assert.Equal(1, fixture.Backend.PlayCount);
        Assert.Equal(1, fixture.Backend.PauseCount);

        fixture.System.ResumeCategories(BuiltinSoundCategories.SoundEffects);
        Assert.Equal(SoundInstanceState.Playing, instance.State);
        Assert.Equal(1, fixture.Backend.ResumeCount);
    }

    [Fact]
    public void PausedSource_RemainsInBudgetAndCanBeStolen()
    {
        using var fixture = CreateFixture(sourceCapacity: 1);
        fixture.System.Load();
        fixture.System.MarkReady();
        var first = fixture.System.PlayTracked(fixture.SoundEvent, looping: true);

        first.Pause();
        var second = fixture.System.PlayTracked(fixture.SoundEvent);

        Assert.Equal(SoundInstanceState.Stolen, first.State);
        Assert.Equal(SoundInstanceState.Playing, second.State);
    }

    [Fact]
    public void VolumeChanges_UpdateActiveSourceGainImmediately()
    {
        using var fixture = CreateFixture(sourceCapacity: 1);
        fixture.System.Load();
        fixture.System.MarkReady();
        var instance = fixture.System.PlayTracked(fixture.SoundEvent);

        fixture.System.MasterVolume = 0.5f;
        fixture.System.SetCategoryVolume(BuiltinSoundCategories.SoundEffects, 0.5f);
        instance.Volume = 0.5f;

        Assert.Equal(0.125f, fixture.Backend!.LastSettings.Gain);
    }

    [Fact]
    public void CategoryVolume_AcceptsOnlyRegisteredCategories()
    {
        var category = new SoundCategory();
        using var fixture = CreateFixture(category: category);

        Assert.Equal(1, fixture.System.GetCategoryVolume(category));
        fixture.System.SetCategoryVolume(category, 0.25f);

        Assert.Equal(0.25f, fixture.System.GetCategoryVolume(category));
        Assert.Throws<InvalidOperationException>(() => fixture.System.GetCategoryVolume(new SoundCategory()));
    }

    [Fact]
    public void Play_ClampsEffectivePitchToOpenAlRange()
    {
        using var fixture = CreateFixture(sourceCapacity: 1);
        fixture.System.Load();
        fixture.System.MarkReady();

        _ = fixture.System.PlayTracked(fixture.SoundEvent, pitch: 10);

        Assert.Equal(2, fixture.Backend!.LastSettings.Pitch);
    }

    [Fact]
    public void Play_LocalSeedUsesSameDerivedResultsAsExplicitSeed()
    {
        var random = new RecordingAudioRandom(42);
        using var fixture = CreateFixture(
            sourceCapacity: 2,
            random: random,
            eventJson: """
                {
                  "variants": [
                    {
                      "resource": "sounds/example.ogg",
                      "volume": [0.5, 1.0],
                      "pitch": [1.0, 1.5]
                    }
                  ]
                }
                """);
        fixture.System.Load();
        fixture.System.MarkReady();

        _ = fixture.System.PlayTracked(fixture.SoundEvent);
        var localBuffer = fixture.Backend!.LastBuffer;
        var localSettings = fixture.Backend.LastSettings;

        _ = fixture.System.PlayTracked(fixture.SoundEvent, seed: 42);

        Assert.Equal(localBuffer, fixture.Backend.LastBuffer);
        Assert.Equal(localSettings.Gain, fixture.Backend.LastSettings.Gain);
        Assert.Equal(localSettings.Pitch, fixture.Backend.LastSettings.Pitch);
        Assert.Equal(1, random.CallCount);
    }

    [Fact]
    public void Play_FixedVariantRangesConsumeOneLocalSeed()
    {
        var random = new RecordingAudioRandom(0);
        using var fixture = CreateFixture(sourceCapacity: 1, random: random);
        fixture.System.Load();
        fixture.System.MarkReady();

        _ = fixture.System.PlayTracked(fixture.SoundEvent);

        Assert.Equal(1, random.CallCount);
    }

    [Fact]
    public void Play_SeededPlaybackDoesNotConsumeLocalRandomSource()
    {
        var random = new RecordingAudioRandom(0);
        using var fixture = CreateFixture(
            sourceCapacity: 1,
            random: random,
            eventJson: """
                {
                  "variants": [
                    {
                      "resource": "sounds/example.ogg",
                      "volume": [0.5, 1.0],
                      "pitch": [1.0, 1.5]
                    }
                  ]
                }
                """);
        fixture.System.Load();
        fixture.System.MarkReady();

        _ = fixture.System.PlayTracked(fixture.SoundEvent, seed: 42);

        Assert.Equal(0, random.CallCount);
    }

    [Fact]
    public void Play_SameSeedProducesSameVariantVolumeAndPitch()
    {
        using var fixture = CreateFixture(
            sourceCapacity: 2,
            eventJson: """
                {
                  "variants": [
                    {
                      "resource": "sounds/example.ogg",
                      "weight": 1,
                      "volume": [0.5, 1.0],
                      "pitch": [0.75, 1.25]
                    },
                    {
                      "resource": "sounds/second.ogg",
                      "weight": 2,
                      "volume": [1.0, 1.5],
                      "pitch": [1.25, 1.75]
                    }
                  ]
                }
                """);
        fixture.System.Load();
        fixture.System.MarkReady();

        _ = fixture.System.PlayTracked(fixture.SoundEvent, seed: -1234);
        var firstBuffer = fixture.Backend!.LastBuffer;
        var firstSettings = fixture.Backend.LastSettings;

        _ = fixture.System.PlayTracked(fixture.SoundEvent, seed: -1234);

        Assert.Equal(firstBuffer, fixture.Backend.LastBuffer);
        Assert.Equal(firstSettings.Gain, fixture.Backend.LastSettings.Gain);
        Assert.Equal(firstSettings.Pitch, fixture.Backend.LastSettings.Pitch);
    }

    [Fact]
    public void PlaybackEntrypoints_AcceptSeed()
    {
        using var fixture = CreateFixture(sourceCapacity: 4);
        fixture.System.Load();
        fixture.System.MarkReady();

        fixture.System.Play(fixture.SoundEvent, seed: 1);
        fixture.System.PlayAt(fixture.SoundEvent, Vector3D<double>.Zero, seed: 2);
        _ = fixture.System.PlayTracked(fixture.SoundEvent, seed: 3);
        _ = fixture.System.PlayTrackedAt(fixture.SoundEvent, Vector3D<double>.Zero, seed: 4);

        Assert.Equal(4, fixture.Backend!.PlayCount);
    }

    [Fact]
    public void HigherGainRequest_StealsLowerGainInstance()
    {
        using var fixture = CreateFixture(sourceCapacity: 1);
        fixture.System.Load();
        fixture.System.MarkReady();
        var first = fixture.System.PlayTracked(fixture.SoundEvent, volume: 0.5f);

        var second = fixture.System.PlayTracked(fixture.SoundEvent, volume: 1);

        Assert.Equal(SoundInstanceState.Stolen, first.State);
        Assert.Equal(SoundInstanceState.Playing, second.State);
    }

    [Fact]
    public void LowerGainRequest_IsRejectedWhenCapacityIsFull()
    {
        using var fixture = CreateFixture(sourceCapacity: 1);
        fixture.System.Load();
        fixture.System.MarkReady();
        var first = fixture.System.PlayTracked(fixture.SoundEvent, volume: 1);

        var second = fixture.System.PlayTracked(fixture.SoundEvent, volume: 0.5f);

        Assert.Equal(SoundInstanceState.Playing, first.State);
        Assert.Equal(SoundInstanceState.Rejected, second.State);
    }

    [Fact]
    public void InstanceAccess_FromAnotherThread_Throws()
    {
        using var fixture = CreateFixture(sourceCapacity: 1);
        fixture.System.Load();
        fixture.System.MarkReady();
        var instance = fixture.System.PlayTracked(fixture.SoundEvent);
        Exception? error = null;
        var thread = new Thread(() => error = Record.Exception(instance.Stop));

        thread.Start();
        thread.Join();

        Assert.IsType<InvalidOperationException>(error);
    }

    [Fact]
    public void Destroy_IsIdempotent()
    {
        using var fixture = CreateFixture(sourceCapacity: 1);
        fixture.System.Load();
        fixture.System.MarkReady();

        fixture.System.Destroy();
        fixture.System.Destroy();

        Assert.True(fixture.System.IsDestroyed);
    }

    private static AudioFixture CreateFixture(
        int sourceCapacity = 0,
        IAudioBackend? backend = null,
        SoundCategory? category = null,
        IAudioRandom? random = null,
        string? eventJson = null)
    {
        var selectedCategory = category ?? BuiltinSoundCategories.SoundEffects;
        var categories = new Registry<SoundCategory>(ResourceKey.Parse("test:sound_category"));
        var registeredCategories = new HashSet<SoundCategory>(ReferenceEqualityComparer.Instance)
        {
            selectedCategory,
            BuiltinSoundCategories.SoundEffects,
            BuiltinSoundCategories.Ambient
        };
        var categoryId = 0;
        foreach (var registeredCategory in registeredCategories)
            categories.Register(ResourceKey.Create("test", $"category_{categoryId++}"), registeredCategory);
        categories.Freeze();
        var soundEvent = new SoundEvent(selectedCategory);
        var registry = new Registry<SoundEvent>(ResourceKey.Parse("test:sound_event"));
        registry.Register(ResourceKey.Parse("test:example"), soundEvent);
        registry.Freeze();
        var source = new SingleAudioResourceSource(eventJson ?? """
            { "variants": ["sounds/example.ogg"] }
            """);
        var resources = new ResourceManager([source]);
        var fakeBackend = backend as TestAudioBackend ?? (backend is null ? new TestAudioBackend(sourceCapacity) : null);
        var selectedBackend = backend ?? fakeBackend!;
        var system = new AudioSystem(
            resources,
            categories,
            registry,
            NullLogger.Instance,
            selectedBackend,
            random ?? new FixedAudioRandom(),
            new Dictionary<string, IAudioDecoder>(StringComparer.Ordinal)
            {
                [".ogg"] = new TestAudioDecoder()
            });
        return new AudioFixture(resources, soundEvent, system, fakeBackend);
    }

    private sealed class AudioFixture(
        ResourceManager resources,
        SoundEvent soundEvent,
        AudioSystem system,
        TestAudioBackend? backend) : IDisposable
    {
        internal SoundEvent SoundEvent { get; } = soundEvent;
        internal AudioSystem System { get; } = system;
        internal TestAudioBackend? Backend { get; } = backend;

        public void Dispose()
        {
            System.Destroy();
            resources.Dispose();
        }
    }

    private sealed class SingleAudioResourceSource(string eventJson) : IResourceSource
    {
        private readonly byte[] _eventJson = Encoding.UTF8.GetBytes(eventJson);

        public bool Exists(string resourcePath) => resourcePath is
            "test/sound_events/example.json"
            or "test/sounds/example.ogg"
            or "test/sounds/second.ogg";

        public Stream Open(string resourcePath) => resourcePath.EndsWith(".json", StringComparison.Ordinal)
            ? new MemoryStream(_eventJson, writable: false)
            : new MemoryStream([1], writable: false);
        public IEnumerable<Resource> List(string directoryPath, bool recursive = false) => [];
        public void Dispose() { }
    }

    private sealed class TestAudioDecoder : IAudioDecoder
    {
        public PcmAudioData Decode(Stream stream) => new([0], 1, 44100);
    }

    private sealed class FixedAudioRandom : IAudioRandom
    {
        public long NextSeed() => 0;
    }

    private sealed class RecordingAudioRandom(long seed) : IAudioRandom
    {
        internal int CallCount { get; private set; }

        public long NextSeed()
        {
            CallCount++;
            return seed;
        }
    }

    private sealed class TestAudioBackend : IAudioBackend
    {
        private readonly Queue<AudioSourceHandle> _available;
        private readonly Dictionary<AudioSourceHandle, AudioBackendSourceState> _states = [];
        private uint _nextBuffer = 1;

        internal TestAudioBackend(int capacity)
        {
            _available = new Queue<AudioSourceHandle>(
                Enumerable.Range(1, capacity).Select(static value => new AudioSourceHandle((uint)value)));
            SourceCapacity = capacity;
        }

        public bool IsAvailable => true;
        public int SourceCapacity { get; }
        internal AudioSourceSettings LastSettings { get; private set; }
        internal AudioBufferHandle LastBuffer { get; private set; }
        internal int ReturnCount { get; private set; }
        internal int PlayCount { get; private set; }
        internal int PauseCount { get; private set; }
        internal int ResumeCount { get; private set; }
        internal int DestroyCount { get; private set; }
        internal bool ThrowOnPause { get; set; }
        internal bool ThrowOnSetListener { get; set; }

        public AudioBufferHandle CreateBuffer(PcmAudioData data) => new(_nextBuffer++);
        public void DestroyBuffer(AudioBufferHandle buffer) { }

        public bool TryRentSource(out AudioSourceHandle source)
        {
            if (!_available.TryDequeue(out source))
                return false;
            _states.Add(source, AudioBackendSourceState.Initial);
            return true;
        }

        public void Play(AudioSourceHandle source, AudioBufferHandle buffer, AudioSourceSettings settings)
        {
            PlayCount++;
            LastBuffer = buffer;
            LastSettings = settings;
            _states[source] = AudioBackendSourceState.Playing;
        }

        public void Pause(AudioSourceHandle source)
        {
            if (ThrowOnPause)
                throw new AudioBackendException("Test audio backend pause failure.");
            PauseCount++;
            _states[source] = AudioBackendSourceState.Paused;
        }

        public void Resume(AudioSourceHandle source)
        {
            ResumeCount++;
            _states[source] = AudioBackendSourceState.Playing;
        }

        public void Stop(AudioSourceHandle source) => _states[source] = AudioBackendSourceState.Stopped;

        public void Update(AudioSourceHandle source, AudioSourceSettings settings)
        {
            LastSettings = settings;
        }

        public AudioBackendSourceState GetState(AudioSourceHandle source) => _states[source];

        public void ReturnSource(AudioSourceHandle source)
        {
            ReturnCount++;
            _states.Remove(source);
            _available.Enqueue(source);
        }

        public void SetListener(AudioListenerState listener)
        {
            if (ThrowOnSetListener)
                throw new AudioBackendException("Test audio backend listener failure.");
        }

        public void Destroy() => DestroyCount++;

        internal void SetAllStates(AudioBackendSourceState state)
        {
            foreach (var source in _states.Keys.ToArray())
                _states[source] = state;
        }
    }
}
