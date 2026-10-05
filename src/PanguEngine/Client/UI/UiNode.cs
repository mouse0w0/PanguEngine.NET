using PanguEngine.ComponentModel;
using PanguEngine.Collections;

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
        Classes = new ObservableSet<string>(StringComparer.Ordinal);
        Classes.Changed += (_, _) => InvalidateStyle();
    }

    /// <inheritdoc />
    protected override T GetFallbackValue<T>(Property<T> property)
    {
        if (property.IsReadOnly)
            return property.DefaultValue;
        if (_styleSnapshot is not null && _styleSnapshot.TryGetBoxedValue(property, out var style))
            return style is null ? default! : (T)style;
        return base.GetFallbackValue(property);
    }
}
