namespace PanguEngine.Client.UI;

/// <summary>
/// Provides owner access to a read-only registered UI property.
/// </summary>
/// <typeparam name="T">The property value type.</typeparam>
public sealed class UiPropertyKey<T>
{
    internal UiPropertyKey(UiProperty<T> property)
    {
        Property = property;
    }

    /// <summary>Gets the read-only property descriptor associated with this key.</summary>
    public UiProperty<T> Property { get; }
}
