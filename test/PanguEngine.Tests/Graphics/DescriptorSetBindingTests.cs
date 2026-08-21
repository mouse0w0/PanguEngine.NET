using PanguEngine.Graphics;
using Xunit;

namespace PanguEngine.Tests.Graphics;

public sealed class DescriptorSetBindingTests
{
    [Fact]
    public void LayoutBindingDefaultsDescriptorCountToOne()
    {
        var binding = new DescriptorSetLayoutBinding(
            3,
            DescriptorType.SampledImage,
            ShaderStageFlags.Fragment);

        Assert.Equal(1u, binding.DescriptorCount);
    }

    [Fact]
    public void ObjectInitializedLayoutBindingDefaultsDescriptorCountToOne()
    {
        var binding = new DescriptorSetLayoutBinding
        {
            Binding = 3,
            Type = DescriptorType.SampledImage,
            StageFlags = ShaderStageFlags.Fragment
        };

        Assert.Equal(1u, binding.DescriptorCount);
    }

    [Fact]
    public void ExplicitDescriptorCountIsPreserved()
    {
        var binding = new DescriptorSetLayoutBinding(
            0,
            DescriptorType.SampledImage,
            ShaderStageFlags.Fragment,
            256);

        Assert.Equal(256u, binding.DescriptorCount);
    }

    [Fact]
    public void LayoutBindingRejectsZeroDescriptorCount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DescriptorSetLayoutBinding(
            0,
            DescriptorType.SampledImage,
            ShaderStageFlags.Fragment,
            0));
    }

    [Fact]
    public void SampledImageBindingKeepsArrayElement()
    {
        var view = new TestTextureView();
        var binding = DescriptorSetBinding.SampledImage(0, 17, view);

        Assert.Equal(DescriptorType.SampledImage, binding.Type);
        Assert.Equal(17u, binding.ArrayElement);
        Assert.Same(view, binding.TextureView);
        Assert.Null(binding.Sampler);
    }

    [Fact]
    public void SamplerBindingDefaultsToArrayElementZero()
    {
        var sampler = new TestSampler();
        var binding = DescriptorSetBinding.SamplerDescriptor(1, sampler);

        Assert.Equal(DescriptorType.Sampler, binding.Type);
        Assert.Equal(0u, binding.ArrayElement);
        Assert.Same(sampler, binding.Sampler);
        Assert.Null(binding.TextureView);
    }

    [Fact]
    public void SamplerBindingKeepsExplicitArrayElement()
    {
        var sampler = new TestSampler();
        var binding = DescriptorSetBinding.SamplerDescriptor(1, sampler, 4);

        Assert.Equal(4u, binding.ArrayElement);
    }

    [Fact]
    public void DifferentArrayElementsShareBindingIndex()
    {
        var first = new TestTextureView();
        var second = new TestTextureView();
        var a = DescriptorSetBinding.SampledImage(0, 0, first);
        var b = DescriptorSetBinding.SampledImage(0, 1, second);

        Assert.Equal(0u, a.Binding);
        Assert.Equal(0u, b.Binding);
        Assert.Equal(0u, a.ArrayElement);
        Assert.Equal(1u, b.ArrayElement);
        Assert.Same(first, a.TextureView);
        Assert.Same(second, b.TextureView);
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

    private sealed class TestSampler : Sampler
    {
        public override void Destroy() => MarkDestroyed();
    }
}
