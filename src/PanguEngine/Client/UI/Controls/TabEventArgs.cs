namespace PanguEngine.Client.UI.Controls;

/// <summary>
/// Provides data for a <see cref="TabView.SelectionChanged"/> notification.
/// </summary>
public sealed class TabSelectionChangedEventArgs : EventArgs
{
    internal TabSelectionChangedEventArgs(TabItem? oldItem, TabItem? newItem, int oldIndex, int newIndex)
    {
        OldItem = oldItem;
        NewItem = newItem;
        OldIndex = oldIndex;
        NewIndex = newIndex;
    }

    /// <summary>Gets the previously selected item, or null when nothing was selected.</summary>
    public TabItem? OldItem { get; }

    /// <summary>Gets the newly selected item, or null when the selection was cleared.</summary>
    public TabItem? NewItem { get; }

    /// <summary>Gets the index of the previously selected item, or -1 when nothing was selected.</summary>
    public int OldIndex { get; }

    /// <summary>Gets the index of the newly selected item, or -1 when the selection was cleared.</summary>
    public int NewIndex { get; }
}

/// <summary>
/// Provides data for a <see cref="TabView.TabCloseRequested"/> notification.
/// </summary>
public sealed class TabCloseRequestedEventArgs : EventArgs
{
    internal TabCloseRequestedEventArgs(TabItem item) => Item = item;

    /// <summary>Gets the tab item whose close was requested.</summary>
    public TabItem Item { get; }
}

/// <summary>
/// Provides data for a <see cref="TabView.TabReordered"/> notification.
/// </summary>
public sealed class TabReorderedEventArgs : EventArgs
{
    internal TabReorderedEventArgs(TabItem item, int oldIndex, int newIndex)
    {
        Item = item;
        OldIndex = oldIndex;
        NewIndex = newIndex;
    }

    /// <summary>Gets the tab item that moved.</summary>
    public TabItem Item { get; }

    /// <summary>Gets the index of the item before the move.</summary>
    public int OldIndex { get; }

    /// <summary>Gets the index of the item after the move.</summary>
    public int NewIndex { get; }
}
