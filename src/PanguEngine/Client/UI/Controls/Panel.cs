using PanguEngine.Client.UI.Styling;

namespace PanguEngine.Client.UI.Controls;

/// <summary>
/// Provides a UI region whose child collection can be modified by callers.
/// </summary>
public class Panel : Region
{
    static Panel()
    {
        UiCssRegistry.RegisterElement<Panel>("Panel");
    }

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
