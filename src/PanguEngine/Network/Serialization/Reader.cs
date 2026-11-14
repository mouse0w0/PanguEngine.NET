using System.Buffers.Binary;
using System.Text;

namespace PanguEngine.Network.Serialization;

/// <summary>Reads sequential binary values from a complete, contiguous network payload.</summary>
/// <remarks>
/// The input must remain valid and unchanged while it is being read. Pass the reader by reference
/// when composing reads. Returned byte spans borrow the input and must not outlive its availability.
/// A failed read does not guarantee that the reader can be reused.
/// </remarks>
public ref struct Reader
{
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private ReadOnlySpan<byte> _remaining;

    internal readonly int RemainingLength => _remaining.Length;

    /// <summary>Creates a reader over the supplied complete payload.</summary>
    public Reader(ReadOnlySpan<byte> input)
    {
        _remaining = input;
    }

    /// <summary>Reads an unsigned 8-bit integer.</summary>
    public byte ReadByte() => ReadRawBytes(1)[0];

    /// <summary>Reads a signed 8-bit integer.</summary>
    public sbyte ReadSByte() => unchecked((sbyte)ReadByte());

    /// <summary>Reads a boolean encoded as 0 or 1.</summary>
    /// <exception cref="InvalidDataException">The encoded byte is neither 0 nor 1.</exception>
    public bool ReadBoolean() => ReadByte() switch
    {
        0 => false,
        1 => true,
        _ => throw new InvalidDataException("The boolean encoding must be 0 or 1.")
    };

    /// <summary>Reads a little-endian signed 16-bit integer.</summary>
    public short ReadInt16() => BinaryPrimitives.ReadInt16LittleEndian(ReadRawBytes(sizeof(short)));

    /// <summary>Reads a little-endian unsigned 16-bit integer.</summary>
    public ushort ReadUInt16() => BinaryPrimitives.ReadUInt16LittleEndian(ReadRawBytes(sizeof(ushort)));

    /// <summary>Reads a little-endian signed 32-bit integer.</summary>
    public int ReadInt32() => BinaryPrimitives.ReadInt32LittleEndian(ReadRawBytes(sizeof(int)));

    /// <summary>Reads a little-endian unsigned 32-bit integer.</summary>
    public uint ReadUInt32() => BinaryPrimitives.ReadUInt32LittleEndian(ReadRawBytes(sizeof(uint)));

    /// <summary>Reads a little-endian signed 64-bit integer.</summary>
    public long ReadInt64() => BinaryPrimitives.ReadInt64LittleEndian(ReadRawBytes(sizeof(long)));

    /// <summary>Reads a little-endian unsigned 64-bit integer.</summary>
    public ulong ReadUInt64() => BinaryPrimitives.ReadUInt64LittleEndian(ReadRawBytes(sizeof(ulong)));

    /// <summary>Reads a little-endian 32-bit floating-point value.</summary>
    public float ReadSingle() => BinaryPrimitives.ReadSingleLittleEndian(ReadRawBytes(sizeof(float)));

    /// <summary>Reads a little-endian 64-bit floating-point value.</summary>
    public double ReadDouble() => BinaryPrimitives.ReadDoubleLittleEndian(ReadRawBytes(sizeof(double)));

    /// <summary>Reads an unsigned 32-bit integer encoded as unsigned LEB128.</summary>
    /// <remarks>Accepts non-shortest representations that terminate within five bytes and fit the value range.</remarks>
    /// <exception cref="EndOfStreamException">The variable-length value is truncated.</exception>
    /// <exception cref="InvalidDataException">The encoding exceeds the permitted width or length.</exception>
    public uint ReadVarUInt32()
    {
        uint value = 0;
        for (var shift = 0; shift < 28; shift += 7)
        {
            var current = ReadByte();
            value |= (uint)(current & 0x7f) << shift;
            if ((current & 0x80) == 0)
                return value;
        }

        var last = ReadByte();
        if (last > 0x0f)
            throw new InvalidDataException("The variable-length integer exceeds 32 bits.");

        return value | ((uint)last << 28);
    }

    /// <summary>Reads an unsigned 64-bit integer encoded as unsigned LEB128.</summary>
    /// <remarks>Accepts non-shortest representations that terminate within ten bytes and fit the value range.</remarks>
    /// <exception cref="EndOfStreamException">The variable-length value is truncated.</exception>
    /// <exception cref="InvalidDataException">The encoding exceeds the permitted width or length.</exception>
    public ulong ReadVarUInt64()
    {
        ulong value = 0;
        for (var shift = 0; shift < 63; shift += 7)
        {
            var current = ReadByte();
            value |= (ulong)(current & 0x7f) << shift;
            if ((current & 0x80) == 0)
                return value;
        }

        var last = ReadByte();
        if (last > 1)
            throw new InvalidDataException("The variable-length integer exceeds 64 bits.");

        return value | ((ulong)last << 63);
    }

    /// <summary>Reads a signed 32-bit integer whose two's-complement bit pattern is encoded as unsigned LEB128.</summary>
    /// <remarks>Negative values occupy five bytes.</remarks>
    /// <exception cref="EndOfStreamException">The variable-length value is truncated.</exception>
    /// <exception cref="InvalidDataException">The encoding exceeds the permitted width or length.</exception>
    public int ReadVarInt32() => unchecked((int)ReadVarUInt32());

    /// <summary>Reads a signed 64-bit integer whose two's-complement bit pattern is encoded as unsigned LEB128.</summary>
    /// <remarks>Negative values occupy ten bytes.</remarks>
    /// <exception cref="EndOfStreamException">The variable-length value is truncated.</exception>
    /// <exception cref="InvalidDataException">The encoding exceeds the permitted width or length.</exception>
    public long ReadVarInt64() => unchecked((long)ReadVarUInt64());

    /// <summary>Reads a signed 32-bit integer encoded using ZigZag and unsigned LEB128.</summary>
    /// <exception cref="EndOfStreamException">The variable-length value is truncated.</exception>
    /// <exception cref="InvalidDataException">The encoding exceeds the permitted width or length.</exception>
    public int ReadVarInt32ZigZag()
    {
        var value = ReadVarUInt32();
        return (int)(value >> 1) ^ -(int)(value & 1);
    }

    /// <summary>Reads a signed 64-bit integer encoded using ZigZag and unsigned LEB128.</summary>
    /// <exception cref="EndOfStreamException">The variable-length value is truncated.</exception>
    /// <exception cref="InvalidDataException">The encoding exceeds the permitted width or length.</exception>
    public long ReadVarInt64ZigZag()
    {
        var value = ReadVarUInt64();
        return (long)(value >> 1) ^ -(long)(value & 1);
    }

    /// <summary>Reads a length-prefixed, strictly encoded UTF-8 string.</summary>
    /// <param name="maxByteCount">The nonnegative maximum permitted UTF-8 byte count.</param>
    /// <exception cref="InvalidDataException">The variable-length prefix is invalid or exceeds the limit.</exception>
    /// <exception cref="EndOfStreamException">The payload is truncated.</exception>
    /// <exception cref="DecoderFallbackException">The content is not valid UTF-8.</exception>
    public string ReadString(int maxByteCount) => Utf8.GetString(ReadBytes(maxByteCount));

    /// <summary>Reads length-prefixed bytes as a borrowed slice of the input.</summary>
    /// <param name="maxByteCount">The nonnegative maximum permitted byte count.</param>
    /// <exception cref="InvalidDataException">The variable-length prefix is invalid or exceeds the limit.</exception>
    /// <exception cref="EndOfStreamException">The payload is truncated.</exception>
    public ReadOnlySpan<byte> ReadBytes(int maxByteCount) => ReadRawBytes(ReadCount(maxByteCount));

    /// <summary>Reads bytes of a known length without a length prefix.</summary>
    /// <param name="length">The nonnegative number of bytes to read.</param>
    /// <returns>A borrowed slice of the input.</returns>
    /// <exception cref="EndOfStreamException">The requested bytes exceed the remaining input.</exception>
    public ReadOnlySpan<byte> ReadRawBytes(int length)
    {
        if (length > _remaining.Length)
            throw new EndOfStreamException("The payload is truncated.");

        var value = _remaining[..length];
        _remaining = _remaining[length..];
        return value;
    }

    /// <summary>Reads a nonnegative length or element count encoded as unsigned 32-bit LEB128.</summary>
    /// <param name="maxCount">The nonnegative maximum permitted count.</param>
    /// <exception cref="InvalidDataException">The variable-length prefix is invalid or exceeds the limit.</exception>
    /// <exception cref="EndOfStreamException">The count is truncated.</exception>
    public int ReadCount(int maxCount)
    {
        var count = ReadVarUInt32();
        if (count > (uint)maxCount)
            throw new InvalidDataException("The length or count exceeds the allowed range.");

        return (int)count;
    }

    /// <summary>Checks that the entire payload has been consumed.</summary>
    /// <exception cref="InvalidDataException">The payload contains trailing bytes.</exception>
    public readonly void EnsureEnd()
    {
        if (!_remaining.IsEmpty)
            throw new InvalidDataException("The payload contains trailing bytes.");
    }
}
