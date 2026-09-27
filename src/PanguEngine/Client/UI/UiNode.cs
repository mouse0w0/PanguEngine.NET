using PanguEngine.ComponentModel;
using PanguEngine.Client.UI.Styling;

namespace PanguEngine.Client.UI;

/// <summary>
/// Provides the base property host for retained-mode UI nodes.
/// </summary>
/// <remarks>
/// Properties cannot be modified while the owning screen is generating drawing commands.
/// </remarks>
public abstract partial class UiNode : ObservableObject
{
    /// <summary>
    /// Initializes a UI node.
    /// </summary>
    protected UiNode()
    {
        Classes = new UiStyleClassCollection(this);
    }

    /// <inheritdoc />
    protected override T GetFallbackValue<T>(Property<T> property)
    {
        if (property.IsReadOnly)
            return property.DefaultValue;
        EnsureStyleSnapshot();
        if (_styleSnapshot is not null && _styleSnapshot.TryGetBoxedValue(property, out var style))
            return style is null ? default! : (T)style;
        return base.GetFallbackValue(property);
    }
}
