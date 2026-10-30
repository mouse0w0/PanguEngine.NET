using PanguEngine.ComponentModel;
using PanguEngine.UI.Styling;
using PanguEngine.UI.Templates;

namespace PanguEngine.UI.Controls;

/// <summary>
/// Represents a selectable tab header together with its associated page content.
/// </summary>
/// <remarks>
/// A tab item can belong to at most one <see cref="TabView"/> at a time. The header and content
/// slots hold data or elements with independent templates. Moving or reusing their roots externally
/// has undefined behavior; slot state is not reconciled. Moving or reusing internal control parts is also
/// unsupported; no rejection, synchronization, or recovery is guaranteed. A stable internal content host
/// retains the page while the item is not hosted by a view.
/// </remarks>
public sealed class TabItem : Control
{
    private const double HeaderGap = 4;

    /// <summary>Identifies the <see cref="Header"/> property.</summary>
    public static readonly DirectProperty<TabItem, object?> HeaderProperty =
        Property.RegisterDirect<TabItem, object?>(nameof(Header),
            static item => item.Header, static (item, value) => item.Header = value);

    /// <summary>Identifies the <see cref="Content"/> property.</summary>
    public static readonly DirectProperty<TabItem, object?> ContentProperty =
        Property.RegisterDirect<TabItem, object?>(nameof(Content),
            static item => item.Content, static (item, value) => item.Content = value);

    /// <summary>Identifies the <see cref="HeaderTemplate"/> property.</summary>
    public static readonly DirectProperty<TabItem, IDataTemplate?> HeaderTemplateProperty =
        Property.RegisterDirect<TabItem, IDataTemplate?>(nameof(HeaderTemplate),
            static item => item.HeaderTemplate, static (item, value) => item.HeaderTemplate = value);

    /// <summary>Identifies the <see cref="ContentTemplate"/> property.</summary>
    public static readonly DirectProperty<TabItem, IDataTemplate?> ContentTemplateProperty =
        Property.RegisterDirect<TabItem, IDataTemplate?>(nameof(ContentTemplate),
            static item => item.ContentTemplate, static (item, value) => item.ContentTemplate = value);

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
    private TabCloseButton? _closeButton;

    /// <summary>Initializes a focusable tab item with a stable internal content host.</summary>
    public TabItem()
    {
        _contentHost = new TabContentHost(this,
            (previous, value) => RaisePropertyChanged(ContentProperty, previous, value),
            (previous, value) => RaisePropertyChanged(ContentTemplateProperty, previous, value));
        _headerHost = new TabHeaderHost(this,
            (previous, value) => RaisePropertyChanged(HeaderProperty, previous, value),
            (previous, value) => RaisePropertyChanged(HeaderTemplateProperty, previous, value));
        Children.Add(_headerHost);
        ClipToBounds = true;
        Focusable = true;
        Subscribe(IsEnabledProperty, (_, _) => OwnerView?.OnItemStateChanged(this));
    }

    /// <summary>Gets or sets the data or element displayed in the tab header.</summary>
    /// <remarks>
    /// Moving or reusing the content root externally has undefined behavior; slot state is not reconciled.
    /// Updates are synchronous and cannot reenter this slot during its update or notifications,
    /// including binding feedback with a different reference. Failures after commit do not roll back.
    /// </remarks>
    public object? Header
    {
        get => _headerHost.Slot.Content;
        set => _headerHost.Slot.Content = value;
    }

    /// <summary>Gets or sets the data or element displayed as the tab page.</summary>
    /// <remarks>
    /// Moving or reusing the content root externally has undefined behavior; slot state is not reconciled.
    /// Updates are synchronous and cannot reenter this slot during its update or notifications,
    /// including binding feedback with a different reference. Failures after commit do not roll back.
    /// </remarks>
    public object? Content
    {
        get => _contentHost.Slot.Content;
        set => _contentHost.Slot.Content = value;
    }

    /// <summary>Gets or sets the explicit template for non-null header data.</summary>
    /// <remarks>Updates share the header's synchronous reentry and failure rules.</remarks>
    public IDataTemplate? HeaderTemplate
    {
        get => _headerHost.Slot.Template;
        set => _headerHost.Slot.Template = value;
    }

    /// <summary>Gets or sets the explicit template for non-null page data.</summary>
    /// <remarks>Updates share the page's synchronous reentry and failure rules.</remarks>
    public IDataTemplate? ContentTemplate
    {
        get => _contentHost.Slot.Template;
        set => _contentHost.Slot.Template = value;
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

    internal UIElement? HeaderRoot => _headerHost.Slot.Root;

    internal UIElement? ContentRoot => _contentHost.Slot.Root;

    internal void VerifySlotMutationAccess() => VerifyMutationAccess();

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
