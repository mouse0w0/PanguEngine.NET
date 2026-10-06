namespace PanguEngine.Client.UI.Controls;

internal sealed class TabHeaderHost : Control
{
    private readonly TabItem _item;

    internal TabHeaderHost(TabItem item)
    {
        _item = item;
        ClipToBounds = true;
        Classes.Add("tab-header-host");
        Children.Changed += (_, change) =>
        {
            foreach (var child in change.OldItems)
            {
                if (!ReferenceEquals(child.Parent, this))
                    _item.NotifyHeaderDetached(child);
            }
        };
    }

    internal void SetHeader(UiNode? header)
    {
        if (Children.Count > 0)
            Children.Remove(Children[0]);
        if (header is not null)
            Children.Add(header);
    }
}

/// <summary>
/// Hosts the single content node of a <see cref="TabItem"/> and reports external content migration.
/// </summary>
internal sealed class TabContentHost : Control
{
    private readonly TabItem _item;

    internal TabContentHost(TabItem item)
    {
        _item = item;
        Classes.Add("tab-content-host");
        Children.Changed += (_, change) =>
        {
            foreach (var child in change.OldItems)
            {
                if (!ReferenceEquals(child.Parent, this))
                    _item.NotifyContentDetached(child);
            }
        };
    }

    internal UiNode? Content => Children.Count == 0 ? null : Children[0];

    internal void SetContent(UiNode? content)
    {
        for (var index = Children.Count - 1; index >= 0; index--)
        {
            var child = Children[index];
            if (!ReferenceEquals(child, content))
                Children.Remove(child);
        }

        if (content is not null && !ReferenceEquals(content.Parent, this))
            Children.Add(content);
    }

    /// <inheritdoc />
    protected override Size MeasureContent(Size availableSize)
    {
        var content = Content;
        if (content is null)
            return Size.Zero;
        content.Measure(availableSize);
        return content.DesiredSize;
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Rect contentBounds)
    {
        Content?.Arrange(contentBounds);
    }
}
