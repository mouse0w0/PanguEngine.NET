using PanguEngine.ComponentModel;
using PanguEngine.UI.Styling;

namespace PanguEngine.UI.Controls;

/// <summary>
/// Represents a selectable tab header together with its associated page content.
/// </summary>
/// <remarks>
/// A tab item can belong to at most one <see cref="TabView"/> at a time. The header and content
/// slots hold arbitrary elements; assigning an element that already belongs elsewhere moves it and clears
/// the previous slot owner. A stable internal content host keeps content ownership even while the
/// item is not hosted by a view.
/// </remarks>
public sealed class TabItem : Control
{
    private const double HeaderGap = 4;

    /// <summary>Identifies the <see cref="IsClosable"/> property.</summary>
    public static readonly Property<bool> IsClosableProperty =
        Property.Register<TabItem, bool>(
            nameof(IsClosable),
            false,
            onChanged: static (node, _, _) =>
            {
                node.InvalidateMeasure();
                node.SynchronizeCloseButton();
            });

    private static readonly PropertyKey<bool> IsSelectedPropertyKey =
        Property.RegisterReadOnly<TabItem, bool>(
            nameof(IsSelected),
            false,
            onChanged: static (node, _, _) => ((TabItem)node).RefreshSelectedPseudoClass());

    private static readonly PropertyKey<bool> IsDraggingPropertyKey =
        Property.RegisterReadOnly<TabItem, bool>(
            nameof(IsDragging),
            false,
            onChanged: static (node, _, _) => ((TabItem)node).RefreshDraggingPseudoClass());

    /// <summary>Identifies the <see cref="IsSelected"/> property.</summary>
    public static readonly Property<bool> IsSelectedProperty = IsSelectedPropertyKey.Property;

    /// <summary>Identifies the <see cref="IsDragging"/> property.</summary>
    public static readonly Property<bool> IsDraggingProperty = IsDraggingPropertyKey.Property;

    static TabItem()
    {
        CssRegistry.RegisterElement<TabItem>("TabItem");
    }

    private readonly TabContentHost _contentHost;
    private readonly TabHeaderHost _headerHost;
    private UIElement? _header;
    private UIElement? _content;
    private TabCloseButton? _closeButton;

    /// <summary>Initializes a focusable tab item with a stable internal content host.</summary>
    public TabItem()
    {
        _contentHost = new TabContentHost(this);
        _headerHost = new TabHeaderHost(this);
        Children.Add(_headerHost);
        ClipToBounds = true;
        Focusable = true;
        Subscribe(IsEnabledProperty, (_, _) => OwnerView?.OnItemStateChanged(this));
    }

    /// <summary>Gets or sets the element displayed in the tab header, or null for an empty header.</summary>
    /// <remarks>
    /// The header cannot be this item, the content element, or an ancestor. Moving the header element
    /// out through the tree API clears this slot. Moving or reusing internal control parts is unsupported;
    /// no rejection, synchronization, or recovery is guaranteed for those operations.
    /// </remarks>
    public UIElement? Header
    {
        get => _header;
        set
        {
            if (ReferenceEquals(_header, value))
                return;
            ValidateHeaderAssignment(value);

            _headerHost.SetHeader(value);
            _header = value;
        }
    }

    /// <summary>Gets or sets the element displayed as the tab page content, or null for no content.</summary>
    /// <remarks>
    /// The content cannot be this item, the header element, or an ancestor of this item or its owning view.
    /// Moving the content element out through the tree API clears this slot. Moving or reusing internal
    /// control parts is unsupported; no rejection, synchronization, or recovery is guaranteed for those operations.
    /// </remarks>
    public UIElement? Content
    {
        get => _content;
        set
        {
            if (ReferenceEquals(_content, value))
                return;
            ValidateContentAssignment(value);
            _contentHost.SetContent(value);
            _content = value;
        }
    }

    /// <summary>Gets or sets whether the tab displays an independent close button.</summary>
    public bool IsClosable
    {
        get => GetValue(IsClosableProperty);
        set => SetValue(IsClosableProperty, value);
    }

    /// <summary>Gets whether the owning view currently selects this tab.</summary>
    public bool IsSelected => GetValue(IsSelectedProperty);

    /// <summary>Gets whether this tab is the active reorder drag source.</summary>
    public bool IsDragging => GetValue(IsDraggingProperty);

    internal TabView? OwnerView { get; set; }

    internal TabContentHost ContentHost => _contentHost;

    internal void SetSelected(bool value)
    {
        if (value)
            SetValue(IsSelectedPropertyKey, true);
        else
            ClearValue(IsSelectedPropertyKey);
    }

    internal void SetDragging(bool value)
    {
        if (value)
            SetValue(IsDraggingPropertyKey, true);
        else
            ClearValue(IsDraggingPropertyKey);
    }

    internal void NotifyCloseRequested() => OwnerView?.RequestClose(this);

    internal void NotifyContentDetached(UIElement child)
    {
        if (ReferenceEquals(_content, child))
            _content = null;
    }

    internal void NotifyHeaderDetached(UIElement child)
    {
        if (ReferenceEquals(_header, child))
            _header = null;
    }

    /// <inheritdoc />
    protected override Size MeasureContent(Size availableSize)
    {
        var closeButton = _closeButton;
        closeButton?.Measure(availableSize);
        var closeWidth = closeButton?.DesiredSize.Width ?? 0;
        var closeHeight = closeButton?.DesiredSize.Height ?? 0;

        var headerAvailableWidth = double.IsPositiveInfinity(availableSize.Width)
            ? double.PositiveInfinity
            : Math.Max(0, availableSize.Width - closeWidth - (closeButton is null ? 0 : HeaderGap));
        _headerHost.Measure(new Size(headerAvailableWidth, availableSize.Height));
        var width = _headerHost.DesiredSize.Width + (closeButton is null ? 0 : closeWidth + HeaderGap);
        var height = Math.Max(_headerHost.DesiredSize.Height, closeHeight);
        return new Size(width, height);
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Rect contentBounds)
    {
        var closeButton = _closeButton;
        if (closeButton is null)
        {
            _headerHost.Arrange(contentBounds);
            return;
        }

        var closeWidth = closeButton.DesiredSize.Width;
        var closeHeight = closeButton.DesiredSize.Height;
        closeButton.Arrange(new Rect(
            contentBounds.X + contentBounds.Width - closeWidth,
            contentBounds.Y + (contentBounds.Height - closeHeight) / 2,
            closeWidth,
            closeHeight));

        var headerWidth = Math.Max(0, contentBounds.Width - closeWidth - HeaderGap);
        _headerHost.Arrange(new Rect(contentBounds.X, contentBounds.Y, headerWidth, contentBounds.Height));
    }

    private void ValidateHeaderAssignment(UIElement? value)
    {
        Scene?.VerifyTreeMutationAccess();
        if (value is null)
            return;
        value.Scene?.VerifyTreeMutationAccess();
        if (ReferenceEquals(value, this))
            throw new InvalidOperationException("A tab item cannot use itself as its header.");
        if (ReferenceEquals(value, _content))
            throw new InvalidOperationException("The header and content cannot reference the same node.");
        if (value.Parent is null && value.Scene is not null)
            throw new InvalidOperationException("A UI scene root must be cleared before it can be a tab header.");

        for (UIElement? node = this; node is not null; node = node.Parent)
        {
            if (ReferenceEquals(node, value))
                throw new InvalidOperationException("Adding the node would create a parent cycle.");
        }
    }

    private void ValidateContentAssignment(UIElement? value)
    {
        Scene?.VerifyTreeMutationAccess();
        if (value is null)
            return;
        value.Scene?.VerifyTreeMutationAccess();
        if (ReferenceEquals(value, this))
            throw new InvalidOperationException("A tab item cannot use itself as its content.");
        if (ReferenceEquals(value, _header))
            throw new InvalidOperationException("The content and header cannot reference the same node.");
        if (value.Parent is null && value.Scene is not null)
            throw new InvalidOperationException("A UI scene root must be cleared before it can be tab content.");

        for (UIElement? node = this; node is not null; node = node.Parent)
        {
            if (ReferenceEquals(node, value))
                throw new InvalidOperationException("Adding the node would create a parent cycle.");
        }
    }

    private void SynchronizeCloseButton()
    {
        if (IsClosable)
        {
            if (_closeButton is not null)
                return;
            _closeButton = new TabCloseButton(this);
            Children.Add(_closeButton);
            return;
        }

        if (_closeButton is null)
            return;
        Children.Remove(_closeButton);

        _closeButton = null;
    }

    private void RefreshSelectedPseudoClass() => SetPseudoClass(PseudoClass.Selected, IsSelected);

    private void RefreshDraggingPseudoClass() => SetPseudoClass(PseudoClass.Dragging, IsDragging);
}
