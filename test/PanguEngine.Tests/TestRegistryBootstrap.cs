using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using PanguEngine.Audio;
using PanguEngine.Client.UI.Huds;
using PanguEngine.Input;
using PanguEngine.Registries;
using PanguEngine.World.Blocks;
using PanguEngine.World.Entities;

namespace PanguEngine.Tests;

internal static class TestRegistryBootstrap
{
    internal static readonly object CustomIdValue = new();
    internal static readonly Block StatefulBlock = new(
        BlockProperty.CreateBoolean("powered"), BlockProperty.CreateInteger("level", 0, 2));

    internal static RegistryManager Manager { get; private set; } = null!;

    internal static RegistryIdTable<object> CustomIdTable { get; private set; } = null!;

    [ModuleInitializer]
    internal static void Initialize()
    {
        if (AssemblyLoadContext.GetLoadContext(typeof(TestRegistryBootstrap).Assembly) != AssemblyLoadContext.Default)
            return;

        Manager = new RegistryManager();
        BuiltinRegistries.Register(Manager);
        BuiltinIdTables.Register(Manager);
        var custom = new Registry<object>(ResourceKey.Parse("test:server_ids"));
        custom.Register(ResourceKey.Parse("test:value"), CustomIdValue);
        CustomIdTable = new RegistryIdTable<object>(custom);
        Manager.Register(custom);
        Manager.RegisterIdTable(CustomIdTable);
        BuiltinHuds.Register(BuiltinRegistries.Hud);
        BuiltinBlocks.Register(BuiltinRegistries.Block);
        BuiltinRegistries.Block.Register(ResourceKey.Parse("test:stateful_block"), StatefulBlock);
        BuiltinSoundCategories.Register(BuiltinRegistries.SoundCategory);
        BuiltinSoundEvents.Register(BuiltinRegistries.SoundEvent);
        BuiltinInputActions.Register(BuiltinRegistries.InputAction);
        BuiltinInputContexts.Register(BuiltinRegistries.InputContext);
        BuiltinEntityComponents.Register(BuiltinRegistries.EntityComponentType);
        BuiltinEntityTypes.Register(BuiltinRegistries.EntityType);
        Manager.FreezeAll();
        typeof(Engine).GetProperty(nameof(Engine.RegistryManager))!.SetValue(null, Manager);
    }
}
