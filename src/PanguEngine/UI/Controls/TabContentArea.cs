namespace PanguEngine.UI.Controls;

/// <summary>
/// Hosts the content hosts of a <see cref="TabView"/>.
/// </summary>
internal sealed class TabContentArea : Region
{
    internal TabContentArea()
    {
        Classes.Add("tab-content-area");
    }

    internal void InsertHost(int index, TabContentHost host) =>
        Children.Insert(index, host);

    internal void RemoveHost(TabContentHost host) => Children.Remove(host);

    internal void MoveHost(int oldIndex, int newIndex) => Children.Move(oldIndex, newIndex);
}
