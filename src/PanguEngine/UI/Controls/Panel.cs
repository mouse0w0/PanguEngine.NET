using PanguEngine.UI.Styling;

namespace PanguEngine.UI.Controls;

/// <summary>
/// Provides a UI region whose child collection can be modified by callers.
/// </summary>
public class Panel : Region
{
    static Panel()
    {
        CssRegistry.RegisterElement<Panel>("Panel");
    }

    /// <summary>
    /// Gets the mutable collection of direct child elements in drawing order.
    /// </summary>
    public new UIElementList Children => base.Children;
}