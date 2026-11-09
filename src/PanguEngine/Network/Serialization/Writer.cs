using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace PanguEngine.Network.Serialization;

/// <summary>Writes sequential binary values to a caller-provided network payload buffer.</summary>
/// <remarks>
/// The caller owns the output and must not interleave different messages on it. Input bytes must
/// remain valid during a write and must not alias output storage that may be invalidated by writing.
/// Each successful operation advances the output; failures do not roll back previously written data.
/// </remarks>
public readonly struct Writer
{
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private readonly IBufferWriter<byte> _output;

    /// <summary>Creates a writer for the supplied output buffer.</summary>
    public Writer(IBufferWriter<byte> output)
    {
        _output = output;
    }

    /// <summary>Writes an unsigned 8-bit integer.</summary>
    public void WriteByte(byte value)
    {
        _output.GetSpan(1)[0] = value;
        _output.Advance(1);
    }

    /// <summary>Writes a signed 8-bit integer.</summary>
    public void WriteSByte(sbyte value) => WriteByte(unchecked((byte)value));

    /// <summary>Writes a boolean as 0 or 1.</summary>
    public void WriteBoolean(bool value) => WriteByte(value ? (byte)1 : (byte)0);

    /// <summary>Writes a little-endian signed 16-bit integer.</summary>
    public void WriteInt16(short value)
    {
        BinaryPrimitives.WriteInt16LittleEndian(_output.GetSpan(sizeof(short)), value);
        _output.Advance(sizeof(short));
    }

    /// <summary>Writes a little-endian unsigned 16-bit integer.</summary>
    public void WriteUInt16(ushort value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(_output.GetSpan(sizeof(ushort)), value);
        _output.Advance(sizeof(ushort));
    }

    /// <summary>Writes a little-endian signed 32-bit integer.</summary>
    public void WriteInt32(int value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(_output.GetSpan(sizeof(int)), value);
        _output.Advance(sizeof(int));
    }

    /// <summary>Writes a little-endian unsigned 32-bit integer.</summary>
    public void WriteUInt32(uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(_output.GetSpan(sizeof(uint)), value);
        _output.Advance(sizeof(uint));
    }

    /// <summary>Writes a little-endian signed 64-bit integer.</summary>
    public void WriteInt64(long value)
    {
        BinaryPrimitives.WriteInt64LittleEndian(_output.GetSpan(sizeof(long)), value);
        _output.Advance(sizeof(long));
    }

    /// <summary>Writes a little-endian unsigned 64-bit integer.</summary>
    public void WriteUInt64(ulong value)
    {
        BinaryPrimitives.WriteUInt64LittleEndian(_output.GetSpan(sizeof(ulong)), value);
        _output.Advance(sizeof(ulong));
    }

    /// <summary>Writes a little-endian 32-bit floating-point value.</summary>
    public void WriteSingle(float value)
    {
        BinaryPrimitives.WriteSingleLittleEndian(_output.GetSpan(sizeof(float)), value);
        _output.Advance(sizeof(float));
    }

    /// <summary>Writes a little-endian 64-bit floating-point value.</summary>
    public void WriteDouble(double value)
    {
        BinaryPrimitives.WriteDoubleLittleEndian(_output.GetSpan(sizeof(double)), value);
        _output.Advance(sizeof(double));
    }

    /// <summary>Writes an unsigned 32-bit integer using its shortest unsigned LEB128 representation.</summary>
    public void WriteVarUInt32(uint value)
    {
        while (value >= 0x80)
        {
            WriteByte((byte)((value & 0x7f) | 0x80));
            value >>= 7;
        }

        WriteByte((byte)value);
    }

    /// <summary>Writes an unsigned 64-bit integer using its shortest unsigned LEB128 representation.</summary>
    public void WriteVarUInt64(ulong value)
    {
        while (value >= 0x80)
        {
            WriteByte((byte)((value & 0x7f) | 0x80));
            value >>= 7;
        }

        WriteByte((byte)value);
    }

    /// <summary>Writes a signed 32-bit integer using ZigZag and shortest unsigned LEB128.</summary>
    public void WriteVarInt32(int value) => WriteVarUInt32(unchecked((uint)((value << 1) ^ (value >> 31))));

    /// <summary>Writes a signed 64-bit integer using ZigZag and shortest unsigned LEB128.</summary>
    public void WriteVarInt64(long value) => WriteVarUInt64(unchecked((ulong)((value << 1) ^ (value >> 63))));

    /// <summary>Writes a VarUInt32 byte count followed by strict UTF-8 content, without a BOM.</summary>
    /// <exception cref="EncoderFallbackException">The string contains invalid UTF-16.</exception>
    public void WriteString(string value)
    {
        var byteCount = Utf8.GetByteCount(value);
        WriteCount(byteCount);
        var written = Utf8.GetBytes(value.AsSpan(), _output.GetSpan(byteCount));
        _output.Advance(written);
    }

    /// <summary>Writes a VarUInt32 byte count followed by the supplied bytes.</summary>
    public void WriteBytes(ReadOnlySpan<byte> value)
    {
        WriteCount(value.Length);
        WriteRawBytes(value);
    }

    /// <summary>Writes the supplied bytes without a length prefix.</summary>
    public void WriteRawBytes(ReadOnlySpan<byte> value)
    {
        value.CopyTo(_output.GetSpan(value.Length));
        _output.Advance(value.Length);
    }

    /// <summary>Writes a nonnegative length or element count as shortest unsigned 32-bit LEB128.</summary>
    /// <param name="value">The count, which the caller must ensure is nonnegative.</param>
    public void WriteCount(int value) => WriteVarUInt32((uint)value);
}
