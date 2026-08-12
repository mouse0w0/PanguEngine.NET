using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using PanguEngine.Audio;
using PanguEngine.Audio.Backend;
using PanguEngine.Audio.Decoding;
using PanguEngine.Registries;
using PanguEngine.Resources;

namespace PanguEngine.Tests.Audio;

public sealed class SoundEventManagerTests
{
    [Fact]
    public void JsonLoader_LoadsStringAndObjectVariants()
    {
        var source = CreateTextSource(("test/sound_events/block/break.json", """
            {
              "variants": [
                "sounds/first.ogg",
                {
                  "resource": "other:sounds/second.ogg",
                  "weight": 2,
                  "volume": [0.5, 0.75],
                  "pitch": 1.25
                }
              ]
            }
            """));
        using var resources = new ResourceManager([source]);

        var variants = new JsonSoundEventLoader(resources)
            .Load(ResourceKey.Parse("test:block/break"));

        Assert.Collection(
            variants,
            variant =>
            {
                Assert.Equal(ResourceKey.Parse("test:sounds/first.ogg"), variant.Resource);
                Assert.Equal(1, variant.Weight);
                Assert.Equal(1, variant.MinVolume);
                Assert.Equal(1, variant.MaxVolume);
                Assert.Equal(1, variant.MinPitch);
                Assert.Equal(1, variant.MaxPitch);
            },
            variant =>
            {
                Assert.Equal(ResourceKey.Parse("other:sounds/second.ogg"), variant.Resource);
                Assert.Equal(2, variant.Weight);
                Assert.Equal(0.5f, variant.MinVolume);
                Assert.Equal(0.75f, variant.MaxVolume);
                Assert.Equal(1.25f, variant.MinPitch);
                Assert.Equal(1.25f, variant.MaxPitch);
            });
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"variants\":[]}")]
    [InlineData("{\"Variants\":[\"sounds/a.ogg\"]}")]
    [InlineData("{\"variants\":[\"sounds/a.ogg\"],\"extra\":true}")]
    [InlineData("{\"variants\":[null]}")]
    [InlineData("{\"variants\":[{}]}")]
    [InlineData("{\"variants\":[{\"resource\":\"sounds/a.ogg\",\"extra\":true}]}")]
    [InlineData("{\"variants\":[{\"resource\":\"sounds/a.OGG\"}]}")]
    [InlineData("{\"variants\":[{\"resource\":\"sounds/a.ogg\",\"weight\":0}]}")]
    [InlineData("{\"variants\":[{\"resource\":\"sounds/a.ogg\",\"volume\":[1]}]}")]
    [InlineData("{\"variants\":[{\"resource\":\"sounds/a.ogg\",\"volume\":[\"x\",1]}]}")]
    [InlineData("{\"variants\":[{\"resource\":\"sounds/a.ogg\",\"volume\":[1,0]}]}")]
    [InlineData("{\"variants\":[{\"resource\":\"sounds/a.ogg\",\"pitch\":0}]}")]
    public void JsonLoader_RejectsInvalidDefinition(string json)
    {
        var source = CreateTextSource(("test/sound_events/event.json", json));
        using var resources = new ResourceManager([source]);

        var exception = Assert.Throws<InvalidDataException>(() =>
            new JsonSoundEventLoader(resources).Load(ResourceKey.Parse("test:event")));

        Assert.Contains("test:event", exception.Message);
        Assert.Contains("test:sound_events/event.json", exception.Message);
    }

    [Fact]
    public void JsonLoader_UsesHighestPriorityDefinitionWithoutFallback()
    {
        var invalidHighPriority = CreateTextSource(("test/sound_events/event.json", "{}"));
        var validLowPriority = CreateTextSource(("test/sound_events/event.json", """
            { "variants": ["sounds/example.ogg"] }
            """));
        using var resources = new ResourceManager([invalidHighPriority, validLowPriority]);

        Assert.Throws<InvalidDataException>(() =>
            new JsonSoundEventLoader(resources).Load(ResourceKey.Parse("test:event")));
        Assert.Equal(1, invalidHighPriority.GetOpenCount("test/sound_events/event.json"));
        Assert.Equal(0, validLowPriority.GetOpenCount("test/sound_events/event.json"));
    }

    [Fact]
    public void Load_DeduplicatesStereoResourceAndCreatesMonoSpatialBuffer()
    {
        var first = new SoundEvent(BuiltinSoundCategories.SoundEffects);
        var second = new SoundEvent(BuiltinSoundCategories.SoundEffects);
        var registry = CreateRegistry(("first", first), ("second", second));
        var source = CreateSource(
            ("test/sound_events/first.json", """
                { "variants": ["sounds/stereo.ogg"] }
                """),
            ("test/sound_events/second.json", """
                { "variants": ["sounds/stereo.ogg"] }
                """),
            ("test/sounds/stereo.ogg", new byte[] { 2 }));
        using var resources = new ResourceManager([source]);
        var decoder = new TestAudioDecoder();
        var backend = new TestAudioBackend();
        var manager = CreateManager(resources, registry, backend, decoder);

        manager.Load();

        Assert.Equal(1, decoder.DecodeCount);
        Assert.Equal(2, backend.CreatedBuffers.Count);
        Assert.Equal(2, backend.CreatedData[0].Channels);
        Assert.Equal(new short[] { 3000, -1000, 1000, 3000 }, backend.CreatedData[0].Samples);
        Assert.Equal(1, backend.CreatedData[1].Channels);
        Assert.Equal(new short[] { 1000, 2000 }, backend.CreatedData[1].Samples);
        Assert.True(manager.TrySelect(
            first,
            spatial: false,
            seed: 0,
            out var selected));
        Assert.Equal(backend.CreatedBuffers[0], selected.Buffer);
        Assert.True(manager.TrySelect(
            second,
            spatial: true,
            seed: 0,
            out selected));
        Assert.Equal(backend.CreatedBuffers[1], selected.Buffer);

        manager.Destroy();

        Assert.Equal(backend.CreatedBuffers.ToHashSet(), backend.DestroyedBuffers.ToHashSet());
    }

    [Fact]
    public void Load_ReusesMonoBufferForTwoDimensionalAndSpatialPlayback()
    {
        var soundEvent = new SoundEvent(BuiltinSoundCategories.SoundEffects);
        var registry = CreateRegistry(("event", soundEvent));
        var source = CreateSource(
            ("test/sound_events/event.json", """
                { "variants": ["sounds/mono.ogg"] }
                """),
            ("test/sounds/mono.ogg", new byte[] { 1 }));
        using var resources = new ResourceManager([source]);
        var backend = new TestAudioBackend();
        var manager = CreateManager(resources, registry, backend, new TestAudioDecoder());

        manager.Load();

        Assert.Single(backend.CreatedBuffers);
        Assert.True(manager.TrySelect(
            soundEvent,
            spatial: false,
            seed: 0,
            out var twoDimensional));
        Assert.True(manager.TrySelect(
            soundEvent,
            spatial: true,
            seed: 0,
            out var threeDimensional));
        Assert.Equal(twoDimensional.Buffer, threeDimensional.Buffer);
    }

    [Fact]
    public void Load_NullBackendValidatesJsonWithoutOpeningOgg()
    {
        var soundEvent = new SoundEvent(BuiltinSoundCategories.SoundEffects);
        var registry = CreateRegistry(("event", soundEvent));
        var source = CreateSource(
            ("test/sound_events/event.json", """
                { "variants": ["sounds/example.ogg"] }
                """),
            ("test/sounds/example.ogg", new byte[] { 1 }));
        using var resources = new ResourceManager([source]);
        var manager = new SoundEventManager(
            resources,
            CreateCategoryRegistry(("sound_effects", BuiltinSoundCategories.SoundEffects)),
            registry,
            new NullAudioBackend(),
            new Dictionary<string, IAudioDecoder>(StringComparer.Ordinal) { [".ogg"] = new TestAudioDecoder() },
            NullLogger.Instance);

        manager.Load();

        Assert.Equal(1, source.GetOpenCount("test/sound_events/event.json"));
        Assert.Equal(0, source.GetOpenCount("test/sounds/example.ogg"));
        Assert.True(manager.Contains(soundEvent));
    }

    [Fact]
    public void Load_NullBackendInvalidJsonLeavesEventUnavailable()
    {
        var soundEvent = new SoundEvent(BuiltinSoundCategories.SoundEffects);
        var registry = CreateRegistry(("event", soundEvent));
        var source = CreateTextSource(("test/sound_events/event.json", "{}"));
        using var resources = new ResourceManager([source]);
        var manager = new SoundEventManager(
            resources,
            CreateCategoryRegistry(("sound_effects", BuiltinSoundCategories.SoundEffects)),
            registry,
            new NullAudioBackend(),
            new Dictionary<string, IAudioDecoder>(StringComparer.Ordinal),
            NullLogger.Instance);

        manager.Load();

        Assert.Equal(1, source.GetOpenCount("test/sound_events/event.json"));
        Assert.True(manager.Contains(soundEvent));
        Assert.False(manager.TrySelect(
            soundEvent,
            spatial: false,
            seed: 0,
            out _));
    }

    [Fact]
    public void Load_MissingJsonDisablesOnlyThatEvent()
    {
        var soundEvent = new SoundEvent(BuiltinSoundCategories.SoundEffects);
        var registry = CreateRegistry(("event", soundEvent));
        var source = CreateTextSource();
        using var resources = new ResourceManager([source]);
        var manager = CreateManager(resources, registry, new TestAudioBackend(), new TestAudioDecoder());

        manager.Load();

        Assert.True(manager.Contains(soundEvent));
        Assert.False(manager.TrySelect(
            soundEvent,
            spatial: false,
            seed: 0,
            out _));
    }

    [Fact]
    public void Load_UnregisteredCategoryDisablesOnlyThatEvent()
    {
        var unavailable = new SoundEvent(new SoundCategory());
        var available = new SoundEvent(BuiltinSoundCategories.SoundEffects);
        var registry = CreateRegistry(("unavailable", unavailable), ("available", available));
        var source = CreateSource(
            ("test/sound_events/unavailable.json", """
                { "variants": ["sounds/unavailable.ogg"] }
                """),
            ("test/sound_events/available.json", """
                { "variants": ["sounds/available.ogg"] }
                """),
            ("test/sounds/available.ogg", new byte[] { 1 }));
        using var resources = new ResourceManager([source]);
        var backend = new TestAudioBackend();
        var manager = CreateManager(resources, registry, backend, new TestAudioDecoder());

        manager.Load();

        Assert.Equal(0, source.GetOpenCount("test/sound_events/unavailable.json"));
        Assert.False(manager.TrySelect(
            unavailable,
            spatial: false,
            seed: 0,
            out _));
        Assert.True(manager.TrySelect(
            available,
            spatial: false,
            seed: 0,
            out _));
    }

    [Fact]
    public void Load_ContinuesWhenOneOggVariantFails()
    {
        var soundEvent = new SoundEvent(BuiltinSoundCategories.SoundEffects);
        var registry = CreateRegistry(("event", soundEvent));
        var source = CreateSource(
            ("test/sound_events/event.json", """
                { "variants": ["sounds/bad.ogg", "sounds/good.ogg"] }
                """),
            ("test/sounds/bad.ogg", new byte[] { 0 }),
            ("test/sounds/good.ogg", new byte[] { 1 }));
        using var resources = new ResourceManager([source]);
        var backend = new TestAudioBackend();
        var manager = CreateManager(resources, registry, backend, new TestAudioDecoder());

        manager.Load();

        Assert.Single(backend.CreatedBuffers);
        Assert.True(manager.TrySelect(
            soundEvent,
            spatial: false,
            seed: 0,
            out _));
    }

    [Fact]
    public void Load_InvalidJsonDisablesOnlyThatEvent()
    {
        var unavailable = new SoundEvent(BuiltinSoundCategories.SoundEffects);
        var available = new SoundEvent(BuiltinSoundCategories.SoundEffects);
        var registry = CreateRegistry(("unavailable", unavailable), ("available", available));
        var source = CreateSource(
            ("test/sound_events/unavailable.json", "{}"),
            ("test/sound_events/available.json", """
                { "variants": ["sounds/available.ogg"] }
                """),
            ("test/sounds/available.ogg", new byte[] { 1 }));
        using var resources = new ResourceManager([source]);
        var manager = CreateManager(resources, registry, new TestAudioBackend(), new TestAudioDecoder());

        manager.Load();

        Assert.False(manager.TrySelect(
            unavailable,
            spatial: false,
            seed: 0,
            out _));
        Assert.True(manager.TrySelect(
            available,
            spatial: false,
            seed: 0,
            out _));
    }

    [Fact]
    public void TrySelect_UsesLongWeightRangeAndPreservesVariantRanges()
    {
        var soundEvent = new SoundEvent(BuiltinSoundCategories.SoundEffects);
        var registry = CreateRegistry(("event", soundEvent));
        var source = CreateSource(
            ("test/sound_events/event.json", $$"""
                {
                  "variants": [
                    { "resource": "sounds/first.ogg", "weight": 1 },
                    {
                      "resource": "sounds/second.ogg",
                      "weight": {{int.MaxValue}},
                      "volume": [0.25, 0.75],
                      "pitch": [0.5, 1.5]
                    }
                  ]
                }
                """),
            ("test/sounds/first.ogg", new byte[] { 1 }),
            ("test/sounds/second.ogg", new byte[] { 1 }));
        using var resources = new ResourceManager([source]);
        var backend = new TestAudioBackend();
        var manager = CreateManager(resources, registry, backend, new TestAudioDecoder());
        manager.Load();

        Assert.True(manager.TrySelect(
            soundEvent,
            spatial: false,
            seed: 0,
            out var selected));

        Assert.Equal(backend.CreatedBuffers[1], selected.Buffer);
        Assert.Equal(0.25f, selected.MinVolume);
        Assert.Equal(0.75f, selected.MaxVolume);
        Assert.Equal(0.5f, selected.MinPitch);
        Assert.Equal(1.5f, selected.MaxPitch);
    }

    private static Registry<SoundEvent> CreateRegistry(params (string Key, SoundEvent Event)[] values)
    {
        var registry = new Registry<SoundEvent>(ResourceKey.Parse("test:sound_event"));
        foreach (var value in values)
            registry.Register(ResourceKey.Create("test", value.Key), value.Event);
        registry.Freeze();
        return registry;
    }

    private static Registry<SoundCategory> CreateCategoryRegistry(
        params (string Key, SoundCategory Category)[] values)
    {
        var registry = new Registry<SoundCategory>(ResourceKey.Parse("test:sound_category"));
        foreach (var value in values)
            registry.Register(ResourceKey.Create("test", value.Key), value.Category);
        registry.Freeze();
        return registry;
    }

    private static SoundEventManager CreateManager(
        ResourceManager resources,
        IRegistry<SoundEvent> registry,
        IAudioBackend backend,
        IAudioDecoder decoder) =>
        new(resources, CreateCategoryRegistry(("sound_effects", BuiltinSoundCategories.SoundEffects)), registry, backend,
            new Dictionary<string, IAudioDecoder>(StringComparer.Ordinal) { [".ogg"] = decoder },
            NullLogger.Instance);

    private static AudioResourceSource CreateTextSource(params (string Path, string Content)[] entries) =>
        new(entries.ToDictionary(
            static entry => entry.Path,
            static entry => Encoding.UTF8.GetBytes(entry.Content),
            StringComparer.Ordinal));

    private static AudioResourceSource CreateSource(
        (string Path, string Content) first,
        (string Path, string Content) second,
        params (string Path, byte[] Content)[] binaryEntries)
    {
        var data = binaryEntries.ToDictionary(
            static entry => entry.Path,
            static entry => entry.Content,
            StringComparer.Ordinal);
        data.Add(first.Path, Encoding.UTF8.GetBytes(first.Content));
        data.Add(second.Path, Encoding.UTF8.GetBytes(second.Content));
        return new AudioResourceSource(data);
    }

    private static AudioResourceSource CreateSource(
        (string Path, string Content) json,
        params (string Path, byte[] Content)[] binaryEntries)
    {
        var data = binaryEntries.ToDictionary(
            static entry => entry.Path,
            static entry => entry.Content,
            StringComparer.Ordinal);
        data.Add(json.Path, Encoding.UTF8.GetBytes(json.Content));
        return new AudioResourceSource(data);
    }

    private sealed class TestAudioDecoder : IAudioDecoder
    {
        public int DecodeCount { get; private set; }

        public PcmAudioData Decode(Stream stream)
        {
            DecodeCount++;
            var marker = stream.ReadByte();
            return marker switch
            {
                0 => throw new InvalidDataException("Invalid test audio."),
                2 => new PcmAudioData([3000, -1000, 1000, 3000], 2, 44100),
                _ => new PcmAudioData([0], 1, 44100)
            };
        }
    }

    private sealed class AudioResourceSource(IReadOnlyDictionary<string, byte[]> data) : IResourceSource
    {
        private readonly Dictionary<string, int> _openCounts = new(StringComparer.Ordinal);

        public bool Exists(string resourcePath) => data.ContainsKey(resourcePath);

        public Stream Open(string resourcePath)
        {
            _openCounts[resourcePath] = GetOpenCount(resourcePath) + 1;
            return new MemoryStream(data[resourcePath], writable: false);
        }

        public int GetOpenCount(string resourcePath) =>
            _openCounts.GetValueOrDefault(resourcePath);

        public IEnumerable<Resource> List(string directoryPath, bool recursive = false) => [];
        public void Dispose() { }
    }

    private sealed class TestAudioBackend : IAudioBackend
    {
        private uint _nextBuffer = 1;
        public List<AudioBufferHandle> CreatedBuffers { get; } = [];
        public List<PcmAudioData> CreatedData { get; } = [];
        public List<AudioBufferHandle> DestroyedBuffers { get; } = [];
        public bool IsAvailable => true;
        public int SourceCapacity => 0;

        public AudioBufferHandle CreateBuffer(PcmAudioData data)
        {
            var buffer = new AudioBufferHandle(_nextBuffer++);
            CreatedBuffers.Add(buffer);
            CreatedData.Add(data);
            return buffer;
        }

        public void DestroyBuffer(AudioBufferHandle buffer) => DestroyedBuffers.Add(buffer);
        public bool TryRentSource(out AudioSourceHandle source) { source = default; return false; }
        public void Play(AudioSourceHandle source, AudioBufferHandle buffer, AudioSourceSettings settings) { }
        public void Pause(AudioSourceHandle source) { }
        public void Resume(AudioSourceHandle source) { }
        public void Stop(AudioSourceHandle source) { }
        public void Update(AudioSourceHandle source, AudioSourceSettings settings) { }
        public AudioBackendSourceState GetState(AudioSourceHandle source) => AudioBackendSourceState.Stopped;
        public void ReturnSource(AudioSourceHandle source) { }
        public void SetListener(AudioListenerState listener) { }
        public void Destroy() { }
    }
}
