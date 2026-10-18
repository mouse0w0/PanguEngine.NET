namespace PanguEngine.Client.UI.Styling;

/// <summary>Identifies a named pseudo class shared by controls and style selectors.</summary>
public sealed class PseudoClass
{
    private static readonly Lock Sync = new();
    private static readonly Dictionary<string, PseudoClass> Instances = new(StringComparer.Ordinal);

    private PseudoClass(string name)
    {
        Name = name;
    }

    /// <summary>Gets the normalized lower-case name without a leading colon.</summary>
    public string Name { get; }

    /// <summary>Gets the built-in structural pseudo class that matches nodes without a parent.</summary>
    /// <remarks>The name <c>root</c> is reserved. Its matching state cannot be changed manually.</remarks>
    public static PseudoClass Root { get; } = Get("root");

    /// <summary>Gets the structural pseudo class that matches the first sibling, including a parentless node.</summary>
    public static PseudoClass FirstChild { get; } = Get("first-child");

    /// <summary>Gets the structural pseudo class that matches the last sibling, including a parentless node.</summary>
    public static PseudoClass LastChild { get; } = Get("last-child");

    /// <summary>Gets the structural pseudo class that matches a node without other siblings.</summary>
    public static PseudoClass OnlyChild { get; } = Get("only-child");

    /// <summary>Gets the structural pseudo class that matches a node without child nodes, regardless of content.</summary>
    public static PseudoClass Empty { get; } = Get("empty");

    /// <summary>Gets whether this identifier constrains sibling position or count.</summary>
    internal bool IsChildIndexed => this == FirstChild || this == LastChild || this == OnlyChild;

    /// <summary>Gets whether this identifier is matched automatically from tree structure.</summary>
    internal bool IsStructural => this == Root || this == Empty || IsChildIndexed;

    /// <summary>Gets the built-in hover pseudo class.</summary>
    public static PseudoClass Hover { get; } = Get("hover");

    /// <summary>Gets the built-in focus pseudo class.</summary>
    public static PseudoClass Focus { get; } = Get("focus");

    /// <summary>Gets the built-in pseudo class for nodes that have focus themselves or in their descendants.</summary>
    public static PseudoClass FocusWithin { get; } = Get("focus-within");

    /// <summary>Gets the built-in disabled pseudo class.</summary>
    public static PseudoClass Disabled { get; } = Get("disabled");

    /// <summary>Gets the built-in pressed pseudo class.</summary>
    public static PseudoClass Pressed { get; } = Get("pressed");

    /// <summary>Gets the built-in checked pseudo class.</summary>
    public static PseudoClass Checked { get; } = Get("checked");

    /// <summary>Gets the built-in indeterminate pseudo class.</summary>
    public static PseudoClass Indeterminate { get; } = Get("indeterminate");

    /// <summary>Gets the built-in read-only pseudo class.</summary>
    public static PseudoClass ReadOnly { get; } = Get("readonly");

    /// <summary>Gets the built-in horizontal orientation pseudo class.</summary>
    public static PseudoClass Horizontal { get; } = Get("horizontal");

    /// <summary>Gets the built-in vertical orientation pseudo class.</summary>
    public static PseudoClass Vertical { get; } = Get("vertical");

    /// <summary>Gets the built-in pseudo class for controls without a value.</summary>
    public static PseudoClass NoValue { get; } = Get("no-value");

    /// <summary>Gets the built-in selected pseudo class.</summary>
    public static PseudoClass Selected { get; } = Get("selected");

    /// <summary>Gets the built-in dragging pseudo class.</summary>
    public static PseudoClass Dragging { get; } = Get("dragging");

    /// <summary>Gets or creates the shared identifier for a case-insensitive pseudo-class name.</summary>
    /// <param name="name">An ASCII identifier without a leading colon.</param>
    /// <returns>The same identifier for all equivalent names, including concurrent requests.</returns>
    /// <exception cref="ArgumentException">Thrown when the name is not a valid ASCII identifier.</exception>
    public static PseudoClass Get(string name)
    {
        StyleIdentifier.ThrowIfInvalid(name, nameof(name));
        var normalized = name.ToLowerInvariant();
        lock (Sync)
        {
            if (Instances.TryGetValue(normalized, out var existing))
                return existing;

            var created = new PseudoClass(normalized);
            Instances.Add(normalized, created);
            return created;
        }
    }
}
