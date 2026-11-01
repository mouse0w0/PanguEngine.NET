using PanguEngine.UI.Input;
using PanguEngine.UI.Styling;
using PanguEngine.UI.Templates;

namespace PanguEngine.UI.Controls;

internal sealed class TreeItemContainer : Control
{
    private const double DisclosureWidth = 20;
    private const double DisclosureSpacing = 6;
    private readonly Button _expand;
    private readonly Action<TreeItemContainer, PointerButtonEventArgs> _clicked;
    private double _indentation;

    static TreeItemContainer() => CssRegistry.RegisterElement<TreeItemContainer>("TreeItemContainer");

    internal TreeItemContainer(Action<TreeItemContainer, PointerButtonEventArgs> clicked,
        Action<TreeItemContainer> toggle)
    {
        _clicked = clicked;
        ClipToBounds = true;
        Focusable = false;
        Margin = Thickness.Zero;
        var arrow = new Path { IsHitTestVisible = false };
        arrow.Classes.Add("tree-disclosure-arrow");
        _expand = new Button { Focusable = false, Content = arrow };
        _expand.Classes.Add("tree-disclosure");
        _expand.Click += (_, _) => toggle(this);
        Children.Add(_expand);
    }

    internal object? Data { get; set; }
    internal IDataTemplate? Template { get; set; }
    internal UIElement? ContentRoot { get; private set; }
    internal double DisclosureStart => BorderThickness.Left + Padding.Left + _indentation;
    internal double ContentStart => DisclosureStart + DisclosureWidth + DisclosureSpacing;

    internal double Indentation
    {
        get => _indentation;
        set
        {
            if (_indentation == value)
                return;
            _indentation = value;
            InvalidateMeasure();
        }
    }

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

    internal void SetStates(bool selected, bool current, bool expanded, bool canExpand)
    {
        SetPseudoClass(PseudoClass.Selected, selected);
        SetPseudoClass(PseudoClass.Current, current);
        SetPseudoClass(PseudoClass.Expanded, expanded);
        _expand.Visibility = canExpand ? Visibility.Visible : Visibility.Hidden;
    }

    protected override Size MeasureContent(Size availableSize)
    {
        _expand.Measure(new Size(DisclosureWidth, availableSize.Height));
        var natural = new Size(double.PositiveInfinity, availableSize.Height);
        ContentRoot?.Measure(natural);
        var content = ContentRoot?.DesiredSize ?? Size.Zero;
        return new Size(_indentation + DisclosureWidth + DisclosureSpacing + content.Width,
            Math.Max(content.Height, _expand.DesiredSize.Height));
    }

    protected override void ArrangeContent(Rect bounds)
    {
        var x = bounds.X + _indentation;
        _expand.Arrange(new Rect(x, bounds.Y, DisclosureWidth, bounds.Height));
        x += DisclosureWidth + DisclosureSpacing;
        if (ContentRoot is { } root)
        {
            root.Arrange(new Rect(x, bounds.Y, root.DesiredSize.Width, bounds.Height));
        }
    }

    protected override void OnPointerClicked(PointerButtonEventArgs args)
    {
        base.OnPointerClicked(args);
        if (!args.Handled)
            _clicked(this, args);
    }
}