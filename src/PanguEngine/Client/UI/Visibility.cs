namespace PanguEngine.Client.UI;

/// <summary>
/// Specifies how a UI node participates in layout, drawing, and hit testing.
/// </summary>
public enum Visibility
{
    /// <summary>The node participates in layout and is visible.</summary>
    Visible,

    /// <summary>The node participates in layout but is not drawn or hit tested.</summary>
    Hidden,

    /// <summary>The node does not occupy layout space and is not drawn or hit tested.</summary>
    Collapsed
}
