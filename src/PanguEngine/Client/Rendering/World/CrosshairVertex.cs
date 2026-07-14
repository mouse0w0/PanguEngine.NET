using System.Runtime.InteropServices;
using PanguEngine.Graphics;

namespace PanguEngine.Client.Rendering.World;

[StructLayout(LayoutKind.Sequential)]
internal readonly struct CrosshairVertex
{
    internal const uint SizeInBytes = 8;

    internal static readonly VertexInputDescription VertexInput = new(
        [new VertexBufferLayoutDescription(0, SizeInBytes)],
        [new VertexAttributeDescription(0, 0, VertexAttributeFormat.Float32x2, 0)]);

    internal readonly float X;
    internal readonly float Y;

    internal CrosshairVertex(float x, float y)
    {
        X = x;
        Y = y;
    }
}