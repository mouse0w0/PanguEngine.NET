using PanguEngine.Graphics;

namespace PanguEngine.Tests.Graphics;

public sealed class PushConstantRangeDescriptionTests
{
    [Fact]
    public void GraphicsPipelineDefaultsToNoPushConstantRanges()
    {
        var description = new GraphicsPipelineDescription
        {
            Shaders = [],
            VertexInput = VertexInputDescription.Empty,
            ColorAttachmentFormats = [],
            DescriptorSetLayouts = []
        };

        Assert.Empty(description.PushConstantRanges);
    }
}