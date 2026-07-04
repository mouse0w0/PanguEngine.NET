using PanguEngine.World.Blocks;

namespace PanguEngine.Registries;

/// <summary>
/// Registers built-in engine registries.
/// </summary>
public static class BuiltinRegistries
{
    /// <summary>The built-in block registry.</summary>
    public static readonly DefaultedRegistry<Block> Block =
        new(RegistryKeys.Block, ResourceKey.Create("pangu", "air"));

    /// <summary>
    /// Registers the built-in registries in the specified registry manager.
    /// </summary>
    /// <param name="manager">The registry manager to populate.</param>
    internal static void Register(RegistryManager manager)
    {
        manager.Register(Block);
    }
}