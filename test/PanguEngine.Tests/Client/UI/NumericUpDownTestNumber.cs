using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;

namespace PanguEngine.Tests.Client.UI;

/// <summary>Provides a reference number for testing typed editing and numeric contract failures.</summary>
internal sealed record NumericUpDownTestNumber(decimal Number) : INumber<NumericUpDownTestNumber>
{
    /// <summary>Gets whether arithmetic should return a non-finite test result.</summary>
    internal bool ProduceNaN { get; init; }
    private bool NaN { get; init; }

    public static NumericUpDownTestNumber Zero => new(0);
    public static NumericUpDownTestNumber One => new(1);
    public static NumericUpDownTestNumber AdditiveIdentity => Zero;
    public static NumericUpDownTestNumber MultiplicativeIdentity => One;
    public static int Radix => 10;
    public int CompareTo(NumericUpDownTestNumber? other) => other is null ? 1 : Number.CompareTo(other.Number);
    public int CompareTo(object? obj) => CompareTo((NumericUpDownTestNumber?)obj);
    public static bool operator <(NumericUpDownTestNumber x, NumericUpDownTestNumber y) => x.Number < y.Number;
    public static bool operator >(NumericUpDownTestNumber x, NumericUpDownTestNumber y) => x.Number > y.Number;
    public static bool operator <=(NumericUpDownTestNumber x, NumericUpDownTestNumber y) => x.Number <= y.Number;
    public static bool operator >=(NumericUpDownTestNumber x, NumericUpDownTestNumber y) => x.Number >= y.Number;
    public static NumericUpDownTestNumber operator +(NumericUpDownTestNumber x, NumericUpDownTestNumber y) =>
        x.ProduceNaN ? new(0) { NaN = true } : new(x.Number + y.Number);
    public static NumericUpDownTestNumber operator checked +(NumericUpDownTestNumber x, NumericUpDownTestNumber y) => x + y;
    public static NumericUpDownTestNumber operator -(NumericUpDownTestNumber x, NumericUpDownTestNumber y) => new(x.Number - y.Number);
    public static NumericUpDownTestNumber operator checked -(NumericUpDownTestNumber x, NumericUpDownTestNumber y) => x - y;
    public static NumericUpDownTestNumber operator *(NumericUpDownTestNumber x, NumericUpDownTestNumber y) => new(x.Number * y.Number);
    public static NumericUpDownTestNumber operator /(NumericUpDownTestNumber x, NumericUpDownTestNumber y) => new(x.Number / y.Number);
    public static NumericUpDownTestNumber operator %(NumericUpDownTestNumber x, NumericUpDownTestNumber y) => new(x.Number % y.Number);
    public static NumericUpDownTestNumber operator +(NumericUpDownTestNumber x) => x;
    public static NumericUpDownTestNumber operator -(NumericUpDownTestNumber x) => new(-x.Number);
    public static NumericUpDownTestNumber operator ++(NumericUpDownTestNumber x) => x + One;
    public static NumericUpDownTestNumber operator --(NumericUpDownTestNumber x) => x - One;
    public static NumericUpDownTestNumber Abs(NumericUpDownTestNumber value) => new(Math.Abs(value.Number));
    public static bool IsCanonical(NumericUpDownTestNumber value) => true;
    public static bool IsComplexNumber(NumericUpDownTestNumber value) => false;
    public static bool IsEvenInteger(NumericUpDownTestNumber value) => value.Number % 2 == 0;
    public static bool IsFinite(NumericUpDownTestNumber value) => !value.NaN;
    public static bool IsImaginaryNumber(NumericUpDownTestNumber value) => false;
    public static bool IsInfinity(NumericUpDownTestNumber value) => false;
    public static bool IsInteger(NumericUpDownTestNumber value) => decimal.Truncate(value.Number) == value.Number;
    public static bool IsNaN(NumericUpDownTestNumber value) => value.NaN;
    public static bool IsNegative(NumericUpDownTestNumber value) => value.Number < 0;
    public static bool IsNegativeInfinity(NumericUpDownTestNumber value) => false;
    public static bool IsNormal(NumericUpDownTestNumber value) => IsFinite(value) && value.Number != 0;
    public static bool IsOddInteger(NumericUpDownTestNumber value) => IsInteger(value) && value.Number % 2 != 0;
    public static bool IsPositive(NumericUpDownTestNumber value) => value.Number >= 0;
    public static bool IsPositiveInfinity(NumericUpDownTestNumber value) => false;
    public static bool IsRealNumber(NumericUpDownTestNumber value) => IsFinite(value);
    public static bool IsSubnormal(NumericUpDownTestNumber value) => false;
    public static bool IsZero(NumericUpDownTestNumber value) => IsFinite(value) && value.Number == 0;
    public static NumericUpDownTestNumber MaxMagnitude(NumericUpDownTestNumber x, NumericUpDownTestNumber y) => Math.Abs(x.Number) >= Math.Abs(y.Number) ? x : y;
    public static NumericUpDownTestNumber MinMagnitude(NumericUpDownTestNumber x, NumericUpDownTestNumber y) => Math.Abs(x.Number) <= Math.Abs(y.Number) ? x : y;
    public static NumericUpDownTestNumber MaxMagnitudeNumber(NumericUpDownTestNumber x, NumericUpDownTestNumber y) => MaxMagnitude(x, y);
    public static NumericUpDownTestNumber MinMagnitudeNumber(NumericUpDownTestNumber x, NumericUpDownTestNumber y) => MinMagnitude(x, y);

    public static NumericUpDownTestNumber Parse(string s, IFormatProvider? provider) => new(decimal.Parse(s, provider));
    public static NumericUpDownTestNumber Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => new(decimal.Parse(s, provider));
    public static NumericUpDownTestNumber Parse(string s, NumberStyles style, IFormatProvider? provider) => new(decimal.Parse(s, style, provider));
    public static NumericUpDownTestNumber Parse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider) => new(decimal.Parse(s, style, provider));
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out NumericUpDownTestNumber result) =>
        TryParse(s, NumberStyles.Number, provider, out result);
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out NumericUpDownTestNumber result) =>
        TryParse(s.ToString(), NumberStyles.Number, provider, out result);
    public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider, out NumericUpDownTestNumber result) =>
        TryParse(s.ToString(), style, provider, out result);
    public static bool TryParse([NotNullWhen(true)] string? s, NumberStyles style, IFormatProvider? provider, out NumericUpDownTestNumber result)
    {
        if (s == "throw")
            throw new OverflowException("custom parsing");
        var success = decimal.TryParse(s, style, provider, out var parsed);
        result = new(parsed);
        return success;
    }

    public string ToString(string? format, IFormatProvider? formatProvider) => format == "throw"
        ? throw new OverflowException("custom formatting")
        : Number.ToString(format, formatProvider);
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) =>
        Number.TryFormat(destination, out charsWritten, format, provider);

    static bool INumberBase<NumericUpDownTestNumber>.TryConvertFromChecked<TOther>(TOther value, out NumericUpDownTestNumber result)
    {
        result = new(decimal.CreateChecked(value));
        return true;
    }
    static bool INumberBase<NumericUpDownTestNumber>.TryConvertFromSaturating<TOther>(TOther value, out NumericUpDownTestNumber result)
    {
        result = new(decimal.CreateSaturating(value));
        return true;
    }
    static bool INumberBase<NumericUpDownTestNumber>.TryConvertFromTruncating<TOther>(TOther value, out NumericUpDownTestNumber result)
    {
        result = new(decimal.CreateTruncating(value));
        return true;
    }
    static bool INumberBase<NumericUpDownTestNumber>.TryConvertToChecked<TOther>(NumericUpDownTestNumber value, out TOther result)
    {
        result = TOther.CreateChecked(value.Number);
        return true;
    }
    static bool INumberBase<NumericUpDownTestNumber>.TryConvertToSaturating<TOther>(NumericUpDownTestNumber value, out TOther result)
    {
        result = TOther.CreateSaturating(value.Number);
        return true;
    }
    static bool INumberBase<NumericUpDownTestNumber>.TryConvertToTruncating<TOther>(NumericUpDownTestNumber value, out TOther result)
    {
        result = TOther.CreateTruncating(value.Number);
        return true;
    }
}
