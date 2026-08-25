namespace PanguEngine.Client.UI;

/// <summary>
/// Provides process-wide UI settings.
/// </summary>
public static class UiSettings
{
    /// <summary>
    /// Gets or sets the default scale used by UI roots without a local scale.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the value is not finite or is not greater than zero.
    /// </exception>
    public static double DefaultScale
    {
        get => Volatile.Read(ref field);
        set
        {
            ValidateScale(value, nameof(value));
            Volatile.Write(ref field, value);
        }
    } = 1;

    internal static void ValidateScale(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "UI scale must be finite and greater than zero.");
        }
    }
}
