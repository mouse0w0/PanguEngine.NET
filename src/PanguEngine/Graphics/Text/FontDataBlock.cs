using System.Runtime.InteropServices;

namespace PanguEngine.Graphics.Text;

internal sealed unsafe class FontDataBlock
{
    internal FontDataBlock(ReadOnlySpan<byte> data)
    {
        Length = data.Length;
        Pointer = (byte*)NativeMemory.Alloc((nuint)data.Length);
        data.CopyTo(new Span<byte>(Pointer, data.Length));
    }

    internal int Length { get; }
    internal int FaceCount { get; set; }
    internal byte* Pointer { get; private set; }

    internal void Destroy()
    {
        if (Pointer == null)
            return;

        NativeMemory.Free(Pointer);
        Pointer = null;
    }
}
