namespace PanguEngine.Client.UI;

/// <summary>
/// Provides a UI region whose child collection can be modified by callers.
/// </summary>
public class Panel : Region
{
    /// <summary>
    /// Initializes a UI panel.
    /// </summary>
    public Panel()
    {
        Children = new UiNodeCollection(this);
    }

    /// <summary>
    /// Gets the mutable collection of direct child nodes in drawing order.
    /// </summary>
    public new UiNodeCollection Children { get; }
}
