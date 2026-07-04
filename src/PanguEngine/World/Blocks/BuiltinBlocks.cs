using PanguEngine.Registries;

namespace PanguEngine.World.Blocks;

/// <summary>
/// Provides the built-in block definitions.
/// </summary>
public static class BuiltinBlocks
{
    /// <summary>The built-in air block.</summary>
    public static Block Air { get; } = new AirBlock();

    /// <summary>The built-in stone block.</summary>
    public static Block Stone { get; } = new();

    /// <summary>The built-in grass block.</summary>
    public static Block Grass { get; } = new();

    /// <summary>The built-in dirt block.</summary>
    public static Block Dirt { get; } = new();

    /// <summary>
    /// Registers the built-in blocks in the specified registry.
    /// </summary>
    /// <param name="registry">The registry to populate.</param>
    internal static void Register(IWritableRegistry<Block> registry)
    {
        registry.Register(ResourceKey.Create("pangu", "air"), Air);
        registry.Register(ResourceKey.Create("pangu", "stone"), Stone);
        registry.Register(ResourceKey.Create("pangu", "grass"), Grass);
        registry.Register(ResourceKey.Create("pangu", "dirt"), Dirt);
    }
}