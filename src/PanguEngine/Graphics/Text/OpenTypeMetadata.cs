using System.Buffers.Binary;
using System.Text;

namespace PanguEngine.Graphics.Text;

internal readonly record struct OpenTypeMetadata(
    string FamilyName,
    FontWeight Weight,
    FontStyle Style)
{
    internal static OpenTypeMetadata Read(ReadOnlySpan<byte> data, int faceIndex)
    {
        var fontOffset = GetFontOffset(data, faceIndex);
        var nameTable = GetTable(data, fontOffset, 0x6E616D65);
        var os2Table = GetTable(data, fontOffset, 0x4F532F32);
        var familyName = ReadName(nameTable, 16) ?? ReadName(nameTable, 1)
            ?? throw new InvalidDataException("The font face does not define a family name.");
        var weight = ReadWeight(os2Table);
        var style = ReadStyle(os2Table);
        return new OpenTypeMetadata(familyName, weight, style);
    }

    private static int GetFontOffset(ReadOnlySpan<byte> data, int faceIndex)
    {
        EnsureRange(data, 0, 12);
        if (BinaryPrimitives.ReadUInt32BigEndian(data) != 0x74746366)
        {
            ArgumentOutOfRangeException.ThrowIfNotEqual(faceIndex, 0);
            return 0;
        }

        var count = checked((int)BinaryPrimitives.ReadUInt32BigEndian(data[8..]));
        if ((uint)faceIndex >= (uint)count)
            throw new ArgumentOutOfRangeException(nameof(faceIndex));
        EnsureRange(data, 12, checked(count * 4));
        return checked((int)BinaryPrimitives.ReadUInt32BigEndian(data[(12 + faceIndex * 4)..]));
    }

    private static ReadOnlySpan<byte> GetTable(ReadOnlySpan<byte> data, int fontOffset, uint tag)
    {
        EnsureRange(data, fontOffset, 12);
        var count = BinaryPrimitives.ReadUInt16BigEndian(data[(fontOffset + 4)..]);
        var recordsOffset = checked(fontOffset + 12);
        EnsureRange(data, recordsOffset, checked(count * 16));
        for (var i = 0; i < count; i++)
        {
            var record = data[(recordsOffset + i * 16)..];
            if (BinaryPrimitives.ReadUInt32BigEndian(record) != tag)
                continue;

            var offset = checked((int)BinaryPrimitives.ReadUInt32BigEndian(record[8..]));
            var length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(record[12..]));
            EnsureRange(data, offset, length);
            return data.Slice(offset, length);
        }

        throw new InvalidDataException($"The OpenType table 0x{tag:X8} is missing.");
    }

    private static string? ReadName(ReadOnlySpan<byte> table, ushort requestedNameId)
    {
        EnsureRange(table, 0, 6);
        var count = BinaryPrimitives.ReadUInt16BigEndian(table[2..]);
        var stringsOffset = BinaryPrimitives.ReadUInt16BigEndian(table[4..]);
        EnsureRange(table, 6, checked(count * 12));
        string? fallback = null;
        for (var i = 0; i < count; i++)
        {
            var record = table[(6 + i * 12)..];
            var platform = BinaryPrimitives.ReadUInt16BigEndian(record);
            var language = BinaryPrimitives.ReadUInt16BigEndian(record[4..]);
            var nameId = BinaryPrimitives.ReadUInt16BigEndian(record[6..]);
            if (nameId != requestedNameId)
                continue;

            var length = BinaryPrimitives.ReadUInt16BigEndian(record[8..]);
            var offset = checked(stringsOffset + BinaryPrimitives.ReadUInt16BigEndian(record[10..]));
            EnsureRange(table, offset, length);
            var value = DecodeName(platform, table.Slice(offset, length));
            if (string.IsNullOrEmpty(value))
                continue;
            if (platform is 0 or 3 && language is 0 or 0x0409)
                return value;
            fallback ??= value;
        }

        return fallback;
    }

    private static string DecodeName(ushort platform, ReadOnlySpan<byte> data)
    {
        if (platform is 0 or 3)
        {
            if ((data.Length & 1) != 0)
                return string.Empty;
            var chars = new char[data.Length / 2];
            for (var i = 0; i < chars.Length; i++)
                chars[i] = (char)BinaryPrimitives.ReadUInt16BigEndian(data[(i * 2)..]);
            return new string(chars).TrimEnd('\0');
        }

        return Encoding.Latin1.GetString(data).TrimEnd('\0');
    }

    private static FontWeight ReadWeight(ReadOnlySpan<byte> table)
    {
        EnsureRange(table, 0, 6);
        var value = BinaryPrimitives.ReadUInt16BigEndian(table[4..]);
        var rounded = Math.Clamp((int)Math.Round(value / 100d, MidpointRounding.AwayFromZero) * 100, 100, 900);
        return (FontWeight)rounded;
    }

    private static FontStyle ReadStyle(ReadOnlySpan<byte> table)
    {
        if (table.Length < 64)
            return FontStyle.Normal;
        var selection = BinaryPrimitives.ReadUInt16BigEndian(table[62..]);
        if ((selection & (1 << 9)) != 0)
            return FontStyle.Oblique;
        return (selection & 1) != 0 ? FontStyle.Italic : FontStyle.Normal;
    }

    private static void EnsureRange(ReadOnlySpan<byte> data, int offset, int length)
    {
        if (offset < 0 || length < 0 || offset > data.Length - length)
            throw new InvalidDataException("The OpenType table contains an invalid range.");
    }
}
