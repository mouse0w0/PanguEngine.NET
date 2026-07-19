using System.Runtime.InteropServices;
using PanguEngine.Graphics;

namespace PanguEngine.Client.Rendering.World;

[StructLayout(LayoutKind.Sequential)]
internal readonly struct SelectionVertex(
    float x,
    float y,
    float z,
    float r,
    float g,
    float b,
    float a)
{
    public const uint SizeInBytes = 28;

    public static readonly VertexInputDescription VertexInput = new(
        [new VertexBufferLayoutDescription(0, SizeInBytes)],
        [
            new VertexAttributeDescription(0, 0, VertexAttributeFormat.Float32x3, 0),
            new VertexAttributeDescription(1, 0, VertexAttributeFormat.Float32x4, 12)
        ]);

    public readonly float X = x;
    public readonly float Y = y;
    public readonly float Z = z;
    public readonly float R = r;
    public readonly float G = g;
    public readonly float B = b;
    public readonly float A = a;
}