using PanguEngine.Graphics;
using PanguEngine.Graphics.Vulkan;
using Xunit;
using VkDescriptorType = Silk.NET.Vulkan.DescriptorType;

namespace PanguEngine.Tests.Graphics.Vulkan;

public sealed class VulkanDescriptorSetBindingStateTests
{
    private static readonly DescriptorSetLayoutBinding[] Layout =
    [
        new(0, DescriptorType.SampledImage, ShaderStageFlags.Fragment, 2),
        new(1, DescriptorType.Sampler, ShaderStageFlags.Fragment)
    ];

    private sealed class TestTextureView : TextureView
    {
        public override Texture Texture => new TestTexture();
        public override TextureViewDimension Dimension => TextureViewDimension.Type2D;
        public override TextureFormat Format => TextureFormat.R8G8B8A8Srgb;
        public override uint Width => 1;
        public override uint Height => 1;
        public override uint Depth => 1;
        public override uint BaseMipLevel => 0;
        public override uint MipLevels => 1;
        public override uint BaseArrayLayer => 0;
        public override uint ArrayLayers => 1;
        public override void Destroy() => MarkDestroyed();
    }

    private sealed class TestTexture : Texture
    {
        public override TextureDimension Dimension => TextureDimension.Type2D;
        public override TextureFormat Format => TextureFormat.R8G8B8A8Srgb;
        public override uint Width => 1;
        public override uint Height => 1;
        public override uint Depth => 1;
        public override uint MipLevels => 1;
        public override uint ArrayLayers => 1;
        public override TextureUsage Usage => TextureUsage.Sampled;
        public override TextureCreateFlags CreateFlags => TextureCreateFlags.None;
        public override void Destroy() => MarkDestroyed();
    }

    private sealed class TestSampler : Sampler
    {
        public override void Destroy() => MarkDestroyed();
    }

    private static DescriptorSetBinding SampledImage(uint element, TextureView view) =>
        DescriptorSetBinding.SampledImage(0, element, view);

    private static DescriptorSetBinding SamplerBinding(Sampler sampler) =>
        DescriptorSetBinding.SamplerDescriptor(1, sampler);

    [Fact]
    public void DescriptorPoolSizesSumArrayCountsByType()
    {
        var sizes = VulkanDescriptorSet.CreateDescriptorPoolSizes(
        [
            new DescriptorSetLayoutBinding(0, DescriptorType.SampledImage, ShaderStageFlags.Fragment, 256),
            new DescriptorSetLayoutBinding(1, DescriptorType.Sampler, ShaderStageFlags.Fragment),
            new DescriptorSetLayoutBinding(2, DescriptorType.Sampler, ShaderStageFlags.Fragment)
        ]);

        Assert.Equal(256u, Assert.Single(sizes.Where(size =>
            size.Type == VkDescriptorType.SampledImage)).DescriptorCount);
        Assert.Equal(2u, Assert.Single(sizes.Where(size =>
            size.Type == VkDescriptorType.Sampler)).DescriptorCount);
    }

    [Fact]
    public void CompleteStateAcceptsEveryArrayElementExactlyOnce()
    {
        var first = new TestTextureView();
        var second = new TestTextureView();
        var sampler = new TestSampler();
        var state = new VulkanDescriptorSetBindingState(Layout,
        [
            SampledImage(0, first),
            SampledImage(1, second),
            SamplerBinding(sampler)
        ]);

        Assert.Equal(3, state.Bindings.Count);
    }

    [Fact]
    public void SparseUpdatePreservesUnchangedElements()
    {
        var first = new TestTextureView();
        var second = new TestTextureView();
        var replacement = new TestTextureView();
        var sampler = new TestSampler();
        var state = new VulkanDescriptorSetBindingState(Layout,
        [
            SampledImage(0, first),
            SampledImage(1, second),
            SamplerBinding(sampler)
        ]);

        var candidate = state.CreateUpdatedBindings([SampledImage(1, replacement)]);

        Assert.Same(first, candidate[0].TextureView);
        Assert.Same(replacement, candidate[1].TextureView);
    }

    [Fact]
    public void ConstructorMissingArrayElementThrows()
    {
        var first = new TestTextureView();
        var sampler = new TestSampler();

        Assert.Throws<ArgumentException>(() => new VulkanDescriptorSetBindingState(Layout,
        [
            SampledImage(0, first),
            SamplerBinding(sampler)
        ]));
    }

    [Fact]
    public void ConstructorRejectsDuplicateArrayElement()
    {
        var first = new TestTextureView();
        var second = new TestTextureView();
        var sampler = new TestSampler();

        Assert.Throws<ArgumentException>(() => new VulkanDescriptorSetBindingState(Layout,
        [
            SampledImage(0, first),
            SampledImage(0, second),
            SamplerBinding(sampler)
        ]));
    }

    [Fact]
    public void ConstructorRejectsTypeMismatch()
    {
        var first = new TestTextureView();
        var second = new TestTextureView();
        var sampler = new TestSampler();

        Assert.Throws<ArgumentException>(() => new VulkanDescriptorSetBindingState(Layout,
        [
            SampledImage(0, first),
            SampledImage(1, second),
            DescriptorSetBinding.SampledImage(1, 0, first)
        ]));
    }

    [Fact]
    public void DuplicateArrayElementInSparseUpdateThrows()
    {
        var first = new TestTextureView();
        var second = new TestTextureView();
        var sampler = new TestSampler();
        var state = new VulkanDescriptorSetBindingState(Layout,
        [
            SampledImage(0, first),
            SampledImage(1, second),
            SamplerBinding(sampler)
        ]);

        Assert.Throws<ArgumentException>(() => state.CreateUpdatedBindings(
        [
            SampledImage(0, first),
            SampledImage(0, first)
        ]));
    }

    [Fact]
    public void OutOfRangeArrayElementThrows()
    {
        var first = new TestTextureView();
        var second = new TestTextureView();
        var sampler = new TestSampler();
        var state = new VulkanDescriptorSetBindingState(Layout,
        [
            SampledImage(0, first),
            SampledImage(1, second),
            SamplerBinding(sampler)
        ]);

        Assert.Throws<ArgumentOutOfRangeException>(() => state.CreateUpdatedBindings(
        [
            SampledImage(2, first)
        ]));
    }

    [Fact]
    public void TypeMismatchInSparseUpdateThrows()
    {
        var first = new TestTextureView();
        var second = new TestTextureView();
        var sampler = new TestSampler();
        var state = new VulkanDescriptorSetBindingState(Layout,
        [
            SampledImage(0, first),
            SampledImage(1, second),
            SamplerBinding(sampler)
        ]);

        Assert.Throws<ArgumentException>(() => state.CreateUpdatedBindings(
        [
            DescriptorSetBinding.SamplerDescriptor(0, sampler, 0)
        ]));
    }

    [Fact]
    public void EmptySparseUpdateThrows()
    {
        var first = new TestTextureView();
        var second = new TestTextureView();
        var sampler = new TestSampler();
        var state = new VulkanDescriptorSetBindingState(Layout,
        [
            SampledImage(0, first),
            SampledImage(1, second),
            SamplerBinding(sampler)
        ]);

        Assert.Throws<ArgumentException>(() => state.CreateUpdatedBindings([]));
    }

    [Fact]
    public void DifferentElementsOfSameBindingAreNotDuplicates()
    {
        var first = new TestTextureView();
        var second = new TestTextureView();
        var sampler = new TestSampler();
        var state = new VulkanDescriptorSetBindingState(Layout,
        [
            SampledImage(0, first),
            SampledImage(1, second),
            SamplerBinding(sampler)
        ]);

        var candidate = state.CreateUpdatedBindings([SampledImage(0, second), SampledImage(1, first)]);

        Assert.Same(second, candidate[0].TextureView);
        Assert.Same(first, candidate[1].TextureView);
    }

    [Fact]
    public void CommitAcceptsProducedCandidate()
    {
        var first = new TestTextureView();
        var second = new TestTextureView();
        var replacement = new TestTextureView();
        var sampler = new TestSampler();
        var state = new VulkanDescriptorSetBindingState(Layout,
        [
            SampledImage(0, first),
            SampledImage(1, second),
            SamplerBinding(sampler)
        ]);

        var candidate = state.CreateUpdatedBindings([SampledImage(1, replacement)]);
        state.Commit(candidate);

        Assert.Same(replacement, state.Bindings[1].TextureView);
    }

    [Fact]
    public void CommitRejectsForeignCandidate()
    {
        var first = new TestTextureView();
        var second = new TestTextureView();
        var sampler = new TestSampler();
        var state = new VulkanDescriptorSetBindingState(Layout,
        [
            SampledImage(0, first),
            SampledImage(1, second),
            SamplerBinding(sampler)
        ]);

        Assert.Throws<ArgumentException>(() => state.Commit([SampledImage(1, second)]));
    }
}
