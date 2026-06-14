using System.Diagnostics.CodeAnalysis;

namespace PanguEngine.Versioning;

/// <summary>
/// Represents a range of Semantic Versioning 2.0.0 versions.
/// </summary>
public sealed class SemVersionRange
{
    private readonly bool _includeMax;
    private readonly bool _includeMin;
    private readonly SemVersion? _exact;
    private readonly SemVersion _max;
    private readonly SemVersion _min;
    private readonly string _text;

    private SemVersionRange(SemVersion exact)
    {
        _exact = exact;
        _min = exact;
        _includeMin = true;
        _max = exact;
        _includeMax = true;
        _text = exact.ToString();
    }

    private SemVersionRange(SemVersion min, bool includeMin, SemVersion max, bool includeMax, string text)
    {
        _exact = null;
        _min = min;
        _includeMin = includeMin;
        _max = max;
        _includeMax = includeMax;
        _text = text;
    }

    /// <summary>
    /// Parses a semantic version range.
    /// </summary>
    /// <param name="value">The range string to parse.</param>
    /// <returns>The parsed semantic version range.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value" /> is null.</exception>
    /// <exception cref="FormatException">Thrown when <paramref name="value" /> is not a valid semantic version range.</exception>
    public static SemVersionRange Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (TryParse(value, out var range))
            return range;

        throw new FormatException("The value is not a valid semantic version range.");
    }

    /// <summary>
    /// Attempts to parse a semantic version range.
    /// </summary>
    /// <param name="value">The range string to parse.</param>
    /// <param name="range">The parsed semantic version range when parsing succeeds.</param>
    /// <returns>True when parsing succeeds; otherwise, false.</returns>
    public static bool TryParse(string? value, [NotNullWhen(true)] out SemVersionRange? range)
    {
        range = null;
        if (string.IsNullOrEmpty(value))
            return false;

        if (SemVersion.TryParse(value, out var exact))
        {
            range = new SemVersionRange(exact);
            return true;
        }

        if (value.Length < 5)
            return false;

        var includeMin = value[0] == '[';
        var includeMax = value[^1] == ']';
        if (!includeMin && value[0] != '(')
            return false;
        if (!includeMax && value[^1] != ')')
            return false;

        var inner = value[1..^1];
        var parts = inner.Split(',');
        if (parts.Length != 2 ||
            !SemVersion.TryParse(parts[0], out var min) ||
            !SemVersion.TryParse(parts[1], out var max))
            return false;

        var comparison = min.CompareTo(max);
        if (comparison > 0 || comparison == 0 && (!includeMin || !includeMax))
            return false;

        range = new SemVersionRange(min, includeMin, max, includeMax, $"{value[0]}{min},{max}{value[^1]}");
        return true;
    }

    /// <summary>
    /// Returns whether the specified version is inside this range.
    /// </summary>
    /// <param name="version">The version to evaluate.</param>
    /// <returns>True when the version is inside the range; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="version" /> is null.</exception>
    public bool Contains(SemVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);

        if (_exact is not null)
            return version.Equals(_exact);

        var lower = version.CompareTo(_min);
        if (lower < 0 || lower == 0 && !_includeMin)
            return false;

        var upper = version.CompareTo(_max);
        if (upper > 0 || upper == 0 && !_includeMax)
            return false;

        return true;
    }

    /// <inheritdoc />
    public override string ToString() => _text;
}