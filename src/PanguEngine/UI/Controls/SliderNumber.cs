using System.Globalization;
using System.Numerics;

namespace PanguEngine.UI.Controls;

internal readonly struct SliderNumber
{
    private readonly BigInteger _numerator;
    private readonly BigInteger _denominator;

    internal SliderNumber(BigInteger value) : this(value, BigInteger.One) { }

    private SliderNumber(BigInteger numerator, BigInteger denominator)
    {
        if (denominator.Sign < 0)
        {
            numerator = -numerator;
            denominator = -denominator;
        }
        var divisor = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), denominator);
        _numerator = numerator / divisor;
        _denominator = denominator / divisor;
    }

    internal static SliderNumber From<T>(T value) where T : INumber<T> =>
        Parse(value.ToString(null, CultureInfo.InvariantCulture));

    private static SliderNumber Parse(string text)
    {
        var exponentIndex = text.IndexOfAny(['E', 'e']);
        var exponent = exponentIndex < 0 ? 0 : int.Parse(text[(exponentIndex + 1)..], CultureInfo.InvariantCulture);
        var mantissa = exponentIndex < 0 ? text : text[..exponentIndex];
        var decimalIndex = mantissa.IndexOf('.');
        var fractionalDigits = decimalIndex < 0 ? 0 : mantissa.Length - decimalIndex - 1;
        var numerator = BigInteger.Parse(mantissa.Replace(".", string.Empty), CultureInfo.InvariantCulture);
        var scale = fractionalDigits - exponent;
        return scale >= 0
            ? new SliderNumber(numerator, BigInteger.Pow(10, scale))
            : new SliderNumber(numerator * BigInteger.Pow(10, -scale));
    }

    internal BigInteger Floor()
    {
        var quotient = BigInteger.DivRem(_numerator, _denominator, out var remainder);
        return remainder.Sign < 0 ? quotient - 1 : quotient;
    }

    internal BigInteger Ceiling()
    {
        var quotient = BigInteger.DivRem(_numerator, _denominator, out var remainder);
        return remainder.Sign > 0 ? quotient + 1 : quotient;
    }

    internal BigInteger RoundUpAtMidpoint() => (this + new SliderNumber(1, 2)).Floor();

    internal T ToValue<T>(bool integral) where T : INumber<T> => integral
        ? T.CreateChecked(RoundUpAtMidpoint())
        : T.Parse(ToScientificText(), NumberStyles.Float, CultureInfo.InvariantCulture);

    internal double ToDouble() => double.Parse(ToScientificText(), CultureInfo.InvariantCulture);

    private string ToScientificText()
    {
        if (_numerator.IsZero)
            return "0";
        const int significantDigits = 40;
        var numerator = BigInteger.Abs(_numerator);
        var exponent = numerator.ToString(CultureInfo.InvariantCulture).Length - _denominator.ToString(CultureInfo.InvariantCulture).Length;
        var comparison = exponent >= 0
            ? numerator.CompareTo(_denominator * BigInteger.Pow(10, exponent))
            : (numerator * BigInteger.Pow(10, -exponent)).CompareTo(_denominator);
        if (comparison < 0)
            exponent--;
        var scale = significantDigits - 1 - exponent;
        var scaledNumerator = scale >= 0 ? numerator * BigInteger.Pow(10, scale) : numerator;
        var scaledDenominator = scale >= 0 ? _denominator : _denominator * BigInteger.Pow(10, -scale);
        var coefficient = BigInteger.DivRem(scaledNumerator, scaledDenominator, out var remainder);
        if (remainder * 2 >= scaledDenominator)
            coefficient++;
        var digits = coefficient.ToString(CultureInfo.InvariantCulture);
        if (digits.Length > significantDigits)
        {
            digits = digits[..significantDigits];
            exponent++;
        }
        return string.Concat(_numerator.Sign < 0 ? "-" : string.Empty, digits[..1], ".", digits[1..], "E", exponent.ToString(CultureInfo.InvariantCulture));
    }

    public static SliderNumber operator +(SliderNumber left, SliderNumber right) =>
        new(left._numerator * right._denominator + right._numerator * left._denominator, left._denominator * right._denominator);

    public static SliderNumber operator -(SliderNumber left, SliderNumber right) =>
        new(left._numerator * right._denominator - right._numerator * left._denominator, left._denominator * right._denominator);

    public static SliderNumber operator *(SliderNumber left, SliderNumber right) =>
        new(left._numerator * right._numerator, left._denominator * right._denominator);

    public static SliderNumber operator /(SliderNumber left, SliderNumber right) =>
        new(left._numerator * right._denominator, left._denominator * right._numerator);

    public static bool operator <(SliderNumber left, SliderNumber right) =>
        left._numerator * right._denominator < right._numerator * left._denominator;

    public static bool operator >(SliderNumber left, SliderNumber right) => right < left;

    public static bool operator <=(SliderNumber left, SliderNumber right) => !(left > right);

    public static bool operator >=(SliderNumber left, SliderNumber right) => !(left < right);
}
