using System.Diagnostics.CodeAnalysis;

namespace PanguEngine.Versioning;

/// <summary>
/// Represents a range of Semantic Versioning 2.0.0 versions.
/// </summary>
public sealed class SemVersionRange
{
    private readonly Segment[] _segments;
    private readonly string _text;

    private SemVersionRange(Segment[] segments, string text)
    {
        _segments = segments;
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
        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (!TrySplitSegments(value, out var parts))
            return false;

        var segments = new Segment[parts.Count];
        for (var i = 0; i < parts.Count; i++)
        {
            if (!TryParseSegment(parts[i], out segments[i]))
                return false;
        }

        range = new SemVersionRange(segments, string.Join(',', segments.Select(segment => segment.Text)));
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

        foreach (var segment in _segments)
        {
            if (Contains(segment, version))
                return true;
        }

        return false;
    }

    /// <inheritdoc />
    public override string ToString() => _text;

    private static bool TrySplitSegments(string value, out List<string> parts)
    {
        parts = [];
        var start = 0;
        var depth = 0;

        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (c is '[' or '(')
            {
                if (depth != 0)
                    return false;

                depth = 1;
                continue;
            }

            if (c is ']' or ')')
            {
                if (depth != 1)
                    return false;

                depth = 0;
                continue;
            }

            if (c != ',' || depth != 0)
                continue;

            if (!AddSegmentPart(value[start..i], parts))
                return false;

            start = i + 1;
        }

        return depth == 0 && AddSegmentPart(value[start..], parts);
    }

    private static bool AddSegmentPart(string part, List<string> parts)
    {
        if (string.IsNullOrWhiteSpace(part))
            return false;

        parts.Add(part);
        return true;
    }

    private static bool TryParseSegment(string value, out Segment segment)
    {
        segment = default;

        if (SemVersion.TryParse(value, out var exact))
        {
            segment = new Segment(exact, exact, true, exact, true, exact.ToString());
            return true;
        }

        return TryParseInterval(value, out segment);
    }

    private static bool TryParseInterval(string value, out Segment segment)
    {
        segment = default;
        if (value.Length < 3)
            return false;

        var includeMin = value[0] == '[';
        var includeMax = value[^1] == ']';
        if (!includeMin && value[0] != '(')
            return false;
        if (!includeMax && value[^1] != ')')
            return false;

        var inner = value[1..^1];
        var comma = inner.IndexOf(',');
        if (comma < 0 || comma != inner.LastIndexOf(','))
            return false;

        var minText = inner[..comma];
        var maxText = inner[(comma + 1)..];
        if (minText.Length == 0 && maxText.Length == 0)
            return false;
        if (minText.Length == 0 && includeMin)
            return false;
        if (maxText.Length == 0 && includeMax)
            return false;

        SemVersion? min = null;
        if (minText.Length > 0 && !SemVersion.TryParse(minText, out min))
            return false;

        SemVersion? max = null;
        if (maxText.Length > 0 && !SemVersion.TryParse(maxText, out max))
            return false;

        if (min is not null && max is not null)
        {
            var comparison = min.CompareTo(max);
            if (comparison > 0 || comparison == 0 && (!includeMin || !includeMax))
                return false;
        }

        var normalizedMin = min?.ToString() ?? string.Empty;
        var normalizedMax = max?.ToString() ?? string.Empty;
        segment = new Segment(null, min, includeMin, max, includeMax,
            $"{value[0]}{normalizedMin},{normalizedMax}{value[^1]}");
        return true;
    }

    private static bool Contains(Segment segment, SemVersion version)
    {
        if (segment.Exact is not null)
            return version.Equals(segment.Exact);

        if (segment.Min is not null)
        {
            var lower = version.CompareTo(segment.Min);
            if (lower < 0 || lower == 0 && !segment.IncludeMin)
                return false;
        }

        if (segment.Max is not null)
        {
            var upper = version.CompareTo(segment.Max);
            if (upper > 0 || upper == 0 && !segment.IncludeMax)
                return false;
        }

        return true;
    }

    private readonly record struct Segment(
        SemVersion? Exact,
        SemVersion? Min,
        bool IncludeMin,
        SemVersion? Max,
        bool IncludeMax,
        string Text);
}