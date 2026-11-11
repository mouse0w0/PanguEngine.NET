using PanguEngine.Registries;

namespace PanguEngine.Tests.Registries;

public sealed class RegistryTests
{
    [Fact]
    public void RegisterValidatesCandidateBeforeWriting()
    {
        var key = ResourceKey.Parse("pangu:candidate");
        var value = new object();
        IRegistry<object>? observedRegistry = null;
        var calls = 0;
        var registry = new Registry<object>(ResourceKey.Parse("pangu:test"), (current, candidateKey, candidate) =>
        {
            observedRegistry = current;
            calls++;
            Assert.Equal(key, candidateKey);
            Assert.Same(value, candidate);
            Assert.Empty(current.Entries);
            Assert.False(current.ContainsKey(candidateKey));
            Assert.False(current.TryGetKey(candidate, out _));
        });

        registry.Register(key, value);

        Assert.Same(registry, observedRegistry);
        Assert.Equal(1, calls);
        Assert.Same(value, registry.Get(key));
    }

    [Fact]
    public void RegisterRejectsInvalidKeyBeforeCallingValidation()
    {
        var calls = 0;
        var registry = new Registry<object>(ResourceKey.Parse("pangu:test"), (_, _, _) => calls++);

        Assert.Throws<ArgumentException>(() => registry.Register(default!, new object()));

        Assert.Equal(0, calls);
    }

    [Fact]
    public void RegisterPropagatesValidationFailureWithoutReservingIdentity()
    {
        var failure = new InvalidOperationException("Rejected candidate.");
        var reject = true;
        var registry = new Registry<object>(ResourceKey.Parse("pangu:test"), (_, _, _) =>
        {
            if (reject)
                throw failure;
        });
        var key = ResourceKey.Parse("pangu:candidate");
        var value = new object();

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => registry.Register(key, value)));
        Assert.Empty(registry.Entries);

        reject = false;
        var entry = registry.Register(key, value);

        Assert.Equal(key, entry.Key);
        Assert.Same(value, registry.Get(key));
        Assert.Equal(key, registry.GetKey(value));
    }

    [Fact]
    public void GetReturnsValuesByKey()
    {
        var registry = CreateRegistry();
        var value = new object();
        registry.Register(ResourceKey.Parse("pangu:stone"), value);

        Assert.Same(value, registry.Get(ResourceKey.Parse("pangu:stone")));
    }

    [Fact]
    public void RegisterRejectsDuplicateKey()
    {
        var registry = CreateRegistry();
        var key = ResourceKey.Parse("pangu:stone");

        registry.Register(key, new object());

        Assert.Throws<InvalidOperationException>(() => registry.Register(key, new object()));
    }

    [Fact]
    public void RegisterRejectsNullValue()
    {
        var registry = CreateRegistry();

        Assert.Throws<ArgumentNullException>(() => registry.Register(ResourceKey.Parse("pangu:null"), null!));
    }

    [Fact]
    public void GetKeyReturnsRegisteredIdentity()
    {
        var registry = CreateRegistry();
        var key = ResourceKey.Parse("pangu:stone");
        var value = new object();
        registry.Register(key, value);

        Assert.Equal(key, registry.GetKey(value));
    }

    [Fact]
    public void TryGetKeyReturnsRegisteredIdentity()
    {
        var registry = CreateRegistry();
        var key = ResourceKey.Parse("pangu:stone");
        var value = new object();
        registry.Register(key, value);

        Assert.True(registry.TryGetKey(value, out var actualKey));
        Assert.Equal(key, actualKey);
    }

    [Fact]
    public void ReverseLookupUsesReferenceEquality()
    {
        var registry = new Registry<ValueEqualResource>(ResourceKey.Parse("pangu:test"));
        var registered = new ValueEqualResource(7);
        var equalButDifferent = new ValueEqualResource(7);
        var key = ResourceKey.Parse("pangu:stone");
        registry.Register(key, registered);

        Assert.Equal(key, registry.GetKey(registered));
        Assert.False(registry.TryGetKey(equalButDifferent, out var actualKey));
        Assert.Equal(default, actualKey);
    }

    [Fact]
    public void RegisterRejectsDuplicateValueInstance()
    {
        var registry = CreateRegistry();
        var value = new object();
        registry.Register(ResourceKey.Parse("pangu:first"), value);

        Assert.Throws<InvalidOperationException>(() =>
            registry.Register(ResourceKey.Parse("pangu:second"), value));
    }

    [Fact]
    public void GetKeyRejectsNullValue()
    {
        var registry = CreateRegistry();

        Assert.Throws<ArgumentNullException>(() => registry.GetKey(null!));
    }

    [Fact]
    public void GetKeyRejectsUnregisteredValue()
    {
        var registry = CreateRegistry();

        Assert.Throws<KeyNotFoundException>(() => registry.GetKey(new object()));
    }

    [Fact]
    public void TryGetKeyRejectsNullAndUnregisteredValues()
    {
        var registry = CreateRegistry();

        Assert.False(registry.TryGetKey(null!, out var nullKey));
        Assert.Equal(default, nullKey);
        Assert.False(registry.TryGetKey(new object(), out var missingKey));
        Assert.Equal(default, missingKey);
    }

    [Fact]
    public void FreezePreventsFurtherRegistration()
    {
        var registry = CreateRegistry();
        registry.Freeze();

        Assert.True(registry.IsFrozen);
        Assert.Throws<InvalidOperationException>(() =>
            registry.Register(ResourceKey.Parse("pangu:stone"), new object()));
    }

    [Fact]
    public void TryGetUsesNullableContractShape()
    {
        var registry = CreateRegistry();

        Assert.False(registry.TryGet(ResourceKey.Parse("pangu:missing"), out var value));
        Assert.Null(value);
        Assert.False(registry.TryGetEntry(ResourceKey.Parse("pangu:missing"), out var entry));
        Assert.Null(entry);
    }

    [Fact]
    public void EntriesKeepRegistrationOrderAndCannotMutateRegistry()
    {
        var registry = CreateRegistry();
        registry.Register(ResourceKey.Parse("pangu:first"), new object());
        registry.Register(ResourceKey.Parse("pangu:second"), new object());

        Assert.Equal(["pangu:first", "pangu:second"], registry.Entries.Select(entry => entry.Key.ToString()).ToArray());
        Assert.False(registry.Entries is List<RegistryEntry<object>>);
    }

    [Fact]
    public void RegistryCanBeManagedThroughNonGenericInterface()
    {
        var registry = CreateRegistry();
        IRegistry managed = registry;

        Assert.Equal(ResourceKey.Parse("pangu:test"), managed.Key);
        Assert.Equal(typeof(object), managed.ValueType);
    }

    private sealed class ValueEqualResource(int value)
    {
        private int Value { get; } = value;

        public override bool Equals(object? obj) => obj is ValueEqualResource other && Value == other.Value;

        public override int GetHashCode() => Value;
    }

    private static Registry<object> CreateRegistry() => new(ResourceKey.Parse("pangu:test"));
}
