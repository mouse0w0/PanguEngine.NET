using PanguEngine.Client.UI.Styling;
using PanguEngine.ComponentModel;

namespace PanguEngine.Client.UI.Controls;

/// <summary>
/// Provides a button that alternates between checked and unchecked states when activated.
/// </summary>
public sealed class ToggleButton : Button
{
    private static readonly UiPseudoClass CheckedPseudoClass = UiPseudoClass.Get("checked");

    /// <summary>
    /// Identifies the <see cref="IsChecked"/> property.
    /// </summary>
    public static readonly Property<bool> IsCheckedProperty =
        Property.Register<ToggleButton, bool>(
            nameof(IsChecked),
            false,
            onChanged: static (node, _, value) => node.SetPseudoClass(CheckedPseudoClass, value));

    static ToggleButton()
    {
        UiCssRegistry.RegisterElement<ToggleButton>("ToggleButton");
    }

    /// <summary>
    /// Gets or sets whether this button is checked.
    /// </summary>
    /// <remarks>Changing this property directly does not raise <see cref="Button.Click"/>.</remarks>
    public bool IsChecked
    {
        get => GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    /// <inheritdoc />
    protected override void OnClick()
    {
        IsChecked = !IsChecked;
        base.OnClick();
    }
}
