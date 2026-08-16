namespace PanguEngine.Graphics.Text;

/// <summary>
/// Describes a size-independent font request.
/// </summary>
public sealed record Font
{
    /// <summary>
    /// Initializes a font request.
    /// </summary>
    /// <param name="familyName">The requested font family name.</param>
    /// <param name="weight">The requested font weight.</param>
    /// <param name="style">The requested font style.</param>
    public Font(
        string familyName,
        FontWeight weight = FontWeight.Normal,
        FontStyle style = FontStyle.Normal)
    {
        FamilyName = familyName;
        Weight = weight;
        Style = style;
    }

    /// <summary>
    /// Gets the requested font family name.
    /// </summary>
    public string FamilyName
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    }

    /// <summary>
    /// Gets the requested font weight.
    /// </summary>
    public FontWeight Weight { get; init; }

    /// <summary>
    /// Gets the requested font style.
    /// </summary>
    public FontStyle Style { get; init; }

    /// <inheritdoc />
    public bool Equals(Font? other)
    {
        return other is not null &&
               Weight == other.Weight &&
               Style == other.Style &&
               string.Equals(FamilyName, other.FamilyName, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(
            StringComparer.OrdinalIgnoreCase.GetHashCode(FamilyName),
            Weight,
            Style);
    }
}
