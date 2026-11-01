using PanguEngine.UI.Input;
using PanguEngine.UI.Styling;
using PanguEngine.UI.Templates;

namespace PanguEngine.UI.Controls;

internal sealed class ItemContainer : Control
{
    private readonly Action<ItemContainer, PointerButtonEventArgs> _clicked;

    static ItemContainer() => CssRegistry.RegisterElement<ItemContainer>("ItemContainer");

    internal ItemContainer(Action<ItemContainer, PointerButtonEventArgs> clicked)
    {
        _clicked = clicked;
        ClipToBounds = true;
        Margin = Thickness.Zero;
        Focusable = false;
    }

    internal int Index { get; set; } = -1;
    internal object? Data { get; set; }
    internal IDataTemplate? Template { get; set; }
    internal UIElement? ContentRoot { get; private set; }

    internal void SetRoot(UIElement? root)
    {
        if (ReferenceEquals(ContentRoot, root))
            return;
        if (ContentRoot is { } previous)
            Children.Remove(previous);
        ContentRoot = root;
        if (root is not null)
            Children.Add(root);
    }

    internal void SetStates(bool selected, bool current)
    {
        SetPseudoClass(PseudoClass.Selected, selected);
        SetPseudoClass(PseudoClass.Current, current);
    }

    protected override void OnPointerClicked(PointerButtonEventArgs eventArgs)
    {
        base.OnPointerClicked(eventArgs);
        if (!eventArgs.Handled)
            _clicked(this, eventArgs);
    }
}