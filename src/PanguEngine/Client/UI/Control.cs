namespace PanguEngine.Client.UI;

/// <summary>
/// Provides the base class for interactive UI regions.
/// </summary>
public abstract class Control : Region
{
    private static readonly UiPropertyKey<bool> IsPressedPropertyKey =
        UiProperty.RegisterReadOnly<Control, bool>(
            nameof(IsPressed),
            invalidation: UiPropertyInvalidation.Render);

    /// <summary>
    /// Identifies the <see cref="IsPressed"/> property.
    /// </summary>
    public static readonly UiProperty<bool> IsPressedProperty = IsPressedPropertyKey.Property;

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
}
