using PanguEngine.Client.UI.Rendering;
using PanguEngine.Graphics;

namespace PanguEngine.Tests.Client.UI;

public sealed class UiTextureTableTests
{
    [Fact]
    public void ConstructorFullyInitializesEveryFrameDescriptorSet()
    {
        var device = new UiTestGraphicsDevice();
        var layout = new UiTestDescriptorSetLayout(default);
        var table = new UiTextureTable(device, layout, 2);

        Assert.Equal(2, device.DescriptorSets.Count);
        Assert.All(device.DescriptorSets, descriptorSet =>
        {
            Assert.Equal(258, descriptorSet.Description.Bindings.Length);
            var images = descriptorSet.Description.Bindings
                .Where(binding => binding.Type == DescriptorType.SampledImage)
                .ToArray();
            Assert.Equal(256, images.Length);
            Assert.Equal(Enumerable.Range(0, 256).Select(index => (uint)index),
                images.Select(binding => binding.ArrayElement));
            Assert.Single(images.Select(binding => binding.TextureView).Distinct());
            Assert.Equal(2, descriptorSet.Description.Bindings.Count(
                binding => binding.Type == DescriptorType.Sampler));
        });

        table.DestroyDescriptorSets();
        table.DestroyOwnedResources();
    }

    [Fact]
    public void RegisteredSlotKeepsFallbackUntilPublishedOnCurrentFrame()
    {
        var device = new UiTestGraphicsDevice();
        var table = new UiTextureTable(device, new UiTestDescriptorSetLayout(default), 2);
        var view = device.CreateSampledView();

        Assert.True(table.TryRegister(view, out var slot));
        Assert.Equal(0u, slot.Index);
        Assert.Empty(device.DescriptorSets[0].Updates);
        Assert.Empty(device.DescriptorSets[1].Updates);

        table.SynchronizeFrame(0);

        Assert.Empty(device.DescriptorSets[0].Updates);
        Assert.Same(
            device.TextureViews[0],
            device.DescriptorSets[0].Bindings[(0, slot.Index)].TextureView);
        table.Publish(slot);
        table.SynchronizeFrame(0);

        var update = Assert.Single(Assert.Single(device.DescriptorSets[0].Updates));
        Assert.Equal((0u, 0u), (update.Binding, update.ArrayElement));
        Assert.Same(view, update.TextureView);
        Assert.Empty(device.DescriptorSets[1].Updates);
        table.DestroyDescriptorSets();
        table.DestroyOwnedResources();
    }

    [Fact]
    public void RetiringSlotReturnsToPoolAfterEveryFrameUsesFallback()
    {
        var device = new UiTestGraphicsDevice();
        var table = new UiTextureTable(device, new UiTestDescriptorSetLayout(default), 2);
        Assert.True(table.TryRegister(device.CreateSampledView(), out var retiring));
        table.Publish(retiring);
        table.SynchronizeFrame(0);
        table.SynchronizeFrame(1);
        var releaseCount = 0;

        table.Retire(retiring, () => releaseCount++);
        table.SynchronizeFrame(0);
        Assert.Equal(0, releaseCount);
        Assert.True(table.TryRegister(device.CreateSampledView(), out var whileRetiring));
        Assert.Equal(1u, whileRetiring.Index);

        table.SynchronizeFrame(1);
        Assert.Equal(1, releaseCount);
        Assert.True(table.TryRegister(device.CreateSampledView(), out var reused));
        Assert.Equal(0u, reused.Index);
        table.DestroyDescriptorSets();
        table.DestroyOwnedResources();
    }

    [Fact]
    public void DescriptorUpdateFailureDoesNotAdvanceAppliedGeneration()
    {
        var device = new UiTestGraphicsDevice();
        var table = new UiTextureTable(device, new UiTestDescriptorSetLayout(default), 1);
        Assert.True(table.TryRegister(device.CreateSampledView(), out var slot));
        table.Publish(slot);
        var expected = new InvalidOperationException("update");
        device.DescriptorUpdateException = expected;

        Assert.Same(expected, Assert.Throws<InvalidOperationException>(() => table.SynchronizeFrame(0)));
        device.DescriptorUpdateException = null;
        table.SynchronizeFrame(0);

        Assert.Single(device.DescriptorSets[0].Updates);
        table.DestroyDescriptorSets();
        table.DestroyOwnedResources();
    }

    [Fact]
    public void ResourceCapacityIncludesSlotsZeroThrough255()
    {
        var device = new UiTestGraphicsDevice();
        var table = new UiTextureTable(device, new UiTestDescriptorSetLayout(default), 1);

        for (var index = 0; index < 256; index++)
        {
            Assert.True(table.TryRegister(device.CreateSampledView(), out var slot));
            Assert.Equal((uint)index, slot.Index);
        }

        Assert.False(table.TryRegister(device.CreateSampledView(), out _));
        table.DestroyDescriptorSets();
        table.DestroyOwnedResources();
    }

    [Fact]
    public void DestroyPhasesAreIdempotentAndOrderedByCaller()
    {
        var device = new UiTestGraphicsDevice();
        var table = new UiTextureTable(device, new UiTestDescriptorSetLayout(default), 2);

        table.DestroyDescriptorSets();
        table.DestroyDescriptorSets();
        Assert.All(device.DescriptorSets, descriptorSet => Assert.True(descriptorSet.IsDestroyed));
        Assert.All(device.Samplers, sampler => Assert.False(sampler.IsDestroyed));

        table.DestroyOwnedResources();
        table.DestroyOwnedResources();
        Assert.All(device.Samplers, sampler => Assert.True(sampler.IsDestroyed));
        Assert.True(device.TextureViews[0].IsDestroyed);
        Assert.True(device.Textures[0].IsDestroyed);
    }
}
