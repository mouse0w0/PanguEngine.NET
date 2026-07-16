using PanguEngine.Graphics;
using PanguEngine.Graphics.Vulkan;
using VkShaderStageFlags = Silk.NET.Vulkan.ShaderStageFlags;

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
}