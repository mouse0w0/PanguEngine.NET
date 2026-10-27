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
    internal static RegistryManager Manager { get; private set; } = null!;

    [ModuleInitializer]
    internal static void Initialize()
    {
        if (AssemblyLoadContext.GetLoadContext(typeof(TestRegistryBootstrap).Assembly) != AssemblyLoadContext.Default)
            return;

        Manager = new RegistryManager();
        BuiltinRegistries.Register(Manager);
        BuiltinHuds.Register(BuiltinRegistries.Hud);
        BuiltinBlocks.Register(BuiltinRegistries.Block);
        BuiltinSoundCategories.Register(BuiltinRegistries.SoundCategory);
        BuiltinSoundEvents.Register(BuiltinRegistries.SoundEvent);
        BuiltinInputActions.Register(BuiltinRegistries.InputAction);
        BuiltinInputContexts.Register(BuiltinRegistries.InputContext);
        BuiltinEntityComponents.Register(BuiltinRegistries.EntityComponentType);
        BuiltinEntityTypes.Register(BuiltinRegistries.EntityType);
        Manager.FreezeAll();
    }
}