namespace PanguEngine.Client.UI.Controls;

/// <summary>
/// Hosts the content hosts of a <see cref="TabView"/>.
/// </summary>
internal sealed class TabContentArea : Region
{
    private readonly TabView _owner;
    private bool _suppressExternalNotification;

    internal TabContentArea(TabView owner)
    {
        _owner = owner;
        Classes.Add("tab-content-area");
        Children.Changed += (_, change) =>
        {
            if (_suppressExternalNotification)
                return;
            foreach (var child in change.OldItems)
            {
                if (child is TabContentHost host && !ReferenceEquals(host.Parent, this))
                    _owner.OnContentHostRemoved(host);
            }
        };
    }

    internal void InsertHost(int index, TabContentHost host) =>
        Children.Insert(index, host);

    internal void RemoveHost(TabContentHost host)
    {
        _suppressExternalNotification = true;
        try
        {
            Children.Remove(host);
        }
        finally
        {
            _suppressExternalNotification = false;
        }
    }

    internal void MoveHost(int oldIndex, int newIndex) => Children.Move(oldIndex, newIndex);
}
