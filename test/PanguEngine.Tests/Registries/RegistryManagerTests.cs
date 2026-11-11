using PanguEngine.Registries;

namespace PanguEngine.Tests.Registries;

public sealed class RegistryManagerTests
{
    [Fact]
    public void ConstructorLeavesRegistryCatalogEmpty()
    {
        var manager = new RegistryManager();

        Assert.False(manager.Registries.IsFrozen);
        Assert.Equal(0, manager.Registries.Count);
        Assert.Empty(manager.Registries.Entries);
        Assert.False(manager.Registries.ContainsKey(RegistryKeys.Registry));
        Assert.False(manager.TryGet(RegistryKeys.Registry, out IRegistry? catalog));
        Assert.Null(catalog);
    }

    [Fact]
    public void FreezeAllFreezesRegistryCatalogBeforeRegisteredRegistries()
    {
        var manager = new RegistryManager();
        var registry = new CatalogObservingRegistry(ResourceKey.Parse("pangu:test"), manager);
        manager.Register(registry);

        manager.FreezeAll();

        Assert.True(manager.Registries.IsFrozen);
        Assert.True(registry.CatalogWasFrozenWhenFreezeRan);
        Assert.True(registry.IsFrozen);
    }

    [Fact]
    public void RegisterRejectsRegistryCatalogKey()
    {
        var manager = new RegistryManager();
        var registry = new Registry<object>(RegistryKeys.Registry);

        Assert.Throws<InvalidOperationException>(() => manager.Register(registry));
    }

    [Fact]
    public void FreezeAllBindsCreatedHolder()
    {
        var manager = new RegistryManager();
        var registryKey = ResourceKey.Parse("pangu:test");
        var entryKey = ResourceKey.Parse("pangu:stone");
        var value = new object();
        var registry = new Registry<object>(registryKey);
        registry.Register(entryKey, value);
        manager.Register(registry);

        var holder = manager.CreateHolder<object>(registryKey, entryKey);

        Assert.Equal(new ResourceAddress(registryKey, entryKey), holder.Address);
        Assert.False(holder.IsBound);
        Assert.False(holder.TryGet(out var missing));
        Assert.Null(missing);
        Assert.Throws<InvalidOperationException>(() => holder.Value);

        manager.FreezeAll();

        Assert.True(holder.IsBound);
        Assert.True(holder.TryGet(out var actual));
        Assert.Same(value, actual);
        Assert.Same(value, holder.Value);
        Assert.Equal(new ResourceAddress(registryKey, entryKey), holder.Address);
    }

    [Fact]
    public void FreezeAllDoesNotUseDefaultedRegistryFallback()
    {
        var manager = new RegistryManager();
        var registryKey = ResourceKey.Parse("pangu:test");
        var defaultKey = ResourceKey.Parse("pangu:air");
        var missingKey = ResourceKey.Parse("pangu:missing");
        var registry = new DefaultedRegistry<object>(registryKey, defaultKey);
        registry.Register(defaultKey, new object());
        manager.Register(registry);
        var holder = manager.CreateHolder<object>(registryKey, missingKey);

        Assert.Throws<KeyNotFoundException>(() => manager.FreezeAll());
        Assert.False(holder.IsBound);
    }

    [Fact]
    public void FreezeAllIsReentrantAfterHolderResolution()
    {
        var manager = new RegistryManager();
        var registryKey = ResourceKey.Parse("pangu:test");
        var entryKey = ResourceKey.Parse("pangu:stone");
        var value = new object();
        var registry = new Registry<object>(registryKey);
        registry.Register(entryKey, value);
        manager.Register(registry);
        var holder = manager.CreateHolder<object>(registryKey, entryKey);

        manager.FreezeAll();
        manager.FreezeAll();

        Assert.True(holder.IsBound);
        Assert.Same(value, holder.Value);
    }

    [Fact]
    public void CreateHolderAfterFreezeResolvesImmediately()
    {
        var manager = new RegistryManager();
        var registryKey = ResourceKey.Parse("pangu:test");
        var entryKey = ResourceKey.Parse("pangu:stone");
        var value = new object();
        var registry = new Registry<object>(registryKey);
        registry.Register(entryKey, value);
        manager.Register(registry);
        manager.FreezeAll();

        var holder = manager.CreateHolder<object>(registryKey, entryKey);

        Assert.True(holder.IsBound);
        Assert.Same(value, holder.Value);
    }

    [Fact]
    public void FailedCreateHolderAfterFreezeDoesNotAffectLaterFreezeAll()
    {
        var manager = new RegistryManager();
        var registryKey = ResourceKey.Parse("pangu:test");
        var registry = new Registry<object>(registryKey);
        manager.Register(registry);
        manager.FreezeAll();

        Assert.Throws<KeyNotFoundException>(() =>
            manager.CreateHolder<object>(registryKey, ResourceKey.Parse("pangu:missing")));

        manager.FreezeAll();
    }

    [Fact]
    public void IdTableRegistrationIsExplicitAndClosesWithRegistryFreezing()
    {
        var manager = new RegistryManager();
        var registry = new Registry<object>(ResourceKey.Parse("test:content"));
        manager.Register(registry);
        Assert.Empty(manager.IdTables);
        manager.FreezeAll();

        Assert.Throws<InvalidOperationException>(() => manager.RegisterIdTable(new RegistryIdTable<object>(registry)));
    }

    [Fact]
    public void DuplicateIdTableKeysAreRejectedBeforeFreezing()
    {
        var manager = new RegistryManager();
        var registry = new Registry<object>(ResourceKey.Parse("test:content"));
        manager.Register(registry);
        manager.RegisterIdTable(new RegistryIdTable<object>(registry));

        Assert.Throws<InvalidOperationException>(() => manager.RegisterIdTable(new RegistryIdTable<object>(registry)));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("extra")]
    [InlineData("duplicate")]
    [InlineData("replacement")]
    public void BindIdTablesRejectsMismatchedTableSets(string difference)
    {
        var manager = new RegistryManager();
        var table = RegisterTable(manager, "content", new object());
        manager.FreezeAll();
        manager.AllocateIdTables();
        var expected = table.ExportMapping();
        var other = new RegistryIdMapping(ResourceKey.Parse("test:other"), []);
        RegistryIdMapping[] mappings = difference switch
        {
            "missing" => [],
            "extra" => [expected, other],
            "duplicate" => [expected, expected],
            "replacement" => [other],
            _ => throw new InvalidOperationException()
        };

        Assert.Throws<InvalidOperationException>(() => manager.BindIdTables(new RegistryIdMappings(mappings)));
    }

    [Fact]
    public void CustomTablesParticipateInAllGroupOperationsRegardlessOfRegistrationOrder()
    {
        var server = new RegistryManager();
        var serverValue = new object();
        var serverObjects = RegisterTable(server, "objects", serverValue);
        RegisterTable(server, "strings", "value");
        server.FreezeAll();
        server.AllocateIdTables();

        var client = new RegistryManager();
        var text = "value";
        var strings = RegisterTable(client, "strings", text);
        var value = new object();
        var objects = RegisterTable(client, "objects", value);
        client.FreezeAll();
        Assert.Empty(objects.Entries);
        Assert.Empty(strings.Entries);
        client.BindIdTables(server.ExportIdMappings());

        Assert.Same(text, strings.Get(0));
        Assert.Same(value, objects.Get(0));
        client.ClearIdTables();
        Assert.False(objects.TryGetId(value, out _));
        Assert.False(strings.TryGetId(text, out _));
        Assert.Same(objects, client.IdTables[objects.Key]);
        Assert.Same(serverValue, serverObjects.Get(0));
    }

    [Fact]
    public void EmptyIdTableDirectorySupportsGroupOperations()
    {
        var manager = new RegistryManager();
        manager.FreezeAll();
        manager.AllocateIdTables();
        var mappings = manager.ExportIdMappings();
        manager.BindIdTables(mappings);
        manager.ClearIdTables();

        Assert.Empty(mappings.Tables);
    }

    private static RegistryIdTable<T> RegisterTable<T>(RegistryManager manager, string name, T value) where T : class
    {
        var registry = new Registry<T>(ResourceKey.Create("test", name));
        registry.Register(ResourceKey.Parse("test:value"), value);
        var table = new RegistryIdTable<T>(registry);
        manager.Register(registry);
        manager.RegisterIdTable(table);
        return table;
    }

    private sealed class CatalogObservingRegistry(ResourceKey key, RegistryManager manager) : Registry<object>(key)
    {
        public bool CatalogWasFrozenWhenFreezeRan { get; private set; }

        public override void Freeze()
        {
            CatalogWasFrozenWhenFreezeRan = manager.Registries.IsFrozen;
            base.Freeze();
        }
    }
}
