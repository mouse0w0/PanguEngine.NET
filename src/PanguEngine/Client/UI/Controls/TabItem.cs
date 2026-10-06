using PanguEngine.Client.UI.Styling;
using PanguEngine.ComponentModel;

namespace PanguEngine.Client.UI.Controls;

/// <summary>
/// Represents a selectable tab header together with its associated page content.
/// </summary>
/// <remarks>
/// A tab item can belong to at most one <see cref="TabView"/> at a time. The header and content
/// slots hold arbitrary nodes; assigning a node that already belongs elsewhere moves it and clears
/// the previous slot owner. A stable internal content host keeps content ownership even while the
/// item is not hosted by a view.
/// </remarks>
public sealed class TabItem : Control
{
    private static readonly UiPseudoClass SelectedPseudoClass = UiPseudoClass.Get("selected");
    private static readonly UiPseudoClass DraggingPseudoClass = UiPseudoClass.Get("dragging");

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
        UiCssRegistry.RegisterElement<TabItem>("TabItem");
    }

    private readonly TabContentHost _contentHost;
    private readonly TabHeaderHost _headerHost;
    private UiNode? _header;
    private UiNode? _content;
    private TabCloseButton? _closeButton;

    /// <summary>Initializes a focusable tab item with a stable internal content host.</summary>
    public TabItem()
    {
        _contentHost = new TabContentHost(this);
        _headerHost = new TabHeaderHost(this);
        Children.Add(_headerHost);
        ClipToBounds = true;
        Focusable = true;
    }

    /// <summary>Gets or sets the node displayed in the tab header, or null for an empty header.</summary>
    /// <remarks>
    /// The header cannot be this item, the content node, the internal content host, an ancestor, or
    /// another child of this item. Moving the header node out of this item through the tree API
    /// clears this slot.
    /// </remarks>
    public UiNode? Header
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

    /// <summary>Gets or sets the node displayed as the tab page content, or null for no content.</summary>
    /// <remarks>
    /// The content cannot be this item, the header node, the internal content host, or an ancestor
    /// of this item or its owning view. Moving the content node out of the host through the tree API
    /// clears this slot.
    /// </remarks>
    public UiNode? Content
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

    internal void NotifyContentDetached(UiNode child)
    {
        if (ReferenceEquals(_content, child))
            _content = null;
    }

    internal void NotifyHeaderDetached(UiNode child)
    {
        if (ReferenceEquals(_header, child))
            _header = null;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(PropertyChangedEventArgs eventArgs)
    {
        if (ReferenceEquals(eventArgs.Property, IsEnabledProperty))
        {
            OwnerView?.OnItemStateChanged(this);
        }

        base.OnPropertyChanged(eventArgs);
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

    private void ValidateHeaderAssignment(UiNode? value)
    {
        Screen?.VerifyTreeMutationAccess();
        if (value is null)
            return;
        value.Screen?.VerifyTreeMutationAccess();
        if (ReferenceEquals(value, this))
            throw new InvalidOperationException("A tab item cannot use itself as its header.");
        if (ReferenceEquals(value, _content))
            throw new InvalidOperationException("The header and content cannot reference the same node.");
        if (ReferenceEquals(value, _contentHost) || ReferenceEquals(value, _closeButton))
            throw new InvalidOperationException("An internal tab item node cannot be used as a header.");
        if (value.Parent is null && value.Screen is not null)
            throw new InvalidOperationException("A UI screen root must be cleared before it can be a tab header.");
        if (TabView.IsInternalPartNode(value))
            throw new InvalidOperationException("A tab view internal node cannot be used as a header.");

        for (UiNode? node = this; node is not null; node = node.Parent)
        {
            if (ReferenceEquals(node, value))
                throw new InvalidOperationException("Adding the node would create a parent cycle.");
        }

        if (ReferenceEquals(value.Parent, this))
            throw new InvalidOperationException("The node is already a child of this tab item.");
    }

    private void ValidateContentAssignment(UiNode? value)
    {
        Screen?.VerifyTreeMutationAccess();
        if (value is null)
            return;
        value.Screen?.VerifyTreeMutationAccess();
        if (ReferenceEquals(value, this))
            throw new InvalidOperationException("A tab item cannot use itself as its content.");
        if (ReferenceEquals(value, _header))
            throw new InvalidOperationException("The content and header cannot reference the same node.");
        if (ReferenceEquals(value, _contentHost) || ReferenceEquals(value, _closeButton))
            throw new InvalidOperationException("An internal tab item node cannot be used as content.");
        if (value.Parent is null && value.Screen is not null)
            throw new InvalidOperationException("A UI screen root must be cleared before it can be tab content.");
        if (TabView.IsInternalPartNode(value))
            throw new InvalidOperationException("A tab view internal node cannot be used as content.");

        for (UiNode? node = this; node is not null; node = node.Parent)
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

    private void RefreshSelectedPseudoClass() => SetPseudoClass(SelectedPseudoClass, IsSelected);

    private void RefreshDraggingPseudoClass() => SetPseudoClass(DraggingPseudoClass, IsDragging);
}
