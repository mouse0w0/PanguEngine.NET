using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using PanguEngine.Network.Serialization;

namespace PanguEngine.Tests.Network.Serialization;

public sealed class WriterTests
{
    [Fact]
    public void ScalarsWriteTheirSpecifiedLittleEndianBytes()
    {
        var output = new ArrayBufferWriter<byte>(1);
        var writer = new Writer(output);
        writer.WriteBoolean(false);
        writer.WriteBoolean(true);
        writer.WriteByte(0xab);
        writer.WriteSByte(-2);
        writer.WriteInt16(-2);
        writer.WriteUInt16(0xfedc);
        writer.WriteInt32(int.MinValue);
        writer.WriteUInt32(0x89abcdef);
        writer.WriteInt64(long.MinValue);
        writer.WriteUInt64(0xfedcba9876543210);
        writer.WriteSingle(1.5f);
        writer.WriteDouble(-2.5);

        byte[] expected =
        [
            0x00, 0x01, 0xab, 0xfe,
            0xfe, 0xff, 0xdc, 0xfe,
            0x00, 0x00, 0x00, 0x80, 0xef, 0xcd, 0xab, 0x89,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x80,
            0x10, 0x32, 0x54, 0x76, 0x98, 0xba, 0xdc, 0xfe,
            0x00, 0x00, 0xc0, 0x3f,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x04, 0xc0
        ];
        Assert.Equal(expected, output.WrittenSpan.ToArray());
    }

    [Theory]
    [InlineData(unchecked((int)0x80000000), unchecked((long)0x8000000000000000))]
    [InlineData(0x7f800000, 0x7ff0000000000000)]
    [InlineData(unchecked((int)0xff800000), unchecked((long)0xfff0000000000000))]
    [InlineData(0x7fc01234, 0x7ff8000000001234)]
    public void FloatingPointSpecialValuesPreserveTheirBits(int singleBits, long doubleBits)
    {
        var output = new ArrayBufferWriter<byte>();
        var writer = new Writer(output);
        writer.WriteSingle(BitConverter.Int32BitsToSingle(singleBits));
        writer.WriteDouble(BitConverter.Int64BitsToDouble(doubleBits));
        Assert.Equal(singleBits, BinaryPrimitives.ReadInt32LittleEndian(output.WrittenSpan));
        Assert.Equal(doubleBits, BinaryPrimitives.ReadInt64LittleEndian(output.WrittenSpan[4..]));
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
    [InlineData(int.MaxValue, "FFFFFFFF07")]
    [InlineData(0x80000000UL, "8080808008")]
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
    public void UnsignedVarIntegersWriteTheirShortestKnownBytes(ulong value, string hex)
    {
        var expected = Convert.FromHexString(hex);
        var output = new ArrayBufferWriter<byte>();
        var writer = new Writer(output);
        writer.WriteVarUInt64(value);
        Assert.Equal(expected, output.WrittenSpan.ToArray());
        var rawWideOutput = new ArrayBufferWriter<byte>();
        var rawWideWriter = new Writer(rawWideOutput);
        rawWideWriter.WriteVarInt64(unchecked((long)value));
        Assert.Equal(expected, rawWideOutput.WrittenSpan.ToArray());
        if (value <= uint.MaxValue)
        {
            var narrowOutput = new ArrayBufferWriter<byte>();
            var narrowWriter = new Writer(narrowOutput);
            narrowWriter.WriteVarUInt32((uint)value);
            Assert.Equal(expected, narrowOutput.WrittenSpan.ToArray());
            var rawOutput = new ArrayBufferWriter<byte>();
            var rawWriter = new Writer(rawOutput);
            rawWriter.WriteVarInt32(unchecked((int)value));
            Assert.Equal(expected, rawOutput.WrittenSpan.ToArray());
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
    public void SignedVarIntegersWriteZigZagAcrossTheFullRange(long value, string hex)
    {
        var expected = Convert.FromHexString(hex);
        var output = new ArrayBufferWriter<byte>();
        var writer = new Writer(output);
        writer.WriteVarInt64ZigZag(value);
        Assert.Equal(expected, output.WrittenSpan.ToArray());
        if (value >= int.MinValue && value <= int.MaxValue)
        {
            var narrowOutput = new ArrayBufferWriter<byte>();
            var narrowWriter = new Writer(narrowOutput);
            narrowWriter.WriteVarInt32ZigZag((int)value);
            Assert.Equal(expected, narrowOutput.WrittenSpan.ToArray());
        }
    }

    [Fact]
    public void CountsUseUnsignedVariableLengthPrefixes()
    {
        var output = new ArrayBufferWriter<byte>();
        var writer = new Writer(output);
        writer.WriteCount(0);
        writer.WriteCount(128);
        writer.WriteCount(int.MaxValue);
        Assert.Equal(Convert.FromHexString("008001FFFFFFFF07"), output.WrittenSpan.ToArray());
    }

    [Fact]
    public void StringsUseUtf8ByteCountWithAVariableLengthPrefix()
    {
        var output = new ArrayBufferWriter<byte>();
        var writer = new Writer(output);
        writer.WriteString("中A");
        Assert.Equal(new byte[] { 4, 0xe4, 0xb8, 0xad, 0x41 }, output.WrittenSpan.ToArray());
        Assert.Throws<EncoderFallbackException>(() => WriteString("\ud800"));
    }

    [Fact]
    public void RawBytesDoNotWriteALengthPrefix()
    {
        var output = new ArrayBufferWriter<byte>();
        var writer = new Writer(output);
        writer.WriteBytes([0x10, 0x20, 0x30]);
        writer.WriteRawBytes([0x40, 0x50]);
        writer.WriteCount(2);
        writer.WriteInt16(-2);
        writer.WriteInt16(3);
        byte[] expected = [3, 0x10, 0x20, 0x30, 0x40, 0x50, 2, 0xfe, 0xff, 3, 0];
        Assert.Equal(expected, output.WrittenSpan.ToArray());
    }

    [Fact]
    public void EmptyFieldsAndZeroCountsWriteSingleBytePrefixes()
    {
        var output = new ArrayBufferWriter<byte>();
        var writer = new Writer(output);
        writer.WriteString("");
        writer.WriteBytes(ReadOnlySpan<byte>.Empty);
        writer.WriteRawBytes(ReadOnlySpan<byte>.Empty);
        writer.WriteCount(0);
        writer.WriteInt32(17);
        byte[] expected = [0, 0, 0, 17, 0, 0, 0];
        Assert.Equal(expected, output.WrittenSpan.ToArray());
    }

    private static void WriteString(string value)
    {
        var writer = new Writer(new ArrayBufferWriter<byte>());
        writer.WriteString(value);
    }
}
