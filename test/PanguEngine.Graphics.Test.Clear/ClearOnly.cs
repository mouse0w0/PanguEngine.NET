using PanguEngine.Graphics.Vulkan;
using Silk.NET.Vulkan;

namespace PanguEngine.Graphics.Test.Clear;

internal static class ClearOnly
{
    private static void Main()
    {
        new VulkanTestApp(new ClearOnlyScene()).Run();
    }
}

internal sealed unsafe class ClearOnlyScene : IVulkanTestScene
{
    public string Name => "ClearOnly";

    public void Initialize(VulkanWindow window)
    {
    }

    public void Record(CommandBuffer commandBuffer, ImageView targetImageView, Extent2D extent, Format imageFormat)
    {
        ClearValue clearColor = new()
        {
            Color = new ClearColorValue { Float32_0 = 0.02f, Float32_1 = 0.04f, Float32_2 = 0.08f, Float32_3 = 1 },
        };

        RenderingAttachmentInfo colorAttachment = new()
        {
            SType = StructureType.RenderingAttachmentInfo,
            ImageView = targetImageView,
            ImageLayout = ImageLayout.ColorAttachmentOptimal,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.Store,
            ClearValue = clearColor,
        };

        RenderingInfo renderingInfo = new()
        {
            SType = StructureType.RenderingInfo,
            RenderArea = new Rect2D { Offset = new Offset2D(0, 0), Extent = extent },
            LayerCount = 1,
            ColorAttachmentCount = 1,
            PColorAttachments = &colorAttachment,
        };

        VulkanContext.Vk.CmdBeginRendering(commandBuffer, &renderingInfo);
        VulkanContext.Vk.CmdEndRendering(commandBuffer);
    }

    public void Destroy()
    {
    }
}