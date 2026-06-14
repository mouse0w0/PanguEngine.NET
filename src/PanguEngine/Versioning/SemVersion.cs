using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace PanguEngine.Versioning;

/// <summary>
/// Represents a Semantic Versioning 2.0.0 version.
/// </summary>
public sealed class SemVersion : IComparable<SemVersion>, IEquatable<SemVersion>
{
    private readonly string[] _prereleaseIdentifiers;
    private readonly string _text;

    private SemVersion(int major, int minor, int patch, string? prerelease, string? metadata)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        Prerelease = prerelease;
        Metadata = metadata;
        IsPrerelease = prerelease is not null;
        _prereleaseIdentifiers = prerelease?.Split('.') ?? [];
        _text = $"{Major}.{Minor}.{Patch}" +
                (Prerelease is null ? string.Empty : $"-{Prerelease}") +
                (Metadata is null ? string.Empty : $"+{Metadata}");
    }

    /// <summary>
    /// Gets the major version number.
    /// </summary>
    public int Major { get; }

    /// <summary>
    /// Gets the minor version number.
    /// </summary>
    public int Minor { get; }

    /// <summary>
    /// Gets the patch version number.
    /// </summary>
    public int Patch { get; }

    /// <summary>
    /// Gets whether the version has a pre-release label.
    /// </summary>
    public bool IsPrerelease { get; }

    /// <summary>
    /// Gets the pre-release label.
    /// </summary>
    public string? Prerelease { get; }

    /// <summary>
    /// Gets the build metadata label.
    /// </summary>
    public string? Metadata { get; }

    /// <summary>
    /// Parses a Semantic Versioning 2.0.0 version string.
    /// </summary>
    /// <param name="value">The version string to parse.</param>
    /// <returns>The parsed semantic version.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value" /> is null.</exception>
    /// <exception cref="FormatException">Thrown when <paramref name="value" /> is not a valid semantic version.</exception>
    public static SemVersion Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (TryParse(value, out var version))
            return version;

        throw new FormatException("The value is not a valid semantic version.");
    }

    /// <summary>
    /// Attempts to parse a Semantic Versioning 2.0.0 version string.
    /// </summary>
    /// <param name="value">The version string to parse.</param>
    /// <param name="version">The parsed semantic version when parsing succeeds.</param>
    /// <returns>True when parsing succeeds; otherwise, false.</returns>
    public static bool TryParse(string? value, [NotNullWhen(true)] out SemVersion? version)
    {
        version = null;
        if (string.IsNullOrEmpty(value))
            return false;

        var metadataStart = value.IndexOf('+');
        var withoutMetadata = value;
        string? metadata = null;
        if (metadataStart >= 0)
        {
            withoutMetadata = value[..metadataStart];
            metadata = value[(metadataStart + 1)..];
            if (!IsValidBuild(metadata))
                return false;
        }

        var prereleaseStart = withoutMetadata.IndexOf('-');
        var core = withoutMetadata;
        string? prerelease = null;
        if (prereleaseStart >= 0)
        {
            core = withoutMetadata[..prereleaseStart];
            prerelease = withoutMetadata[(prereleaseStart + 1)..];
            if (!IsValidPrerelease(prerelease))
                return false;
        }

        var coreParts = core.Split('.');
        if (coreParts.Length != 3 ||
            !TryParseCoreNumber(coreParts[0], out var major) ||
            !TryParseCoreNumber(coreParts[1], out var minor) ||
            !TryParseCoreNumber(coreParts[2], out var patch))
            return false;

        version = new SemVersion(major, minor, patch, prerelease, metadata);
        return true;
    }

    /// <summary>
    /// Compares this version to another version using SemVer precedence rules.
    /// </summary>
    /// <param name="other">The version to compare against.</param>
    /// <returns>A value less than zero, zero, or greater than zero when this version has lower, equal, or higher precedence.</returns>
    public int CompareTo(SemVersion? other)
    {
        if (other is null)
            return 1;

        var core = Major.CompareTo(other.Major);
        if (core != 0) return core;
        core = Minor.CompareTo(other.Minor);
        if (core != 0) return core;
        core = Patch.CompareTo(other.Patch);
        if (core != 0) return core;

        if (!IsPrerelease && !other.IsPrerelease) return 0;
        if (!IsPrerelease) return 1;
        if (!other.IsPrerelease) return -1;

        return ComparePrerelease(other);
    }

    /// <summary>
    /// Determines whether this version has the same normalized text as another version.
    /// </summary>
    /// <param name="other">The version to compare against.</param>
    /// <returns>True when both versions have the same normalized text; otherwise, false.</returns>
    public bool Equals(SemVersion? other) => other is not null && _text == other._text;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is SemVersion other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(_text);

    /// <inheritdoc />
    public override string ToString() => _text;

    /// <summary>
    /// Returns whether two versions have the same normalized text.
    /// </summary>
    /// <param name="left">The left version.</param>
    /// <param name="right">The right version.</param>
    /// <returns>True when both versions have the same normalized text; otherwise, false.</returns>
    public static bool operator ==(SemVersion? left, SemVersion? right) =>
        left is null ? right is null : left.Equals(right);

    /// <summary>
    /// Returns whether two versions have different normalized text.
    /// </summary>
    /// <param name="left">The left version.</param>
    /// <param name="right">The right version.</param>
    /// <returns>True when the versions have different normalized text; otherwise, false.</returns>
    public static bool operator !=(SemVersion? left, SemVersion? right) => !(left == right);

    /// <summary>
    /// Returns whether the left version has lower precedence than the right version.
    /// </summary>
    /// <param name="left">The left version.</param>
    /// <param name="right">The right version.</param>
    /// <returns>True when the left version has lower precedence; otherwise, false.</returns>
    public static bool operator <(SemVersion left, SemVersion right) => left.CompareTo(right) < 0;

    /// <summary>
    /// Returns whether the left version has lower or equal precedence than the right version.
    /// </summary>
    /// <param name="left">The left version.</param>
    /// <param name="right">The right version.</param>
    /// <returns>True when the left version has lower or equal precedence; otherwise, false.</returns>
    public static bool operator <=(SemVersion left, SemVersion right) => left.CompareTo(right) <= 0;

    /// <summary>
    /// Returns whether the left version has higher precedence than the right version.
    /// </summary>
    /// <param name="left">The left version.</param>
    /// <param name="right">The right version.</param>
    /// <returns>True when the left version has higher precedence; otherwise, false.</returns>
    public static bool operator >(SemVersion left, SemVersion right) => left.CompareTo(right) > 0;

    /// <summary>
    /// Returns whether the left version has higher or equal precedence than the right version.
    /// </summary>
    /// <param name="left">The left version.</param>
    /// <param name="right">The right version.</param>
    /// <returns>True when the left version has higher or equal precedence; otherwise, false.</returns>
    public static bool operator >=(SemVersion left, SemVersion right) => left.CompareTo(right) >= 0;

    private int ComparePrerelease(SemVersion other)
    {
        var length = Math.Min(_prereleaseIdentifiers.Length, other._prereleaseIdentifiers.Length);
        for (var i = 0; i < length; i++)
        {
            var left = _prereleaseIdentifiers[i];
            var right = other._prereleaseIdentifiers[i];
            var leftNumeric = IsAllDigits(left);
            var rightNumeric = IsAllDigits(right);

            if (leftNumeric && rightNumeric)
            {
                var result = CompareNumericIdentifier(left, right);
                if (result != 0)
                    return result;
                continue;
            }

            if (leftNumeric) return -1;
            if (rightNumeric) return 1;

            var lexical = string.CompareOrdinal(left, right);
            if (lexical != 0)
                return lexical;
        }

        return _prereleaseIdentifiers.Length.CompareTo(other._prereleaseIdentifiers.Length);
    }

    private static bool TryParseCoreNumber(string value, out int number)
    {
        number = 0;
        return IsValidNumericIdentifier(value) &&
               int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out number);
    }

    private static bool IsValidPrerelease(string value) =>
        IsValidDotSeparatedIdentifiers(value, allowNumericLeadingZeroes: false);

    private static bool IsValidBuild(string value) =>
        IsValidDotSeparatedIdentifiers(value, allowNumericLeadingZeroes: true);

    private static bool IsValidDotSeparatedIdentifiers(string value, bool allowNumericLeadingZeroes)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        foreach (var identifier in value.Split('.'))
        {
            if (identifier.Length == 0)
                return false;

            foreach (var c in identifier)
            {
                if (c is >= '0' and <= '9' or >= 'A' and <= 'Z' or >= 'a' and <= 'z' or '-')
                    continue;

                return false;
            }

            if (!allowNumericLeadingZeroes && IsAllDigits(identifier) && !IsValidNumericIdentifier(identifier))
                return false;
        }

        return true;
    }

    private static bool IsValidNumericIdentifier(string value)
    {
        if (value.Length == 0)
            return false;
        if (value.Length > 1 && value[0] == '0')
            return false;

        return IsAllDigits(value);
    }

    private static bool IsAllDigits(string value)
    {
        foreach (var c in value)
        {
            if (c is < '0' or > '9')
                return false;
        }

        return true;
    }

    private static int CompareNumericIdentifier(string left, string right)
    {
        if (left.Length != right.Length)
            return left.Length.CompareTo(right.Length);

        return string.CompareOrdinal(left, right);
    }
}