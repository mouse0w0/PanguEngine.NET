using PanguEngine.Collections;
using PanguEngine.ComponentModel;
using PanguEngine.Input;
using PanguEngine.UI.Input;
using PanguEngine.UI.Styling;

namespace PanguEngine.UI.Controls;

/// <summary>
/// Provides a checkable control with optional text or element content and an indeterminate state.
/// </summary>
public sealed class CheckBox : Control
{
    /// <summary>Identifies the <see cref="IsChecked"/> property.</summary>
    public static readonly Property<bool?> IsCheckedProperty =
        Property.Register<CheckBox, bool?>(nameof(IsChecked), false,
            onChanged: static (box, _, _) => box.SynchronizeCheckState());

    /// <summary>Identifies the <see cref="IsThreeState"/> property.</summary>
    public static readonly Property<bool> IsThreeStateProperty =
        Property.Register<CheckBox, bool>(nameof(IsThreeState));

    /// <summary>Identifies the <see cref="Content"/> property.</summary>
    public static readonly DirectProperty<CheckBox, object?> ContentProperty =
        Property.RegisterDirect<CheckBox, object?>(nameof(Content),
            static box => box.Content,
            static (box, value) => box.Content = value,
            onChanged: static (box, _, value) => box.SynchronizeContent(value));

    /// <summary>Identifies the <see cref="Spacing"/> property.</summary>
    public static readonly Property<double> SpacingProperty =
        Property.Register<CheckBox, double>(nameof(Spacing), 8,
            onChanged: static (box, _, _) => box.InvalidateMeasure(),
            validate: IsFiniteNonNegative,
            validationMessage: "Spacing must be a finite non-negative value.");

    private readonly Panel _box;
    private readonly Path _mark;
    private readonly Panel _contentPanel;
    private object? _content;
    private bool _spaceKeyDown;
    private int _focusVersion;

    private UIElement? ContentNode => _contentPanel.Children.Count == 0 ? null : _contentPanel.Children[0];

    static CheckBox()
    {
        CssRegistry.RegisterElement<CheckBox>("CheckBox");
        CssRegistry.RegisterProperty<CheckBox, double>("spacing", SpacingProperty, CssValueConverters.ParseLength);
    }

    /// <summary>Initializes a focusable check box with an empty label.</summary>
    public CheckBox()
    {
        Focusable = true;
        _box = new Panel { IsHitTestVisible = false };
        _box.Classes.Add("checkbox-box");
        _mark = new Path { IsHitTestVisible = false };
        _mark.Classes.Add("checkbox-mark");
        _box.Children.Add(_mark);
        _contentPanel = new Panel { IsHitTestVisible = false };
        _contentPanel.Classes.Add("checkbox-content");
        _contentPanel.Children.Changed += OnContentChildrenChanged;
        Children.Add(_box);
        Children.Add(_contentPanel);
        SynchronizeCheckState();
    }

    /// <summary>Gets or sets the checked state, or null for an indeterminate state.</summary>
    public bool? IsChecked
    {
        get => GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    /// <summary>Gets or sets whether user activation cycles through all three states.</summary>
    /// <remarks>Programmatic indeterminate values are accepted regardless of this setting.</remarks>
    public bool IsThreeState
    {
        get => GetValue(IsThreeStateProperty);
        set => SetValue(IsThreeStateProperty, value);
    }

    /// <summary>Gets or sets the label as an element, text, another object's text representation, or null.</summary>
    /// <remarks>
    /// An element moves from its previous owner and clears this slot when moved elsewhere.
    /// Assignments of the same content instance do not refresh text, and changes inside an ordinary object are not observed.
    /// </remarks>
    public object? Content
    {
        get => _content;
        set
        {
            if (value is UIElement node)
            {
                for (UIElement? ancestor = this; ancestor is not null; ancestor = ancestor.Parent)
                    if (ReferenceEquals(node, ancestor))
                        throw new ArgumentException("Content cannot be this check box or an ancestor.", nameof(value));
            }

            SetField(ContentProperty, ref _content, value);
        }
    }

    /// <summary>Gets or sets the logical-pixel gap between the box and a label that participates in layout.</summary>
    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    /// <summary>Occurs after pointer or keyboard activation updates the checked state.</summary>
    public event EventHandler? Click;

    /// <inheritdoc />
    protected override Size MeasureContent(Size availableSize)
    {
        _box.Measure(availableSize);
        var spacing = GetLayoutSpacing();
        var remainingWidth = double.IsPositiveInfinity(availableSize.Width)
            ? double.PositiveInfinity
            : Math.Max(0, availableSize.Width - _box.DesiredSize.Width - spacing);
        _contentPanel.Measure(new Size(remainingWidth, availableSize.Height));
        return new Size(
            _box.DesiredSize.Width + spacing + _contentPanel.DesiredSize.Width,
            Math.Max(_box.DesiredSize.Height, _contentPanel.DesiredSize.Height));
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Rect contentBounds)
    {
        var boxSize = _box.DesiredSize;
        _box.Arrange(new Rect(contentBounds.X,
            contentBounds.Y + (contentBounds.Height - boxSize.Height) / 2, boxSize));
        var x = contentBounds.X + boxSize.Width + GetLayoutSpacing();
        var contentHeight = _contentPanel.DesiredSize.Height;
        _contentPanel.Arrange(new Rect(x, contentBounds.Y + (contentBounds.Height - contentHeight) / 2,
            Math.Max(0, contentBounds.X + contentBounds.Width - x), contentHeight));
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerButtonEventArgs eventArgs)
    {
        base.OnPointerPressed(eventArgs);
        if (eventArgs.Button == MouseButton.Left)
            eventArgs.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerButtonEventArgs eventArgs)
    {
        base.OnPointerReleased(eventArgs);
        if (eventArgs.Button == MouseButton.Left)
            eventArgs.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerClicked(PointerButtonEventArgs eventArgs)
    {
        var scene = Scene;
        base.OnPointerClicked(eventArgs);
        if (eventArgs.Button != MouseButton.Left)
            return;
        eventArgs.Handled = true;
        if (CanActivate(scene))
            Activate();
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs eventArgs)
    {
        if (eventArgs.Key != Key.Space)
        {
            base.OnKeyDown(eventArgs);
            return;
        }

        if (!eventArgs.IsRepeat)
            SetSpaceKeyDown(IsEnabled && IsFocused);
        base.OnKeyDown(eventArgs);
        eventArgs.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnKeyUp(KeyEventArgs eventArgs)
    {
        if (eventArgs.Key != Key.Space)
        {
            base.OnKeyUp(eventArgs);
            return;
        }

        var activate = _spaceKeyDown;
        var scene = Scene;
        var focusVersion = _focusVersion;
        SetSpaceKeyDown(false);
        base.OnKeyUp(eventArgs);
        eventArgs.Handled = true;
        if (activate && IsFocused && focusVersion == _focusVersion && CanActivate(scene))
            Activate();
    }

    /// <inheritdoc />
    protected override void OnLostFocus(FocusChangedEventArgs eventArgs)
    {
        _focusVersion++;
        SetSpaceKeyDown(false);
        base.OnLostFocus(eventArgs);
    }

    /// <inheritdoc />
    protected override bool IsPressedPseudoClassActive => IsPressed || _spaceKeyDown;

    private void SynchronizeContent(object? value)
    {
        UIElement? node = value as UIElement;
        if (node is null && value is not null)
        {
            var text = value is string content ? content : value.ToString();
            if (!string.IsNullOrEmpty(text))
            {
                node = new Text { Content = text };
                node.Classes.Add("checkbox-text");
            }
        }

        if (ReferenceEquals(Content, value))
            SetContentNode(node, value);
    }

    private void SetContentNode(UIElement? node, object? expectedValue)
    {
        if (ReferenceEquals(ContentNode, node))
            return;
        if (ContentNode is { } previous)
            _contentPanel.Children.Remove(previous);
        if (!ReferenceEquals(Content, expectedValue))
            return;
        if (node is null)
            return;
        if (node.Parent is { } source)
            source.Children.Remove(node);
        else if (node.Scene is { } scene)
            scene.Root = null;
        if (!ReferenceEquals(Content, expectedValue))
            return;
        if (node.Parent is not null)
            throw new InvalidOperationException("The content node was reparented during removal notification.");
        _contentPanel.Children.Add(node);
    }

    private void OnContentChildrenChanged(object? sender, ListChangedEventArgs<UIElement> change)
    {
        foreach (var node in change.OldItems)
            if (!ReferenceEquals(node.Parent, _contentPanel) && ReferenceEquals(Content, node))
                Content = null;
    }

    private void SynchronizeCheckState()
    {
        var state = IsChecked;
        SetPseudoClass(PseudoClass.Checked, state == true);
        SetPseudoClass(PseudoClass.Indeterminate, state is null);
    }

    private double GetLayoutSpacing()
    {
        if (ContentNode is not { Visibility: not Visibility.Collapsed })
            return 0;
        var spacing = Spacing;
        var scene = Scene;
        return scene?.UseLayoutRounding ?? true
            ? LayoutHelper.RoundLayoutValue(spacing, scene?.Scale ?? 1)
            : spacing;
    }

    private void SetSpaceKeyDown(bool value)
    {
        if (_spaceKeyDown == value)
            return;
        _spaceKeyDown = value;
        RefreshPressedPseudoClass();
    }

    private void Activate()
    {
        IsChecked = IsChecked switch
        {
            false => true,
            true when IsThreeState => null,
            _ => false
        };
        Click?.Invoke(this, EventArgs.Empty);
    }

    private bool CanActivate(UIScene? scene)
    {
        if (scene is null || !scene.IsOpen() || !ReferenceEquals(Scene, scene))
            return false;
        for (UIElement? node = this; node is not null; node = node.Parent)
        {
            if (!node.IsEnabled || node.Visibility != Visibility.Visible)
                return false;
            if (ReferenceEquals(node, scene.Root))
                return true;
        }

        return false;
    }
}
