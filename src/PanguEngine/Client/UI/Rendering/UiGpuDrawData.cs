using System.Numerics;
using System.Runtime.InteropServices;
using PanguEngine.Client.UI.Drawing.Geometry;

namespace PanguEngine.Client.UI.Rendering;

[StructLayout(LayoutKind.Sequential)]
internal readonly struct UiGpuDrawData(
    uint clipIndex, uint outerIndex = 0, uint innerIndex = 0,
    Vector4 background = default, Vector4 border = default, uint decoration = 0)
{
    internal const uint SizeInBytes = 48;
    internal readonly Vector4 Background = background;
    internal readonly Vector4 Border = border;
    internal readonly uint ClipIndex = clipIndex;
    internal readonly uint OuterIndex = outerIndex;
    internal readonly uint InnerIndex = innerIndex;
    internal readonly uint Decoration = decoration;
}

[StructLayout(LayoutKind.Sequential)]
internal readonly struct UiGpuClipData(UiRoundedRectangle shape, uint parentIndex = 0, bool exclude = false)
{
    internal const uint SizeInBytes = 64;
    internal readonly Vector4 Bounds = new(
        (float)shape.Bounds.X, (float)shape.Bounds.Y,
        (float)(shape.Bounds.X + shape.Bounds.Width), (float)(shape.Bounds.Y + shape.Bounds.Height));
    internal readonly Vector4 RadiusX = new(
        (float)shape.TopLeft.X, (float)shape.TopRight.X,
        (float)shape.BottomRight.X, (float)shape.BottomLeft.X);
    internal readonly Vector4 RadiusY = new(
        (float)shape.TopLeft.Y, (float)shape.TopRight.Y,
        (float)shape.BottomRight.Y, (float)shape.BottomLeft.Y);
    internal readonly uint ParentIndex = parentIndex;
    internal readonly uint Exclude = exclude ? 1u : 0u;
    internal readonly uint Padding0 = 0;
    internal readonly uint Padding1 = 0;
}
