using PanguEngine.Graphics.Vulkan;
using Silk.NET.Vulkan;

namespace PanguEngine.Graphics.Test;

public interface IVulkanTestScene
{
    string Name { get; }

    void Initialize(VulkanWindow window);

    void PrepareFrame()
    {
    }

    void Record(CommandBuffer commandBuffer, ImageView targetImageView, Extent2D extent, Format imageFormat);

    void Destroy();
}