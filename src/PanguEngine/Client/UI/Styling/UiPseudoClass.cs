namespace PanguEngine.Client.UI.Styling;

/// <summary>Identifies a named pseudo class shared by controls and style selectors.</summary>
public sealed class UiPseudoClass
{
    private static readonly Lock Sync = new();
    private static readonly Dictionary<string, UiPseudoClass> Instances = new(StringComparer.Ordinal);

    private UiPseudoClass(string name)
    {
        Name = name;
    }

    /// <summary>Gets the normalized lower-case name without a leading colon.</summary>
    public string Name { get; }

    /// <summary>Gets the built-in structural pseudo class that matches nodes without a parent.</summary>
    /// <remarks>The name <c>root</c> is reserved. Its matching state cannot be changed manually.</remarks>
    public static UiPseudoClass Root { get; } = Get("root");

    /// <summary>Gets the built-in hover pseudo class.</summary>
    public static UiPseudoClass Hover { get; } = Get("hover");

    /// <summary>Gets the built-in focus pseudo class.</summary>
    public static UiPseudoClass Focus { get; } = Get("focus");

    /// <summary>Gets the built-in disabled pseudo class.</summary>
    public static UiPseudoClass Disabled { get; } = Get("disabled");

    /// <summary>Gets the built-in pressed pseudo class.</summary>
    public static UiPseudoClass Pressed { get; } = Get("pressed");

    /// <summary>Gets or creates the shared identifier for a case-insensitive pseudo-class name.</summary>
    /// <param name="name">An ASCII identifier without a leading colon.</param>
    /// <returns>The same identifier for all equivalent names, including concurrent requests.</returns>
    /// <exception cref="ArgumentException">Thrown when the name is not a valid ASCII identifier.</exception>
    public static UiPseudoClass Get(string name)
    {
        UiStyleIdentifier.ThrowIfInvalid(name, nameof(name));
        var normalized = name.ToLowerInvariant();
        lock (Sync)
        {
            if (Instances.TryGetValue(normalized, out var existing))
                return existing;

            var created = new UiPseudoClass(normalized);
            Instances.Add(normalized, created);
            return created;
        }
    }
}