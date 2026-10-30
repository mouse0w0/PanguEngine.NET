using PanguEngine.UI.Templates;

namespace PanguEngine.UI.Controls;

internal sealed class TabHeaderHost : Control
{
    internal TabHeaderHost(TabItem item, Action<object?, object?> contentChanged,
        Action<IDataTemplate?, IDataTemplate?> templateChanged)
    {
        ClipToBounds = true;
        Classes.Add("tab-header-host");
        Slot = new ContentSlot(this, item, item.VerifySlotMutationAccess,
            contentChanged, templateChanged);
    }

    internal ContentSlot Slot { get; }
}

/// <summary>
/// Retains a stable host for a <see cref="TabItem"/> page.
/// </summary>
internal sealed class TabContentHost : Control
{
    internal TabContentHost(TabItem item, Action<object?, object?> contentChanged,
        Action<IDataTemplate?, IDataTemplate?> templateChanged)
    {
        Classes.Add("tab-content-host");
        Slot = new ContentSlot(this, item, item.VerifySlotMutationAccess,
            contentChanged, templateChanged);
    }

    internal ContentSlot Slot { get; }

    internal UIElement? Content => Slot.Root;
}
