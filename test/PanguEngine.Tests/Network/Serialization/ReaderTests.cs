using System.Buffers.Binary;
using System.Text;
using PanguEngine.Network.Serialization;

namespace PanguEngine.Tests.Network.Serialization;

public sealed class ReaderTests
{
    [Fact]
    public void ScalarsReadTheirSpecifiedLittleEndianBytes()
    {
        byte[] input =
        [
            0x00, 0x01, 0xab, 0xfe,
            0xfe, 0xff, 0xdc, 0xfe,
            0x00, 0x00, 0x00, 0x80, 0xef, 0xcd, 0xab, 0x89,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x80,
            0x10, 0x32, 0x54, 0x76, 0x98, 0xba, 0xdc, 0xfe,
            0x00, 0x00, 0xc0, 0x3f,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x04, 0xc0
        ];
        var reader = new Reader(input);
        Assert.False(reader.ReadBoolean());
        Assert.True(reader.ReadBoolean());
        Assert.Equal((byte)0xab, reader.ReadByte());
        Assert.Equal((sbyte)-2, reader.ReadSByte());
        Assert.Equal((short)-2, reader.ReadInt16());
        Assert.Equal((ushort)0xfedc, reader.ReadUInt16());
        Assert.Equal(int.MinValue, reader.ReadInt32());
        Assert.Equal(0x89abcdefu, reader.ReadUInt32());
        Assert.Equal(long.MinValue, reader.ReadInt64());
        Assert.Equal(0xfedcba9876543210ul, reader.ReadUInt64());
        Assert.Equal(1.5f, reader.ReadSingle());
        Assert.Equal(-2.5, reader.ReadDouble());
        reader.EnsureEnd();
    }

    [Theory]
    [InlineData(unchecked((int)0x80000000), unchecked((long)0x8000000000000000))]
    [InlineData(0x7f800000, 0x7ff0000000000000)]
    [InlineData(unchecked((int)0xff800000), unchecked((long)0xfff0000000000000))]
    [InlineData(0x7fc01234, 0x7ff8000000001234)]
    public void FloatingPointSpecialValuesPreserveTheirBits(int singleBits, long doubleBits)
    {
        byte[] input = new byte[12];
        BinaryPrimitives.WriteInt32LittleEndian(input, singleBits);
        BinaryPrimitives.WriteInt64LittleEndian(input.AsSpan(4), doubleBits);
        var reader = new Reader(input);
        Assert.Equal(singleBits, BitConverter.SingleToInt32Bits(reader.ReadSingle()));
        Assert.Equal(doubleBits, BitConverter.DoubleToInt64Bits(reader.ReadDouble()));
        reader.EnsureEnd();
    }

    [Theory]
    [InlineData(0UL, "00")]
    [InlineData(1UL, "01")]
    [InlineData(127UL, "7F")]
    [InlineData(128UL, "8001")]
    [InlineData(16383UL, "FF7F")]
    [InlineData(16384UL, "808001")]
    [InlineData(0x1fffffUL, "FFFF7F")]
    [InlineData(0x200000UL, "80808001")]
    [InlineData(0xfffffffUL, "FFFFFF7F")]
    [InlineData(0x10000000UL, "8080808001")]
    [InlineData(uint.MaxValue, "FFFFFFFF0F")]
    [InlineData(0x7ffffffffUL, "FFFFFFFF7F")]
    [InlineData(0x800000000UL, "808080808001")]
    [InlineData(0x3ffffffffffUL, "FFFFFFFFFF7F")]
    [InlineData(0x40000000000UL, "80808080808001")]
    [InlineData(0x1ffffffffffffUL, "FFFFFFFFFFFF7F")]
    [InlineData(0x2000000000000UL, "8080808080808001")]
    [InlineData(0xffffffffffffffUL, "FFFFFFFFFFFFFF7F")]
    [InlineData(0x100000000000000UL, "808080808080808001")]
    [InlineData(long.MaxValue, "FFFFFFFFFFFFFFFF7F")]
    [InlineData(0x8000000000000000UL, "80808080808080808001")]
    [InlineData(ulong.MaxValue, "FFFFFFFFFFFFFFFFFF01")]
    public void UnsignedVarIntegersReadKnownBytes(ulong expected, string hex)
    {
        var input = Convert.FromHexString(hex);
        var reader = new Reader(input);
        Assert.Equal(expected, reader.ReadVarUInt64());
        reader.EnsureEnd();
        if (expected <= uint.MaxValue)
        {
            var narrowReader = new Reader(input);
            Assert.Equal((uint)expected, narrowReader.ReadVarUInt32());
            narrowReader.EnsureEnd();
        }
    }

    [Theory]
    [InlineData(0L, "00")]
    [InlineData(-1L, "01")]
    [InlineData(1L, "02")]
    [InlineData(-2L, "03")]
    [InlineData(-64L, "7F")]
    [InlineData(63L, "7E")]
    [InlineData(-65L, "8101")]
    [InlineData(64L, "8001")]
    [InlineData(int.MinValue, "FFFFFFFF0F")]
    [InlineData(int.MaxValue, "FEFFFFFF0F")]
    [InlineData(long.MinValue, "FFFFFFFFFFFFFFFFFF01")]
    [InlineData(long.MaxValue, "FEFFFFFFFFFFFFFFFF01")]
    public void SignedVarIntegersDecodeZigZagAcrossTheFullRange(long expected, string hex)
    {
        var input = Convert.FromHexString(hex);
        var reader = new Reader(input);
        Assert.Equal(expected, reader.ReadVarInt64());
        reader.EnsureEnd();
        if (expected >= int.MinValue && expected <= int.MaxValue)
        {
            var narrowReader = new Reader(input);
            Assert.Equal((int)expected, narrowReader.ReadVarInt32());
            narrowReader.EnsureEnd();
        }
    }

    [Theory]
    [InlineData("8000", 0UL)]
    [InlineData("8100", 1UL)]
    [InlineData("8080808000", 0UL)]
    [InlineData("80808080808080808000", 0UL)]
    public void NonShortestUnsignedValuesTerminateWithoutConsumingTheNextField(string hex, ulong expected)
    {
        byte[] input = [.. Convert.FromHexString(hex), 0x2a];
        var reader = new Reader(input);
        Assert.Equal(expected, reader.ReadVarUInt64());
        Assert.Equal((byte)0x2a, reader.ReadByte());
        reader.EnsureEnd();
        if (input.Length - 1 <= 5)
        {
            var narrowReader = new Reader(input);
            Assert.Equal((uint)expected, narrowReader.ReadVarUInt32());
            Assert.Equal((byte)0x2a, narrowReader.ReadByte());
            narrowReader.EnsureEnd();
        }
    }

    [Theory]
    [InlineData("varuint32", 5)]
    [InlineData("varint32", 5)]
    [InlineData("varuint64", 10)]
    [InlineData("varint64", 10)]
    public void VarIntegersRejectEveryIncompletePrefix(string kind, int maxBytes)
    {
        for (var length = 0; length < maxBytes; length++)
        {
            var input = new byte[length];
            Array.Fill(input, (byte)0x80);
            Assert.Throws<EndOfStreamException>(() => ReadValue(input, kind));
        }
    }

    [Theory]
    [InlineData("varuint32", "FFFFFFFF10")]
    [InlineData("varint32", "FFFFFFFF10")]
    [InlineData("varuint32", "8080808080")]
    [InlineData("varint32", "8080808080")]
    [InlineData("varuint64", "FFFFFFFFFFFFFFFFFF02")]
    [InlineData("varint64", "FFFFFFFFFFFFFFFFFF02")]
    [InlineData("varuint64", "80808080808080808080")]
    [InlineData("varint64", "80808080808080808080")]
    public void VarIntegersRejectOverflowAndContinuationAtTheWidthLimit(string kind, string hex)
    {
        Assert.Throws<InvalidDataException>(() => ReadValue(Convert.FromHexString(hex), kind));
    }

    [Theory]
    [InlineData("byte", 1)]
    [InlineData("sbyte", 1)]
    [InlineData("bool", 1)]
    [InlineData("int16", 2)]
    [InlineData("uint16", 2)]
    [InlineData("int32", 4)]
    [InlineData("uint32", 4)]
    [InlineData("int64", 8)]
    [InlineData("uint64", 8)]
    [InlineData("single", 4)]
    [InlineData("double", 8)]
    public void FixedWidthValuesRejectEveryTruncation(string kind, int width)
    {
        for (var length = 0; length < width; length++)
        {
            var input = new byte[length];
            Assert.Throws<EndOfStreamException>(() => ReadValue(input, kind));
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(255)]
    public void BooleansRejectOtherByteValues(byte value)
    {
        Assert.Throws<InvalidDataException>(() => ReadValue([value], "bool"));
    }

    [Theory]
    [InlineData("string")]
    [InlineData("bytes")]
    [InlineData("count")]
    public void LengthPrefixesCheckLimitsBeforeNarrowingOrAllocating(string kind)
    {
        Assert.Throws<InvalidDataException>(() => ReadValue([9], kind, 8));
        Assert.Throws<InvalidDataException>(() => ReadValue(Convert.FromHexString("8080808008"), kind));
        Assert.Throws<InvalidDataException>(() => ReadValue(Convert.FromHexString("FFFFFFFF0F"), kind));
        Assert.Throws<EndOfStreamException>(() => ReadValue([], kind));
        Assert.Throws<EndOfStreamException>(() => ReadValue([0x80], kind));
    }

    [Fact]
    public void CountsAllowTheirInclusiveLimitWithoutConsumingFollowingBytes()
    {
        var reader = new Reader(Convert.FromHexString("008001FFFFFFFF072A"));
        Assert.Equal(0, reader.ReadCount(0));
        Assert.Equal(128, reader.ReadCount(128));
        Assert.Equal(int.MaxValue, reader.ReadCount(int.MaxValue));
        Assert.Equal((byte)0x2a, reader.ReadByte());
        reader.EnsureEnd();
    }

    [Theory]
    [InlineData("string")]
    [InlineData("bytes")]
    public void VariableLengthBodiesMustFitRemainingInput(string kind)
    {
        Assert.Throws<EndOfStreamException>(() => ReadValue([4, 1, 2, 3], kind, 4));
        Assert.Throws<EndOfStreamException>(() => ReadValue(Convert.FromHexString("FFFFFFFF0700"), kind));
    }

    [Fact]
    public void StringsUseStrictUtf8AndLimitEncodedByteCount()
    {
        byte[] input = [4, 0xe4, 0xb8, 0xad, 0x41];
        var reader = new Reader(input);
        Assert.Equal("中A", reader.ReadString(4));
        reader.EnsureEnd();
        Assert.Throws<InvalidDataException>(() => ReadValue(input, "string", 3));
        Assert.Throws<DecoderFallbackException>(() => ReadValue([2, 0xc3, 0x28], "string"));
        Assert.Throws<DecoderFallbackException>(() => ReadValue([1, 0xc3], "string"));
    }

    [Fact]
    public void BytesBorrowInputAndRawBytesDoNotConsumeALengthPrefix()
    {
        ReadOnlySpan<byte> input = [3, 0x10, 0x20, 0x30, 0x40, 0x50, 2, 0xfe, 0xff, 3, 0];
        var reader = new Reader(input);
        var bytes = reader.ReadBytes(3);
        Assert.Equal(new byte[] { 0x10, 0x20, 0x30 }, bytes.ToArray());
        Assert.True(input.Overlaps(bytes, out var bytesOffset));
        Assert.Equal(1, bytesOffset);

        var rawBytes = reader.ReadRawBytes(2);
        Assert.Equal(new byte[] { 0x40, 0x50 }, rawBytes.ToArray());
        Assert.True(input.Overlaps(rawBytes, out var rawOffset));
        Assert.Equal(4, rawOffset);
        Assert.Equal(2, reader.ReadCount(2));
        Assert.Equal((short)-2, reader.ReadInt16());
        Assert.Equal((short)3, reader.ReadInt16());
        reader.EnsureEnd();
    }

    [Fact]
    public void EmptyFieldsAndZeroCountsDoNotConsumeFollowingValues()
    {
        var reader = new Reader([0, 0, 0, 17, 0, 0, 0]);
        Assert.Equal("", reader.ReadString(0));
        Assert.True(reader.ReadBytes(0).IsEmpty);
        Assert.True(reader.ReadRawBytes(0).IsEmpty);
        Assert.Equal(0, reader.ReadCount(0));
        Assert.Equal(17, reader.ReadInt32());
        reader.EnsureEnd();
    }

    [Fact]
    public void RawReadsRejectTruncationAndEnsureEndRejectsTrailingBytes()
    {
        Assert.Throws<EndOfStreamException>(() => ReadValue([1, 2], "raw", 3));
        Assert.Throws<InvalidDataException>(() => ReadValue([1, 2], "end"));
        var reader = new Reader([]);
        reader.EnsureEnd();
    }

    private static void ReadValue(byte[] input, string kind, int limit = int.MaxValue)
    {
        var reader = new Reader(input);
        switch (kind)
        {
            case "byte":
                _ = reader.ReadByte();
                break;
            case "sbyte":
                _ = reader.ReadSByte();
                break;
            case "bool":
                _ = reader.ReadBoolean();
                break;
            case "int16":
                _ = reader.ReadInt16();
                break;
            case "uint16":
                _ = reader.ReadUInt16();
                break;
            case "int32":
                _ = reader.ReadInt32();
                break;
            case "uint32":
                _ = reader.ReadUInt32();
                break;
            case "int64":
                _ = reader.ReadInt64();
                break;
            case "uint64":
                _ = reader.ReadUInt64();
                break;
            case "single":
                _ = reader.ReadSingle();
                break;
            case "double":
                _ = reader.ReadDouble();
                break;
            case "varuint32":
                _ = reader.ReadVarUInt32();
                break;
            case "varint32":
                _ = reader.ReadVarInt32();
                break;
            case "varuint64":
                _ = reader.ReadVarUInt64();
                break;
            case "varint64":
                _ = reader.ReadVarInt64();
                break;
            case "string":
                _ = reader.ReadString(limit);
                break;
            case "bytes":
                _ = reader.ReadBytes(limit);
                break;
            case "count":
                _ = reader.ReadCount(limit);
                break;
            case "raw":
                _ = reader.ReadRawBytes(limit);
                break;
            case "end":
                _ = reader.ReadByte();
                reader.EnsureEnd();
                break;
            default:
                throw new InvalidOperationException("Unknown test operation.");
        }
    }
}
