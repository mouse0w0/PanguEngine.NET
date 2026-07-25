using PanguEngine.Client.Resources.Models;
using PanguEngine.Registries;
using PanguEngine.World.Blocks;
using PanguEngine.World.Chunking;

namespace PanguEngine.Tests.Client.Resources.Models;

public sealed class BakedBlockAppearanceTests
{
    [Fact]
    public void SelectsVariantFromCanonicalBlockState()
    {
        var powered = BlockProperty.CreateBoolean("powered");
        var block = new Block(powered);
        var off = new BakedBlockModel([], [], []);
        var on = new BakedBlockModel([], [], []);
        var onState = block.DefaultState.With(powered, true);
        var appearance = new BakedBlockAppearance(
            ResourceKey.Create("test", "machine"),
            new Dictionary<BlockState, IReadOnlyList<BakedBlockAppearanceEntry>>
            {
                [block.DefaultState] = [new BakedBlockAppearanceEntry(off, 1)],
                [onState] = [new BakedBlockAppearanceEntry(on, 1)]
            });

        Assert.Same(off, appearance.Get(block.DefaultState, default));
        Assert.Same(on, appearance.Get(onState, default));
    }

    [Fact]
    public void SelectsWeightedModelFromWorldPosition()
    {
        var block = new Block();
        var first = new BakedBlockModel([], [], []);
        var second = new BakedBlockModel([], [], []);
        var appearance = CreateAppearance(
            ResourceKey.Create("pangu", "stone"),
            block,
            [
                new BakedBlockAppearanceEntry(first, 1),
                new BakedBlockAppearanceEntry(second, 1)
            ]);

        Assert.Same(first, appearance.Get(block.DefaultState, new BlockPos(32, 0, 32)));
        Assert.Same(second, appearance.Get(block.DefaultState, new BlockPos(48, 0, 32)));
        Assert.Same(first, appearance.Get(block.DefaultState, new BlockPos(32, 0, 32)));
    }

    [Fact]
    public void SelectsWeightedBoundary()
    {
        var block = new Block();
        var first = new BakedBlockModel([], [], []);
        var second = new BakedBlockModel([], [], []);
        var appearance = CreateAppearance(
            ResourceKey.Create("pangu", "stone"),
            block,
            [
                new BakedBlockAppearanceEntry(first, 3),
                new BakedBlockAppearanceEntry(second, 1)
            ]);

        Assert.Same(first, appearance.Get(block.DefaultState, new BlockPos(32, 0, 32)));
        Assert.Same(second, appearance.Get(block.DefaultState, new BlockPos(48, 0, 32)));
    }

    [Fact]
    public void SelectsWeightedModelAtNegativePosition()
    {
        var block = new Block();
        var first = new BakedBlockModel([], [], []);
        var second = new BakedBlockModel([], [], []);
        var appearance = CreateAppearance(
            ResourceKey.Create("test", "block"),
            block,
            [
                new BakedBlockAppearanceEntry(first, 1),
                new BakedBlockAppearanceEntry(second, 3)
            ]);
        var position = new BlockPos(-1, 2, -3);

        Assert.Same(second, appearance.Get(block.DefaultState, position));
        Assert.Same(second, appearance.Get(block.DefaultState, position));
    }

    private static BakedBlockAppearance CreateAppearance(
        ResourceKey blockKey,
        Block block,
        IReadOnlyList<BakedBlockAppearanceEntry> entries)
    {
        return new BakedBlockAppearance(
            blockKey,
            new Dictionary<BlockState, IReadOnlyList<BakedBlockAppearanceEntry>>
            {
                [block.DefaultState] = entries
            });
    }
}