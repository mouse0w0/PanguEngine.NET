using System.Collections;
using PanguEngine.Client.UI.Styling;

namespace PanguEngine.Client.UI.Controls;

/// <summary>Provides an immutable ordered sequence of grid row or column definitions.</summary>
/// <remarks>Replace the corresponding Grid property to update a sequence or its sizing limits.</remarks>
public sealed class GridDefinitions : IReadOnlyList<GridDefinition>, IEquatable<GridDefinitions>
{
    private readonly GridDefinition[] _definitions;

    /// <summary>Initializes a sequence by copying its definitions.</summary>
    /// <param name="definitions">The ordered row or column definitions.</param>
    public GridDefinitions(params GridDefinition[] definitions) => _definitions = [.. definitions];

    /// <summary>Gets an empty sequence, representing the grid's default single proportional track.</summary>
    public static GridDefinitions Empty { get; } = new();

    /// <summary>Gets the number of explicitly defined rows or columns.</summary>
    public int Count => _definitions.Length;

    /// <summary>Gets the definition at a zero-based index.</summary>
    /// <param name="index">The definition index.</param>
    public GridDefinition this[int index] => _definitions[index];

    /// <summary>Parses comma-separated fixed lengths, Auto values, and star weights.</summary>
    /// <param name="value">The definition list, or none for an empty list.</param>
    /// <returns>The immutable definition sequence.</returns>
    /// <exception cref="FormatException">Thrown when the text contains an unsupported definition.</exception>
    public static GridDefinitions Parse(string value)
    {
        if (value.Trim().Equals("none", StringComparison.OrdinalIgnoreCase))
            return Empty;

        var parts = value.Split(',');
        var definitions = new GridDefinition[parts.Length];
        for (var index = 0; index < parts.Length; index++)
        {
            var part = parts[index].Trim();
            if (part.Equals("auto", StringComparison.OrdinalIgnoreCase))
            {
                definitions[index] = GridLength.Auto;
                continue;
            }

            var star = part.EndsWith('*');
            var number = star
                ? part.Length == 1 ? 1 : UiCssValueConverters.ParseNumber(part[..^1])
                : UiCssValueConverters.ParseLength(part);
            if (number < 0)
                throw new FormatException("Grid lengths and weights cannot be negative.");
            definitions[index] = new GridLength(number, star ? GridUnitType.Star : GridUnitType.Pixel);
        }
        return new GridDefinitions(definitions);
    }

    /// <summary>Determines whether another sequence contains the same definitions in the same order.</summary>
    /// <param name="other">The sequence to compare.</param>
    /// <returns>Whether the sequences are equal.</returns>
    public bool Equals(GridDefinitions? other) =>
        other is not null && _definitions.AsSpan().SequenceEqual(other._definitions);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is GridDefinitions other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var definition in _definitions)
            hash.Add(definition);
        return hash.ToHashCode();
    }

    /// <summary>Enumerates the definitions in row or column order.</summary>
    /// <returns>The definition enumerator.</returns>
    public IEnumerator<GridDefinition> GetEnumerator() => ((IEnumerable<GridDefinition>)_definitions).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
