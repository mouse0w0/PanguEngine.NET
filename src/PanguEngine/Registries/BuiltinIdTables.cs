using PanguEngine.Audio;
using PanguEngine.World.Blocks;
using PanguEngine.World.Entities;

namespace PanguEngine.Registries;

/// <summary>
/// Provides the fixed built-in identifier tables for the current session.
/// </summary>
public static class BuiltinIdTables
{
    /// <summary>The current block identifiers.</summary>
    public static readonly RegistryIdTable<Block> Block = new(BuiltinRegistries.Block);

    /// <summary>The current entity type identifiers.</summary>
    public static readonly RegistryIdTable<EntityType> EntityType = new(BuiltinRegistries.EntityType);

    /// <summary>The current entity component type identifiers.</summary>
    public static readonly RegistryIdTable<ComponentType> EntityComponentType = new(BuiltinRegistries.EntityComponentType);

    /// <summary>The current sound category identifiers.</summary>
    public static readonly RegistryIdTable<SoundCategory> SoundCategory = new(BuiltinRegistries.SoundCategory);

    /// <summary>The current sound event identifiers.</summary>
    public static readonly RegistryIdTable<SoundEvent> SoundEvent = new(BuiltinRegistries.SoundEvent);

    internal static void Register(RegistryManager manager)
    {
        manager.RegisterIdTable(Block);
        manager.RegisterIdTable(EntityType);
        manager.RegisterIdTable(EntityComponentType);
        manager.RegisterIdTable(SoundCategory);
        manager.RegisterIdTable(SoundEvent);
    }
}
