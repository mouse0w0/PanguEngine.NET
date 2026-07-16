using System.Runtime.InteropServices;
using PanguEngine.Graphics;

namespace PanguEngine.Client.Rendering.World;

[StructLayout(LayoutKind.Sequential)]
internal readonly struct ChunkVertex
{
    public const uint SizeInBytes = 28;

    public static readonly VertexInputDescription VertexInput = new(
        [new VertexBufferLayoutDescription(0, SizeInBytes)],
        [
            new VertexAttributeDescription(0, 0, VertexAttributeFormat.Float32x3, 0),
            new VertexAttributeDescription(1, 0, VertexAttributeFormat.Float32x4, 12)
        ]);

    public readonly float X;
    public readonly float Y;
    public readonly float Z;
    public readonly float R;
    public readonly float G;
    public readonly float B;
    public readonly float A;

    public ChunkVertex(
        float x,
        float y,
        float z,
        float r,
        float g,
        float b,
        float a)
    {
        X = x;
        Y = y;
        Z = z;
        R = r;
        G = g;
        B = b;
        A = a;
    }
}