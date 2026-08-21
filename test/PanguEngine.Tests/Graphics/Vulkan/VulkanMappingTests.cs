using PanguEngine.Graphics;
using PanguEngine.Graphics.Vulkan;
using VkShaderStageFlags = Silk.NET.Vulkan.ShaderStageFlags;
using VkDescriptorType = Silk.NET.Vulkan.DescriptorType;
using Format = Silk.NET.Vulkan.Format;

namespace PanguEngine.Tests.Graphics.Vulkan;

public sealed class VulkanMappingTests
{
    [Fact]
    public void MapsPushConstantRangeWithoutChangingOffsetOrSize()
    {
        var description = new PushConstantRangeDescription(
            ShaderStageFlags.Vertex | ShaderStageFlags.Fragment,
            4,
            16);

        var actual = VulkanMapping.ToVulkanPushConstantRange(description);

        Assert.Equal(VkShaderStageFlags.VertexBit | VkShaderStageFlags.FragmentBit, actual.StageFlags);
        Assert.Equal(4u, actual.Offset);
        Assert.Equal(16u, actual.Size);
    }

    [Theory]
    [InlineData(DescriptorType.UniformBuffer, VkDescriptorType.UniformBuffer)]
    [InlineData(DescriptorType.StorageBuffer, VkDescriptorType.StorageBuffer)]
    [InlineData(DescriptorType.CombinedImageSampler, VkDescriptorType.CombinedImageSampler)]
    [InlineData(DescriptorType.SampledImage, VkDescriptorType.SampledImage)]
    [InlineData(DescriptorType.Sampler, VkDescriptorType.Sampler)]
    public void MapsDescriptorTypes(DescriptorType type, VkDescriptorType expected)
    {
        Assert.Equal(expected, VulkanMapping.ToVulkanDescriptorType(type));
    }

    [Fact]
    public void MapsUInt32VertexAttributeToR32Uint()
    {
        Assert.Equal(Format.R32Uint, VulkanMapping.ToVulkanVertexAttributeFormat(VertexAttributeFormat.UInt32));
    }
}