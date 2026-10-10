namespace PanguEngine.Client.UI;

internal static class UiLayoutHelper
{
    private const int RoundUpNormalizationDigits = 8;
    private const double PhysicalSnapTolerance = 1e-10;

    internal static double RoundLayoutValue(double value, double scale) =>
        RoundCore(value, scale, roundUp: false);

    internal static double RoundLayoutValueUp(double value, double scale) =>
        RoundCore(value, scale, roundUp: true);

    internal static Point RoundLayoutPoint(Point value, double scale) =>
        new(
            RoundLayoutValue(value.X, scale),
            RoundLayoutValue(value.Y, scale));

    internal static Size RoundLayoutSizeUp(Size value, double scale) =>
        new(
            RoundLayoutValueUp(value.Width, scale),
            RoundLayoutValueUp(value.Height, scale));

    internal static Thickness RoundLayoutThickness(Thickness value, double scale) =>
        new(
            RoundLayoutValue(value.Left, scale),
            RoundLayoutValue(value.Top, scale),
            RoundLayoutValue(value.Right, scale),
            RoundLayoutValue(value.Bottom, scale));

    internal static Rect RoundLayoutRect(Rect value, double scale) =>
        new(
            RoundLayoutValue(value.X, scale),
            RoundLayoutValue(value.Y, scale),
            RoundLayoutValueUp(value.Width, scale),
            RoundLayoutValueUp(value.Height, scale));

    private static double RoundCore(double value, double scale, bool roundUp)
    {
        var adjustedValue = value;
        if (roundUp)
        {
            adjustedValue = Math.Round(value, RoundUpNormalizationDigits, MidpointRounding.ToZero);
            if (value > 0 && adjustedValue == 0)
                adjustedValue = value;
        }
        var physicalValue = adjustedValue * scale;
        if (!double.IsFinite(physicalValue))
            throw new InvalidOperationException("Layout rounding produced a non-finite physical value.");

        if (roundUp)
        {
            var nearest = Math.Round(physicalValue, MidpointRounding.AwayFromZero);
            if (nearest > 0 && physicalValue > nearest &&
                physicalValue - nearest <= PhysicalSnapTolerance &&
                physicalValue == Math.BitIncrement(nearest))
                physicalValue = nearest;
        }

        var roundedPhysicalValue = roundUp
            ? Math.Ceiling(physicalValue)
            : Math.Round(physicalValue);
        var result = roundedPhysicalValue / scale;
        if (!double.IsFinite(result))
            throw new InvalidOperationException("Layout rounding produced a non-finite logical value.");

        return result;
    }
}
