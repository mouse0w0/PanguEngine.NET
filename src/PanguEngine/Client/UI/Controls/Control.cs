using PanguEngine.Client.UI.Styling;

using PanguEngine.ComponentModel;

namespace PanguEngine.Client.UI.Controls;

/// <summary>
/// Provides the base class for interactive UI regions.
/// </summary>
public abstract class Control : Region
{
    private static readonly PropertyKey<bool> IsPressedPropertyKey =
        Property.RegisterReadOnly<Control, bool>(
            nameof(IsPressed),
            onChanged: static (node, _, _) => ((Control)node).RefreshPressedPseudoClass());

    /// <summary>
    /// Identifies the <see cref="IsPressed"/> property.
    /// </summary>
    public static readonly Property<bool> IsPressedProperty = IsPressedPropertyKey.Property;

    static Control()
    {
        UiCssRegistry.RegisterElement<Control>("Control");
    }

    /// <summary>
    /// Initializes a UI control.
    /// </summary>
    protected Control()
    {
    }

    /// <summary>
    /// Gets whether a left pointer press currently belongs to this control or its subtree.
    /// </summary>
    public bool IsPressed => GetValue(IsPressedProperty);

    internal void SetPressed(bool value)
    {
        if (value)
            SetValue(IsPressedPropertyKey, true);
        else
            ClearValue(IsPressedPropertyKey);
    }

    /// <summary>
    /// Gets whether any input source currently activates the built-in pressed pseudo class.
    /// </summary>
    protected virtual bool IsPressedPseudoClassActive => IsPressed;

    /// <summary>
    /// Refreshes the built-in pressed pseudo class from <see cref="IsPressedPseudoClassActive"/>.
    /// </summary>
    protected void RefreshPressedPseudoClass()
    {
        SetPseudoClass(UiPseudoClass.Pressed, IsPressedPseudoClassActive);
    }
}
